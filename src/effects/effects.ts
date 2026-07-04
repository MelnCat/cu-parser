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
	holder?: string;
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
		const upsertEffect = (key: string, field: string, operator: string, holder?: string, amount?: MathNode) => {
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
					throw new Error("uho h");
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
		if (effect.type === "assignment") {
			const key = `${effect.holder}.${effect.field}`;

			if (isBody(effect.holder)) {
				upsertEffect(effect.field, effect.field, effect.operator, "body", effect.expression);
				// temperature, happiness, sicknessAmount
			} else {
				upsertEffect(key, effect.field, effect.operator, effect.holder, effect.expression);
			}
		} else if (effect.type === "method_call") {
			if (effect.method === "Drink") {
				upsertEffect("thirst", "thirst", "+=", "body", effect.arguments[0]);
			} else if (effect.method === "Eat") {
				upsertEffect("hunger", "hunger", "+=", "body", effect.arguments[0]);
				upsertEffect("weightOffset", "weightOffset", "+=", "body", effect.arguments[1]);
			} else if (effect.method === "SetDisinfect") {
				upsertEffect("disinfect", "disinfect", "=", "body", effect.arguments[0]);
			} else if (effect.method === "Vomit") {
				upsertEffect("vomit", "vomit", "()", "body");
			} else {
				console.log(effect.method);
			}
		}
	}
	return result.values().toArray().concat(conditional).concat(timer);
};

const isBody = (holder: string) => holder === "body" || holder.endsWith(".body");
