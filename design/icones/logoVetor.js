// Gera apps/mobile/src/marca/logoVetor.ts a partir de design/assets/maxx-ouro.svg: o
// traçado do logo para o componente Logo desenhar com react-native-svg, na cor do tema.
// Roda junto com gerar.js, ou sozinho: node design/icones/logoVetor.js
const fs = require("node:fs");
const path = require("node:path");

const raiz = path.resolve(__dirname, "../..");
const ARQUIVO = path.join(raiz, "apps/mobile/src/marca/logoVetor.ts");

function gerarLogoVetor() {
  const svg = fs.readFileSync(
    path.join(raiz, "design/assets/maxx-ouro.svg"),
    "utf8",
  );
  const [, largura, altura] = svg
    .match(/viewBox="0 0 (\d+) (\d+)"/)
    .map(Number);
  const caminhos = [...svg.matchAll(/<path\b[^>]*\sd="([^"]+)"/g)].map(
    (m) => m[1],
  );
  if (caminhos.length !== 1) {
    throw new Error(`Esperado um <path> no logo, há ${caminhos.length}.`);
  }

  fs.writeFileSync(
    ARQUIVO,
    `// Gerado por design/icones/logoVetor.js a partir de design/assets/maxx-ouro.svg.
// Não editar: troque o SVG e rode node design/icones/gerar.js.
export const logoVetor = {
  largura: ${largura},
  altura: ${altura},
  caminho:
    ${JSON.stringify(caminhos[0])},
} as const;
`,
  );
  console.log(path.relative(raiz, ARQUIVO), "gerado");
}

module.exports = { gerarLogoVetor };

if (require.main === module) {
  gerarLogoVetor();
}
