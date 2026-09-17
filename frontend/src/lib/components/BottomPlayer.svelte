<script lang="ts">
	import type { FeedTrack } from '$lib/types';
	import { resolvePlayerAction, isEditableTarget, SEEK_STEP_MS } from '$lib/utils/keyboardShortcuts';
	import { isPlayable } from '$lib/utils/playability';
	import { fetchStreamUrl } from '$lib/api';

	let { track, shuffle, onprev, onnext, ontoggleShuffle, onclose }: {
		track: FeedTrack;
		shuffle: boolean;
		onprev: () => void;
		onnext: () => void;
		ontoggleShuffle: () => void;
		onclose: () => void;
	} = $props();

	const embedUrl = $derived(
		`https://w.soundcloud.com/player/?url=${encodeURIComponent(track.permalinkUrl ?? '')}&color=%23ff5500&auto_play=true&hide_related=true&show_comments=false&show_user=false&show_reposts=false&show_teaser=false&visual=false`
	);

	const artistUrl = $derived.by(() => {
		if (!track.permalinkUrl) return null;
		try {
			const u = new URL(track.permalinkUrl);
			const slug = u.pathname.split('/').filter(Boolean)[0];
			return slug ? `${u.origin}/${slug}` : null;
		} catch {
			return null;
		}
	});

	// Tracks the widget can't stream (access=preview) are played here directly:
	// the API still serves this account a ~30s preview, fetched via the backend.
	const previewMode = $derived(!isPlayable(track));
	let audioEl = $state<HTMLAudioElement | null>(null);
	let previewSrc = $state<string | null>(null);
	let previewError = $state(false);
	// Mirrored from the <audio> element via bind:, drives the custom control.
	let paused = $state(true);
	let currentTime = $state(0);
	let duration = $state(0);
	const progressPct = $derived(duration > 0 ? Math.min(100, (currentTime / duration) * 100) : 0);

	function formatTime(seconds: number): string {
		if (!Number.isFinite(seconds) || seconds < 0) return '0:00';
		const m = Math.floor(seconds / 60);
		const sec = Math.floor(seconds % 60);
		return `${m}:${sec.toString().padStart(2, '0')}`;
	}

	function togglePreview() {
		if (!audioEl) return;
		if (audioEl.paused) void audioEl.play();
		else audioEl.pause();
	}

	function seekPreviewBy(deltaMs: number) {
		if (!audioEl) return;
		audioEl.currentTime = Math.max(0, audioEl.currentTime + deltaMs / 1000);
	}

	function seekPreviewTo(e: MouseEvent) {
		if (!audioEl || !duration) return;
		const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
		const ratio = Math.min(1, Math.max(0, (e.clientX - rect.left) / rect.width));
		audioEl.currentTime = ratio * duration;
	}

	function handleProgressKey(e: KeyboardEvent) {
		if (e.code !== 'ArrowLeft' && e.code !== 'ArrowRight') return;
		e.preventDefault();
		e.stopPropagation(); // the window-level shortcut would seek a second time
		seekPreviewBy(e.code === 'ArrowRight' ? SEEK_STEP_MS : -SEEK_STEP_MS);
	}

	$effect(() => {
		const url = track.permalinkUrl;
		if (!previewMode || !url) return;
		previewSrc = null;
		previewError = false;
		let cancelled = false;
		fetchStreamUrl(url)
			.then((r) => {
				if (!cancelled) previewSrc = r.url;
			})
			.catch((err) => {
				if (cancelled) return;
				// Deliberately no auto-skip: if the backend is down this would race
				// through the whole queue. The note below offers the SoundCloud link.
				console.warn('Preview stream unavailable for', url, err);
				previewError = true;
			});
		return () => {
			cancelled = true;
		};
	});

	let iframeEl = $state<HTMLIFrameElement | null>(null);
	let finishBound = $state(false);
	let widget: any = null;

	let apiPromise: Promise<void> | null = null;
	function loadWidgetApi(): Promise<void> {
		if ((window as any).SC?.Widget) return Promise.resolve();
		if (apiPromise) return apiPromise;
		apiPromise = new Promise((resolve, reject) => {
			const script = document.createElement('script');
			script.src = 'https://w.soundcloud.com/player/api.js';
			script.onload = () => resolve();
			script.onerror = () => reject(new Error('Failed to load SC Widget API'));
			document.head.appendChild(script);
		});
		return apiPromise;
	}

	function bindWidgetEvents(iframe: HTMLIFrameElement) {
		if (finishBound) return;
		const SC = (window as any).SC;
		if (!SC?.Widget) return;
		widget = SC.Widget(iframe);
		widget.bind(SC.Widget.Events.FINISH, () => {
			onnext();
		});
		// A track whose stream 404s (Go+-gated, region-blocked, removed) would
		// otherwise sit silently and stall autoplay/shuffle. Skip it.
		if (SC.Widget.Events.ERROR) {
			widget.bind(SC.Widget.Events.ERROR, () => {
				console.warn('SoundCloud widget could not play', track.permalinkUrl, '— skipping');
				onnext();
			});
		}
		finishBound = true;
	}

	$effect(() => {
		const iframe = iframeEl;
		if (!iframe) return;
		finishBound = false;
		widget = null;

		loadWidgetApi().then(() => {
			iframe.addEventListener('load', () => bindWidgetEvents(iframe), { once: true });
		}).catch((err) => console.warn('SC Widget API unavailable, autoplay-next disabled:', err));
	});

	function seekBy(deltaMs: number) {
		if (!widget) return;
		widget.getPosition((pos: number) => widget.seekTo(Math.max(0, pos + deltaMs)));
	}

	function handleKey(e: KeyboardEvent) {
		const action = resolvePlayerAction(e, isEditableTarget(document.activeElement));
		if (!action) return;
		e.preventDefault();
		switch (action.type) {
			case 'toggle':
				if (previewMode) togglePreview();
				else widget?.toggle();
				break;
			case 'seek':
				if (previewMode) seekPreviewBy(action.deltaMs);
				else seekBy(action.deltaMs);
				break;
			case 'prev': onprev(); break;
			case 'next': onnext(); break;
		}
	}
</script>

<svelte:window onkeydown={handleKey} />

<div class="player-bar">
	<div class="accent-line"></div>
	<div class="player-inner">
		<div class="track-section">
			{#if track.permalinkUrl}
				<a
					class="artwork-link"
					href={track.permalinkUrl}
					target="_blank"
					rel="noopener noreferrer"
					title="Open track on SoundCloud"
				>
					<img
						src={track.artworkUrl ?? '/placeholder.png'}
						alt={track.title}
						class="artwork"
						width="48"
						height="48"
					/>
				</a>
			{:else}
				<img
					src={track.artworkUrl ?? '/placeholder.png'}
					alt={track.title}
					class="artwork"
					width="48"
					height="48"
				/>
			{/if}
			<div class="track-meta">
				{#if track.permalinkUrl}
					<a
						class="track-title"
						href={track.permalinkUrl}
						target="_blank"
						rel="noopener noreferrer"
						title="Open track on SoundCloud"
					>{track.title}</a>
				{:else}
					<span class="track-title">{track.title}</span>
				{/if}
				{#if artistUrl}
					<a
						class="track-artist"
						href={artistUrl}
						target="_blank"
						rel="noopener noreferrer"
						title="Open artist on SoundCloud"
					>{track.artistName}</a>
				{:else}
					<span class="track-artist">{track.artistName}</span>
				{/if}
			</div>
		</div>

		<div class="controls-section">
			<button
				class="ctrl-btn"
				class:active={shuffle}
				onclick={ontoggleShuffle}
				title={shuffle ? 'Shuffle on' : 'Shuffle off'}
			>
				<svg
					width="18"
					height="18"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					stroke-linecap="round"
					stroke-linejoin="round"
				>
					<path d="M2 18h1.4c1.3 0 2.5-.6 3.3-1.7l6.1-8.6c.7-1.1 2-1.7 3.3-1.7H22"/>
					<path d="m18 2 4 4-4 4"/>
					<path d="M2 6h1.9c1.5 0 2.9.9 3.6 2.2"/>
					<path d="M22 18h-5.9c-1.3 0-2.6-.7-3.3-1.8l-.5-.8"/>
					<path d="m18 14 4 4-4 4"/>
				</svg>
			</button>
			<button class="ctrl-btn" onclick={onprev} title="Previous track">
				<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
					<path d="M6 6h2v12H6zm3.5 6l8.5 6V6z"/>
				</svg>
			</button>
			<button class="ctrl-btn" onclick={onnext} title="Next track">
				<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
					<path d="M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z"/>
				</svg>
			</button>
		</div>

		<div class="embed-section">
			{#if previewMode}
				<!-- Styled after the 20px SoundCloud mini embed: orange round play, grey title, progress. -->
				<div class="preview-player">
					{#if previewSrc}
						<audio
							bind:this={audioEl}
							src={previewSrc}
							autoplay
							preload="auto"
							bind:paused
							bind:currentTime
							bind:duration
							onended={onnext}
							onerror={() => (previewError = true)}
						></audio>
					{/if}
					<button
						class="preview-play"
						onclick={togglePreview}
						disabled={!previewSrc}
						aria-label={paused ? 'Play preview' : 'Pause preview'}
						title={paused ? 'Play' : 'Pause'}
					>
						{#if paused}
							<svg width="10" height="10" viewBox="0 0 24 24" fill="currentColor"><path d="M7 4v16l14-8z"/></svg>
						{:else}
							<svg width="10" height="10" viewBox="0 0 24 24" fill="currentColor"><path d="M6 4h4v16H6zm8 0h4v16h-4z"/></svg>
						{/if}
					</button>
					{#if previewSrc}
						<span class="preview-elapsed">{formatTime(currentTime)}</span>
					{:else}
						<span class="preview-status">{previewError ? 'Preview unavailable' : 'Loading preview…'}</span>
					{/if}
					<div
						class="preview-progress"
						role="slider"
						aria-label="Seek"
						aria-valuemin="0"
						aria-valuemax={Math.round(duration)}
						aria-valuenow={Math.round(currentTime)}
						tabindex="0"
						onclick={seekPreviewTo}
						onkeydown={handleProgressKey}
					>
						<div class="preview-progress-fill" style="width: {progressPct}%"></div>
					</div>
					<span class="preview-total">{formatTime(duration)}</span>
					<span class="preview-badge">Preview</span>
					{#if track.permalinkUrl}
						<a class="preview-link" href={track.permalinkUrl} target="_blank" rel="noopener noreferrer">Full track on SoundCloud ↗</a>
					{/if}
				</div>
			{:else}
				{#key track.permalinkUrl}
					<iframe
						bind:this={iframeEl}
						title="SoundCloud Player"
						width="100%"
						height="20"
						scrolling="no"
						frameborder="no"
						allow="autoplay"
						src={embedUrl}
					></iframe>
				{/key}
			{/if}
		</div>

		<button class="close-btn" onclick={onclose} title="Close player">
			<svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
				<path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
			</svg>
		</button>
	</div>
</div>

<style>
	.player-bar {
		position: fixed;
		bottom: 0;
		left: 0;
		right: 0;
		z-index: 100;
		background: rgba(18, 18, 18, 0.92);
		backdrop-filter: blur(24px);
		-webkit-backdrop-filter: blur(24px);
	}

	.accent-line {
		height: 2px;
		background: linear-gradient(90deg, #f50 0%, #ff8a3d 50%, #f50 100%);
		opacity: 0.8;
	}

	.player-inner {
		display: flex;
		align-items: center;
		gap: 16px;
		padding: 10px 20px;
		max-width: 1200px;
		margin: 0 auto;
	}

	.track-section {
		display: flex;
		align-items: center;
		gap: 12px;
		min-width: 0;
		flex: 0 1 280px;
	}

	.artwork {
		border-radius: 6px;
		flex-shrink: 0;
		object-fit: cover;
		box-shadow: 0 2px 8px rgba(0, 0, 0, 0.4);
		display: block;
	}

	.artwork-link {
		display: block;
		flex-shrink: 0;
		line-height: 0;
		transition: opacity 0.15s;
	}

	.artwork-link:hover {
		opacity: 0.85;
	}

	.track-meta {
		display: flex;
		flex-direction: column;
		gap: 2px;
		min-width: 0;
	}

	.track-title {
		color: #f0f0f0;
		font-size: 13px;
		font-weight: 500;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
		letter-spacing: 0.01em;
		text-decoration: none;
	}

	a.track-title:hover {
		color: #f50;
	}

	.track-artist {
		color: #888;
		font-size: 11px;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
		text-decoration: none;
	}

	a.track-artist:hover {
		color: #f50;
	}

	.controls-section {
		display: flex;
		align-items: center;
		gap: 4px;
		flex-shrink: 0;
	}

	.ctrl-btn {
		background: transparent;
		border: none;
		color: #999;
		width: 36px;
		height: 36px;
		border-radius: 50%;
		cursor: pointer;
		display: flex;
		align-items: center;
		justify-content: center;
		transition: color 0.15s, background 0.15s;
	}

	.ctrl-btn:hover {
		color: #f50;
		background: rgba(255, 85, 0, 0.08);
	}

	.ctrl-btn.active {
		color: #f50;
	}

	.ctrl-btn.active:hover {
		color: #f50;
		background: rgba(255, 85, 0, 0.12);
	}

	.embed-section {
		flex: 1;
		min-width: 0;
		overflow: hidden;
		border-radius: 4px;
	}

	.preview-player {
		display: flex;
		align-items: center;
		gap: 10px;
		height: 20px;
		min-width: 0;
	}

	.preview-play {
		width: 20px;
		height: 20px;
		border-radius: 50%;
		border: none;
		padding: 0;
		background: #f50;
		color: #fff;
		display: flex;
		align-items: center;
		justify-content: center;
		cursor: pointer;
		flex-shrink: 0;
		transition: background 0.15s;
	}

	.preview-play:hover {
		background: #ff6a1a;
	}

	.preview-play:disabled {
		opacity: 0.5;
		cursor: default;
	}

	/* Mirrors the embed while playing: elapsed in orange left of the bar, total in grey right of it. */
	.preview-elapsed,
	.preview-total,
	.preview-status {
		font-size: 11px;
		font-variant-numeric: tabular-nums;
		white-space: nowrap;
	}

	.preview-elapsed {
		color: #f50;
	}

	.preview-total,
	.preview-status {
		color: #999;
	}

	.preview-progress {
		flex: 1;
		min-width: 60px;
		height: 3px;
		background: #333;
		border-radius: 2px;
		cursor: pointer;
	}

	.preview-progress:focus-visible {
		outline: 1px solid #f50;
		outline-offset: 4px;
	}

	.preview-progress-fill {
		height: 100%;
		background: #f50;
		border-radius: 2px;
	}

	.preview-badge {
		padding: 1px 6px;
		border: 1px solid #f50;
		border-radius: 3px;
		color: #f50;
		font-size: 10px;
		font-weight: 600;
		letter-spacing: 0.04em;
		text-transform: uppercase;
		white-space: nowrap;
	}

	.preview-link {
		color: #f50;
		font-size: 11px;
		text-decoration: none;
		white-space: nowrap;
	}

	.preview-link:hover {
		text-decoration: underline;
	}

	.embed-section iframe {
		display: block;
		border: none;
		opacity: 0.85;
		transition: opacity 0.2s;
	}

	.embed-section:hover iframe {
		opacity: 1;
	}

	.close-btn {
		background: transparent;
		border: none;
		color: #555;
		cursor: pointer;
		padding: 8px;
		border-radius: 50%;
		display: flex;
		align-items: center;
		justify-content: center;
		flex-shrink: 0;
		transition: color 0.15s, background 0.15s;
	}

	.close-btn:hover {
		color: #f50;
		background: rgba(255, 85, 0, 0.08);
	}
</style>
