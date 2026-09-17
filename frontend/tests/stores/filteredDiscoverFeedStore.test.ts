import { describe, it, expect, vi, afterEach } from 'vitest';
import { get } from 'svelte/store';
import { filterAndSortDiscover, hiddenUnplayableDiscoverCount } from '$lib/stores/filteredDiscoverFeedStore';
import { discoverFeedStore } from '$lib/stores/discoverFeedStore';
import { hideUnplayable } from '$lib/stores/filterStore';
import type { DiscoverTrack } from '$lib/types';

function makeTrack(opts: Partial<DiscoverTrack> = {}): DiscoverTrack {
	return {
		title: 't',
		artistName: 'a',
		artworkUrl: '',
		genre: null,
		tags: [],
		likesCount: 0,
		playbackCount: 0,
		repostsCount: 0,
		commentCount: 0,
		createdAt: '2026-04-10T00:00:00Z',
		permalinkUrl: 'url',
		duration: 180_000,
		access: 'playable',
		activityType: 'track-repost',
		appearedAt: '2026-04-10T00:00:00Z',
		reposterCount: 1,
		reposters: [],
		lastRepostedAt: '2026-04-10T00:00:00Z',
		...opts,
	} as DiscoverTrack;
}

describe('filterAndSortDiscover', () => {
	it('sorts by reposterCount desc', () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'a', reposterCount: 1 }),
			makeTrack({ permalinkUrl: 'b', reposterCount: 3 }),
			makeTrack({ permalinkUrl: 'c', reposterCount: 2 }),
		];
		const out = filterAndSortDiscover(tracks, 'reposterCount', 'all', [], null, null);
		expect(out.map((t) => t.permalinkUrl)).toEqual(['b', 'c', 'a']);
	});

	it('sorts by lastRepostedAt desc by default', () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'x', lastRepostedAt: '2026-04-10T00:00:00Z' }),
			makeTrack({ permalinkUrl: 'y', lastRepostedAt: '2026-04-15T00:00:00Z' }),
		];
		const out = filterAndSortDiscover(tracks, 'date', 'all', [], null, null);
		expect(out.map((t) => t.permalinkUrl)).toEqual(['y', 'x']);
	});

	it('filters by genre include', () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'a', genre: 'Electronic' }),
			makeTrack({ permalinkUrl: 'b', genre: 'Rock' }),
		];
		const out = filterAndSortDiscover(tracks, 'date', 'all', ['Electronic'], null, null);
		expect(out.map((t) => t.permalinkUrl)).toEqual(['a']);
	});

	it('filters by duration bounds', () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'short', duration: 60_000 }),
			makeTrack({ permalinkUrl: 'ok', duration: 300_000 }),
			makeTrack({ permalinkUrl: 'long', duration: 900_000 }),
		];
		const out = filterAndSortDiscover(tracks, 'date', 'all', [], 120_000, 600_000);
		expect(out.map((t) => t.permalinkUrl)).toEqual(['ok']);
	});

	it('dedupes by permalinkUrl', () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'a', reposterCount: 1 }),
			makeTrack({ permalinkUrl: 'a', reposterCount: 2 }),
		];
		const out = filterAndSortDiscover(tracks, 'reposterCount', 'all', [], null, null);
		expect(out).toHaveLength(1);
	});

	it('sorts by likes, plays, reposts, and comments desc', () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'a', likesCount: 1, playbackCount: 3, repostsCount: 2, commentCount: 1 }),
			makeTrack({ permalinkUrl: 'b', likesCount: 3, playbackCount: 1, repostsCount: 3, commentCount: 2 }),
			makeTrack({ permalinkUrl: 'c', likesCount: 2, playbackCount: 2, repostsCount: 1, commentCount: 3 }),
		];
		const ids = (sort: 'likes' | 'plays' | 'reposts' | 'comments') =>
			filterAndSortDiscover(tracks, sort, 'all', [], null, null).map((t) => t.permalinkUrl);
		expect(ids('likes')).toEqual(['b', 'c', 'a']);
		expect(ids('plays')).toEqual(['a', 'c', 'b']);
		expect(ids('reposts')).toEqual(['b', 'a', 'c']);
		expect(ids('comments')).toEqual(['c', 'b', 'a']);
	});

	it('filters by time range using lastRepostedAt by default', () => {
		const now = Date.now();
		const hoursAgo = (h: number) => new Date(now - h * 60 * 60 * 1000).toISOString();
		const tracks = [
			makeTrack({ permalinkUrl: 'recent', lastRepostedAt: hoursAgo(1), createdAt: hoursAgo(500) }),
			makeTrack({ permalinkUrl: 'old', lastRepostedAt: hoursAgo(48), createdAt: hoursAgo(1) }),
		];
		const out = filterAndSortDiscover(tracks, 'date', '24h', [], null, null);
		expect(out.map((t) => t.permalinkUrl)).toEqual(['recent']);
	});

	it('filters by time range using createdAt when timeField is uploaded', () => {
		const now = Date.now();
		const daysAgo = (d: number) => new Date(now - d * 24 * 60 * 60 * 1000).toISOString();
		const tracks = [
			makeTrack({ permalinkUrl: 'fresh', lastRepostedAt: daysAgo(30), createdAt: daysAgo(1) }),
			makeTrack({ permalinkUrl: 'stale', lastRepostedAt: daysAgo(1), createdAt: daysAgo(30) }),
		];
		const out = filterAndSortDiscover(tracks, 'date', '7d', [], null, null, 'uploaded');
		expect(out.map((t) => t.permalinkUrl)).toEqual(['fresh']);
	});

	it('excludes tracks with excluded genres but keeps null-genre tracks', () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'a', genre: 'Electronic' }),
			makeTrack({ permalinkUrl: 'b', genre: 'Rock' }),
			makeTrack({ permalinkUrl: 'c', genre: null }),
		];
		const out = filterAndSortDiscover(tracks, 'date', 'all', [], null, null, 'feed', ['Rock']);
		expect(out.map((t) => t.permalinkUrl)).toEqual(['a', 'c']);
	});

	it('exclusion wins over inclusion for the same genre', () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'a', genre: 'Electronic' }),
			makeTrack({ permalinkUrl: 'b', genre: 'Rock' }),
		];
		const out = filterAndSortDiscover(tracks, 'date', 'all', ['Electronic', 'Rock'], null, null, 'feed', ['Rock']);
		expect(out.map((t) => t.permalinkUrl)).toEqual(['a']);
	});

	it('dedupes by title and artist when permalinkUrl is missing', () => {
		const tracks = [
			makeTrack({ permalinkUrl: null as unknown as string, title: 'Same', artistName: 'Artist' }),
			makeTrack({ permalinkUrl: null as unknown as string, title: 'Same', artistName: 'Artist' }),
			makeTrack({ permalinkUrl: null as unknown as string, title: 'Other', artistName: 'Artist' }),
		];
		const out = filterAndSortDiscover(tracks, 'date', 'all', [], null, null);
		expect(out.map((t) => t.title)).toEqual(['Same', 'Other']);
	});

	it('hides Go+-only (non-playable) tracks when hideUnplayable is set', () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'free', access: 'playable' }),
			makeTrack({ permalinkUrl: 'goplus', access: 'preview' }),
		];
		const out = filterAndSortDiscover(tracks, 'date', 'all', [], null, null, 'feed', [], true);
		expect(out.map((t) => t.permalinkUrl)).toEqual(['free']);
	});

	it('keeps non-playable tracks by default', () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'free', access: 'playable' }),
			makeTrack({ permalinkUrl: 'goplus', access: 'preview' }),
		];
		expect(filterAndSortDiscover(tracks, 'date', 'all', [], null, null)).toHaveLength(2);
	});
});

describe('hiddenUnplayableDiscoverCount', () => {
	afterEach(() => {
		discoverFeedStore.stop();
		vi.unstubAllGlobals();
		hideUnplayable.set(true);
	});

	it('counts non-playable tracks in the discover feed while hiding is on', async () => {
		const tracks = [
			makeTrack({ permalinkUrl: 'a', access: 'playable' }),
			makeTrack({ permalinkUrl: 'b', access: 'preview' }),
		];
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: async () => ({ tracks, totalCount: 2, loadingComplete: true, lastRefreshedAt: null, progress: 1 }),
		}));
		hideUnplayable.set(true);
		discoverFeedStore.start();
		await vi.waitFor(() => expect(get(hiddenUnplayableDiscoverCount)).toBe(1));

		hideUnplayable.set(false);
		expect(get(hiddenUnplayableDiscoverCount)).toBe(0);
	});
});
