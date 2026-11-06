/// <reference types="node" />
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

import { fontAssets, fontFamily, textStyles, type FontFamily } from "./fonts";
import { tipografia } from "./tokens";

const PASTA = join(__dirname, "../../assets/fonts");

// Leitor mínimo de sfnt: o suficiente para conferir o que o script de instanciação promete.
function lerFonte(arquivo: string) {
  const dados = readFileSync(join(PASTA, arquivo));
  const tabelas = new Map<string, number>();
  for (let i = 0; i < dados.readUInt16BE(4); i++) {
    const registro = 12 + i * 16;
    tabelas.set(
      dados.toString("latin1", registro, registro + 4),
      dados.readUInt32BE(registro + 8),
    );
  }
  const tabela = (tag: string) => {
    const inicio = tabelas.get(tag);
    if (inicio === undefined) throw new Error(`${arquivo} sem tabela ${tag}`);
    return inicio;
  };

  return {
    temTabela: (tag: string) => tabelas.has(tag),
    peso: dados.readUInt16BE(tabela("OS/2") + 4),
    classeDeLargura: dados.readUInt16BE(tabela("OS/2") + 6),
    anguloItalico: dados.readInt32BE(tabela("post") + 4) / 65536,
    recursos() {
      const gsub = tabela("GSUB");
      const lista = gsub + dados.readUInt16BE(gsub + 6);
      const tags = new Set<string>();
      for (let i = 0; i < dados.readUInt16BE(lista); i++) {
        const registro = lista + 2 + i * 6;
        tags.add(dados.toString("latin1", registro, registro + 4));
      }
      return tags;
    },
    // Registro Windows, em UTF-16 BE.
    nome(id: number) {
      const name = tabela("name");
      const textos = name + dados.readUInt16BE(name + 4);
      for (let i = 0; i < dados.readUInt16BE(name + 2); i++) {
        const registro = name + 6 + i * 12;
        if (
          dados.readUInt16BE(registro) === 3 &&
          dados.readUInt16BE(registro + 6) === id
        ) {
          const inicio = textos + dados.readUInt16BE(registro + 10);
          const fim = inicio + dados.readUInt16BE(registro + 8);
          return Buffer.from(dados.subarray(inicio, fim))
            .swap16()
            .toString("utf16le");
        }
      }
      return undefined;
    },
  };
}

const FAMILIAS = Object.keys(fontAssets) as FontFamily[];

test("cada arquivo da pasta está registrado, e vice-versa", () => {
  const arquivos = readdirSync(PASTA)
    .filter((nome) => nome.endsWith(".ttf"))
    .map((nome) => nome.replace(".ttf", ""));

  expect(arquivos.sort()).toEqual([...FAMILIAS].sort());
});

test("a licença das duas famílias acompanha os arquivos", () => {
  const arquivos = readdirSync(PASTA);

  expect(arquivos).toContain("OFL-Archivo.txt");
  expect(arquivos).toContain("OFL-InstrumentSans.txt");
});

describe.each(FAMILIAS)("%s", (familia) => {
  const fonte = lerFonte(`${familia}.ttf`);
  const archivo = familia.startsWith("Archivo");

  test("é estática, sem eixo variável", () => {
    expect(fonte.temTabela("fvar")).toBe(false);
    expect(fonte.temTabela("gvar")).toBe(false);
  });

  test("tem o peso do nome", () => {
    expect(familia).toContain(`-${fonte.peso}`);
  });

  test("Archivo é semi-expandida, Instrument Sans tem largura normal", () => {
    expect(fonte.classeDeLargura).toBe(archivo ? 6 : 5);
  });

  test("só o arquivo itálico é inclinado", () => {
    expect(fonte.anguloItalico !== 0).toBe(familia.endsWith("Italic"));
  });

  test("oferece algarismos tabulares", () => {
    expect(fonte.recursos().has("tnum")).toBe(true);
  });

  test("tem nome PostScript próprio, que o iOS exige, e traz a licença", () => {
    expect(fonte.nome(6)).toBe(familia);
    expect(fonte.nome(13)).toMatch(/Open Font License/);
  });
});

test.each(Object.keys(tipografia) as (keyof typeof tipografia)[])(
  "o estilo %s aponta para um arquivo registrado, sem depender de fontWeight",
  (nome) => {
    const estilo = textStyles[nome];

    expect(FAMILIAS).toContain(estilo.fontFamily);
    expect(estilo.fontWeight).toBeUndefined();
    expect(estilo.fontSize).toBe(tipografia[nome].tamanho);
  },
);

test("o nome da família sai da família, do peso e do itálico", () => {
  expect(fontFamily({ familia: "Archivo", peso: 800, italico: true })).toBe(
    "Archivo112-800Italic",
  );
  expect(fontFamily({ familia: "Instrument Sans", peso: 600 })).toBe(
    "InstrumentSans-600",
  );
});

test("pontos saem em Archivo 800 itálico com algarismos tabulares", () => {
  expect(textStyles.pontos).toEqual({
    fontFamily: "Archivo112-800Italic",
    fontSize: 18,
    fontVariant: ["tabular-nums"],
  });
});

test("o rótulo vai em caixa alta, com espaçamento de 0,04 em", () => {
  expect(textStyles.rotulo).toMatchObject({
    fontFamily: "InstrumentSans-600",
    fontSize: 11,
    lineHeight: 14,
    textTransform: "uppercase",
    letterSpacing: 11 * 0.04,
  });
});
