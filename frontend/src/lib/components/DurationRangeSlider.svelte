<script lang="ts">
	import { durationMin, durationMax } from '$lib/stores/filterStore';

	const STEP = 30_000; // 30 seconds
	const SLIDER_MIN = 0;
	const SLIDER_MAX = 60 * 60_000; // 60 minutes
	const TRACK_SELECTOR = '.range-track';

	let low = $state(SLIDER_MIN);
	let high = $state(SLIDER_MAX);
	let dragging = $state<'low' | 'high' | null>(null);
	let trackEl: HTMLDivElement | undefined = $state();

	let editingLow = $state(false);
	let editingHigh = $state(false);
	let editLowValue = $state('');
	let editHighValue = $state('');

	function toPercent(value: number): number {
		return ((value - SLIDER_MIN) / (SLIDER_MAX - SLIDER_MIN)) * 100;
	}

	function clampToStep(value: number): number {
		const clamped = Math.max(SLIDER_MIN, Math.min(SLIDER_MAX, value));
		return Math.round(clamped / STEP) * STEP;
	}

	function formatDuration(ms: number): string {
		const totalSeconds = Math.floor(ms / 1000);
		const minutes = Math.floor(totalSeconds / 60);
		const seconds = totalSeconds % 60;
		return `${minutes}:${String(seconds).padStart(2, '0')}`;
	}

	function parseDuration(text: string): number | null {
		const trimmed = text.trim();
		if (!trimmed) return null;

		const parts = trimmed.split(':');
		if (parts.length === 1) {
			// Treat as minutes
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

	function filterInput(e: Event) {
		const input = e.target as HTMLInputElement;
		// Allow only digits and at most one colon
		let value = input.value.replace(/[^0-9:]/g, '');
		const colonCount = (value.match(/:/g) || []).length;
		if (colonCount > 1) {
			// Keep only the first colon
			const firstColon = value.indexOf(':');
			value = value.slice(0, firstColon + 1) + value.slice(firstColon + 1).replace(/:/g, '');
		}
		input.value = value;
	}

	function startEditLow() {
		editLowValue = formatDuration(low);
		editingLow = true;
	}

	function startEditHigh() {
		editHighValue = high === SLIDER_MAX ? '60:00' : formatDuration(high);
		editingHigh = true;
	}

	function commitLow() {
		if (!editingLow) return;
		editingLow = false;
		const parsed = parseDuration(editLowValue);
		if (parsed === null) return;
		const clamped = clampToStep(Math.min(parsed, high - STEP));
		low = Math.max(SLIDER_MIN, clamped);
		updateStore();
	}

	function commitHigh() {
		if (!editingHigh) return;
		editingHigh = false;
		const parsed = parseDuration(editHighValue);
		if (parsed === null) return;
		const clamped = clampToStep(Math.max(parsed, low + STEP));
		high = Math.min(SLIDER_MAX, clamped);
		updateStore();
	}

	function onInputKeydown(e: KeyboardEvent, commit: () => void, cancel: () => void) {
		if (e.key === 'Enter') {
			e.preventDefault();
			commit();
		} else if (e.key === 'Escape') {
			e.preventDefault();
			cancel();
		}
	}

	function updateStore() {
		durationMin.set(low === SLIDER_MIN ? null : low);
		durationMax.set(high === SLIDER_MAX ? null : high);
	}

	function resetDuration() {
		low = SLIDER_MIN;
		high = SLIDER_MAX;
		updateStore();
	}

	let isFiltered = $derived(low !== SLIDER_MIN || high !== SLIDER_MAX);

	function valueFromPointer(clientX: number): number {
		if (!trackEl) return 0;
		const rect = trackEl.getBoundingClientRect();
		const ratio = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
		return clampToStep(SLIDER_MIN + ratio * (SLIDER_MAX - SLIDER_MIN));
	}

	function onPointerDown(e: PointerEvent, thumb: 'low' | 'high') {
		dragging = thumb;
		(e.target as HTMLElement).setPointerCapture(e.pointerId);
	}

	function onPointerMove(e: PointerEvent) {
		if (!dragging) return;
		const val = valueFromPointer(e.clientX);
		if (dragging === 'low') {
			low = Math.min(val, high - STEP);
		} else {
			high = Math.max(val, low + STEP);
		}
		updateStore();
	}

	function onPointerUp() {
		dragging = null;
	}

	$effect(() => {
		if (dragging) {
			const handleMove = (e: PointerEvent) => onPointerMove(e);
			const handleUp = () => onPointerUp();
			window.addEventListener('pointermove', handleMove);
			window.addEventListener('pointerup', handleUp);
			return () => {
				window.removeEventListener('pointermove', handleMove);
				window.removeEventListener('pointerup', handleUp);
			};
		}
	});

	let lowLabel = $derived(low === SLIDER_MIN ? '0:00' : formatDuration(low));
	let highLabel = $derived(high === SLIDER_MAX ? '60:00+' : formatDuration(high));
</script>

<div class="duration-slider">
	<span class="label">Duration:</span>

	{#if editingLow}
		<input
			class="range-input range-input-low"
			type="text"
			bind:value={editLowValue}
			oninput={filterInput}
			onblur={commitLow}
			onkeydown={(e) => onInputKeydown(e, commitLow, () => (editingLow = false))}
			autofocus
		/>
	{:else}
		<button class="range-label range-label-low" onclick={startEditLow}>{lowLabel}</button>
	{/if}

	<div class="range-track" bind:this={trackEl}>
		<div
			class="range-fill"
			style="left: {toPercent(low)}%; right: {100 - toPercent(high)}%"
		></div>
		<div
			class="thumb"
			role="slider"
			tabindex="0"
			aria-label="Minimum duration"
			aria-valuemin={SLIDER_MIN}
			aria-valuemax={SLIDER_MAX}
			aria-valuenow={low}
			style="left: {toPercent(low)}%"
			onpointerdown={(e) => onPointerDown(e, 'low')}
		></div>
		<div
			class="thumb"
			role="slider"
			tabindex="0"
			aria-label="Maximum duration"
			aria-valuemin={SLIDER_MIN}
			aria-valuemax={SLIDER_MAX}
			aria-valuenow={high}
			style="left: {toPercent(high)}%"
			onpointerdown={(e) => onPointerDown(e, 'high')}
		></div>
	</div>

	{#if editingHigh}
		<input
			class="range-input range-input-high"
			type="text"
			bind:value={editHighValue}
			oninput={filterInput}
			onblur={commitHigh}
			onkeydown={(e) => onInputKeydown(e, commitHigh, () => (editingHigh = false))}
			autofocus
		/>
	{:else}
		<button class="range-label range-label-high" onclick={startEditHigh}>{highLabel}</button>
	{/if}

	{#if isFiltered}
		<button class="reset-btn" onclick={resetDuration} title="Reset duration filter">
			Reset
		</button>
	{/if}
</div>

<style>
	.duration-slider {
		display: flex;
		align-items: center;
		gap: 8px;
	}
	.label {
		color: #aaa;
		font-size: 13px;
		margin-right: 4px;
	}
	.range-label {
		background: transparent;
		border: none;
		color: #ccc;
		font-size: 12px;
		user-select: none;
		cursor: pointer;
		padding: 2px 4px;
		border-radius: 3px;
		font-family: inherit;
	}
	.range-label:hover {
		background: #333;
		color: #fff;
	}
	.range-label-low {
		text-align: right;
		min-width: 28px;
	}
	.range-label-high {
		text-align: left;
		min-width: 42px;
	}
	.range-input {
		background: #222;
		border: 1px solid #f50;
		color: #fff;
		font-size: 12px;
		font-family: inherit;
		padding: 1px 4px;
		border-radius: 3px;
		outline: none;
		text-align: center;
	}
	.range-input-low {
		width: 36px;
	}
	.range-input-high {
		width: 44px;
	}
	.range-track {
		position: relative;
		width: 140px;
		height: 4px;
		background: #333;
		border-radius: 2px;
		cursor: pointer;
		margin: 0 5px;
	}
	.range-fill {
		position: absolute;
		top: 0;
		bottom: 0;
		background: #f50;
		border-radius: 2px;
	}
	.thumb {
		position: absolute;
		top: 50%;
		width: 14px;
		height: 14px;
		background: #f50;
		border: 2px solid #fff;
		border-radius: 50%;
		transform: translate(-50%, -50%);
		cursor: grab;
		touch-action: none;
	}
	.thumb:active {
		cursor: grabbing;
	}
	.reset-btn {
		background: transparent;
		border: none;
		color: #666;
		padding: 4px 10px;
		border-radius: 4px;
		cursor: pointer;
		font-size: 13px;
	}
	.reset-btn:hover {
		color: #ccc;
	}
</style>
