import { createContext, useContext, useState, type ReactNode } from "react";

// O que o cadastro junta nas três telas. Fica só em memória: a senha não vai para a URL
// nem para o armazenamento, e recarregar a página recomeça do primeiro passo.
export interface DadosDoCadastro {
  ra: string;
  email: string;
  senha: string;
  // A sugestão da API, que já confere RA e e-mail na mesma linha.
  sufixo: string;
  prefixoSugerido: string;
  nomeDeExibicao: string;
  prefixo: string;
}

interface Cadastro {
  dados: DadosDoCadastro | null;
  // O passo 1 começa o cadastro; os seguintes completam.
  comecar: (dados: DadosDoCadastro) => void;
  completar: (parte: Partial<DadosDoCadastro>) => void;
}

const CadastroContext = createContext<Cadastro | null>(null);

export function CadastroProvider({ children }: { children: ReactNode }) {
  const [dados, setDados] = useState<DadosDoCadastro | null>(null);

  return (
    <CadastroContext.Provider
      value={{
        dados,
        comecar: setDados,
        completar: (parte) =>
          setDados((atual) => (atual ? { ...atual, ...parte } : atual)),
      }}
    >
      {children}
    </CadastroContext.Provider>
  );
}

export function useCadastro(): Cadastro {
  const cadastro = useContext(CadastroContext);
  if (!cadastro) {
    throw new Error("useCadastro fora do CadastroProvider.");
  }
  return cadastro;
}
