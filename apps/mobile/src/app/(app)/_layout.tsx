import { Redirect, Slot } from "expo-router";

import { Screen } from "../../components/Screen";
import { ErrorState } from "../../components/StateMessage";
import { EsperaDaSessao } from "../../sessao/EsperaDaSessao";
import { useSessao } from "../../sessao/SessaoProvider";

// Tudo que exige login. Sem sessão, vai para o login; sem conexão para saber, explica e
// oferece tentar de novo, sem apagar a sessão guardada.
export default function AppLayout() {
  const { estado, tentarDeNovo } = useSessao();

  if (estado === "restaurando") {
    return <EsperaDaSessao />;
  }

  if (estado === "anonimo") {
    return <Redirect href="/login" />;
  }

  if (estado === "sem-conexao") {
    return (
      <Screen>
        <ErrorState
          title="Sem conexão."
          message="Confira a internet e tente de novo."
          onRetry={() => void tentarDeNovo()}
        />
      </Screen>
    );
  }

  return <Slot />;
}
