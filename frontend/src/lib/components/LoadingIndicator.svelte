<script lang="ts">
	let {
		totalCount,
		label = 'Loading your feed...',
		progress = null,
	}: {
		totalCount: number;
		label?: string;
		progress?: number | null;
	} = $props();
</script>

<div class="loading">
	<p>
		<span class="spinner"></span>
		{label}
		{#if progress !== null && progress > 0} {progress}% ·{/if}
		{totalCount} tracks fetched
	</p>
</div>

<style>
	.loading {
		text-align: center;
		color: #999;
		padding: 24px 0;
		font-size: 14px;
	}

	.loading p {
		display: inline-flex;
		align-items: center;
		gap: 8px;
	}

	.spinner {
		display: inline-block;
		width: 14px;
		height: 14px;
		border: 2px solid #555;
		border-top-color: #ff5500;
		border-radius: 50%;
		animation: spin 0.8s linear infinite;
	}

	@keyframes spin {
		to {
			transform: rotate(360deg);
		}
	}
</style>
