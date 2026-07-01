import { Language, Parser } from "web-tree-sitter";

await Parser.init();

export const parser = new Parser();
export const CSharp = await Language.load("./node_modules/tree-sitter-c-sharp/tree-sitter-c_sharp.wasm");
parser.setLanguage(CSharp);
