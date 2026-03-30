<script lang="ts">
	import type { FeedTrack } from '$lib/types';
	import TrackRow from './TrackRow.svelte';

	let { tracks, selectedUrl = null, onselect }: {
		tracks: FeedTrack[];
		selectedUrl?: string | null;
		onselect?: (url: string | null) => void;
	} = $props();
</script>

{#if tracks.length === 0}
	<div class="empty">
		<p>No tracks match your filters.</p>
	</div>
{:else}
	<div class="track-list">
		{#each tracks as track, i (track.permalinkUrl ? track.permalinkUrl + '-' + i : i)}
			<TrackRow
				{track}
				selected={selectedUrl === track.permalinkUrl}
				onselect={() => onselect?.(track.permalinkUrl)}
			/>
		{/each}
	</div>
{/if}

<style>
	.track-list {
		display: flex;
		flex-direction: column;
		gap: 4px;
	}
	.empty {
		text-align: center;
		color: #888;
		padding: 48px 0;
	}
</style>
