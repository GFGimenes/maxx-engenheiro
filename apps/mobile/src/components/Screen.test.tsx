import { render, screen } from "@testing-library/react-native";
import { StyleSheet, Text } from "react-native";
import { SafeAreaProvider } from "react-native-safe-area-context";

import { Screen } from "./Screen";

const SEM_RECORTE = { top: 0, bottom: 0, left: 0, right: 0 };

function renderCom(
  insets: typeof SEM_RECORTE,
  scroll?: boolean,
  aboveNav?: boolean,
) {
  return render(
    <SafeAreaProvider
      initialMetrics={{
        insets,
        frame: { x: 0, y: 0, width: 390, height: 844 },
      }}
    >
      <Screen scroll={scroll} aboveNav={aboveNav}>
        <Text>conteúdo</Text>
      </Screen>
    </SafeAreaProvider>,
  );
}

test("tem margem lateral de 16 e 24 de respiro no fim", async () => {
  await renderCom(SEM_RECORTE);

  expect(
    StyleSheet.flatten(screen.getByTestId("tela").props.style),
  ).toMatchObject({ paddingLeft: 16, paddingRight: 16, paddingBottom: 24 });
  expect(screen.getByText("conteúdo")).toBeTruthy();
});

test("soma a área segura do aparelho ao espaço da tela", async () => {
  await renderCom({ top: 47, bottom: 34, left: 0, right: 0 });

  expect(
    StyleSheet.flatten(screen.getByTestId("tela").props.style),
  ).toMatchObject({ paddingTop: 47, paddingBottom: 58 });
});

test("acima da navegação, a área segura de baixo não conta duas vezes", async () => {
  await renderCom({ top: 47, bottom: 34, left: 0, right: 0 }, false, true);

  expect(
    StyleSheet.flatten(screen.getByTestId("tela").props.style),
  ).toMatchObject({ paddingTop: 47, paddingBottom: 24 });
});

test("com rolagem, o espaço vai no conteúdo, e o respiro só aparece no fim", async () => {
  await renderCom(SEM_RECORTE, true);

  const tela = screen.getByTestId("tela");
  expect(StyleSheet.flatten(tela.props.contentContainerStyle)).toMatchObject({
    paddingLeft: 16,
    paddingBottom: 24,
  });
  expect(StyleSheet.flatten(tela.props.style).paddingBottom).toBeUndefined();
});
