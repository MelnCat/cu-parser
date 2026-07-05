<script lang="ts">
	import { summarizeEffects } from "../effects/effects";
	import { liquidData } from "../parser/parser";
	import { format, formatOperation } from "../parser/format";
	import { FunctionNode } from "mathjs";
	import EffectDisplay from "../components/EffectDisplay.svelte";
	import luaJson from "lua-json";
	let ml = $state(100);
	let useMl = $state(true);

	const mappedLiquids = $derived(
		liquidData
			.entries()
			.map(([l, data]) => {
				return {
					liquid: l,
					data,
					drinkEffects: summarizeEffects(data.drinkEffects, useMl ? ml : undefined),
					injectEffects: data.injectEffects && summarizeEffects(data.injectEffects, useMl ? ml : undefined),
				};
			})
			.toArray(),
	);
	const dl = () => {
		const obj = Object.fromEntries(
			liquidData
				.entries()
				.map(x => ({
					...x[1],
					liquid: x[0],
					drinkEffects: summarizeEffects(x[1].drinkEffects),
					injectEffects: x[1].injectEffects && summarizeEffects(x[1].injectEffects),
				}))
				.map(x => [x.liquid, x]),
		);
		console.log(obj);
		console.log(luaJson.format(obj));
	};
</script>

<button onclick={dl}>??</button>

<div class="input">
	<input type="number" bind:value={ml} />
	<input type="checkbox" bind:checked={useMl} />
</div>

<div class="liquids">
	{#each mappedLiquids as { liquid, data, drinkEffects, injectEffects }}
		<div class="liquid" style:--color={`#${data.color.map(x => x.toString(16).padStart(2, "0")).join("")}`}>
			<h1 class="label">{liquid}</h1>
			<div class="effects">
				<h2 class="sub-label">Drink Effects</h2>
				<div class="effect-list">
					{#each drinkEffects as effect}
						<EffectDisplay {effect} />
					{/each}
				</div>
				{#if injectEffects.length}
					<h2 class="sub-label">Inject Effects</h2>
					<div class="effect-list">
						{#each injectEffects as effect}
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
