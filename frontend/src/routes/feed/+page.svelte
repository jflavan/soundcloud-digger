<script lang="ts">
	import { onMount } from 'svelte';
	import { fetchFeed } from '$lib/api';
	import { feedTracks, loadingComplete, totalCount } from '$lib/stores/feedStore';
	import { filteredFeed } from '$lib/stores/filteredFeedStore';
	import ControlsBar from '$lib/components/ControlsBar.svelte';
	import TrackList from '$lib/components/TrackList.svelte';
	import LoadingIndicator from '$lib/components/LoadingIndicator.svelte';

	let error = $state('');
	let intervalId: ReturnType<typeof setInterval> | null = null;

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

<div class="feed-page">
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

	<TrackList tracks={$filteredFeed} />
</div>

<style>
	.feed-page {
		max-width: 800px;
		margin: 0 auto;
		padding: 16px;
		display: flex;
		flex-direction: column;
		gap: 12px;
	}
	.header {
		display: flex;
		justify-content: space-between;
		align-items: center;
	}
	h1 {
		color: #e94560;
		font-size: 24px;
		margin: 0;
	}
	.logout {
		background: transparent;
		border: 1px solid #333;
		color: #888;
		padding: 6px 12px;
		border-radius: 4px;
		cursor: pointer;
	}
	.error {
		text-align: center;
		color: #e94560;
		padding: 16px;
	}
</style>
