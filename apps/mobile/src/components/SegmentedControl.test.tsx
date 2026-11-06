import AsyncStorage from "@react-native-async-storage/async-storage";
import { fireEvent, screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { THEME_STORAGE_KEY } from "../theme/ThemeProvider";
import { cores } from "../theme/tokens";
import { SegmentedControl } from "./SegmentedControl";

const OPCOES = [
  { value: "entrar", label: "Entrar" },
  { value: "cadastrar", label: "Cadastrar" },
] as const;

function Controle({
  onChange = () => {},
  disabled,
}: {
  onChange?: (value: string) => void;
  disabled?: boolean;
}) {
  return (
    <SegmentedControl
      label="Acesso"
      options={OPCOES}
      value="entrar"
      onChange={onChange}
      disabled={disabled}
    />
  );
}

const estilo = (name: string) =>
  StyleSheet.flatten(screen.getByRole("radio", { name }).props.style);

beforeEach(() => AsyncStorage.clear());

test("o trilho é surface com raio 10; o segmento ativo, segmentOn com raio 8", async () => {
  await renderWithTheme(<Controle />);

  expect(
    StyleSheet.flatten(screen.getByTestId("segmentado-trilho").props.style),
  ).toMatchObject({
    backgroundColor: cores.claro.surface,
    borderRadius: 10,
  });
  expect(estilo("Entrar")).toMatchObject({
    backgroundColor: cores.claro.segmentOn,
    borderRadius: 8,
  });
  expect(estilo("Cadastrar").backgroundColor).toBeUndefined();
});

test("cada segmento chega aos 44 de toque junto com o trilho", async () => {
  await renderWithTheme(<Controle />);

  expect(estilo("Entrar").minHeight + 2 * (4 + 1)).toBe(44);
});

test("marca a opção vigente e avisa a escolhida", async () => {
  const onChange = jest.fn();
  await renderWithTheme(<Controle onChange={onChange} />);

  expect(
    screen.getByRole("radio", { name: "Entrar", checked: true }),
  ).toBeTruthy();
  await fireEvent.press(screen.getByRole("radio", { name: "Cadastrar" }));

  expect(onChange).toHaveBeenCalledWith("cadastrar");
});

test("a sombra aparece só no segmento ativo, e só no claro", async () => {
  await renderWithTheme(<Controle />);

  expect(estilo("Entrar").boxShadow).toBe(cores.claro.segmentShadow);
  expect(estilo("Cadastrar").boxShadow).toBeUndefined();
});

test("no escuro não há sombra", async () => {
  await AsyncStorage.setItem(THEME_STORAGE_KEY, "escuro");
  await renderWithTheme(<Controle />);

  expect(estilo("Entrar").backgroundColor).toBe(cores.escuro.segmentOn);
  expect(estilo("Entrar").boxShadow).toBeUndefined();
});

test("desativado usa surface2 e ink3, sem sombra e sem responder", async () => {
  const onChange = jest.fn();
  await renderWithTheme(<Controle onChange={onChange} disabled />);

  expect(
    StyleSheet.flatten(screen.getByTestId("segmentado-trilho").props.style)
      .backgroundColor,
  ).toBe(cores.claro.surface2);
  expect(StyleSheet.flatten(screen.getByText("Entrar").props.style).color).toBe(
    cores.claro.ink3,
  );
  expect(estilo("Entrar").boxShadow).toBeUndefined();

  await fireEvent.press(screen.getByRole("radio", { name: "Cadastrar" }));
  expect(onChange).not.toHaveBeenCalled();
});

test("em foco, o segmento ganha o anel do token", async () => {
  await renderWithTheme(<Controle />);

  await fireEvent(
    screen.getByRole("radio", { name: "Cadastrar" }),
    "focus",
    {},
  );

  expect(estilo("Cadastrar")).toMatchObject({
    outlineColor: cores.claro.focus,
    outlineWidth: 2,
    outlineOffset: 2,
  });
});
