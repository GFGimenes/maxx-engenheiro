# syntax=docker/dockerfile:1
# Contexto: raiz do repositório. O build web do Expo vai dentro da imagem do Caddy.

FROM node:24-alpine AS web
WORKDIR /repo

# Instalação em camada própria: só refaz quando um package.json ou o lock muda.
COPY package.json package-lock.json ./
COPY apps/mobile/package.json apps/mobile/
COPY packages/api-client/package.json packages/api-client/
RUN npm ci

COPY design/ design/
COPY packages/api-client/ packages/api-client/
COPY apps/mobile/ apps/mobile/
RUN npm run export:web -w @maxx/mobile

FROM caddy:2.11-alpine
COPY infra/caddy/Caddyfile /etc/caddy/Caddyfile
COPY --from=web /repo/apps/mobile/dist /srv
