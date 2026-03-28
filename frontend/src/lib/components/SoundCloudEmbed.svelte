<script lang="ts">
	let { url }: { url: string } = $props();

	const embedUrl = `https://w.soundcloud.com/player/?url=${encodeURIComponent(url)}&color=%23f50&auto_play=true&hide_related=true&show_comments=false&show_user=true&show_reposts=false&show_teaser=false&visual=true`;
</script>

<div class="embed-container">
	<iframe
		title="SoundCloud Player"
		width="100%"
		height="166"
		scrolling="no"
		frameborder="no"
		allow="autoplay"
		src={embedUrl}
	></iframe>
</div>

<style>
	.embed-container {
		padding: 4px 0 4px 52px;
		background: #111;
		border-radius: 0 0 6px 6px;
		margin-top: -2px;
	}
	iframe {
		border-radius: 4px;
	}
</style>
