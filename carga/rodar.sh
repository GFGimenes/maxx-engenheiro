#!/usr/bin/env bash
# A carga reduzida do passo 13, na própria VPS, em duas rodadas:
#   1. planos: auto_explain ligado, 1 usuário virtual por 20 s. O Postgres registra o
#      EXPLAIN ANALYZE das consultas reais (feed, busca, ranking...) com o volume do seed
#      demo. Depois o auto_explain sai, e as réplicas reiniciam sem ele.
#   2. medida: 50 usuários virtuais por 2 minutos, sem auto_explain, para o p95.
# A chave JWT só passa do .env da VPS para o contêiner do k6, por arquivo temporário do
# root; nada é impresso. Saída em /root/carga: planos.log e resumo.json.
#   /opt/maxx/carga/rodar.sh
set -euo pipefail

cd /opt/maxx
dc=(docker compose -f docker-compose.producao.yml)
saida=/root/carga
k6=grafana/k6:1.8.2
umask 077
mkdir -p "$saida"

psql() { "${dc[@]}" exec -T postgres sh -c "psql -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -v ON_ERROR_STOP=1 -tA -c \"$1\""; }

# Os alunos ativos do seed (ids não são segredo), para o sub de cada token.
psql "SELECT coalesce(json_agg(json_build_object('id', u.id)), '[]') FROM usuario u
      JOIN cadastro_elegivel c ON c.id = u.cadastro_elegivel_id
      WHERE c.email LIKE '%@al.unieduk.invalid' AND u.ex_aluno_desde IS NULL" > "$saida/alunos.json"
echo "alunos: $(grep -o '"id"' "$saida/alunos.json" | wc -l)"

grep '^MAXX_JWT_CHAVE=' .env > "$saida/k6.env"
trap 'rm -f "$saida/k6.env"' EXIT

carga() {
  # Como root no contêiner: a pasta de saída é só do root, como o arquivo da chave.
  docker run --rm --user 0:0 --network host --env-file "$saida/k6.env" \
    -e MAXX_CARGA_ALUNOS=/carga/alunos.json -e MAXX_CARGA_VUS="$1" -e MAXX_CARGA_DURACAO="$2" \
    -v /opt/maxx/carga:/script:ro -v "$saida":/carga "$k6" run --quiet "${@:3}" /script/leitura.js
}

reiniciar_api() {
  "${dc[@]}" restart api > /dev/null
  until curl -fsS https://app.maxx-unimax.com/api/ready > /dev/null 2>&1; do sleep 2; done
}

# 1. Planos. MAXX_CARGA_SO_MEDIDA=1 pula esta parte.
if [ "${MAXX_CARGA_SO_MEDIDA:-0}" != 1 ]; then
psql "ALTER SYSTEM SET session_preload_libraries = 'auto_explain'"
psql "ALTER SYSTEM SET auto_explain.log_min_duration = 0"
psql "ALTER SYSTEM SET auto_explain.log_analyze = on"
psql "ALTER SYSTEM SET auto_explain.log_buffers = on"
psql "SELECT pg_reload_conf()" > /dev/null
reiniciar_api
desde=$(date -u +%Y-%m-%dT%H:%M:%SZ)
carga 1 20s > /dev/null || true
"${dc[@]}" logs --no-color --since "$desde" postgres > "$saida/planos.log"
psql "ALTER SYSTEM RESET session_preload_libraries"
psql "ALTER SYSTEM RESET auto_explain.log_min_duration"
psql "ALTER SYSTEM RESET auto_explain.log_analyze"
psql "ALTER SYSTEM RESET auto_explain.log_buffers"
psql "SELECT pg_reload_conf()" > /dev/null
reiniciar_api
echo "planos: $(grep -c 'duration:' "$saida/planos.log") consultas registradas"
fi

# 2. Medida, depois de um aquecimento descartado: as réplicas acabaram de reiniciar, e o
# primeiro pedido de cada rota paga a compilação do .NET e o pool de conexões.
carga 5 20s > /dev/null || true
carga "${MAXX_CARGA_VUS:-50}" "${MAXX_CARGA_DURACAO:-2m}" --summary-export /carga/resumo.json
