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
		repostsCount: 50,
		commentCount: 10,
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

		const result = filterAndSort(tracks, 'likes', 'all', [], null, null);
		expect(result.map((t) => t.title)).toEqual(['High', 'Mid', 'Low']);
	});

	it('sorts by plays descending', () => {
		const tracks = [
			makeTrack({ title: 'Low', playbackCount: 100 }),
			makeTrack({ title: 'High', playbackCount: 10000 }),
			makeTrack({ title: 'Mid', playbackCount: 1000 }),
		];

		const result = filterAndSort(tracks, 'plays', 'all', [], null, null);
		expect(result.map((t) => t.title)).toEqual(['High', 'Mid', 'Low']);
	});

	it('sorts by reposts descending', () => {
		const tracks = [
			makeTrack({ title: 'Low', repostsCount: 5 }),
			makeTrack({ title: 'High', repostsCount: 500 }),
			makeTrack({ title: 'Mid', repostsCount: 50 }),
		];

		const result = filterAndSort(tracks, 'reposts', 'all', [], null, null);
		expect(result.map((t) => t.title)).toEqual(['High', 'Mid', 'Low']);
	});

	it('sorts by comments descending', () => {
		const tracks = [
			makeTrack({ title: 'Low', commentCount: 1 }),
			makeTrack({ title: 'High', commentCount: 100 }),
			makeTrack({ title: 'Mid', commentCount: 10 }),
		];

		const result = filterAndSort(tracks, 'comments', 'all', [], null, null);
		expect(result.map((t) => t.title)).toEqual(['High', 'Mid', 'Low']);
	});

	it('sorts by date descending', () => {
		const tracks = [
			makeTrack({ title: 'Old', createdAt: '2026-01-01T00:00:00Z' }),
			makeTrack({ title: 'New', createdAt: '2026-03-13T00:00:00Z' }),
			makeTrack({ title: 'Mid', createdAt: '2026-02-01T00:00:00Z' }),
		];

		const result = filterAndSort(tracks, 'date', 'all', [], null, null);
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

		const result = filterAndSort(tracks, 'likes', '24h', [], null, null);
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

		const result = filterAndSort(tracks, 'likes', '7d', [], null, null);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('Recent');
	});

	it('filters by genre', () => {
		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic' }),
			makeTrack({ title: 'B', genre: 'Hip-hop' }),
			makeTrack({ title: 'C', genre: 'Electronic' }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', ['Electronic'], null, null);
		expect(result).toHaveLength(2);
		expect(result.every((t) => t.genre === 'Electronic')).toBe(true);
	});

	it('multiple genre filters use OR logic', () => {
		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic' }),
			makeTrack({ title: 'B', genre: 'Hip-hop' }),
			makeTrack({ title: 'C', genre: 'Ambient' }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', ['Electronic', 'Ambient'], null, null);
		expect(result).toHaveLength(2);
	});

	it('empty genre filter shows all tracks', () => {
		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic' }),
			makeTrack({ title: 'B', genre: 'Hip-hop' }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', [], null, null);
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

		const result = filterAndSort(tracks, 'likes', '24h', ['Electronic'], null, null);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('A');
	});

	it('filters by minimum duration', () => {
		const tracks = [
			makeTrack({ title: 'Short', duration: 60_000 }),
			makeTrack({ title: 'Long', duration: 300_000 }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', [], 120_000, null);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('Long');
	});

	it('filters by maximum duration', () => {
		const tracks = [
			makeTrack({ title: 'Short', duration: 60_000 }),
			makeTrack({ title: 'Long', duration: 300_000 }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', [], null, 120_000);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('Short');
	});

	it('filters by duration range', () => {
		const tracks = [
			makeTrack({ title: 'Short', duration: 30_000 }),
			makeTrack({ title: 'Mid', duration: 180_000 }),
			makeTrack({ title: 'Long', duration: 600_000 }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', [], 60_000, 300_000);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('Mid');
	});

	it('null duration bounds apply no filter', () => {
		const tracks = [
			makeTrack({ title: 'A', duration: 30_000 }),
			makeTrack({ title: 'B', duration: 600_000 }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', [], null, null);
		expect(result).toHaveLength(2);
	});
});
