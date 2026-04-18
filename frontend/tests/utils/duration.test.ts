import { describe, it, expect } from 'vitest';
import {
	DURATION_STEP_MS,
	DURATION_SLIDER_MIN,
	DURATION_SLIDER_MAX,
	formatDuration,
	parseDuration,
	clampToStep,
	toPercent,
	sanitizeDurationInput,
} from '$lib/utils/duration';

describe('formatDuration', () => {
	it('formats zero as 0:00', () => {
		expect(formatDuration(0)).toBe('0:00');
	});

	it('zero-pads seconds', () => {
		expect(formatDuration(5_000)).toBe('0:05');
		expect(formatDuration(65_000)).toBe('1:05');
	});

	it('handles minute boundaries', () => {
		expect(formatDuration(60_000)).toBe('1:00');
		expect(formatDuration(60 * 60_000)).toBe('60:00');
	});

	it('floors sub-second values', () => {
		expect(formatDuration(999)).toBe('0:00');
		expect(formatDuration(1_500)).toBe('0:01');
	});
});

describe('parseDuration', () => {
	it('treats a bare number as minutes', () => {
		expect(parseDuration('5')).toBe(5 * 60_000);
	});

	it('parses mm:ss format', () => {
		expect(parseDuration('2:30')).toBe(150_000);
	});

	it('treats missing pieces as zero', () => {
		expect(parseDuration(':30')).toBe(30_000);
		expect(parseDuration('2:')).toBe(120_000);
	});

	it('returns null for empty input', () => {
		expect(parseDuration('')).toBeNull();
		expect(parseDuration('   ')).toBeNull();
	});

	it('returns null for non-numeric input', () => {
		expect(parseDuration('abc')).toBeNull();
		expect(parseDuration('1:xx')).toBeNull();
		expect(parseDuration('xx:30')).toBeNull();
	});

	it('returns null for more than one colon', () => {
		expect(parseDuration('1:2:3')).toBeNull();
	});

	it('trims whitespace', () => {
		expect(parseDuration('  3:00  ')).toBe(180_000);
	});
});

describe('clampToStep', () => {
	it('rounds to the nearest 30-second step', () => {
		expect(clampToStep(14_000)).toBe(0);
		expect(clampToStep(15_000)).toBe(30_000);
		expect(clampToStep(44_000)).toBe(30_000);
		expect(clampToStep(45_000)).toBe(60_000);
	});

	it('clamps below the minimum to minimum', () => {
		expect(clampToStep(-1_000)).toBe(DURATION_SLIDER_MIN);
	});

	it('clamps above the maximum to maximum', () => {
		expect(clampToStep(DURATION_SLIDER_MAX + 10_000)).toBe(DURATION_SLIDER_MAX);
	});
});

describe('toPercent', () => {
	it('maps min to 0 and max to 100', () => {
		expect(toPercent(DURATION_SLIDER_MIN)).toBe(0);
		expect(toPercent(DURATION_SLIDER_MAX)).toBe(100);
	});

	it('maps midpoint to 50', () => {
		expect(toPercent(DURATION_SLIDER_MAX / 2)).toBe(50);
	});
});

describe('sanitizeDurationInput', () => {
	it('strips non-numeric, non-colon characters', () => {
		expect(sanitizeDurationInput('1:2a3b')).toBe('1:23');
	});

	it('keeps only the first colon', () => {
		expect(sanitizeDurationInput('1:2:3:4')).toBe('1:234');
	});

	it('returns an empty string for all-invalid input', () => {
		expect(sanitizeDurationInput('abc')).toBe('');
	});

	it('preserves valid input unchanged', () => {
		expect(sanitizeDurationInput('12:34')).toBe('12:34');
	});
});

describe('constants', () => {
	it('DURATION_STEP_MS is 30 seconds', () => {
		expect(DURATION_STEP_MS).toBe(30_000);
	});

	it('DURATION_SLIDER_MAX is 60 minutes', () => {
		expect(DURATION_SLIDER_MAX).toBe(60 * 60_000);
	});
});
