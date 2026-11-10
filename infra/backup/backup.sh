#!/usr/bin/env bash
# Dump do Postgres da VPS em /opt/maxx/backups, com rotação de 14 dias (passo 13).
# Noturno pelo timer do systemd (maxx-backup.timer) e antes de toda migration pelo
# infra/deploy.sh. O envio para um bucket sem domínio público é pendência fora da fase 1.
#   /opt/maxx/infra/backup/backup.sh [rótulo]     rótulo: noturno (padrão) ou antes-de-migration
set -euo pipefail

cd /opt/maxx
rotulo="${1:-noturno}"
pasta=/opt/maxx/backups
dias=14
dc=(docker compose -f docker-compose.producao.yml)

umask 077
mkdir -p "$pasta"
chmod 700 "$pasta"

arquivo="$pasta/maxx-$(date -u +%Y%m%dT%H%M%SZ)-$rotulo.dump"

# Formato custom (-Fc): comprimido e restaurável tabela por tabela. Escrito primeiro com
# .parcial, para um dump interrompido nunca parecer um backup bom.
"${dc[@]}" exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$arquivo.parcial"

# O dump só vale se o pg_restore consegue ler o índice dele.
"${dc[@]}" exec -T postgres pg_restore --list < "$arquivo.parcial" > /dev/null
mv "$arquivo.parcial" "$arquivo"
echo "backup: $arquivo ($(du -h "$arquivo" | cut -f1))"

# Rotação: some o que tem mais de 14 dias, e também .parcial esquecido de uma falha.
find "$pasta" -maxdepth 1 -name 'maxx-*.dump*' -mmin +$((dias * 24 * 60)) -print -delete
