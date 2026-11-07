# apps/mobile

Cliente Expo do Maxx: um código para web, Android e iOS, com Expo Router (rotas em
`src/app/`).

## Desenvolvimento

```bash
# API local, em outro terminal (porta 5028). A conexão é obrigatória para subir, mas o
# /api/health não toca no banco; o /api/ready precisa do Postgres do compose de pé.
export ConnectionStrings__Maxx="Host=localhost;Port=5432;Database=maxx;Username=maxx;Password=<a do .env>"
dotnet run --project apps/api/src/Maxx.Api

# web
npm run web -w @maxx/mobile
```

Na web, o servidor do Metro repassa `/api/*` para `http://localhost:5028` (ou para
`MAXX_DEV_API_URL`), e a página e a API ficam na mesma origem, sem CORS. **Esse proxy é
só de desenvolvimento** (`metro.config.js`): ele não existe no build exportado
(`npm run export:web`). Em produção, quem serve o build e faz o proxy de `/api` é o Caddy,
no `docker-compose.yml` da raiz.

No nativo, a URL da API vem de `EXPO_PUBLIC_API_URL`.

## Design system

As decisões estão em `docs/decisoes/README.md`, em "Design system (passo 4)".

| Onde              | O quê                                                                                                                        |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| `src/theme/`      | Tokens tipados sobre `design/tokens.json`, tema com `useTheme()`, fontes e escala em `textStyles`                            |
| `src/components/` | `Button`, `TextField`, `SegmentedControl`, `FilterChip`, `BottomNav`, `Avatar`, `EmptyState`, `ErrorState`, `Icon`, `Screen` |
| `src/premios/`    | `Medalhao`, `SeloPremio`, `FaixaPremio`, `TrilhaFamilia`                                                                     |
| `src/vitrine/`    | A bancada dos componentes, em `/vitrine`, só em desenvolvimento                                                              |
| `assets/`         | Fontes, licenças, ícone, adaptativo, splash e favicon                                                                        |

Cor só pelo token: o ESLint recusa cor literal em `src/`. Gradiente só em `Medalhao` e
`SeloPremio`, e sombra só em `SegmentedControl`, conferidos por teste.

Para ver os componentes, com o app em desenvolvimento (`npm run web -w @maxx/mobile`), abra
`/vitrine`. No build de produção, a rota volta para o início.

Os arquivos gerados têm script próprio, rodado da raiz. As fontes precisam de Docker; os
ícones, do `npm ci`:

```bash
bash design/fontes/gerar.sh
node design/icones/gerar.js
```

## Autenticação (passo 5)

As decisões estão em `docs/decisoes/README.md`, em "Autenticação no cliente (passo 5)".

| Onde                                                                          | O quê                                                                                                     |
| ----------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| `src/app/(publico)/`                                                          | Login, cadastro em três passos, esqueci-senha e demonstração: só para quem não entrou                     |
| `src/app/(app)/`                                                              | O que exige sessão: as cinco abas em `(abas)/` e as telas internas, como `/turmas` (passo 6)              |
| `src/app/status.tsx`, `src/app/redefinir-senha.tsx`                           | Fora dos grupos, com ou sem sessão                                                                        |
| `src/sessao/`                                                                 | `GerenciadorDeSessao` (access em memória, refresh serializado, fila entre abas na web) e `SessaoProvider` |
| `src/api/erros.ts`, `src/api/FalhaDaApi.ts`                                   | Código da API para mensagem amigável                                                                      |
| `src/autenticacao/`, `src/cadastro/`, `src/recuperacao/`, `src/demonstracao/` | As telas e as regras de cada fluxo                                                                        |
| `src/termo/`                                                                  | O texto do termo vigente, lido da API                                                                     |
| `src/marca/`                                                                  | O logo em vetor, gerado de `design/assets/maxx-ouro.svg`                                                  |

### e2e

Contra o compose local de pé, com o mundo de demonstração:

```bash
docker compose run --rm seeder seed demo
npm test -w @maxx/e2e -- --grep-invert @demo

# A demonstração, com a flag ligada por cima e um lote descartável:
MAXX_DEMO_LOTE=e2e-local docker compose -f docker-compose.yml -f docker-compose.demo.yml up -d
npm test -w @maxx/e2e -- --grep @demo
docker compose up -d   # desliga de novo
```

Os testes criam linhas no cadastro elegível e alunos de demonstração no banco local. Os de
demonstração saem com a purga de `docs/decisoes/README.md`, com o lote usado.

## Medalhas e Troféus (passo 6)

As decisões estão em `docs/decisoes/README.md`, em "Medalhas, fila e worker (passo 6)".

| Onde                           | O quê                                                                                                   |
| ------------------------------ | ------------------------------------------------------------------------------------------------------- |
| `src/app/(app)/(abas)/`        | As cinco abas: Home (início provisório), Busca, Mural, Troféus e Perfil; Busca, Mural e Perfil em breve |
| `src/navegacao/`               | `LayoutDasAbas`, com o `BottomNav` do passo 4, e as telas "Em breve"                                    |
| `src/app/(app)/turmas/`        | As turmas do professor e os alunos de cada turma, fora das abas                                         |
| `src/professor/`               | Consultas e concessão (`api.ts`), as telas de turmas e o `ModalDeConcessao`                             |
| `src/trofeus/`                 | A tela de Troféus: abas, lista, trilhas e a montagem pura em `montagem.ts`                              |
| `src/components/Cabecalho.tsx` | O topo das telas internas, com voltar                                                                   |

O professor entra por "Minhas turmas", no início. O e2e do passo (`e2e/tests/medalhas.spec.ts`)
cria a própria professora, que ganha conta pelo `seed demo` e senha pelo Mailpit, e roda o
comando de anulação no contêiner do seeder; precisa do compose de pé e de hoje cair num
bimestre do calendário.

## Nativo

O identificador do app é `tech.gimenes.maxx`, no Android e no iOS. `npx expo prebuild` gera
`android/` e `ios/`, que ficam fora do git. Ele também mexe em `package.json`, e isso não
deve entrar em commit.

## Verificações

```bash
npm run lint -w @maxx/mobile
npm run typecheck -w @maxx/mobile
npm test -w @maxx/mobile
```
