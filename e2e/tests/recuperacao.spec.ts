import { expect, test, type Page } from "@playwright/test";

import { arroba, novaConta, novoElegivel } from "./apoio/contas";
import { linkDeRecuperacao, quantosEmails } from "./apoio/mailpit";

// O fluxo inteiro passa pelo e-mail que chega ao Mailpit do compose. Só no celular, claro
// e escuro: o pedido tem limite de 3 a cada 15 minutos por e-mail, e cada teste usa o seu.

async function pedirLink(page: Page, email: string) {
  await page.goto("/login");
  await page.getByRole("link", { name: "Esqueci minha senha" }).click();
  await expect(page).toHaveURL(/\/esqueci-senha$/);
  await page.getByLabel("E-mail institucional").fill(email);
  await page.getByRole("button", { name: "Enviar link" }).click();
  await expect(page.getByText("Confira o seu e-mail.")).toBeVisible();
}

async function entrar(page: Page, identificador: string, senha: string) {
  await page.goto("/login");
  await page.getByLabel("RA ou e-mail").fill(identificador);
  await page.getByLabel("Senha", { exact: true }).fill(senha);
  await page.getByRole("button", { name: "Entrar" }).click();
}

test(
  "pede o link, abre o e-mail, troca a senha e entra só com a nova",
  { tag: "@celular" },
  async ({ page, request }) => {
    const aluno = await novaConta(request);
    const senhaNova = `${aluno.senha}-nova`;

    await pedirLink(page, aluno.email);
    const link = await linkDeRecuperacao(request, aluno.email);
    await page.goto(link);

    // O token sai da barra de endereço assim que é lido.
    await expect(page.getByLabel("Senha nova")).toBeVisible();
    await expect(page).toHaveURL(/\/redefinir-senha$/);
    expect(new URL(page.url()).hash).toBe("");

    await page.getByLabel("Senha nova").fill(senhaNova);
    await page.getByRole("button", { name: "Salvar senha" }).click();
    await expect(page.getByText("Senha nova salva.")).toBeVisible();

    await entrar(page, aluno.ra, aluno.senha);
    await expect(
      page.getByText("RA, e-mail ou senha não conferem."),
    ).toBeVisible();

    await entrar(page, aluno.ra, senhaNova);
    await expect(page.getByTestId("arroba")).toHaveText(arroba(aluno));
  },
);

test(
  "o mesmo link não troca a senha duas vezes",
  { tag: "@celular" },
  async ({ page, request }) => {
    const aluno = await novaConta(request);
    await pedirLink(page, aluno.email);
    const link = await linkDeRecuperacao(request, aluno.email);

    await page.goto(link);
    await page.getByLabel("Senha nova").fill(`${aluno.senha}-1`);
    await page.getByRole("button", { name: "Salvar senha" }).click();
    await expect(page.getByText("Senha nova salva.")).toBeVisible();

    // Como quem clica de novo no e-mail: uma página nova, não só o fragmento trocado.
    const outraVez = await page.context().newPage();
    await outraVez.goto(link);
    await outraVez.getByLabel("Senha nova").fill(`${aluno.senha}-2`);
    await outraVez.getByRole("button", { name: "Salvar senha" }).click();

    await expect(outraVez.getByText("Esse link não vale mais.")).toBeVisible();
    await outraVez.getByRole("button", { name: "Pedir link novo" }).click();
    await expect(outraVez).toHaveURL(/\/esqueci-senha$/);
  },
);

test(
  "senha curta fica no campo e o link continua valendo",
  { tag: "@celular" },
  async ({ page, request }) => {
    const aluno = await novaConta(request);
    await pedirLink(page, aluno.email);
    await page.goto(await linkDeRecuperacao(request, aluno.email));

    await page.getByLabel("Senha nova").fill("curta");
    await page.getByRole("button", { name: "Salvar senha" }).click();
    await expect(
      page.getByText("A senha precisa ter de 8 a 128 caracteres."),
    ).toBeVisible();

    await page.getByLabel("Senha nova").fill(`${aluno.senha}-nova`);
    await page.getByRole("button", { name: "Salvar senha" }).click();
    await expect(page.getByText("Senha nova salva.")).toBeVisible();
  },
);

test(
  "e-mail sem conta recebe a mesma resposta, e nada é enviado",
  { tag: "@celular" },
  async ({ page, request }) => {
    // Está na base da faculdade, mas ainda não criou conta.
    const semConta = novoElegivel();

    await pedirLink(page, semConta.email);

    // Dá tempo de um envio chegar, se houvesse.
    await page.waitForTimeout(2_000);
    expect(await quantosEmails(request, semConta.email)).toBe(0);
  },
);

test(
  "link sem token oferece pedir outro, sem formulário",
  { tag: ["@desktop", "@celular"] },
  async ({ page }) => {
    await page.goto("/redefinir-senha");

    await expect(page.getByText("Esse link está incompleto.")).toBeVisible();
    await expect(page.getByLabel("Senha nova")).toHaveCount(0);
  },
);
