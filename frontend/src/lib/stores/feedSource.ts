import { writable } from 'svelte/store';
import { replaceState } from '$app/navigation';

export type FeedSource = 'feed' | 'discover';

function readFromUrl(): FeedSource {
	if (typeof window === 'undefined') return 'feed';
	const param = new URLSearchParams(window.location.search).get('source');
	return param === 'discover' ? 'discover' : 'feed';
}

function writeToUrl(source: FeedSource) {
	if (typeof window === 'undefined') return;
	const url = new URL(window.location.href);
	if (source === 'feed') url.searchParams.delete('source');
	else url.searchParams.set('source', source);
	if (url.toString() === window.location.href) return;
	replaceState(url, {});
}

function createFeedSource() {
	const store = writable<FeedSource>(readFromUrl());
	return {
		subscribe: store.subscribe,
		set(value: FeedSource) {
			writeToUrl(value);
			store.set(value);
		},
	};
}

export const feedSource = createFeedSource();
