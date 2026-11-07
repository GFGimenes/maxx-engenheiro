import AsyncStorage from "@react-native-async-storage/async-storage";
import { fireEvent, screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { THEME_STORAGE_KEY } from "../theme/ThemeProvider";
import { cores } from "../theme/tokens";
import { TextField } from "./TextField";

const caixa = () =>
  StyleSheet.flatten(screen.getByTestId("campo-caixa").props.style);
const campo = (name: string) => screen.getByLabelText(name);

beforeEach(() => AsyncStorage.clear());

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

test("sem erro, não há mensagem nem marca de inválido", async () => {
  await renderWithTheme(<TextField label="RA" />);

  expect(screen.queryByTestId("campo-erro")).toBeNull();
  expect(campo("RA").props["aria-invalid"]).toBe(false);
});

test("com erro, a borda vira like, o ícone sai em like e o texto em ink", async () => {
  await renderWithTheme(
    <TextField
      label="Senha"
      password
      error="A senha precisa ter de 8 a 128 caracteres."
    />,
  );

  expect(caixa()).toMatchObject({
    borderColor: cores.claro.like,
    borderWidth: 1,
  });
  const mensagem = screen.getByText(
    "A senha precisa ter de 8 a 128 caracteres.",
  );
  expect(StyleSheet.flatten(mensagem.props.style).color).toBe(cores.claro.ink);
  expect(
    screen.getByTestId("icone-alert", { includeHiddenElements: true }),
  ).toBeTruthy();
});

test("com erro, a borda continua like no foco, com a espessura do foco", async () => {
  await renderWithTheme(<TextField label="RA" error="Confira o RA." />);

  await fireEvent(campo("RA"), "focus");

  expect(caixa()).toMatchObject({
    borderColor: cores.claro.like,
    borderWidth: 1.5,
  });
});

test("o erro é anunciado e fica ligado ao campo", async () => {
  await renderWithTheme(<TextField label="RA" error="Confira o RA." />);

  expect(screen.getByRole("alert")).toHaveTextContent("Confira o RA.");
  expect(campo("RA").props["aria-invalid"]).toBe(true);
  expect(campo("RA").props.accessibilityHint).toBe("Confira o RA.");
});

test("a caixa do campo tem a mesma altura com e sem erro", async () => {
  const { rerender } = await renderWithTheme(<TextField label="RA" />);
  const sem = caixa().height;

  rerender(<TextField label="RA" error="Confira o RA." />);

  expect(caixa().height).toBe(sem);
});

test("no escuro, a borda do erro é o like do escuro", async () => {
  await AsyncStorage.setItem(THEME_STORAGE_KEY, "escuro");
  await renderWithTheme(<TextField label="RA" error="Confira o RA." />);

  expect(caixa().borderColor).toBe(cores.escuro.like);
});
