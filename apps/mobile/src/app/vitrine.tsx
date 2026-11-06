import { Redirect } from "expo-router";
import type { ComponentType } from "react";

// Só em desenvolvimento. Com o `require` atrás do __DEV__, a vitrine nem entra no bundle de
// produção, e a rota volta para o início.
const VitrineScreen: ComponentType | null = __DEV__
  ? // eslint-disable-next-line @typescript-eslint/no-require-imports
    require("../vitrine/VitrineScreen").VitrineScreen
  : null;

export default function VitrineRoute() {
  return VitrineScreen ? <VitrineScreen /> : <Redirect href="/" />;
}
