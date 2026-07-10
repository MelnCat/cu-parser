<script lang="ts">
	import { summarizeEffects } from "../../effects/effects";
	import { format, formatOperation } from "../../parser/format";
	import { FunctionNode } from "mathjs";
	import EffectDisplay from "../../components/EffectDisplay.svelte";
	import luaJson from "lua-json";
	import { itemData } from "../../parser/item/parser";
	let ml = $state(100);
	let useMl = $state(true);

	const mappedLiquids = $derived(
		itemData
			.entries()
			.map(([l, data]) => {
				return {
					liquid: l,
					data,
					useAction:
						data.useAction &&
						summarizeEffects(data.useAction),
					useLimbAction:
						data.useLimbAction &&
						summarizeEffects(data.useLimbAction),
				};
			})
			.toArray(),
	);
	const dl = () => {
		const obj = Object.fromEntries(
			itemData
				.entries()
				.map(x => ({
					...x[1],
					key: x[0],
					useAction:
						x[1].useAction &&
						summarizeEffects(x[1].useAction),
					useLimbAction:
						x[1].useLimbAction &&
						summarizeEffects(x[1].useLimbAction),
				}))
				.map(x => [x.key, x]),
		);
		console.log(obj);
		console.log(luaJson.format(JSON.parse(JSON.stringify(obj))));
	};
</script>

<button onclick={dl}>??</button>

<div class="input">
	<input type="number" bind:value={ml} />
	<input type="checkbox" bind:checked={useMl} />
</div>

<div class="liquids">
	{#each mappedLiquids as { liquid, data, useAction, useLimbAction }}
		<div class="liquid">
			<h1 class="label">{liquid}</h1>
			<div class="effects">
				{#if useAction?.length}
					<h2 class="sub-label">Use Effects</h2>
					<div class="effect-list">
						{#each useAction as effect}
							<EffectDisplay {effect} />
						{/each}
					</div>
				{/if}
				{#if useLimbAction?.length}
					<h2 class="sub-label">Use Limb Effects</h2>
					<div class="effect-list">
						{#each useLimbAction as effect}
							<EffectDisplay {effect} />
						{/each}
					</div>
				{/if}
			</div>
		</div>
	{/each}
</div>

<style>
	.input {
		position: sticky;
		top: 0;
		left: 0;
	}
	.effect-list {
		margin-left: 1em;
	}
	.liquids {
		display: flex;
		margin: 1em;
		flex-wrap: wrap;
		gap: 1em;
		justify-content: center;
	}
	.liquid {
		width: 20em;
		background-color: #eeeeee;
		border-left: 0.4em solid var(--color);
		height: 20em;
		padding: 0.5em;
		overflow-y: auto;
	}
	.label {
		font-size: 1.2em;
		text-transform: uppercase;
		text-align: center;
		-webkit-text-stroke: 0.3em oklch(from var(--color) 0.96 c h);
		paint-order: stroke fill;
		margin-bottom: 0.2em;
	}
	.sub-label {
		text-transform: uppercase;
		font-size: 1em;
	}
</style>
