import { writable } from 'svelte/store';
import type { SortBy, TimeRange, TimeField } from '$lib/types';

export const sortBy = writable<SortBy>('likes');
export const timeRange = writable<TimeRange>('24h');
export const selectedGenres = writable<string[]>([]);

/** Duration filter bounds in milliseconds. null means no limit. */
export const durationMin = writable<number | null>(null);
export const durationMax = writable<number | null>(null);
export const timeField = writable<TimeField>('feed');
