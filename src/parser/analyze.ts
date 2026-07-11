import { OperatorNode, type MathNode } from "mathjs";
import { Query, type Node } from "web-tree-sitter";
import { CSharp } from "./treeSitter";
import { math, toMath } from "./math";
import { queryRoot } from "./query";
import type { RawEffect } from "./types";

const declarationQuery = new Query(
	CSharp,
	`(local_declaration_statement 
				   (variable_declaration type: (predefined_type) @type (variable_declarator name: (identifier) @name (_) @content)))`,
);

const analyzeExpression = (
	exp: Node,
	liquid: string,
	stuffToCheck: string[],
	variables: Record<string, MathNode>,
	classMethods: Map<string, Node>,
): RawEffect[] => {
	const effects: RawEffect[] = [];

	const assignment = queryRoot(
		exp,
		`(expression_statement
					   (assignment_expression 
						 left: (_) @left
						 _ @assign
						 right: (_) @right))`,
	);
	if (assignment) {
		if (assignment.left!.text === "_") return effects;
		const holder = assignment.left.namedChildCount >= 2 ? assignment.left!.namedChild(0)!.text : null;
		const field = assignment.left.namedChildCount >= 2 ? assignment.left!.namedChild(1)!.text : assignment.left!.text;
		try {
			effects.push({
				type: "assignment",
				holder,
				field,
				operator: assignment.assign!.text,
				expression: math.resolve(toMath(assignment.right!), variables),
				rawLeft: assignment.left,
				rawRight: assignment.right,
			});
		} catch (e) {
			stuffToCheck.push(`!! Check ${liquid} for assignment for ${assignment.left!.text} | ${e}`);
		}
		return effects;
	}

	const methodCall = queryRoot(
		exp,
		`((expression_statement 
						(invocation_expression 
							function: (_) @method
							arguments: (argument_list) @args)))`,
	);
	if (methodCall) {
		const holder = methodCall.method!.namedChildren.length ? methodCall.method!.namedChild(0)!.text : null;
		const method = methodCall.method!.namedChildren.length ? methodCall.method!.namedChild(1)!.text : methodCall.method!.text;
		if (classMethods.has(method)) {
			const found = classMethods.get(method)!;
			effects.push(...analyzeBlock(found, liquid, stuffToCheck, {}, classMethods));
		}
		const args = methodCall.args!.namedChildren.map(x => x.child(0)!);
		if (holder === "CoUtils.instance" && method === "DoTimedOp") {
			const callback = args[1];
			const duration = args[2];
			const mathDuration = math.resolve(toMath(duration), variables);
			const timerEffects = analyzeBlock(callback.namedChild(0)!, liquid, stuffToCheck, variables, classMethods);
			effects.push(...timerEffects.map(x => ({ ...x, timer: mathDuration })));
			return effects;
		}
		try {
			effects.push({
				type: "method_call",
				arguments: args.map(x => math.resolve(toMath(x!), variables)),
				method,
				holder,
				rawMethod: methodCall.method,
				rawArguments: args,
			});
		} catch (e) {
			effects.push({
				type: "misc_method_call",
				arguments: args.map(x => x!.text!),
				method,
				holder,
				rawMethod: methodCall.method,
				rawArguments: args,
			});
			stuffToCheck.push(`!! Check ${liquid} for method ${holder} ${method} | ${e}`);
		}
	}

	return effects;
};

export const analyzeBlock = (
	block: Node,
	liquid: string,
	stuffToCheck: string[],
	variables: Record<string, MathNode> = {},
	classMethods: Map<string, Node> = new Map(),
) => {
	const effects: RawEffect[] = [];
	const body = block.namedChildren;
	const declarations = body.filter(x => x.type === "local_declaration_statement");
	for (const decl of declarations) {
		if (!declarationQuery.matches(decl)[0]) {
			stuffToCheck.push(`Weird decl at ${liquid}: ${decl.text}`);
			continue;
		}
		const declData = Object.fromEntries(declarationQuery.matches(decl)[0]!.captures.map(x => [x.name, x.node, x.node.toString()])) as {
			type: Node;
			name: Node;
			content: Node;
		};
		if (declData.type.text === "float" || declData.type.text === "bool") {
			try {
				variables[declData.name.text] = toMath(declData.content);
			} catch (e) {
				stuffToCheck.push(`Check ${liquid} -> Variable ${declData.name.text} | ${e}`);
			}
		} else {
			stuffToCheck.push(`Unknown type ${declData.type.text} and ${declData.name.text}`);
		}
	}

	for (const exp of body.filter(x => x.type === "expression_statement")) {
		effects.push(...analyzeExpression(exp, liquid, stuffToCheck, variables, classMethods));
	}

	const ifs = body.filter(x => x.type === "if_statement");
	const processIf = (exp: Node) => {
        const result: RawEffect[] = [];
		const ifStatement = queryRoot(
			exp,
			`(if_statement 
				condition: (_) @condition consequence: (_) @consequence alternative: (_)? @alternative)`,
		);
		if (!ifStatement) return [];
		const cond = ifStatement.condition!;
		const cons = ifStatement.consequence!;
		const mathCond = math.resolve(toMath(cond!), variables);
		const condEffects = analyzeBlock(cons, liquid, stuffToCheck, variables, classMethods);
		result.push(
			...condEffects.map(x => ({
				...x,
				condition: x.condition ? new OperatorNode("and", "and", [x.condition, mathCond]) : mathCond,
			})),
		);
		const alt = ifStatement.alternative;
		if (!alt) return result;
		if (alt.type === "block") {
			const elseEffects = analyzeBlock(alt, liquid, stuffToCheck, variables, classMethods);
			result.push(
				...elseEffects.map(x => ({
					...x,
					condition: x.condition ? new OperatorNode("and", "and", [new OperatorNode("not", "not", [mathCond]), x.condition]) : mathCond,
				})),
			);
		} else if (alt.type === "if_statement") {
			const elseEffects = processIf(alt);
			result.push(
				...elseEffects.map(x => ({
					...x,
					condition: x.condition ? new OperatorNode("and", "and", [new OperatorNode("not", "not", [mathCond]), x.condition]) : mathCond,
				})),
			);
        }
        return result;
	};
	for (const exp of ifs) {
		effects.push(...processIf(exp));
	}

	return effects;
};
