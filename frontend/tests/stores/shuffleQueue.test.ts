import { describe, it, expect, vi, afterEach } from 'vitest';
import { buildShuffleQueue } from '$lib/stores/shuffleQueue';

describe('buildShuffleQueue', () => {
	afterEach(() => {
		vi.restoreAllMocks();
	});

	it('returns empty queue for empty input', () => {
		expect(buildShuffleQueue([], null)).toEqual([]);
		expect(buildShuffleQueue([], 'a')).toEqual([]);
	});

	it('returns single element when given single url and no current', () => {
		expect(buildShuffleQueue(['a'], null)).toEqual(['a']);
	});

	it('returns a permutation of the input urls (length + set equality)', () => {
		const urls = ['a', 'b', 'c', 'd', 'e'];
		const result = buildShuffleQueue(urls, null);
		expect(result).toHaveLength(urls.length);
		expect(new Set(result)).toEqual(new Set(urls));
	});

	it('places currentUrl at index 0 when present', () => {
		const urls = ['a', 'b', 'c', 'd'];
		const result = buildShuffleQueue(urls, 'c');
		expect(result[0]).toBe('c');
		expect(result).toHaveLength(urls.length);
		expect(new Set(result)).toEqual(new Set(urls));
	});

	it('ignores currentUrl when absent from urls', () => {
		const urls = ['a', 'b', 'c'];
		const result = buildShuffleQueue(urls, 'z');
		expect(result).toHaveLength(urls.length);
		expect(new Set(result)).toEqual(new Set(urls));
	});

	it('shuffles the non-current tail (Math.random mocked)', () => {
		vi.spyOn(Math, 'random').mockReturnValue(0);
		const result = buildShuffleQueue(['a', 'b', 'c', 'd'], 'b');
		expect(result[0]).toBe('b');
		expect(new Set(result.slice(1))).toEqual(new Set(['a', 'c', 'd']));
	});
});
