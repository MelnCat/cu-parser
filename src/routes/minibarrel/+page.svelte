<script lang="ts">
	import { SvelteMap } from "svelte/reactivity";
	import { liquidData } from "../../parser/parser";
	import type { LiquidEffect } from "../../parser/types";
	import { FunctionNode, OperatorNode } from "mathjs";
	import { format } from "../../parser/format";
	import { summarizeEffects } from "../../effects/effects";

	const MAX_VOLUME = 10000;
	const DRINK_VOLUME = 100;

	const contents: { liquid: string; amount: number }[] = $state([
		{ liquid: "biochem", amount: 1500 },
		{ liquid: "water", amount: 144 },
		{ liquid: "oil", amount: 123 },
		{ liquid: "epinephrine", amount: 1000 },
	]);
	const total = $derived(contents.map(x => x.amount).reduce((l, c) => l + c, 0));

	const barrelViewImage = $derived.by(() => {
		let gradient: string[] = [];
		let last = 0;
		for (const stack of contents) {
			const l = liquidData.get(stack.liquid)!;
			const current = last + stack.amount / MAX_VOLUME;
			gradient.push(`${l.hexColor} ${last * 100}%`, `${l.hexColor} ${current * 100}%`);
			last = current;
		}
		if (last !== 1) {
			gradient.push(`transparent ${last * 100}%`, `transparent 100% `);
		}
		return `linear-gradient(0deg, ${gradient.join(", ")})`;
	});

	const summary = $derived.by(() => {
		const toSummarize: { effect: LiquidEffect; ml: number }[] = [];

		const num = Math.min(DRINK_VOLUME, total);

		for (const { liquid, amount } of contents) {
			const liquidDrink = amount * (num / total);
			for (const effect of liquidData.get(liquid)!.drinkEffects) {
				toSummarize.push({ effect, ml: liquidDrink });
			}
		}

		console.log(summarizeEffects(toSummarize));
		return summarizeEffects(toSummarize);
	});
</script>

<main>
	<div class="left-panel">
		<div class="liquid-contents">
			{#each contents as { liquid, amount }, i}
				<div class="liquid-control control" style:--color={liquidData.get(liquid)!.hexColor}>
					<select bind:value={contents[i].liquid}>
						{#each liquidData as [l]}
							<option value={l}>{l}</option>
						{/each}
					</select>
					<input type="number" bind:value={contents[i].amount} />
					<button
						onclick={() => {
							contents.splice(i, 1);
						}}>-</button
					>
				</div>
			{/each}
			<div class="add-control control">
				<button onclick={() => contents.push({ liquid: "water", amount: 1000 })}>+</button>
			</div>
		</div>
	</div>
	<div class="center-panel">
		<div class="barrel-view" style:background-image={barrelViewImage}></div>
	</div>
	<div class="right-panel">
		<div class="effect-list">
			{#each summary.effects as { key, value }}
				<div class="effect">
					{key}: {format(value)}
				</div>
			{/each}
		</div>
		<div class="effect-list">
			{#each summary.conditional as { key, value, condition }}
				<div class="effect">
					<div>{key}: {format(value)}</div>
					<div class="condition">↳ CONDITION: {format(condition)}</div>
				</div>
			{/each}
		</div>
		<div class="effect-list">
			{#each summary.timer as { key, value, timer, condition }}
				{@const activeTime = new FunctionNode("ceil", [timer])}
				<div class="effect">
					<div>{key}: {format(new OperatorNode("*", "multiply", [value, activeTime]))} over {format(activeTime)} seconds</div>
					{#if condition}
						<div class="condition">↳ CONDITION: {format(condition)}</div>
					{/if}
				</div>
			{/each}
		</div>
	</div>
</main>

<style>
	main {
		padding: 1em;
		display: flex;
		gap: 1em;
		button {
			border-radius: 0;
			padding: 0.1em 0.4em;
			border-width: 1px;
		}
	}
	.liquid-contents {
		display: flex;
		flex-direction: column;
		width: 30em;
	}
	.control {
		padding: 0.5em;
	}
	.liquid-control {
		background-color: oklch(from var(--color) 0.9 c h);
	}
	.barrel-view {
		width: 8em;
		height: 20em;
		border: 0.6em solid #6a6a6a;
	}
	.condition {
		font-size: 0.8em;
		margin-left: 1em;
	}
</style>
