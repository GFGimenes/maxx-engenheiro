#!/usr/bin/env bash
# Implanta na VPS a versão de um commit cujas imagens o job publicar da CI já publicou
# (docs/operacao.md). Traz do repositório, no mesmo commit, os arquivos do compose e os
# scripts; troca a tag no .env; puxa as imagens; faz o dump antes de qualquer migration
# (decisoes/README.md, Backup); sobe, e o migrator roda antes das réplicas.
#   /opt/maxx/infra/deploy.sh <sha completo> [demo]
#   demo: sobe com o docker-compose.demo.yml por cima (ensaio e avaliação).
set -euo pipefail

sha="${1:?informe o sha completo do commit publicado}"
modo="${2:-}"
curta="sha-${sha:0:7}"
base="https://raw.githubusercontent.com/GFGimenes/maxx-engenheiro/$sha"

cd /opt/maxx
mkdir -p infra/backup
for arquivo in docker-compose.producao.yml docker-compose.demo.yml .env.producao.example \
  infra/deploy.sh infra/backup/backup.sh infra/backup/restaurar-teste.sh \
  infra/backup/maxx-backup.service infra/backup/maxx-backup.timer; do
  # Baixa ao lado e troca com mv: este próprio script está na lista, e o bash lê o arquivo
  # aos poucos enquanto roda; o mv deixa o processo atual com a versão que abriu.
  curl -fsSL "$base/$arquivo" -o "$arquivo.novo"
  mv "$arquivo.novo" "$arquivo"
done
chmod +x infra/deploy.sh infra/backup/*.sh

# Nome exigido faltando no .env: para aqui, sem tocar no que está no ar.
for nome in $(sed -n 's/^\([A-Z_]*\)=.*/\1/p' .env.producao.example); do
  grep -q "^$nome=..*" .env || { echo "falta no .env: $nome"; exit 1; }
done

sed -i "s/^MAXX_IMAGEM_TAG=.*/MAXX_IMAGEM_TAG=$curta/" .env
arquivos=(-f docker-compose.producao.yml)
[ "$modo" = demo ] && arquivos+=(-f docker-compose.demo.yml)
dc=(docker compose "${arquivos[@]}")

"${dc[@]}" pull -q

# O dump antes da migration. Na primeira subida não há banco a guardar.
if [ -n "$("${dc[@]}" ps -q --status running postgres)" ]; then
  infra/backup/backup.sh antes-de-migration
fi

"${dc[@]}" up -d --remove-orphans
for _ in $(seq 1 60); do
  curl -fsS https://app.maxx-unimax.com/api/ready > /dev/null 2>&1 && break
  sleep 2
done
curl -fsS https://app.maxx-unimax.com/api/ready > /dev/null
"${dc[@]}" ps --format '{{.Service}} {{.Image}} {{.Status}}'
echo "implantado: $curta${modo:+ ($modo)}"
