import { Pressable, StyleSheet, Text, View } from "react-native";

import { fontFamily } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { interacao, raios, tamanhos } from "../theme/tokens";
import { focusRingStyle, noOutline, useFocusRing } from "./focusRing";

interface SegmentedControlProps<T extends string> {
  // Nome do grupo para o leitor de tela.
  label: string;
  options: readonly { value: T; label: string }[];
  value: T;
  onChange: (value: T) => void;
  disabled?: boolean;
}

const RESPIRO_DO_TRILHO = 4;
const BORDA_DO_TRILHO = 1;

export function SegmentedControl<T extends string>({
  label,
  options,
  value,
  onChange,
  disabled = false,
}: SegmentedControlProps<T>) {
  const { colors } = useTheme();

  return (
    <View
      role="radiogroup"
      aria-label={label}
      testID="segmentado-trilho"
      style={[
        styles.trilho,
        {
          backgroundColor: disabled ? colors.surface2 : colors.surface,
          borderColor: colors.line,
        },
      ]}
    >
      {options.map((option) => (
        <Segment
          key={option.value}
          label={option.label}
          selected={option.value === value}
          disabled={disabled}
          onPress={() => onChange(option.value)}
        />
      ))}
    </View>
  );
}

function Segment({
  label,
  selected,
  disabled,
  onPress,
}: {
  label: string;
  selected: boolean;
  disabled: boolean;
  onPress: () => void;
}) {
  const { name, colors } = useTheme();
  const { focusVisible, focusProps } = useFocusRing();

  return (
    <Pressable
      role="radio"
      aria-checked={selected}
      aria-label={label}
      disabled={disabled}
      onPress={onPress}
      {...focusProps}
      style={({ pressed }) => [
        styles.segmento,
        noOutline,
        selected && { backgroundColor: colors.segmentOn },
        // A única sombra da interface: controle segmentado, só no claro.
        selected &&
          !disabled &&
          name === "claro" && { boxShadow: colors.segmentShadow },
        pressed && { opacity: interacao.opacidadePressionado },
        focusVisible && focusRingStyle(colors.focus),
      ]}
    >
      <Text
        numberOfLines={1}
        style={[
          styles.texto,
          {
            color: disabled ? colors.ink3 : selected ? colors.ink : colors.ink2,
          },
        ]}
      >
        {label}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  trilho: {
    flexDirection: "row",
    borderRadius: raios.controleSegmentado,
    borderWidth: BORDA_DO_TRILHO,
    padding: RESPIRO_DO_TRILHO,
  },
  // Com o respiro e a borda do trilho, cada segmento chega aos 44 de toque.
  segmento: {
    flex: 1,
    minHeight: tamanhos.alvoDeToque - 2 * (RESPIRO_DO_TRILHO + BORDA_DO_TRILHO),
    paddingHorizontal: 12,
    alignItems: "center",
    justifyContent: "center",
    borderRadius: raios.segmentoAtivo,
  },
  texto: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 600 }),
    fontSize: tamanhos.botaoDiscreto.texto,
  },
});
