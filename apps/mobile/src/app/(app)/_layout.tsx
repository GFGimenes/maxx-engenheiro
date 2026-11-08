import { Redirect, Slot, usePathname, type Href } from "expo-router";

import { Screen } from "../../components/Screen";
import { ErrorState } from "../../components/StateMessage";
import { loginVoltandoPara } from "../../navegacao/voltar";
import { EsperaDaSessao } from "../../sessao/EsperaDaSessao";
import { useSessao } from "../../sessao/SessaoProvider";

// Tudo que exige login. Sem sessão, vai para o login, que depois volta para cá (o deep link
// /post/:id); sem conexão para saber, explica e oferece tentar de novo, sem apagar a sessão
// guardada.
export default function AppLayout() {
  const { estado, tentarDeNovo } = useSessao();
  const caminho = usePathname();

  if (estado === "restaurando") {
    return <EsperaDaSessao />;
  }

  if (estado === "anonimo") {
    return <Redirect href={loginVoltandoPara(caminho) as Href} />;
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
