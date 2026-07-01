<script lang="ts">
	import { ConstantNode, format, type MathNode, type MathScope } from "mathjs";
	import type { LiquidEffect } from "../parser/util";
	import { math } from "../parser/math";
	import { formatAssignment } from "../parser/format";

	const { effect, variables, evaluate }: { effect: LiquidEffect; variables: MathScope; evaluate: boolean } = $props();

	const populateMath = (node: MathNode) => {
		try {
			return evaluate
				? new ConstantNode(node.evaluate(variables))
				: math.simplify(math.resolve(node, variables), {}, { exactFractions: false });
		} catch {
			return math.simplify(math.resolve(node, variables), {}, { exactFractions: false });
		}
	};

	const display = $derived.by(() => {
		if (effect.type === "assignment") {
			const amount = populateMath(effect.expression);
			if (effect.holder === "body" || effect.holder === "limb.body") {
				const property =
					{
						happiness: "Happiness",
						sicknessAmount: "Sickness",
						temperature: "Temperature",
						septicShock: "Sepsis",
					}[effect.field] ?? effect.field;
				console.log(`   ${property}: ${formatAssignment(amount, effect.operator)}`);
                return `${property}: ${formatAssignment(amount, effect.operator)}`;
			} else console.log(`   ${effect.holder}->${effect.field} ${effect.operator} ${formatAssignment(amount, effect.operator)}`);
		}
		if (effect.type === "method_call") {
			if (effect.holder === "body.talker") return;
			const amounts = effect.arguments.map(x => populateMath(x));
			if (effect.holder === "body" || effect.holder === "limb.body") {
				if (effect.method === "Eat") {
					console.log(`   Hunger: ${format(amounts[0]!)}`);
					console.log(`   Weight: ${format(amounts[1]!)}`);
					return;
				}
				const property =
					{
						Drink: "Thirst",
					}[effect.method] ?? effect.method;
				console.log(`   ${property}: ${format(amounts[0] ?? new ConstantNode(0))}`);
			} else console.log(`   ${effect.holder}->${effect.method} : ${amounts.join(" | ")}`);
		}
		if (effect.timer) console.log(`   ^ [Timer: Repeated ${populateMath(effect.timer)} times]`);
		if (effect.condition) console.log(`   ^ [Condition: ${format(math.simplify(effect.condition))}]`);
	});
</script>

<div class="effect">
	{display}
</div>
