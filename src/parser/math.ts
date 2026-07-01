import { type MathNode, SymbolNode, ConstantNode, OperatorNode, create, all, FunctionNode, type MathScope } from "mathjs";
import { type Node } from "web-tree-sitter";
import { findIdentifier } from "./util.js";

export const math = create(all!);

math.import({
	"Mathf.Clamp01": (n: number) => Math.max(0, Math.min(1, n)),
	//"Random.Range": (from: number, to: number) => Math.random() * (to - from) + from
	"Random.Range": (from: number, to: number) => [from, to],
	"Mathf.Min": Math.min,
	"Mathf.MoveTowards": (current: number, target: number, maxDelta: number) => {
		if (Math.abs(target - current) <= maxDelta) {
			return target;
		}
		return current + Math.sign(target - current) * maxDelta;
	},
});

export const toMath = (node: Node): MathNode => {
	if (node.type === "identifier") {
		return new SymbolNode(node.text);
	}
	if (node.type === "real_literal" || node.type === "integer_literal") {
		return new ConstantNode(+node!.text.replace("f", ""));
	}
	if (node.type === "string_literal") {
		return new ConstantNode(node!.namedChild(0)!.text);
	}
	if (node.type === "binary_expression") {
		const left = node.childForFieldName("left")!;
		const right = node.childForFieldName("right")!;
		const op = node.child(1);
		const mathOp = op!.text === "||" ? "or" : op!.text === "&&" ? "and" : op!.text;
		return new OperatorNode(
			mathOp as "+",
			{
				"+": "add",
				"-": "subtract",
				"*": "multiply",
				"/": "divide",
				"<": "smaller",
				">": "larger",
				"<=": "smallerEq",
				">=": "largerEq",
				and: "and",
				or: "or",
				"==": "equal",
			}[mathOp] as "add",
			[toMath(left), toMath(right)],
		);
	}
	if (node.type === "prefix_unary_expression") {
		if (node.child(0)!.text === "!") {
			return new OperatorNode("not", "not", [toMath(node.child(1)!)]);
		}
		if (node.child(0)!.text === "+") {
			return new OperatorNode("+", "unaryPlus", [toMath(node.child(1)!)]);
		}
		if (node.child(0)!.text === "-") {
			return new OperatorNode("-", "unaryMinus", [toMath(node.child(1)!)]);
		}
	}
	if (node.type === "invocation_expression") {
		const func = node.namedChild(0)!;
		const args = node.namedChild(1)!.namedChildren;
		return new FunctionNode(
			new SymbolNode(func.text),
			args.map(x => toMath(x.childCount > 1 ? findIdentifier(x) : x.child(0)!)),
		);
	}
	if (node.type === "member_access_expression" || node.type === "element_access_expression") {
		return new SymbolNode(node.text);
	}
	if (node.type === "parenthesized_expression") {
		return toMath(node.namedChild(0)!);
	}
	if (node.type === "conditional_expression") {
		return new FunctionNode("if", [
			toMath(node.childForFieldName("condition")!),
			toMath(node.childForFieldName("consequence")!),
			toMath(node.childForFieldName("alternative")!),
		]);
	}
	throw new Error(`${node.text}: ${node.type}`);
};
