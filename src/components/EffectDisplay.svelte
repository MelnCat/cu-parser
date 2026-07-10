<script lang="ts">
	import { ConstantNode, FunctionNode, OperatorNode, type MathNode } from "mathjs";
	import { matchLimb, type AnySummarizedEffect } from "../effects/effects";
	import { format, formatOperation } from "../parser/format";
	import { match, P } from "ts-pattern";

	const { effect }: { effect: AnySummarizedEffect } = $props();

	const { operation, field, key, holder } = $derived(effect);

	const mult = (x: MathNode, y: number) => new OperatorNode("*", "multiply", [x, new ConstantNode(y)]);

	const HOLDER_FIELD_LABELS: Record<string, Record<string, string>> = {
		body: {
			temperature: "Temperature",
			happiness: "Happiness",
			consciousness: "Consciousness",
			sicknessAmount: "Sickness",
			adrenaline: "Adrenaline",
			heartRate: "Heart Rate",
			fibrillationProgress: "Fibrillation",
			shock: "Pain Shock",
			antibioticImmunityTime: "Antibiotic Time",
			weightOffset: "Weight",
			hunger: "Hunger",
			thirst: "Thirst",
			stamina: "Stamina",
			vomit: "Vomit",
		},
		_: {
			disinfect: "Disinfect Time",
			mindwipe: "Mindwipe",
			fibrillate: "Start Fibrillation",
		},
	};
	const FIELD_LABELS: Record<string, string> = {
		opiateAmount: "Opiate",
		antagonistAmount: "Antagonist",
		opiateTolerance: "Opiate Tolerance",
	};
	const LIMB_FIELD_LABELS: Record<string, string> = {
		pain: "Pain",
		muscleHealth: "Muscle Health",
		skinHealth: "Skin Health",
	};

	const limb = $derived(holder && holder.startsWith("limb_") ? +holder.replace("limb_", "") : null);
	const displayKey = $derived.by(() => {
		if (limb) {
			return field in LIMB_FIELD_LABELS
				? `${
						{
                            [-1]: "Limb",
							0: "Head",
							1: "Upper Torso",
							2: "Lower Torso",
							3: "Right Arm",
							6: "Left Arm",
							9: "Right Leg",
							12: "Left Leg",
						}[limb]
					} ${LIMB_FIELD_LABELS[field]}`
				: `${holder}_${field}`;
		} else {
			return HOLDER_FIELD_LABELS[holder ?? "_"]?.[field] ?? FIELD_LABELS[field] ?? `${holder}_${field}`;
		}
	});
	const displayValue = $derived.by(() => {
		if (operation.type !== "call" && operation.value.toString().includes("ml")) {
			return formatOperation(operation);
		}
		const [modifier, suffix] = match([holder, field])
			.with(["body", "temperature"], () => [null, "°C"] as const)
			.with(["body", "weightOffset"], () => [0.34, "kg"] as const)
			.with([P._, "antibioticImmunityTime"], () => [0.01, "s"] as const)
			.with([P._, "pain"], () => [0.01, null] as const)
			.otherwise(() => [null, null] as const);

		const formatted = modifier ? formatOperation(operation, x => mult(x, modifier)) : formatOperation(operation);

		return `${formatted}${suffix ?? ""}`;
	});
</script>

<div class="effect">
	{#if effect.timer}
		{@const duration = new FunctionNode("ceil", [effect.timer])}
		<div><b>{displayKey}</b>: {displayValue} every second</div>
		{#if effect.condition}
			<div class="condition">Condition: {format(effect.condition)}</div>
		{/if}
		<div class="condition">Duration: {format(duration)}s</div>
	{:else}
		<div><b>{displayKey}</b>: {displayValue}</div>
		{#if effect.condition}
			<div class="condition">Condition: {format(effect.condition)}</div>
		{/if}
	{/if}
</div>

<style>
	.condition {
		font-size: 0.8em;
		margin-left: 1em;
	}
</style>
