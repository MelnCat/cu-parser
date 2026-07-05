import { Language, Parser } from "web-tree-sitter";
import cSharpWasm from "tree-sitter-c-sharp/tree-sitter-c_sharp.wasm?uint8array";
await Parser.init();

export const parser = new Parser();
export const CSharp = await Language.load(cSharpWasm);
parser.setLanguage(CSharp);
