import { describe, it, expect, beforeEach, vi } from 'vitest';
import { get } from 'svelte/store';

async function loadStore() {
	const mod = await import('$lib/stores/feedSource');
	return mod;
}

describe('feedSource', () => {
	beforeEach(() => {
		vi.resetModules();
		window.history.replaceState({}, '', '/feed');
	});

	it('defaults to "feed"', async () => {
		const { feedSource } = await loadStore();
		expect(get(feedSource)).toBe('feed');
	});

	it('initializes from ?source=discover', async () => {
		window.history.replaceState({}, '', '/feed?source=discover');
		const { feedSource } = await loadStore();
		expect(get(feedSource)).toBe('discover');
	});

	it('falls back to "feed" on invalid param', async () => {
		window.history.replaceState({}, '', '/feed?source=bogus');
		const { feedSource } = await loadStore();
		expect(get(feedSource)).toBe('feed');
	});

	it('writes back to URL on change', async () => {
		const { feedSource } = await loadStore();
		feedSource.set('discover');
		expect(window.location.search).toContain('source=discover');
	});

	it('removes the param when setting back to feed', async () => {
		const { feedSource } = await loadStore();
		feedSource.set('discover');
		feedSource.set('feed');
		expect(window.location.search).not.toContain('source=');
	});
});
