import type { MathNode } from "mathjs";

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

export type RawEffect = AssignmentEffect | MethodCallEffect | MiscMethodCallEffect;

export interface LiquidData {
	color: [number, number, number];
	hexColor: string;
	drinkEffects: RawEffect[];
	injectEffects: RawEffect[];
	injectionSickness: number;
}