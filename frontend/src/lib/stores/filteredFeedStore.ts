import { derived } from 'svelte/store';
import { feedTracks } from './feedStore';
import { sortBy, timeRange, selectedGenres, excludedGenres, durationMin, durationMax, timeField, hideUnplayable } from './filterStore';
import { isPlayable, countUnplayable } from '$lib/utils/playability';
import type { FeedTrack, SortBy, TimeRange, TimeField } from '$lib/types';

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
	genres: string[],
	durMin: number | null,
	durMax: number | null,
	field: TimeField = 'feed',
	excluded: string[] = [],
	hideNonPlayable = false
): FeedTrack[] {
	const now = Date.now();
	const cutoff = TIME_RANGE_MS[range];

	let filtered = tracks;

	if (range !== 'all') {
		filtered = filtered.filter((t) => {
			const dateStr = field === 'uploaded' ? t.createdAt : t.appearedAt;
			return now - new Date(dateStr).getTime() <= cutoff;
		});
	}

	if (genres.length > 0) {
		filtered = filtered.filter((t) => t.genre !== null && genres.includes(t.genre));
	}

	if (excluded.length > 0) {
		filtered = filtered.filter((t) => t.genre === null || !excluded.includes(t.genre));
	}

	if (hideNonPlayable) {
		filtered = filtered.filter(isPlayable);
	}

	if (durMin !== null) {
		filtered = filtered.filter((t) => t.duration >= durMin);
	}

	if (durMax !== null) {
		filtered = filtered.filter((t) => t.duration <= durMax);
	}

	// Deduplicate by permalinkUrl (same track can appear multiple times in feed)
	const seen = new Set<string>();
	filtered = filtered.filter((t) => {
		const key = t.permalinkUrl ?? `${t.title}-${t.artistName}`;
		if (seen.has(key)) return false;
		seen.add(key);
		return true;
	});

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
	[feedTracks, sortBy, timeRange, selectedGenres, excludedGenres, durationMin, durationMax, timeField, hideUnplayable],
	([$tracks, $sortBy, $timeRange, $genres, $excluded, $durMin, $durMax, $timeField, $hide]) =>
		filterAndSort($tracks, $sortBy, $timeRange, $genres, $durMin, $durMax, $timeField, $excluded, $hide)
);

/** How many feed tracks the hideUnplayable filter is currently removing. */
export const hiddenUnplayableFeedCount = derived(
	[feedTracks, hideUnplayable],
	([$tracks, $hide]) => ($hide ? countUnplayable($tracks) : 0)
);

export const availableGenres = derived(feedTracks, ($tracks) => {
	const genres = new Set<string>();
	for (const track of $tracks) {
		if (track.genre) genres.add(track.genre);
	}
	return [...genres].sort();
});
