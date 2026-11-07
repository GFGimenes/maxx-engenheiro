import { act, render, screen } from "@testing-library/react-native";
import { Text } from "react-native";
import { SafeAreaProvider } from "react-native-safe-area-context";

import { ThemeProvider } from "../theme/ThemeProvider";
import { TelaDeAutenticacao } from "./TelaDeAutenticacao";

async function renderizar(segmentado?: React.ReactNode) {
  await render(
    <SafeAreaProvider
      initialMetrics={{
        insets: { top: 0, bottom: 0, left: 0, right: 0 },
        frame: { x: 0, y: 0, width: 390, height: 844 },
      }}
    >
      <ThemeProvider>
        <TelaDeAutenticacao segmentado={segmentado}>
          <Text>formulário</Text>
        </TelaDeAutenticacao>
      </ThemeProvider>
    </SafeAreaProvider>,
  );
  await act(async () => {});
}

test("tem o logo, a frase, o conteúdo e o selo da rede no pé, como nas pranchetas", async () => {
  await renderizar();

  expect(screen.getByRole("img", { name: "Maxx" })).toBeTruthy();
  expect(screen.getByText("A rede de conquistas da UniMax")).toBeTruthy();
  expect(screen.getByText("formulário")).toBeTruthy();
  expect(screen.getByText("Rede UniEduk · UniMax e UniFaj")).toBeTruthy();
});

test("mostra o segmentado só quando a tela o tem", async () => {
  await renderizar(<Text>Entrar / Cadastrar</Text>);

  expect(screen.getByText("Entrar / Cadastrar")).toBeTruthy();
});
