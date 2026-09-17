<script lang="ts">
	import { toasts, dismissToast } from '$lib/stores/toastStore';
</script>

{#if $toasts.length > 0}
	<div class="toast-stack" role="status" aria-live="polite">
		{#each $toasts as toast (toast.id)}
			<div class="toast">
				<span class="message">{toast.message}</span>
				{#if toast.actionLabel}
					<button
						class="action"
						onclick={() => {
							toast.onAction?.();
							dismissToast(toast.id);
						}}
					>
						{toast.actionLabel}
					</button>
				{/if}
				<button class="close" aria-label="Dismiss" onclick={() => dismissToast(toast.id)}>×</button>
			</div>
		{/each}
	</div>
{/if}

<style>
	.toast-stack {
		position: fixed;
		top: 16px;
		left: 50%;
		transform: translateX(-50%);
		display: flex;
		flex-direction: column;
		gap: 8px;
		z-index: 110;
		width: min(480px, calc(100vw - 32px));
	}
	.toast {
		display: flex;
		align-items: center;
		gap: 12px;
		padding: 10px 12px 10px 16px;
		background: #1a1a1a;
		border: 1px solid #333;
		border-left: 3px solid #f50;
		border-radius: 8px;
		color: #eee;
		font-size: 13px;
		box-shadow: 0 6px 24px rgba(0, 0, 0, 0.5);
	}
	.message {
		flex: 1;
	}
	.action {
		background: transparent;
		border: 1px solid #f50;
		color: #f50;
		padding: 4px 10px;
		border-radius: 4px;
		cursor: pointer;
		font-size: 13px;
		white-space: nowrap;
	}
	.action:hover {
		background: #f50;
		color: white;
	}
	.close {
		background: transparent;
		border: none;
		color: #888;
		font-size: 18px;
		line-height: 1;
		cursor: pointer;
		padding: 0 4px;
	}
	.close:hover {
		color: #eee;
	}
</style>
