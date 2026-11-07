import { Redirect, Slot } from "expo-router";

import { EsperaDaSessao } from "../../sessao/EsperaDaSessao";
import { useSessao } from "../../sessao/SessaoProvider";

// Login, cadastro, termo, recuperação e demonstração: só para quem não entrou. Quem já tem
// sessão vai para o início. Sem conexão na abertura, o formulário aparece do mesmo jeito:
// é ele que vai tentar falar com a API.
export default function PublicoLayout() {
  const { estado } = useSessao();

  if (estado === "restaurando") {
    return <EsperaDaSessao />;
  }

  if (estado === "autenticado") {
    return <Redirect href="/" />;
  }

  return <Slot />;
}
