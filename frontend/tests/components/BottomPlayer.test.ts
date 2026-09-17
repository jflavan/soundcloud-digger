import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import BottomPlayer from '$lib/components/BottomPlayer.svelte';
import type { FeedTrack } from '$lib/types';
import * as api from '$lib/api';

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

describe('BottomPlayer widget events', () => {
	let handlers: Record<string, () => void>;

	beforeEach(() => {
		handlers = {};
		(window as any).SC = {
			Widget: Object.assign(
				(_iframe: HTMLIFrameElement) => ({
					bind: (event: string, cb: () => void) => { handlers[event] = cb; },
					toggle: vi.fn(),
					getPosition: (cb: (pos: number) => void) => cb(0),
					seekTo: vi.fn(),
				}),
				{ Events: { FINISH: 'finish', ERROR: 'error' } }
			),
		};
	});

	async function renderAndLoad(props: ReturnType<typeof defaultProps>) {
		const { container } = render(BottomPlayer, { props });
		// The component binds widget events once the iframe reports load.
		await Promise.resolve();
		container.querySelector('iframe')!.dispatchEvent(new Event('load'));
		return container;
	}

	it('advances to the next track when the widget finishes', async () => {
		const props = defaultProps();
		await renderAndLoad(props);
		handlers['finish']();
		expect(props.onnext).toHaveBeenCalledOnce();
	});

	it('skips to the next track when the widget reports a playback error', async () => {
		const props = defaultProps();
		await renderAndLoad(props);
		expect(handlers['error']).toBeTypeOf('function');
		handlers['error']();
		expect(props.onnext).toHaveBeenCalledOnce();
	});
});

describe('BottomPlayer preview mode (tracks the widget cannot stream)', () => {
	let playSpy: ReturnType<typeof vi.spyOn>;
	let pauseSpy: ReturnType<typeof vi.spyOn>;

	beforeEach(() => {
		(window as any).SC = { Widget: Object.assign(() => ({ bind: vi.fn() }), { Events: { FINISH: 'finish' } }) };
		document.body.focus();
		playSpy = vi.spyOn(HTMLMediaElement.prototype, 'play').mockImplementation(() => Promise.resolve());
		pauseSpy = vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {});
	});

	afterEach(() => {
		vi.restoreAllMocks();
	});

	function previewProps(overrides: Record<string, unknown> = {}) {
		return defaultProps({ track: makeTrack({ access: 'preview' }), ...overrides });
	}

	it('uses an audio element fed by the stream endpoint instead of the widget iframe', async () => {
		vi.spyOn(api, 'fetchStreamUrl').mockResolvedValue({ url: 'https://cdn.example/preview.mp3', preview: true });
		const { container } = render(BottomPlayer, { props: previewProps() });

		expect(container.querySelector('iframe')).toBeNull();
		await vi.waitFor(() => {
			const audio = container.querySelector('audio') as HTMLAudioElement;
			expect(audio?.src).toBe('https://cdn.example/preview.mp3');
		});
		expect(api.fetchStreamUrl).toHaveBeenCalledWith('https://soundcloud.com/artist-slug/test-track');
	});

	it('explains that only a preview is available and links to the full track', async () => {
		vi.spyOn(api, 'fetchStreamUrl').mockResolvedValue({ url: 'https://cdn.example/preview.mp3', preview: true });
		render(BottomPlayer, { props: previewProps() });

		const link = await screen.findByRole('link', { name: /Full track on SoundCloud/ });
		expect((link as HTMLAnchorElement).href).toBe('https://soundcloud.com/artist-slug/test-track');
		expect(screen.getByText(/30s preview/)).toBeTruthy();
	});

	it('advances to the next track when the preview ends', async () => {
		vi.spyOn(api, 'fetchStreamUrl').mockResolvedValue({ url: 'https://cdn.example/preview.mp3', preview: true });
		const props = previewProps();
		const { container } = render(BottomPlayer, { props });
		await vi.waitFor(() => expect(container.querySelector('audio')).toBeTruthy());

		container.querySelector('audio')!.dispatchEvent(new Event('ended'));
		expect(props.onnext).toHaveBeenCalledOnce();
	});

	it('shows an unavailable notice, and does not auto-skip, when the stream cannot be fetched', async () => {
		vi.spyOn(api, 'fetchStreamUrl').mockRejectedValue(new Error('HTTP 404'));
		const props = previewProps();
		render(BottomPlayer, { props });

		await screen.findByText(/Preview unavailable/);
		expect(props.onnext).not.toHaveBeenCalled();
	});

	it('Space toggles the audio element and arrows seek it', async () => {
		vi.spyOn(api, 'fetchStreamUrl').mockResolvedValue({ url: 'https://cdn.example/preview.mp3', preview: true });
		const { container } = render(BottomPlayer, { props: previewProps() });
		await vi.waitFor(() => expect(container.querySelector('audio')).toBeTruthy());
		const audio = container.querySelector('audio') as HTMLAudioElement;
		Object.defineProperty(audio, 'paused', { value: true, configurable: true });
		playSpy.mockClear();

		await fireEvent.keyDown(window, { code: 'Space' });
		expect(playSpy).toHaveBeenCalledOnce();

		Object.defineProperty(audio, 'paused', { value: false, configurable: true });
		await fireEvent.keyDown(window, { code: 'Space' });
		expect(pauseSpy).toHaveBeenCalledOnce();

		audio.currentTime = 5;
		await fireEvent.keyDown(window, { code: 'ArrowRight' });
		expect(audio.currentTime).toBe(15);
		await fireEvent.keyDown(window, { code: 'ArrowLeft' });
		await fireEvent.keyDown(window, { code: 'ArrowLeft' });
		expect(audio.currentTime).toBe(0);
	});
});
