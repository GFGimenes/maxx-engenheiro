import { fireEvent, screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { cores } from "../theme/tokens";
import { TextField } from "./TextField";

const caixa = () =>
  StyleSheet.flatten(screen.getByTestId("campo-caixa").props.style);
const campo = (name: string) => screen.getByLabelText(name);

test("tem 48 de altura, raio 8 e borda fieldLine", async () => {
  await renderWithTheme(<TextField label="RA ou e-mail" />);

  expect(caixa()).toMatchObject({
    height: 48,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: cores.claro.fieldLine,
    backgroundColor: cores.claro.fieldBg,
  });
});

test("o placeholder sai em ink3", async () => {
  await renderWithTheme(<TextField label="Curso" placeholder="Escolha" />);

  expect(campo("Curso").props.placeholderTextColor).toBe(cores.claro.ink3);
});

test("em foco, a borda vira 1,5 em focus sem mover o texto, e volta ao sair", async () => {
  const onFocus = jest.fn();
  const onBlur = jest.fn();
  await renderWithTheme(
    <TextField label="Curso" onFocus={onFocus} onBlur={onBlur} />,
  );
  const antes = caixa();

  await fireEvent(campo("Curso"), "focus");
  expect(caixa()).toMatchObject({
    borderWidth: 1.5,
    borderColor: cores.claro.focus,
  });
  expect(caixa().paddingLeft + caixa().borderWidth).toBe(
    antes.paddingLeft + antes.borderWidth,
  );
  expect(onFocus).toHaveBeenCalledTimes(1);

  await fireEvent(campo("Curso"), "blur");
  expect(caixa().borderColor).toBe(cores.claro.fieldLine);
  expect(onBlur).toHaveBeenCalledTimes(1);
});

test("avisa o que foi digitado", async () => {
  const onChangeText = jest.fn();
  await renderWithTheme(
    <TextField label="Curso" onChangeText={onChangeText} />,
  );

  await fireEvent.changeText(campo("Curso"), "ADS");

  expect(onChangeText).toHaveBeenCalledWith("ADS");
});

test("campo comum não tem botão de mostrar", async () => {
  await renderWithTheme(<TextField label="Curso" />);

  expect(screen.queryByRole("button")).toBeNull();
  expect(campo("Curso").props.secureTextEntry).toBe(false);
});

test("senha começa escondida, e o botão alterna entre mostrar e ocultar", async () => {
  await renderWithTheme(<TextField label="Senha" password value="segredo" />);
  expect(campo("Senha").props.secureTextEntry).toBe(true);
  expect(campo("Senha").props.autoCapitalize).toBe("none");

  await fireEvent.press(screen.getByRole("button", { name: "Mostrar senha" }));
  expect(campo("Senha").props.secureTextEntry).toBe(false);
  expect(campo("Senha").props.value).toBe("segredo");

  await fireEvent.press(screen.getByRole("button", { name: "Ocultar senha" }));
  expect(campo("Senha").props.secureTextEntry).toBe(true);
});

test("desativado fica em surface2 e ink3, sem edição e sem mostrar a senha", async () => {
  await renderWithTheme(<TextField label="Senha" password disabled />);

  expect(caixa().backgroundColor).toBe(cores.claro.surface2);
  const entrada = screen.getByLabelText("Senha");
  expect(entrada.props.editable).toBe(false);
  expect(StyleSheet.flatten(entrada.props.style).color).toBe(cores.claro.ink3);

  await fireEvent.press(
    screen.getByRole("button", { name: "Mostrar senha", disabled: true }),
  );
  expect(entrada.props.secureTextEntry).toBe(true);
});
