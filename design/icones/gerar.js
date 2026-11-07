// Gera ícone, adaptativo, splash e favicon do app a partir de design/assets/maxx-ouro.svg,
// e o traçado do logo para o componente Logo (logoVetor.js).
// Rode da raiz, com o npm ci feito: node design/icones/gerar.js
// Trocar o SVG e rodar de novo basta: nenhum código depende do desenho.
const fs = require("node:fs");
const path = require("node:path");

const raiz = path.resolve(__dirname, "../..");
const { chromium } = require(
  require.resolve("@playwright/test", { paths: [path.join(raiz, "e2e")] }),
);
const tokens = require(path.join(raiz, "design/tokens.json"));
const { gerarLogoVetor } = require("./logoVetor");

const MARINHO = tokens.cores.claro.hero;
const saida = path.join(raiz, "apps/mobile/assets");
const svg = fs.readFileSync(
  path.join(raiz, "design/assets/maxx-ouro.svg"),
  "utf8",
);
const [, largura, altura] = svg.match(/viewBox="0 0 (\d+) (\d+)"/).map(Number);

// O XX: da borda esquerda do primeiro X até o fim do logo.
const INICIO_DO_XX = 0.5785;

// O logo, ou o recorte do XX, na largura pedida. `cor` troca o ouro, no ícone monocromático.
function logo({ w, cor, xx = false }) {
  let conteudo = svg;
  if (cor)
    conteudo = conteudo.replace(/fill="#[0-9A-Fa-f]{6}"/, `fill="${cor}"`);
  if (xx) {
    const x = Math.round(largura * INICIO_DO_XX);
    conteudo = conteudo.replace(
      /viewBox="[^"]+"/,
      `viewBox="${x} 0 ${largura - x} ${altura}"`,
    );
  }
  const dados = Buffer.from(conteudo).toString("base64");
  return `<img src="data:image/svg+xml;base64,${dados}" style="display:block;width:${w}px">`;
}

const ARQUIVOS = [
  // iOS não aceita transparência: logo em ouro sobre marinho.
  { nome: "icon.png", lado: 1024, fundo: MARINHO, logo: { w: 760 } },
  // Android recorta o adaptativo: o logo fica dentro do círculo seguro, de 66% do lado.
  { nome: "android-icon-foreground.png", lado: 1024, logo: { w: 620 } },
  {
    nome: "android-icon-monochrome.png",
    lado: 1024,
    logo: { w: 620, cor: "#FFFFFF" },
  },
  // Sem fundo: a cor de cada tema vem do app.json.
  { nome: "splash-icon.png", lado: 1024, logo: { w: 920 } },
  // Em aba do navegador, o logo inteiro não se lê: fica só o XX.
  {
    nome: "favicon.png",
    lado: 64,
    fundo: MARINHO,
    raio: 12,
    logo: { w: 52, xx: true },
  },
];

(async () => {
  gerarLogoVetor();
  const navegador = await chromium.launch();
  for (const { nome, lado, fundo, raio = 0, logo: desenho } of ARQUIVOS) {
    const pagina = await navegador.newPage({
      viewport: { width: lado, height: lado },
    });
    await pagina.setContent(`<body style="margin:0;background:transparent">
      <div style="width:${lado}px;height:${lado}px;display:flex;align-items:center;justify-content:center;
        background:${fundo ?? "transparent"};border-radius:${raio}px">${logo(desenho)}</div></body>`);
    await pagina.evaluate(() => document.images[0].decode());
    await pagina.screenshot({
      path: path.join(saida, nome),
      omitBackground: true,
    });
    await pagina.close();
    console.log(nome, fs.statSync(path.join(saida, nome)).size, "bytes");
  }
  await navegador.close();
})();
