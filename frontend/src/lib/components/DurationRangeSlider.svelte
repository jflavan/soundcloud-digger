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

	function updateStore() {
		durationMin.set(low === SLIDER_MIN ? null : low);
		durationMax.set(high === SLIDER_MAX ? null : high);
	}

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
	<span class="range-label range-label-low">{lowLabel}</span>
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
	<span class="range-label range-label-high">{highLabel}</span>
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
		color: #ccc;
		font-size: 12px;
		user-select: none;
	}
	.range-label-low {
		text-align: right;
		min-width: 28px;
	}
	.range-label-high {
		text-align: left;
		min-width: 42px;
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
		background: #e94560;
		border-radius: 2px;
	}
	.thumb {
		position: absolute;
		top: 50%;
		width: 14px;
		height: 14px;
		background: #e94560;
		border: 2px solid #fff;
		border-radius: 50%;
		transform: translate(-50%, -50%);
		cursor: grab;
		touch-action: none;
	}
	.thumb:active {
		cursor: grabbing;
	}
</style>
