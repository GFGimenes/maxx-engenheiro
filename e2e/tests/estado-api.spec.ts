import { expect, test, type Locator, type Page } from "@playwright/test";

import tokens from "../../design/tokens.json";

const FRAME_WIDTH = 390;
const FRAME_MAX_HEIGHT = 844;

function themeColors(colorScheme: string | null | undefined) {
  return colorScheme === "dark" ? tokens.cores.escuro : tokens.cores.claro;
}

// "#0B2A55" -> "rgb(11, 42, 85)", como o navegador devolve no estilo computado.
function rgb(hex: string): string {
  const n = Number.parseInt(hex.slice(1), 16);
  return `rgb(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255})`;
}

async function backgroundOf(locator: Locator): Promise<string> {
  return locator.evaluate((el) => getComputedStyle(el).backgroundColor);
}

async function openApp(page: Page) {
  await page.goto("/");
  // O estado vem de uma chamada real: web, Caddy, réplica da API, /api/health.
  await expect(page.getByText("A API respondeu 200.")).toBeVisible();
}

test(
  "no desktop, o app aparece na moldura centralizada e recebe 200 do /health",
  { tag: "@desktop" },
  async ({ page }, testInfo) => {
    const viewport = page.viewportSize()!;
    await openApp(page);

    const frame = page.getByTestId("moldura");
    const box = (await frame.boundingBox())!;
    const height = Math.min(viewport.height, FRAME_MAX_HEIGHT);

    expect(box.width).toBe(FRAME_WIDTH);
    expect(box.height).toBe(height);
    expect(box.x).toBeCloseTo((viewport.width - FRAME_WIDTH) / 2, 0);
    expect(box.y).toBeCloseTo((viewport.height - height) / 2, 0);

    // O conteúdo fica dentro da moldura.
    await expect(frame.getByTestId("estado-api")).toBeVisible();

    const colors = themeColors(testInfo.project.use.colorScheme);
    expect(await backgroundOf(frame)).toBe(rgb(colors.bg));
    expect(await backgroundOf(page.getByTestId("moldura-fundo"))).toBe(
      rgb(colors.surface),
    );
    expect(
      await frame.evaluate((el) => getComputedStyle(el).borderTopColor),
    ).toBe(rgb(colors.line));
    // Sem sombra na interface.
    expect(await frame.evaluate((el) => getComputedStyle(el).boxShadow)).toBe(
      "none",
    );
  },
);

test(
  "no celular, o app ocupa a largura toda, sem moldura",
  { tag: "@celular" },
  async ({ page }, testInfo) => {
    const viewport = page.viewportSize()!;
    await openApp(page);

    await expect(page.getByTestId("moldura")).toHaveCount(0);

    const app = page.getByTestId("app-fundo");
    expect((await app.boundingBox())!.width).toBe(viewport.width);
    await expect(app.getByTestId("estado-api")).toBeVisible();

    // Nenhuma rolagem horizontal.
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth),
    ).toBe(viewport.width);

    const colors = themeColors(testInfo.project.use.colorScheme);
    expect(await backgroundOf(app)).toBe(rgb(colors.bg));
  },
);
