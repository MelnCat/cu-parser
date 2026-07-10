import { Node } from "web-tree-sitter";
import { Query } from "web-tree-sitter";
import { analyzeBlock } from "../analyze";
import type { ItemData, LiquidData, RawEffect } from "../types";
import { CSharp, parser } from "../treeSitter";
import itemCs from "../../assets/Item.cs?raw";

const tree = parser.parse(itemCs)!;

const queryString = `
(invocation_expression
    function: (member_access_expression 
        expression: (identifier) @holder 
        name: (identifier) @func)
    arguments: (argument_list 
        (argument (string_literal) @key)
        (argument (object_creation_expression
            type: (identifier)
            initializer: (initializer_expression) @initializer
        )))
)`;

const matches = new Query(CSharp, queryString).matches(tree.rootNode);
type RawItemProperties = {
	autoFill?: Node;
	capacity?: Node;
	category?: Node;
	combineable?: Node;
	decayMinutes?: Node;
	defaultContents?: Node;
	destroyAtZeroCondition?: Node;
	jumpHeightMultChange?: Node;
	onlyHoldInHands?: Node;
	qualities?: Node;
	rec?: Node;
	scaleWeightWithCondition?: Node;
	slotRotation?: Node;
	tags?: Node;
	usable?: Node;
	usableOnLimb?: Node;
	usableWithLMB?: Node;
	useAction?: Node;
	useLimbAction?: Node;
	value?: Node;
	weight?: Node;
};
const rawMap = new Map<string, RawItemProperties>();

for (const found of matches) {
	const props = Object.fromEntries(found.captures.map(x => [x.name, x.node]));
	if (props.holder.text !== "GlobalItems" || props.func.text !== "Add") continue;
	const key = props.key.namedChild(0)!.text;
	rawMap.set(key, {});
	for (const initializer of props.initializer.namedChildren) {
		rawMap.get(key)![initializer.childForFieldName("left")!.text as keyof RawItemProperties] = initializer.childForFieldName("right")!;
	}
}

const check: string[] = [];
export const itemData = new Map(rawMap.entries().map(([key, props]) => {
	const data: ItemData = {};
    if (props.usable) {
        data.usable = props.usable.text === "true";
    }
    if (props.usableOnLimb) {
        data.usableOnLimb = props.usableOnLimb.text === "true";
    }
    if (props.usableWithLMB) {
        data.usableWithLMB = props.usableWithLMB.text === "true";
    }
	if (props.useAction) {
		data.useAction = analyzeBlock(props.useAction.children.find(x => x.type === "block")!, key, check, {}, new Map());
	}
	if (props.useLimbAction) {
		data.useLimbAction = analyzeBlock(props.useLimbAction.children.find(x => x.type === "block")!, key, check, {}, new Map());
	}
    return [key, data];
}));

console.log(check);
