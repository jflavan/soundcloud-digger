import { derived } from 'svelte/store';
import { discoverFeedStore } from './discoverFeedStore';
import {
	discoverSortBy,
	timeRange,
	selectedGenres,
	excludedGenres,
	durationMin,
	durationMax,
	timeField,
} from './filterStore';
import type { DiscoverTrack, DiscoverSortBy, TimeRange, TimeField } from '$lib/types';

const TIME_RANGE_MS: Record<TimeRange, number> = {
	'24h': 24 * 60 * 60 * 1000,
	'7d': 7 * 24 * 60 * 60 * 1000,
	'30d': 30 * 24 * 60 * 60 * 1000,
	all: Infinity,
};

export function filterAndSortDiscover(
	tracks: DiscoverTrack[],
	sort: DiscoverSortBy,
	range: TimeRange,
	genres: string[],
	durMin: number | null,
	durMax: number | null,
	field: TimeField = 'feed',
	excluded: string[] = []
): DiscoverTrack[] {
	const now = Date.now();
	const cutoff = TIME_RANGE_MS[range];

	let filtered = tracks;

	if (range !== 'all') {
		filtered = filtered.filter((t) => {
			const dateStr = field === 'uploaded' ? t.createdAt : t.lastRepostedAt;
			return now - new Date(dateStr).getTime() <= cutoff;
		});
	}

	if (genres.length > 0) {
		filtered = filtered.filter((t) => t.genre !== null && genres.includes(t.genre));
	}

	if (excluded.length > 0) {
		filtered = filtered.filter((t) => t.genre === null || !excluded.includes(t.genre));
	}

	if (durMin !== null) {
		filtered = filtered.filter((t) => t.duration >= durMin);
	}

	if (durMax !== null) {
		filtered = filtered.filter((t) => t.duration <= durMax);
	}

	// Deduplicate by permalinkUrl
	const seen = new Set<string>();
	filtered = filtered.filter((t) => {
		const key = t.permalinkUrl ?? `${t.title}-${t.artistName}`;
		if (seen.has(key)) return false;
		seen.add(key);
		return true;
	});

	const sorted = [...filtered];
	if (sort === 'reposterCount') {
		sorted.sort((a, b) => b.reposterCount - a.reposterCount);
	} else if (sort === 'likes') {
		sorted.sort((a, b) => b.likesCount - a.likesCount);
	} else if (sort === 'plays') {
		sorted.sort((a, b) => b.playbackCount - a.playbackCount);
	} else if (sort === 'reposts') {
		sorted.sort((a, b) => b.repostsCount - a.repostsCount);
	} else if (sort === 'comments') {
		sorted.sort((a, b) => b.commentCount - a.commentCount);
	} else {
		sorted.sort(
			(a, b) => new Date(b.lastRepostedAt).getTime() - new Date(a.lastRepostedAt).getTime()
		);
	}

	return sorted;
}

export const filteredDiscover = derived(
	[
		discoverFeedStore,
		discoverSortBy,
		timeRange,
		selectedGenres,
		excludedGenres,
		durationMin,
		durationMax,
		timeField,
	],
	([$discover, $sortBy, $timeRange, $genres, $excluded, $durMin, $durMax, $timeField]) =>
		filterAndSortDiscover(
			$discover.tracks,
			$sortBy,
			$timeRange,
			$genres,
			$durMin,
			$durMax,
			$timeField,
			$excluded
		)
);

export const availableDiscoverGenres = derived(discoverFeedStore, ($discover) => {
	const genres = new Set<string>();
	for (const track of $discover.tracks) {
		if (track.genre) genres.add(track.genre);
	}
	return [...genres].sort();
});
