/**
 * Return a shuffled copy of `urls`. If `currentUrl` is present in `urls`, it is
 * placed at index 0 and the remaining elements are Fisher-Yates shuffled after it.
 */
export function buildShuffleQueue(urls: string[], currentUrl: string | null): string[] {
	if (urls.length === 0) return [];

	const hasCurrent = currentUrl !== null && urls.includes(currentUrl);
	const rest = hasCurrent ? urls.filter((u) => u !== currentUrl) : urls.slice();

	for (let i = rest.length - 1; i > 0; i--) {
		const j = Math.floor(Math.random() * (i + 1));
		[rest[i], rest[j]] = [rest[j], rest[i]];
	}

	return hasCurrent ? [currentUrl as string, ...rest] : rest;
}
