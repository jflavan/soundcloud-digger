<div class="login-page">
	<h1>SoundCloud Digger</h1>
	<p>Sort your feed by likes, plays, reposts, and more. Filter by genre.</p>
	<a href="/auth/login" class="login-button" data-sveltekit-reload>Log in with SoundCloud</a>
</div>

<style>
	.login-page {
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		min-height: 80vh;
		text-align: center;
	}
	h1 {
		color: #f50;
		font-size: 32px;
		margin-bottom: 8px;
	}
	p {
		color: #999;
		margin-bottom: 32px;
	}
	.login-button {
		background: #f50;
		color: white;
		padding: 12px 32px;
		border-radius: 6px;
		text-decoration: none;
		font-size: 16px;
	}
	.login-button:hover {
		background: #e64a00;
	}
</style>
