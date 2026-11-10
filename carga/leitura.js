// Carga de leitura do Maxx (passo 13, versão reduzida aprovada pelo autor): 50 usuários
// virtuais por 2 minutos, rodando de dentro da própria VPS, com as contas que o seed demo
// já criou. Não é o cenário do ADR 0005 (2.000 usuários virtuais, 3.200 alunos), que fica
// como pendência; o p95 daqui entra no registro com essa ressalva.
//
// Os alunos do seed não têm senha. O token de acesso de cada usuário virtual é assinado aqui
// com a chave HS256 da API, lida do .env da VPS (MAXX_JWT_CHAVE): a chave nunca sai da VPS e
// nenhum token é impresso. Mesmo formato do JwtAccessTokenIssuer: sub, papel, iss, aud, 15 min.
//
// Rodado pelo carga/rodar.sh, na VPS. Variáveis:
//   MAXX_JWT_CHAVE    a chave da API, em base64
//   MAXX_CARGA_URL    padrão https://app.maxx-unimax.com
//   MAXX_CARGA_ALUNOS arquivo JSON com [{ id, curso }] dos alunos ativos do seed
//   MAXX_CARGA_VUS, MAXX_CARGA_DURACAO   padrão 50 e 2m
import crypto from "k6/crypto";
import encoding from "k6/encoding";
import http from "k6/http";
import { check, sleep } from "k6";
import { SharedArray } from "k6/data";

const BASE = __ENV.MAXX_CARGA_URL || "https://app.maxx-unimax.com";
const CHAVE = encoding.b64decode(__ENV.MAXX_JWT_CHAVE, "std");
const alunos = new SharedArray("alunos", () =>
  JSON.parse(open(__ENV.MAXX_CARGA_ALUNOS)),
);
// Trechos de nome e arroba para a busca, que passa pelos índices de trigramas.
const TERMOS = [
  "ana",
  "silva",
  "mari",
  "oliv",
  "pedro",
  "lima",
  "santos",
  "jul",
];

export const options = {
  scenarios: {
    leitura: {
      executor: "constant-vus",
      vus: Number(__ENV.MAXX_CARGA_VUS || 50),
      duration: __ENV.MAXX_CARGA_DURACAO || "2m",
    },
  },
  // O alvo de p95 do ADR 0005 (300 ms), rota por rota: além de cobrar, faz o resumo do k6
  // abrir o p95 de cada uma.
  thresholds: {
    http_req_failed: ["rate<0.01"],
    ...Object.fromEntries(
      [
        "feed",
        "feed-pagina-2",
        "perfil",
        "ranking-geral",
        "ranking-curso",
        "mural",
        "novidades",
        "busca",
      ].map((rota) => [`http_req_duration{rota:${rota}}`, ["p(95)<300"]]),
    ),
  },
  summaryTrendStats: ["avg", "med", "p(90)", "p(95)", "p(99)", "max"],
};

function base64url(objeto) {
  return encoding.b64encode(JSON.stringify(objeto), "rawurl");
}

function tokenDe(id) {
  const agora = Math.floor(Date.now() / 1000);
  const cabecalho = base64url({ alg: "HS256", typ: "JWT" });
  const corpo = base64url({
    sub: id,
    papel: ["aluno"],
    iss: "maxx-api",
    aud: "maxx",
    iat: agora,
    nbf: agora,
    exp: agora + 15 * 60,
  });
  const assinatura = crypto.hmac(
    "sha256",
    CHAVE,
    `${cabecalho}.${corpo}`,
    "base64rawurl",
  );
  return `${cabecalho}.${corpo}.${assinatura}`;
}

function sorteio(lista) {
  return lista[Math.floor(Math.random() * lista.length)];
}

// Estado de cada usuário virtual: quem ele é e a ETag do mural que já leu.
let eu;
let cabecalhos;
let etagDoMural;

function pedir(rota, nome, extras = {}) {
  const resposta = http.get(`${BASE}${rota}`, {
    headers: { ...cabecalhos, ...extras },
    tags: { rota: nome },
  });
  check(resposta, {
    [`${nome} 2xx ou 304`]: (r) =>
      (r.status >= 200 && r.status < 300) || r.status === 304,
  });
  return resposta;
}

export default function () {
  if (!eu) {
    eu = alunos[(__VU - 1) % alunos.length];
    cabecalhos = { Authorization: `Bearer ${tokenDe(eu.id)}` };
  }

  // Feed: a primeira página e a seguinte, que já entra no trecho de recomendação.
  const feed = pedir("/api/v1/feed", "feed");
  const proximo = feed.status === 200 ? feed.json("proximo") : null;
  if (proximo) {
    pedir(
      `/api/v1/feed?cursor=${encodeURIComponent(proximo)}`,
      "feed-pagina-2",
    );
  }
  sleep(1);

  // Perfil de um colega, com a posição no ranking.
  pedir(`/api/v1/perfis/${sorteio(alunos).id}`, "perfil");
  sleep(1);

  // Ranking: o geral do semestre e o do curso, as duas visões mais abertas.
  pedir("/api/v1/rankings?escopo=geral&periodo=semestre", "ranking-geral");
  pedir("/api/v1/rankings?escopo=curso&periodo=semestre", "ranking-curso");
  sleep(1);

  // Mural com a ETag da leitura anterior: o caso comum é o 304.
  const mural = pedir(
    "/api/v1/mural",
    "mural",
    etagDoMural ? { "If-None-Match": etagDoMural } : {},
  );
  etagDoMural = mural.headers.Etag || mural.headers.ETag || etagDoMural;

  // Novidades: no app, a cada 60 s e ao voltar ao foco.
  pedir("/api/v1/eu/novidades", "novidades");
  sleep(1);

  pedir(`/api/v1/busca/alunos?termo=${sorteio(TERMOS)}`, "busca");
  sleep(Math.random() * 2);
}
