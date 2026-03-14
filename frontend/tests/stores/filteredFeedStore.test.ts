import { describe, it, expect } from 'vitest';
import { filterAndSort } from '$lib/stores/filteredFeedStore';
import type { FeedTrack, SortBy, TimeRange } from '$lib/types';

function makeTrack(overrides: Partial<FeedTrack> = {}): FeedTrack {
	return {
		title: 'Test Track',
		artistName: 'Artist',
		artworkUrl: null,
		genre: 'Electronic',
		tags: [],
		likesCount: 100,
		playbackCount: 500,
		createdAt: new Date().toISOString(),
		permalinkUrl: null,
		duration: 180000,
		access: 'playable',
		activityType: 'track',
		appearedAt: new Date().toISOString(),
		...overrides,
	};
}

describe('filterAndSort', () => {
	it('sorts by likes descending', () => {
		const tracks = [
			makeTrack({ title: 'Low', likesCount: 10 }),
			makeTrack({ title: 'High', likesCount: 1000 }),
			makeTrack({ title: 'Mid', likesCount: 100 }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', []);
		expect(result.map((t) => t.title)).toEqual(['High', 'Mid', 'Low']);
	});

	it('sorts by date descending', () => {
		const tracks = [
			makeTrack({ title: 'Old', createdAt: '2026-01-01T00:00:00Z' }),
			makeTrack({ title: 'New', createdAt: '2026-03-13T00:00:00Z' }),
			makeTrack({ title: 'Mid', createdAt: '2026-02-01T00:00:00Z' }),
		];

		const result = filterAndSort(tracks, 'date', 'all', []);
		expect(result.map((t) => t.title)).toEqual(['New', 'Mid', 'Old']);
	});

	it('filters by time range 24h', () => {
		const now = new Date();
		const yesterday = new Date(now.getTime() - 12 * 60 * 60 * 1000);
		const twoDaysAgo = new Date(now.getTime() - 48 * 60 * 60 * 1000);

		const tracks = [
			makeTrack({ title: 'Recent', appearedAt: yesterday.toISOString() }),
			makeTrack({ title: 'Old', appearedAt: twoDaysAgo.toISOString() }),
		];

		const result = filterAndSort(tracks, 'likes', '24h', []);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('Recent');
	});

	it('filters by time range 7d', () => {
		const now = new Date();
		const threeDaysAgo = new Date(now.getTime() - 3 * 24 * 60 * 60 * 1000);
		const tenDaysAgo = new Date(now.getTime() - 10 * 24 * 60 * 60 * 1000);

		const tracks = [
			makeTrack({ title: 'Recent', appearedAt: threeDaysAgo.toISOString() }),
			makeTrack({ title: 'Old', appearedAt: tenDaysAgo.toISOString() }),
		];

		const result = filterAndSort(tracks, 'likes', '7d', []);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('Recent');
	});

	it('filters by genre', () => {
		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic' }),
			makeTrack({ title: 'B', genre: 'Hip-hop' }),
			makeTrack({ title: 'C', genre: 'Electronic' }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', ['Electronic']);
		expect(result).toHaveLength(2);
		expect(result.every((t) => t.genre === 'Electronic')).toBe(true);
	});

	it('multiple genre filters use OR logic', () => {
		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic' }),
			makeTrack({ title: 'B', genre: 'Hip-hop' }),
			makeTrack({ title: 'C', genre: 'Ambient' }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', ['Electronic', 'Ambient']);
		expect(result).toHaveLength(2);
	});

	it('empty genre filter shows all tracks', () => {
		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic' }),
			makeTrack({ title: 'B', genre: 'Hip-hop' }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', []);
		expect(result).toHaveLength(2);
	});

	it('combines time range and genre filters', () => {
		const now = new Date();
		const recent = new Date(now.getTime() - 12 * 60 * 60 * 1000);
		const old = new Date(now.getTime() - 48 * 60 * 60 * 1000);

		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic', appearedAt: recent.toISOString() }),
			makeTrack({ title: 'B', genre: 'Hip-hop', appearedAt: recent.toISOString() }),
			makeTrack({ title: 'C', genre: 'Electronic', appearedAt: old.toISOString() }),
		];

		const result = filterAndSort(tracks, 'likes', '24h', ['Electronic']);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('A');
	});
});
