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
| Storage | Cloudflare R2 com a CDN da Cloudflare em produção. No compose, na CI e nos testes de integração, o emulador VersityGW, compatível com S3; trocar um pelo outro muda só o `.env`. Ver Posts, upload e feed (passo 7) |
| Migrations | Contêiner separado com advisory lock, antes de subir as réplicas. Nunca no startup da aplicação |
| Worker | Contêiner separado, `Maxx.Api worker`, uma réplica, consumindo a fila do Postgres (ADR 0008). Ver Medalhas, fila e worker (passo 6) |
| Backup | `pg_dump` noturno para o bucket, mais dump manual obrigatório antes de cada migration, com restauração testada uma vez |
| Avaliação | Sem plano B local: a VPS é o único ambiente. A avaliação é remota, antes de 05/12, sem apresentação presencial: cada avaliador entra na VPS pela própria conexão, e não há rede do campus nem aparelho compartilhado de que dependa. O ensaio do passo 13 confirma o acesso de fora, por conexões diferentes |
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
| Formato de erro | ProblemDetails (RFC 9457) com a extensão `codigo`, estável e em kebab-case (`requisicao-invalida`, por exemplo), que o cliente traduz em mensagem amigável. Todo erro sai com `codigo`: quem não traz um próprio ganha o do status (`dados-invalidos`, `nao-autenticado`, `sem-permissao`, `nao-encontrado`, `limite-de-taxa`, `erro-interno` e outros). A mensagem interna de uma regra de domínio violada vai para o log, nunca para a resposta |

## Autenticação

Complementa o [ADR 0006](0006-autenticacao-propria.md) com os números e as escolhas que
ele deixou abertas. Fechadas no plano do passo 3.

| Decisão | Detalhe |
|---|---|
| Access token | JWT de 15 minutos, com `sub` e papéis; nunca a lista de turmas |
| Assinatura | HS256, com uma chave em variável de ambiente compartilhada pelas duas réplicas. Assimétrica não traz ganho: só a própria API valida o token. Sem a chave, a API não sobe fora de desenvolvimento e teste |
| Refresh | 30 dias, renovados a cada rotação, sem teto absoluto na fase 1. Guardado no banco só como hash SHA-256 |
| Refresh na web e no nativo | Na web, cookie `maxx_refresh`, `HttpOnly`, `Secure`, `SameSite=Strict`, restrito a `/api/v1/auth`; no nativo, no corpo da resposta, para o secure store. O cabeçalho `X-Maxx-Cliente: web` escolhe o cookie; sem ele, o refresh vai no corpo |
| Cookie só com o cabeçalho | Na web, o cookie do refresh só é lido quando o pedido traz `X-Maxx-Cliente: web`: um formulário de outro site não manda cabeçalho próprio, defesa somada ao `SameSite=Strict`. Refresh recusado e logout apagam o cookie |
| Refresh simultâneo | Conta como reuso. Quem perde a corrida relê o banco, encontra o token já trocado e derruba a família: simultâneo ou em sequência, o resultado é o mesmo. Por isso o cliente serializa o refresh (passo 5) |
| Sessão recusada | Um só código, `sessao-invalida`, para refresh ausente, desconhecido, expirado, de família revogada ou reusado: em todos os casos o cliente volta ao login. O motivo exato fica só no log |
| Recuperação de senha | Token aleatório de 30 minutos contados do envio, uso único, guardado só como hash e emitido pelo worker na hora de mandar o e-mail (ver Envio de e-mail). A resposta ao pedido é sempre a mesma, exista o e-mail ou não. Senha nova revoga todas as famílias de refresh do usuário e invalida os outros pedidos de recuperação pendentes dele |
| Link de recuperação | `<App__UrlPublica>/redefinir-senha#token=...`, com o token no fragmento: o navegador não manda a servidor nenhum o que vem depois do `#`, e o token não cai em log de acesso nem em `Referer`. A tela é do passo 5. Sem `App__UrlPublica`, a API não sobe fora de desenvolvimento |
| Hash de senha | Argon2id com `Konscious.Security.Cryptography`, parâmetros do OWASP: 19 MiB de memória, 2 iterações, paralelismo 1. Login de usuário inexistente calcula um hash fictício, para o tempo de resposta não revelar quem tem conta |
| Política de senha | De 8 a 128 caracteres, sem regra de composição, na linha do NIST SP 800-63B |
| Normalização da senha | NFC antes do hash, no cadastro e no login: o "ç" composto de um teclado e o decomposto de outro são a mesma senha |
| Tolerância de relógio | 30 segundos na validação do access token; as réplicas estão no mesmo host |
| Chave em desenvolvimento | Sem `Jwt__Chave` em Development, a API gera uma chave por processo, que some no restart. Nenhuma chave versionada, nem a de desenvolvimento |
| E-mail | SMTP genérico com MailKit; Mailpit no compose local. Provedor da VPS escolhido no passo 13 |
| Envio de e-mail | Desde o passo 6, por um trabalho da fila do Postgres (ADR 0008), executado só pelo worker, que é quem tem o SMTP. O pedido de recuperação grava o pedido sem token e enfileira só o id dele, na mesma transação: nenhum segredo espera em claro na fila. O worker emite o token, grava o hash e manda o e-mail na transação do trabalho. Falha de envio desfaz o hash e volta com o recuo da fila; a retentativa emite outro token. Pedido já encerrado quando o trabalho roda (outro link trocou a senha) não gera e-mail. Se o envio sai e a transação não fecha, o link que chegou não vale, e o usuário pede outro |
| Contas de professor e instituição | Criadas pelo seed demo, sem senha, para toda linha de professor e de instituição que estiver no banco, não só as do arquivo; primeiro acesso pela recuperação de senha. Ver `produto.md` 2.5 |
| Login exigido | Toda rota de `/v1` exige login, e a pública declara `AllowAnonymous`. A exigência fica no grupo `/v1`, e não numa `FallbackPolicy`, porque a fallback vale também para pedido sem endpoint, e rota inexistente tem de continuar 404 |
| `GET /v1/eu` | Lê papéis e arroba do banco, não do token: o arroba pode mudar depois da emissão |
| Escopo de turma | Verificado no banco a cada mutação, contra `vinculo_professor`, só nas turmas do semestre letivo vigente, com a data interpretada em `America/Sao_Paulo`. Fora de semestre vigente, o professor não tem escopo |
| Demonstração | Chave aleatória do dispositivo guardada só como hash; lote por configuração; cadastro elegível marcado como demonstração. Ver `produto.md` 11 |
| Flag da demonstração | `Demo__Habilitada` só pelo `docker-compose.demo.yml`, por cima do `docker-compose.yml`, que nunca a menciona. Desligada, as rotas nem são mapeadas, e o 404 é o de uma rota inexistente; a geração do OpenAPI as mapeia mesmo assim, para o cliente ter os tipos. Ligada, `Demo__Lote` é obrigatório, ou a API não sobe |
| Aluno de demonstração | Escolhe o curso pela sigla, entre os que têm turma no semestre vigente, e é matriculado nas turmas vigentes dele. RA gerado no formato `DEMO` mais 8 caracteres, fora do formato numérico dos RAs da faculdade; e-mail `<ra>@demonstracao.invalid`, domínio reservado que nunca recebe nada; semestre 1. Sem senha e sem recuperação: entra só pela chave do dispositivo, que vai sempre no corpo da resposta, na web também |
| Registro simultâneo | Mesmo nome registrado ao mesmo tempo: em conflito de prefixo, o registro não repete a sugestão recusada e avança para a próxima livre, consultando os prefixos já reservados. Até 10 tentativas. Cada recusa vem de outro pedido que gravou, então o limite cobre até 10 pedidos simultâneos com o mesmo nome; acima disso, alguns podem receber a recusa. Esgotadas, responde 409 com `registro-simultaneo`, nunca 500 |
| Termo de uso | Um arquivo por versão em `data/termos/termo-de-uso.v<n>.md`, gravado pelo seed base em `termo_de_uso` e servido por `GET /v1/termo-de-uso`, sem login: o app instalado atrasa, e o texto do aceite tem de ser o vigente. A vigente é a versão de número mais alto. Versão publicada não muda nem sai: texto alterado ou arquivo apagado faz o seed falhar sem gravar nada, e texto novo entra como a versão seguinte. O fim de linha do checkout não conta como mudança. O texto usa só parágrafos e títulos `## `. Sem termo publicado, a rota responde 404 com `termo-indisponivel` |
| Aceite do termo | Aceitam o cadastro do aluno e o registro da demonstração; professor e instituição não aceitam no primeiro acesso, e versão nova não pede novo aceite na fase 1. O aceite grava usuário, versão e instante em `aceite_de_termo`, na mesma transação da conta, e sai em cascata com o usuário. Uma versão aceita por alguém não pode ser apagada. Aceite de versão que não é a vigente: 409 com `termo-desatualizado`, e a conta não nasce |
| Exceção às mudanças aditivas | `versaoDoTermo` entrou como campo obrigatório em `POST /v1/auth/cadastro` e `POST /v1/demo/alunos` no passo 5, quando nenhum app consumia essas rotas. Opcional, ele deixaria criar conta sem aceite. Exceção única e registrada: daqui em diante, só mudanças aditivas |

Limites de taxa, por réplica:

| Rota | Limite |
|---|---|
| Login | 5 por minuto por identificador informado e 30 por minuto por IP |
| Cadastro | 3 a cada 15 minutos por RA |
| Sugestão e disponibilidade do arroba | 60 por minuto por IP, somadas as duas: elas respondem diferente para um par de RA e e-mail que existe. Cabe na digitação porque o cliente do passo 5 faz debounce antes de perguntar |
| Recuperação de senha | 3 a cada 15 minutos por e-mail, só no pedido. A confirmação não tem limite: o token tem 256 bits |
| Registro de demonstração | 60 por hora por IP. Alto de propósito: na avaliação remota, cada avaliador entra pela própria conexão, mas a rede móvel costuma pôr muitos aparelhos atrás do mesmo IP (CGNAT da operadora), e o ensaio do passo 13 cria várias contas a partir de poucas conexões. Um limite baixo recusaria avaliador legítimo. A entrada pela chave do dispositivo não tem limite: a chave tem 256 bits |
| Upload | 30 por hora por usuário, contando pedidos de assinatura: cada pedido cobre as duas derivadas. Declarado no passo 3, aplicado e testado no passo 7. Um usuário não esgota o limite de outro |

O IP vem do `X-Forwarded-For` posto pelo Caddy, aceito só dele. Estourado o limite, a
resposta é 429 em ProblemDetails, com `Retry-After`.

Os limites são filtros de endpoint sobre os limitadores nativos de
`System.Threading.RateLimiting`, e não policies do middleware: três das chaves estão no
corpo (identificador, RA, e-mail), e o particionador do middleware roda antes de o corpo
ser lido. Janela fixa, sem fila; toda tentativa conta, inclusive a que dá certo. A chave
é comparada sem espaços nas pontas e em minúsculas. O log registra só o nome do limite,
nunca a chave, que é dado pessoal.

### Purga da demonstração

Depois da avaliação, os dados de um lote saem numa transação só, com o lote que estava
em `MAXX_DEMO_LOTE`:

```sql
BEGIN;
CREATE TEMP TABLE alvo ON COMMIT DROP AS
  SELECT id AS usuario_id, cadastro_elegivel_id AS cadastro_id
  FROM usuario WHERE lote_da_demonstracao = :'lote';
DELETE FROM familia_sessao WHERE usuario_id IN (SELECT usuario_id FROM alvo);
DELETE FROM recuperacao_senha WHERE usuario_id IN (SELECT usuario_id FROM alvo);
DELETE FROM notificacao WHERE usuario_id IN (SELECT usuario_id FROM alvo);
DELETE FROM curtida WHERE usuario_id IN (SELECT usuario_id FROM alvo);
DELETE FROM comentario WHERE autor_id IN (SELECT usuario_id FROM alvo);
DELETE FROM seguimento WHERE seguidor_id IN (SELECT usuario_id FROM alvo) OR seguido_id IN (SELECT usuario_id FROM alvo);
DELETE FROM comprovacao WHERE aluno_id IN (SELECT usuario_id FROM alvo);
DELETE FROM post WHERE autor_id IN (SELECT usuario_id FROM alvo);
DELETE FROM contestacao WHERE premio_participacao_id IN (SELECT id FROM premio_participacao WHERE aluno_id IN (SELECT usuario_id FROM alvo));
DELETE FROM premio_participacao WHERE aluno_id IN (SELECT usuario_id FROM alvo);
DELETE FROM evento WHERE criado_por_id IN (SELECT usuario_id FROM alvo);
DELETE FROM medalha WHERE aluno_id IN (SELECT usuario_id FROM alvo);
DELETE FROM upload_pendente WHERE usuario_id IN (SELECT usuario_id FROM alvo);
DELETE FROM usuario WHERE id IN (SELECT usuario_id FROM alvo);
DELETE FROM matricula WHERE aluno_id IN (SELECT cadastro_id FROM alvo);
DELETE FROM cadastro_elegivel WHERE id IN (SELECT cadastro_id FROM alvo) AND demonstracao;
COMMIT;
```

Rodado com `psql -v lote=<lote> -v ON_ERROR_STOP=1`. Os prefixos reservados, os aceites do
termo e os refresh saem em cascata com o usuário e a família. Curtidas e comentários de
outras pessoas nos posts do lote saem em cascata com o post. A lista cresce com os passos
que gravam dados do aluno (prêmios, pontos): cada um acrescenta aqui a tabela dele, antes
de `usuario`. As fotos no bucket não saem por este SQL, que só alcança o banco: saem na purga
pela área web da instituição, no passo 12.

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
| Identificador do app | `tech.gimenes.maxx`, no Android e no iOS |

### Design system (passo 4)

Os valores vêm de `design/tokens.json` e `design/identidade-visual.md`. A estrutura do JSON
não mudou; só a frase de uso de `surface` perdeu os chips e a regra de raios foi corrigida
para raio 8 nos chips e pill só nos selos de tier. As pranchetas foram renderizadas com fonte
substituta, então não servem de referência de fonte. Componentes técnicos ficam em inglês
(`Button`, `TextField`) e os de domínio em português (`Medalhao`, `TrilhaFamilia`).

| Decisão | Detalhe |
|---|---|
| Tema | Sistema, Claro e Escuro, com Sistema por padrão e a escolha guardada no AsyncStorage. O controle fica na tela de status e na vitrine e, desde o passo 7, no menu do perfil, que é o lugar definitivo |
| Ícones | Só `react-native-svg`, com os caminhos do Lucide 1.70.0 num componente nosso (`Icon`), com a licença ao lado. Traço 1.8 em grade de 24. A taça é o `trophy` e a fita é o `medal`. No preenchido, o desenho interno sai recortado na cor de `detailColor`, o fundo sobre o qual o ícone está |
| Vitrine | Rota `/vitrine` só em `__DEV__`. Em produção, ela volta para o início e nem entra no bundle |
| Trilha | Por itens da família, de 2 a 5 passos, rolando na horizontal quando não couberem. O primeiro não obtido é o próximo, com cadeado; os demais não obtidos ficam só tracejados. A linha é contínua só entre dois obtidos, e uma lacuna no meio fica sem linha contínua. Círculo de 36, passo de 82, com o cartão de título dentro do componente |
| Foco | Anel do token `focus`, com 2 px de espessura e 2 px de afastamento, só por teclado (`:focus-visible` na web). A prancheta mostra ouro no claro; vale o token. O contorno padrão do navegador sai com `outline-style: none`, porque largura 0 não o remove. O campo usa a borda de 1,5 px em `focus`, sem anel |
| Pressionado e desativado | Pressionado com opacidade 0,85, embora a prancheta escureça. Desativado em `surface2` com `ink3`. O segmento ativo desativado mantém `segmentOn`, sem sombra, para a escolha continuar visível |
| Avatar | Iniciais com as cores do token, do primeiro e do último nome, em Archivo 700 a 40% do tamanho. Com foto, a derivada da grade no lugar das iniciais. O anel dourado marca o próprio avatar no cabeçalho do perfil desde o passo 7; o do ranking chega no passo 11 |
| Raios | 8 nos chips. O segmento ativo tem raio 8, valor que vem de `identidade-visual.md`, porque o JSON só traz o raio do trilho |
| Botão | Discreto com 32 de altura, 44 de toque e texto 13. O grande tem 50, em Instrument Sans 700 tamanho 15. Desde o passo 5, o grande é o da ação principal das telas de autenticação |
| Campo | Texto em 16, fora da escala, porque abaixo disso o Safari do iPhone dá zoom ao focar. Rótulo em 14, peso 600. O erro de campo entrou no passo 5 (ver Autenticação no cliente) |
| Controle segmentado | Borda de 1 px em `line` no trilho, como na prancheta, nos dois temas |
| Chip de filtro | Inativo em contorno, como na identidade e na prancheta. 32 de altura e 44 de toque |
| Navegação | Altura de 50 mais a área segura, com mínimo de 84. Halo da bolinha sempre em `bg`. A novidade também vai no rótulo de acessibilidade. Acima da navegação, o `Screen` não soma a área segura de baixo (`aboveNav`) |
| Vazio e erro | Ícone de 32. Texto padrão do erro: "Não deu certo agora." e "Tente de novo em instantes.". `like` só no ícone. O anúncio ao leitor de tela fica só no texto, para não esconder o botão |
| Medalhão | As medidas do ornamento no token valem para o medalhão de 48 e escalam com ele. Lendário com 32 raios. Com raridade, o espaço da folga do lendário fica sempre reservado, para listas alinhadas. Glifo em contorno, a 50% do metal |
| Selo e faixa | Selo com 26 de altura, medalhão de 18 e nome em Archivo 700 tamanho 12. Faixa com raio 16 |
| Fontes | Instâncias estáticas geradas por `design/fontes/gerar.sh`, com fontTools em contêiner e fontes de origem travadas por commit e hash. Uma família por peso, porque o Android não escolhe arquivo por `fontWeight` |
| Logo vetorial | `design/assets/maxx-ouro.svg`, com a procedência em `design/README.md`. Pode ser substituído pelo vetor original, se existir, sem mudança de código |
| Ícone, splash e favicon | Gerados do SVG por `design/icones/gerar.js`. Logo em ouro sobre marinho no ícone e no adaptativo; em ouro sobre o `bg` de cada tema no splash; recorte do XX sobre marinho com cantos arredondados no favicon |
| Limite do splash | O splash nativo segue o tema do aparelho, não a preferência escolhida no app, porque aparece antes de o app abrir |
| Tema no Android | `expo-system-ui`, sem o qual o `userInterfaceStyle` não se aplica no Android |

### Autenticação no cliente (passo 5)

As telas de login, cadastro, termo, recuperação e demonstração, e a sessão por trás delas.
As telas sem prancheta (termo, recuperação, demonstração e os passos 2 e 3 do cadastro)
seguem a identidade visual.

| Decisão | Detalhe |
|---|---|
| Access token | Só em memória. Renovado antes de vencer, com 30 segundos de folga, e uma vez depois de um 401, repetindo o pedido uma vez só, com o mesmo corpo. As rotas de `/v1/auth` passam sem token e sem renovação |
| Refresh serializado | Um refresh por vez na aba: quem chega durante a renovação espera o resultado dela. Na web, também entre abas, pela Web Locks API (`navigator.locks`), porque todas as abas mandam o mesmo cookie e duas abas abrindo juntas contariam como reuso. Sem a API (página fora de HTTPS), fica só a fila da aba |
| Onde o refresh fica | Na web, no cookie, e o JavaScript não guarda nada; todo pedido leva `X-Maxx-Cliente: web`. No nativo, no `expo-secure-store`, e o refresh novo é gravado antes de valer |
| Sessão recusada e falha | Só `sessao-invalida` encerra a sessão. Rede, 429 e 5xx deixam o estado "sem conexão", com o refresh guardado e Tentar de novo. O cache de consultas é esvaziado só na saída de uma sessão autenticada |
| Rotas | O grupo `(app)` exige sessão e manda ao `/login`. O grupo `(publico)` (login, cadastro, esqueci-senha, demonstração) manda quem já entrou para `/`. `/status` e `/redefinir-senha` ficam fora dos grupos. Em `/`, um início provisório (arroba, tema e Sair) até o passo 7, quando `/` vira o feed e o `?voltar=` do deep link entra (ver passo 7) |
| Cadastro em três telas | `/cadastro` segue a prancheta (RA, e-mail, senha) com "Continuar", que pede a sugestão de arroba e com ela confere RA e e-mail; `/cadastro/arroba` pede nome e arroba; `/cadastro/termo` mostra o termo vigente, e a conta nasce no aceite. Os dados ficam só em memória: recarregar no meio volta ao primeiro passo |
| Debounce do arroba | A disponibilidade é consultada 500 ms depois de a digitação parar, e cada consulta cancela a anterior. Arroba fora da forma e a própria sugestão não consultam. 429 ou rede na consulta não travam: o cadastro confere de novo ao criar a conta |
| Primeiro acesso | "Primeiro acesso? Aluno cria a conta em Cadastrar. Professor define a senha em Esqueci minha senha.", sem link, no lugar do Portal UniMax da prancheta, que não existe na fase 1 |
| Recuperação | `/esqueci-senha` pede o link pelo e-mail, com a mesma resposta exista a conta ou não. `/redefinir-senha` lê o `#token=` uma vez, quando o bundle carrega (antes de o roteador mexer na URL), e o apaga com `replaceState`. Sem token, a tela oferece pedir outro. Salvar a senha nova encerra a sessão aberta no aparelho |
| Demonstração | O botão do login aparece só se `GET /v1/demo` responde 200: nenhuma flag no bundle. As contas criadas no aparelho ficam numa lista (`maxx.demo.contas`), no armazenamento local na web e no secure store no nativo, e entram sem senha pela chave. `chave-invalida` tira a conta da lista; `registro-simultaneo` mostra a mensagem e Tentar de novo |
| Mensagens de erro | Uma tabela só, de código para mensagem, em `apps/mobile/src/api/erros.ts`. Regra: um teste lê `CodigosDeRecusa.cs` e `DomainExceptionHandler.cs` e falha se um código da API ficar sem mensagem |
| Erro de campo | Borda em `like` no campo com erro, também em foco, ícone de alerta em `like` e texto em `ink`, na escala `legenda`, anunciado ao leitor de tela, com `aria-invalid`. A borda é exceção registrada à regra do `like` só em ícone. O erro que não é de um campo usa o mesmo desenho, abaixo do botão |
| Botão grande | A ação principal das telas de autenticação (Entrar, Continuar, Aceitar e criar conta, Enviar link, Salvar senha, Aceitar e entrar), como na prancheta do cadastro |
| Logo | Componente `Logo` em vetor, com `react-native-svg` no `gold` do tema. O traçado é gerado de `design/assets/maxx-ouro.svg` por `design/icones/logoVetor.js`, que o `gerar.js` também roda; um teste falha se o SVG mudar sem gerar de novo |
| e2e | Cada teste cria a própria linha do cadastro elegível, direto no Postgres do compose, e a conta pela API; a CI roda o `seed demo`. A recuperação lê o e-mail no Mailpit. Os fluxos com conta rodam só no celular, pelo limite de taxa por IP. A demonstração tem uma segunda passada, com `docker-compose.demo.yml` por cima |

## Medalhas, fila e worker (passo 6)

A concessão de medalha pelo professor, a anulação pela instituição, a fila do Postgres com
o worker e a tela de Troféus. As decisões A a L foram fechadas no plano do passo; as
demais, nas etapas. A regra de produto que nasceu aqui (aluno sem conta não recebe
medalha) está em `produto.md` 4.8.

### Fila e worker

| Decisão | Detalhe |
|---|---|
| Onde roda o worker | Contêiner próprio, `worker` no compose, com a mesma imagem e o comando `Maxx.Api worker`, uma réplica e sem porta. As réplicas da API enfileiram e não consomem |
| Tabela `trabalho` | Tipo, carga em jsonb, estado (`pendente` ou `falho`), tentativas, criado em, disponível em e último erro. Trabalho concluído sai da tabela; o falho fica para alguém olhar. A carga nunca leva segredo |
| Consumo | Um trabalho por vez, com `FOR UPDATE SKIP LOCKED`, e o handler roda na transação que segura a linha: o que ele grava e a saída do trabalho valem juntos. Com a fila vazia, o worker espera 1 s |
| Retentativa | 5 tentativas, com recuo de 10, 20, 40 e 80 s. A falha volta a um ponto de salvamento anterior ao handler e é registrada ainda sob o lock. Na quinta, o trabalho fica `falho`, com tipo e mensagem do erro, sem pilha e sem a carga |
| Limites do handler | Passados 60 s, o trabalho é cancelado e conta como falha. Tipo sem handler também é falha. Queda do processo desfaz a transação, e o trabalho volta sem contar tentativa: por isso todo handler é idempotente |
| Trabalho adiado | `Enfileirar` aceita o instante em que o trabalho fica disponível; é o que a coalescência usa |
| Teste do worker hospedado | O worker sobe antes da migração do banco do teste e registra uma vez "relation trabalho does not exist" antes de se recuperar. Ruído só desse teste, aceito: no compose o worker depende do seeder |

### Concessão e anulação

| Decisão | Detalhe |
|---|---|
| Rotas do professor | `GET /v1/professor/turmas`, `GET /v1/professor/turmas/{turmaId}/alunos`, `GET /v1/professor/medalhas` e `POST /v1/professor/turmas/{turmaId}/medalhas` com `{alunoId, item}`, sob a policy de professor. Escopo de turma sempre no banco: turma fora dele é 403 `fora-do-escopo`, tipo de recusa `Proibido` |
| Aluno sem conta | Não aparece na lista da turma e não recebe medalha; ex-aluno também não (`produto.md` 4.8). A medalha é sempre de uma conta |
| Entrada do professor | No passo 6, pela lista de alunos da turma, a partir de "Minhas turmas" no início provisório. O perfil do aluno vira outra entrada no passo 7 |
| Ocorrência | Atribuída pelo sistema: a menor vaga livre de 1 ao limite do período, por aluno, item, matéria e instância do período (bimestre ou semestre). O professor não escolhe |
| Chave de deduplicação | Montada dos campos que o item declara no catálogo, com a ocorrência nos repetíveis. Índice único `ix_medalha_chave_ativa` só entre as medalhas não anuladas |
| Concessão simultânea | Quem perde a corrida pela mesma vaga relê e tenta uma vez a próxima vaga livre. Sem vaga, 409 `limite-atingido`, nunca 500 |
| Fora de bimestre | Data do semestre entre dois bimestres, ou fora do semestre da turma: 409 `fora-do-periodo`. A medalha de período semestre (M10, M11) também grava o bimestre da data |
| Outras recusas | 404 `aluno-fora-da-turma` e `item-nao-encontrado`; 409 `ex-aluno`, `item-fora-de-vigencia`, `item-de-outro-curso` e `limite-atingido`. Códigos em `CodigosDaMedalha.cs` |
| Gravação | A medalha, quem concedeu e quando, e os trabalhos `notificar-medalha-recebida` e `recalcular-raridade`, numa transação só |
| Auditoria | Colunas na própria `medalha`: quem concedeu e quando, quem anulou e quando, sempre juntos por constraint. Sem tabela genérica de auditoria |
| Anulação | Por comando administrativo até a área web do passo 12: `Maxx.Api anular-medalha <id> --instituicao <e-mail>`, no compose `docker compose run --rm seeder anular-medalha <id> --instituicao <e-mail>`. Recusa e-mail sem papel de instituição; conta de professor e instituição anula. Sai com 0 (anulada ou já estava), 1 (recusa) ou 2 (uso errado) |
| Anulação e vaga | A anulada sai do índice de vagas e libera a ocorrência para nova concessão. Anular de novo não muda nada |
| Anulação simultânea | `xmin` na medalha: só a primeira grava, e a segunda responde "já estava anulada" |
| Purga | A purga da demonstração apaga `notificacao` e `medalha` do lote antes do usuário |

### Notificação e raridade

| Decisão | Detalhe |
|---|---|
| Notificação | Tabela `notificacao`, com usuário, tipo, referência, criada em e lida em. Uma por usuário, tipo e referência, por índice único. Medalha anulada antes do trabalho, ou apagada pela purga, não notifica. O texto é do cliente, pelo tipo, no passo 10 |
| Bolinha de novidade | A de Troféus chega no passo 10, com o sininho. No passo 6, a notificação só é gravada |
| Raridade | Tabela `raridade_do_item`, uma linha por item do catálogo, com alunos com o item, alunos elegíveis, raridade e quando foi calculada. A leitura só lê |
| Numerador | Alunos distintos com ao menos uma medalha ativa do item, ex-alunos inclusive. Prêmio de participação e conquista entram nos passos 8 e 9 |
| Divisor | Alunos com conta, ativos e ex-alunos, os de demonstração inclusive: o campus inteiro para item aberto, o curso atual para item restrito |
| Troca de curso | Quem trocou de curso leva a medalha de item restrito ao curso antigo e não é elegível no curso novo: o numerador fica limitado ao divisor |
| Faixas | Em inteiros, com "abaixo de" estrito: Lendário abaixo de 1%, Épico abaixo de 5%, Raro abaixo de 20%. Item que ninguém tem é Lendário com 0% |
| Coalescência | Uma linha travada em `recalculo` impede dois cálculos ao mesmo tempo; o recálculo apaga os outros pedidos pendentes, e, se a última execução foi há menos de 10 s, deixa um pedido só para quando a janela fechar (ADR 0005) |
| Quem pede o recálculo | Concessão e anulação de medalha; cadastro de aluno e registro da demonstração, porque mudam o divisor (ADR 0005); e o seed base, quando há item novo, mudado ou ainda sem raridade, sem acrescentar pedido se já houver um pendente |

### Leitura do aluno e tela de Troféus

| Decisão | Detalhe |
|---|---|
| Rota | `GET /v1/eu/premios`, só para aluno: pontos (soma das medalhas ativas), famílias e os itens do catálogo com critério, raridade e o que ele obteve, com tier e pontos do momento. Só o dono vê os faltantes (`produto.md` 9.1) |
| Item restrito | Item restrito a outro curso fica fora da lista, salvo o que o aluno já tem |
| `GET /v1/eu` | Ganhou `nomeDeExibicao`, para as iniciais do avatar na navegação. Só acréscimo |
| Navegação | As cinco abas desde o passo 6: Home é o início provisório, Troféus é a tela de verdade, e Busca, Mural e Perfil mostram "Em breve" até os passos 7 e 10. `/turmas` fica fora das abas, como tela interna com voltar |
| Abas da tela | Troféus (prêmio de participação e conquista) e Medalhas, sublinhadas como na prancheta. "Mostrar não obtidos" vem ligado, para o aluno novo ver o catálogo |
| Trilhas | Só das famílias em que o aluno já tem algum item, com os passos rotulados pelo tier, como na prancheta. A família que mistura medalha e conquista (Nota 10 em prova) aparece nas duas abas |
| Lista | Obtidos primeiro, depois os que faltam, cada grupo do maior valor para o menor. "Tier · Raridade · x% dos alunos"; o critério, ou, na medalha obtida, a matéria e o bimestre da mais recente e quantas vezes. O que falta aparece apagado, com o valor do catálogo sem o "+" |
| Professor na aba Troféus | Vê "Troféus e medalhas são dos alunos.", e a tela não pede os prêmios |
| Modal de concessão | Folha que sobe de baixo, como as do Instagram, com as medalhas do catálogo vigentes: medalhão, nome, tier e pontos, critério e limite. Recusa e falta de conexão ficam dentro da folha |
| Mensagens | Todo código de `Codigos*.cs` da Application tem mensagem em `erros.ts`; o teste lê todos esses arquivos |
| e2e | Cada execução cria uma professora (cadastro e vínculo no Postgres, conta pelo `seed demo`, senha pelo Mailpit) e um aluno matriculado na turma vigente de Banco de Dados; a professora concede pela tela, o aluno vê em Troféus, e o comando de anulação roda no contêiner do seeder. Só no celular. Desde o passo 7, o e2e roda o seed e a anulação com `docker compose exec` no worker, que já está de pé (ver passo 7) |

## Posts, upload e feed (passo 7)

O upload direto ao bucket, o post, o feed, as interações, o perfil e a busca. As decisões
D1 a D14 foram fechadas no plano do passo; as demais, nas etapas. As regras de produto que
nasceram aqui (post de medalha anulada e de prêmio revogado) estão em `produto.md` 4.7 e
4.8.

### Armazenamento e upload

| Decisão | Detalhe |
|---|---|
| Emulador | VersityGW `v1.10.0`, compatível com S3, no perfil `armazenamento-local` do compose (ligado por `COMPOSE_PROFILES` no `.env.example`), com bucket e política de leitura criados por um contêiner de inicialização com o `aws-cli`. O mesmo emulador nos testes de integração, por Testcontainers. Só desenvolvimento e teste, nunca produção. A MinIO, primeira escolha, deixou de publicar a imagem da edição comunitária |
| Adaptador | `IArmazenamentoDeFotos` na Application, implementado com AWSSDK.S3 na Infrastructure: assinar o PUT, conferir com HEAD, remover e montar a URL de leitura. Serve ao R2 e ao emulador. Região `auto`, como no R2 |
| Dois endereços | `Armazenamento__Endpoint`, por onde a API faz HEAD e remoção, e `Armazenamento__EndpointPublico`, que vai na URL assinada. Vazio, o público é o interno, como no R2. No compose diferem: a API alcança o emulador pela rede interna, e o navegador, pela porta publicada |
| Configuração | `MAXX_ARMAZENAMENTO_*` no `.env`, com os valores do emulador no `.env.example` e os do R2 só no `.env` da VPS. Sem bucket, endpoint, leitura, chave ou segredo, a API não sobe fora de desenvolvimento e teste |
| Assinatura | PUT pré-assinado com `content-type` e `content-length` entre os cabeçalhos assinados, válido por 15 minutos. Outro tipo, outro tamanho ou fora da validade: o próprio bucket recusa (ADR 0002). O R2 não aceita POST com política, e por isso o tamanho vai exato na assinatura |
| Formato (D2) | Só `image/jpeg`, porque o cliente reencoda tudo. Derivada do feed com até 1080 de lado e 1 MiB; da grade, 320 de lado e 200 KiB. Recorte quadrado e central, sem editor (ADR 0003) |
| Chaves | `<ULID>/feed.jpg` e `<ULID>/grade.jpg`, um ULID novo por pedido: aleatório e sem dado de ninguém na chave |
| Pedido de upload | `POST /v1/uploads` com finalidade (`post` ou `fotoDePerfil`) e os dois tamanhos, sob o limite `UploadPorUsuario`. Grava `upload_pendente` (dono, finalidade, chaves, tamanhos, criado em, confirmado em) e devolve as duas URLs |
| Confirmação | O post ou a foto de perfil confirmam o upload, depois de o HEAD achar os dois objetos. Upload de outro usuário ou de outra finalidade recebe 404 `upload-nao-encontrado`; já confirmado, 409 `upload-ja-usado`, também na corrida. Não há prazo próprio na confirmação: até a limpeza de 24 horas, o upload pendente ainda serve |
| Upload abandonado | O trabalho `limpar-upload-abandonado`, adiado 24 horas e enfileirado na mesma transação da assinatura, apaga os objetos e a linha se o upload continuar pendente. Confirmado, não toca. Idempotente, com `xmin` contra a confirmação simultânea |
| Remoção de fotos | Post apagado, foto de perfil trocada ou removida: os objetos saem pelo trabalho `remover-fotos`, enfileirado na mesma transação. Remover chave que já não existe não é erro |
| Verificação do R2 | `VerificacaoDoR2Tests`, pulado por padrão e fora da CI: com `MAXX_VERIFICAR_R2=1` e as `MAXX_ARMAZENAMENTO_*` do bucket real, prova no R2 o que o emulador prova na CI (tipo e tamanho presos, leitura pela CDN sem login, remoção), e o CORS do PUT com `MAXX_VERIFICAR_R2_ORIGEM`. Roda uma vez à mão, quando a conta existir |

### Post e interações

| Decisão | Detalhe |
|---|---|
| Post (D5) | Ao menos um entre medalha, foto e legenda. Legenda de até 1.000 caracteres. Não é editável. Um prêmio rende um post, por índice único `ix_post_medalha_ativa`, e a corrida responde 409, nunca 500. Os passos 8 e 9 acrescentam os outros prêmios com no máximo um por post |
| Quem posta | Só aluno ativo. Ex-aluno, professor e instituição recebem 403. A medalha tem de ser do autor e não estar anulada |
| Remoção pelo autor | Física: curtidas e comentários saem em cascata, as fotos pela fila, e o prêmio fica livre para um post novo |
| Remoção pela instituição (D7) | Lógica, com `removido_em` e `removido_por_id`: o post some do feed, do perfil e do link, e o registro fica. No app, pelo menu "..." do post e do comentário |
| Medalha anulada (D6) | O post perde o vínculo com ela; se ficar sem foto e sem legenda, sai, porque post vazio não existe. A mesma regra vale para o prêmio revogado no passo 8 (`produto.md` 4.7 e 4.8) |
| Curtida | `PUT` e `DELETE`, idempotentes; curtir duas vezes conta uma |
| Comentário | Até 500 caracteres, do mais antigo ao mais novo, 20 por página por cursor. Apaga o autor do comentário e a instituição, esta com registro de quem e quando; o dono do post não apaga comentário alheio |
| Seguir (D9) | Aluno segue aluno, ativo ou ex-aluno. Professor segue só aluno matriculado em turma dele no semestre vigente, conferido no banco, e o vínculo continua depois do semestre. Ninguém segue professor. A conta de professor e instituição segue, curte e comenta como professora. Contadores sem tela de lista |

### Feed

| Decisão | Detalhe |
|---|---|
| Página (D8) | 12 posts, três de seguidos para cada recomendação, nas posições 4, 8 e 12. O trecho que acaba cede a vez ao outro: quem não segue ninguém vê só recomendações, e o feed só vem vazio sem post nenhum. A regra está no domínio (`Feed.Intercalar`) |
| Seguidos | Os do próprio leitor e de quem ele segue, do mais novo ao mais antigo, pelo índice `(autor_id, criado_em desc, id desc)` e cursor `(criado_em, id)` |
| Recomendação (D8, opção A) | Posts de quem o leitor não segue, por `md5(semente \|\| id)`, com o id desempatando e `COLLATE "C"` na ordem e no cursor. Aval explícito: esse trecho varre os candidatos sem índice na ordenação, barato nesta escala; o p95 é medido no passo 13 |
| Semente | Sorteada na primeira página e levada dentro do cursor opaco, com a posição de cada trecho. Rolar mantém a ordem; puxar para atualizar começa sessão nova. Cursor adulterado recebe 400 `cursor-invalido` |
| Por papel | A professora vê os alunos que segue mais recomendações; a instituição, só recomendações. Post removido pela instituição não entra |
| Seed (D13) | Sem posts semeados neste passo: o mundo de demonstração não tem contas de aluno. Fica para o seed completo do passo 12 |

### Perfil e busca

| Decisão | Detalhe |
|---|---|
| Rotas | `GET /v1/perfis/{id}` e `GET /v1/eu/perfil`; a grade em `GET /v1/perfis/{id}/posts`, 18 por página; os prêmios obtidos em `GET /v1/perfis/{id}/premios`. Instituição sem papel de professor não tem perfil social (404) |
| O que o perfil traz | Nome, arroba, papel, curso, semestre, ex-aluno, pontos, contadores, `euSigo` e foto. O RA só no próprio perfil. Faltantes e progresso só para o dono. O professor mostra as turmas vigentes e quantas medalhas concedeu. A posição no ranking chega no passo 11 |
| ETag | Fraca, calculada do conteúdo, com 304 e `Cache-Control: private, no-cache`, porque a resposta depende de quem pede. Poupa banda, não banco: a resposta é montada antes de comparar |
| Editar perfil | `PATCH /v1/eu/perfil` com nome, prefixo, upload de foto ou remoção, tudo ou nada. O prefixo antigo fica reservado ao dono, que pode voltar a ele; o de outra pessoa recebe 409. Trocar e remover a foto no mesmo pedido: 400 `foto-de-perfil-ambigua`. Disponibilidade em `GET /v1/eu/perfil/disponibilidade` |
| Busca (D11) | `GET /v1/busca/alunos`, 20 por página: por trecho do nome sem acento nem maiúsculas, por trecho do prefixo ou pelo arroba inteiro com "@". Ativos e ex-alunos, filtro pelo curso atual e pelo semestre. Nunca professor, nunca pelo RA; `%` e `_` são texto. Menos de 2 caracteres e nenhum filtro: 400 `busca-sem-criterio` |
| Índices da busca | Extensões `unaccent` e `pg_trgm`, uma função imutável `maxx_sem_acento` (o `unaccent` sozinho não entra em índice) e índices GIN de trigramas no nome sem acento e no prefixo |
| Semestres | `GET /v1/cursos` traz a duração em semestres; o filtro vai do 1º ao último semestre do curso, até o 12º de Medicina |

### Cliente

| Decisão | Detalhe |
|---|---|
| Derivadas | `expo-image-picker` e `expo-image-manipulator`, sem ampliar foto menor. Derivada acima do limite ganha uma segunda tentativa mais comprimida; ainda grande, "Essa foto ficou grande demais. Escolha outra." |
| Envio | PUT direto na URL assinada, com o tipo e sem token nem cabeçalho do Maxx. Post que falha depois do envio reaproveita o upload, sem gastar o limite |
| Novo post | `/novo-post`, pelo "+" do cabeçalho do feed: foto opcional, legenda com contador e a medalha opcional entre as que ainda não têm post. Professor vê "Só aluno publica no feed." |
| Cartão de post | Como a prancheta, sem o ícone de salvar (não existe na fase 1). O nome no cartão é o prefixo do arroba, sem "@" e sem o sufixo do curso. Curtida otimista; "Enviar" abre a folha de compartilhamento ou copia o link |
| Deep link | `/post/:id` e `/post/:id/comentarios` atrás do login: o grupo `(app)` manda ao `/login?voltar=`, e o grupo público devolve só para esses dois caminhos, nunca para esquema, host ou outra tela. Na web, o roteador tira o `?voltar=` da URL ao entrar, antes do redirecionamento; por isso o destino é guardado enquanto não há sessão |
| Pilha sobre as abas | O grupo `(app)` é um `Stack` sem cabeçalho, não um `Slot`: post, comentários, perfil de outra pessoa, novo post e editar perfil ficam por cima das abas, que continuam montadas. O voltar volta para a aba de onde se veio, com a busca digitada. Publicar e post removido voltam ao feed com `dismissTo`, sem empilhar outro |
| Perfil (D10) | Sem bio, sem "Seguida por" e sem Compartilhar. Arroba sob o nome; anel dourado no próprio avatar; abas por ícone (grade, taça, fita) com a grade de três colunas. Post sem foto aparece na grade como medalhão sobre o metal, ou o começo da legenda sobre `surface`. O perfil de outra pessoa abre fora das abas, com voltar |
| Menu do perfil | Tema, "Minhas turmas" para o professor e Sair. Fecha a pendência do tema do passo 4 |
| Busca | Chips de curso e de semestre; os semestres seguem a duração do curso escolhido e, sem curso, vão até a maior. Os atalhos de ranking chegam no passo 11 |
| Editar perfil | Foto (trocar e remover), nome e prefixo com a disponibilidade consultada 500 ms depois de a digitação parar; o prefixo atual não é consultado. Salvar fica desativado sem mudança |
| Foto na demonstração (D12) | Opcional, depois de a conta existir, porque o upload exige login. Melhor esforço: se a foto falhar, a conta segue sem ela |

### Testes de ponta a ponta

| Decisão | Detalhe |
|---|---|
| Fluxos | Só no celular, pelo limite de login por IP: post com foto desenhada no navegador e enviada de verdade ao emulador, deep link depois do login, curtida, comentário e remoção; busca, seguir e editar perfil |
| Comandos no worker | O seed e a anulação do e2e rodam com `docker compose exec -T worker`, no contêiner que já está de pé. O `docker compose run seeder` refazia também a dependência (o migrator) a cada chamada: cerca de 4,6 s contra 1,9 s com a pilha parada, e bem mais com os testes em paralelo, o que estourava os 30 s do caso de medalha. Com isso saiu o `test.slow()` |

### Limites conhecidos

| Limite | Onde é tratado |
|---|---|
| O teste do índice do feed desliga a varredura (`enable_seqscan = off`): prova que o índice serve, não que o planejador o escolhe com pouco dado | k6 do passo 13 |
| A busca não tem teste com `EXPLAIN` dos índices de trigramas | k6 do passo 13 |
| Seguir no meio da sessão pode repetir um post já visto como recomendação, e deixar de seguir pode tirar um da sessão. O feed não guarda estado no servidor; puxar para atualizar resolve | Aceito |
| A migration cria extensões: o usuário do banco precisa de `CREATE EXTENSION` | Passo 13, revisar na VPS |
| O PUT de Blob no Android e no iOS só passa pelo Jest | Maestro do passo 13 |
| HEIC não abre no Chrome: quem escolher um vê "Não deu para abrir essa foto. Escolha outra." | Aceito |
| A conferência do `content-length` assinado foi provada no emulador, não no R2 | `VerificacaoDoR2Tests`, quando a conta existir |
| As fotos do lote de demonstração não saem pelo SQL da purga (D14) | Purga pela área web, passo 12 |

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
| Etapa pronta | Só depois de a CI do push dela passar, com o resultado no relatório da etapa. Falha depois do push vira commit `fix` separado, sem amend nem force push |
