import { expect, test, type Page } from "@playwright/test";

import {
  arroba,
  novaConta,
  novoElegivel,
  versoesAceitas,
  type Aluno,
} from "./apoio/contas";
import { abrirPerfil } from "./apoio/navegacao";

// Cada teste cria a própria linha do cadastro elegível. Rodam só no celular, claro e
// escuro, pelo limite de consultas por IP.

async function passo1(page: Page, aluno: Aluno) {
  await page.goto("/login");
  await page.getByRole("radio", { name: "Cadastrar" }).click();
  await expect(page).toHaveURL(/\/cadastro$/);
  await page.getByLabel("RA", { exact: true }).fill(aluno.ra);
  await page.getByLabel("E-mail institucional").fill(aluno.email);
  await page.getByLabel("Crie uma senha").fill(aluno.senha);
  await page.getByRole("button", { name: "Continuar" }).click();
}

test(
  "cria a conta nos três passos, aceita o termo vigente e já entra",
  { tag: "@celular" },
  async ({ page, request }) => {
    const aluno = novoElegivel();
    const termo = await (await request.get("/api/v1/termo-de-uso")).json();

    await passo1(page, aluno);

    // Passo 2: o arroba vem sugerido; a pessoa troca a parte antes do hífen.
    await expect(page).toHaveURL(/\/cadastro\/arroba$/);
    await expect(page.getByText("2 de 3")).toBeVisible();
    await expect(page.getByLabel("Arroba")).not.toHaveValue("");
    await page.getByLabel("Nome de exibição").fill("Teste");
    await page.getByLabel("Arroba").fill(aluno.prefixo);
    await expect(page.getByText(arroba(aluno))).toBeVisible();
    await expect(page.getByText("Está livre.")).toBeVisible();
    await page.getByRole("button", { name: "Continuar" }).click();

    // Passo 3: o termo que a API serve, com a seção da demonstração.
    await expect(page).toHaveURL(/\/cadastro\/termo$/);
    await expect(page.getByRole("heading", { name: "Fotos" })).toBeVisible();
    await expect(
      page.getByRole("heading", { name: "Demonstração" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Aceitar e criar conta" }).click();

    await expect(page).toHaveURL(/\/$/);
    await abrirPerfil(page);
    await expect(page.getByTestId("arroba")).toHaveText(arroba(aluno));
    expect(versoesAceitas(aluno)).toEqual([termo.versao]);
  },
);

test(
  "arroba de outra pessoa é recusado no campo antes do termo",
  { tag: "@celular" },
  async ({ page, request }) => {
    const dono = await novaConta(request);
    const aluno = novoElegivel();

    await passo1(page, aluno);
    await page.getByLabel("Nome de exibição").fill("Teste");
    await page.getByLabel("Arroba").fill(dono.prefixo.toUpperCase());

    await expect(
      page.getByText("Esse arroba já tem dono. Tente outro."),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Continuar" }),
    ).toBeDisabled();
  },
);

test(
  "RA que já tem conta leva a Esqueci minha senha",
  { tag: "@celular" },
  async ({ page, request }) => {
    const aluno = await novaConta(request);

    await passo1(page, aluno);

    await expect(
      page.getByText(
        "Esse RA já tem conta. Use Esqueci minha senha para entrar.",
      ),
    ).toBeVisible();
    await expect(page).toHaveURL(/\/cadastro$/);
  },
);

test(
  "recarregar no meio volta ao primeiro passo, sem guardar a senha",
  { tag: "@celular" },
  async ({ page }) => {
    const aluno = novoElegivel();
    await passo1(page, aluno);
    await expect(page).toHaveURL(/\/cadastro\/arroba$/);

    await page.reload();

    await expect(page).toHaveURL(/\/cadastro$/);
    await expect(page.getByLabel("Crie uma senha")).toHaveValue("");
  },
);
