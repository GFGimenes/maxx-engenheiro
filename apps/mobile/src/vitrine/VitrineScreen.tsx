import { useState, type ReactNode } from "react";
import { StyleSheet, Text, View } from "react-native";

import { Button } from "../components/Button";
import { FilterChip } from "../components/FilterChip";
import { Icon, iconNames } from "../components/Icon";
import { Screen } from "../components/Screen";
import { SegmentedControl } from "../components/SegmentedControl";
import { TextField } from "../components/TextField";
import { textStyles } from "../theme/fonts";
import { ThemePreferenceControl } from "../theme/ThemePreferenceControl";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, tamanhos } from "../theme/tokens";

// Bancada do design system, em /vitrine. Só existe em desenvolvimento (app/vitrine.tsx). Cada
// etapa do passo 4 acrescenta aqui o que entrega.
export function VitrineScreen() {
  const { colors } = useTheme();

  return (
    <Screen scroll>
      <View style={styles.coluna}>
        <View style={styles.cabecalho}>
          <Text style={[textStyles.display, { color: colors.ink }]}>
            Vitrine
          </Text>
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            Só em desenvolvimento.
          </Text>
        </View>

        <ThemePreferenceControl />

        <Secao titulo="Tipografia">
          <Text style={[textStyles.display, { color: colors.ink }]}>
            15.020 pts
          </Text>
          <Text style={[textStyles.titulo, { color: colors.ink }]}>
            Troféus e medalhas
          </Text>
          <Text style={[textStyles.pontos, { color: colors.ink }]}>+150</Text>
          <Text style={[textStyles.secao, { color: colors.ink }]}>
            Pássaro Raro no Campus
          </Text>
          <Text style={[textStyles.corpo, { color: colors.ink }]}>
            Prova integrativa remarcada para 14/08.
          </Text>
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            ADS · 3º semestre · #29
          </Text>
          <Text style={[textStyles.rotulo, { color: colors.ink2 }]}>
            Último troféu
          </Text>
        </Secao>

        <Secao titulo="Ícones em contorno">
          <View style={styles.grade}>
            {iconNames.map((name) => (
              <Icon
                key={name}
                name={name}
                color={colors.ink2}
                size={tamanhos.iconeNav}
              />
            ))}
          </View>
        </Secao>

        <Secao titulo="Ícones preenchidos">
          <View style={styles.grade}>
            {iconNames.map((name) => (
              <Icon
                key={name}
                name={name}
                color={colors.ink}
                detailColor={colors.bg}
                size={tamanhos.iconeNav}
                filled
              />
            ))}
          </View>
        </Secao>

        <Controles />

        <Secao titulo="Espaçamento">
          {espacamento.escala.map((valor) => (
            <View key={valor} style={styles.medida}>
              <View
                style={{
                  width: valor,
                  height: valor,
                  backgroundColor: colors.ink2,
                }}
              />
              <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                {valor}
              </Text>
            </View>
          ))}
        </Secao>
      </View>
    </Screen>
  );
}

const ACESSO = [
  { value: "entrar", label: "Entrar" },
  { value: "cadastrar", label: "Cadastrar" },
] as const;
const FILTROS = ["Todos", "Faculdade", "Curso", "Professores"];

function Controles() {
  const [acesso, setAcesso] = useState<"entrar" | "cadastrar">("entrar");
  const [filtro, setFiltro] = useState("Todos");
  const [senha, setSenha] = useState("maxx2026");
  const nada = () => {};

  return (
    <>
      <Secao titulo="Botões">
        <Button label="Entrar" size="large" onPress={nada} />
        <Button label="Entrar" size="large" onPress={nada} disabled />
        <View style={styles.linha}>
          <Button label="Seguir" onPress={nada} />
          <Button label="Editar perfil" variant="secondary" onPress={nada} />
          <Button label="Seguir" onPress={nada} disabled />
        </View>
      </Secao>

      <Secao titulo="Campos">
        <TextField label="RA ou e-mail" placeholder="2023001" />
        <TextField
          label="Senha"
          password
          value={senha}
          onChangeText={setSenha}
        />
        <TextField label="Curso" value="ADS" disabled />
      </Secao>

      <Secao titulo="Controle segmentado">
        <SegmentedControl
          label="Acesso"
          options={ACESSO}
          value={acesso}
          onChange={setAcesso}
        />
        <SegmentedControl
          label="Acesso desativado"
          options={ACESSO}
          value="entrar"
          onChange={nada}
          disabled
        />
      </Secao>

      <Secao titulo="Filtros">
        <View style={styles.linha}>
          {FILTROS.map((nome) => (
            <FilterChip
              key={nome}
              label={nome}
              selected={nome === filtro}
              onPress={() => setFiltro(nome)}
            />
          ))}
          <FilterChip
            label="Desativado"
            selected={false}
            onPress={nada}
            disabled
          />
        </View>
      </Secao>
    </>
  );
}

function Secao({ titulo, children }: { titulo: string; children: ReactNode }) {
  const { colors } = useTheme();

  return (
    <View style={[styles.secao, { borderTopColor: colors.line }]}>
      <Text style={[textStyles.rotulo, { color: colors.ink2 }]}>{titulo}</Text>
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  coluna: { gap: 24, paddingTop: 16 },
  cabecalho: { gap: 4 },
  secao: { gap: 12, borderTopWidth: 1, paddingTop: 16 },
  grade: { flexDirection: "row", flexWrap: "wrap", gap: 16 },
  linha: { flexDirection: "row", flexWrap: "wrap", columnGap: 8 },
  medida: { flexDirection: "row", alignItems: "center", gap: 8 },
});
