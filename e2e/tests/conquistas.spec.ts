import {
  expect,
  test,
  type APIRequestContext,
  type Page,
} from "@playwright/test";

import { novaConta } from "./apoio/contas";
import { matricular, novaProfessora, type Professora } from "./apoio/medalhas";
import { comToken, token } from "./apoio/premios";

// O passo 9 de ponta a ponta (fluxos/03-concessao-de-medalha.md): a professora concede, o
// worker reavalia as conquistas, e o aluno vê em Troféus o progresso ("4 de 5 neste
// bimestre") e, com a quinta medalha, a conquista obtida; depois posta a conquista pelo
// novo post. As concessões vão pela API da professora, porque a tela de concessão já tem o
// e2e dela (passo 6); o que este cobre é o que o aluno vê. Só no celular, claro e escuro;
// cada execução tem a sua professora e o seu aluno. Depende da turma vigente, como o e2e
// da medalha.

async function entrar(page: Page, identificador: string, senha: string) {
  await page.goto("/login");
  await page.getByLabel("RA ou e-mail").fill(identificador);
  await page.getByLabel("Senha", { exact: true }).fill(senha);
  await page.getByRole("button", { name: "Entrar", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Novo post" }).first(),
  ).toBeVisible();
}

// A concessão pela API da professora, como a tela faz.
async function concessora(
  request: APIRequestContext,
  professora: Professora,
  nome: string,
) {
  const headers = comToken(
    await token(request, professora.email, professora.senha),
  );
  const alunos = await (
    await request.get(`/api/v1/professor/turmas/${professora.turmaId}/alunos`, {
      headers,
    })
  ).json();
  const alunoId = alunos.find(
    (aluno: { nomeDeExibicao: string }) => aluno.nomeDeExibicao === nome,
  ).id;
  return async (item: string) => {
    const resposta = await request.post(
      `/api/v1/professor/turmas/${professora.turmaId}/medalhas`,
      {
        headers,
        data: { alunoId, item },
      },
    );
    expect(resposta.status()).toBe(201);
  };
}

// O worker avalia em segundos, fora da requisição: a tela de Troféus é relida até o que se
// espera aparecer.
async function emTrofeusAte(page: Page, texto: string | RegExp) {
  const linha = page.getByTestId("premio-C01");
  await expect(async () => {
    await page.goto("/trofeus");
    await expect(linha.getByText(texto)).toBeVisible({ timeout: 2000 });
  }).toPass({ timeout: 30_000 });
  return linha;
}

test(
  "cinco medalhas no bimestre fecham a conquista, que aparece em Troféus e vira post",
  { tag: "@celular" },
  async ({ page, request }) => {
    // Cinco concessões e o worker fechando a conquista, com a suíte em paralelo: o prazo dos
    // casos de vários papéis.
    test.setTimeout(90_000);
    const professora = await novaProfessora(request);
    const nome = `Aluno ${Date.now().toString().slice(-6)}`;
    const aluno = await novaConta(request, nome);
    matricular(aluno, professora.turmaId);
    const conceder = await concessora(request, professora, nome);

    // Quatro de cinco: a C01 aparece com o progresso do bimestre, ainda não obtida.
    for (const item of ["M01", "M01", "M02", "M02"]) {
      await conceder(item);
    }
    await entrar(page, aluno.ra, aluno.senha);
    const c01 = await emTrofeusAte(page, "4 de 5 neste bimestre");
    await expect(
      c01.getByText(/^Regra automática · Receber cinco medalhas/),
    ).toBeVisible();

    // A quinta fecha a conquista: a instância do bimestre no lugar do critério, os pontos
    // somados e a linha de progresso fora.
    await conceder("M03");
    await emTrofeusAte(page, /^Regra automática · \dº bimestre de \d{4}$/);
    await expect(c01.getByText("+25")).toBeVisible();
    await expect(c01.getByTestId("progresso-C01")).toHaveCount(0);
    await expect(page.getByText(/^160 pts( · #[\d.]+)?$/)).toBeVisible();

    // O aluno posta a conquista: um prêmio rende um post.
    await page.goto("/");
    await page.getByRole("button", { name: "Novo post" }).first().click();
    await page.getByLabel("Legenda").fill(`Cinco no bimestre, ${nome}!`);
    await page
      .getByRole("checkbox", { name: /^Cinco no bimestre, Bronze, 25 pontos/ })
      .click();
    await page.getByRole("button", { name: "Publicar", exact: true }).click();

    const cartao = page
      .locator('[data-testid^="post-"]', {
        hasText: `Cinco no bimestre, ${nome}!`,
      })
      .first();
    await expect(cartao).toBeVisible();
    await expect(
      cartao.getByText("Cinco no bimestre", { exact: true }).first(),
    ).toBeVisible();
  },
);
