import type { Node } from "web-tree-sitter";
import { Query } from "web-tree-sitter";
import { analyzeBlock } from "./analyze";
import type { LiquidData, RawEffect } from "./types";
import { CSharp, parser } from "./treeSitter";
import liquidsCs from "../assets/Liquids.cs?raw";

const getBlock = (node: Node) => node.children.find(x => x.type === "block")!;

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

const rawLiquidProperties = new Map<string, RawLiquidProperties>();

for (const match of matches) {
	const captures = Object.fromEntries(match.captures.map(x => [x.name, x.node]));

	const liquidIdNode = captures.liquid_id;
	const propKeyNode = captures.prop_key;
	const propValueNode = captures.prop_value;

	if (!liquidIdNode || !propKeyNode || !propValueNode) throw new Error(`Fuck: ${liquidIdNode?.text}`);

	const liquidId = liquidIdNode.text.match(/"(.+)"/)![1]!;
	if (!liquidId) throw new Error(`fuck 2`);

	if (!rawLiquidProperties.has(liquidId)) {
		rawLiquidProperties.set(liquidId, {} as RawLiquidProperties);
	}
	rawLiquidProperties.get(liquidId)![propKeyNode.text as keyof RawLiquidProperties] = propValueNode;
}

export const liquidData = new Map<string, LiquidData>();

const stuffToCheck: string[] = [];

for (const [liquid, properties] of rawLiquidProperties) {
	const colorClass = properties.color!.childForFieldName("type")!.text;
	const colorArguments = properties.color.childForFieldName("arguments")!.namedChildren!.map(x => x.text);
	// [r, g, b, a]
	const color = colorArguments
		.map(x => (colorClass === "Color" ? Math.round(+x.replace("f", "") * 255) : x))
		.map(x => (x === "byte.MaxValue" ? 255 : +x));

	const injectionSickness = +(properties.injectionSickness?.text.replace("f", "") ?? 1);

	let drinkEffects: RawEffect[] = [];
	let injectEffects: RawEffect[] = [];
	if (properties.onDrink) {
		drinkEffects = analyzeBlock(getBlock(properties.onDrink), liquid, stuffToCheck, {}, classMethods);
	}
	if (properties.onHealthUse) {
		injectEffects = analyzeBlock(getBlock(properties.onHealthUse), liquid, stuffToCheck, {}, classMethods);
	}
	liquidData.set(liquid, {
		color: color.slice(0, 3) as [number, number, number],
		hexColor: `#${color
			.slice(0, 3)
			.map(x => x.toString(16).padStart(2, "0"))
			.join("")}`,
		drinkEffects,
		injectEffects,
		injectionSickness,
	});
}

console.log(liquidData.entries().toArray());
