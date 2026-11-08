import { Redirect, Stack, usePathname, type Href } from "expo-router";

import { Screen } from "../../components/Screen";
import { ErrorState } from "../../components/StateMessage";
import { loginVoltandoPara } from "../../navegacao/voltar";
import { EsperaDaSessao } from "../../sessao/EsperaDaSessao";
import { useSessao } from "../../sessao/SessaoProvider";
import { useTheme } from "../../theme/ThemeProvider";

// Tudo que exige login. Sem sessão, vai para o login, que depois volta para cá (o deep link
// /post/:id); sem conexão para saber, explica e oferece tentar de novo, sem apagar a sessão
// guardada.
export default function AppLayout() {
  const { estado, tentarDeNovo } = useSessao();
  const { colors } = useTheme();
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

  // Pilha, não Slot: o post, o perfil de outra pessoa e o editar perfil ficam por cima das
  // abas, que continuam montadas embaixo; o voltar volta para a aba de onde se veio.
  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: colors.bg },
      }}
    />
  );
}
