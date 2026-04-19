import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { get } from 'svelte/store';

async function loadStore() {
	const mod = await import('$lib/stores/discoverFeedStore');
	return mod;
}

/** Flush all pending microtasks (resolved promises) without advancing timers. */
async function flushMicrotasks() {
	await Promise.resolve();
	await Promise.resolve();
	await Promise.resolve();
}

describe('discoverFeedStore', () => {
	beforeEach(() => {
		vi.useFakeTimers();
		vi.resetModules();
		global.fetch = vi.fn();
	});

	afterEach(() => {
		vi.useRealTimers();
		vi.restoreAllMocks();
	});

	it('starts with empty tracks and default state', async () => {
		const { discoverFeedStore } = await loadStore();
		const state = get(discoverFeedStore);
		expect(state.tracks).toEqual([]);
		expect(state.totalCount).toBe(0);
		expect(state.loadingComplete).toBe(false);
		expect(state.lastRefreshedAt).toBeNull();
		expect(state.progress).toBe(0);
		expect(state.error).toBeNull();
	});

	it('fetches from /api/feed/discover on start()', async () => {
		const { discoverFeedStore } = await loadStore();

		(global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
			ok: true,
			json: async () => ({
				tracks: [
					{
						title: 'Test Track',
						artistName: 'Artist',
						artworkUrl: null,
						genre: 'Electronic',
						tags: [],
						likesCount: 10,
						playbackCount: 100,
						repostsCount: 5,
						commentCount: 2,
						createdAt: '2026-01-01T00:00:00Z',
						permalinkUrl: 'https://soundcloud.com/test',
						duration: 180000,
						access: 'playable',
						activityType: 'track',
						appearedAt: '2026-01-01T00:00:00Z',
						reposterCount: 3,
						reposters: ['user1', 'user2', 'user3'],
						lastRepostedAt: '2026-01-01T00:00:00Z',
					},
				],
				totalCount: 1,
				loadingComplete: true,
				lastRefreshedAt: '2026-01-01T00:00:00Z',
				progress: 1.0,
			}),
		});

		discoverFeedStore.start();
		await flushMicrotasks();

		expect(fetch).toHaveBeenCalledWith('/api/feed/discover', { credentials: 'include' });
		expect(fetch).toHaveBeenCalledTimes(1);
		const state = get(discoverFeedStore);
		expect(state.tracks).toHaveLength(1);
		expect(state.tracks[0].title).toBe('Test Track');
		expect(state.tracks[0].reposterCount).toBe(3);
		expect(state.totalCount).toBe(1);
		expect(state.loadingComplete).toBe(true);
		expect(state.error).toBeNull();

		discoverFeedStore.stop();
	});

	it('polls again after 60 seconds', async () => {
		const { discoverFeedStore } = await loadStore();

		const mockResponse = () => ({
			ok: true,
			json: async () => ({
				tracks: [],
				totalCount: 0,
				loadingComplete: true,
				lastRefreshedAt: null,
				progress: 1.0,
			}),
		});

		(global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue(mockResponse());

		discoverFeedStore.start();
		await flushMicrotasks();
		expect(fetch).toHaveBeenCalledTimes(1);

		await vi.advanceTimersByTimeAsync(60_000);
		expect(fetch).toHaveBeenCalledTimes(2);

		await vi.advanceTimersByTimeAsync(60_000);
		expect(fetch).toHaveBeenCalledTimes(3);

		discoverFeedStore.stop();
	});

	it('stop() halts polling', async () => {
		const { discoverFeedStore } = await loadStore();

		(global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
			ok: true,
			json: async () => ({
				tracks: [],
				totalCount: 0,
				loadingComplete: true,
				lastRefreshedAt: null,
				progress: 1.0,
			}),
		});

		discoverFeedStore.start();
		await flushMicrotasks();
		discoverFeedStore.stop();

		const callsAfterStop = (fetch as ReturnType<typeof vi.fn>).mock.calls.length;
		await vi.advanceTimersByTimeAsync(120_000);
		expect(fetch).toHaveBeenCalledTimes(callsAfterStop);
	});

	it('sets error state on failed fetch', async () => {
		const { discoverFeedStore } = await loadStore();

		(global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
			ok: false,
			status: 500,
			json: async () => ({}),
		});

		discoverFeedStore.start();
		await flushMicrotasks();

		const state = get(discoverFeedStore);
		expect(state.error).toBe('HTTP 500');

		discoverFeedStore.stop();
	});

	it('refresh() returns { enqueued: true } on success and re-fetches', async () => {
		const { discoverFeedStore } = await loadStore();

		const feedResponse = {
			tracks: [],
			totalCount: 0,
			loadingComplete: true,
			lastRefreshedAt: '2026-01-01T00:00:00Z',
			progress: 1.0,
		};

		(global.fetch as ReturnType<typeof vi.fn>)
			.mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({}) }) // POST /refresh
			.mockResolvedValueOnce({ ok: true, json: async () => feedResponse }); // GET /discover

		const result = await discoverFeedStore.refresh();

		expect(result.enqueued).toBe(true);
		expect(result.retryAfterSec).toBeUndefined();
		expect(fetch).toHaveBeenCalledWith('/api/feed/discover/refresh', {
			method: 'POST',
			credentials: 'include',
		});
		expect(fetch).toHaveBeenCalledWith('/api/feed/discover', { credentials: 'include' });
	});

	it('refresh() returns { enqueued: false, retryAfterSec } on 429', async () => {
		const { discoverFeedStore } = await loadStore();

		(global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
			ok: false,
			status: 429,
			json: async () => ({ retryAfterSec: 87 }),
		});

		const result = await discoverFeedStore.refresh();

		expect(result.enqueued).toBe(false);
		expect(result.retryAfterSec).toBe(87);
	});

	it('refresh() returns { enqueued: false, error } on non-429 error', async () => {
		const { discoverFeedStore } = await loadStore();

		(global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
			ok: false,
			status: 503,
			json: async () => ({}),
		});

		const result = await discoverFeedStore.refresh();

		expect(result.enqueued).toBe(false);
		expect(result.error).toBe('HTTP 503');
	});

	it('stop() without start() is a no-op', async () => {
		const { discoverFeedStore } = await loadStore();
		// Should not throw
		discoverFeedStore.stop();
		discoverFeedStore.stop();
	});

	it('tick error with non-Error thrown value falls back to String(e)', async () => {
		const { discoverFeedStore } = await loadStore();

		(global.fetch as ReturnType<typeof vi.fn>).mockRejectedValueOnce('plain string');

		discoverFeedStore.start();
		await flushMicrotasks();

		expect(get(discoverFeedStore).error).toBe('plain string');
		discoverFeedStore.stop();
	});

	it('refresh() with non-Error thrown value falls back to String(e)', async () => {
		const { discoverFeedStore } = await loadStore();

		(global.fetch as ReturnType<typeof vi.fn>).mockRejectedValueOnce('boom');

		const result = await discoverFeedStore.refresh();

		expect(result.enqueued).toBe(false);
		expect(result.error).toBe('boom');
	});

	it('start() is idempotent — calling twice does not double-poll', async () => {
		const { discoverFeedStore } = await loadStore();

		(global.fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
			ok: true,
			json: async () => ({
				tracks: [],
				totalCount: 0,
				loadingComplete: true,
				lastRefreshedAt: null,
				progress: 1.0,
			}),
		});

		discoverFeedStore.start();
		discoverFeedStore.start(); // second call should be a no-op
		await flushMicrotasks();

		// Only one initial fetch, not two
		expect(fetch).toHaveBeenCalledTimes(1);

		await vi.advanceTimersByTimeAsync(60_000);
		// Only one poll tick, not two
		expect(fetch).toHaveBeenCalledTimes(2);

		discoverFeedStore.stop();
	});
});
