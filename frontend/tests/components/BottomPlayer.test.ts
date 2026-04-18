import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import BottomPlayer from '$lib/components/BottomPlayer.svelte';
import type { FeedTrack } from '$lib/types';

function makeTrack(overrides: Partial<FeedTrack> = {}): FeedTrack {
	return {
		title: 'Test Track',
		artistName: 'Test Artist',
		artworkUrl: 'https://example.com/art.jpg',
		genre: 'Electronic',
		tags: [],
		likesCount: 0,
		playbackCount: 0,
		repostsCount: 0,
		commentCount: 0,
		createdAt: new Date().toISOString(),
		permalinkUrl: 'https://soundcloud.com/artist-slug/test-track',
		duration: 180000,
		access: 'playable',
		activityType: 'track',
		appearedAt: new Date().toISOString(),
		...overrides,
	};
}

function defaultProps(overrides: Record<string, unknown> = {}) {
	return {
		track: makeTrack(),
		shuffle: false,
		onprev: vi.fn(),
		onnext: vi.fn(),
		ontoggleShuffle: vi.fn(),
		onclose: vi.fn(),
		...overrides,
	};
}

describe('BottomPlayer rendering', () => {
	beforeEach(() => {
		// Stub the SoundCloud Widget API so loadWidgetApi resolves immediately.
		(window as any).SC = {
			Widget: Object.assign(
				(_iframe: HTMLIFrameElement) => ({
					bind: vi.fn(),
					toggle: vi.fn(),
					play: vi.fn(),
					pause: vi.fn(),
					getPosition: (cb: (pos: number) => void) => cb(15000),
					seekTo: vi.fn(),
				}),
				{ Events: { FINISH: 'finish' } }
			),
		};
	});

	it('renders the track title and artist name', () => {
		render(BottomPlayer, { props: defaultProps() });
		expect(screen.getByText('Test Track')).toBeTruthy();
		expect(screen.getByText('Test Artist')).toBeTruthy();
	});

	it('wraps the track title in a link to the track permalink', () => {
		render(BottomPlayer, { props: defaultProps() });
		const titleLink = screen.getByText('Test Track').closest('a') as HTMLAnchorElement;
		expect(titleLink).toBeTruthy();
		expect(titleLink.href).toBe('https://soundcloud.com/artist-slug/test-track');
		expect(titleLink.target).toBe('_blank');
	});

	it('derives the artist link from the permalink URL', () => {
		render(BottomPlayer, { props: defaultProps() });
		const artistLink = screen.getByText('Test Artist').closest('a') as HTMLAnchorElement;
		expect(artistLink).toBeTruthy();
		expect(artistLink.href).toBe('https://soundcloud.com/artist-slug');
	});

	it('renders a plain span for title when permalinkUrl is null', () => {
		render(BottomPlayer, {
			props: defaultProps({ track: makeTrack({ permalinkUrl: null as unknown as string }) }),
		});
		const title = screen.getByText('Test Track');
		expect(title.closest('a')).toBeNull();
	});

	it('marks the shuffle button active when shuffle is on', () => {
		render(BottomPlayer, { props: defaultProps({ shuffle: true }) });
		const shuffleBtn = screen.getByTitle('Shuffle on');
		expect(shuffleBtn.classList.contains('active')).toBe(true);
	});

	it('shows "Shuffle off" title when shuffle is off', () => {
		render(BottomPlayer, { props: defaultProps({ shuffle: false }) });
		expect(screen.getByTitle('Shuffle off')).toBeTruthy();
	});
});

describe('BottomPlayer controls', () => {
	beforeEach(() => {
		(window as any).SC = undefined;
	});

	it('invokes onprev when the prev button is clicked', async () => {
		const onprev = vi.fn();
		render(BottomPlayer, { props: defaultProps({ onprev }) });
		await fireEvent.click(screen.getByTitle('Previous track'));
		expect(onprev).toHaveBeenCalledOnce();
	});

	it('invokes onnext when the next button is clicked', async () => {
		const onnext = vi.fn();
		render(BottomPlayer, { props: defaultProps({ onnext }) });
		await fireEvent.click(screen.getByTitle('Next track'));
		expect(onnext).toHaveBeenCalledOnce();
	});

	it('invokes ontoggleShuffle when the shuffle button is clicked', async () => {
		const ontoggleShuffle = vi.fn();
		render(BottomPlayer, { props: defaultProps({ ontoggleShuffle }) });
		await fireEvent.click(screen.getByTitle('Shuffle off'));
		expect(ontoggleShuffle).toHaveBeenCalledOnce();
	});

	it('invokes onclose when the close button is clicked', async () => {
		const onclose = vi.fn();
		render(BottomPlayer, { props: defaultProps({ onclose }) });
		await fireEvent.click(screen.getByTitle('Close player'));
		expect(onclose).toHaveBeenCalledOnce();
	});
});

describe('BottomPlayer keyboard shortcuts', () => {
	beforeEach(() => {
		(window as any).SC = undefined;
		document.body.focus();
	});

	it('Ctrl+ArrowRight triggers onnext', async () => {
		const onnext = vi.fn();
		render(BottomPlayer, { props: defaultProps({ onnext }) });
		await fireEvent.keyDown(window, { code: 'ArrowRight', ctrlKey: true });
		expect(onnext).toHaveBeenCalledOnce();
	});

	it('Ctrl+ArrowLeft triggers onprev', async () => {
		const onprev = vi.fn();
		render(BottomPlayer, { props: defaultProps({ onprev }) });
		await fireEvent.keyDown(window, { code: 'ArrowLeft', ctrlKey: true });
		expect(onprev).toHaveBeenCalledOnce();
	});

	it('bare arrow keys do not trigger prev/next', async () => {
		const onprev = vi.fn();
		const onnext = vi.fn();
		render(BottomPlayer, { props: defaultProps({ onprev, onnext }) });
		await fireEvent.keyDown(window, { code: 'ArrowLeft' });
		await fireEvent.keyDown(window, { code: 'ArrowRight' });
		expect(onprev).not.toHaveBeenCalled();
		expect(onnext).not.toHaveBeenCalled();
	});

	it('ignores shortcuts when an input has focus', async () => {
		const onnext = vi.fn();
		render(BottomPlayer, { props: defaultProps({ onnext }) });
		const input = document.createElement('input');
		document.body.appendChild(input);
		input.focus();
		await fireEvent.keyDown(window, { code: 'ArrowRight', ctrlKey: true });
		expect(onnext).not.toHaveBeenCalled();
		input.remove();
	});
});
