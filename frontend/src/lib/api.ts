import type { FeedResponse } from './types';

const API_BASE = '/api';

export async function fetchFeed(): Promise<FeedResponse> {
	const response = await fetch(`${API_BASE}/feed`, {
		credentials: 'include',
	});

	if (response.status === 401) {
		window.location.href = '/';
		throw new Error('Unauthorized');
	}

	if (!response.ok) {
		throw new Error(`Failed to fetch feed: ${response.statusText}`);
	}

	return response.json();
}
