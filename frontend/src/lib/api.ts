import type { FeedResponse } from './types';

const API_BASE = '/api';

export async function checkSetupStatus(): Promise<{ configured: boolean }> {
	const response = await fetch(`${API_BASE}/setup/status`, {
		credentials: 'include',
	});
	if (!response.ok) {
		throw new Error(`Failed to check setup status: ${response.statusText}`);
	}
	return response.json();
}

export async function saveCredentials(
	clientId: string,
	clientSecret: string,
): Promise<{ success: boolean }> {
	const response = await fetch(`${API_BASE}/setup/credentials`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		credentials: 'include',
		body: JSON.stringify({ clientId, clientSecret }),
	});
	if (!response.ok) {
		const data = await response.json().catch(() => ({}));
		throw new Error(data.error || `Failed to save credentials: ${response.statusText}`);
	}
	return response.json();
}

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
