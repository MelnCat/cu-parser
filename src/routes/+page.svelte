<script lang="ts">
	import EffectDisplay from "../components/EffectDisplay.svelte";
	import { liquidData } from "../parser/parser";

	let ml = $state(100);
</script>

<input bind:value={ml} />

<div class="liquids">
	{#each liquidData as [liquid, data]}
		<div class="liquid" style:--color={`#${data.color.map(x => x.toString(16).padStart(2, "0")).join("")}`}>
			<h1 class="label">{liquid}</h1>
			<div class="effects">
				<h2 class="sub-label">Drink Effects</h2>
				<div class="effect-list">
					{#each data.drinkEffects as effect}
						<EffectDisplay {effect} variables={{ ml }} evaluate={false} />
					{/each}
				</div>
			</div>
		</div>
	{/each}
</div>

<style>
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
