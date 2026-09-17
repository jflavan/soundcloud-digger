import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { get } from 'svelte/store';
import { toasts, showToast, dismissToast } from '$lib/stores/toastStore';

describe('toastStore', () => {
	beforeEach(() => {
		vi.useFakeTimers();
		for (const t of get(toasts)) dismissToast(t.id);
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	it('starts empty', () => {
		expect(get(toasts)).toEqual([]);
	});

	it('showToast adds a toast with a unique id and the message', () => {
		const a = showToast('first');
		const b = showToast('second');
		expect(a).not.toBe(b);
		expect(get(toasts).map((t) => t.message)).toEqual(['first', 'second']);
	});

	it('carries an optional action', () => {
		const onAction = vi.fn();
		showToast('msg', { actionLabel: 'Undo', onAction });
		const [t] = get(toasts);
		expect(t.actionLabel).toBe('Undo');
		t.onAction?.();
		expect(onAction).toHaveBeenCalledOnce();
	});

	it('auto-dismisses after the default duration', () => {
		showToast('bye');
		expect(get(toasts)).toHaveLength(1);
		vi.advanceTimersByTime(7_999);
		expect(get(toasts)).toHaveLength(1);
		vi.advanceTimersByTime(1);
		expect(get(toasts)).toHaveLength(0);
	});

	it('honours a custom duration', () => {
		showToast('quick', { durationMs: 100 });
		vi.advanceTimersByTime(100);
		expect(get(toasts)).toHaveLength(0);
	});

	it('dismissToast removes only the matching toast', () => {
		const a = showToast('a');
		showToast('b');
		dismissToast(a);
		expect(get(toasts).map((t) => t.message)).toEqual(['b']);
	});

	it('dismissing an unknown id is a no-op', () => {
		showToast('a');
		dismissToast(999_999);
		expect(get(toasts)).toHaveLength(1);
	});
});
