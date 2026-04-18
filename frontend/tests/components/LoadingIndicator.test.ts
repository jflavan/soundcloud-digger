import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/svelte';
import LoadingIndicator from '$lib/components/LoadingIndicator.svelte';

describe('LoadingIndicator', () => {
	it('renders the loading message with the current track count', () => {
		render(LoadingIndicator, { props: { totalCount: 42 } });
		expect(screen.getByText(/Loading your feed\.\.\. 42 tracks fetched/)).toBeTruthy();
	});

	it('renders zero when no tracks have been fetched yet', () => {
		render(LoadingIndicator, { props: { totalCount: 0 } });
		expect(screen.getByText(/0 tracks fetched/)).toBeTruthy();
	});
});
