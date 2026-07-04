import {
	ConstantNode,
	FunctionNode,
	isArrayNode,
	isConstantNode,
	isFunctionNode,
	isOperatorNode,
	isSymbolNode,
	OperatorNode,
	SymbolNode,
	type MathNode,
	type MathScope,
} from "mathjs";
import { math } from "./math";
import type { EffectOperation, SummarizedEffect } from "../effects/effects";
import { match } from "ts-pattern";

const mappings = {
	[`body.HoldingItem(body.handSlot) and body.GetItem(body.handSlot).id == "filterstraw"`]: "[Using filter straw]",
	"body.GetComponent<MindwipeScript>()": "[Mindwiped]",
	"body.alive": "[Alive]",
	"body.clawRegrowTime": "[Keratin Booster Duration]",
	[`CoUtils.instance.DurationOf("antirad")`]: "[Antirad Duration]",
	"body.inCardiacArrest": "[In Cardiac Arrest]",
};

export const format = (node: MathNode): string => {
	node = node.transform((node, path, parent) => {
		if (isOperatorNode(node)) {
			if (node.args.some(x => x.toString() === "Random.value" || x.toString() === "Random.Range(0, 1)")) {
				const left = node.args[0].toString() === "Random.value" || node.args[0].toString() === "Random.Range(0, 1)";
				const chance = new OperatorNode("*", "multiply", [left ? node.args[1] : node.args[0], new ConstantNode(100)]);
				const invChance = new OperatorNode("-", "subtract", [new ConstantNode(100), chance]);
				if ((left && node.op === "<") || (!left && node.op === ">")) {
					return new SymbolNode(`[${format(chance)}% Chance]`);
				}
				if ((left && node.op === ">") || (!left && node.op === "<")) {
					return new SymbolNode(`[${format(invChance)}% Chance]`);
				}
			}
		}
        return node;
	});
	node = math.simplify(node, {}, { exactFractions: false });
	const str = node.toString();
	if (str in mappings) return mappings[str as keyof typeof mappings];
	if (isOperatorNode(node)) {
		if (node.isUnary()) return `${node.op}${format(node.args[0]!)}`;

		return `${node.args.map(x => format(x)).join(` ${node.op} `)}`;
	}
	if (isSymbolNode(node)) {
		return node.name;
	}
	if (isFunctionNode(node)) {
		return `${node.fn.name}(${node.args.map(x => format(x)).join(", ")})`;
	}
	if (isConstantNode(node)) {
		if (typeof node.value === "number") return `${+node.value.toFixed(4)}`;
		return `${node.value}`;
	}
	if (isArrayNode(node)) {
		return `[${node.items.map(x => format(x)).join("-")}]`;
	}
	return `??? ${node.type}`;
};

export const formatOperation = (op: EffectOperation, map: (node: MathNode) => MathNode = x => x) => {
	const { type } = op;
	if (type === "call") return "";

	const value = math.simplify(op.value, {}, { exactFractions: false });
	return match(type)
		.with("add", () => {
			if (value.toString().startsWith("-")) return format(map(value));
			return `+${format(map(value))}`;
		})
		.with("multiply", () => `×${format(value)}`)
		.with("set", () => {
			if (isFunctionNode(value) && value.fn.name === "Mathf.MoveTowards") {
				return `-> ${format(map(value.args[1]))} by ${format(map(value.args[2]))}`;
			}
			return `= ${format(map(value))}`;
		})
		.exhaustive();
};
