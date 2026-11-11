import { expect, test, type Browser, type Page } from "@playwright/test";

import { abrirMenu, abrirPerfil } from "./apoio/navegacao";

// Cada papel no próprio contexto, com a lembrança do aparelho separada, na tela do projeto.
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

// Da tela de demonstração até a sessão aberta, pelo termo.
async function registrar(
  page: Page,
  papel: "aluno" | "professor" | "coordenação",
  nome: string,
) {
  await page.goto("/demonstracao");
  await page.getByRole("button", { name: `Entrar como ${papel}` }).click();
  await page.getByLabel("Nome", { exact: true }).fill(nome);
  if (papel !== "coordenação") {
    await page.getByRole("radio", { name: "Ciência da Computação" }).click();
  }
  await page.getByRole("button", { name: "Continuar" }).click();
  await page.getByRole("button", { name: "Aceitar e entrar" }).click();
  await expect(page).not.toHaveURL(/\/demonstracao$/);
}

// A demonstração existe só com a flag. Os testes @demo rodam numa passada própria, com o
// docker-compose.demo.yml por cima; a passada normal os exclui (--grep-invert @demo) e
// confere a flag desligada.

const CONTAS = "maxx.demo.contas";

test(
  "com a flag desligada, não há botão, a tela volta ao login e a API responde 404",
  { tag: ["@desktop", "@celular"] },
  async ({ page, request }) => {
    expect((await request.get("/api/v1/demo")).status()).toBe(404);

    await page.goto("/login");
    await expect(page.getByRole("button", { name: "Entrar" })).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Entrar na demonstração" }),
    ).toHaveCount(0);

    await page.goto("/demonstracao");
    await expect(page).toHaveURL(/\/login$/);
  },
);

test(
  "registra com nome, curso e o termo, entra, sai e volta sem senha pelo aparelho",
  { tag: ["@demo", "@celular"] },
  async ({ page }) => {
    const nome = `Banca ${Date.now() % 100000}`;

    await page.goto("/login");
    await page.getByRole("button", { name: "Entrar na demonstração" }).click();
    await expect(page).toHaveURL(/\/demonstracao$/);

    // Passo 14: a escolha do papel vem antes do registro.
    await page.getByRole("button", { name: "Entrar como aluno" }).click();
    await page.getByLabel("Nome", { exact: true }).fill(nome);
    await page.getByRole("radio", { name: "Ciência da Computação" }).click();
    await page.getByRole("button", { name: "Continuar" }).click();
    await expect(
      page.getByRole("heading", { name: "Demonstração" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Aceitar e entrar" }).click();

    await expect(page).toHaveURL(/\/$/);
    await abrirPerfil(page);
    await expect(page.getByTestId("arroba")).toHaveText(/-CC$/);

    await abrirMenu(page);
    await page.getByRole("button", { name: "Sair" }).click();
    await expect(page).toHaveURL(/\/login$/);
    await page.getByRole("button", { name: "Entrar na demonstração" }).click();
    await page
      .getByRole("button", { name: `Entrar como ${nome} · CC` })
      .click();

    await abrirPerfil(page);
    await expect(page.getByTestId("arroba")).toHaveText(/-CC$/);
  },
);

test(
  "conta que não existe mais some da lista do aparelho, com a mensagem",
  { tag: ["@demo", "@celular"] },
  async ({ page }) => {
    await page.goto("/login");
    await page.evaluate(
      ([chave, valor]) => localStorage.setItem(chave, valor),
      [
        CONTAS,
        JSON.stringify([
          { chave: "chave-que-nao-existe", nome: "Fantasma", curso: "CC" },
        ]),
      ],
    );

    await page.goto("/demonstracao");
    await page
      .getByRole("button", { name: "Entrar como Fantasma · CC" })
      .click();

    await expect(
      page.getByText("Essa conta de demonstração não existe mais."),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Entrar como Fantasma · CC" }),
    ).toHaveCount(0);
    expect(
      await page.evaluate((chave) => localStorage.getItem(chave), CONTAS),
    ).toBe("[]");
  },
);

// Passo 14: a banca entra nos três papéis pela demonstração. O professor de CC concede
// medalha ao aluno de demonstração de CC, e a coordenação acha esse aluno pela ficha e vê o
// próprio lote marcado na purga, sem purgar: o lote vigente é o de todos os testes @demo.
test(
  "entra como aluno, professor e coordenação, e cada papel chega à sua tela",
  { tag: ["@demo", "@celular"] },
  async ({ browser }) => {
    test.setTimeout(90_000);
    const n = Date.now().toString().slice(-6);
    const aluno = `Banca Aluno ${n}`;

    const doAluno = await novaPagina(browser);
    await registrar(doAluno, "aluno", aluno);
    await abrirPerfil(doAluno);
    await expect(doAluno.getByTestId("arroba")).toHaveText(/-CC$/);
    await doAluno.close();

    const doProfessor = await novaPagina(browser);
    await registrar(doProfessor, "professor", `Banca Professor ${n}`);
    await abrirPerfil(doProfessor);
    await expect(doProfessor.getByTestId("arroba")).toHaveText(/-PROF$/);
    await abrirMenu(doProfessor);
    await doProfessor.getByRole("button", { name: "Minhas turmas" }).click();
    await doProfessor.getByRole("button", { name: /^Banco de Dados/ }).click();
    await doProfessor
      .getByRole("button", { name: `Conceder medalha a ${aluno}` })
      .click();
    await doProfessor
      .getByRole("radio", { name: /^Nota 10 em prova, Bronze, 30 pontos/ })
      .click();
    await doProfessor
      .getByRole("button", { name: "Conceder", exact: true })
      .click();
    await expect(
      doProfessor.getByText(`Medalha Nota 10 em prova concedida a ${aluno}.`),
    ).toBeVisible();
    await doProfessor.close();

    const daCoordenacao = await novaPagina(browser);
    await registrar(daCoordenacao, "coordenação", `Banca Coordenação ${n}`);
    await daCoordenacao.getByRole("tab", { name: "Perfil" }).click();
    await daCoordenacao
      .getByRole("button", { name: "Área da instituição" })
      .click();
    await daCoordenacao.goto("/instituicao/alunos");
    await daCoordenacao.getByLabel("Buscar aluno").fill(aluno);
    await daCoordenacao.getByRole("button", { name: aluno }).click();
    await expect(
      daCoordenacao.getByText(/@demonstracao\.invalid$/),
    ).toBeVisible();
    await daCoordenacao.goto("/instituicao/purga");
    await expect(daCoordenacao.getByText("Seu lote")).toBeVisible();
    await daCoordenacao.close();
  },
);
