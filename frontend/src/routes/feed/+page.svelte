<script lang="ts">
	import { onMount } from 'svelte';
	import { fetchFeed } from '$lib/api';
	import { feedTracks, loadingComplete, totalCount } from '$lib/stores/feedStore';
	import { filteredFeed } from '$lib/stores/filteredFeedStore';
	import ControlsBar from '$lib/components/ControlsBar.svelte';
	import TrackList from '$lib/components/TrackList.svelte';
	import LoadingIndicator from '$lib/components/LoadingIndicator.svelte';
	import BottomPlayer from '$lib/components/BottomPlayer.svelte';
	import type { FeedTrack } from '$lib/types';

	let error = $state('');
	let intervalId: ReturnType<typeof setInterval> | null = null;
	let selectedUrl = $state<string | null>(null);

	const selectedTrack = $derived(
		selectedUrl ? $filteredFeed.find((t) => t.permalinkUrl === selectedUrl) ?? null : null
	);

	function selectTrack(url: string | null) {
		if (!url) return;
		selectedUrl = selectedUrl === url ? null : url;
	}

	function cycleTrack(direction: number) {
		const tracks = $filteredFeed;
		if (tracks.length === 0) return;
		const currentIndex = tracks.findIndex((t) => t.permalinkUrl === selectedUrl);
		let nextIndex: number;
		if (currentIndex === -1) {
			nextIndex = 0;
		} else {
			nextIndex = (currentIndex + direction + tracks.length) % tracks.length;
		}
		selectedUrl = tracks[nextIndex].permalinkUrl;
	}

	async function pollFeed() {
		try {
			const data = await fetchFeed();
			feedTracks.set(data.tracks);
			totalCount.set(data.totalCount);
			loadingComplete.set(data.loadingComplete);
			error = '';
			return data.loadingComplete;
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load feed';
			return false;
		}
	}

	function clearPoll() {
		if (intervalId !== null) {
			clearInterval(intervalId);
			intervalId = null;
		}
	}

	function startLoadingPoll() {
		clearPoll();
		intervalId = setInterval(async () => {
			const complete = await pollFeed();
			if (complete) {
				clearPoll();
				startRefreshPoll();
			}
		}, 2000);
	}

	function startRefreshPoll() {
		clearPoll();
		intervalId = setInterval(pollFeed, 60000);
	}

	async function handleLogout() {
		await fetch('/auth/logout', { method: 'POST', credentials: 'include' });
		window.location.href = '/';
	}

	onMount(() => {
		pollFeed().then((complete) => {
			if (complete) startRefreshPoll();
			else startLoadingPoll();
		});

		return () => clearPoll();
	});
</script>

<div class="feed-page" class:has-player={selectedTrack !== null}>
	<div class="header">
		<h1>SoundCloud Digger</h1>
		<button class="logout" onclick={handleLogout}>Logout</button>
	</div>

	<ControlsBar />

	{#if error}
		<div class="error">
			<p>{error}</p>
			<button onclick={pollFeed}>Retry</button>
		</div>
	{:else if !$loadingComplete}
		<LoadingIndicator totalCount={$totalCount} />
	{/if}

	<TrackList tracks={$filteredFeed} {selectedUrl} onselect={selectTrack} />
</div>

{#if selectedTrack}
	<BottomPlayer
		track={selectedTrack}
		onprev={() => cycleTrack(-1)}
		onnext={() => cycleTrack(1)}
		onclose={() => (selectedUrl = null)}
	/>
{/if}

<style>
	.feed-page {
		max-width: 800px;
		margin: 0 auto;
		padding: 16px;
		display: flex;
		flex-direction: column;
		gap: 12px;
	}
	.feed-page.has-player {
		padding-bottom: 90px;
	}
	.header {
		display: flex;
		justify-content: space-between;
		align-items: center;
	}
	h1 {
		color: #f50;
		font-size: 24px;
		margin: 0;
	}
	.logout {
		background: transparent;
		border: 1px solid #333;
		color: #999;
		padding: 6px 12px;
		border-radius: 4px;
		cursor: pointer;
	}
	.error {
		text-align: center;
		color: #f50;
		padding: 16px;
	}
</style>
