import { writable } from 'svelte/store';

export interface ToastItem {
	id: number;
	message: string;
	actionLabel?: string;
	onAction?: () => void;
}

export interface ToastOptions {
	actionLabel?: string;
	onAction?: () => void;
	/** Auto-dismiss delay. Defaults to 8 seconds. */
	durationMs?: number;
}

const DEFAULT_DURATION_MS = 8_000;

const store = writable<ToastItem[]>([]);
let nextId = 1;

export const toasts = { subscribe: store.subscribe };

export function showToast(message: string, opts: ToastOptions = {}): number {
	const id = nextId++;
	store.update((list) => [...list, { id, message, actionLabel: opts.actionLabel, onAction: opts.onAction }]);
	setTimeout(() => dismissToast(id), opts.durationMs ?? DEFAULT_DURATION_MS);
	return id;
}

export function dismissToast(id: number) {
	store.update((list) => list.filter((t) => t.id !== id));
}
