import { writable } from 'svelte/store';
import type { FeedTrack } from '$lib/types';

export const feedTracks = writable<FeedTrack[]>([]);
export const loadingComplete = writable(false);
export const totalCount = writable(0);
