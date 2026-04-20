<script lang="ts">
	import type { FeedTrack } from '$lib/types';
	import TrackRow from './TrackRow.svelte';

	const CHUNK_SIZE = 100;

	let { tracks, selectedUrl = null, onselect }: {
		tracks: FeedTrack[];
		selectedUrl?: string | null;
		onselect?: (url: string | null) => void;
	} = $props();

	let displayCount = $state(CHUNK_SIZE);
	let sentinel = $state<HTMLDivElement | null>(null);

	// Keep the selected track rendered so scroll-to / auto-next can find its row.
	$effect(() => {
		if (!selectedUrl) return;
		const idx = tracks.findIndex((t) => t.permalinkUrl === selectedUrl);
		if (idx >= displayCount) {
			displayCount = Math.min(tracks.length, idx + CHUNK_SIZE);
		}
	});

	$effect(() => {
		if (!sentinel) return;
		const io = new IntersectionObserver(
			(entries) => {
				if (entries[0]?.isIntersecting) {
					displayCount = Math.min(tracks.length, displayCount + CHUNK_SIZE);
				}
			},
			{ rootMargin: '600px' }
		);
		io.observe(sentinel);
		return () => io.disconnect();
	});

	const visibleTracks = $derived(tracks.slice(0, displayCount));
</script>

{#if tracks.length === 0}
	<div class="empty">
		<p>No tracks match your filters.</p>
	</div>
{:else}
	<div class="track-list">
		{#each visibleTracks as track, i (track.permalinkUrl ? track.permalinkUrl + '-' + i : i)}
			<TrackRow
				{track}
				selected={selectedUrl === track.permalinkUrl}
				onselect={() => onselect?.(track.permalinkUrl)}
			/>
		{/each}
		{#if displayCount < tracks.length}
			<div bind:this={sentinel} class="sentinel" aria-hidden="true"></div>
		{/if}
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
	.sentinel {
		height: 1px;
	}
</style>
