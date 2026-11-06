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
