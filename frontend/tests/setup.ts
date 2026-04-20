import { vi } from 'vitest';

// Mock SvelteKit's runtime modules that aren't available under plain vitest/jsdom.
vi.mock('$app/navigation', () => ({
	replaceState: (url: string | URL, _state: unknown) => {
		window.history.replaceState({}, '', url.toString());
	},
	pushState: (url: string | URL, _state: unknown) => {
		window.history.pushState({}, '', url.toString());
	},
	goto: vi.fn(),
}));
