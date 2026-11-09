import {
  expect,
  test,
  type APIRequestContext,
  type Browser,
  type Page,
} from "@playwright/test";

import { novaConta, sql, type Aluno } from "./apoio/contas";
import { matricular, novaProfessora, type Professora } from "./apoio/medalhas";
import { comToken, token } from "./apoio/premios";

// O passo 10 de ponta a ponta (produto.md 7 e 8): a professora publica pela tela um aviso
// para duas turmas dela, e o aluno de uma delas vê a bolinha do Mural e a do sininho, abre o
// aviso e as apaga; e uma medalha concedida acende o sininho e a bolinha de Troféus, e o
// toque na notificação leva a Troféus. Só no celular, claro e escuro; cada execução tem a
// sua professora e o seu aluno. Depende da turma vigente, como o e2e da medalha.
//
// As turmas vigentes são as mesmas para todas as execuções, e o aviso de uma execução chega
// ao aluno da outra. Por isso o aluno começa com tudo lido, e o teste confere o aviso dele
// pelo título, e apaga as bolinhas pelo "Ler todos" e pelo sininho, que leem tudo.

async function entrar(page: Page, identificador: string, senha: string) {
  await page.goto("/login");
  await page.getByLabel("RA ou e-mail").fill(identificador);
  await page.getByLabel("Senha", { exact: true }).fill(senha);
  await page.getByRole("button", { name: "Entrar", exact: true }).click();
  await expect(page.getByRole("tab", { name: /^Mural/ })).toBeVisible({
    timeout: 15_000,
  });
}

// Outra pessoa no mesmo teste: um contexto próprio, com o tema e o tamanho do projeto.
async function novaPagina(browser: Browser): Promise<Page> {
  const { viewport, isMobile, hasTouch, colorScheme, baseURL } =
    test.info().project.use;
  const contexto = await browser.newContext({
    viewport,
    isMobile,
    hasTouch,
    colorScheme,
    baseURL,
  });
  return contexto.newPage();
}

// Uma segunda turma vigente da professora, de outra matéria de CC.
function segundaTurma(professora: Professora): string {
  return sql(
    `WITH turma_nova AS (
       SELECT t.id FROM turma t JOIN materia m ON m.id = t.materia_id
       WHERE m.nome = 'Algoritmos e Programação'
         AND t.semestre_letivo_id = (SELECT semestre_letivo_id FROM turma WHERE id = '${professora.turmaId}'))
     INSERT INTO vinculo_professor (id, professor_id, turma_id)
     SELECT gen_random_uuid(), c.id, turma_nova.id
     FROM cadastro_elegivel c, turma_nova WHERE c.email = '${professora.email}'
     RETURNING turma_id;`,
  );
}

// O aluno começa sem nada aceso: os avisos e as notificações de outras execuções lidos.
async function semNovidades(request: APIRequestContext, aluno: Aluno) {
  const headers = comToken(await token(request, aluno.email, aluno.senha));
  expect(
    (await request.post("/api/v1/mural/leitura", { headers })).status(),
  ).toBe(204);
  expect(
    (
      await request.post("/api/v1/eu/notificacoes/leitura", { headers })
    ).status(),
  ).toBe(204);
  expect(
    (await request.put("/api/v1/eu/trofeus/visto", { headers })).status(),
  ).toBe(204);
}

// O worker grava as notificações fora da requisição, e o app pergunta pelas novidades a
// cada 60 s: a tela é recarregada até a bolinha aparecer, por até 30 s.
async function ateAcender(page: Page, rotulo: string | RegExp) {
  await expect(async () => {
    await page.reload();
    await expect(page.getByRole("button", { name: rotulo })).toBeVisible({
      timeout: 2000,
    });
  }).toPass({ timeout: 30_000 });
}

test(
  "a professora publica um aviso para duas turmas, e o aluno o lê pelo Mural e pelo sininho",
  { tag: "@celular" },
  async ({ browser, page, request }) => {
    test.setTimeout(90_000);
    const professora = await novaProfessora(request);
    const outraTurma = segundaTurma(professora);
    const aluno = await novaConta(request, "Aluno do Mural");
    matricular(aluno, professora.turmaId);
    await semNovidades(request, aluno);
    const titulo = `Prova remarcada ${Date.now().toString().slice(-6)}`;

    // A professora, pelo "+" do Mural: as duas turmas, título e texto.
    const daProfessora = await novaPagina(browser);
    await entrar(daProfessora, professora.email, professora.senha);
    await daProfessora.getByRole("tab", { name: /^Mural/ }).click();
    await daProfessora.getByRole("button", { name: "Novo aviso" }).click();
    await daProfessora
      .getByRole("checkbox", { name: /^Banco de Dados/ })
      .click();
    await daProfessora
      .getByRole("checkbox", { name: /^Algoritmos e Programação/ })
      .click();
    await daProfessora.getByLabel("Título").fill(titulo);
    await daProfessora
      .getByLabel("Texto")
      .fill("A prova passou para 14/08, no mesmo horário.");
    await daProfessora.getByRole("button", { name: "Publicar aviso" }).click();
    await expect(
      daProfessora.getByRole("heading", { name: "Mural" }),
    ).toBeVisible();
    await expect(daProfessora.getByText(titulo).first()).toBeVisible();
    expect(
      sql(
        `SELECT string_agg(at.turma_id::text, ',' ORDER BY at.turma_id) FROM aviso a
         JOIN aviso_turma at ON at.aviso_id = a.id WHERE a.titulo = '${titulo}';`,
      ),
    ).toBe([professora.turmaId, outraTurma].sort().join(","));
    await daProfessora.close();

    // O aluno: a bolinha do Mural e a do sininho acesas.
    await entrar(page, aluno.email, aluno.senha);
    await ateAcender(page, "Notificações, com novidade");
    await expect(
      page.getByRole("tab", { name: "Mural, com novidade" }),
    ).toBeVisible();

    // No Mural, o aviso não lido vem aberto; "Abrir" leva ao aviso inteiro e registra a
    // leitura, e de volta ao Mural ele vem recolhido.
    await page.getByRole("tab", { name: /^Mural/ }).click();
    await expect(
      page.getByRole("heading", { name: `${titulo}, não lido` }),
    ).toBeVisible();
    await page.getByRole("link", { name: `Abrir o aviso ${titulo}` }).click();
    await expect(page.getByRole("heading", { name: titulo })).toBeVisible();
    await page.getByRole("button", { name: "Voltar" }).click();
    await expect(
      page.getByRole("button", { name: `${titulo}. Mostrar aviso` }),
    ).toBeVisible();

    // "Ler todos" apaga a bolinha do Mural, mesmo com aviso de outra execução chegando.
    const lerTodos = page.getByRole("button", { name: "Ler todos os avisos" });
    if (await lerTodos.isEnabled()) {
      await lerTodos.click();
    }
    await expect(
      page.getByRole("tab", { name: "Mural", exact: true }),
    ).toBeVisible();

    // O sininho tem o aviso; abrir marca tudo como lido e apaga a bolinha dele.
    await page.getByRole("button", { name: /^Notificações/ }).click();
    await expect(
      page.getByText(`Aviso novo no Mural: ${titulo}`),
    ).toBeVisible();
    await page.getByRole("button", { name: "Voltar" }).click();
    await expect(
      page.getByRole("button", { name: "Notificações", exact: true }),
    ).toBeVisible();
  },
);

test(
  "a medalha concedida acende o sininho e Troféus, e o toque na notificação leva a Troféus",
  { tag: "@celular" },
  async ({ page, request }) => {
    test.setTimeout(90_000);
    const professora = await novaProfessora(request);
    const nome = `Aluno ${Date.now().toString().slice(-6)}`;
    const aluno = await novaConta(request, nome);
    matricular(aluno, professora.turmaId);
    await semNovidades(request, aluno);

    // A concessão pela API da professora: a tela de concessão já tem o e2e dela (passo 6).
    const headers = comToken(
      await token(request, professora.email, professora.senha),
    );
    const alunos = await (
      await request.get(
        `/api/v1/professor/turmas/${professora.turmaId}/alunos`,
        { headers },
      )
    ).json();
    const alunoId = alunos.find(
      (linha: { nomeDeExibicao: string }) => linha.nomeDeExibicao === nome,
    ).id;
    expect(
      (
        await request.post(
          `/api/v1/professor/turmas/${professora.turmaId}/medalhas`,
          {
            headers,
            data: { alunoId, item: "M01" },
          },
        )
      ).status(),
    ).toBe(201);

    await entrar(page, aluno.email, aluno.senha);
    await ateAcender(page, "Notificações, com novidade");
    await expect(
      page.getByRole("tab", { name: "Troféus, com novidade" }),
    ).toBeVisible();

    await page
      .getByRole("button", { name: "Notificações, com novidade" })
      .click();
    await page
      .getByRole("button", {
        name: /^Você recebeu a medalha Nota 10 em prova\./,
      })
      .click();

    // Em Troféus, abrir a aba apaga a bolinha dela; o sininho já foi lido ao abrir. A tela
    // tem outra aba chamada "Troféus" (a sublinhada, ao lado de Medalhas): por isso a
    // conferência é a de que a aba com novidade sumiu.
    await expect(page).toHaveURL(/\/trofeus$/);
    await expect(
      page.getByRole("tab", { name: "Troféus, com novidade" }),
    ).toHaveCount(0);
    await page.getByRole("tab", { name: /^Home/ }).click();
    await expect(
      page.getByRole("button", { name: "Notificações", exact: true }),
    ).toBeVisible();
  },
);
