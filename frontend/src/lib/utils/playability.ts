import type { FeedTrack } from '$lib/types';

/**
 * SoundCloud's `access` field: 'playable' streams normally, 'preview' means only a
 * snippet is offered to API clients (Go+ catalogue and tracks with third-party
 * playback disabled), 'blocked' is unavailable. For non-playable tracks the API
 * still advertises mp3 transcodings but the CDN 404s them (only DRM-encrypted AAC
 * exists), so the embed widget can't play them. A listener's Go+ subscription does
 * NOT change this — embedded players can't sign in, so SoundCloud serves full
 * Go+ tracks only inside its own app. An unknown/null value is treated as playable.
 */
export function isPlayable(track: FeedTrack): boolean {
	return track.access === null || track.access === 'playable';
}

export function countUnplayable(tracks: FeedTrack[]): number {
	return tracks.reduce((n, t) => (isPlayable(t) ? n : n + 1), 0);
}
