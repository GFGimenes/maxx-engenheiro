# Operação do Maxx na VPS

Roteiro do ambiente de produção: uma VPS Hetzner CX23 (Ubuntu 24.04, Docker Engine e
Compose), em `app.maxx-unimax.com`, com o Cloudflare R2 nas fotos e o Brevo no e-mail.
Decisões em [`decisoes/README.md`](decisoes/README.md), Entrega e infraestrutura.

## Como está montada

| Item | Onde |
|---|---|
| Pasta | `/opt/maxx`, com `docker-compose.producao.yml`, `docker-compose.demo.yml` e `.env` |
| Segredos | Só no `/opt/maxx/.env`, escrito à mão na VPS. Os nomes estão em `.env.producao.example`; nenhum valor sai da VPS |
| Imagens | `ghcr.io/gfgimenes/maxx-engenheiro-api` e `ghcr.io/gfgimenes/maxx-engenheiro-caddy`, públicas, publicadas pelo job `publicar` da CI |
| Versão no ar | `MAXX_IMAGEM_TAG` no `.env`: `sha-<curto>` de uma publicação, ou `latest`, só da `main`. É a única linha do `.env` que muda num deploy |
| Portas | 80 e 443, só do Caddy. Postgres e API ficam na rede interna do compose |
| HTTPS | O Caddy emite o certificado do Let's Encrypt sozinho, pela porta 80, porque o endereço do site é a `MAXX_URL_PUBLICA` com `https://`. O DNS na Cloudflare aponta direto para a VPS, sem proxy. Os certificados ficam no volume `caddy-data` |

Todos os comandos abaixo rodam em `/opt/maxx`. Para encurtar:

```sh
cd /opt/maxx
alias dc='docker compose -f docker-compose.producao.yml'
alias dcdemo='docker compose -f docker-compose.producao.yml -f docker-compose.demo.yml'
```

## Publicar uma versão

Na `main`, o job `publicar` roda a cada push, com as tags `latest` e `sha-<curto>`. Antes de
a rodada chegar à `main`, a publicação é pelo disparo à mão da CI na branch, só com
`sha-<curto>`:

```sh
gh workflow run ci.yml --ref <branch>
```

O job só publica com os seis jobs verdes. A tag sai no fim do log do passo "Constrói e
publica".

## Trazer os arquivos do compose

Os dois arquivos do compose vêm do repositório, no mesmo commit da imagem:

```sh
sha=<commit completo>
base=https://raw.githubusercontent.com/GFGimenes/maxx-engenheiro/$sha
curl -fsSLO "$base/docker-compose.producao.yml"
curl -fsSLO "$base/docker-compose.demo.yml"
```

## Conferir o `.env` sem ler os valores

Lista cada nome exigido que falta ou está vazio, sem imprimir valor nenhum:

```sh
for nome in $(sed -n 's/^\([A-Z_]*\)=.*/\1/p' .env.producao.example); do
  grep -q "^$nome=..*" .env || echo "falta: $nome"
done
```

O `.env.producao.example` vem do repositório, como os arquivos do compose. O próprio
compose confere de novo: faltando um valor, ele recusa subir e diz o nome.

## Subir e atualizar

```sh
sed -i "s/^MAXX_IMAGEM_TAG=.*/MAXX_IMAGEM_TAG=sha-<curto>/" .env
dc pull
dc up -d                 # migrator, seed base, réplicas, worker e Caddy, nessa ordem
dc ps
curl -fsS https://app.maxx-unimax.com/api/ready
```

Com a demonstração ligada (só durante o ensaio e a avaliação), troque `dc` por `dcdemo`
no `up`. Voltar a `dc up -d` desliga a flag, e as rotas de demonstração voltam a responder
404.

Logs: `dc logs -f --tail=200 api worker caddy`. O log de cada contêiner tem teto de
5 arquivos de 10 MB.

## Seed de demonstração

Uma vez, depois da primeira subida. Ele sobe as fotos dos posts do mundo para o R2 e
deixa cerca de 1.500 trabalhos na fila do worker:

```sh
dc run --rm seeder seed demo
```

Tempo de drenagem da fila: repetir até chegar a zero.

```sh
dc exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc \
  "SELECT count(*) FROM trabalho WHERE estado = '"'"'pendente'"'"' AND disponivel_em <= now()"'
```

## Primeiro acesso de professor e instituição

As contas do seed não têm senha. Os e-mails delas estão em `maxx-unimax.com`, e o Email
Routing da Cloudflare entrega tudo nesse domínio na caixa do autor. Na tela de login, "Esqueci
a senha" com o e-mail da conta: o worker manda o link pelo SMTP de produção, e a senha nova é
o primeiro acesso.

## Verificação do R2

Uma vez, contra o bucket de verdade, num contêiner do SDK na própria VPS: as credenciais
do R2 não saem dela. Só as linhas do R2 entram no contêiner, por um arquivo temporário que
só o root lê e que é apagado no fim.

```sh
git clone --depth 1 https://github.com/GFGimenes/maxx-engenheiro.git /tmp/maxx-src
(umask 077; grep '^MAXX_ARMAZENAMENTO_' /opt/maxx/.env > /root/r2.env)
docker run --rm -v /tmp/maxx-src:/src -w /src --env-file /root/r2.env \
  -e MAXX_VERIFICAR_R2=1 -e MAXX_VERIFICAR_R2_ORIGEM=https://app.maxx-unimax.com \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test --project apps/api/tests/Maxx.Api.IntegrationTests \
    --filter-class Maxx.Api.IntegrationTests.Fotos.VerificacaoDoR2Tests
rm -rf /tmp/maxx-src /root/r2.env
```

O `--env-file` do Docker não tira aspas: as linhas do R2 no `.env` ficam sem aspas.

Para provar uma mudança ainda sem commit, o clone pode ser da branch (`--branch <branch>`),
com o arquivo alterado mandado por cima, um por vez:
`ssh root@<vps> "cat > /tmp/maxx-src/<caminho>" < <caminho>`. Mandar o repositório inteiro
pela conexão de casa é lento demais.

## Saúde do worker e alerta

O worker morto congela o ranking sem erro em tela (ADR 0005). Por isso a API responde por
ele em `https://app.maxx-unimax.com/api/health/worker`: 200 com `"status": "ok"`, ou 503 com
a lista `problemas`. O monitor de fora (UptimeRobot, criado pelo autor) pergunta a cada 5
minutos e manda e-mail no 503. Ele também avisa se a VPS inteira cair.

| Problema | Quando | O que fazer |
|---|---|---|
| `worker-parado` | Sem batimento há mais de 2 minutos (o worker grava um a cada 15 s) | `dc ps worker` e `dc logs --tail=200 worker`; `dc up -d worker` |
| `fila-parada` | Um trabalho disponível espera há mais de 5 minutos | O worker está vivo mas não avança: um handler travado ou o banco lento. Logs do worker |
| `trabalho-falho` | Um trabalho ficou `falho` (5 tentativas) nas últimas 24 horas | Ler o erro no banco (abaixo), corrigir a causa e devolver o trabalho à fila |

```sh
dc exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c \
  "SELECT id, tipo, disponivel_em, ultimo_erro FROM trabalho WHERE estado = '"'"'falho'"'"' ORDER BY id DESC LIMIT 20"'
# Devolver à fila, depois de corrigida a causa:
#   UPDATE trabalho SET estado = 'pendente', tentativas = 0, disponivel_em = now() WHERE id = <id>;
```

O alerta de falho se apaga sozinho 24 horas depois da falha, ou quando o trabalho volta à
fila.

## Voltar uma versão

Trocar `MAXX_IMAGEM_TAG` pela tag anterior e `dc up -d`. Migration não volta sozinha: se a
versão nova trouxe migration, a volta é pela restauração do dump feito antes dela.
