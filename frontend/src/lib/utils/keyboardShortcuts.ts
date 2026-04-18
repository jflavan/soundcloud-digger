export type PlayerAction =
	| { type: 'toggle' }
	| { type: 'seek'; deltaMs: number }
	| { type: 'prev' }
	| { type: 'next' };

export interface KeyEventLike {
	code: string;
	ctrlKey: boolean;
	metaKey: boolean;
	shiftKey: boolean;
	altKey: boolean;
}

export const SEEK_STEP_MS = 10_000;

export function resolvePlayerAction(e: KeyEventLike, disabled: boolean): PlayerAction | null {
	if (disabled) return null;
	const mod = e.ctrlKey || e.metaKey;

	if (e.code === 'Space') {
		if (mod || e.shiftKey || e.altKey) return null;
		return { type: 'toggle' };
	}
	if (e.code === 'ArrowLeft') {
		return mod ? { type: 'prev' } : { type: 'seek', deltaMs: -SEEK_STEP_MS };
	}
	if (e.code === 'ArrowRight') {
		return mod ? { type: 'next' } : { type: 'seek', deltaMs: SEEK_STEP_MS };
	}
	return null;
}

export function isEditableTarget(el: Element | null): boolean {
	if (!el || el === document.body) return false;
	const html = el as HTMLElement;
	if (html.isContentEditable || html.contentEditable === 'true') return true;
	const tag = el.tagName;
	return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || tag === 'BUTTON' || tag === 'A';
}
