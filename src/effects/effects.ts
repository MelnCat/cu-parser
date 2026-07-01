import { OperatorNode, type MathNode } from "mathjs";
import { math } from "../parser/math";
import type { LiquidEffect } from "../parser/util";

export const summarizeEffects = (effects: { effect: LiquidEffect; ml: number }[]) => {
	const result = new Map<string, MathNode>();
	const conditional: { key: string; value: MathNode; condition: MathNode }[] = [];
	const timer: { key: string; value: MathNode; timer: MathNode; condition?: MathNode }[] = [];
	const upsertEffect = (effect: LiquidEffect, key: string, amount: MathNode, ml: number) => {
		if (effect.timer) {
			timer.push({
				key,
				value: math.resolve(amount, { ml }),
				timer: math.resolve(effect.timer, { ml }),
				condition: effect.condition && math.resolve(effect.condition, { ml }),
			});
		} else if (effect.condition && effect.condition.toString() !== "body.TryGetComponent<Painkillers>(component)") {
			conditional.push({ key, value: math.resolve(amount, { ml }), condition: math.resolve(effect.condition, { ml }) });
		} else {
			if (result.has(key)) {
				result.set(key, new OperatorNode("+", "add", [result.get(key)!, math.resolve(amount, { ml })]));
			} else {
				result.set(key, math.resolve(amount, { ml }));
			}
		}
	};

	for (const { effect, ml } of effects) {
		if (effect.type === "assignment") {
			if (effect.operator !== "+=" && effect.operator !== "-=") {
				console.log(effect.operator, effect.field);
				continue;
			}
			const computed =
				effect.operator === "-="
					? new OperatorNode("-", "unaryMinus", [effect.expression])
					: effect.expression;
			if (
				isBody(effect.holder) ||
				effect.holder === "limb.body.GetOrAddComponent<Painkillers>()" ||
				effect.field === "antagonistAmount" ||
				effect.field === "opiateTolerance"
			) {
				upsertEffect(effect, effect.field, computed, ml);
				// temperature, happiness, sicknessAmount
			}
		} else if (effect.type === "method_call") {
            if (effect.method === "Drink") {
                upsertEffect(effect, "thirst", effect.arguments[0], ml);
            }
            if (effect.method === "Eat") {
                upsertEffect(effect, "hunger", effect.arguments[0], ml);
                upsertEffect(effect, "weightOffset", effect.arguments[1], ml);
            }
        }
	}
	for (const [k, v] of result) {
		console.log(`${k}: ${v}`);
	}

	return { effects: result, conditional, timer };
};

const isBody = (holder: string) => holder === "body" || holder.endsWith(".body");
