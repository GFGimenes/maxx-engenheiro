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

## Verificações

```bash
npm run lint -w @maxx/mobile
npm run typecheck -w @maxx/mobile
npm test -w @maxx/mobile
```
