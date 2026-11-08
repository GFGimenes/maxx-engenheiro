import type { Page } from "@playwright/test";

// Desde o passo 7 a Home é o feed. O arroba, o tema, "Minhas turmas" e "Sair" ficam na aba
// Perfil (provisória até o perfil de verdade, na etapa 9 do passo).
export async function abrirPerfil(page: Page) {
  await page.getByRole("tab", { name: "Perfil" }).click();
}
