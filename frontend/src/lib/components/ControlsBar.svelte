<script lang="ts">
	import type { SortBy, TimeRange, TimeField } from '$lib/types';
	import { sortBy, timeRange, selectedGenres, excludedGenres, timeField } from '$lib/stores/filterStore';
	import { availableGenres } from '$lib/stores/filteredFeedStore';
	import DurationRangeSlider from './DurationRangeSlider.svelte';

	let dropdownOpen = $state(false);
	let excludeDropdownOpen = $state(false);

	const sortOptions: { value: SortBy; label: string }[] = [
		{ value: 'likes', label: 'Likes' },
		{ value: 'plays', label: 'Plays' },
		{ value: 'reposts', label: 'Reposts' },
		{ value: 'comments', label: 'Comments' },
		{ value: 'date', label: 'Date' },
	];

	const timeOptions: { value: TimeRange; label: string }[] = [
		{ value: '24h', label: '24h' },
		{ value: '7d', label: '7d' },
		{ value: '30d', label: '30d' },
		{ value: 'all', label: 'All' },
	];

	function toggleGenre(genre: string) {
		selectedGenres.update((current) => {
			if (current.includes(genre)) {
				return current.filter((g) => g !== genre);
			}
			return [...current, genre];
		});
	}

	function toggleExcludedGenre(genre: string) {
		excludedGenres.update((current) => {
			if (current.includes(genre)) {
				return current.filter((g) => g !== genre);
			}
			return [...current, genre];
		});
	}
</script>

<div class="controls-bar">
	<div class="control-group">
		<span class="label">Sort:</span>
		{#each sortOptions as opt}
			<button
				class="toggle"
				class:active={$sortBy === opt.value}
				onclick={() => sortBy.set(opt.value)}
			>
				{opt.label}
			</button>
		{/each}
	</div>

	<div class="control-group">
		<span class="label">Time:</span>
		{#each timeOptions as opt}
			<button
				class="toggle"
				class:active={$timeRange === opt.value}
				onclick={() => timeRange.set(opt.value)}
			>
				{opt.label}
			</button>
		{/each}
		<span class="separator"></span>
		<button
			class="toggle"
			class:active={$timeField === 'feed'}
			onclick={() => timeField.set('feed')}
		>
			In feed
		</button>
		<button
			class="toggle"
			class:active={$timeField === 'uploaded'}
			onclick={() => timeField.set('uploaded')}
		>
			Uploaded
		</button>
	</div>

	<div class="control-group">
		<DurationRangeSlider />
	</div>

	{#if $availableGenres.length > 0}
		<div class="control-group genre-group">
			<span class="label">Genre:</span>
			<div class="genre-dropdown">
				<button class="dropdown-toggle" onclick={() => (dropdownOpen = !dropdownOpen)}>
					{$selectedGenres.length === 0
						? 'All genres'
						: `${$selectedGenres.length} selected`}
				</button>
				{#if dropdownOpen}
					<div class="dropdown-menu">
						{#each $availableGenres as genre}
							<label class="dropdown-item">
								<input
									type="checkbox"
									checked={$selectedGenres.includes(genre)}
									onchange={() => toggleGenre(genre)}
								/>
								{genre}
							</label>
						{/each}
					</div>
				{/if}
			</div>
		</div>

		<div class="control-group genre-group">
			<span class="label">Exclude:</span>
			<div class="genre-dropdown">
				<button class="dropdown-toggle" onclick={() => (excludeDropdownOpen = !excludeDropdownOpen)}>
					{$excludedGenres.length === 0
						? 'None'
						: `${$excludedGenres.length} excluded`}
				</button>
				{#if excludeDropdownOpen}
					<div class="dropdown-menu">
						{#each $availableGenres as genre}
							<label class="dropdown-item">
								<input
									type="checkbox"
									checked={$excludedGenres.includes(genre)}
									onchange={() => toggleExcludedGenre(genre)}
								/>
								{genre}
							</label>
						{/each}
					</div>
				{/if}
			</div>
		</div>
	{/if}
</div>

<style>
	.controls-bar {
		display: flex;
		gap: 16px;
		align-items: center;
		flex-wrap: wrap;
		padding: 12px 16px;
		background: #1a1a1a;
		border-radius: 8px;
	}
	.control-group {
		display: flex;
		align-items: center;
		gap: 4px;
	}
	.label {
		color: #aaa;
		font-size: 13px;
		margin-right: 4px;
	}
	.toggle {
		background: transparent;
		border: none;
		color: #666;
		padding: 4px 10px;
		border-radius: 4px;
		cursor: pointer;
		font-size: 13px;
	}
	.toggle.active {
		background: #f50;
		color: white;
	}
	.separator {
		width: 1px;
		height: 16px;
		background: #333;
		margin: 0 4px;
	}
	.genre-group {
		position: relative;
	}
	.dropdown-toggle {
		background: transparent;
		border: 1px solid #333;
		color: #aaa;
		padding: 4px 12px;
		border-radius: 4px;
		cursor: pointer;
		font-size: 13px;
	}
	.dropdown-menu {
		position: absolute;
		top: 100%;
		left: 0;
		background: #1a1a1a;
		border: 1px solid #333;
		border-radius: 6px;
		padding: 8px 0;
		max-height: 240px;
		overflow-y: auto;
		z-index: 10;
		min-width: 180px;
		margin-top: 4px;
	}
	.dropdown-item {
		display: flex;
		align-items: center;
		gap: 8px;
		padding: 4px 12px;
		color: #ccc;
		font-size: 13px;
		cursor: pointer;
	}
	.dropdown-item:hover {
		background: #222;
	}
</style>
