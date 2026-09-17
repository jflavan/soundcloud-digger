import { describe, it, expect } from 'vitest';
import { isPlayable, countUnplayable } from '$lib/utils/playability';
import type { FeedTrack } from '$lib/types';

function track(access: string | null): FeedTrack {
	return {
		title: 't', artistName: 'a', artworkUrl: null, genre: null, tags: [],
		likesCount: 0, playbackCount: 0, repostsCount: 0, commentCount: 0,
		createdAt: '2026-01-01T00:00:00Z', permalinkUrl: 'u', duration: 1,
		access, activityType: 'track', appearedAt: '2026-01-01T00:00:00Z',
	};
}

describe('isPlayable', () => {
	it('is true for playable tracks', () => {
		expect(isPlayable(track('playable'))).toBe(true);
	});

	it('treats an unknown access value (null) as playable', () => {
		expect(isPlayable(track(null))).toBe(true);
	});

	it('is false for Go+-gated preview tracks', () => {
		expect(isPlayable(track('preview'))).toBe(false);
	});

	it('is false for blocked tracks', () => {
		expect(isPlayable(track('blocked'))).toBe(false);
	});
});

describe('countUnplayable', () => {
	it('counts tracks that are not playable', () => {
		expect(countUnplayable([track('playable'), track('preview'), track('blocked'), track(null)])).toBe(2);
	});

	it('is zero for an empty list', () => {
		expect(countUnplayable([])).toBe(0);
	});
});
