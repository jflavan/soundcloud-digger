<script lang="ts">
	import type { FeedTrack } from '$lib/types';
	import TrackRow from './TrackRow.svelte';

	let { tracks }: { tracks: FeedTrack[] } = $props();
</script>

{#if tracks.length === 0}
	<div class="empty">
		<p>No tracks match your filters.</p>
	</div>
{:else}
	<div class="track-list">
		{#each tracks as track (track.permalinkUrl ?? track.title + track.artistName)}
			<TrackRow {track} />
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
