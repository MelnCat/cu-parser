import type { MathNode } from "mathjs";
import type { Node } from "web-tree-sitter";

export interface BaseLiquidEffect {
	condition?: MathNode;
	timer?: MathNode;
}

export interface AssignmentEffect extends BaseLiquidEffect {
	type: "assignment";
	holder: string | null;
	field: string;
	operator: string;
	expression: MathNode;
    rawLeft: Node;
    rawRight: Node;
}

export interface MethodCallEffect extends BaseLiquidEffect {
	type: "method_call";
	holder: string | null;
	method: string;
	arguments: MathNode[];
    rawMethod: Node;
    rawArguments: Node[];
}

export interface MiscMethodCallEffect extends BaseLiquidEffect {
	type: "misc_method_call";
	holder: string | null;
	method: string;
	arguments: string[];
    rawMethod: Node;
    rawArguments: Node[];
}

export type RawEffect = AssignmentEffect | MethodCallEffect | MiscMethodCallEffect;

export interface LiquidData {
	color: [number, number, number];
	hexColor: string;
	drinkEffects: RawEffect[];
	injectEffects: RawEffect[];
	injectionSickness: number;
}