import { Node } from "web-tree-sitter";
import { Query } from "web-tree-sitter";
import { analyzeBlock } from "../analyze";
import type { ItemData, LiquidData, RawEffect } from "../types";
import { CSharp, parser } from "../treeSitter";
import worldGenerationCs from "../../assets/WorldGeneration.cs?raw";
import { toMath } from "../math";

const tree = parser.parse(worldGenerationCs)!;

const classQuery = `
(class_declaration body: (declaration_list) @declarations)`;
const clazz = Object.fromEntries(new Query(CSharp, classQuery).captures(tree.rootNode).map(x => [x.name, x.node]));

const methods = Object.fromEntries(
	clazz.declarations.namedChildren
		.filter(x => x.type === "method_declaration")
		.map(x => [x.childForFieldName("name")!.text, x.childForFieldName("body")!]),
);

const info = new Query(CSharp, `(switch_expression (switch_expression_arm)+ @arm)`).captures(methods.GetBlockInfo).map(x => x.node);

interface RawTileProperties {
	id: number;
	key: string;
    health: number;
}

export const rawTiles = new Map<number, RawTileProperties>();

for (const arm of info) {
	if (arm.namedChild(0)!.text === "null") continue;
	const id = +arm.namedChild(0)!.text;
	const assignments = Object.fromEntries(
		new Query(CSharp, `(assignment_expression left: (_) @left right: (_) @right)`)
			.matches(arm.namedChild(1)!)
			.map(x => [x.captures.find(y => y.name === "left")!.node.text, x.captures.find(y => y.name === "right")!.node]),
	);
	if (isNaN(id)) {
		continue;
	}
	const key = assignments.name.childForFieldName("arguments")!.namedChild(0)!.namedChild(0)!.namedChild(0)!.text;
	rawTiles.set(id, { id, key, health: +assignments.health.text.replace("f", "") });
}
