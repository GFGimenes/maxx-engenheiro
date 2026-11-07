import { useState, type ReactNode } from "react";
import { StyleSheet, Text, View } from "react-native";

import { Avatar } from "../components/Avatar";
import { BottomNav, type Aba } from "../components/BottomNav";
import { Button } from "../components/Button";
import { FilterChip } from "../components/FilterChip";
import { Icon, iconNames } from "../components/Icon";
import { Screen } from "../components/Screen";
import { SegmentedControl } from "../components/SegmentedControl";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { TextField } from "../components/TextField";
import { TextLink } from "../components/TextLink";
import { Logo } from "../marca/Logo";
import { FaixaPremio } from "../premios/FaixaPremio";
import { Medalhao } from "../premios/Medalhao";
import { SeloPremio } from "../premios/SeloPremio";
import { TrilhaFamilia, type ItemDaTrilha } from "../premios/TrilhaFamilia";
import { textStyles } from "../theme/fonts";
import { ThemePreferenceControl } from "../theme/ThemePreferenceControl";
import { useTheme } from "../theme/ThemeProvider";
import {
  espacamento,
  tamanhos,
  type Raridade,
  type Tier,
} from "../theme/tokens";

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

        <Estrutura />

        <Premios />

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
        <TextField
          label="Crie uma senha"
          password
          value="curta"
          error="A senha precisa ter de 8 a 128 caracteres."
        />
      </Secao>

      <Secao titulo="Marca e links">
        <View style={styles.centro}>
          <Logo largura={245} />
        </View>
        <TextLink label="Esqueci minha senha" onPress={nada} />
        <TextLink label="Esqueci minha senha" onPress={nada} disabled />
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

function Estrutura() {
  const [aba, setAba] = useState<Aba>("mural");

  return (
    <>
      <Secao titulo="Avatar">
        <View style={styles.linha}>
          <Avatar nome="Ana Souza" size={tamanhos.avatarNav} />
          <Avatar nome="João Lima" />
          <Avatar nome="Maria Costa" size={80} />
        </View>
      </Secao>

      <Secao titulo="Navegação">
        <BottomNav
          active={aba}
          onSelect={setAba}
          nome="Ana Souza"
          novidades={{ mural: true, trofeus: true }}
        />
      </Secao>

      <Secao titulo="Vazio">
        <EmptyState
          title="Nenhum aviso por aqui"
          message="Os avisos da faculdade aparecem nesta tela."
          action={{ label: "Atualizar", onPress: () => {} }}
        />
      </Secao>

      <Secao titulo="Erro">
        <ErrorState onRetry={() => {}} />
      </Secao>
    </>
  );
}

const TIERS: Tier[] = ["bronze", "prata", "ouro", "diamante"];
const RARIDADES: Raridade[] = ["comum", "raro", "epico", "lendario"];
const passo = (
  id: string,
  tier: Tier,
  obtido: boolean,
  detalhes: string[],
): ItemDaTrilha => ({ id, rotulo: id, tier, glifo: "taca", obtido, detalhes });
const QUATRO_PASSOS = [
  passo("Bronze", "bronze", true, ["+5 pts", "1º sem."]),
  passo("Prata", "prata", true, ["+35 pts", "2º sem."]),
  passo("Ouro", "ouro", true, ["+75 pts", "3º sem."]),
  passo("Diamante", "diamante", false, ["101 a 150 pts", "próximo"]),
];
const CINCO_PASSOS = [
  passo("1 bimestre", "bronze", true, ["+10 pts"]),
  passo("2 bimestres", "prata", false, ["+40 pts", "próximo"]),
  passo("Semestre", "ouro", false, ["+80 pts"]),
  passo("Ano", "diamante", false, ["+120 pts"]),
  passo("Curso", "diamante", false, ["+150 pts"]),
];

function Premios() {
  return (
    <>
      <Secao titulo="Medalhões: tier e raridade">
        {RARIDADES.map((raridade) => (
          <View key={raridade} style={styles.matriz}>
            {TIERS.map((tier, indice) => (
              <Medalhao
                key={tier}
                tier={tier}
                raridade={raridade}
                glifo={indice % 2 === 0 ? "taca" : "fita"}
              />
            ))}
          </View>
        ))}
      </Secao>

      <Secao titulo="Selos">
        <SeloPremio
          nome="Pássaro Raro no Campus"
          tier="diamante"
          glifo="taca"
        />
        <SeloPremio nome="Techday 2026" tier="ouro" glifo="taca" />
        <SeloPremio nome="Assiduidade semestral" tier="prata" glifo="fita" />
        <SeloPremio nome="Média 10" tier="bronze" glifo="fita" />
      </Secao>

      <Secao titulo="Faixas">
        <FaixaPremio
          nome="Pássaro Raro no Campus"
          tier="diamante"
          raridade="lendario"
          glifo="taca"
          pontos={150}
          nota="foto validada"
        />
        <FaixaPremio
          nome="Techday 2026"
          tier="ouro"
          raridade="raro"
          glifo="taca"
          pontos={80}
          rotulo="Último troféu"
        />
      </Secao>

      <Secao titulo="Trilhas">
        <TrilhaFamilia titulo="Duas etapas" itens={QUATRO_PASSOS.slice(0, 2)} />
        <TrilhaFamilia
          titulo="Média 10 em uma matéria"
          resumo="3 de 4 · 115 pts"
          itens={QUATRO_PASSOS}
        />
        <TrilhaFamilia
          titulo="Cinco passos rolam na horizontal"
          resumo="1 de 5"
          itens={CINCO_PASSOS}
        />
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
  centro: { alignItems: "center" },
  secao: { gap: 12, borderTopWidth: 1, paddingTop: 16 },
  grade: { flexDirection: "row", flexWrap: "wrap", gap: 16 },
  linha: { flexDirection: "row", flexWrap: "wrap", columnGap: 8 },
  matriz: { flexDirection: "row", justifyContent: "space-between" },
  medida: { flexDirection: "row", alignItems: "center", gap: 8 },
});
