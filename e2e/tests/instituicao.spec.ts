import { readFileSync } from "node:fs";
import path from "node:path";
import { randomInt } from "node:crypto";

import {
  expect,
  test,
  type APIRequestContext,
  type Page,
} from "@playwright/test";

import { novaConta, sql, type Aluno } from "./apoio/contas";
import { matricular, novaProfessora } from "./apoio/medalhas";
import {
  comToken,
  fotoDeTeste,
  fotoEnviada,
  novaInstituicao,
  token,
  type Instituicao,
} from "./apoio/premios";

// O passo 12 de ponta a ponta: a área web da instituição. A instituição entra pela aba
// Perfil, decide comprovações e contestações pelas filas, concede, anula, corrige e-mail e
// marca ex-aluno pela ficha, publica aviso de curso e purga um lote de demonstração criado
// pela execução, com as fotos do lote saindo do bucket. Só no celular, claro e escuro; cada
// execução tem a sua instituição, os seus alunos e, na purga, o seu lote.

const RAIZ = path.resolve(__dirname, "../..");

async function entrarComoInstituicao(page: Page, instituicao: Instituicao) {
  await page.goto("/login");
  await page.getByLabel("RA ou e-mail").fill(instituicao.email);
  await page.getByLabel("Senha", { exact: true }).fill(instituicao.senha);
  await page.getByRole("button", { name: "Entrar", exact: true }).click();
  await expect(page.getByRole("tab", { name: "Perfil" })).toBeVisible();
}

const marca = () => `${Date.now()}${randomInt(100, 999)}`.slice(-8);

// dd/mm/aaaa de hoje em São Paulo, para o evento digitado.
function hojeEmSaoPaulo(): string {
  const local = new Date(Date.now() - 3 * 60 * 60 * 1000);
  const dois = (n: number) => String(n).padStart(2, "0");
  return `${dois(local.getUTCDate())}/${dois(local.getUTCMonth() + 1)}/${local.getUTCFullYear()}`;
}

// A reivindicação pela API do aluno, com foto e evento digitado só deste teste: o chip do
// evento isola a comprovação na fila, que é compartilhada com o resto da suíte.
async function reivindicar(
  page: Page,
  request: APIRequestContext,
  aluno: Aluno,
  evento: string,
): Promise<{ premioId: string; postId: string }> {
  const feed = test.info().outputPath("feed.jpg");
  const grade = test.info().outputPath("grade.jpg");
  await fotoDeTeste(page, feed, 1080, "#2b6cb0");
  await fotoDeTeste(page, grade, 320, "#2b6cb0");
  const acesso = await token(request, aluno.email, aluno.senha);
  const uploadId = await fotoEnviada(request, acesso, feed, grade);
  const resposta = await request.post("/api/v1/reivindicacoes", {
    headers: comToken(acesso),
    data: {
      item: "P01",
      eventoDigitado: {
        nome: evento,
        inicio: new Date(Date.now() - 3 * 60 * 60 * 1000)
          .toISOString()
          .slice(0, 10),
        fim: new Date(Date.now() - 3 * 60 * 60 * 1000)
          .toISOString()
          .slice(0, 10),
      },
      uploadId,
      legenda: "Na palestra.",
    },
  });
  expect(resposta.status()).toBe(201);
  const post = await resposta.json();
  return { premioId: post.premio.id as string, postId: post.id as string };
}

const estadoDoPremio = (premioId: string) =>
  sql(`SELECT estado FROM premio_participacao WHERE id = '${premioId}';`);

test(
  "a instituição entra pela aba Perfil, aprova pela fila de comprovações e decide as contestações",
  { tag: "@celular" },
  async ({ page, request }) => {
    test.setTimeout(120_000);
    const instituicao = await novaInstituicao(request);
    const acessoDaInstituicao = await token(
      request,
      instituicao.email,
      instituicao.senha,
    );
    const confirmado = await novaConta(request, "Aluna Confirmada");
    const revogado = await novaConta(request, "Aluno Revogado");
    const pendente = await novaConta(request, "Aluna da Fila");
    const n = marca();
    const daFila = await reivindicar(
      page,
      request,
      pendente,
      `Palestra da fila ${n}`,
    );
    const aConfirmar = await reivindicar(
      page,
      request,
      confirmado,
      `Palestra confirmada ${n}`,
    );
    const aRevogar = await reivindicar(
      page,
      request,
      revogado,
      `Palestra revogada ${n}`,
    );

    // Os dois que vão a contestação: aprovados e contestados pela API da instituição.
    const contestacoes: Record<string, string> = {};
    for (const { premioId } of [aConfirmar, aRevogar]) {
      const comprovacao = sql(
        `SELECT id FROM comprovacao WHERE premio_participacao_id = '${premioId}';`,
      );
      expect(
        (
          await request.post(`/api/v1/comprovacoes/${comprovacao}/aprovacao`, {
            headers: comToken(acessoDaInstituicao),
          })
        ).status(),
      ).toBe(204);
      const contestacao = await request.post(
        `/api/v1/premios-participacao/${premioId}/contestacao`,
        {
          headers: comToken(acessoDaInstituicao),
          data: { motivo: "Conferir a lista de presença." },
        },
      );
      expect(contestacao.status()).toBe(201);
      contestacoes[premioId] = (await contestacao.json()).id;
    }

    // A entrada: a aba Perfil da conta só de instituição leva à área.
    await entrarComoInstituicao(page, instituicao);
    await page.getByRole("tab", { name: "Perfil" }).click();
    await page.getByRole("button", { name: "Área da instituição" }).click();
    await expect(
      page.getByRole("button", { name: "Contestações" }),
    ).toBeVisible();

    // A fila de comprovações, filtrada pelo evento deste teste.
    await page.getByRole("button", { name: "Comprovações" }).click();
    await page
      .getByRole("radio", { name: `Palestra da fila ${n} (1)` })
      .click();
    await page
      .getByRole("button", { name: "Aprovar a comprovação de Aluna da Fila" })
      .click();
    await expect(
      page.getByRole("button", {
        name: "Aprovar a comprovação de Aluna da Fila",
      }),
    ).toHaveCount(0);
    expect(estadoDoPremio(daFila.premioId)).toBe("valido");

    // A fila de contestações: confirmar um, revogar o outro com motivo.
    await page.goto("/instituicao/contestacoes");
    const cartaoConfirmado = page.getByTestId(
      `contestacao-${contestacoes[aConfirmar.premioId]}`,
    );
    await cartaoConfirmado.scrollIntoViewIfNeeded();
    await cartaoConfirmado
      .getByRole("button", { name: "Confirmar o prêmio de Aluna Confirmada" })
      .click();
    await expect(cartaoConfirmado).toHaveCount(0);
    expect(estadoDoPremio(aConfirmar.premioId)).toBe("definitivo");

    const cartaoRevogado = page.getByTestId(
      `contestacao-${contestacoes[aRevogar.premioId]}`,
    );
    await cartaoRevogado.scrollIntoViewIfNeeded();
    await cartaoRevogado
      .getByRole("button", { name: "Revogar o prêmio de Aluno Revogado" })
      .click();
    const folha = page.getByTestId("folha-de-motivo");
    await folha.getByLabel("Motivo").fill("Não estava na palestra.");
    await folha.getByRole("button", { name: "Revogar" }).click();
    await expect(cartaoRevogado).toHaveCount(0);
    expect(estadoDoPremio(aRevogar.premioId)).toBe("revogado");
  },
);

test(
  "pela ficha do aluno, a instituição corrige o e-mail, concede prêmio, anula a medalha e marca ex-aluno",
  { tag: "@celular" },
  async ({ page, request }) => {
    test.setTimeout(120_000);
    const instituicao = await novaInstituicao(request);
    const professora = await novaProfessora(request);
    // Nome único: a turma vigente é compartilhada com as outras execuções.
    const nome = `Aluno da Ficha ${marca()}`;
    const aluno = await novaConta(request, nome);
    matricular(aluno, professora.turmaId);

    // Uma medalha pela API da professora.
    const daProfessora = comToken(
      await token(request, professora.email, professora.senha),
    );
    const alunos = await (
      await request.get(
        `/api/v1/professor/turmas/${professora.turmaId}/alunos`,
        {
          headers: daProfessora,
        },
      )
    ).json();
    const alunoId = alunos.find(
      (candidato: { nomeDeExibicao: string }) =>
        candidato.nomeDeExibicao === nome,
    ).id;
    expect(
      (
        await request.post(
          `/api/v1/professor/turmas/${professora.turmaId}/medalhas`,
          { headers: daProfessora, data: { alunoId, item: "M01" } },
        )
      ).status(),
    ).toBe(201);

    await entrarComoInstituicao(page, instituicao);
    await page.goto("/instituicao/alunos");
    await page.getByLabel("Buscar aluno").fill(aluno.ra);
    await page.getByRole("button", { name: aluno.nomeCompleto }).click();
    await expect(page.getByText(aluno.email)).toBeVisible();

    // Corrigir o e-mail.
    const novoEmail = aluno.email.replace("e2e.", "e2e.corrigido.");
    await page.getByRole("button", { name: "Corrigir e-mail" }).click();
    let folha = page.getByTestId("folha-de-ato");
    await folha.getByLabel("E-mail").fill(novoEmail);
    await folha.getByRole("button", { name: "Salvar e-mail" }).click();
    await expect(page.getByText(novoEmail)).toBeVisible();

    // Conceder um prêmio com evento digitado, de hoje.
    await page.getByRole("button", { name: "Conceder prêmio" }).click();
    await page.getByRole("radio", { name: /^Palestra obrigatória,/ }).click();
    await page.getByRole("radio", { name: "Outro evento" }).click();
    await page
      .getByLabel("Nome do evento")
      .fill(`Palestra de abertura ${marca()}`);
    await page.getByLabel("Data de início").fill(hojeEmSaoPaulo());
    await page.getByRole("button", { name: "Conceder prêmio" }).click();
    await expect(page.getByText("Prêmio concedido.")).toBeVisible();
    await page.getByRole("button", { name: "Voltar à ficha" }).click();

    // Anular a medalha.
    await page
      .getByRole("button", { name: /^Anular Nota 10 em prova/ })
      .click();
    folha = page.getByTestId("folha-de-ato");
    await folha.getByRole("button", { name: "Anular" }).click();
    await expect(page.getByTestId(/^medalha-/)).toHaveCount(0);

    // Marcar ex-aluno, por último: depois dele não há concessão nem troca.
    await page.getByRole("button", { name: "Marcar ex-aluno" }).click();
    folha = page.getByTestId("folha-de-ato");
    await folha.getByRole("button", { name: "Marcar ex-aluno" }).click();
    await expect(page.getByText(/^Ex-aluno desde /)).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Conceder prêmio" }),
    ).toHaveCount(0);

    const estado = sql(
      `SELECT c.email || '|' || (u.ex_aluno_desde IS NOT NULL) || '|' ||
              (SELECT count(*) FROM premio_participacao p WHERE p.aluno_id = u.id AND p.estado = 'valido') || '|' ||
              (SELECT count(*) FROM medalha m WHERE m.aluno_id = u.id AND m.anulada_em IS NOT NULL)
       FROM usuario u JOIN cadastro_elegivel c ON c.id = u.cadastro_elegivel_id
       WHERE c.ra = '${aluno.ra}';`,
    );
    expect(estado).toBe(`${novoEmail}|true|1|1`);
  },
);

test(
  "a instituição publica um aviso para o curso, e o aluno do curso o vê no mural",
  { tag: "@celular" },
  async ({ page, request }) => {
    test.setTimeout(90_000);
    const instituicao = await novaInstituicao(request);
    const aluno = await novaConta(request, "Aluna do Aviso");
    const titulo = `Laboratório fechado ${marca()}`;

    await entrarComoInstituicao(page, instituicao);
    await page.goto("/instituicao/novo-aviso");
    await page.getByRole("radio", { name: "Cursos" }).click();
    await page.getByRole("checkbox", { name: "CC" }).click();
    await page.getByLabel("Título").fill(titulo);
    await page
      .getByLabel("Texto")
      .fill("As aulas práticas vão para o laboratório 5.");
    await page.getByRole("button", { name: "Publicar aviso" }).click();
    await expect(page).toHaveURL(/\/mural$/);

    const mural = await request.get("/api/v1/mural?escopo=curso", {
      headers: comToken(await token(request, aluno.email, aluno.senha)),
    });
    expect(mural.status()).toBe(200);
    expect(
      (await mural.json()).itens.map(
        (aviso: { titulo: string }) => aviso.titulo,
      ),
    ).toContain(titulo);
  },
);

test(
  "a purga tira do banco o lote de demonstração desta execução e as fotos dele do bucket",
  { tag: "@celular" },
  async ({ page, request }) => {
    test.setTimeout(120_000);
    const instituicao = await novaInstituicao(request);
    const lote = `e2e-${marca()}`;

    // Um aluno de demonstração do lote, criado direto no banco (a flag da demonstração fica
    // desligada nesta passada), com um post de foto enviada de verdade ao bucket. A foto sobe
    // por outra conta, porque o aluno de demonstração não tem senha; o post aponta para ela.
    const doador = await novaConta(request, "Quem Envia");
    const feed = test.info().outputPath("feed.jpg");
    const grade = test.info().outputPath("grade.jpg");
    await fotoDeTeste(page, feed, 1080, "#805ad5");
    await fotoDeTeste(page, grade, 320, "#805ad5");
    const uploadId = await fotoEnviada(
      request,
      await token(request, doador.email, doador.senha),
      feed,
      grade,
    );
    const chaves = sql(
      `SELECT chave_do_feed || ' ' || chave_da_grade FROM upload_pendente WHERE id = '${uploadId}';`,
    ).split(" ");
    sql(
      `WITH cadastro AS (
         INSERT INTO cadastro_elegivel (id, papeis, ra, email, nome_completo, curso_id, semestre, demonstracao)
         SELECT gen_random_uuid(), 1, 'DEMO${lote.slice(-8).toUpperCase()}', '${lote}@demonstracao.invalid', 'Demo ${lote}', id, 1, true
         FROM curso WHERE sigla = 'CC' RETURNING id),
       conta AS (
         INSERT INTO usuario (id, cadastro_elegivel_id, papeis, nome_de_exibicao, prefixo, sufixo, criado_em, lote_da_demonstracao, hash_da_chave_do_dispositivo)
         SELECT gen_random_uuid(), id, 1, 'Demo E2E', 'demo${lote.slice(-8)}', 'CC', now(), '${lote}', 'hash-${lote}' FROM cadastro RETURNING id),
       reserva AS (
         INSERT INTO prefixo_reservado (prefixo, usuario_id, reservado_em) SELECT 'demo${lote.slice(-8)}', id, now() FROM conta)
       INSERT INTO post (id, autor_id, legenda, chave_da_foto_do_feed, chave_da_foto_da_grade, criado_em)
       SELECT gen_random_uuid(), id, 'Post da demonstração', '${chaves[0]}', '${chaves[1]}', now() FROM conta;`,
    );
    const leitura = lerEnv("MAXX_ARMAZENAMENTO_URL_DE_LEITURA");
    const urlDoFeed = new URL(chaves[0]!, leitura).toString();
    expect((await fetch(urlDoFeed)).status).toBe(200);

    await entrarComoInstituicao(page, instituicao);
    await page.goto("/instituicao/purga");
    await page.getByRole("button", { name: `Purgar o lote ${lote}` }).click();
    const folha = page.getByTestId("folha-de-ato");
    await folha.getByLabel("Nome do lote").fill(lote);
    await folha.getByRole("button", { name: "Purgar lote" }).click();
    await expect(page.getByTestId("purga-concluida")).toContainText(
      `Lote ${lote} purgado: 1 aluno e 2 fotos a remover do bucket.`,
    );

    expect(
      sql(
        `SELECT count(*) FROM usuario WHERE lote_da_demonstracao = '${lote}';`,
      ),
    ).toBe("0");
    // O worker tira as fotos do bucket em segundos.
    await expect(async () => {
      expect((await fetch(urlDoFeed)).status).toBe(404);
    }).toPass({ timeout: 30_000 });
  },
);

// Uma variável do .env da raiz, o mesmo que o compose lê.
function lerEnv(nome: string): string {
  const linha = readFileSync(path.join(RAIZ, ".env"), "utf8")
    .split(/\r?\n/)
    .find((candidata) => candidata.startsWith(`${nome}=`));
  if (!linha) throw new Error(`${nome} ausente do .env.`);
  return linha.slice(nome.length + 1);
}
