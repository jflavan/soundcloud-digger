<script lang="ts">
	import type { FeedTrack } from '$lib/types';

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

	let iframeEl = $state<HTMLIFrameElement | null>(null);
	let finishBound = $state(false);

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

	function bindFinishEvent(iframe: HTMLIFrameElement) {
		if (finishBound) return;
		const SC = (window as any).SC;
		if (!SC?.Widget) return;
		const widget = SC.Widget(iframe);
		widget.bind(SC.Widget.Events.FINISH, () => {
			onnext();
		});
		finishBound = true;
	}

	$effect(() => {
		const iframe = iframeEl;
		if (!iframe) return;
		finishBound = false;

		loadWidgetApi().then(() => {
			iframe.addEventListener('load', () => bindFinishEvent(iframe), { once: true });
		}).catch((err) => console.warn('SC Widget API unavailable, autoplay-next disabled:', err));
	});
</script>

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
