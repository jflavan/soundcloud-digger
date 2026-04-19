import { describe, it, expect } from 'vitest';
import { filterAndSortDiscover } from '$lib/stores/filteredDiscoverFeedStore';
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
});
