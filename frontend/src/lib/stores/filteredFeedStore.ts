import { derived } from 'svelte/store';
import { feedTracks } from './feedStore';
import { sortBy, timeRange, selectedGenres } from './filterStore';
import type { FeedTrack, SortBy, TimeRange } from '$lib/types';

const TIME_RANGE_MS: Record<TimeRange, number> = {
	'24h': 24 * 60 * 60 * 1000,
	'7d': 7 * 24 * 60 * 60 * 1000,
	'30d': 30 * 24 * 60 * 60 * 1000,
	all: Infinity,
};

export function filterAndSort(
	tracks: FeedTrack[],
	sort: SortBy,
	range: TimeRange,
	genres: string[]
): FeedTrack[] {
	const now = Date.now();
	const cutoff = TIME_RANGE_MS[range];

	let filtered = tracks;

	if (range !== 'all') {
		filtered = filtered.filter((t) => now - new Date(t.appearedAt).getTime() <= cutoff);
	}

	if (genres.length > 0) {
		filtered = filtered.filter((t) => t.genre !== null && genres.includes(t.genre));
	}

	const sorted = [...filtered];
	if (sort === 'likes') {
		sorted.sort((a, b) => b.likesCount - a.likesCount);
	} else if (sort === 'plays') {
		sorted.sort((a, b) => b.playbackCount - a.playbackCount);
	} else if (sort === 'reposts') {
		sorted.sort((a, b) => b.repostsCount - a.repostsCount);
	} else if (sort === 'comments') {
		sorted.sort((a, b) => b.commentCount - a.commentCount);
	} else {
		sorted.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
	}

	return sorted;
}

export const filteredFeed = derived(
	[feedTracks, sortBy, timeRange, selectedGenres],
	([$tracks, $sortBy, $timeRange, $genres]) => filterAndSort($tracks, $sortBy, $timeRange, $genres)
);

export const availableGenres = derived(feedTracks, ($tracks) => {
	const genres = new Set<string>();
	for (const track of $tracks) {
		if (track.genre) genres.add(track.genre);
	}
	return [...genres].sort();
});
