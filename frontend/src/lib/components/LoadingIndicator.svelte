<script lang="ts">
	let { totalCount }: { totalCount: number } = $props();
</script>

<div class="loading">
	<p>Loading your feed... {totalCount} tracks fetched</p>
</div>

<style>
	.loading {
		text-align: center;
		color: #999;
		padding: 24px 0;
		font-size: 14px;
	}
</style>
