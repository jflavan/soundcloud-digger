<script lang="ts">
	import type { FeedTrack } from '$lib/types';

	let { track }: { track: FeedTrack } = $props();

	function formatDuration(ms: number): string {
		const minutes = Math.floor(ms / 60000);
		const seconds = Math.floor((ms % 60000) / 1000);
		return `${minutes}:${seconds.toString().padStart(2, '0')}`;
	}

	function formatLikes(count: number): string {
		if (count >= 1000) return `${(count / 1000).toFixed(1)}k`;
		return count.toString();
	}
</script>

<a
	href={track.permalinkUrl}
	target="_blank"
	rel="noopener noreferrer"
	class="track-row"
>
	<img
		src={track.artworkUrl ?? '/placeholder.png'}
		alt={track.title}
		class="artwork"
		width="40"
		height="40"
	/>
	<div class="info">
		<span class="title">{track.title}</span>
		<span class="meta">{track.artistName}{track.genre ? ` · ${track.genre}` : ''}</span>
	</div>
	<span class="likes">♥ {formatLikes(track.likesCount)}</span>
	<span class="duration">{formatDuration(track.duration)}</span>
</a>

<style>
	.track-row {
		display: flex;
		align-items: center;
		gap: 12px;
		padding: 8px 12px;
		border-radius: 6px;
		background: #16213e;
		text-decoration: none;
		color: inherit;
	}
	.track-row:hover {
		background: #1a2744;
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
		color: #eee;
		font-size: 14px;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}
	.meta {
		color: #888;
		font-size: 12px;
	}
	.likes {
		color: #e94560;
		font-size: 13px;
		flex-shrink: 0;
	}
	.duration {
		color: #888;
		font-size: 12px;
		flex-shrink: 0;
	}
</style>
