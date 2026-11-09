import { useLocalSearchParams } from "expo-router";

import { ehVisao } from "../../rankings/api";
import { RankingsScreen } from "../../rankings/RankingsScreen";

// /rankings?visao=historico: aberta pela posição no perfil (histórico geral, pergunta 4 do
// passo 11) e pelos atalhos da Busca. Sem visão conhecida, o geral do semestre.
export default function Rankings() {
  const { visao } = useLocalSearchParams<{ visao?: string }>();
  return <RankingsScreen inicial={ehVisao(visao) ? visao : "geral"} />;
}
