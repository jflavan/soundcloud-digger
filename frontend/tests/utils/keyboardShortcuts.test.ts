import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
	resolvePlayerAction,
	isEditableTarget,
	SEEK_STEP_MS,
	type KeyEventLike,
} from '$lib/utils/keyboardShortcuts';

function key(overrides: Partial<KeyEventLike>): KeyEventLike {
	return {
		code: '',
		ctrlKey: false,
		metaKey: false,
		shiftKey: false,
		altKey: false,
		...overrides,
	};
}

describe('resolvePlayerAction', () => {
	it('returns null when shortcuts are disabled', () => {
		expect(resolvePlayerAction(key({ code: 'Space' }), true)).toBeNull();
		expect(resolvePlayerAction(key({ code: 'ArrowLeft' }), true)).toBeNull();
		expect(resolvePlayerAction(key({ code: 'ArrowRight' }), true)).toBeNull();
	});

	it('returns null for unrelated keys', () => {
		expect(resolvePlayerAction(key({ code: 'KeyA' }), false)).toBeNull();
		expect(resolvePlayerAction(key({ code: 'Enter' }), false)).toBeNull();
		expect(resolvePlayerAction(key({ code: 'ArrowUp' }), false)).toBeNull();
	});

	it('Space toggles playback', () => {
		expect(resolvePlayerAction(key({ code: 'Space' }), false)).toEqual({ type: 'toggle' });
	});

	it('Space with any modifier is ignored', () => {
		expect(resolvePlayerAction(key({ code: 'Space', ctrlKey: true }), false)).toBeNull();
		expect(resolvePlayerAction(key({ code: 'Space', metaKey: true }), false)).toBeNull();
		expect(resolvePlayerAction(key({ code: 'Space', shiftKey: true }), false)).toBeNull();
		expect(resolvePlayerAction(key({ code: 'Space', altKey: true }), false)).toBeNull();
	});

	it('ArrowLeft seeks backward by 10 seconds', () => {
		expect(resolvePlayerAction(key({ code: 'ArrowLeft' }), false)).toEqual({
			type: 'seek',
			deltaMs: -SEEK_STEP_MS,
		});
	});

	it('ArrowRight seeks forward by 10 seconds', () => {
		expect(resolvePlayerAction(key({ code: 'ArrowRight' }), false)).toEqual({
			type: 'seek',
			deltaMs: SEEK_STEP_MS,
		});
	});

	it('Ctrl+ArrowLeft goes to previous track', () => {
		expect(resolvePlayerAction(key({ code: 'ArrowLeft', ctrlKey: true }), false)).toEqual({
			type: 'prev',
		});
	});

	it('Ctrl+ArrowRight goes to next track', () => {
		expect(resolvePlayerAction(key({ code: 'ArrowRight', ctrlKey: true }), false)).toEqual({
			type: 'next',
		});
	});

	it('Meta (Cmd) works as the modifier on macOS', () => {
		expect(resolvePlayerAction(key({ code: 'ArrowLeft', metaKey: true }), false)).toEqual({
			type: 'prev',
		});
		expect(resolvePlayerAction(key({ code: 'ArrowRight', metaKey: true }), false)).toEqual({
			type: 'next',
		});
	});

	it('SEEK_STEP_MS is exactly 10 seconds', () => {
		expect(SEEK_STEP_MS).toBe(10_000);
	});
});

describe('isEditableTarget', () => {
	let host: HTMLDivElement;

	beforeEach(() => {
		host = document.createElement('div');
		document.body.appendChild(host);
	});

	afterEach(() => {
		host.remove();
	});

	it('returns false for null', () => {
		expect(isEditableTarget(null)).toBe(false);
	});

	it('returns false for document.body', () => {
		expect(isEditableTarget(document.body)).toBe(false);
	});

	it('returns true for INPUT', () => {
		const input = document.createElement('input');
		host.appendChild(input);
		expect(isEditableTarget(input)).toBe(true);
	});

	it('returns true for TEXTAREA', () => {
		const ta = document.createElement('textarea');
		host.appendChild(ta);
		expect(isEditableTarget(ta)).toBe(true);
	});

	it('returns true for SELECT', () => {
		const sel = document.createElement('select');
		host.appendChild(sel);
		expect(isEditableTarget(sel)).toBe(true);
	});

	it('returns true for BUTTON', () => {
		const btn = document.createElement('button');
		host.appendChild(btn);
		expect(isEditableTarget(btn)).toBe(true);
	});

	it('returns true for A', () => {
		const a = document.createElement('a');
		host.appendChild(a);
		expect(isEditableTarget(a)).toBe(true);
	});

	it('returns true for contenteditable elements', () => {
		const div = document.createElement('div');
		div.contentEditable = 'true';
		host.appendChild(div);
		expect(isEditableTarget(div)).toBe(true);
	});

	it('returns false for a plain DIV', () => {
		const div = document.createElement('div');
		host.appendChild(div);
		expect(isEditableTarget(div)).toBe(false);
	});

	it('returns false for a SPAN', () => {
		const span = document.createElement('span');
		host.appendChild(span);
		expect(isEditableTarget(span)).toBe(false);
	});
});
