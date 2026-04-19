import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import { get } from 'svelte/store';
import FeedTabs from '$lib/components/FeedTabs.svelte';
import { feedSource } from '$lib/stores/feedSource';

describe('FeedTabs', () => {
	beforeEach(() => {
		window.history.replaceState({}, '', '/feed');
		feedSource.set('feed');
	});

	it('renders both pills', () => {
		render(FeedTabs);
		expect(screen.getByRole('tab', { name: /^feed$/i })).toBeTruthy();
		expect(screen.getByRole('tab', { name: /^discover$/i })).toBeTruthy();
	});

	it('applies active class to the current source', () => {
		render(FeedTabs);
		const feedPill = screen.getByRole('tab', { name: /^feed$/i });
		expect(feedPill.className).toMatch(/active/);
	});

	it('clicking Discover updates the store', async () => {
		render(FeedTabs);
		await fireEvent.click(screen.getByRole('tab', { name: /^discover$/i }));
		expect(get(feedSource)).toBe('discover');
	});

	it('switching back to Feed updates the active class', async () => {
		render(FeedTabs);
		await fireEvent.click(screen.getByRole('tab', { name: /^discover$/i }));
		await fireEvent.click(screen.getByRole('tab', { name: /^feed$/i }));
		const feedPill = screen.getByRole('tab', { name: /^feed$/i });
		expect(feedPill.className).toMatch(/active/);
	});
});
