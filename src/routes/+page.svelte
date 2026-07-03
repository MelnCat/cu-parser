<script lang="ts">
	import { summarizeEffects } from "../effects/effects";
	import { liquidData } from "../parser/parser";
	import { format } from "../parser/format";
	import { FunctionNode } from "mathjs";

	let ml = $state(100);
	let useMl = $state(true);

	const mappedLiquids = $derived(
		liquidData
			.entries()
			.map(([l, data]) => {
				return {
					liquid: l,
					data,
					drinkEffects: summarizeEffects(data.drinkEffects.map(x => ({ effect: x, ml: useMl ? ml : undefined }))),
					injectEffects:
						data.injectEffects && summarizeEffects(data.injectEffects.map(x => ({ effect: x, ml: useMl ? ml : undefined }))),
				};
			})
			.toArray(),
	);
</script>

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
					{#each drinkEffects.effects as { key, value }}
						<div>{key}: {format(value)}</div>
					{/each}
					{#each drinkEffects.conditional as { key, value, condition }}
						<div>{key}: {format(value)}</div>
						<div class="condition">Condition: {format(condition)}</div>
					{/each}
					{#each drinkEffects.timer as { key, value, timer, condition }}
						{@const duration = new FunctionNode("ceil", [timer])}
						<div>{key}: {format(value)} / s</div>
						{#if condition}
							<div class="condition">Condition: {format(condition)}</div>
						{/if}
						<div class="condition">Duration: {format(duration)}s</div>
					{/each}
				</div>
				{#if injectEffects.length}
					<h2 class="sub-label">Inject Effects</h2>
					<div class="effect-list">
						{#each injectEffects.effects as { key, value }}
							<div>{key}: {format(value)}</div>
						{/each}
						{#each injectEffects.conditional as { key, value, condition }}
							<div>{key}: {format(value)}</div>
							<div class="condition">Condition: {format(condition)}</div>
						{/each}
						{#each injectEffects.timer as { key, value, timer, condition }}
							{@const duration = new FunctionNode("ceil", [timer])}
							<div>{key}: {format(value)} / s</div>
							{#if condition}
								<div class="condition">Condition: {format(condition)}</div>
							{/if}
							<div class="condition">Duration: {format(duration)}s</div>
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
	.condition {
		font-size: 0.8em;
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
