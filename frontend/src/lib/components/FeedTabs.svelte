<script lang="ts">
	import { feedSource } from '$lib/stores/feedSource';
</script>

<div class="tabs" role="tablist">
	<button
		type="button"
		role="tab"
		class:active={$feedSource === 'feed'}
		onclick={() => feedSource.set('feed')}
	>
		Feed
	</button>
	<button
		type="button"
		role="tab"
		class:active={$feedSource === 'discover'}
		onclick={() => feedSource.set('discover')}
	>
		Discover
	</button>
</div>

<style>
	.tabs {
		display: flex;
		gap: 0.5rem;
		padding: 0.5rem 1rem;
	}

	button {
		padding: 0.5rem 1rem;
		border: 1px solid var(--border, #ccc);
		border-radius: 999px;
		background: transparent;
		cursor: pointer;
		color: inherit;
		font: inherit;
	}

	button.active {
		background: var(--accent, #333);
		color: var(--accent-fg, #fff);
	}
</style>
