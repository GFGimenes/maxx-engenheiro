# Maxx

Aplicativo da UniMax (rede UniEduk, Indaiatuba/SP): rede social fechada e gamificada para
alunos, professores e a instituição. Fase 1 é o TCC, com apresentação a uma banca
rotativa.

## Leia antes de agir

| Preciso de | Arquivo |
|---|---|
| Regra de produto | `docs/produto.md` |
| Decisão técnica e por quê | `docs/decisoes/README.md` e os oito ADRs |
| Como as conquistas são avaliadas | `docs/regras-conquista.md` |
| O que vem agora | `docs/plano-mestre.md` |
| Prêmios, valores, períodos, trilhas | `data/catalogo/` |
| Cor, tipografia, componente, espaçamento | `design/tokens.json` e `design/identidade-visual.md` |
| Como uma tela é | `design/telas/`, só no passo daquela tela |

Decisão que não está nesses arquivos não foi tomada. Pergunte, não assuma.

## Protocolo de trabalho

1. Antes de cada passo, um **plano de etapas** com entrega e critério de aceite por etapa.
2. O autor revisa, ajusta, e só então a primeira etapa começa.
3. **Nenhum código antes do plano de etapas aprovado.**
4. Cada etapa termina em revisão humana do autor antes do commit.
5. Um commit por etapa. Um PR por passo.

## Definição de pronto

- Testes passando nas camadas que o passo tocou.
- Lint e formatação limpos.
- Migration criada e aplicada.
- OpenAPI regenerado e cliente TypeScript sem diferença pendente.
- p95 medido, quando houver decisão de cache.
- Revisão humana do autor.

## Stack

| Camada | Escolha |
|---|---|
| API | ASP.NET Core em .NET 10 LTS, EF Core, OpenAPI nativo |
| Banco | PostgreSQL |
| Cliente | Expo com React Native e TypeScript, Expo Router, um código para web, Android e iOS |
| Contrato | `openapi-typescript` e `openapi-fetch`, hooks TanStack Query escritos à mão |
| Fotos | Cloudflare R2 com CDN; a API só assina URL, nunca recebe bytes |
| Infra | Docker Compose em VPS, Caddy servindo o web em `/` e proxy de `/api` |
| Assíncrono | Fila em tabela do Postgres com `FOR UPDATE SKIP LOCKED`, sem broker e sem Redis |
| CI | GitHub Actions, um job por pasta |

## Mapa do repositório

```
apps/api/            Maxx.Domain, Maxx.Application, Maxx.Infrastructure, Maxx.Api
apps/mobile/         Expo, Expo Router
packages/api-client/ gerado do OpenAPI, nunca editado à mão
data/catalogo/       os 46 itens e as famílias de trilha
design/              tokens, identidade visual, telas, logos
docs/                produto, decisões, regras de conquista, plano mestre, fluxos
```

## Camadas e dependências

`Api` conhece `Application`. `Application` conhece `Domain`. `Infrastructure` implementa o
que `Application` e `Domain` declaram. **`Domain` não conhece ninguém.** Dentro de
`Application`, fatias verticais por recurso.

Regra de produto mora no `Domain`. Validação de entrada na borda. Um formato só de erro.
API em `/v1`, mudanças só aditivas, porque o app instalado atrasa em relação à API.

## Idioma do código

Domínio em **português**, técnico em **inglês**: `PremioRepository`, `ConcederMedalhaHandler`,
`ContestacaoOptions`.

**Identificadores sem acento e sem cedilha** (`Premio`, `Materia`, `Contestacao`,
`Comprovacao`, `Notificacao`). Texto para o usuário com acentuação correta.

Tabelas e colunas em `snake_case` no Postgres, mapeadas a partir do PascalCase do C#.

## Glossário: use exatamente estas palavras

| Termo | Significado |
|---|---|
| `Medalha` | Concessão humana do professor, sobre fato de nota ou presença. Sempre Bronze, salvo as duas de fechamento de matéria |
| `PremioParticipacao` | Reivindicado pelo aluno com foto, ou concedido pela instituição. Único contestável e revogável |
| `Conquista` | Automática, por acúmulo dentro do período. Nunca conta outra conquista |
| Troféu | Rótulo **só de interface**, cobrindo prêmio de participação e conquista. Não existe no modelo |
| `Tier` | Bronze 1 a 30, Prata 31 a 70, Ouro 71 a 100, Diamante 101 a 150 |
| Raridade | Derivada da escassez, calculada pelo worker. Não é campo de catálogo |
| `Familia` | Trilha do design: itens em ordem, com o próximo em cadeado |
| `Comprovacao` | A foto que sustenta um prêmio de participação |
| `Contestacao` | Questionamento com motivo, dentro da janela de 15 dias |
| Averiguação | Estado do prêmio contestado: não pontua |
| Definitivo | Prêmio fora da janela: pontua e passa a contar para conquista |
| Anulação | Ato administrativo que remove uma medalha concedida por erro |
| `Turma` | Matéria mais semestre letivo |
| `Matricula` | Vínculo aluno e turma. Única fonte de "quantas matérias o aluno tem" |
| `CadastroElegivel` | Base com RA, e-mail e curso que a faculdade abastece. Estar nela não é ter conta |
| Semestre letivo | 2026/2, corta o ranking. Diferente do semestre do aluno, que é só perfil |

Não invente sinônimo: nada de `Award`, `Prize`, `Badge`, `Reward`, `Trophy` no código.

## Segurança: regras invioláveis

- Segredo só em variável de ambiente. `.env` fora do git, só `.env.example`.
- Senha só com Argon2id. Nunca em texto, nunca em log.
- A API assina URL de upload apenas depois de validar tipo e tamanho, e a assinatura prende
  o `content-type`. **Foto nunca passa pela API.**
- Chave de objeto sempre aleatória, porque a leitura é pública para quem tem o link.
- Refresh rotativo por família: reuso de token já rotacionado derruba a sessão inteira.
- Escopo de turma do professor é checado no banco, nunca lido do token.
- Flag da demonstração desligada por padrão e ausente da configuração de produção.
  Desligada, as rotas de demonstração respondem 404.
- Toda concessão, aprovação, reprovação, contestação, decisão, revogação e anulação grava
  quem fez e quando.
- Nada sensível no bundle web nem no binário nativo.
- Migration em contêiner separado com advisory lock, nunca no startup da aplicação.

## Testes

| Camada | Como |
|---|---|
| Domínio | xUnit, unitário, sem banco. TDD onde couber |
| Integração | xUnit com Testcontainers e Postgres real |
| Cliente | Jest com preset `jest-expo` e Testing Library |
| e2e | Playwright contra o build web; Maestro mínimo no nativo, no passo 13 |
| Carga | k6 versionado, 2.000 usuários virtuais, rodado nos passos de endurecimento |

## Git

Branch: `<tipo>/<assunto-em-kebab>`, tipos `feat`, `fix`, `docs`, `chore`, `refactor`,
`test`, `ci`. As branches saem da rodada atual e voltam para ela por PR; a rodada vai para
a `main` pelo autor.

Commit convencional: `<tipo>(<escopo>): <assunto>`, escopos `api`, `mobile`, `client`,
`seed`, `infra`, `docs`, `design`. Assunto no imperativo, minúsculo, sem ponto final.

**Sem rodapé de co-autoria neste repositório.** Nada de `Co-Authored-By:` nem de menção a
ferramenta na mensagem de commit ou na descrição de PR. O autor do projeto é o autor dos
commits, e revisa cada um antes de ele existir.

PR pequeno e de assunto único.

## Interface

- Claro e escuro, sempre os dois.
- Na dúvida entre um componente genérico e o equivalente do Instagram, use o do Instagram.
- Sem sombra, exceto o controle segmentado no claro. Sem gradiente, exceto o metal dos
  prêmios.
- Nunca escrever texto em ouro sobre fundo claro. Nunca pintar aluno com cor de tier.
- Sem emoji na interface. Português do Brasil, frases curtas.
- Todo erro em tela vira mensagem amigável. O usuário nunca deve sentir que o app travou.
- Superfície sem prancheta desenhada segue a identidade visual: entrada da demonstração,
  aceite de termos, busca, editar perfil, perfil do professor, turmas do professor, modal
  de medalha, área web da instituição.

## Texto em docs

Sem travessões. Vírgula, dois pontos ou parênteses.
