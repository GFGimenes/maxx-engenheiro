import { useQueryClient } from "@tanstack/react-query";
import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useSyncExternalStore,
  type ReactNode,
} from "react";

import type {
  EstadoDaSessao,
  GerenciadorDeSessao,
  SessaoResponse,
} from "./GerenciadorDeSessao";

interface Sessao {
  estado: EstadoDaSessao;
  iniciar: (sessao: SessaoResponse) => Promise<void>;
  sair: () => Promise<void>;
  // Depois de "sem-conexao": tenta restaurar de novo.
  tentarDeNovo: () => Promise<void>;
}

const SessaoContext = createContext<Sessao | null>(null);

export function SessaoProvider({
  gerenciador,
  children,
}: {
  gerenciador: GerenciadorDeSessao;
  children: ReactNode;
}) {
  const queryClient = useQueryClient();
  const estado = useSyncExternalStore(
    gerenciador.observar,
    () => gerenciador.estado,
  );

  useEffect(() => {
    void gerenciador.restaurar();
  }, [gerenciador]);

  // Dado de uma conta não pode aparecer para a próxima que entrar no mesmo aparelho.
  useEffect(() => {
    if (estado === "anonimo") {
      queryClient.clear();
    }
  }, [estado, queryClient]);

  const sessao = useMemo<Sessao>(
    () => ({
      estado,
      iniciar: (resposta) => gerenciador.iniciar(resposta),
      sair: () => gerenciador.sair(),
      tentarDeNovo: () => gerenciador.restaurar(),
    }),
    [estado, gerenciador],
  );

  return (
    <SessaoContext.Provider value={sessao}>{children}</SessaoContext.Provider>
  );
}

export function useSessao(): Sessao {
  const sessao = useContext(SessaoContext);
  if (!sessao) {
    throw new Error("useSessao fora do SessaoProvider.");
  }
  return sessao;
}
