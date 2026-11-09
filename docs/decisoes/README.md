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
DELETE FROM leitura_de_aviso WHERE usuario_id IN (SELECT usuario_id FROM alvo);
DELETE FROM curtida WHERE usuario_id IN (SELECT usuario_id FROM alvo);
DELETE FROM comentario WHERE autor_id IN (SELECT usuario_id FROM alvo);
DELETE FROM seguimento WHERE seguidor_id IN (SELECT usuario_id FROM alvo) OR seguido_id IN (SELECT usuario_id FROM alvo);
DELETE FROM comprovacao WHERE aluno_id IN (SELECT usuario_id FROM alvo);
DELETE FROM post WHERE autor_id IN (SELECT usuario_id FROM alvo);
DELETE FROM progresso_de_conquista WHERE aluno_id IN (SELECT usuario_id FROM alvo);
DELETE FROM conquista WHERE aluno_id IN (SELECT usuario_id FROM alvo);
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
termo e os refresh saem em cascata com o usuário e a família, e as evidências, com a
conquista. Curtidas e comentários de outras pessoas nos posts do lote saem em cascata com o
post. A lista cresce com os passos que gravam dados do aluno (prêmios, pontos): cada um
acrescenta aqui a tabela dele, antes de `usuario`. As fotos no bucket não saem por este SQL,
que só alcança o banco: saem na purga pela área web da instituição, no passo 12.

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
| Numerador | Alunos distintos com ao menos uma medalha ativa do item, ex-alunos inclusive. Desde o passo 8, também o prêmio de participação válido ou definitivo. Desde o passo 9, também a conquista não removida, uma vez por aluno |
| Divisor | Alunos com conta, ativos e ex-alunos, os de demonstração inclusive: o campus inteiro para item aberto, o curso atual para item restrito |
| Troca de curso | Quem trocou de curso leva a medalha de item restrito ao curso antigo e não é elegível no curso novo: o numerador fica limitado ao divisor |
| Faixas | Em inteiros, com "abaixo de" estrito: Lendário abaixo de 1%, Épico abaixo de 5%, Raro abaixo de 20%. Item que ninguém tem é Lendário com 0% |
| Coalescência | Uma linha travada em `recalculo` impede dois cálculos ao mesmo tempo; o recálculo apaga os outros pedidos pendentes, e, se a última execução foi há menos de 10 s, deixa um pedido só para quando a janela fechar (ADR 0005) |
| Quem pede o recálculo | Concessão e anulação de medalha; cadastro de aluno e registro da demonstração, porque mudam o divisor (ADR 0005); e o seed base, quando há item novo, mudado ou ainda sem raridade, sem acrescentar pedido se já houver um pendente. Desde o passo 8, também a aprovação de comprovação, a contestação, a decisão da instituição e a remoção do post pela instituição quando revoga |

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
| Post (D5) | Ao menos um entre medalha, foto e legenda. Legenda de até 1.000 caracteres. Não é editável. Um prêmio rende um post, por índice único `ix_post_medalha_ativa`, e a corrida responde 409, nunca 500. O passo 8 acrescentou o prêmio de participação (`ix_post_premio_participacao_ativo`, e `ck_post_um_premio` contra os dois no mesmo post). O passo 9 acrescentou a conquista (`ix_post_conquista_ativa`, e `ck_post_um_premio` passou a aceitar no máximo um dos três) |
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

## Prêmios de participação (passo 8)

Eventos, reivindicação, fila de comprovações, contestação e decisão da instituição. As
decisões D1 a D13 foram fechadas no plano do passo; as demais, nas etapas. As regras de
produto que nasceram aqui estão em `produto.md` 4.6 (datas do evento, itens do evento
semeado, o revogado segurando a chave), 4.7 (post de reivindicação preso enquanto o prêmio
pode ser decidido, remoção pela instituição, averiguação para os outros leitores) e 8
(prêmio revogado sem contestação).

### Eventos

| Decisão | Detalhe |
|---|---|
| Evento (D1) | Semeado, com os itens do catálogo que aceita e, se for o caso, o curso a que se restringe; ou digitado pelo aluno na reivindicação, com nome (3 a 120 caracteres) e período, aceitando qualquer item. O item que deduplica por matéria (P17) vai sempre por texto livre, com a matéria: um evento semeado que o aceite faz o seed falhar |
| Seed (D2) | `data/demo/eventos.json`, com 8 eventos de 2026/2 (4 abertos e 1 de cada curso), lido pelo seed demo: só acrescenta, e falha sem gravar nada com evento divergente, item que não é prêmio de participação, P17, curso inexistente ou código repetido. A CI confere o arquivo contra o catálogo sem banco, no passo "Catálogo e eventos válidos". O resto chega com o seed completo do passo 12 |
| Datas (D3) | O evento já começou (início até hoje, em São Paulo) e começou depois do fim do último semestre letivo encerrado. O limite é o fim do semestre anterior, e não o início do vigente, para o evento do recesso continuar reivindicável e a reivindicação não parar no recesso. O fim pode estar adiante |
| Semestre do evento (D4) | O prêmio guarda só as datas do evento. Em que semestre ele conta é regra do passo 11 |
| Lista | `GET /v1/eventos?item=`, só aluno: os semeados abertos ao curso dele, reivindicáveis hoje e que aceitam o item, do mais recente ao mais antigo. Item que não é prêmio de participação: 404 `item-nao-encontrado` |
| Matérias do aluno | `GET /v1/eu/materias`, só aluno: as matérias das turmas dele no semestre de hoje, para a apresentação em sala. Pela D3, a data do evento é deste semestre. A matéria vale se o aluno estava matriculado numa turma dela no semestre que contém a data do evento; P17 com data no recesso não acha matéria |

### Reivindicação

| Decisão | Detalhe |
|---|---|
| Modelo | `PremioParticipacao` (estado, tier, pontos e versão do item congelados, evento, matéria, chave de deduplicação, aprovação, reprovação com motivo, contestação, revogação com motivo, definitivo) e `Comprovacao` (prêmio, aluno, evento e post), filtrável por evento pensando no lote da fase 2. A concessão direta da instituição, do passo 12, não terá comprovação |
| Rota | `POST /v1/reivindicacoes`, só aluno: item, o id do evento semeado ou o evento digitado (nunca os dois), a matéria no P17, o upload e a legenda. Grava evento digitado, prêmio, post e comprovação numa transação só, depois do HEAD das duas derivadas, e responde 201 com o post. Nada vai para a fila |
| Deduplicação (D5) | Chave montada do catálogo: aluno e evento; com evento digitado, aluno, item e data de início; no P17, também a matéria. Índice único `ix_premio_participacao_chave_ativa` entre os que não foram reprovados: o reprovado libera a nova tentativa, o revogado bloqueia o mesmo evento. A corrida responde 409 `premio-ja-reivindicado` |
| Post de reivindicação | A foto é obrigatória (`foto-obrigatoria`). O post ganhou `premio_participacao_id`, com índice único e `ck_post_um_premio`. O prêmio do post traz, só como acréscimo, `estado` e `evento`; "reivindicado" é a marca de pendente |
| Post preso | Enquanto o prêmio está reivindicado, válido ou em averiguação, o autor não apaga o post (409 `comprovacao-em-andamento`): a foto sairia do bucket, e a aprovação e a contestação dependem dela. Definitivo, o post é como outro qualquer |

### Fila de comprovações e decisão

| Decisão | Detalhe |
|---|---|
| Quem decide | Policy `aprovador` (professor ou instituição) na borda; o papel de instituição e o escopo do professor lidos do banco. O professor decide só a comprovação de aluno matriculado em turma sua no semestre vigente (`fora-do-escopo`, 403) |
| Fila | `GET /v1/comprovacoes?eventoId=&cursor=`: as pendentes, das mais antigas para as mais novas, 20 por página por cursor `(criada_em, id)`, com aluno, item, evento (semeado ou digitado), foto, legenda e as matérias em comum (D13: para o professor, só as que ele também leciona). `GET /v1/comprovacoes/eventos` traz os eventos com pendentes e quantas, para os chips |
| Marca de 7 dias (D11) | Mais de 7 vezes 24 horas desde a reivindicação, na fila de comprovações, e desde a contestação, na fila de contestações. Sem decisão automática por decurso |
| Corrida | Vale quem chega primeiro: `xmin` no prêmio, e quem perde recebe 409 `comprovacao-ja-decidida` |
| Aprovação | `POST /v1/comprovacoes/{id}/aprovacao`: o prêmio fica válido e pontua na hora. Na mesma transação, a notificação, o recálculo de raridade e o trabalho `tornar-premio-definitivo`, adiado para o fim da janela de 15 dias. Aluno que virou ex-aluno não tem a comprovação aprovada (409 `ex-aluno`) |
| Reprovação | `POST /v1/comprovacoes/{id}/reprovacao`, com motivo de 1 a 500 caracteres (D10, `motivo-invalido`): o post perde o vínculo e fica comum, e a notificação vai para a fila |
| Definitivo | O handler do fim da janela entrou já na etapa 3 (a fila conta como falha o tipo sem handler): torna definitivo só o prêmio ainda válido, e o trabalho que chega antes da hora volta para a fila no instante certo |
| Pontos | Perfil e Troféus somam medalhas ativas e prêmios de participação válidos e definitivos; o item obtido traz a data da aprovação e o post |
| Raridade (D6) | O numerador passou a contar prêmio de participação válido ou definitivo. Em averiguação não conta: os outros não o veem |

### Contestação e decisão da instituição

| Decisão | Detalhe |
|---|---|
| Contestação | `POST /v1/premios-participacao/{id}/contestacao`, professor da turma (inclusive quem aprovou) ou instituição, com motivo, dentro da janela. Uma por prêmio, por índice único, também depois de confirmado (`premio-ja-contestado`). Não aprovado: `premio-nao-contestavel`; janela fechada, definitivo ou revogado: `fora-da-janela-de-contestacao`. O prêmio vai a averiguação, para de pontuar, e notificação e raridade vão para a fila |
| Averiguação para os outros | O post continua no ar, mas sem o prêmio para quem não é o aluno nem a instituição: sem pontos, sem tier e sem a marca. Mostrar o nome do item sem tier e pontos tornaria opcionais campos hoje obrigatórios da resposta, o que não é aditivo |
| Fila de contestações | `GET /v1/contestacoes`, só instituição: as abertas, das mais antigas para as mais novas, com motivo, quem contestou, a foto e a marca de 7 dias |
| Decisão | `POST /v1/contestacoes/{id}/confirmacao` (definitivo na hora, janela encerrada) e `.../revogacao` com motivo (sai da pontuação, fica no histórico, o post perde o vínculo). `xmin` na contestação: `contestacao-ja-decidida`. O trabalho do fim da janela, ainda agendado, não muda nada depois |
| Remoção pela instituição | Remover o post de reivindicação resolve o prêmio no mesmo ato, com o motivo "Removido pela instituição.": pendente, reprova; válido, revoga; em averiguação, decide a contestação revogando. Só então o post sai, e a foto pela fila. A instituição não depende da fila para tirar do ar uma foto imprópria |
| Notificações | `comprovacaoAprovada`, `comprovacaoReprovada`, `contestacaoAberta`, `decisaoDeContestacao` e `premioRevogado` (o válido revogado pela remoção, que não tem contestação), uma por prêmio e tipo. O texto é do cliente, no passo 10 |
| Histórico do aluno (D9) | `GET /v1/eu/reivindicacoes`, só aluno: pendente, em averiguação, reprovada e revogada, com o motivo nas duas últimas, pelo instante em que entrou no estado |
| Telas da instituição (D7) | A API da instituição está completa e testada; as telas chegam no passo 12. O e2e decide pela API |
| Purga | O SQL apaga `comprovacao`, `contestacao`, `premio_participacao` e o evento digitado por quem é do lote, antes do usuário |

### Cliente

| Decisão | Detalhe |
|---|---|
| Novo post | Controle segmentado "Post" e "Reivindicar prêmio". Na reivindicação, a foto é obrigatória, o prêmio vem do catálogo de participação (a lista recolhe para o escolhido, com "Trocar prêmio"), o evento vem da lista ou de "Outro evento" (nome e datas em dd/mm/aaaa), e a matéria só no item que a pede (`exigeMateria` em `GET /v1/eu/premios`, para o app não cravar "P17") |
| Marcas no post | Pendente: selo do metal, chip "Pendente" e "Aguardando aprovação" sob os pontos. Em averiguação, para o aluno: só o chip, sem metal, tier e pontos. Chip neutro em `surface2`, raio 8, na altura do selo |
| Troféus | Seção "Reivindicações" na aba Troféus, só quando há alguma, com o medalhão apagado, o estado e o motivo |
| Comprovações (D8) | `/comprovacoes`, pelo menu do perfil do professor, ao lado de "Minhas turmas": chips de evento, cartões como os do feed, "Aprovar" e "Reprovar". A marca de 7 dias é um chip em `highlight` com texto `ink`, sem ouro sobre claro |
| Folha de motivo | Uma só, para reprovar e contestar, que sobe de baixo: motivo de 1 a 500 com contador, recusa dentro da folha |
| Contestar | "Contestar prêmio" no "..." do post, para o professor, no prêmio de participação válido. Escopo e janela, a API confere |

### Testes de ponta a ponta

| Decisão | Detalhe |
|---|---|
| Fluxos | Só no celular, claro e escuro, cada execução com os seus alunos, professora e instituição: reivindicação pela tela com foto, aprovação pela fila e os pontos em Troféus; reprovação pela folha, nova tentativa com outra foto, aprovação e contestação pela tela da professora, revogação pela API da instituição e o histórico do aluno |
| Instituição própria | Uma conta de instituição por teste, como a professora: a recuperação de senha aceita 3 pedidos a cada 15 minutos por e-mail, e a CI roda dois temas e repete o que falha |

### Limites conhecidos

| Limite | Onde é tratado |
|---|---|
| O ranking não é recalculado pela aprovação, contestação, decisão e remoção: a tabela de ranking não existe ainda | Passo 11 |
| Prêmio definitivo não reavalia conquistas | Resolvido no passo 9: o fim da janela e a confirmação da contestação enfileiram a reavaliação |
| A conta de instituição não tem telas para a fila de contestações nem para a concessão direta | Passo 12 |
| A remoção pela instituição que corre contra outro ato sobre o mesmo prêmio responde 404, como a corrida de duas remoções | Aceito |
| P17 com data no recesso não acha matéria | Aceito |
| O e2e depende do evento semeado da semana acadêmica de CC no semestre vigente | Risco "Semestre vigente na avaliação" do plano mestre |

## Conquistas (passo 9)

O avaliador das regras do catálogo, os gatilhos por fato e por fechamento de período, o
recálculo na anulação, a leitura em Troféus e o post de conquista. As decisões D1 a D10
foram fechadas no plano do passo, com as quatro perguntas dele; as demais, nas etapas. As
regras de produto que nasceram aqui estão em `produto.md` 4.9 (semestre do prêmio de
participação, todas as matérias, conquista removida e o post dela, progresso em Troféus) e
8 (notificações de conquista). A especificação do avaliador foi atualizada em
`regras-conquista.md`.

### Avaliador e modelo

| Decisão | Detalhe |
|---|---|
| Avaliador | `AvaliadorDeConquista`, no domínio: recebe a regra, a instância e os fatos, devolve contagem, alvo, se satisfaz e as evidências. Não grava nem sabe quando roda. O escape `customizada` delega a uma `IRegraCustomizada` registrada pelo nome; sem registro, erro |
| Fato contável | `FatoContavel`: só a medalha ativa (`DaMedalha`) e o prêmio de participação definitivo (`DoPremio`) viram fato. `DescreverMedalha` descreve a anulada só para localizar as instâncias que ela tocava, nunca para contar |
| Instância | `InstanciaDePeriodo`: tipo e id do bimestre, do semestre, do ano letivo ou do próprio aluno (período curso) |
| Modelo (D1) | `conquista` (aluno, item, versão, tier e pontos congelados, tipo e referência do período, concedida em, removida em) e `evidencia_da_conquista` (medalha ou prêmio, nunca os dois, por check) |
| Unicidade (D2) | Índice parcial `ix_conquista_instancia_ativa` entre as não removidas. Dentro do worker, uma violação aborta a transação do trabalho, então quem evita o disparo duplo é uma trava consultiva por aluno (`pg_advisory_xact_lock`), tomada no início de cada avaliação e solta no commit. Prova reversa: sem a trava, o teste de três consumidores simultâneos falha nas três rodadas |
| Remoção (D3) | Lógica, com `removida_em`: a linha fica no histórico e sai do índice; se o critério volta, nasce outra |
| Evidências (D4) | Todos os fatos contáveis da instância na concessão; no recálculo, se ainda satisfeita, o conjunto atual |
| Progresso (D5) | `progresso_de_conquista` (aluno, item, instância, contagem, alvo), escrito pelo worker para toda conquista que um fato toca, inclusive as de fechamento. A leitura só lê |
| Alvo dinâmico | Matérias distintas das matrículas do período; alvo zero nunca satisfaz (pergunta 3) |
| Semestre do prêmio | O semestre que contém o início do evento; no recesso, o seguinte (pergunta 1). Sem o semestre seguinte no calendário, o prêmio ainda não é fato. Conquista de bimestre só conta medalha, e o validador do catálogo recusa outra fonte |
| Vigência e curso | O item restrito a outro curso, ou fora da vigência na data de referência (o fim da instância; hoje, no período curso), não se aplica: não concede nem grava progresso |
| Teste por item | Em `Maxx.Api.IntegrationTests`, sem banco: o leitor do JSON é da Infrastructure, que o domínio não conhece |

### Gatilhos e worker

| Decisão | Detalhe |
|---|---|
| Por fato (D6) | `reavaliar-conquistas`, com o aluno e o id da medalha ou do prêmio, na mesma transação do fato: concessão de medalha, fim da janela que torna o prêmio definitivo e confirmação da contestação. Avalia só as conquistas que o fato pode mexer, na instância dele |
| Fechamento (D7) | `verificar-fechamentos` se reenfileira de hora em hora, com trava própria, e apaga as outras pendentes. Fecha cada instância encerrada (meia-noite de São Paulo do dia seguinte ao fim) sem registro em `fechamento_de_periodo`: grava o registro e enfileira `fechar-periodo` juntos. Este abre um `avaliar-fechamento-do-aluno` por aluno matriculado no período, ex-alunos inclusive, para não passar dos 60 s por trabalho. O seed base garante uma verificação pendente. Na primeira execução no calendário real, fechou os bimestres 1 a 3 e o semestre 2026/1 |
| Carga com período | O tipo de período vai pelo nome do enum ("Bimestre"), não pelo número |
| Congelamento (D8) | Com a instância fechada, o fato que chega concede a conquista de fechamento até a véspera do congelamento: o fim do último semestre coberto, mais 30 dias, já é congelado. Constante `InstanciaDePeriodo.DiasAteOCongelamento`, que o passo 11 herda. O primeiro fechamento roda mesmo depois do congelamento |
| Anulação (D9) | `recalcular-conquistas-da-medalha`, sem limite de data: refaz o progresso das instâncias que a medalha tocava e reavalia só as conquistas com ela nas evidências. Nunca concede. Mudança de catálogo não remove conquista |
| Notificação e pontos (D10) | `conquistaObtida` e `conquistaRemovida`, gravadas pelo próprio handler, na transação do trabalho; o texto chega no passo 10. Perfil e Troféus somam a conquista não removida; a raridade a conta uma vez por aluno, e conceder ou remover pede o recálculo |
| Serviços | `AvaliacaoDeConquistas` (avaliar, gravar progresso e, se permitido, conceder) e `ContextoDasConquistas` servem aos handlers |

### Leitura, tela e post

| Decisão | Detalhe |
|---|---|
| Troféus | `GET /v1/eu/premios`, só acréscimos: `obtidos[].instancia` (`{periodo, ano, semestre, bimestre}`, em números; o texto é do app) e `itens[].progresso` (`{periodo, contagem, alvo, unidade}`), com `unidade: "materias"` no alvo dinâmico |
| Progresso | Da instância vigente, só na conquista não obtida nela. Sem linha gravada, zero e o alvo do catálogo ou a contagem de matrículas. Fora: bimestre e semestre no recesso (ano e curso seguem), alvo zero, fora da vigência, regra customizada |
| Perfil público | Mostra a conquista obtida com a instância; nunca o progresso |
| Tela | "Regra automática · {critério}" na que falta, com a linha "2 de 3 neste bimestre" ou "1 de 2 matérias neste bimestre"; "Regra automática · 4º bimestre de 2026 · 2 vezes" na obtida. O critério é a frase inteira do catálogo, sem campo curto |
| Post de conquista | `POST /v1/posts` aceita `conquistaId` (acréscimo). `post.conquista_id`, índice `ix_post_conquista_ativa`, `ck_post_um_premio` com no máximo um dos três. Recusas novas: `conquista-removida` (409) e `mais-de-um-premio` (400). O novo post oferece a conquista obtida sem post, com a taça e a instância |
| Conquista removida e post | Como a medalha anulada: o post perde o vínculo; o que era só a conquista é apagado |
| Purga | O SQL apaga `progresso_de_conquista` e `conquista` depois de `post`, que segura a conquista; as evidências saem em cascata |

### Testes de ponta a ponta

| Decisão | Detalhe |
|---|---|
| Fluxo | Só no celular, claro e escuro, cada execução com a sua professora e o seu aluno: quatro medalhas pela API da professora, "4 de 5 neste bimestre" em Troféus, a quinta fecha a C01 com a instância e os pontos, e o aluno posta a conquista pelo novo post. A tela de concessão já tem o e2e dela (passo 6) |
| Espera | A tela é relida até o que se espera aparecer, por até 30 s: a avaliação é no worker |

### Limites conhecidos

| Limite | Onde é tratado |
|---|---|
| O ranking não é recalculado pela conquista concedida ou removida: a tabela de ranking não existe ainda | Passo 11 |
| O texto das notificações de conquista e a bolinha de Troféus | Resolvido no passo 10: os textos estão em `notificacoes/textos.ts` do app, e a bolinha segue o marcador `trofeus_visto_em` |
| A regra customizada não é disparada por fato nem tem progresso | Aceito: nenhuma conquista da fase 1 usa |
| O fato que fica contável depois do fechamento não se reproduz pelo fluxo com o catálogo da fase 1 (as de fechamento contam medalha, concedida só no bimestre da data); o teste isola o mecanismo | Aceito |
| O post de conquista no feed não mostra a instância, só o nome, o tier e os pontos | Aceito |
| Conquistas no seed de demonstração | Passo 12 |
| A suíte local da API disputa recurso com a pilha do compose de pé (o teste do migrador passa de 1 min); para a suíte local, pilha parada | Ocorrência de ambiente |

## Mural e notificações (passo 10)

O aviso nos três escopos, a leitura do mural, o sininho, a tela de notificações e as
bolinhas das abas. As decisões D1 a D7 foram fechadas no plano do passo, com as nove
perguntas dele; as demais, nas etapas. As regras de produto que nasceram aqui estão em
`produto.md` 7 (título e texto, quem vê o quê, leitura, destinatários da notificação), 8
(sininho também no Mural, leitura ao abrir, destinos, prêmio novo) e 10.1 (tela de
notificações e novo aviso do professor).

### Aviso e publicação

| Decisão | Detalhe |
|---|---|
| Modelo (D1) | `aviso` (autor, escopo, título, texto, as duas chaves da foto, publicado em), com os alvos em `aviso_turma` e `aviso_curso`, um aviso para N alvos. Quem publicou e quando ficam no próprio aviso. Título de 3 a 120 e texto de 1 a 2.000 caracteres, contando um emoji como um, também por check no banco (`char_length`) |
| Rota | `POST /v1/avisos` com `escopo` (`faculdade`, `curso`, `turma`) e `turmaIds` ou `cursoIds`. Lista do escopo errado ou alvo na faculdade: 400 `alvos-do-aviso-invalidos`. Códigos em `CodigosDoMural.cs` |
| Papel e escopo (D3) | O domínio decide o papel: instituição na faculdade e no curso, professor na turma, a conta que acumula os dois nos três; aluno recebe 403 `aviso-nao-permitido`. O papel é conferido antes do escopo. A turma, no banco, contra `vinculo_professor` e o semestre vigente: uma turma de fora recusa o aviso inteiro (403 `fora-do-escopo`). Curso inexistente: 404 `curso-nao-encontrado` |
| Foto (D2) | Finalidade `aviso` no `POST /v1/uploads`, de professor e instituição, com as mesmas derivadas e o mesmo limite do post. Confirmada pelo HEAD das duas na publicação. Sem edição e sem remoção de aviso, a foto só sai pela limpeza do upload abandonado |
| Distribuição (D4) | `notificar-aviso-novo`, enfileirado na transação da publicação. O worker grava todas as notificações num `INSERT ... SELECT ... ON CONFLICT DO NOTHING` sobre o índice único da notificação: idempotente e sem passar dos 60 s num aviso da faculdade |
| Destinatários | Fixados no instante da publicação: conta criada até ele, e aluno ativo nele (quem virou ex-aluno depois ainda recebe). Aluno pelo escopo (todos na faculdade, o curso atual no curso, a matrícula na turma); professor só na faculdade. Nunca o autor nem a instituição; o ex-aluno não é notificado. Prova reversa: sem a condição da conta criada, o teste dos destinatários falha |

### Leitura do mural

| Decisão | Detalhe |
|---|---|
| Visibilidade | Uma regra só (`ConsultaDoMural.Visiveis`), para a lista, o aviso aberto, a leitura e o "Ler todos": a instituição vê tudo; os outros, a faculdade, o que publicaram, o curso atual (do cadastro) e as turmas em que têm matrícula, de qualquer semestre. O professor, sem curso nem matrícula, vê a faculdade e os dele |
| Rotas | `GET /v1/mural?escopo=&cursor=`, 20 por página por cursor `(publicado_em, id)`; `GET /v1/avisos/{id}`; `PUT /v1/avisos/{id}/leitura`; `POST /v1/mural/leitura` ("Ler todos"); `GET /v1/perfis/{id}/avisos`, os do professor pela mesma regra, 404 `usuario-nao-encontrado` para perfil inexistente, como a grade de posts. Aviso fora do mural responde 404 `aviso-nao-encontrado`, como o que não existe |
| Resposta | O autor com `instituicao` (o app mostra "UniMax"; a conta de professor e instituição, publicando para turma, aparece pelo nome), as matérias das turmas ou as siglas dos cursos, a foto e `lido`, deste leitor |
| Leitura | `leitura_de_aviso` (usuário, aviso, lida em), chave `(usuario_id, aviso_id)`, sem cascata do usuário. Gravada com `ON CONFLICT DO NOTHING`: ler de novo não muda o instante. Na mesma transação, a notificação de aviso novo daquele aviso fica lida. Prova reversa: sem o filtro do mural, o "Ler todos" marca avisos de fora e o teste falha |
| ETag | No mural e no aviso aberto, fraca e do conteúdo, como no perfil (`Cache-Control: private, no-cache`); a leitura muda a ETag. O p95 fica para o k6 do passo 13, como o do perfil |
| Purga | O SQL apaga `leitura_de_aviso` do lote antes do usuário; o aviso não é do lote e fica |

### Notificações e novidades

| Decisão | Detalhe |
|---|---|
| Lista | `GET /v1/eu/notificacoes`, só as de quem pede, 20 por página por cursor `(criada_em, id)`. Cada uma com tipo, referência, quando, se foi lida e o que a frase pede: o nome do item, o motivo (reprovação e revogação), a decisão (`confirmada` ou `revogada`), o post do prêmio que ainda está no ar e o título do aviso. Uma consulta por tipo de referência, nunca uma por linha |
| Leitura | `POST /v1/eu/notificacoes/leitura` com `ate`, opcional: marca as criadas até ele, e a que chegou depois de o sininho abrir fica acesa. Sem corpo, todas as de agora. Acréscimo aprovado na etapa 3. Prova reversa: sem o limite, a notificação nova é marcada e o teste falha |
| Novidades (D5) | `GET /v1/eu/novidades` com três booleanos: sininho (notificação não lida), mural (aviso do mural não lido) e troféus (só aluno: medalha ativa, prêmio de participação aprovado e ainda válido ou definitivo, ou conquista não removida, depois do marcador). Um aluno novo vê a bolinha do Mural acesa pelos avisos antigos da faculdade; "Ler todos" a apaga |
| Marcador de Troféus | `usuario.trofeus_visto_em`, por `PUT /v1/eu/trofeus/visto`, que só anda para a frente (`Usuario.MarcarTrofeusVistos`). Separado do sininho: abrir um não lê o outro. O prêmio de participação acende a bolinha pela aprovação; a contestação confirmada não acende de novo |
| Texto | Do app, pelo tipo, em `notificacoes/textos.ts`, com o destino de cada toque. Tipo que o app ainda não conhece vira "Você tem uma novidade.", sem destino. Um teste do app lê o enum `TipoDeNotificacao` do C# e falha se algum tipo ficar sem texto |

### Cliente

| Decisão | Detalhe |
|---|---|
| Aba Mural | Logo, sininho, "Mural" com "Ler todos", chips Todos, Faculdade, Curso e Professores (o escopo turma), e os cartões em `surface` com raio 16. O não lido vem aberto, com a bolinha, o título em negrito e "não lido" no rótulo de acessibilidade; o lido, recolhido, e tocar o abre ali mesmo, sem registrar leitura. "Abrir" leva a `/aviso/:id` |
| "Ler todos" | Segue a bolinha do Mural (`novidades.mural`), que é do mural inteiro, e não a lista do chip; sem as novidades, a lista |
| Aviso aberto | Cabeçalho com voltar, rótulo, data dd/mm em São Paulo (UTC-3 fixo, sem depender do `Intl`), título, autor, a foto quadrada e o texto. Abrir registra a leitura uma vez |
| Novo aviso | `/novo-aviso`, pelo "+" do Mural, só para quem tem papel de professor: as turmas vigentes dele em chips de seleção múltipla, título e texto com contador e a foto opcional, com só "Remover foto" (tocar na foto troca). A conta de professor e instituição publica só para turmas pelo app; faculdade e curso ficam para a área web (passo 12). A foto já enviada é reaproveitada se o aviso falhar, como no post |
| Sininho | No cabeçalho do feed, à direita do "+", e no do Mural, com a bolinha de halo como a das abas e "com novidade" no rótulo. A tela de notificações marca como lido até a mais nova mostrada, e o que era novo segue em destaque enquanto ela está aberta. Tempo relativo como no feed |
| Bolinhas | Das novidades, perguntadas ao voltar ao foco e a cada 60 s; abrir Troféus, como aluno, chama o marcador |
| Ícones | `sino` (bell), `abrir` (chevron-down) e `lerTodos` (check-check) do Lucide. `navegacao/EmBreve.tsx` saiu, sem mais uso |

### Testes de ponta a ponta

| Decisão | Detalhe |
|---|---|
| Fluxos | Só no celular, claro e escuro, cada execução com a sua professora e o seu aluno: a professora publica pela tela para duas turmas dela (a segunda vinculada no Postgres), o aluno vê as bolinhas do Mural e do sininho, abre o aviso, usa "Ler todos" e abre o sininho; uma medalha pela API acende o sininho e Troféus, e o toque na notificação leva a Troféus |
| Isolamento | As turmas vigentes são as mesmas para todas as execuções, e o aviso de uma chega ao aluno da outra. O aluno começa com tudo lido pela API, o aviso dele é conferido pelo título, e as bolinhas são apagadas por "Ler todos" e pelo sininho, que leem tudo |
| Espera | A tela é recarregada até a bolinha aparecer, por até 30 s: a notificação é gravada no worker |

### Limites conhecidos

| Limite | Onde é tratado |
|---|---|
| Os destinatários do aviso de curso são lidos pelo curso atual: quem trocar de curso entre a publicação e o trabalho do worker (segundos) é lido pelo curso novo | Aceito |
| A tela de aviso de faculdade e de curso da instituição | Passo 12, com a API pronta |
| Avisos no seed de demonstração | Passo 12 |
| O p95 do mural com ETag e das novidades perguntadas a cada 60 s por usuário | k6 do passo 13 |
| Sem push: a bolinha só acende quando o app pergunta (foco ou 60 s) | Fase 2, push nativo |
| Na suíte e2e inteira rodada localmente (Windows, pilha no ar há horas, banco com os dados de muitas rodadas), o e2e da medalha estoura os 30 s: a página do aluno não volta a desenhar depois do recarregamento que segue a anulação. Sozinho, passa; na CI, passou de primeira em todas as etapas do passo, entre 12 e 16 s | Ocorrência de ambiente; acompanhar no passo 13 |
| Medalhas concedidas em sequência rápida: a reavaliação da primeira roda com as outras já gravadas e pode conceder a conquista antes de as outras medalhas serem notificadas; no sininho, a conquista obtida aparece entre as medalhas | Aceito: o avaliador conta os fatos do momento |

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
