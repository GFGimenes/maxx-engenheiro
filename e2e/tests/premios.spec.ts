import { expect, test, type Browser, type Page } from "@playwright/test";

import { arroba, novaConta, type Aluno } from "./apoio/contas";
import { matricular, novaProfessora } from "./apoio/medalhas";
import { abrirMenu } from "./apoio/navegacao";
import {
  contestacaoDoPost,
  comToken,
  fotoDeTeste,
  novaInstituicao,
  token,
} from "./apoio/premios";

// O passo 8 de ponta a ponta (fluxos/02-post-com-comprovacao.md e 04-contestacao.md): o
// aluno reivindica pela tela com foto, a professora decide pela fila de comprovações, e o
// aluno vê os pontos e as reivindicações em Troféus; a professora contesta pelo "..." do post.
// A decisão da instituição passa pela API, porque as telas dela chegam no passo 12 (D7). Só
// no celular, claro e escuro; cada execução tem os seus alunos, professora e instituição.
// Depende do evento semeado "Semana Acadêmica de Ciência da Computação" (14 a 18/09) no
// semestre vigente, como o e2e da medalha depende da turma vigente.

async function entrar(page: Page, identificador: string, senha: string) {
  await page.goto("/login");
  await page.getByLabel("RA ou e-mail").fill(identificador);
  await page.getByLabel("Senha", { exact: true }).fill(senha);
  await page.getByRole("button", { name: "Entrar", exact: true }).click();
}

// Uma página com sessão própria, no tamanho e no tema do projeto (celular claro ou escuro).
async function novaPagina(browser: Browser): Promise<Page> {
  const { viewport, isMobile, hasTouch, colorScheme, baseURL } =
    test.info().project.use;
  const contexto = await browser.newContext({
    viewport,
    isMobile,
    hasTouch,
    colorScheme,
    baseURL,
  });
  return contexto.newPage();
}

// A reivindicação da semana acadêmica pela tela do novo post, com a foto da galeria.
async function reivindicarNaSemana(
  page: Page,
  foto: string,
  legenda: string,
): Promise<string> {
  await page.getByRole("button", { name: "Novo post" }).first().click();
  await page.getByRole("radio", { name: "Reivindicar prêmio" }).click();
  const seletor = page.waitForEvent("filechooser");
  await page.getByRole("button", { name: "Escolher foto" }).click();
  await (await seletor).setFiles(foto);
  await expect(page.getByTestId("previa-da-foto")).toBeVisible();
  await page.getByLabel("Legenda").fill(legenda);
  await page.getByRole("radio", { name: /^Semana acadêmica,/ }).click();
  await page
    .getByRole("radio", { name: /^Semana Acadêmica de Ciência da Computação/ })
    .click();
  await page.getByRole("button", { name: "Reivindicar", exact: true }).click();

  // De volta ao feed, o post público com a marca de pendente.
  const cartao = page
    .locator('[data-testid^="post-"]', { hasText: legenda })
    .first();
  await expect(cartao.getByTestId("marca-reivindicado")).toBeVisible();
  return (await cartao.getAttribute("data-testid"))!.replace("post-", "");
}

async function abrirComprovacoes(page: Page) {
  await abrirMenu(page);
  await page.getByRole("button", { name: "Comprovações" }).click();
}

const cartaoNaFila = (page: Page, aluno: Aluno) =>
  page.locator('[data-testid^="comprovacao-"]', { hasText: arroba(aluno) });

async function trofeus(page: Page) {
  await page.reload();
  await page.getByRole("tab", { name: "Troféus" }).first().click();
}

test(
  "o aluno reivindica com foto, a professora aprova pela fila e os pontos aparecem",
  { tag: "@celular" },
  async ({ browser, request }, info) => {
    test.setTimeout(90_000);
    const professora = await novaProfessora(request);
    const nome = `Aluna ${Date.now().toString().slice(-6)}`;
    const aluno = await novaConta(request, nome);
    matricular(aluno, professora.turmaId);

    const doAluno = await novaPagina(browser);
    const foto = info.outputPath("semana.jpg");
    await fotoDeTeste(doAluno, foto, 1080, "#8ec5ff");
    await entrar(doAluno, aluno.ra, aluno.senha);
    await reivindicarNaSemana(doAluno, foto, `Semana acadêmica ${nome}`);

    // Pendente não pontua.
    await trofeus(doAluno);
    await expect(doAluno.getByText(/^0 pts( · #[\d.]+)?$/)).toBeVisible();
    await expect(doAluno.getByText("Pendente", { exact: true })).toBeVisible();

    // A professora, pela fila de comprovações do menu do perfil.
    const daProfessora = await novaPagina(browser);
    await entrar(daProfessora, professora.email, professora.senha);
    await abrirComprovacoes(daProfessora);
    const cartao = cartaoNaFila(daProfessora, aluno);
    await expect(
      cartao.getByText("Semana acadêmica", { exact: true }),
    ).toBeVisible();
    await cartao
      .getByRole("button", { name: `Aprovar a comprovação de ${nome}` })
      .click();
    await expect(cartao).toHaveCount(0);

    // Aprovado, pontua na hora.
    await trofeus(doAluno);
    await expect(doAluno.getByText(/^30 pts( · #[\d.]+)?$/)).toBeVisible();
    await expect(
      doAluno.getByTestId("premio-P06").getByText("+30"),
    ).toBeVisible();
  },
);

test(
  "reprovada com motivo, nova tentativa com outra foto, contestação e revogação",
  { tag: "@celular" },
  async ({ browser, request }, info) => {
    test.setTimeout(120_000);
    const professora = await novaProfessora(request);
    const instituicao = await novaInstituicao(request);
    const nome = `Aluno ${Date.now().toString().slice(-6)}`;
    const aluno = await novaConta(request, nome);
    matricular(aluno, professora.turmaId);

    const doAluno = await novaPagina(browser);
    const primeira = info.outputPath("primeira.jpg");
    const segunda = info.outputPath("segunda.jpg");
    await fotoDeTeste(doAluno, primeira, 1080, "#8ec5ff");
    await fotoDeTeste(doAluno, segunda, 1080, "#c9a6ff");
    await entrar(doAluno, aluno.ra, aluno.senha);
    await reivindicarNaSemana(doAluno, primeira, `Primeira tentativa ${nome}`);

    // A professora reprova pela folha, com motivo.
    const daProfessora = await novaPagina(browser);
    await entrar(daProfessora, professora.email, professora.senha);
    await abrirComprovacoes(daProfessora);
    const cartao = cartaoNaFila(daProfessora, aluno);
    await cartao
      .getByRole("button", { name: `Reprovar a comprovação de ${nome}` })
      .click();
    const folha = daProfessora.getByTestId("folha-de-motivo");
    await folha
      .getByLabel("Motivo")
      .fill("A foto não mostra a semana acadêmica.");
    await folha.getByRole("button", { name: "Reprovar", exact: true }).click();
    await expect(cartao).toHaveCount(0);

    // O aluno vê a reprovada com o motivo e tenta de novo, com outra foto.
    await trofeus(doAluno);
    await expect(doAluno.getByText("Reprovada", { exact: true })).toBeVisible();
    await expect(
      doAluno.getByText("Motivo: A foto não mostra a semana acadêmica."),
    ).toBeVisible();
    await doAluno.getByRole("tab", { name: "Home" }).click();
    const postId = await reivindicarNaSemana(
      doAluno,
      segunda,
      `Segunda tentativa ${nome}`,
    );

    // A professora aprova a nova tentativa pela fila e a contesta pelo "..." do post.
    await daProfessora.reload();
    await cartao
      .getByRole("button", { name: `Aprovar a comprovação de ${nome}` })
      .click();
    await expect(cartao).toHaveCount(0);
    await daProfessora.goto(`/post/${postId}`);
    await daProfessora.getByRole("button", { name: "Mais opções" }).click();
    await daProfessora
      .getByRole("button", { name: "Contestar prêmio" })
      .click();
    const contestar = daProfessora.getByTestId("folha-de-motivo");
    await contestar.getByLabel("Motivo").fill("Revi a foto.");
    await contestar
      .getByRole("button", { name: "Contestar", exact: true })
      .click();
    await expect(contestar).toBeHidden();

    // A instituição revoga pela API (D7): a tela dela chega no passo 12.
    const contestacao = contestacaoDoPost(postId);
    const daInstituicao = await token(
      request,
      instituicao.email,
      instituicao.senha,
    );
    expect(
      (
        await request.post(`/api/v1/contestacoes/${contestacao}/revogacao`, {
          headers: comToken(daInstituicao),
          data: { motivo: "A foto é de outro evento." },
        })
      ).status(),
    ).toBe(204);

    // Revogada fica no histórico do aluno, com o motivo, e não pontua.
    await trofeus(doAluno);
    await expect(doAluno.getByText("Revogada", { exact: true })).toBeVisible();
    await expect(
      doAluno.getByText("Motivo: A foto é de outro evento."),
    ).toBeVisible();
    await expect(doAluno.getByText(/^0 pts( · #[\d.]+)?$/)).toBeVisible();
  },
);
