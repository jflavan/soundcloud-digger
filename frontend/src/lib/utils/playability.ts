import type { FeedTrack } from '$lib/types';

/**
 * SoundCloud's `access` field is per-user: 'playable' streams normally, 'preview'
 * means the track is gated behind Go+/Next Pro for this account, 'blocked' is
 * unavailable. For non-playable tracks the API still advertises mp3 transcodings
 * but the CDN 404s them (only DRM-encrypted AAC exists), so the embed widget
 * can't play them. An unknown/null value is treated as playable.
 */
export function isPlayable(track: FeedTrack): boolean {
	return track.access === null || track.access === 'playable';
}

export function countUnplayable(tracks: FeedTrack[]): number {
	return tracks.reduce((n, t) => (isPlayable(t) ? n : n + 1), 0);
}
