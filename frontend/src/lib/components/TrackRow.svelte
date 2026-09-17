<script lang="ts">
	import type { FeedTrack } from '$lib/types';
	import { isPlayable } from '$lib/utils/playability';

	let { track, selected = false, onselect }: {
		track: FeedTrack;
		selected?: boolean;
		onselect?: () => void;
	} = $props();

	function formatDuration(ms: number): string {
		const minutes = Math.floor(ms / 60000);
		const seconds = Math.floor((ms % 60000) / 1000);
		return `${minutes}:${seconds.toString().padStart(2, '0')}`;
	}

	function formatCount(count: number): string {
		if (count >= 1000) return `${(count / 1000).toFixed(1)}k`;
		return count.toString();
	}

	function handleClick(e: MouseEvent) {
		if (onselect) {
			e.preventDefault();
			onselect();
		}
	}
</script>

<a
	href={track.permalinkUrl}
	target="_blank"
	rel="noopener noreferrer"
	class="track-row"
	class:selected
	data-track-url={track.permalinkUrl}
	onclick={handleClick}
>
	<img
		src={track.artworkUrl ?? '/placeholder.png'}
		alt={track.title}
		class="artwork"
		width="40"
		height="40"
	/>
	<div class="info">
		<span class="title">
			{track.title}
			{#if !isPlayable(track)}<span class="badge" title="Only a 30s preview is available here; the full track plays on SoundCloud">Preview</span>{/if}
		</span>
		<span class="meta">{track.artistName}{track.genre ? ` · ${track.genre}` : ''}</span>
	</div>
	<div class="stats">
		<span class="stat likes">♥ {formatCount(track.likesCount)}</span>
		<span class="stat plays">▶ {formatCount(track.playbackCount)}</span>
		<span class="stat reposts">↻ {formatCount(track.repostsCount)}</span>
		<span class="stat comments">💬 {formatCount(track.commentCount)}</span>
	</div>
	<span class="duration">{formatDuration(track.duration)}</span>
</a>

<style>
	.badge {
		display: inline-block;
		margin-left: 6px;
		padding: 1px 6px;
		border: 1px solid #f50;
		border-radius: 3px;
		color: #f50;
		font-size: 10px;
		font-weight: 600;
		letter-spacing: 0.04em;
		text-transform: uppercase;
		vertical-align: 1px;
	}
	.track-row {
		display: flex;
		align-items: center;
		gap: 12px;
		padding: 8px 12px;
		border-radius: 6px;
		background: #1a1a1a;
		text-decoration: none;
		color: inherit;
		cursor: pointer;
	}
	.track-row:hover {
		background: #222;
	}
	.track-row.selected {
		background: #252525;
		outline: 1px solid #f50;
		outline-offset: -1px;
	}
	.artwork {
		border-radius: 4px;
		flex-shrink: 0;
		object-fit: cover;
	}
	.info {
		flex: 1;
		min-width: 0;
		display: flex;
		flex-direction: column;
	}
	.title {
		color: #e5e5e5;
		font-size: 14px;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}
	.meta {
		color: #888;
		font-size: 12px;
	}
	.stats {
		display: flex;
		align-items: center;
		gap: 10px;
		flex-shrink: 0;
	}
	.stat {
		font-size: 12px;
		line-height: 1;
		color: #888;
		white-space: nowrap;
	}
	.stat.likes {
		color: #f50;
	}
	.duration {
		color: #888;
		font-size: 12px;
		line-height: 1;
		flex-shrink: 0;
	}
</style>
