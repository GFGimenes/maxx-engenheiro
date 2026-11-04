# Decisões do Maxx

Índice de todas as decisões técnicas aceitas para a fase 1. As oito que têm troca real
entre alternativas viraram ADR com contexto, opções pesadas, decisão e consequências,
inclusive as negativas. As demais estão aqui em uma linha, porque não houve dúvida
relevante.

Decisões de **produto** não estão aqui: ficam em [`../produto.md`](../produto.md), e as
seis assumidas de propósito, na seção 12 daquele documento.

## ADRs

| # | Decisão | Consequência negativa principal |
|---|---|---|
| [0001](0001-origem-unica.md) | O Caddy serve o web e faz proxy de `/api` | Web e API acoplados no mesmo host; a API não troca de domínio sem atualizar o binário nativo |
| [0002](0002-fotos-fora-da-api.md) | Fotos nunca passam pela API; leitura pública por chave aleatória | A foto é acessível a quem tem a URL, mesmo sem conta |
| [0003](0003-derivadas-no-cliente.md) | As derivadas de imagem são geradas no cliente | O mesmo trabalho existe três vezes, e confiamos no cliente para a dimensão |
| [0004](0004-motor-de-conquistas-declarativo.md) | Conquista é dado declarativo, avaliado por um motor único | Um erro no avaliador quebra todas as conquistas de uma vez |
| [0005](0005-ranking-por-worker.md) | Ranking e raridade em tabela, escritos pelo worker com coalescência | Worker morto congela o ranking sem erro visível em tela |
| [0006](0006-autenticacao-propria.md) | Autenticação própria, com Argon2id e família de refresh | É código de segurança nosso, e não herdamos confirmação de e-mail nem bloqueio por tentativas |
| [0007](0007-snapshot-de-pontos.md) | Tier e pontos congelam na concessão; raridade não | Corrigir valor errado exige migração de dados, não edição de catálogo |
| [0008](0008-fila-no-postgres.md) | Fila numa tabela do Postgres, sem broker e sem Redis | O banco acumula a função de fila e disputa I/O com o feed no pico |

## Entrega e infraestrutura

| Decisão | Detalhe |
|---|---|
| Hospedagem | Docker Compose em uma VPS, Caddy na frente, HTTPS automático com HSTS e cabeçalhos de segurança, CSP no build web |
| Réplicas | Duas réplicas de API sem estado atrás do Caddy: resiliência de deploy, não capacidade |
| Storage | Cloudflare R2 com a CDN da Cloudflare |
| Migrations | Contêiner separado com advisory lock, antes de subir as réplicas. Nunca no startup da aplicação |
| Backup | `pg_dump` noturno para o bucket, mais dump manual obrigatório antes de cada migration, com restauração testada uma vez |
| Banca | Sem plano B local: a VPS é o único ambiente. Se o wifi do campus falhar, a banca entra pela rede móvel e o notebook é roteado pelo celular |
| CI | GitHub Actions, seis jobs: api, mobile, api-client, e2e, segurança e build (imagens da API e do Caddy com o build web) |

## Dados

| Decisão | Detalhe |
|---|---|
| Banco | PostgreSQL com EF Core |
| Catálogo | JSON versionado em `data/catalogo/`, carregado por seeder idempotente pelo código do item. Nunca `HasData` em migration |
| Demonstração | Marcação por flag e por lote, para purga manual em uma transação |
| Tempo | Instantes em UTC; fronteiras de bimestre, semestre e ano interpretadas em `America/Sao_Paulo` |

## Domínio e API

| Decisão | Detalhe |
|---|---|
| Camadas | Quatro projetos (`Maxx.Domain`, `Maxx.Application`, `Maxx.Infrastructure`, `Maxx.Api`), com fatias verticais por recurso dentro de Application |
| Onde mora a regra | Regra de produto no domínio, validação na borda, um formato só de erro |
| Versionamento | API em `/v1`, mudanças só aditivas, porque o app instalado atrasa em relação à API |
| Plataforma | .NET 10 LTS, com a geração de OpenAPI nativa do framework |
| Idioma do código | Domínio em português (`Premio`, `Conquista`, `Turma`, `Contestacao`), técnico em inglês (`Repository`, `Handler`, `Options`) |
| Limite de taxa | Em login, upload e registro de demonstração, com o limitador nativo do ASP.NET Core: por réplica, sem estado compartilhado. Também em cadastro e recuperação de senha, que disparam consulta à base e e-mail. Números na seção Autenticação |
| Validação de entrada | `AddValidation()` nativo do .NET 10 na borda; erro de validação e de domínio saem em ProblemDetails |

## Autenticação

Complementa o [ADR 0006](0006-autenticacao-propria.md) com os números e as escolhas que
ele deixou abertas. Fechadas no plano do passo 3.

| Decisão | Detalhe |
|---|---|
| Access token | JWT de 15 minutos, com `sub` e papéis; nunca a lista de turmas |
| Assinatura | HS256, com uma chave em variável de ambiente compartilhada pelas duas réplicas. Assimétrica não traz ganho: só a própria API valida o token. Sem a chave, a API não sobe fora de desenvolvimento e teste |
| Refresh | 30 dias, renovados a cada rotação, sem teto absoluto na fase 1. Guardado no banco só como hash SHA-256 |
| Refresh na web e no nativo | Na web, cookie `HttpOnly`, `Secure`, `SameSite=Strict`, restrito ao caminho de autenticação; no nativo, no corpo da resposta, para o secure store. O cliente se identifica por cabeçalho |
| Recuperação de senha | Token aleatório de 30 minutos, uso único, guardado só como hash. A resposta ao pedido é sempre a mesma, exista o e-mail ou não. Senha nova revoga todas as famílias de refresh do usuário |
| Hash de senha | Argon2id com `Konscious.Security.Cryptography`, parâmetros do OWASP: 19 MiB de memória, 2 iterações, paralelismo 1. Login de usuário inexistente calcula um hash fictício, para o tempo de resposta não revelar quem tem conta |
| Política de senha | De 8 a 128 caracteres, sem regra de composição, na linha do NIST SP 800-63B |
| E-mail | SMTP genérico com MailKit; Mailpit no compose local. Provedor da VPS escolhido no passo 13 |
| Envio de e-mail | Fora da requisição, num `BackgroundService` com fila em memória, até a fila do Postgres nascer no passo 6, quando migra para ela. Perder um e-mail num restart é aceito: o usuário pede de novo |
| Contas de professor e instituição | Criadas pelo seed, sem senha; primeiro acesso pela recuperação de senha. Ver `produto.md` 2.5 |
| Escopo de turma | Verificado no banco a cada mutação, contra `vinculo_professor`, só nas turmas do semestre letivo vigente, com a data interpretada em `America/Sao_Paulo`. Fora de semestre vigente, o professor não tem escopo |
| Demonstração | Chave aleatória do dispositivo guardada só como hash; lote por configuração; cadastro elegível marcado como demonstração. Ver `produto.md` 11 |

Limites de taxa, por réplica:

| Rota | Limite |
|---|---|
| Login | 5 por minuto por identificador informado e 30 por minuto por IP |
| Cadastro | 3 a cada 15 minutos por RA |
| Recuperação de senha | 3 a cada 15 minutos por e-mail |
| Registro de demonstração | 60 por hora por IP. Alto de propósito: a banca inteira entra pelo mesmo celular roteado |
| Upload | Policy declarada no passo 3; aplicada e testada no passo 7, quando o endpoint nasce |

O IP vem do `X-Forwarded-For` posto pelo Caddy, aceito só dele. Estourado o limite, a
resposta é 429 em ProblemDetails, com `Retry-After`.

## Cliente

| Decisão | Detalhe |
|---|---|
| Stack | Expo com React Native e TypeScript, Expo Router, um código para web, Android e iOS |
| Prioridade | A web é o acesso principal da fase 1; o nativo fica pronto para publicar, com APK gerado, sem publicação em loja |
| Contrato | `openapi-typescript` e `openapi-fetch`, com hooks TanStack Query escritos à mão. A CI falha se o cliente gerado divergir do OpenAPI |
| Cache | Consulta com stale-while-revalidate no app; ETag com 304 no perfil e nos avisos; micro-cache em memória da primeira página do feed só se o teste de carga pedir. Nada que dependa de quem pede vai para cache compartilhado |
| Feed | Paginação por cursor com índice, sem offset, e semente de sessão dentro do cursor para a recomendação aleatória ser estável durante a sessão |
| Tipografia | Archivo instanciado estaticamente em largura óptica 112% e embarcado nas três plataformas, porque React Native não expõe eixo de fonte variável |
| Desktop | O app aparece centralizado numa moldura de largura de celular |

## Processo e qualidade

| Decisão | Detalhe |
|---|---|
| Branch | `<tipo>/<assunto-em-kebab>`, com `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `ci` |
| Commit | Convencional: `<tipo>(<escopo>): <assunto>`, escopos `api`, `mobile`, `client`, `seed`, `infra`, `docs`, `design` |
| Fluxo | Um commit por etapa, revisado antes de cada um; um PR por assunto; as branches saem da rodada e voltam para ela por PR; a rodada vai para a `main` pelo autor do projeto |
| Testes | Unitário no domínio, integração com Postgres real via Testcontainers, e2e com Playwright contra o build web, Maestro mínimo no endurecimento, Jest com `jest-expo` no cliente |
| Carga | k6 versionado no repositório, cenário de leitura a 2.000 usuários virtuais, rodado nos passos de endurecimento |
| Pronto | Testes passando, lint limpo, migration aplicada, OpenAPI regenerado sem diferença no cliente, p95 medido onde houver decisão de cache, e revisão humana do autor |
| Trabalho | Plano de etapas aprovado antes de qualquer código, em todo passo |
