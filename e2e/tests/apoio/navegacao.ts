import type { Page } from "@playwright/test";

// Desde o passo 7 a Home é o feed, e o perfil é a aba Perfil: o arroba fica no cabeçalho
// dele, e o tema, "Minhas turmas" e "Sair", no menu (≡) do próprio perfil.
export async function abrirPerfil(page: Page) {
  await page.getByRole("tab", { name: "Perfil" }).click();
}

export async function abrirMenu(page: Page) {
  await abrirPerfil(page);
  await page.getByRole("button", { name: "Menu" }).click();
}
