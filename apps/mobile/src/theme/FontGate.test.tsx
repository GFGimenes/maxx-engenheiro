import { render, screen } from "@testing-library/react-native";
import { useFonts } from "expo-font";
import { Text } from "react-native";

import { FontGate } from "./FontGate";
import { fontAssets } from "./fonts";

jest.mock("expo-font", () => ({ useFonts: jest.fn() }));

async function renderGate(state: [boolean, Error | null]) {
  jest.mocked(useFonts).mockReturnValue(state);
  await render(
    <FontGate>
      <Text>conteúdo</Text>
    </FontGate>,
  );
}

test("pede todas as fontes e não pinta enquanto carregam", async () => {
  await renderGate([false, null]);

  expect(useFonts).toHaveBeenCalledWith(fontAssets);
  expect(screen.queryByText("conteúdo")).toBeNull();
});

test("pinta quando as fontes carregam", async () => {
  await renderGate([true, null]);

  expect(screen.getByText("conteúdo")).toBeTruthy();
});

test("se a carga falhar, o app segue com a fonte do sistema", async () => {
  await renderGate([false, new Error("sem fonte")]);

  expect(screen.getByText("conteúdo")).toBeTruthy();
});
