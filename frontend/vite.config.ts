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
		proxy: {
			'/api': 'http://localhost:5000',
			'/auth': 'http://localhost:5000',
		},
	},
});
