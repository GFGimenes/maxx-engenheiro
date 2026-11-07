import { Redirect, useRouter } from "expo-router";
import { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

import { TelaDeAutenticacao } from "../autenticacao/TelaDeAutenticacao";
import { Button } from "../components/Button";
import { Icon } from "../components/Icon";
import { TextField } from "../components/TextField";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { useCadastro, type DadosDoCadastro } from "./CadastroContext";
import { PassoDoCadastro } from "./PassoDoCadastro";
import { erroDoNome } from "./regras";
import {
  useDisponibilidadeDoPrefixo,
  type Disponibilidade,
} from "./useDisponibilidadeDoPrefixo";

// Passo 2 de 3: como a pessoa aparece. O arroba vem sugerido; ela muda só a parte antes do
// hífen, e o sufixo do curso é do sistema (produto.md 2.3).
export function ArrobaScreen() {
  const { dados } = useCadastro();
  if (!dados) {
    // Recarregou no meio: os dados ficam só em memória.
    return <Redirect href="/cadastro" />;
  }
  return <Formulario dados={dados} />;
}

function Formulario({ dados }: { dados: DadosDoCadastro }) {
  const router = useRouter();
  const { completar } = useCadastro();
  const [nome, setNome] = useState(dados.nomeDeExibicao);
  const [prefixo, setPrefixo] = useState(dados.prefixo);
  const [erroNome, setErroNome] = useState<string>();
  const disponibilidade = useDisponibilidadeDoPrefixo(
    dados.ra,
    dados.email,
    prefixo,
    dados.prefixoSugerido,
  );
  const prefixoRecusado =
    disponibilidade.estado === "invalido"
      ? disponibilidade.mensagem
      : disponibilidade.estado === "indisponivel"
        ? "Esse arroba já tem dono. Tente outro."
        : undefined;

  const continuar = () => {
    const erro = erroDoNome(nome);
    setErroNome(erro);
    if (erro || prefixoRecusado) {
      return;
    }
    completar({ nomeDeExibicao: nome.trim(), prefixo });
    router.push("/cadastro/termo");
  };

  return (
    <TelaDeAutenticacao>
      <View style={styles.formulario}>
        <PassoDoCadastro
          numero={2}
          titulo="Como você vai aparecer"
          onVoltar={() => {
            completar({ nomeDeExibicao: nome, prefixo });
            router.back();
          }}
        />
        <TextField
          label="Nome de exibição"
          placeholder="Seu nome no Maxx"
          value={nome}
          onChangeText={setNome}
          autoComplete="name"
          error={erroNome}
        />
        <View style={styles.arroba}>
          <TextField
            label="Arroba"
            value={prefixo}
            onChangeText={(texto) => setPrefixo(texto.trim())}
            autoCapitalize="none"
            autoCorrect={false}
            maxLength={30}
            error={prefixoRecusado}
          />
          <PreviaDoArroba
            arroba={`@${prefixo}-${dados.sufixo}`}
            disponibilidade={disponibilidade}
          />
        </View>

        <Button
          label="Continuar"
          size="large"
          disabled={!!prefixoRecusado}
          onPress={continuar}
        />
      </View>
    </TelaDeAutenticacao>
  );
}

// Como o arroba fica, e se está livre. O texto diz o estado; a cor nunca sozinha.
function PreviaDoArroba({
  arroba,
  disponibilidade,
}: {
  arroba: string;
  disponibilidade: Disponibilidade;
}) {
  const { colors } = useTheme();
  if (disponibilidade.estado === "invalido") {
    return null;
  }

  const estado = {
    verificando: "Conferindo se está livre...",
    disponivel: "Está livre.",
    indisponivel: null,
    "nao-verificado":
      "Não deu para conferir agora. Conferimos de novo ao criar a conta.",
  }[disponibilidade.estado];

  return (
    <View
      testID="previa-do-arroba"
      aria-live="polite"
      accessible
      style={styles.previa}
    >
      <Text style={[textStyles.corpo, { color: colors.ink }]}>{arroba}</Text>
      {estado && (
        <View style={styles.estado}>
          {disponibilidade.estado === "disponivel" && (
            <Icon name="check" color={colors.ink2} size={16} />
          )}
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            {estado}
          </Text>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  formulario: { gap: 16 },
  arroba: { gap: 8 },
  previa: { gap: 4 },
  estado: { flexDirection: "row", alignItems: "center", gap: 4 },
});
