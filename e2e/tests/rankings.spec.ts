import {
  expect,
  test,
  type APIRequestContext,
  type Page,
} from "@playwright/test";

import { novaConta } from "./apoio/contas";
import { matricular, novaProfessora, type Professora } from "./apoio/medalhas";
import { comToken, token } from "./apoio/premios";

// O passo 11 de ponta a ponta (fluxos/05-ranking.md): o aluno sem pontos abre o ranking
// pela posição no perfil e vê "Sem posição ainda"; a professora concede uma medalha, o
// worker recalcula, e em segundos o card mostra a posição e os pontos, e o perfil, o "#".
// Depois, os chips trocam entre semestre e histórico, e o atalho da Busca abre o ranking do
// curso. A concessão vai pela API da professora, como no e2e das conquistas. Só no celular,
// claro e escuro; cada execução tem a sua professora e o seu aluno. Depende da turma
// vigente, como o e2e da medalha.

async function entrar(page: Page, identificador: string, senha: string) {
  await page.goto("/login");
  await page.getByLabel("RA ou e-mail").fill(identificador);
  await page.getByLabel("Senha", { exact: true }).fill(senha);
  await page.getByRole("button", { name: "Entrar", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Novo post" }).first(),
  ).toBeVisible();
}

async function conceder(
  request: APIRequestContext,
  professora: Professora,
  nome: string,
  item: string,
) {
  const headers = comToken(
    await token(request, professora.email, professora.senha),
  );
  const alunos: { id: string; nomeDeExibicao: string }[] = await (
    await request.get(`/api/v1/professor/turmas/${professora.turmaId}/alunos`, {
      headers,
    })
  ).json();
  const alunoId = alunos.find((aluno) => aluno.nomeDeExibicao === nome)!.id;
  const resposta = await request.post(
    `/api/v1/professor/turmas/${professora.turmaId}/medalhas`,
    { headers, data: { alunoId, item } },
  );
  expect(resposta.status()).toBe(201);
}

test(
  "a medalha move a posição em segundos, e o ranking abre pelo perfil e pela Busca",
  { tag: "@celular" },
  async ({ page, request }) => {
    const professora = await novaProfessora(request);
    const nome = `Aluno ${Date.now().toString().slice(-6)}`;
    const aluno = await novaConta(request, nome);
    matricular(aluno, professora.turmaId);
    await entrar(page, aluno.ra, aluno.senha);

    // Sem pontos: o perfil não tem "#", e o ranking diz que ainda não há posição.
    await page.getByRole("tab", { name: "Perfil" }).click();
    await page.getByRole("link", { name: "0 pontos. Abrir rankings" }).click();
    await expect(
      page.getByRole("radio", { name: "Histórico", exact: true }),
    ).toBeChecked();
    const card = page.getByTestId("card-da-posicao");
    await expect(card.getByText("Sem posição ainda")).toBeVisible();

    // A professora concede; o worker recalcula fora da requisição, com no máximo uma
    // execução a cada 10 s (ADR 0005). A tela é relida até a posição chegar.
    await conceder(request, professora, nome, "M01");
    await expect(async () => {
      await page.reload();
      await expect(card.getByText("30 pts")).toBeVisible({ timeout: 2000 });
    }).toPass({ timeout: 45_000 });
    await expect(card.getByText(/^#[\d.]+$/)).toBeVisible();

    // O semestre atual mostra o rótulo do semestre; o histórico não.
    await page.getByRole("radio", { name: "Geral" }).click();
    await expect(page.getByText(/^Semestre \d\/\d{4}$/)).toBeVisible();
    await expect(card.getByText("30 pts")).toBeVisible();
    await page.getByRole("radio", { name: "Histórico", exact: true }).click();
    await expect(page.getByText(/^Semestre \d\/\d{4}$/)).toHaveCount(0);

    // Só os seguidos: ninguém, além do próprio aluno.
    await page.getByRole("radio", { name: "Quem eu sigo" }).click();
    await expect(page.getByText(`${nome} · você`)).toBeVisible();

    // O perfil mostra a posição ao lado dos pontos.
    await page.goto("/perfil");
    await expect(page.getByText(/^pts · #[\d.]+$/)).toBeVisible();

    // O atalho da Busca abre o ranking do curso.
    await page.goto("/busca");
    await page.getByRole("button", { name: "Ranking do curso" }).click();
    await expect(
      page.getByRole("radio", { name: "Curso", exact: true }),
    ).toBeChecked();
    await expect(
      page.getByTestId("card-da-posicao").getByText("30 pts"),
    ).toBeVisible();
  },
);
