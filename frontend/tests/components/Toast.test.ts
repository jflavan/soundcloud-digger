import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import { get } from 'svelte/store';
import Toast from '$lib/components/Toast.svelte';
import { toasts, showToast, dismissToast } from '$lib/stores/toastStore';

describe('Toast', () => {
	beforeEach(() => {
		for (const t of get(toasts)) dismissToast(t.id);
	});

	it('renders nothing when there are no toasts', () => {
		const { container } = render(Toast);
		expect(container.querySelector('.toast')).toBeNull();
	});

	it('renders each toast message in a live region', () => {
		showToast('Hello there');
		render(Toast);
		const region = screen.getByRole('status');
		expect(region.textContent).toContain('Hello there');
	});

	it('renders the action button and runs the action, then dismisses', async () => {
		const onAction = vi.fn();
		showToast('Hidden tracks', { actionLabel: 'Show anyway', onAction });
		render(Toast);
		await fireEvent.click(screen.getByRole('button', { name: 'Show anyway' }));
		expect(onAction).toHaveBeenCalledOnce();
		expect(get(toasts)).toHaveLength(0);
	});

	it('omits the action button when no action is given', () => {
		showToast('plain');
		render(Toast);
		expect(screen.queryByRole('button', { name: 'Show anyway' })).toBeNull();
	});

	it('the close button dismisses the toast', async () => {
		showToast('closable');
		render(Toast);
		await fireEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
		expect(get(toasts)).toHaveLength(0);
	});
});
