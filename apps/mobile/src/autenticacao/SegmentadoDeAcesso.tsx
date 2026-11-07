import { useRouter } from "expo-router";

import { SegmentedControl } from "../components/SegmentedControl";

type Acesso = "entrar" | "cadastrar";

const OPCOES = [
  { value: "entrar", label: "Entrar" },
  { value: "cadastrar", label: "Cadastrar" },
] as const;

const ROTAS = { entrar: "/login", cadastrar: "/cadastro" } as const;

// O Entrar / Cadastrar das pranchetas. Cada lado é uma rota, para o link e o voltar do
// navegador funcionarem.
export function SegmentadoDeAcesso({ atual }: { atual: Acesso }) {
  const router = useRouter();

  return (
    <SegmentedControl
      label="Acesso"
      options={OPCOES}
      value={atual}
      onChange={(acesso) => {
        if (acesso !== atual) {
          router.replace(ROTAS[acesso]);
        }
      }}
    />
  );
}
