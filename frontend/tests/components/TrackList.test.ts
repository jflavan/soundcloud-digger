import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/svelte';
import TrackList from '$lib/components/TrackList.svelte';
import type { FeedTrack } from '$lib/types';

function makeTrack(overrides: Partial<FeedTrack> = {}): FeedTrack {
	return {
		title: 'Test Track',
		artistName: 'Artist',
		artworkUrl: null,
		genre: 'Electronic',
		tags: [],
		likesCount: 100,
		playbackCount: 500,
		createdAt: new Date().toISOString(),
		permalinkUrl: 'https://soundcloud.com/test',
		duration: 180000,
		access: 'playable',
		activityType: 'track',
		appearedAt: new Date().toISOString(),
		...overrides,
	};
}

describe('TrackList', () => {
	it('renders empty state when no tracks', () => {
		render(TrackList, { props: { tracks: [] } });
		expect(screen.getByText('No tracks match your filters.')).toBeTruthy();
	});

	it('renders track rows for each track', () => {
		const tracks = [
			makeTrack({ title: 'Track A', permalinkUrl: 'https://soundcloud.com/track-a' }),
			makeTrack({ title: 'Track B', permalinkUrl: 'https://soundcloud.com/track-b' }),
		];
		render(TrackList, { props: { tracks } });
		expect(screen.getByText('Track A')).toBeTruthy();
		expect(screen.getByText('Track B')).toBeTruthy();
	});
});
