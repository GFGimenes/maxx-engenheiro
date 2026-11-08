import { readFileSync, writeFileSync } from "node:fs";
import { randomInt } from "node:crypto";

import { expect, type APIRequestContext, type Page } from "@playwright/test";

import { sql } from "./contas";
import { linkDeRecuperacao } from "./mailpit";
import { comandoDaApi } from "./medalhas";

// O que o e2e do prêmio de participação (passo 8) precisa além do da medalha: fotos de
// verdade, a API com token e uma conta de instituição própria. A contestação e a decisão
// da instituição passam pela API, porque as telas dela chegam no passo 12 (D7).

// Uma foto quadrada desenhada no navegador e salva como JPEG: nada binário no repositório.
export async function fotoDeTeste(
  page: Page,
  caminho: string,
  lado: number,
  cor: string,
) {
  await page.setContent(`<canvas id=c width=${lado} height=${lado}></canvas>`);
  const base64 = await page.evaluate(
    ({ lado, cor }) => {
      const canvas = document.getElementById("c") as HTMLCanvasElement;
      const contexto = canvas.getContext("2d")!;
      const ceu = contexto.createLinearGradient(0, 0, 0, lado);
      ceu.addColorStop(0, cor);
      ceu.addColorStop(1, "#0b2a55");
      contexto.fillStyle = ceu;
      contexto.fillRect(0, 0, lado, lado);
      contexto.fillStyle = "#f5b400";
      contexto.beginPath();
      contexto.arc(lado / 2, lado / 2, lado / 5, 0, Math.PI * 2);
      contexto.fill();
      return canvas.toDataURL("image/jpeg", 0.85).split(",")[1];
    },
    { lado, cor },
  );
  writeFileSync(caminho, Buffer.from(base64!, "base64"));
}

export async function token(
  request: APIRequestContext,
  identificador: string,
  senha: string,
): Promise<string> {
  const resposta = await request.post("/api/v1/auth/login", {
    data: { identificador, senha },
  });
  expect(resposta.status()).toBe(200);
  return (await resposta.json()).accessToken as string;
}

export const comToken = (accessToken: string) => ({
  Authorization: `Bearer ${accessToken}`,
});

// Upload como o app faz: a assinatura com os tamanhos e os dois PUTs direto no bucket.
export async function fotoEnviada(
  request: APIRequestContext,
  accessToken: string,
  feed: string,
  grade: string,
): Promise<string> {
  const bytesDoFeed = readFileSync(feed);
  const bytesDaGrade = readFileSync(grade);
  const pedido = await request.post("/api/v1/uploads", {
    headers: comToken(accessToken),
    data: {
      finalidade: "post",
      tipoDeConteudo: "image/jpeg",
      tamanhoDoFeed: bytesDoFeed.length,
      tamanhoDaGrade: bytesDaGrade.length,
    },
  });
  expect(pedido.status()).toBe(201);
  const assinado = await pedido.json();
  for (const [envio, bytes] of [
    [assinado.feed, bytesDoFeed],
    [assinado.grade, bytesDaGrade],
  ] as const) {
    const put = await fetch(envio.url, {
      method: "PUT",
      headers: { "Content-Type": "image/jpeg" },
      body: bytes,
    });
    expect(put.status).toBe(200);
  }
  return assinado.id as string;
}

export interface Instituicao {
  email: string;
  senha: string;
}

// Uma conta de instituição só deste teste: a linha no cadastro elegível, a conta pelo
// `seed demo` e a senha pelo primeiro acesso. Uma por teste, e não a do seed, porque a
// recuperação aceita 3 pedidos a cada 15 minutos por e-mail, e a CI roda dois temas e
// repete o que falha.
export async function novaInstituicao(
  request: APIRequestContext,
): Promise<Instituicao> {
  const n = `${Date.now()}${randomInt(100, 999)}`.slice(-11);
  const instituicao: Instituicao = {
    email: `e2e.inst.${n}@unieduk.com.br`,
    senha: `senha-inst-${n}`,
  };

  sql(
    `INSERT INTO cadastro_elegivel (id, papeis, email, nome_completo)
     VALUES (gen_random_uuid(), 4, '${instituicao.email}', 'Instituicao E2E ${n}');`,
  );
  comandoDaApi(["seed", "demo"]);

  expect(
    (
      await request.post("/api/v1/auth/recuperacao", {
        data: { email: instituicao.email },
      })
    ).status(),
  ).toBe(202);
  const link = await linkDeRecuperacao(request, instituicao.email);
  expect(
    (
      await request.post("/api/v1/auth/recuperacao/confirmar", {
        data: {
          token: new URL(link).hash.replace("#token=", ""),
          senha: instituicao.senha,
        },
      })
    ).status(),
  ).toBe(204);

  return instituicao;
}

// A contestação do prêmio de um post, lida do banco.
export const contestacaoDoPost = (postId: string) =>
  sql(
    `SELECT c.id FROM contestacao c
     JOIN post p ON p.premio_participacao_id = c.premio_participacao_id
     WHERE p.id = '${postId}';`,
  );
