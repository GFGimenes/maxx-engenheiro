#!/usr/bin/env bash
# Prova de que o backup volta (passo 13): restaura um dump num Postgres descartável, fora do
# compose, e compara a contagem de linhas de cada tabela com a do banco de produção. Não
# toca no banco de produção além de ler as contagens. O contêiner some no fim.
#   /opt/maxx/infra/backup/restaurar-teste.sh [arquivo.dump]   padrão: o dump mais novo
set -euo pipefail

cd /opt/maxx
dc=(docker compose -f docker-compose.producao.yml)
dump="${1:-$(ls -1t /opt/maxx/backups/maxx-*.dump | head -1)}"
nome="maxx-restauracao-teste-$$"
senha="$(head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9')"

trap 'docker rm -f "$nome" > /dev/null 2>&1 || true' EXIT

echo "dump: $dump"
docker run -d --name "$nome" -e POSTGRES_PASSWORD="$senha" -e POSTGRES_DB=restaurado postgres:18-alpine > /dev/null
until docker exec "$nome" pg_isready -U postgres -d restaurado > /dev/null 2>&1; do sleep 1; done
# O pg_isready responde antes de o script de inicialização da imagem terminar.
sleep 3
until docker exec "$nome" psql -U postgres -d restaurado -tAc 'select 1' > /dev/null 2>&1; do sleep 1; done

inicio=$(date +%s)
docker exec -i "$nome" pg_restore -U postgres -d restaurado --no-owner --exit-on-error < "$dump"
echo "restauração: $(( $(date +%s) - inicio )) s"

# Uma linha por tabela: "tabela|linhas". As contagens exatas, não a estimativa do catálogo.
contar='SELECT format($$SELECT %L || $s$|$s$ || count(*) FROM %I.%I;$$, table_name, table_schema, table_name)
        FROM information_schema.tables
        WHERE table_schema = $$public$$ AND table_type = $$BASE TABLE$$ ORDER BY table_name'
# ON_ERROR_STOP: uma contagem que falha derruba o script, em vez de deixar o arquivo vazio.
"${dc[@]}" exec -T postgres sh -c "set -e; psql -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -tAc '$contar' > /tmp/contar.sql; psql -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -v ON_ERROR_STOP=1 -tA -f /tmp/contar.sql" > /tmp/contagem-producao.txt
docker exec "$nome" sh -c "set -e; psql -U postgres -d restaurado -tAc '$contar' > /tmp/contar.sql; psql -U postgres -d restaurado -v ON_ERROR_STOP=1 -tA -f /tmp/contar.sql" > /tmp/contagem-restaurada.txt

# Dois arquivos vazios seriam "iguais": sem tabela contada, não houve prova nenhuma.
tabelas=$(grep -c '|' /tmp/contagem-restaurada.txt || true)
if [ "$tabelas" -eq 0 ] || [ "$(grep -c '|' /tmp/contagem-producao.txt || true)" -eq 0 ]; then
  echo "FALHA: nenhuma tabela contada; a comparação não prova nada."
  exit 1
fi
if diff /tmp/contagem-producao.txt /tmp/contagem-restaurada.txt; then
  echo "conferido: $tabelas tabelas com a mesma contagem de linhas da produção"
else
  echo "DIFERENÇA entre a produção e o restaurado (linhas acima: < produção, > restaurado)."
  echo "Escrita entre o dump e a contagem também aparece aqui: rode logo depois de um backup."
  exit 1
fi
