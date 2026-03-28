<script lang="ts">
	import type { FeedTrack } from '$lib/types';
	import TrackRow from './TrackRow.svelte';
	import SoundCloudEmbed from './SoundCloudEmbed.svelte';

	let { tracks }: { tracks: FeedTrack[] } = $props();

	let selectedUrl = $state<string | null>(null);

	function toggleTrack(url: string | null) {
		if (!url) return;
		selectedUrl = selectedUrl === url ? null : url;
	}
</script>

{#if tracks.length === 0}
	<div class="empty">
		<p>No tracks match your filters.</p>
	</div>
{:else}
	<div class="track-list">
		{#each tracks as track, i (track.permalinkUrl ? track.permalinkUrl + '-' + i : i)}
			<div class="track-item">
				<TrackRow
					{track}
					selected={selectedUrl === track.permalinkUrl}
					onselect={() => toggleTrack(track.permalinkUrl)}
				/>
				{#if selectedUrl === track.permalinkUrl && track.permalinkUrl}
					<SoundCloudEmbed url={track.permalinkUrl} />
				{/if}
			</div>
		{/each}
	</div>
{/if}

<style>
	.track-list {
		display: flex;
		flex-direction: column;
		gap: 4px;
	}
	.track-item {
		display: flex;
		flex-direction: column;
	}
	.empty {
		text-align: center;
		color: #888;
		padding: 48px 0;
	}
</style>
