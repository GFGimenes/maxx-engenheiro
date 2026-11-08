import { useLocalSearchParams } from "expo-router";

import { PerfilScreen } from "../../../perfil/PerfilScreen";

// O perfil de outra pessoa, aberto pelo feed, pelos comentários ou pela Busca.
export default function PerfilDeOutro() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <PerfilScreen usuarioId={id} />;
}
