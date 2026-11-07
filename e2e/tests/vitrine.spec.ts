import { expect, test } from "@playwright/test";

test(
  "a vitrine não existe no build de produção",
  { tag: ["@desktop", "@celular"] },
  async ({ page, request }) => {
    await page.goto("/vitrine");

    // Volta para o início, que manda quem não entrou para o login.
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole("button", { name: "Entrar" })).toBeVisible();

    // O texto da vitrine também não viaja no bundle.
    const scripts = await page
      .locator("script[src]")
      .evaluateAll((tags) => tags.map((tag) => (tag as HTMLScriptElement).src));
    expect(scripts.length).toBeGreaterThan(0);
    for (const script of scripts) {
      const corpo = await (await request.get(script)).text();
      expect(corpo).not.toContain("Só em desenvolvimento");
    }
  },
);
