<script lang="ts">
	import type { FeedTrack } from '$lib/types';

	let { track, onprev, onnext, onclose }: {
		track: FeedTrack;
		onprev: () => void;
		onnext: () => void;
		onclose: () => void;
	} = $props();

	const embedUrl = $derived(
		`https://w.soundcloud.com/player/?url=${encodeURIComponent(track.permalinkUrl ?? '')}&color=%23ff5500&auto_play=true&hide_related=true&show_comments=false&show_user=false&show_reposts=false&show_teaser=false&visual=false`
	);
</script>

<div class="player-bar">
	<div class="accent-line"></div>
	<div class="player-inner">
		<div class="track-section">
			<img
				src={track.artworkUrl ?? '/placeholder.png'}
				alt={track.title}
				class="artwork"
				width="48"
				height="48"
			/>
			<div class="track-meta">
				<span class="track-title">{track.title}</span>
				<span class="track-artist">{track.artistName}</span>
			</div>
		</div>

		<div class="controls-section">
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
	}

	.track-artist {
		color: #888;
		font-size: 11px;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
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
