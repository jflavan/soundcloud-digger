import { sveltekit } from '@sveltejs/kit/vite';
import { svelteTesting } from '@testing-library/svelte/vite';
import { defineConfig } from 'vitest/config';

export default defineConfig({
	plugins: [sveltekit(), svelteTesting()],
	test: {
		include: ['tests/**/*.test.ts'],
		environment: 'jsdom',
		setupFiles: ['./tests/setup.ts'],
		coverage: {
			provider: 'v8',
			reporter: ['text', 'html'],
			include: ['src/lib/**/*.{ts,svelte}'],
			exclude: ['src/lib/types.ts', 'src/lib/api.ts'],
			thresholds: {
				lines: 80,
				statements: 80,
				functions: 80,
				branches: 80,
			},
		},
	},
	server: {
		host: 'localhost',
		port: 5173,
		strictPort: true,
		allowedHosts: ['scdigger.localhost'],
		proxy: {
			'/api': 'http://localhost:5032',
			'/auth': 'http://localhost:5032',
		},
	},
});
