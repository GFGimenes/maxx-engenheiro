import { expect, test } from "@playwright/test";

import { abrirMenu, abrirPerfil } from "./apoio/navegacao";

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
