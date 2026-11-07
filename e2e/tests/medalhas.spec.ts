import { expect, test, type Browser, type Page } from "@playwright/test";

import { novaConta } from "./apoio/contas";
import {
  anularMedalha,
  matricular,
  medalhaAtiva,
  novaProfessora,
} from "./apoio/medalhas";

// O passo 6 de ponta a ponta (fluxos/03-concessao-de-medalha.md): a professora concede pela
// lista da turma, o aluno vê em Troféus, e o comando administrativo anula. Só no celular,
// claro e escuro; cada execução tem a sua professora e o seu aluno.

async function entrar(page: Page, identificador: string, senha: string) {
  await page.goto("/login");
  await page.getByLabel("RA ou e-mail").fill(identificador);
  await page.getByLabel("Senha", { exact: true }).fill(senha);
  await page.getByRole("button", { name: "Entrar" }).click();
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

async function medalhasDoAluno(page: Page) {
  await page.getByRole("tab", { name: "Troféus" }).click();
  await page.getByRole("tab", { name: "Medalhas" }).click();
}

test(
  "a professora concede, o aluno vê em Troféus e a anulação tira a medalha",
  { tag: "@celular" },
  async ({ browser, request }) => {
    const professora = await novaProfessora(request);
    const n = Date.now().toString().slice(-6);
    const nome = `Aluno ${n}`;
    const aluno = await novaConta(request, nome);
    matricular(aluno, professora.turmaId);

    // A professora, pela lista da turma e pelo modal do catálogo.
    const daProfessora = await novaPagina(browser);
    await entrar(daProfessora, professora.email, professora.senha);
    await daProfessora.getByRole("button", { name: "Minhas turmas" }).click();
    await daProfessora.getByRole("button", { name: /^Banco de Dados/ }).click();
    await daProfessora
      .getByRole("button", { name: `Conceder medalha a ${nome}` })
      .click();
    await expect(daProfessora.getByText(`Para ${nome}`)).toBeVisible();
    await daProfessora
      .getByRole("radio", { name: /^Nota 10 em prova, Bronze, 30 pontos/ })
      .click();
    await daProfessora
      .getByRole("button", { name: "Conceder", exact: true })
      .click();
    await expect(
      daProfessora.getByText(`Medalha Nota 10 em prova concedida a ${nome}.`),
    ).toBeVisible();
    await daProfessora.close();

    // O aluno, na aba Medalhas da tela de Troféus.
    const doAluno = await novaPagina(browser);
    await entrar(doAluno, aluno.ra, aluno.senha);
    await medalhasDoAluno(doAluno);
    const linha = doAluno.getByTestId("premio-M01");
    await expect(doAluno.getByText("30 pts", { exact: true })).toBeVisible();
    await expect(linha.getByText("+30")).toBeVisible();
    await expect(
      linha.getByText("Banco de Dados · ", { exact: false }),
    ).toBeVisible();

    // A instituição anula pelo comando administrativo.
    expect(anularMedalha(medalhaAtiva(aluno))).toContain("anulada.");

    await doAluno.reload();
    await medalhasDoAluno(doAluno);
    await expect(doAluno.getByText("0 pts", { exact: true })).toBeVisible();
    await expect(
      doAluno.getByTestId("premio-M01").getByText("+30"),
    ).toHaveCount(0);
    await doAluno.close();
  },
);
