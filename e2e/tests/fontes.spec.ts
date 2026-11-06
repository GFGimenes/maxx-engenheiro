import { expect, test } from "@playwright/test";

const FAMILIAS = [
  "Archivo112-700",
  "Archivo112-800",
  "Archivo112-800Italic",
  "InstrumentSans-400",
  "InstrumentSans-500",
  "InstrumentSans-600",
  "InstrumentSans-700",
];

test(
  "as fontes vêm do próprio build, sem CDN, e a Archivo chega em 112%",
  { tag: ["@desktop", "@celular"] },
  async ({ page, baseURL }) => {
    const origem = new URL(baseURL!).origin;
    const fontes: { url: string; status: number }[] = [];
    const deFora: string[] = [];
    page.on("response", (resposta) => {
      if (/\.(ttf|otf|woff2?)(\?|$)/.test(resposta.url())) {
        fontes.push({ url: resposta.url(), status: resposta.status() });
      }
      if (new URL(resposta.url()).origin !== origem) {
        deFora.push(resposta.url());
      }
    });

    await page.goto("/");
    const detalhe = page.getByText("A API respondeu 200.");
    await expect(detalhe).toBeVisible();
    await page.evaluate(() => document.fonts.ready);

    expect(deFora).toEqual([]);
    for (const familia of FAMILIAS) {
      const arquivo = fontes.find((f) => f.url.includes(`/${familia}.`));
      expect(arquivo?.status, `arquivo de ${familia}`).toBe(200);
    }

    // `document.fonts.check` responde true até para fonte inexistente, e uma face fora de uso
    // pode ficar "unloaded" mesmo com o arquivo já baixado: vale pedir a face e ver o que volta.
    const estados = await page.evaluate(
      (familias) =>
        Promise.all(
          familias.map(async (familia) =>
            (await document.fonts.load(`16px "${familia}"`, "M")).map(
              (face) => `${face.family.replace(/"/g, "")}: ${face.status}`,
            ),
          ),
        ),
      FAMILIAS,
    );
    expect(estados.flat()).toEqual(FAMILIAS.map((f) => `${f}: loaded`));

    // A largura óptica chega ao navegador: em Archivo 800, o M tem 1026 unidades em 112%
    // (914 em 100%), então quatro deles a 100 px medem cerca de 410 px.
    const largura = await page.evaluate(() => {
      const medida = document.createElement("span");
      medida.style.cssText =
        'position:absolute;visibility:hidden;font:100px "Archivo112-800"';
      medida.textContent = "MMMM";
      document.body.append(medida);
      const { width } = medida.getBoundingClientRect();
      medida.remove();
      return width;
    });
    expect(Math.abs(largura - 410.4)).toBeLessThan(1);

    // O texto usa a fonte embarcada, não a do sistema.
    await expect(detalhe).toHaveCSS("font-family", /InstrumentSans-500/);
  },
);
