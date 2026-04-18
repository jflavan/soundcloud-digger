export const DURATION_STEP_MS = 30_000;
export const DURATION_SLIDER_MIN = 0;
export const DURATION_SLIDER_MAX = 60 * 60_000;

export function formatDuration(ms: number): string {
	const totalSeconds = Math.floor(ms / 1000);
	const minutes = Math.floor(totalSeconds / 60);
	const seconds = totalSeconds % 60;
	return `${minutes}:${String(seconds).padStart(2, '0')}`;
}

export function parseDuration(text: string): number | null {
	const trimmed = text.trim();
	if (!trimmed) return null;

	const parts = trimmed.split(':');
	if (parts.length === 1) {
		const minutes = parseInt(parts[0], 10);
		if (isNaN(minutes)) return null;
		return minutes * 60_000;
	}
	if (parts.length === 2) {
		const minutes = parseInt(parts[0] || '0', 10);
		const seconds = parseInt(parts[1] || '0', 10);
		if (isNaN(minutes) || isNaN(seconds)) return null;
		return (minutes * 60 + seconds) * 1000;
	}
	return null;
}

export function clampToStep(value: number): number {
	const clamped = Math.max(DURATION_SLIDER_MIN, Math.min(DURATION_SLIDER_MAX, value));
	return Math.round(clamped / DURATION_STEP_MS) * DURATION_STEP_MS;
}

export function toPercent(value: number): number {
	return ((value - DURATION_SLIDER_MIN) / (DURATION_SLIDER_MAX - DURATION_SLIDER_MIN)) * 100;
}

export function sanitizeDurationInput(raw: string): string {
	let value = raw.replace(/[^0-9:]/g, '');
	const colonCount = (value.match(/:/g) || []).length;
	if (colonCount > 1) {
		const firstColon = value.indexOf(':');
		value = value.slice(0, firstColon + 1) + value.slice(firstColon + 1).replace(/:/g, '');
	}
	return value;
}
