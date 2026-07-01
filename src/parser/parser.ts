import { Language, Node, Parser, Query, Tree } from "web-tree-sitter";
import { analyzeBlock, queryRoot, type LiquidData, type LiquidEffect } from "./util";
import { ConstantNode, efimovFactor, isConstantNode, type MathNode } from "mathjs";
import { math, toMath } from "./math";
import { CSharp, parser } from "./treeSitter";
import { format, formatAssignment } from "./format";
import liquidsCs from "../assets/Liquids.cs?raw";

const tree = parser.parse(liquidsCs)!;

const queryString = `
(assignment_expression
	left: (element_binding_expression 
	  (argument (string_literal) @liquid_id))
	right: (object_creation_expression
	  type: (identifier) @type
	  (#eq? @type "LiquidType")
	  initializer: (initializer_expression
		(assignment_expression
		  left: (identifier) @prop_key
		  right: (_) @prop_value)))
  )
`;
const methodQuery = `
	(method_declaration
		returns: (_) @returns
		name: (_) @name
		parameters: (_) @parameters
		body: (_) @body)`;

interface RawLiquidProperties {
	localeName: Node;
	color: Node;
	valuePerLiter: Node;
	onDrink: Node;
	injectionSickness?: Node;
	qualities?: Node;
	injectable?: Node;
	onHealthUse?: Node;
	healthUsable?: Node;
}

const classMethodQuery = new Query(CSharp, methodQuery);

const classMethods = new Map<string, Node>();

for (const classMethod of classMethodQuery.matches(tree.rootNode)) {
	const data = Object.fromEntries(classMethod.captures.map(x => [x.name, x.node]));
	classMethods.set(data.name!.text, data.body!);
}

const query = new Query(CSharp, queryString);

const matches = query.matches(tree.rootNode);

const liquidsRegistry = new Map<string, RawLiquidProperties>();

for (const match of matches) {
	const captures = Object.fromEntries(match.captures.map(x => [x.name, x.node]));

	const liquidIdNode = captures.liquid_id;
	const propKeyNode = captures.prop_key;
	const propValueNode = captures.prop_value;

	if (!liquidIdNode || !propKeyNode || !propValueNode) throw new Error(`Fuck: ${liquidIdNode?.text}`);

	const liquidId = liquidIdNode.text.match(/"(.+)"/)![1]!;
	if (!liquidId) throw new Error(`fuck 2`);

	liquidsRegistry.getOrInsert(liquidId, {} as RawLiquidProperties)[propKeyNode.text as keyof RawLiquidProperties] = propValueNode;
}

export const liquidData = new Map<string, LiquidData>();

const stuffToCheck: string[] = [];

for (const [liquid, properties] of liquidsRegistry) {
	const colorClass = properties.color!.childForFieldName("type")!.text;
	const colorArguments = properties.color.childForFieldName("arguments")!.namedChildren!.map(x => x.text);
	// [r, g, b, a]
	const color = colorArguments
		.map(x => (colorClass === "Color" ? Math.round(+x.replace("f", "") * 255) : x))
		.map(x => (x === "byte.MaxValue" ? 255 : +x));

	let drinkEffects: LiquidEffect[] = [];
	let injectEffects: LiquidEffect[] = [];
	if (properties.onDrink) {
		drinkEffects = analyzeBlock(properties.onDrink.children.find(x => x.type === "block")!, liquid, stuffToCheck, {}, classMethods);
	}
	if (properties.onHealthUse) {
		injectEffects = analyzeBlock(
			properties.onHealthUse.children.find(x => x.type === "block")!,
			liquid,
			stuffToCheck,
			{},
			classMethods,
		);
	}
	liquidData.set(liquid, {
		color: color.slice(0, 3) as [number, number, number],
        hexColor: `#${color.slice(0, 3).map(x => x.toString(16).padStart(2, "0")).join("")}`,
		drinkEffects,
		injectEffects,
	});
}

const scope = { ml: 100 };
const evaluate = true;

const logEffects = (effects: LiquidEffect[]) => {
	const populateMath = (node: MathNode) => {
		try {
			return (evaluate ? new ConstantNode(node.evaluate(scope)) : math.simplify(math.resolve(node, scope), {}, { exactFractions: false }));
		} catch {
			return math.simplify(math.resolve(node, scope), {}, { exactFractions: false });
		}
	};
	for (const effect of effects) {
		if (effect.type === "assignment") {
			const amount = populateMath(effect.expression);
			if (effect.holder === "body" || effect.holder === "limb.body") {
				const property =
					{
						happiness: "Happiness",
						sicknessAmount: "Sickness",
						temperature: "Temperature",
						septicShock: "Sepsis",
					}[effect.field] ?? effect.field;
				console.log(`   ${property}: ${formatAssignment(amount, effect.operator)}`);
			} else console.log(`   ${effect.holder}->${effect.field} ${effect.operator} ${formatAssignment(amount, effect.operator)}`);
		}
		if (effect.type === "method_call") {
			if (effect.holder === "body.talker") continue;
			const amounts = effect.arguments.map(x => populateMath(x));
			if (effect.holder === "body" || effect.holder === "limb.body") {
				if (effect.method === "Eat") {
					console.log(`   Hunger: ${format(amounts[0]!)}`);
					console.log(`   Weight: ${format(amounts[1]!)}`);
					continue;
				}
				const property =
					{
						Drink: "Thirst",
					}[effect.method] ?? effect.method;
				console.log(`   ${property}: ${format(amounts[0] ?? new ConstantNode(0))}`);
			} else console.log(`   ${effect.holder}->${effect.method} : ${amounts.join(" | ")}`);
		}
		if (effect.timer) console.log(`   ^ [Timer: Repeated ${populateMath(effect.timer)} times]`);
		if (effect.condition) console.log(`   ^ [Condition: ${format(math.simplify(effect.condition))}]`);
	}
};

// for (const [liquid, { drinkEffects, injectEffects, color }] of liquidData) {
// 	console.log(
// 		`\x1b[48;2;${color[0]};${color[1]};${color[2]}m--- ${liquid} (per 100mL) ---\x1b[48;2;${Math.floor(color[0] * 0.8)};${Math.floor(color[1] * 0.8)};${Math.floor(color[2] * 0.8)}m`,
// 	);
// 	console.log("   [DRINKING]");
// 	logEffects(drinkEffects);
// 	if (injectEffects.length) {
// 		console.log("   [INJECTING]");
// 		logEffects(injectEffects);
// 	}
// 	process.stdout.write(`\x1b[0m`);
// }
