import { writable } from 'svelte/store';
import type { SortBy, DiscoverSortBy, TimeRange, TimeField } from '$lib/types';

export const sortBy = writable<SortBy>('likes');
export const timeRange = writable<TimeRange>('24h');
export const selectedGenres = writable<string[]>([]);
export const excludedGenres = writable<string[]>([]);

/** Duration filter bounds in milliseconds. null means no limit. */
export const durationMin = writable<number | null>(null);
export const durationMax = writable<number | null>(null);
export const timeField = writable<TimeField>('feed');
export const discoverSortBy = writable<DiscoverSortBy>('reposterCount');

/**
 * Hide preview-only tracks (see utils/playability). Off by default: the player
 * can play their ~30s preview in-app, so they're shown with a badge instead.
 */
export const hideUnplayable = writable<boolean>(false);
