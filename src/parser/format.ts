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
} from "mathjs";
import { math } from "./math";

const mappings = {
	[`body.HoldingItem(body.handSlot) and body.GetItem(body.handSlot).id == "filterstraw"`]: "[Using filter straw]",
	"body.GetComponent<MindwipeScript>()": "[Mindwiped]",
};

export const formatAssignment = (node: MathNode, op: string) => {
	if (op === "+=") {
		return `+ ${format(node)}`;
	} else if (op === "-=") {
		return format(new OperatorNode("-", "unaryMinus", [node]));
	} else return `${op} ${format(node)}`
};

export const format = (node: MathNode): string => {
	node = math.simplify(node, {}, { exactFractions: false });
	const str = node.toString();
	if (str in mappings) return mappings[str as keyof typeof mappings];
	if (isOperatorNode(node)) {
		if (node.isUnary()) return `${node.op} ${format(node.args[0]!)}`;
		return `${node.args.map(x => format(x)).join(` ${node.op} `)}`;
	}
	if (isSymbolNode(node)) {
		return node.name;
	}
	if (isFunctionNode(node)) {
		return `${node.fn.name}(${node.args.map(x => format(x)).join(", ")})`;
	}
	if (isConstantNode(node)) {
        if (typeof node.value === "number") return `${+node.value.toFixed(4)}`
		return `${node.value}`;
	}
	if (isArrayNode(node)) {
		return node.items.map(x => format(x)).join("-")
	}
	return `??? ${node.type}`;
};
