import { execFileSync } from "node:child_process";
import { randomInt } from "node:crypto";
import path from "node:path";

import { expect, type APIRequestContext } from "@playwright/test";

// Contas próprias de cada teste, para o e2e poder rodar de novo, em quatro projetos e com
// repetição na CI, sem esbarrar em "um RA, uma conta" nem no limite de 3 cadastros por RA.
// A linha do cadastro elegível entra direto no Postgres do compose, porque a faculdade é
// quem abastece essa base: a API não tem rota para isso, e não deve ter. Exige o compose
// local de pé com o `seed demo` (curso CC).

const RAIZ = path.resolve(__dirname, "../../..");

export interface Aluno {
  ra: string;
  email: string;
  nomeCompleto: string;
  prefixo: string;
  senha: string;
}

export function sql(comando: string): string {
  return execFileSync(
    "docker",
    [
      "compose",
      "exec",
      "-T",
      "postgres",
      "sh",
      "-c",
      'psql -q -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tA',
    ],
    { cwd: RAIZ, input: comando, encoding: "utf8" },
  ).trim();
}

// Uma linha nova de aluno no cadastro elegível, ainda sem conta. Só dígitos e letras
// geradas aqui vão para o SQL.
export function novoElegivel(): Aluno {
  const n = `${Date.now()}${randomInt(100, 999)}`.slice(-11);
  const aluno: Aluno = {
    ra: `9${n}`,
    email: `e2e.${n}@al.unieduk.com.br`,
    nomeCompleto: `Teste E2E ${n}`,
    prefixo: `e2e${n}`,
    senha: `senha-e2e-${n}`,
  };

  const inseridas = sql(
    `INSERT INTO cadastro_elegivel (id, papeis, ra, email, nome_completo, curso_id, semestre)
     SELECT gen_random_uuid(), 1, '${aluno.ra}', '${aluno.email}', '${aluno.nomeCompleto}', id, 1
     FROM curso WHERE sigla = 'CC'
     RETURNING id;`,
  );
  if (inseridas.split("\n").filter(Boolean).length !== 1) {
    throw new Error(
      "Curso CC ausente: rode docker compose run --rm seeder seed demo.",
    );
  }

  return aluno;
}

// Um aluno com conta, criada pela API como o app cria, com o termo vigente aceito.
export async function novaConta(
  request: APIRequestContext,
  nomeDeExibicao = "Teste",
): Promise<Aluno> {
  const aluno = novoElegivel();
  const termo = await request.get("/api/v1/termo-de-uso");
  expect(termo.ok()).toBe(true);

  const cadastro = await request.post("/api/v1/auth/cadastro", {
    data: {
      ra: aluno.ra,
      email: aluno.email,
      nomeDeExibicao,
      prefixo: aluno.prefixo,
      senha: aluno.senha,
      versaoDoTermo: (await termo.json()).versao,
    },
  });
  expect(cadastro.status()).toBe(201);
  return aluno;
}

// As versões do termo que o aluno aceitou, lidas do banco.
export function versoesAceitas(aluno: Aluno): number[] {
  return sql(
    `SELECT a.versao FROM aceite_de_termo a
     JOIN usuario u ON u.id = a.usuario_id
     JOIN cadastro_elegivel c ON c.id = u.cadastro_elegivel_id
     WHERE c.ra = '${aluno.ra}' ORDER BY a.versao;`,
  )
    .split("\n")
    .filter(Boolean)
    .map(Number);
}

// Como o arroba aparece: o sufixo é o do curso.
export const arroba = (aluno: Aluno) => `@${aluno.prefixo}-CC`;
