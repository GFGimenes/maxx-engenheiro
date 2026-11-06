import { expect, test } from "@playwright/test";

import tokens from "../../design/tokens.json";

function rgb(hex: string) {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
  return `rgb(${r}, ${g}, ${b})`;
}

test(
  "o anel de foco vem só pelo teclado, e a sombra do segmentado só no claro",
  { tag: ["@desktop", "@celular"] },
  async ({ page }, testInfo) => {
    const tema =
      testInfo.project.use.colorScheme === "dark" ? "escuro" : "claro";
    await page.goto("/");
    const sistema = page.getByRole("radio", { name: "Sistema" });
    await expect(sistema).toBeVisible();

    // Só o segmento ativo do claro tem sombra.
    if (tema === "claro") {
      await expect(sistema).toHaveCSS("box-shadow", /rgba?\(/);
    } else {
      await expect(sistema).toHaveCSS("box-shadow", "none");
    }
    await expect(page.getByRole("radio", { name: "Claro" })).toHaveCSS(
      "box-shadow",
      "none",
    );

    // Clique com mouse: foca, mas sem anel.
    await sistema.click();
    await expect(sistema).toBeFocused();
    await expect(sistema).toHaveCSS("outline-style", "none");

    // Teclado: o próximo segmento recebe o anel do token, afastado 2 px.
    await page.keyboard.press("Tab");
    const claro = page.getByRole("radio", { name: "Claro" });
    await expect(claro).toBeFocused();
    await expect(claro).toHaveCSS("outline-style", "solid");
    await expect(claro).toHaveCSS("outline-width", "2px");
    await expect(claro).toHaveCSS("outline-offset", "2px");
    await expect(claro).toHaveCSS(
      "outline-color",
      rgb(tokens.cores[tema].focus),
    );

    await page.keyboard.press("Tab");
    await expect(claro).toHaveCSS("outline-style", "none");
  },
);
