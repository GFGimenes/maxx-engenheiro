import { fireEvent, screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { cores } from "../theme/tokens";
import { EmptyState, ErrorState } from "./StateMessage";

const OCULTOS = { includeHiddenElements: true };
const cor = (texto: string) =>
  StyleSheet.flatten(screen.getByText(texto).props.style).color;

// O SVG é nativo: aqui, o caminho vira uma View que mostra o traço pedido.
jest.mock("react-native-svg", () => {
  const { createElement } = jest.requireActual("react");
  const { View } = jest.requireActual("react-native");
  function elemento(tipo: string) {
    return function ElementoSvg(props: object) {
      return createElement(View, { testID: tipo, ...props });
    };
  }
  return {
    __esModule: true,
    default: elemento("svg"),
    Path: elemento("path"),
    Circle: elemento("circle"),
    Rect: elemento("rect"),
  };
});

test("vazio mostra título, explicação e o ícone em ink2", async () => {
  await renderWithTheme(
    <EmptyState title="Nenhum aviso" message="Os avisos aparecem aqui." />,
  );

  expect(cor("Nenhum aviso")).toBe(cores.claro.ink);
  expect(cor("Os avisos aparecem aqui.")).toBe(cores.claro.ink2);
  expect(screen.getByTestId("icone-inbox", OCULTOS)).toBeTruthy();
  expect(screen.getAllByTestId("path", OCULTOS)[0]?.props.stroke).toBe(
    cores.claro.ink2,
  );
  expect(screen.queryByRole("button")).toBeNull();
});

test("vazio pode oferecer uma saída", async () => {
  const onPress = jest.fn();
  await renderWithTheme(
    <EmptyState title="Nada aqui" action={{ label: "Buscar", onPress }} />,
  );

  await fireEvent.press(screen.getByRole("button", { name: "Buscar" }));

  expect(onPress).toHaveBeenCalledTimes(1);
});

test("erro traz mensagem amigável por padrão e é anunciado", async () => {
  await renderWithTheme(<ErrorState />);

  expect(screen.getByRole("alert")).toBeTruthy();
  expect(screen.getByText("Não deu certo agora.")).toBeTruthy();
  expect(screen.getByText("Tente de novo em instantes.")).toBeTruthy();
});

test("like fica só no ícone do erro, nunca no texto", async () => {
  await renderWithTheme(<ErrorState title="Falhou" message="Tente depois." />);

  expect(screen.getAllByTestId("circle", OCULTOS)[0]?.props.stroke).toBe(
    cores.claro.like,
  );
  expect(cor("Falhou")).toBe(cores.claro.ink);
  expect(cor("Tente depois.")).toBe(cores.claro.ink2);
});

test("tentar de novo só aparece com a ação, e a chama", async () => {
  const onRetry = jest.fn();
  const { rerender } = await renderWithTheme(<ErrorState />);
  expect(screen.queryByRole("button")).toBeNull();

  await rerender(<ErrorState onRetry={onRetry} />);
  await fireEvent.press(screen.getByRole("button", { name: "Tentar de novo" }));

  expect(onRetry).toHaveBeenCalledTimes(1);
});
