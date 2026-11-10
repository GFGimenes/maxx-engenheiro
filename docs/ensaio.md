# Ensaio da demonstração remota

Roteiro do ensaio do passo 13. A avaliação é remota, antes de 05/12/2026, sem apresentação
presencial: cada avaliador entra em `https://app.maxx-unimax.com` pela própria conexão
(`decisoes/README.md`, Avaliação). O ensaio repete isso de fora da VPS, por conexões
diferentes, com a demonstração ligada, e termina com a purga do lote de ensaio pela tela da
instituição, para a purga real, depois da avaliação, ser rotina. Comandos da VPS em
[`operacao.md`](operacao.md).

## Antes

| O quê | Quem |
|---|---|
| Primeiro acesso da coordenação e de ao menos um professor de cada curso que o ensaio usar, pela recuperação de senha (os e-mails chegam pelo Email Routing) | Autor |
| `MAXX_DEMO_LOTE` no `.env` com um lote só do ensaio (por exemplo `ensaio-2026-11`), diferente do lote da avaliação | Autor |
| Subir com a demonstração ligada: `deploy.sh <sha> demo` | Engenheiro |
| Conferir `GET /api/v1/demo` com os quatro cursos, `/api/health/worker` em 200 e o backup da noite anterior em `/opt/maxx/backups` | Engenheiro |
| Ao menos três participantes, cada um numa conexão: 4G de uma operadora, banda larga de casa e uma terceira (outra operadora, outra cidade ou outra rede) | Autor |

## Durante

Cada participante, no próprio celular, pela própria conexão. Anotar a conexão, o aparelho
e o navegador.

1. Abrir `https://app.maxx-unimax.com` com o cache limpo e cronometrar até a tela de login
   aparecer (risco do bundle grande no primeiro acesso, plano mestre, Riscos).
2. Entrar pela demonstração: nome, curso e foto de perfil. Aceitar o termo.
3. Publicar um post com foto e legenda. Conferir que a foto aparece no feed (PUT direto ao
   R2 e leitura pela CDN, sob a CSP de produção).
4. Curtir e comentar o post de outro participante. Seguir alguém.
5. Abrir Troféus, o ranking geral e o do curso, o Mural e o sininho.
6. Reivindicar um prêmio de participação com foto.

O autor, como professor e como instituição, pelo navegador do computador:

7. Conceder uma medalha a um participante. **O participante confere a posição no ranking
   mudar em segundos**: é o momento de maior impacto da avaliação (ADR 0005).
8. Aprovar a reivindicação do passo 6 pela fila de comprovações.
9. Publicar um aviso de curso; os participantes daquele curso o veem no Mural.
10. Voltar ao app pelo mesmo celular e entrar no usuário criado, pela lembrança local.

O engenheiro, na VPS, durante todo o ensaio: logs da API, do worker e do Caddy,
`docker stats`, `/api/health/worker` e a fila. Qualquer erro em tela é anotado com a hora,
para cruzar com o log.

## Depois

1. **Purga de ensaio pela tela**: a instituição abre `/instituicao/purga`, escolhe o lote do
   ensaio e digita o nome dele de novo.
2. Conferir na VPS: nenhum usuário com o lote do ensaio, e o registro em
   `purga_da_demonstracao` com o número de alunos e de fotos.
3. **Limpar o cache da CDN**: no painel da Cloudflare, Caching, Purge Cache, Purge
   Everything na zona. A purga tira as fotos do bucket, mas a borda da Cloudflare continua
   servindo a cópia que já tinha até ela expirar (limite conhecido, `decisoes/README.md`,
   passo 13).
4. Conferir no R2: a URL de uma foto de post do ensaio (anotada no passo 3) responde 404
   pela CDN, sem parâmetro nenhum na URL.
5. Trocar `MAXX_DEMO_LOTE` pelo lote da avaliação (autor) e subir de novo com a
   demonstração ligada.

## O que registrar

| Medida | Onde |
|---|---|
| Tempo até a tela de login, por conexão e aparelho | `decisoes/README.md`, passo 13 |
| Segundos entre a medalha e a posição nova no ranking | Idem |
| Erros em tela e no log, com a causa | Idem, e correção antes da avaliação |
| Contas e fotos removidas pela purga, e o tempo até a fila esvaziar | Idem |
