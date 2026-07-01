import { Query, type Node } from "web-tree-sitter";
import { CSharp } from "./treeSitter";

const cachedQueries: Record<string, Query> = {};

export const queryRoot = (node: Node, queryStr: string) => {
	const query = (cachedQueries[queryStr] ??= new Query(CSharp, queryStr));
	const result = query.captures(node, { maxStartDepth: 0 });
	if (!result.length) return null;
	return Object.fromEntries(result.map(x => [x.name, x.node]));
};

const identifierQuery = new Query(CSharp, "((identifier) @id)");

export const findIdentifier = (node: Node) => {
	return identifierQuery.captures(node)[0]!.node;
};