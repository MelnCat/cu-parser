<script lang="ts">
	import { SvelteMap } from "svelte/reactivity";
	import EffectDisplay from "../../components/EffectDisplay.svelte";
	import { liquidData } from "../../parser/parser";

	const MAX_VOLUME = 10000;
	const contents: { liquid: string; amount: number }[] = $state([]);
</script>

<div class="contents">
	{#each contents as { liquid, amount }, i}
		<div class="liquid-control" style:--color={liquidData.get(liquid)!.hexColor}>
			<select bind:value={contents[i].liquid}>
				{#each liquidData as [l]}
					<option value={l}>{l}</option>
				{/each}
			</select>
			<input bind:value={contents[i].amount} />
		</div>
	{/each}
	<div class="add-control">
		<button onclick={() => contents.push({ liquid: "water", amount: 1000 })}>+</button>
	</div>
</div>

<style>
	.liquid-control {
		background-color: oklch(from var(--color) 0.9 c h);
	}
</style>
