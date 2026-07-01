<script lang="ts">
	import { ConstantNode, format, type MathNode, type MathScope } from "mathjs";
	import type { LiquidEffect } from "../parser/util";
	import { math } from "../parser/math";
	import { formatAssignment, populateMath } from "../parser/format";

	const { effect, variables, evaluate }: { effect: LiquidEffect; variables: MathScope; evaluate: boolean } = $props();

	const display = $derived.by(() => {
		if (effect.type === "assignment") {
			const amount = populateMath(effect.expression, variables, evaluate);
			if (effect.holder === "body" || effect.holder === "limb.body") {
				const property =
					{
						happiness: "Happiness",
						sicknessAmount: "Sickness",
						temperature: "Temperature",
						septicShock: "Sepsis",
					}[effect.field] ?? effect.field;
				//console.log(`   ${property}: ${formatAssignment(amount, effect.operator)}`);
				return `${property}: ${formatAssignment(amount, effect.operator)}`;
			} else return `   ${effect.holder}->${effect.field} ${formatAssignment(amount, effect.operator)}`;
		}
		if (effect.type === "method_call") {
			if (effect.holder === "body.talker") return;
			const amounts = effect.arguments.map(x => populateMath(x, variables, evaluate));
			if (effect.holder === "body" || effect.holder === "limb.body") {
				if (effect.method === "Eat") {
					return `Hunger: ${format(amounts[0]!)}\nWeight: ${format(amounts[1]!)}`;
				}
				const property =
					{
						Drink: "Thirst",
					}[effect.method] ?? effect.method;
				return `   ${property}: ${format(amounts[0] ?? new ConstantNode(0))}`;
			} else return `   ${effect.holder}->${effect.method} : ${amounts.join(" | ")}`;
		}
		if (effect.timer) return `   ^ [Timer: Repeated ${populateMath(effect.timer, variables, evaluate)} times]`;
		if (effect.condition) return `   ^ [Condition: ${format(math.simplify(effect.condition))}]`;
	});
</script>

<div class="effect">
	{display}
</div>
