import { writable } from 'svelte/store';
import type { DiscoverResponse, DiscoverTrack } from '$lib/types';

export interface DiscoverState {
	tracks: DiscoverTrack[];
	totalCount: number;
	loadingComplete: boolean;
	lastRefreshedAt: string | null;
	progress: number;
	error: string | null;
}

const initialState: DiscoverState = {
	tracks: [],
	totalCount: 0,
	loadingComplete: false,
	lastRefreshedAt: null,
	progress: 0,
	error: null,
};

const POLL_INTERVAL_MS = 60_000;

function createDiscoverFeedStore() {
	const { subscribe, update } = writable<DiscoverState>(initialState);
	let interval: ReturnType<typeof setInterval> | null = null;

	async function tick() {
		try {
			const res = await fetch('/api/feed/discover', { credentials: 'include' });
			if (!res.ok) throw new Error(`HTTP ${res.status}`);
			const data: DiscoverResponse = await res.json();
			update((s) => ({
				...s,
				tracks: data.tracks,
				totalCount: data.totalCount,
				loadingComplete: data.loadingComplete,
				lastRefreshedAt: data.lastRefreshedAt,
				progress: data.progress,
				error: null,
			}));
		} catch (e: unknown) {
			const message = e instanceof Error ? e.message : String(e);
			update((s) => ({ ...s, error: message }));
		}
	}

	return {
		subscribe,

		start() {
			if (interval) return;
			void tick();
			interval = setInterval(() => void tick(), POLL_INTERVAL_MS);
		},

		stop() {
			if (interval) {
				clearInterval(interval);
				interval = null;
			}
		},

		async refresh(): Promise<{ enqueued: boolean; retryAfterSec?: number; error?: string }> {
			try {
				const res = await fetch('/api/feed/discover/refresh', {
					method: 'POST',
					credentials: 'include',
				});
				if (res.status === 429) {
					const body = await res.json();
					return { enqueued: false, retryAfterSec: body.retryAfterSec as number };
				}
				if (!res.ok) throw new Error(`HTTP ${res.status}`);
				await tick();
				return { enqueued: true };
			} catch (e: unknown) {
				const message = e instanceof Error ? e.message : String(e);
				return { enqueued: false, error: message };
			}
		},
	};
}

export const discoverFeedStore = createDiscoverFeedStore();
