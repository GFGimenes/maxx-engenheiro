import { expect, test } from "@playwright/test";

// Os cabeçalhos de segurança do Caddy (passo 13), na web e na API. Os fluxos com foto (post,
// reivindicação, purga) já provam que a CSP deixa a foto subir ao bucket e voltar pela CDN;
// aqui ficam os cabeçalhos em si e uma página sem violação nenhuma da CSP.

function diretiva(csp: string, nome: string): string[] {
  const achada = csp
    .split(";")
    .map((parte) => parte.trim().split(/\s+/))
    .find(([chave]) => chave === nome);
  return achada ? achada.slice(1) : [];
}

test(
  "web e API saem com CSP, HSTS só em HTTPS e os demais cabeçalhos",
  { tag: "@celular" },
  async ({ request, baseURL }, testInfo) => {
    test.skip(testInfo.project.name !== "celular-claro", "Uma vez basta.");

    for (const caminho of ["/", "/post/qualquer", "/api/health"]) {
      const resposta = await request.get(caminho);
      const h = resposta.headers();
      const csp = h["content-security-policy"] ?? "";

      expect(diretiva(csp, "default-src")).toEqual(["'self'"]);
      expect(diretiva(csp, "script-src")).toEqual(["'self'"]);
      expect(diretiva(csp, "object-src")).toEqual(["'none'"]);
      expect(diretiva(csp, "frame-ancestors")).toEqual(["'none'"]);
      // Além do próprio app: a CDN das fotos e o bucket do PUT assinado.
      expect(
        diretiva(csp, "img-src").filter((s) => s.startsWith("http")),
      ).toHaveLength(1);
      expect(
        diretiva(csp, "connect-src").filter((s) => s.startsWith("http")),
      ).toHaveLength(1);

      expect(h["x-content-type-options"]).toBe("nosniff");
      expect(h["x-frame-options"]).toBe("DENY");
      expect(h["referrer-policy"]).toBe("strict-origin-when-cross-origin");
      expect(h["permissions-policy"]).toContain("camera=()");
      expect(h["server"]).toBeUndefined();

      if (baseURL?.startsWith("https://")) {
        expect(h["strict-transport-security"]).toBe("max-age=31536000");
      } else {
        expect(h["strict-transport-security"]).toBeUndefined();
      }
    }
  },
);

test(
  "o login abre sem nenhuma violação da CSP",
  { tag: "@celular" },
  async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "celular-claro", "Uma vez basta.");
    await page.addInitScript(() => {
      const violacoes: string[] = [];
      Object.assign(window, { violacoesDaCsp: violacoes });
      document.addEventListener("securitypolicyviolation", (evento) =>
        violacoes.push(`${evento.violatedDirective} ${evento.blockedURI}`),
      );
    });

    await page.goto("/");
    await expect(page.getByRole("button", { name: "Entrar" })).toBeVisible();

    expect(
      await page.evaluate(
        () =>
          (window as unknown as { violacoesDaCsp: string[] }).violacoesDaCsp,
      ),
    ).toEqual([]);
  },
);
