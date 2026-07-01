import { OperatorNode, type MathNode } from "mathjs";
import { math } from "../parser/math";
import type { LiquidEffect } from "../parser/util";

export const summarizeEffects = (effects: { effect: LiquidEffect; ml?: number }[]) => {
	const result = new Map<string, MathNode>();
	const conditional: { key: string; value: MathNode; condition: MathNode }[] = [];
	const timer: { key: string; value: MathNode; timer: MathNode; condition?: MathNode }[] = [];
	const upsertEffect = (effect: LiquidEffect, key: string, amount: MathNode, ml?: number) => {
		const scope = ml === undefined ? {} : { ml };
		if (effect.timer) {
			timer.push({
				key,
				value: math.resolve(amount, scope),
				timer: math.resolve(effect.timer, scope),
				condition: effect.condition && math.resolve(effect.condition, scope),
			});
		} else if (effect.condition && effect.condition.toString() !== "body.TryGetComponent<Painkillers>(component)") {
			conditional.push({ key, value: math.resolve(amount, scope), condition: math.resolve(effect.condition, scope) });
		} else {
			if (result.has(key)) {
				result.set(key, new OperatorNode("+", "add", [result.get(key)!, math.resolve(amount, scope)]));
			} else {
				result.set(key, math.resolve(amount, scope));
			}
		}
	};

	for (const { effect, ml } of effects) {
		if (effect.type === "assignment") {
			if (effect.operator !== "+=" && effect.operator !== "-=") {
				console.log(effect.operator, effect.field);
				continue;
			}
			const computed = effect.operator === "-=" ? new OperatorNode("-", "unaryMinus", [effect.expression]) : effect.expression;
            const limb = matchLimb(effect.holder);
			if (
				isBody(effect.holder) ||
				effect.holder === "limb.body.GetOrAddComponent<Painkillers>()" ||
				effect.field === "antagonistAmount" ||
				effect.field === "opiateTolerance"
			) {
				upsertEffect(effect, effect.field, computed, ml);
				// temperature, happiness, sicknessAmount
			} else if (limb) {
                upsertEffect(effect, `${limb}.${effect.field}`, computed, ml)
            } else {
				console.log(`unknown effect assignment ${effect.holder} ${effect.field}`);
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
	return { effects: result, conditional, timer };
};

const isBody = (holder: string) => holder === "body" || holder.endsWith(".body");
const matchLimb = (holder: string) => {
	const index = holder.match(/limbs\[\d\]$/)?.[1];
	if (index === undefined) return null;
    return {
        0: "Head",
        1: "Upper Torso",
        2: "Lower Torso",
        3: "Right Arm",
        6: "Left Arm",
        9: "Right Leg",
        12: "Left Leg"
    }[index]
};
