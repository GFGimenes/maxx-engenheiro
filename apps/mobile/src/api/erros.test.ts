import { readdirSync, readFileSync } from "fs";
import { join } from "path";

import {
  codigoDoErro,
  codigosConhecidos,
  mensagemDaFalha,
  mensagemDeErro,
} from "./erros";

const API = join(__dirname, "../../../api/src");

// Os arquivos de códigos de recusa da Application (CodigosDeRecusa.cs,
// CodigosDaMedalha.cs, ...), onde quer que estejam: código novo num arquivo novo também
// precisa de mensagem.
function arquivosDeCodigos(): string[] {
  const raiz = join(API, "Maxx.Application");
  return readdirSync(raiz, { recursive: true, encoding: "utf8" })
    .filter((caminho) => /(^|[\\/])Codigos\w*\.cs$/.test(caminho))
    .map((caminho) => join(raiz, caminho));
}

// Os códigos que a API pode mandar, lidos do próprio código dela.
function codigosDaApi(): string[] {
  const recusas = arquivosDeCodigos()
    .map((arquivo) => readFileSync(arquivo, "utf8"))
    .join("\n");
  const erros = readFileSync(
    join(API, "Maxx.Api/Erros/DomainExceptionHandler.cs"),
    "utf8",
  );
  const literais = (texto: string, padrao: RegExp) =>
    [...texto.matchAll(padrao)].map((m) => m[1]!);

  return [
    ...literais(recusas, /const string \w+ = "([a-z-]+)";/g),
    ...literais(erros, /=> "([a-z-]+)"/g),
  ];
}

describe("mensagens de erro", () => {
  it("todo código que a API manda tem mensagem própria", () => {
    const daApi = codigosDaApi();

    expect(arquivosDeCodigos()).toHaveLength(10);
    expect(daApi).toEqual(
      expect.arrayContaining([
        "registro-simultaneo",
        "limite-atingido",
        "tamanho-de-foto-invalido",
        "premio-ja-postado",
        "premio-ja-reivindicado",
        "seguir-nao-permitido",
        "cursor-invalido",
        "busca-sem-criterio",
        "dados-invalidos",
      ]),
    );
    for (const codigo of daApi) {
      expect(codigosConhecidos).toContain(codigo);
    }
  });

  it("registro-simultaneo diz o que houve e convida a tentar de novo", () => {
    expect(mensagemDeErro("registro-simultaneo")).toBe(
      "Muita gente entrou com esse nome agora. Tente de novo em instantes.",
    );
  });

  it("código desconhecido, ou nenhum, cai na mensagem padrão", () => {
    const padrao = "Não deu certo agora. Tente de novo em instantes.";

    expect(mensagemDeErro("codigo-de-uma-api-mais-nova")).toBe(padrao);
    expect(mensagemDeErro(undefined)).toBe(padrao);
    expect(mensagemDeErro("toString")).toBe(padrao);
  });

  it("lê o código do ProblemDetails e ignora o que não é", () => {
    expect(codigoDoErro({ status: 409, codigo: "conta-existente" })).toBe(
      "conta-existente",
    );
    expect(codigoDoErro({ codigo: 42 })).toBeUndefined();
    expect(codigoDoErro("texto")).toBeUndefined();
    expect(codigoDoErro(null)).toBeUndefined();
  });

  it("falha de rede vira sem conexão, e o resto, a padrão, nunca o erro cru", () => {
    expect(mensagemDaFalha(new TypeError("Network request failed"))).toBe(
      "Sem conexão. Confira a internet e tente de novo.",
    );
    expect(mensagemDaFalha(new Error("stack trace interno"))).toBe(
      "Não deu certo agora. Tente de novo em instantes.",
    );
  });

  it("nenhuma mensagem tem emoji nem travessão", () => {
    for (const codigo of codigosConhecidos) {
      const texto = mensagemDeErro(codigo);
      expect(texto).not.toMatch(/\p{Extended_Pictographic}/u);
      expect(texto).not.toMatch(/[—–]/);
    }
  });
});
