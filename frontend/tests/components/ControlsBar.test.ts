import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import { get } from 'svelte/store';
import ControlsBar from '$lib/components/ControlsBar.svelte';
import {
	sortBy,
	discoverSortBy,
	timeRange,
	timeField,
	selectedGenres,
	excludedGenres,
	durationMin,
	durationMax,
} from '$lib/stores/filterStore';
import { feedTracks } from '$lib/stores/feedStore';
import { feedSource } from '$lib/stores/feedSource';
import type { FeedTrack } from '$lib/types';

function makeTrack(overrides: Partial<FeedTrack> = {}): FeedTrack {
	return {
		title: 'Track',
		artistName: 'Artist',
		artworkUrl: null,
		genre: 'Electronic',
		tags: [],
		likesCount: 0,
		playbackCount: 0,
		repostsCount: 0,
		commentCount: 0,
		createdAt: new Date().toISOString(),
		permalinkUrl: 'https://soundcloud.com/t',
		duration: 180000,
		access: 'playable',
		activityType: 'track',
		appearedAt: new Date().toISOString(),
		...overrides,
	};
}

describe('ControlsBar', () => {
	beforeEach(() => {
		feedSource.set('feed');
		sortBy.set('likes');
		discoverSortBy.set('reposterCount');
		timeRange.set('24h');
		timeField.set('feed');
		selectedGenres.set([]);
		excludedGenres.set([]);
		durationMin.set(null);
		durationMax.set(null);
		feedTracks.set([]);
	});

	it('updates sortBy when a sort button is clicked', async () => {
		render(ControlsBar);
		await fireEvent.click(screen.getByText('Plays'));
		expect(get(sortBy)).toBe('plays');
	});

	it('updates timeRange when a time button is clicked', async () => {
		render(ControlsBar);
		await fireEvent.click(screen.getByText('7d'));
		expect(get(timeRange)).toBe('7d');
	});

	it('updates timeField when the "Uploaded" button is clicked', async () => {
		render(ControlsBar);
		await fireEvent.click(screen.getByText('Uploaded'));
		expect(get(timeField)).toBe('uploaded');
	});

	it('updates timeField when the "In feed" button is clicked', async () => {
		timeField.set('uploaded');
		render(ControlsBar);
		await fireEvent.click(screen.getByText('In feed'));
		expect(get(timeField)).toBe('feed');
	});

	it('marks the active sort button', () => {
		sortBy.set('reposts');
		render(ControlsBar);
		const button = screen.getByText('Reposts') as HTMLButtonElement;
		expect(button.classList.contains('active')).toBe(true);
	});

	it('does not render genre group when no genres are available', () => {
		render(ControlsBar);
		expect(screen.queryByText('Genre:')).toBeNull();
	});

	it('renders genre dropdowns when genres are available', () => {
		feedTracks.set([makeTrack({ genre: 'House' }), makeTrack({ genre: 'Techno' })]);
		render(ControlsBar);
		expect(screen.getByText('Genre:')).toBeTruthy();
		expect(screen.getByText('Exclude:')).toBeTruthy();
	});

	it('toggles a genre into selectedGenres', async () => {
		feedTracks.set([makeTrack({ genre: 'House' })]);
		render(ControlsBar);
		await fireEvent.click(screen.getByText('All genres'));
		await fireEvent.click(screen.getByText('House'));
		expect(get(selectedGenres)).toEqual(['House']);
	});

	it('toggles a genre out of selectedGenres when re-clicked', async () => {
		feedTracks.set([makeTrack({ genre: 'House' })]);
		selectedGenres.set(['House']);
		render(ControlsBar);
		await fireEvent.click(screen.getByText('1 selected'));
		await fireEvent.click(screen.getByText('House'));
		expect(get(selectedGenres)).toEqual([]);
	});

	it('toggles a genre into excludedGenres', async () => {
		feedTracks.set([makeTrack({ genre: 'Pop' })]);
		render(ControlsBar);
		await fireEvent.click(screen.getByText('None'));
		await fireEvent.click(screen.getByText('Pop'));
		expect(get(excludedGenres)).toEqual(['Pop']);
	});

	it('toggles a genre out of excludedGenres when re-clicked', async () => {
		feedTracks.set([makeTrack({ genre: 'Pop' })]);
		excludedGenres.set(['Pop']);
		render(ControlsBar);
		await fireEvent.click(screen.getByText('1 excluded'));
		await fireEvent.click(screen.getByText('Pop'));
		expect(get(excludedGenres)).toEqual([]);
	});
});

describe('ControlsBar — Discover mode', () => {
	beforeEach(() => {
		feedSource.set('feed');
		sortBy.set('likes');
		discoverSortBy.set('reposterCount');
		feedTracks.set([]);
	});

	it('shows "Reposts from follows" sort option when on Discover', () => {
		feedSource.set('discover');
		render(ControlsBar);
		expect(screen.getByText(/reposts from follows/i)).toBeTruthy();
	});

	it('hides "Reposts from follows" on primary feed', () => {
		feedSource.set('feed');
		render(ControlsBar);
		expect(screen.queryByText(/reposts from follows/i)).toBeNull();
	});

	it('clicking a sort button on Discover writes to discoverSortBy', async () => {
		feedSource.set('discover');
		render(ControlsBar);
		await fireEvent.click(screen.getByText(/^likes$/i));
		expect(get(discoverSortBy)).toBe('likes');
		expect(get(sortBy)).toBe('likes'); // untouched from default setup
	});

	it('clicking a sort button on Feed writes to sortBy', async () => {
		feedSource.set('feed');
		sortBy.set('likes');
		discoverSortBy.set('reposterCount');
		render(ControlsBar);
		await fireEvent.click(screen.getByText(/^plays$/i));
		expect(get(sortBy)).toBe('plays');
		expect(get(discoverSortBy)).toBe('reposterCount'); // unchanged
	});
});
