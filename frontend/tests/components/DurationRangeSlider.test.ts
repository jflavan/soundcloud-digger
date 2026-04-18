import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import { get } from 'svelte/store';
import DurationRangeSlider from '$lib/components/DurationRangeSlider.svelte';
import { durationMin, durationMax } from '$lib/stores/filterStore';

describe('DurationRangeSlider', () => {
	beforeEach(() => {
		durationMin.set(null);
		durationMax.set(null);
	});

	it('renders the default labels', () => {
		render(DurationRangeSlider);
		expect(screen.getByText('0:00')).toBeTruthy();
		expect(screen.getByText('60:00+')).toBeTruthy();
	});

	it('does not show the Reset button by default', () => {
		render(DurationRangeSlider);
		expect(screen.queryByText('Reset')).toBeNull();
	});

	it('clicking the low label enters edit mode and commits a valid value on blur', async () => {
		render(DurationRangeSlider);
		await fireEvent.click(screen.getByText('0:00'));
		const input = document.querySelector('.range-input-low') as HTMLInputElement;
		expect(input).toBeTruthy();
		input.value = '2:00';
		await fireEvent.input(input);
		await fireEvent.blur(input);
		expect(get(durationMin)).toBe(120_000);
	});

	it('clicking the high label enters edit mode and commits on Enter', async () => {
		render(DurationRangeSlider);
		await fireEvent.click(screen.getByText('60:00+'));
		const input = document.querySelector('.range-input-high') as HTMLInputElement;
		expect(input).toBeTruthy();
		input.value = '5:00';
		await fireEvent.input(input);
		await fireEvent.keyDown(input, { key: 'Enter' });
		expect(get(durationMax)).toBe(300_000);
	});

	it('pressing Escape cancels editing without committing', async () => {
		render(DurationRangeSlider);
		await fireEvent.click(screen.getByText('0:00'));
		const input = document.querySelector('.range-input-low') as HTMLInputElement;
		input.value = '10:00';
		await fireEvent.input(input);
		await fireEvent.keyDown(input, { key: 'Escape' });
		expect(get(durationMin)).toBeNull();
	});

	it('invalid input is ignored on commit', async () => {
		render(DurationRangeSlider);
		await fireEvent.click(screen.getByText('0:00'));
		const input = document.querySelector('.range-input-low') as HTMLInputElement;
		input.value = 'bogus';
		await fireEvent.input(input);
		await fireEvent.blur(input);
		expect(get(durationMin)).toBeNull();
	});

	it('input sanitization strips non-numeric characters', async () => {
		render(DurationRangeSlider);
		await fireEvent.click(screen.getByText('0:00'));
		const input = document.querySelector('.range-input-low') as HTMLInputElement;
		input.value = '1a:2b3';
		await fireEvent.input(input);
		expect(input.value).toBe('1:23');
	});

	it('Reset button appears once a filter is applied and clears it', async () => {
		render(DurationRangeSlider);
		await fireEvent.click(screen.getByText('0:00'));
		const input = document.querySelector('.range-input-low') as HTMLInputElement;
		input.value = '1:00';
		await fireEvent.input(input);
		await fireEvent.blur(input);

		const resetBtn = screen.getByText('Reset');
		expect(resetBtn).toBeTruthy();
		await fireEvent.click(resetBtn);
		expect(get(durationMin)).toBeNull();
		expect(get(durationMax)).toBeNull();
	});
});
