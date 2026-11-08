import { writeFileSync } from "node:fs";

import { expect, test, type Page } from "@playwright/test";

import { novaConta, type Aluno } from "./apoio/contas";
import { abrirPerfil } from "./apoio/navegacao";

// O passo 7 de ponta a ponta: post com foto subindo de verdade ao bucket (o emulador do
// compose na CI), deep link atrás do login, curtida, comentário e remoção, busca, seguir e
// editar perfil. Só no celular, como os outros fluxos com conta, pelo limite de login por IP.

async function entrar(page: Page, aluno: Aluno) {
  await page.getByLabel("RA ou e-mail").fill(aluno.ra);
  await page.getByLabel("Senha", { exact: true }).fill(aluno.senha);
  await page.getByRole("button", { name: "Entrar", exact: true }).click();
}

// Uma foto de verdade, desenhada no navegador e salva como JPEG: nada binário no repositório.
async function fotoDeTeste(page: Page, caminho: string) {
  await page.setContent("<canvas id=c width=1200 height=900></canvas>");
  const base64 = await page.evaluate(() => {
    const canvas = document.getElementById("c") as HTMLCanvasElement;
    const contexto = canvas.getContext("2d")!;
    const ceu = contexto.createLinearGradient(0, 0, 0, 900);
    ceu.addColorStop(0, "#8ec5ff");
    ceu.addColorStop(1, "#0b2a55");
    contexto.fillStyle = ceu;
    contexto.fillRect(0, 0, 1200, 900);
    contexto.fillStyle = "#f5b400";
    contexto.beginPath();
    contexto.arc(600, 450, 220, 0, Math.PI * 2);
    contexto.fill();
    return canvas.toDataURL("image/jpeg", 0.9).split(",")[1];
  });
  writeFileSync(caminho, Buffer.from(base64!, "base64"));
}

test(
  "post com foto, deep link depois do login, curtida e comentário apagado",
  { tag: "@celular" },
  async ({ browser, page, request }, info) => {
    const autora = await novaConta(request, "Autora E2E");
    const leitor = await novaConta(request, "Leitor E2E");
    const foto = info.outputPath("foto.jpg");
    await fotoDeTeste(page, foto);
    const legenda = `Post de ponta a ponta ${Date.now()}`;

    // A autora publica pelo "+" do feed, com a foto da galeria (o seletor de arquivo na web).
    await page.goto("/login");
    await entrar(page, autora);
    // O "+" do cabeçalho; com o feed vazio, o estado vazio também oferece "Novo post".
    await page.getByRole("button", { name: "Novo post" }).first().click();
    const seletor = page.waitForEvent("filechooser");
    await page.getByRole("button", { name: "Escolher foto" }).click();
    await (await seletor).setFiles(foto);
    await expect(page.getByTestId("previa-da-foto")).toBeVisible();
    await page.getByLabel("Legenda").fill(legenda);
    await page.getByRole("button", { name: "Publicar" }).click();

    // No feed, com a foto carregada do bucket.
    const cartao = page.locator('[data-testid^="post-"]', { hasText: legenda });
    await expect(cartao).toBeVisible();
    const imagem = cartao.getByTestId("foto-do-post").locator("img");
    await expect
      .poll(() => imagem.evaluate((img: HTMLImageElement) => img.naturalWidth))
      .toBeGreaterThan(0);
    const postId = (await cartao.getAttribute("data-testid"))!.replace(
      "post-",
      "",
    );

    // O leitor chega pelo link do post sem sessão, entra e cai no post.
    const contexto = await browser.newContext(info.project.use);
    const doLeitor = await contexto.newPage();
    await doLeitor.goto(`/post/${postId}`);
    await expect(doLeitor).toHaveURL(/\/login\?voltar=%2Fpost%2F/);
    await entrar(doLeitor, leitor);
    await expect(doLeitor).toHaveURL(new RegExp(`/post/${postId}$`));
    await expect(doLeitor.getByText(legenda)).toBeVisible();

    await doLeitor.getByRole("button", { name: "Curtir" }).click();
    await expect(doLeitor.getByText("1 curtida")).toBeVisible();
    await expect(
      doLeitor.getByRole("button", { name: "Descurtir" }),
    ).toBeVisible();

    await doLeitor.getByRole("button", { name: "Comentários" }).click();
    // Pelo papel: o post continua montado embaixo, com o botão "Comentários".
    await doLeitor
      .getByRole("textbox", { name: "Comentário" })
      .fill("Que foto bonita!");
    await doLeitor.getByRole("button", { name: "Publicar" }).click();
    await expect(doLeitor.getByText("Que foto bonita!")).toBeVisible();
    await doLeitor.getByText("Apagar").click();
    await expect(doLeitor.getByText("Nenhum comentário ainda.")).toBeVisible();
    await contexto.close();
  },
);

test(
  "busca acha o colega, seguir conta no perfil e editar perfil troca nome e arroba",
  { tag: "@celular" },
  async ({ page, request }) => {
    const n = `${Date.now()}`.slice(-7);
    const aluna = await novaConta(request, "Aluna E2E");
    await novaConta(request, `Colega Raro ${n}`);

    await page.goto("/login");
    await entrar(page, aluna);
    await page.getByRole("tab", { name: "Busca" }).click();
    await page.getByLabel("Buscar").fill(`raro ${n}`);
    await page
      .getByRole("link", { name: new RegExp(`^Colega Raro ${n}`) })
      .click();

    await page.getByRole("button", { name: `Seguir Colega Raro ${n}` }).click();
    await expect(
      page.getByRole("button", { name: `Deixar de seguir Colega Raro ${n}` }),
    ).toBeVisible();
    await expect(page.getByLabel("1 seguidores")).toBeVisible();

    // O perfil de outra pessoa abre fora das abas: volta para a Busca antes.
    await page.getByRole("button", { name: "Voltar" }).click();
    await abrirPerfil(page);
    await expect(page.getByLabel("1 seguindo")).toBeVisible();
    await page.getByRole("button", { name: "Editar perfil" }).click();
    await page.getByLabel("Nome de exibição").fill("Aluna Editada");
    await page.getByLabel("Arroba").fill(`editada${n}`);
    await expect(page.getByRole("button", { name: "Salvar" })).toBeEnabled();
    await page.getByRole("button", { name: "Salvar" }).click();

    await expect(page.getByTestId("arroba")).toHaveText(`@editada${n}-CC`);
    await expect(page.getByText("Aluna Editada")).toBeVisible();
  },
);
