import { type MathNode, SymbolNode, ConstantNode, OperatorNode, indexDependencies, FunctionNode } from "mathjs";
import { Query, type Node } from "web-tree-sitter";
import { CSharp } from "./treeSitter.js";
import { toMath, math } from "./math.js";

export interface BaseLiquidEffect {
	condition?: MathNode;
	timer?: MathNode;
}

export interface AssignmentEffect extends BaseLiquidEffect {
	type: "assignment";
	holder: string;
	field: string;
	operator: string;
	expression: MathNode;
}
export interface MethodCallEffect extends BaseLiquidEffect {
	type: "method_call";
	holder: string | null;
	method: string;
	arguments: MathNode[];
}
export interface MiscMethodCallEffect extends BaseLiquidEffect {
	type: "misc_method_call";
	holder: string | null;
	method: string;
	arguments: string[];
}

export type LiquidEffect = AssignmentEffect | MethodCallEffect | MiscMethodCallEffect;

export interface LiquidData {
	color: [number, number, number];
    hexColor: string;
	drinkEffects: LiquidEffect[];
	injectEffects: LiquidEffect[];
}

const cachedQueries: Record<string, Query> = {};

export const queryRoot = (node: Node, queryStr: string) => {
	const query = (cachedQueries[queryStr] ??= new Query(CSharp, queryStr));
	const result = query.captures(node, { maxStartDepth: 0 });
	if (!result.length) return null;
	return Object.fromEntries(result.map(x => [x.name, x.node]));
};

export const analyzeBlock = (
	block: Node,
	liquid: string,
	stuffToCheck: string[],
	variables: Record<string, MathNode> = {},
	classMethods: Map<string, Node> = new Map(),
) => {
	const effects: LiquidEffect[] = [];
	const body = block.namedChildren;
	const declarations = body.filter(x => x.type === "local_declaration_statement");
	for (const decl of declarations) {
		const declQuery = new Query(
			CSharp,
			`(local_declaration_statement 
					   (variable_declaration type: (predefined_type) @type (variable_declarator name: (identifier) @name (_) @content)))`,
		);
		if (!declQuery.matches(decl)[0]) {
			stuffToCheck.push(`Weird decl at ${liquid}: ${decl.text}`);
			continue;
		}
		const declData = Object.fromEntries(declQuery.matches(decl)[0]!.captures.map(x => [x.name, x.node, x.node.toString()])) as {
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
	const expressions = body.filter(x => x.type === "expression_statement");

	for (const exp of expressions) {
		const assignment = queryRoot(
			exp,
			`(expression_statement
					   (assignment_expression 
						 left: (_) @left
						 _ @assign
						 right: (_) @right))`,
		);
		if (assignment) {
			if (assignment.left!.text === "_") continue;
			const holder = assignment.left!.namedChild(0)!.text;
			const field = assignment.left!.namedChild(1)!.text;
			try {
				effects.push({
					type: "assignment",
					holder,
					field,
					operator: assignment.assign!.text,
					expression: math.resolve(toMath(assignment.right!), variables),
				});
			} catch (e) {
				stuffToCheck.push(`!! Check ${liquid} for assignment for ${assignment.left!.text} | ${e}`);
			}
			continue;
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
			const args = methodCall.args!.namedChildren.map(x => x.child(0));
			if (holder === "CoUtils.instance" && method === "DoTimedOp") {
				const key = args[0]!;
				const callback = args[1]!;
				const duration = args[2]!;
				const mathDuration = math.resolve(toMath(duration), variables);
				const timerEffects = analyzeBlock(callback.namedChild(0)!, liquid, stuffToCheck, variables, classMethods);
				effects.push(...timerEffects.map(x => ({ ...x, timer: mathDuration })));
				continue;
			}
			try {
				effects.push({
					type: "method_call",
					arguments: args.map(x => math.resolve(toMath(x!), variables)),
					method,
					holder,
				});
			} catch (e) {
				effects.push({
					type: "misc_method_call",
					arguments: args.map(x => x!.text!),
					method,
					holder,
				});
				stuffToCheck.push(`!! Check ${liquid} for method ${holder} ${method} | ${e}`);
			}
			continue;
		}
		stuffToCheck.push(`unknown statement for ${liquid} ${exp.type} : ${exp.text} | ${exp.toString()}`);
	}
	const ifs = body.filter(x => x.type === "if_statement");
	for (const exp of ifs) {
		const ifStatement = queryRoot(
			exp,
			`(if_statement 
				condition: (_) @condition consequence: (_) @consequence)`,
		);
		if (ifStatement) {
			const cond = ifStatement.condition!;
			const cons = ifStatement.consequence!;
			const mathCond = math.resolve(toMath(cond!), variables);
			const condEffects = analyzeBlock(cons, liquid, stuffToCheck, variables, classMethods);
			effects.push(
				...condEffects.map(x => ({
					...x,
					condition: x.condition ? new OperatorNode("and", "and", [x.condition, mathCond]) : mathCond,
				})),
			);
		}
	}
	return effects;
};

const identifierQuery = new Query(CSharp, "((identifier) @id)");
export const findIdentifier = (node: Node) => {
	return identifierQuery.captures(node)[0]!.node;
};
