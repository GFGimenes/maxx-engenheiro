import json from "../../../../design/tokens.json";
import {
  cores,
  espacamento,
  raios,
  tamanhos,
  tiers,
  tipografia,
  type EstiloDeTexto,
  type Tier,
} from "./tokens";

const TIERS: Tier[] = ["bronze", "prata", "ouro", "diamante"];

// Primeiro grupo numérico do texto que casa com a expressão.
function numero(texto: string, expressao: RegExp): number {
  const achado = expressao.exec(texto);
  if (!achado?.[1]) throw new Error(`"${texto}" não casa com ${expressao}`);
  return Number(achado[1]);
}

test("as cores são as do JSON, com as mesmas chaves nos dois temas", () => {
  expect(cores.claro).toEqual(json.cores.claro);
  expect(cores.escuro).toEqual(json.cores.escuro);
  expect(Object.keys(cores.escuro).sort()).toEqual(
    Object.keys(cores.claro).sort(),
  );
});

test.each(TIERS)("o tier %s tem os gradientes no formato declarado", (tier) => {
  const estilo = tiers[tier];
  expect(estilo).toEqual(json.tiers[tier]);
  expect(estilo.medalhao).toHaveLength(3);
  expect(estilo.pill).toHaveLength(5);
  expect(estilo.fundoGrade.claro).toHaveLength(2);
  expect(estilo.fundoGrade.escuro).toHaveLength(2);
});

test("o espaçamento é o do JSON e cai na grade de 4 px", () => {
  expect(espacamento).toEqual(json.espacamento);
  for (const valor of espacamento.escala) {
    expect(valor % espacamento.base).toBe(0);
  }
});

test("os raios conferem com o JSON", () => {
  const texto = json.raios;
  expect(raios.filtros).toBe(texto.filtros);
  expect(raios.campos).toBe(texto.campos);
  expect(raios.botoes).toBe(texto.botoes);
  expect(raios.controleSegmentado).toBe(texto.controleSegmentado);
  expect(raios.realceNav).toBe(texto.realceNav);
  expect(raios.pill).toBe(texto.pill);
  expect(`${raios.cardMenor} e ${raios.cardMaior}`).toBe(texto.cards);
});

test("os filtros têm raio 8: pill só nos selos de tier", () => {
  expect(raios.filtros).toBe(8);
  expect(json.raios.regra).toBe("pill só em selos de tier");
});

test("os tamanhos conferem com o texto do JSON", () => {
  const texto = json.tamanhos;
  expect(tamanhos.alvoDeToque).toBe(texto.alvoDeToque);
  expect(tamanhos.campo).toBe(texto.campo);
  expect(tamanhos.iconeNav).toBe(texto.iconeNav);
  expect(tamanhos.avatarNav).toBe(texto.avatarNav);
  expect(tamanhos.avatarPost).toBe(texto.avatarPost);
  expect(tamanhos.medalhaoLista).toBe(texto.medalhaoLista);
  expect(tamanhos.tracoIcone).toBe(texto.tracoIcone);

  const discreto = texto.botaoDiscreto;
  expect(tamanhos.botaoDiscreto.altura).toBe(
    numero(discreto, /^(\d+) de altura visual/),
  );
  expect(tamanhos.botaoDiscreto.texto).toBe(numero(discreto, /texto (\d+)/));
  expect(tamanhos.botaoDiscreto.toque).toBe(numero(discreto, /(\d+) de toque/));

  expect(tamanhos.botaoGrande).toBe(
    numero(texto.botaoGrande, /^(\d+) de altura/),
  );
  expect(tamanhos.navegacaoInferior).toBe(
    numero(texto.navegacaoInferior, /^(\d+) de altura/),
  );
  expect(tamanhos.realceAbaAtiva).toBe(
    numero(texto.realceAbaAtiva, /^(\d+) x \d+/),
  );
  expect(raios.realceNav).toBe(numero(texto.realceAbaAtiva, /raio (\d+)/));
  expect(tamanhos.bolinhaNovidade.diametro).toBe(
    numero(texto.bolinhaNovidade, /^(\d+),/),
  );
  expect(tamanhos.bolinhaNovidade.halo).toBe(
    numero(texto.bolinhaNovidade, /halo de (\d+)px/),
  );
  expect(tamanhos.fotoPost).toBe(numero(texto.fotoPost, /^(\d+) x/));
});

test.each(Object.keys(tipografia) as (keyof typeof tipografia)[])(
  "o estilo de texto %s confere com a escala do JSON",
  (nome) => {
    const estilo: EstiloDeTexto = tipografia[nome];
    const texto = json.tipografia.escala[nome];

    expect(texto.startsWith(`${estilo.tamanho}`)).toBe(true);
    if (estilo.alturaLinha !== undefined) {
      expect(texto).toContain(`${estilo.tamanho}/${estilo.alturaLinha},`);
    }
    expect(texto).toContain(`${estilo.familia} ${estilo.peso}`);
    expect(texto.includes("itálico")).toBe(estilo.italico === true);
    expect(texto.includes("caixa alta")).toBe(estilo.caixaAlta === true);
    if (estilo.espacamentoLetras !== undefined) {
      expect(texto).toContain(`${estilo.espacamentoLetras}em`);
    }
  },
);
