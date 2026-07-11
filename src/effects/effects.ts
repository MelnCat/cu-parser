import { ConstantNode, isFunctionNode, OperatorNode, orDependencies, SymbolNode, type MathNode } from "mathjs";
import { math } from "../parser/math";
import type { RawEffect } from "../parser/types";
import { match, P } from "ts-pattern";

export interface NumericOperation {
	type: "add" | "multiply" | "set";
	value: MathNode;
}
export interface CallOperation {
	type: "call";
}

export type EffectOperation = NumericOperation | CallOperation;

export interface SummarizedEffect {
	key: string;
	field: string;
	holder: string | null;
	operation: EffectOperation;
}
export interface AnySummarizedEffect extends SummarizedEffect {
	condition?: MathNode;
	timer?: MathNode;
}

export const summarizeEffects = (effects: RawEffect[], ml?: number | number[]): AnySummarizedEffect[] => {
	const result = new Map<string, SummarizedEffect>();
	const conditional: (SummarizedEffect & { condition: MathNode })[] = [];
	const timer: (SummarizedEffect & { condition?: MathNode; timer: MathNode })[] = [];

	for (const [i, effect] of effects.entries()) {
		const upsertEffect = ({
			field,
			key = field,
			operator,
			holder = null,
			amount,
		}: {
			key?: string;
			field: string;
			operator: string;
			holder?: string | null;
			amount?: MathNode;
		}) => {
			const type = match(operator)
				.with("+=", "-=", () => "add" as const)
				.with("*=", "/=", () => "multiply" as const)
				.with("=", () => "set" as const)
				.with("()", () => "call" as const)
				.otherwise(() => {
					throw new Error(`Unknown operator ${operator}`);
				});
			const scope = ml === undefined ? {} : typeof ml === "number" ? { ml } : { ml: ml[i] };

			if (!amount || type === "call") {
				if (type !== "call") {
					throw new Error(`what ${key} ${field} ${operator} ${amount}`);
				}
				if (effect.timer) {
					timer.push({
						key,
						operation: { type },
						timer: math.resolve(effect.timer, scope),
						field,
						holder,
						...(effect.condition ? { condition: math.resolve(effect.condition, scope) } : null),
					});
				} else if (effect.condition) {
					conditional.push({
						key,
						field,
						holder,
						operation: { type },
						condition: math.resolve(effect.condition, scope),
					});
				} else {
					result.set(key, { key, field, holder, operation: { type } });
				}

				return;
			}

			const value = math.resolve(
				match(operator)
					.with("-=", () => new OperatorNode("-", "unaryMinus", [amount]))
					.with("/=", () => new OperatorNode("/", "divide", [new ConstantNode(1), amount]))
					.otherwise(() => amount),
				scope,
			);

			if (effect.timer) {
				timer.push({
					key,
					operation: { type, value },
					timer: math.resolve(effect.timer, scope),
					field,
					holder,
					...(effect.condition ? { condition: math.resolve(effect.condition, scope) } : null),
				});
			} else if (effect.condition && effect.condition.toString() !== "body.TryGetComponent<Painkillers>(component)") {
				conditional.push({
					key,
					field,
					holder,
					operation: { type, value },
					condition: math.resolve(effect.condition, scope),
				});
			} else {
				const existing = result.get(key);
				if (!existing || existing.operation.type === "call") {
					result.set(key, { key, field, holder, operation: { type, value } });
					return;
				}

				const last = existing.operation;
				match([last.type, type])
					.with([P.union("add", "set"), "add"], () => {
						last.value = new OperatorNode("+", "add", [last.value, value]);
					})
					.with([P.union("multiply", "set"), "multiply"], () => {
						last.value = new OperatorNode("*", "multiply", [last.value, value]);
					})
					.with([P._, "set"], () => {
						last.type = "set";
						last.value = value;
					})
					.with(["add", "multiply"], () => {
						last.type = "set";
						last.value = new OperatorNode("*", "multiply", [
							new OperatorNode("+", "add", [new SymbolNode("current"), last.value]),
							value,
						]);
					})
					.with(["multiply", "add"], () => {
						last.type = "set";
						last.value = new OperatorNode("+", "add", [
							new OperatorNode("*", "multiply", [new SymbolNode("current"), last.value]),
							value,
						]);
					})
					.exhaustive();
			}
		};

		const holder = parseHolder(effect.holder);
		if (effect.type === "assignment") {
			upsertEffect({ field: effect.field, operator: effect.operator, holder, amount: effect.expression });
		} else if (effect.type === "method_call") {
			match(effect.method)
				.with("Drink", () => {
                    if (effect.holder === "item.GetComponent<WaterContainerItem>()") {
                        upsertEffect({ operator: "()", amount: effect.arguments[1] ?? new ConstantNode(100), field: "Drink" });
                    }
					if (effect.holder === "body") {
                        upsertEffect({ field: "thirst", operator: "+=", holder: "body", amount: effect.arguments[0] });
                    }
				})
				.with("Eat", () => {
					if (effect.holder !== "body") {
						console.warn("idk");
						return;
					}
					upsertEffect({ field: "hunger", operator: "+=", holder: "body", amount: effect.arguments[0] });
					upsertEffect({
						field: "weightOffset",
						operator: "+=",
						holder: "body",
						amount: effect.arguments[1],
					});
				})
				.with("SetDisinfect", () => {
					upsertEffect({ field: "disinfect", operator: "=", holder: "body", amount: effect.arguments[0] });
				})
				.with("Vomit", () => {
					upsertEffect({ field: "vomit", operator: "()", holder: "body" });
				})
				.with("AddComponent<MindwipeScript>", () => {
					upsertEffect({ field: "mindwipe", operator: "()" });
				})
				.with("TryStartFibrillation", () => {
					upsertEffect({ field: "fibrillate", operator: "()" });
				})
				.with("Ragdoll", () => {
					upsertEffect({ field: "ragdoll", operator: "()" });
				})
				.otherwise(method => {
					console.log(method);
				});
		}
	}
	return result.values().toArray().concat(conditional).concat(timer);
};

const isBody = (holder: string) => holder === "body" || holder.endsWith(".body");
const parseHolder = (rawHolder: string | null) => {
	if (!rawHolder) return null;
	if (isBody(rawHolder)) {
		return "body";
	}
	const limb = matchLimb(rawHolder);
	if (limb !== null) {
		return `limb_${limb}`;
	}
	return null;
};
export const matchLimb = (holder: string) => {
	const index = holder.match(/limbs\[(\d+)\]$/)?.[1];
	if (index === undefined) {
		if (holder === "limb") {
			return -1;
		}
		return null;
	}
	return +index;
};

