<script lang="ts">
	import { summarizeEffects } from "../../effects/effects";
	import { liquidData } from "../../parser/liquid/parser";
	import { format, formatOperation } from "../../parser/format";
	import { FunctionNode } from "mathjs";
	import EffectDisplay from "../../components/EffectDisplay.svelte";
	import luaJson from "lua-json";
	import { rawTiles } from "../../parser/tile/parser";

	const tiles = rawTiles;
	const dl = () => {
		const obj = Object.fromEntries(
			liquidData
				.entries()
				.map(x => ({
					...x[1],
					liquid: x[0],
					drinkEffects: summarizeEffects(x[1].drinkEffects),
					injectEffects:
						x[1].injectEffects &&
						summarizeEffects(x[1].injectEffects),
				}))
				.map(x => [x.liquid, x]),
		);
		console.log(obj);
		console.log(luaJson.format(JSON.parse(JSON.stringify(obj))));
	};
</script>

<button onclick={dl}>??</button>


<div class="liquids">
	{#each rawTiles.values() as { key, health, id }}
		<div class="liquid">
			<h1 class="label">{key}</h1>
            <div><b>ID</b>: {id}</div>
            <div><b>Health</b>: {health}</div>
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
