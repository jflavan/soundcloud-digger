<script lang="ts">
	let { totalCount }: { totalCount: number } = $props();
</script>

<div class="loading">
	<p>Loading your feed... {totalCount} tracks fetched</p>
</div>

<style>
	.loading {
		text-align: center;
		color: #aaa;
		padding: 24px 0;
		font-size: 14px;
	}
</style>
