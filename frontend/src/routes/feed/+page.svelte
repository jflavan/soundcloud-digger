<script lang="ts">
	import { onMount } from 'svelte';
	import { fetchFeed } from '$lib/api';
	import { feedTracks, loadingComplete, totalCount } from '$lib/stores/feedStore';
	import { filteredFeed } from '$lib/stores/filteredFeedStore';
	import ControlsBar from '$lib/components/ControlsBar.svelte';
	import TrackList from '$lib/components/TrackList.svelte';
	import LoadingIndicator from '$lib/components/LoadingIndicator.svelte';
	import BottomPlayer from '$lib/components/BottomPlayer.svelte';

	let error = $state('');
	let refreshing = $state(false);
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
		const tracks = $filteredFeed.filter((t) => t.permalinkUrl != null);
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

	async function refreshFeed() {
		refreshing = true;
		await pollFeed();
		refreshing = false;
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

<div class="fab-group" class:has-player={selectedTrack !== null}>
	<button
		class="fab"
		class:spinning={refreshing}
		title="Refresh feed"
		disabled={refreshing}
		onclick={refreshFeed}
	>
		<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
			<path d="M17.65 6.35A7.958 7.958 0 0 0 12 4C7.58 4 4.01 7.58 4.01 12S7.58 20 12 20c3.73 0 6.84-2.55 7.73-6h-2.08A5.99 5.99 0 0 1 12 18c-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z"/>
		</svg>
	</button>
	{#if selectedTrack}
		<button
			class="fab"
			title="Scroll to current track"
			onclick={() => {
				const el = document.querySelector(`[data-track-url="${CSS.escape(selectedUrl ?? '')}"]`);
				el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
			}}
		>
			<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
				<path d="M12 3v10.55A4 4 0 1 0 14 17V7h4V3h-6z"/>
			</svg>
		</button>
	{/if}
	<button
		class="fab"
		title="Scroll to top"
		onclick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
	>
		<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
			<path d="M4 12l1.41 1.41L11 7.83V20h2V7.83l5.58 5.59L20 12l-8-8-8 8z"/>
		</svg>
	</button>
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
	.fab-group {
		position: fixed;
		bottom: 20px;
		right: 20px;
		display: flex;
		flex-direction: column;
		gap: 8px;
		z-index: 99;
	}
	.fab-group.has-player {
		bottom: 90px;
	}
	.fab {
		width: 40px;
		height: 40px;
		border-radius: 50%;
		border: 1px solid #333;
		background: rgba(30, 30, 30, 0.9);
		backdrop-filter: blur(12px);
		-webkit-backdrop-filter: blur(12px);
		color: #999;
		cursor: pointer;
		display: flex;
		align-items: center;
		justify-content: center;
		transition: color 0.15s, border-color 0.15s, background 0.15s;
	}
	.fab:hover {
		color: #f50;
		border-color: #f50;
		background: rgba(255, 85, 0, 0.08);
	}
	.fab.spinning svg {
		animation: spin 0.8s linear infinite;
	}
	@keyframes spin {
		from { transform: rotate(0deg); }
		to { transform: rotate(360deg); }
	}
</style>
