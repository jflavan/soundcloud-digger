import { sveltekit } from '@sveltejs/kit/vite';
import { svelteTesting } from '@testing-library/svelte/vite';
import { defineConfig } from 'vitest/config';

export default defineConfig({
	plugins: [sveltekit(), svelteTesting()],
	test: {
		include: ['tests/**/*.test.ts'],
		environment: 'jsdom',
	},
	server: {
		port: 5173,
		strictPort: true,
		proxy: {
			'/api': 'http://localhost:5032',
			'/auth': 'http://localhost:5032',
		},
	},
});
