import { execFileSync } from "node:child_process";
import { randomInt } from "node:crypto";
import path from "node:path";

import { expect, type APIRequestContext } from "@playwright/test";

import { sql, type Aluno } from "./contas";
import { linkDeRecuperacao } from "./mailpit";

// O mundo da medalha para um teste: uma professora própria, vinculada à turma de Banco de
// Dados do semestre vigente do curso CC, e os alunos que o teste matricula nela. Como na
// faculdade, cadastro e vínculo entram direto no Postgres; a conta da professora nasce do
// `seed demo`, que cria conta para toda linha de professor do banco (produto.md 2.5), e a
// senha vem do primeiro acesso, pelo e-mail no Mailpit.

const RAIZ = path.resolve(__dirname, "../../..");

export interface Professora {
  email: string;
  nome: string;
  senha: string;
  turmaId: string;
}

// Turma de Banco de Dados, CC, num semestre cujos bimestres cobrem hoje em São Paulo.
function turmaVigente(): string {
  const turma = sql(
    `SELECT t.id FROM turma t
     JOIN materia m ON m.id = t.materia_id
     JOIN curso c ON c.id = m.curso_id
     WHERE c.sigla = 'CC' AND m.nome = 'Banco de Dados' AND t.semestre_letivo_id IN (
       SELECT semestre_letivo_id FROM bimestre GROUP BY semestre_letivo_id
       HAVING min(inicio) <= (now() AT TIME ZONE 'America/Sao_Paulo')::date
          AND max(fim) >= (now() AT TIME ZONE 'America/Sao_Paulo')::date);`,
  );
  if (!/^[0-9a-f-]{36}$/.test(turma)) {
    throw new Error(
      "Sem turma vigente de Banco de Dados em CC: rode o seed demo dentro de um semestre letivo.",
    );
  }
  return turma;
}

export async function novaProfessora(
  request: APIRequestContext,
): Promise<Professora> {
  const n = `${Date.now()}${randomInt(100, 999)}`.slice(-11);
  const professora: Professora = {
    email: `e2e.prof.${n}@prof.unieduk.com.br`,
    nome: `Professora E2E ${n}`,
    senha: `senha-prof-${n}`,
    turmaId: turmaVigente(),
  };

  sql(
    `WITH cadastro AS (
       INSERT INTO cadastro_elegivel (id, papeis, email, nome_completo)
       VALUES (gen_random_uuid(), 2, '${professora.email}', '${professora.nome}')
       RETURNING id)
     INSERT INTO vinculo_professor (id, professor_id, turma_id)
     SELECT gen_random_uuid(), id, '${professora.turmaId}' FROM cadastro;`,
  );
  comandoDaApi(["seed", "demo"]);

  // Primeiro acesso: o link chega ao e-mail institucional.
  expect(
    (
      await request.post("/api/v1/auth/recuperacao", {
        data: { email: professora.email },
      })
    ).status(),
  ).toBe(202);
  const link = await linkDeRecuperacao(request, professora.email);
  const token = new URL(link).hash.replace("#token=", "");
  expect(
    (
      await request.post("/api/v1/auth/recuperacao/confirmar", {
        data: { token, senha: professora.senha },
      })
    ).status(),
  ).toBe(204);

  return professora;
}

export function matricular(aluno: Aluno, turmaId: string) {
  sql(
    `INSERT INTO matricula (id, aluno_id, turma_id)
     SELECT gen_random_uuid(), id, '${turmaId}' FROM cadastro_elegivel WHERE ra = '${aluno.ra}';`,
  );
}

// A medalha ativa do aluno, se houver uma só.
export function medalhaAtiva(aluno: Aluno): string {
  return sql(
    `SELECT m.id FROM medalha m
     JOIN usuario u ON u.id = m.aluno_id
     JOIN cadastro_elegivel c ON c.id = u.cadastro_elegivel_id
     WHERE c.ra = '${aluno.ra}' AND m.anulada_em IS NULL;`,
  );
}

// O comando administrativo da fase 1, dentro do worker (comandoDaApi), com a primeira
// conta da instituição do seed demo.
export function anularMedalha(medalhaId: string): string {
  const instituicao = sql(
    `SELECT c.email FROM cadastro_elegivel c
     JOIN usuario u ON u.cadastro_elegivel_id = c.id
     WHERE c.papeis & 4 = 4 ORDER BY c.email LIMIT 1;`,
  );
  return comandoDaApi([
    "anular-medalha",
    medalhaId,
    "--instituicao",
    instituicao,
  ]);
}

// Um subcomando da API (seed, anulação) dentro do worker, que já está de pé com a mesma
// imagem e a mesma conexão. O `compose run` do seeder refazia também o migrator a cada
// chamada (4,6 s contra 1,9 s com a pilha parada, bem mais com a suíte em paralelo), e o
// caso da medalha passava dos 30 s (passo 7, etapa 11).
export function comandoDaApi(argumentos: string[]): string {
  return execFileSync(
    "docker",
    [
      "compose",
      "exec",
      "-T",
      "worker",
      "dotnet",
      "Maxx.Api.dll",
      ...argumentos,
    ],
    { cwd: RAIZ, encoding: "utf8" },
  );
}
