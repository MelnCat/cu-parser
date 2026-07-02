import { OperatorNode, type MathNode } from "mathjs";
import { math } from "../parser/math";
import type { LiquidEffect } from "../parser/types";

export interface SummarizedEffect {
	key: string;
	field: string;
	holder?: string;
	value: MathNode;
}
export interface Summary {
	effects: SummarizedEffect[];
	conditional: (SummarizedEffect & { condition: MathNode })[];
	timer: (SummarizedEffect & { condition?: MathNode; timer: MathNode })[];
}

export const summarizeEffects = (effects: { effect: LiquidEffect; ml?: number }[]): Summary => {
	const result = new Map<string, SummarizedEffect>();
	const conditional: (SummarizedEffect & { condition: MathNode })[] = [];
	const timer: (SummarizedEffect & { condition?: MathNode; timer: MathNode })[] = [];

	for (const { effect, ml } of effects) {
		const upsertEffect = (key: string, amount: MathNode, field: string, holder?: string) => {
			const scope = ml === undefined ? {} : { ml };
			if (effect.timer) {
				timer.push({
					key,
					value: math.resolve(amount, scope),
					timer: math.resolve(effect.timer, scope),
					condition: effect.condition && math.resolve(effect.condition, scope),
					field,
					holder,
				});
			} else if (effect.condition && effect.condition.toString() !== "body.TryGetComponent<Painkillers>(component)") {
				conditional.push({
					key,
					field,
					holder,
					value: math.resolve(amount, scope),
					condition: math.resolve(effect.condition, scope),
				});
			} else {
				if (result.has(key)) {
					result.set(key, {
						key,
						field,
						holder,
						value: new OperatorNode("+", "add", [result.get(key)!.value, math.resolve(amount, scope)]),
					});
				} else {
					result.set(key, { key, field, holder, value: math.resolve(amount, scope) });
				}
			}
		};
		if (effect.type === "assignment") {
			const key = `${effect.holder}.${effect.field}`;
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
				effect.field === "opiateTolerance" ||
                effect.field === "opiateAmount"
			) {
				upsertEffect(effect.field, computed, effect.field, effect.holder);
				// temperature, happiness, sicknessAmount
			} else if (limb) {
				upsertEffect(key, computed, effect.field, effect.holder);
			} else {
				console.log(`unknown effect assignment ${effect.holder} ${effect.field}`);
			}
		} else if (effect.type === "method_call") {
			if (effect.method === "Drink") {
				upsertEffect("thirst", effect.arguments[0], "thirst");
			}
			if (effect.method === "Eat") {
				upsertEffect("hunger", effect.arguments[0], "hunger");
				upsertEffect("weightOffset", effect.arguments[1], "weightOffset");
			}
		}
	}
	return { effects: result.values().toArray(), conditional, timer };
};

const isBody = (holder: string) => holder === "body" || holder.endsWith(".body");
const matchLimb = (holder: string) => {
	const index = holder.match(/limbs\[(\d)\]$/)?.[1];
	if (index === undefined) return null;
	return {
		0: "Head",
		1: "Upper Torso",
		2: "Lower Torso",
		3: "Right Arm",
		6: "Left Arm",
		9: "Right Leg",
		12: "Left Leg",
	}[index];
};
