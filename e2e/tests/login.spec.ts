import { expect, test, type Page } from "@playwright/test";

import { arroba, novaConta, type Aluno } from "./apoio/contas";

// Os fluxos com conta rodam só no celular, claro e escuro: cada um gasta tentativas de
// login, e o limite é por IP. O desktop confere o caminho de quem não entrou.

async function entrar(page: Page, identificador: string, senha: string) {
  await page.goto("/login");
  await page.getByLabel("RA ou e-mail").fill(identificador);
  await page.getByLabel("Senha", { exact: true }).fill(senha);
  await page.getByRole("button", { name: "Entrar" }).click();
}

async function estaDentro(page: Page, aluno: Aluno) {
  await expect(page.getByTestId("arroba")).toHaveText(arroba(aluno));
}

test(
  "quem não entrou vai para o login, com Entrar e Cadastrar",
  { tag: ["@desktop", "@celular"] },
  async ({ page }) => {
    await page.goto("/");

    await expect(page).toHaveURL(/\/login$/);
    await expect(
      page.getByRole("radio", { name: "Entrar", checked: true }),
    ).toBeVisible();
    await expect(page.getByRole("radio", { name: "Cadastrar" })).toBeVisible();
    await expect(
      page.getByText("Rede UniEduk · UniMax e UniFaj"),
    ).toBeVisible();
  },
);

test(
  "entra pelo RA, continua dentro ao recarregar e sai",
  { tag: "@celular" },
  async ({ page, request }) => {
    const aluno = await novaConta(request);

    await entrar(page, aluno.ra, aluno.senha);
    await expect(page).toHaveURL(/\/$/);
    await estaDentro(page, aluno);

    // O access token só vive em memória: a recarga renova pelo cookie.
    await page.reload();
    await estaDentro(page, aluno);

    await page.getByRole("button", { name: "Sair" }).click();
    await expect(page).toHaveURL(/\/login$/);
    await page.goto("/");
    await expect(page).toHaveURL(/\/login$/);
  },
);

test(
  "entra pelo e-mail, sem diferenciar maiúsculas",
  { tag: "@celular" },
  async ({ page, request }) => {
    const aluno = await novaConta(request);

    await entrar(page, aluno.email.toUpperCase(), aluno.senha);

    await estaDentro(page, aluno);
  },
);

test(
  "quem já entrou e abre o login vai para o início",
  { tag: "@celular" },
  async ({ page, request }) => {
    const aluno = await novaConta(request);
    await entrar(page, aluno.ra, aluno.senha);
    await estaDentro(page, aluno);

    await page.goto("/login");

    await expect(page).toHaveURL(/\/$/);
    await estaDentro(page, aluno);
  },
);

test(
  "senha errada aparece no campo da senha, com mensagem amigável",
  { tag: "@celular" },
  async ({ page, request }) => {
    const aluno = await novaConta(request);

    await entrar(page, aluno.ra, "senha-que-nao-e");

    await expect(
      page.getByText("RA, e-mail ou senha não conferem."),
    ).toBeVisible();
    await expect(page.getByLabel("Senha", { exact: true })).toHaveAttribute(
      "aria-invalid",
      "true",
    );
    await expect(page).toHaveURL(/\/login$/);
  },
);

test(
  "duas abas abrindo juntas não derrubam a sessão",
  { tag: "@celular" },
  async ({ context, page, request }) => {
    const aluno = await novaConta(request);
    await entrar(page, aluno.ra, aluno.senha);
    await estaDentro(page, aluno);

    // As duas renovam com o mesmo cookie. Sem a fila entre abas, o segundo refresh
    // seria reuso, e a família inteira cairia.
    const outra = await context.newPage();
    await Promise.all([page.reload(), outra.goto("/")]);
    await estaDentro(page, aluno);
    await estaDentro(outra, aluno);

    // A família continua viva: mais uma renovação passa.
    await page.reload();
    await estaDentro(page, aluno);
  },
);

test(
  "muitas tentativas mostram a mensagem do limite, sem travar a tela",
  { tag: "@celular" },
  async ({ page, request }, testInfo) => {
    // Uma vez basta: o limite é por IP, e os outros projetos ainda vão entrar.
    test.skip(testInfo.project.name !== "celular-claro");
    const aluno = await novaConta(request);

    // 5 por minuto por identificador, em cada uma das duas réplicas.
    for (let tentativa = 0; tentativa < 12; tentativa++) {
      await request.post("/api/v1/auth/login", {
        data: { identificador: aluno.ra, senha: "senha-que-nao-e" },
      });
    }
    await entrar(page, aluno.ra, aluno.senha);

    await expect(page.getByRole("alert")).toContainText(
      "Muitas tentativas seguidas. Espere um pouco e tente de novo.",
    );
    await expect(page.getByRole("button", { name: "Entrar" })).toBeEnabled();
  },
);
