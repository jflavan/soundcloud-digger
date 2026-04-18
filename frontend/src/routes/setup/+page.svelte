<script lang="ts">
	import { saveCredentials } from '$lib/api';

	let step = $state(1);
	let clientId = $state('');
	let clientSecret = $state('');
	let saving = $state(false);
	let error = $state('');

	async function handleSave() {
		saving = true;
		error = '';
		try {
			await saveCredentials(clientId, clientSecret);
			step = 3;
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to save credentials';
		} finally {
			saving = false;
		}
	}
</script>

<div class="setup-page">
	<h1>SoundCloud Digger</h1>
	<p class="subtitle">First-time setup</p>

	<div class="wizard">
		<div class="steps">
			<span class="step" class:active={step === 1} class:done={step > 1}>1. Register App</span>
			<span class="step" class:active={step === 2} class:done={step > 2}>2. Enter Credentials</span>
			<span class="step" class:active={step === 3}>3. Done</span>
		</div>

		{#if step === 1}
			<div class="step-content">
				<h2>Register a SoundCloud App</h2>
				<ol>
					<li>Go to <a href="https://soundcloud.com/you/apps" target="_blank" rel="noopener">soundcloud.com/you/apps</a> (log in if needed)</li>
					<li>Click <strong>"Register a new application"</strong></li>
					<li>Give it any name (e.g. "SoundCloud Digger")</li>
					<li>
						Set the <strong>Redirect URI</strong> to:
						<code class="uri">http://scdigger.localhost:5173/auth/callback</code>
					</li>
					<li>Save the app, then copy your <strong>Client ID</strong> and <strong>Client Secret</strong></li>
				</ol>
				<button class="primary" onclick={() => (step = 2)}>I have my credentials</button>
			</div>
		{:else if step === 2}
			<div class="step-content">
				<h2>Enter Your Credentials</h2>
				<form onsubmit={(e) => { e.preventDefault(); handleSave(); }}>
					<label>
						Client ID
						<input type="text" bind:value={clientId} placeholder="Your SoundCloud Client ID" required />
					</label>
					<label>
						Client Secret
						<input type="password" bind:value={clientSecret} placeholder="Your SoundCloud Client Secret" required />
					</label>
					{#if error}
						<p class="error">{error}</p>
					{/if}
					<div class="buttons">
						<button type="button" class="secondary" onclick={() => (step = 1)}>Back</button>
						<button type="submit" class="primary" disabled={saving || !clientId || !clientSecret}>
							{saving ? 'Saving...' : 'Save Credentials'}
						</button>
					</div>
				</form>
			</div>
		{:else}
			<div class="step-content done-step">
				<h2>You're all set!</h2>
				<p>Your SoundCloud credentials have been saved.</p>
				<a href="/auth/login" class="primary login-link" data-sveltekit-reload>Log in with SoundCloud</a>
			</div>
		{/if}
	</div>
</div>

<style>
	.setup-page {
		display: flex;
		flex-direction: column;
		align-items: center;
		min-height: 80vh;
		padding: 48px 16px;
	}
	h1 {
		color: #f50;
		font-size: 32px;
		margin-bottom: 4px;
	}
	.subtitle {
		color: #999;
		margin-bottom: 32px;
	}
	.wizard {
		background: #1a1a1a;
		border: 1px solid #333;
		border-radius: 12px;
		padding: 32px;
		max-width: 520px;
		width: 100%;
	}
	.steps {
		display: flex;
		gap: 8px;
		margin-bottom: 24px;
		font-size: 13px;
	}
	.step {
		color: #666;
		padding: 4px 8px;
		border-radius: 4px;
	}
	.step.active {
		color: #f50;
		background: rgba(255, 85, 0, 0.1);
	}
	.step.done {
		color: #4caf50;
	}
	.step-content h2 {
		color: #e5e5e5;
		font-size: 18px;
		margin-bottom: 16px;
	}
	ol {
		color: #ccc;
		padding-left: 20px;
		line-height: 1.8;
	}
	ol a {
		color: #f50;
	}
	.uri {
		display: block;
		background: #222;
		padding: 8px 12px;
		border-radius: 4px;
		margin-top: 4px;
		font-size: 13px;
		color: #e5e5e5;
		user-select: all;
	}
	label {
		display: block;
		color: #aaa;
		font-size: 13px;
		margin-bottom: 16px;
	}
	input {
		display: block;
		width: 100%;
		margin-top: 4px;
		padding: 10px 12px;
		background: #222;
		border: 1px solid #333;
		border-radius: 6px;
		color: #e5e5e5;
		font-size: 14px;
		box-sizing: border-box;
	}
	input:focus {
		outline: none;
		border-color: #f50;
	}
	.error {
		color: #ef5350;
		font-size: 13px;
		margin-bottom: 8px;
	}
	.buttons {
		display: flex;
		gap: 12px;
		justify-content: flex-end;
	}
	.primary {
		background: #f50;
		color: white;
		border: none;
		padding: 10px 24px;
		border-radius: 6px;
		font-size: 14px;
		cursor: pointer;
	}
	.primary:hover {
		background: #e64a00;
	}
	.primary:disabled {
		opacity: 0.5;
		cursor: not-allowed;
	}
	.secondary {
		background: transparent;
		color: #999;
		border: 1px solid #333;
		padding: 10px 24px;
		border-radius: 6px;
		font-size: 14px;
		cursor: pointer;
	}
	.secondary:hover {
		border-color: #555;
		color: #ccc;
	}
	.login-link {
		display: inline-block;
		text-decoration: none;
		margin-top: 16px;
	}
	.done-step p {
		color: #999;
		margin-bottom: 8px;
	}
</style>
