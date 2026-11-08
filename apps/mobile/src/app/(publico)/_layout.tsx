import { Redirect, Slot, useGlobalSearchParams, type Href } from "expo-router";

import { useDestinoDepoisDoLogin } from "../../navegacao/voltar";
import { EsperaDaSessao } from "../../sessao/EsperaDaSessao";
import { useSessao } from "../../sessao/SessaoProvider";

// Login, cadastro, termo, recuperação e demonstração: só para quem não entrou. Quem já tem
// sessão vai para o início, ou para onde ia antes do login (`?voltar=`, só caminho interno).
// Sem conexão na abertura, o formulário aparece do mesmo jeito: é ele que vai tentar falar
// com a API.
export default function PublicoLayout() {
  const { estado } = useSessao();
  const { voltar } = useGlobalSearchParams<{ voltar?: string }>();
  const destino = useDestinoDepoisDoLogin(estado === "autenticado", voltar);

  if (estado === "restaurando") {
    return <EsperaDaSessao />;
  }

  if (estado === "autenticado") {
    return <Redirect href={(destino ?? "/") as Href} />;
  }

  return <Slot />;
}
