import { Slot } from "expo-router";

import { CadastroProvider } from "../../../cadastro/CadastroContext";

// Os três passos do cadastro dividem os dados em memória.
export default function CadastroLayout() {
  return (
    <CadastroProvider>
      <Slot />
    </CadastroProvider>
  );
}
