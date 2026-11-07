import { act, fireEvent, screen } from "@testing-library/react-native";
import { useEffect } from "react";

import { caminhos, json, renderComApi } from "../test/renderComApi";
import { ArrobaScreen } from "./ArrobaScreen";
import {
  CadastroProvider,
  useCadastro,
  type DadosDoCadastro,
} from "./CadastroContext";
import { DadosScreen } from "./DadosScreen";
import { erroDaSenha, erroDoNome, erroDoPrefixo } from "./regras";
import { TermoScreen } from "./TermoScreen";
import { ESPERA_DA_DIGITACAO_MS } from "./useDisponibilidadeDoPrefixo";

const mockRoteador = { push: jest.fn(), replace: jest.fn(), back: jest.fn() };
jest.mock("expo-router", () => ({
  useRouter: () => mockRoteador,
  Redirect: ({ href }: { href: string }) => {
    const { Text } = jest.requireActual("react-native");
    return <Text>{`redireciona para ${href}`}</Text>;
  },
}));

const DADOS: DadosDoCadastro = {
  ra: "2023001",
  email: "ana.souza@al.unieduk.com.br",
  senha: "senha-boa-123",
  sufixo: "CC",
  prefixoSugerido: "anaSouza",
  nomeDeExibicao: "",
  prefixo: "anaSouza",
};

const SESSAO = {
  accessToken: "a1",
  accessTokenExpiraEm: "2999-01-01T00:00:00Z",
  refreshToken: "r1",
  refreshTokenExpiraEm: "2999-01-01T00:00:00Z",
};

let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  api = jest.fn();
});

// A tela com o cadastro já começado, como se o passo anterior tivesse passado.
function ComDados({
  dados,
  children,
}: {
  dados: DadosDoCadastro | null;
  children: React.ReactNode;
}) {
  return (
    <CadastroProvider>
      <Semear dados={dados} />
      {children}
    </CadastroProvider>
  );
}

function Semear({ dados }: { dados: DadosDoCadastro | null }) {
  const { comecar } = useCadastro();
  useEffect(() => {
    if (dados) comecar(dados);
  }, [comecar, dados]);
  return null;
}

async function apertar(nome: string) {
  await act(async () => {
    fireEvent.press(await screen.findByRole("button", { name: nome }));
  });
  await act(async () => {});
}

describe("regras do lado do cliente", () => {
  it("senha de 8 a 128 caracteres, contando emoji como um", () => {
    expect(erroDaSenha("1234567")).toBeDefined();
    expect(erroDaSenha("12345678")).toBeUndefined();
    expect(erroDaSenha("😀".repeat(8))).toBeUndefined();
    expect(erroDaSenha("a".repeat(129))).toBeDefined();
  });

  it("arroba com letras sem acento e números, de 3 a 30, começando por letra", () => {
    expect(erroDoPrefixo("anaSouza2")).toBeUndefined();
    for (const ruim of ["an", "2ana", "ana-souza", "joão", "a".repeat(31)]) {
      expect(erroDoPrefixo(ruim)).toBeDefined();
    }
  });

  it("nome de 1 a 60 caracteres, sem contar os espaços das pontas", () => {
    expect(erroDoNome("   ")).toBeDefined();
    expect(erroDoNome(" Ana ")).toBeUndefined();
    expect(erroDoNome("a".repeat(61))).toBeDefined();
  });
});

describe("passo 1: dados", () => {
  async function preencher(ra: string, email: string, senha: string) {
    await fireEvent.changeText(screen.getByLabelText("RA"), ra);
    await fireEvent.changeText(
      screen.getByLabelText("E-mail institucional"),
      email,
    );
    await fireEvent.changeText(screen.getByLabelText("Crie uma senha"), senha);
  }

  it("tem os campos e textos da prancheta", async () => {
    await renderComApi(
      <ComDados dados={null}>
        <DadosScreen />
      </ComDados>,
      api,
    );

    expect(
      screen.getByRole("radio", { name: "Cadastrar", checked: true }),
    ).toBeTruthy();
    expect(screen.getByPlaceholderText("seu e-mail da faculdade")).toBeTruthy();
    expect(screen.getByPlaceholderText("Mínimo de 8 caracteres")).toBeTruthy();
  });

  it("senha curta é apontada no campo, sem ir à API", async () => {
    await renderComApi(
      <ComDados dados={null}>
        <DadosScreen />
      </ComDados>,
      api,
    );

    await preencher("2023001", "ana@al.unieduk.com.br", "curta");
    await apertar("Continuar");

    expect(
      await screen.findByText("A senha precisa ter de 8 a 128 caracteres."),
    ).toBeTruthy();
    expect(api).not.toHaveBeenCalled();
  });

  it("confere RA e e-mail pela sugestão e segue para o arroba", async () => {
    api.mockResolvedValue(
      json(200, { prefixo: "anaSouza", sufixo: "CC", arroba: "@anaSouza-CC" }),
    );
    await renderComApi(
      <ComDados dados={null}>
        <DadosScreen />
      </ComDados>,
      api,
    );

    await preencher(
      " 2023001 ",
      " ana.souza@al.unieduk.com.br ",
      "senha-boa-123",
    );
    await apertar("Continuar");

    expect(caminhos(api)).toEqual(["/api/v1/auth/cadastro/sugestao"]);
    expect(await api.mock.calls[0]![0].json()).toEqual({
      ra: "2023001",
      email: "ana.souza@al.unieduk.com.br",
    });
    expect(mockRoteador.push).toHaveBeenCalledWith("/cadastro/arroba");
  });

  it("RA que já tem conta leva à recuperação de senha", async () => {
    api.mockResolvedValue(json(409, { codigo: "conta-existente" }));
    await renderComApi(
      <ComDados dados={null}>
        <DadosScreen />
      </ComDados>,
      api,
    );

    await preencher("2023001", "ana@al.unieduk.com.br", "senha-boa-123");
    await apertar("Continuar");

    expect(
      await screen.findByText(
        "Esse RA já tem conta. Use Esqueci minha senha para entrar.",
      ),
    ).toBeTruthy();
    await fireEvent.press(
      screen.getByRole("link", { name: "Esqueci minha senha" }),
    );
    expect(mockRoteador.push).toHaveBeenCalledWith("/esqueci-senha");
  });

  it("par de RA e e-mail que não está na base tem a mensagem amigável", async () => {
    api.mockResolvedValue(json(404, { codigo: "cadastro-nao-encontrado" }));
    await renderComApi(
      <ComDados dados={null}>
        <DadosScreen />
      </ComDados>,
      api,
    );

    await preencher("2023001", "outra@al.unieduk.com.br", "senha-boa-123");
    await apertar("Continuar");

    expect(screen.getByRole("alert")).toHaveTextContent(
      "Não achamos esse RA com esse e-mail. Confira os dois.",
    );
  });
});

describe("passo 2: arroba", () => {
  beforeEach(() => jest.useFakeTimers());
  afterEach(() => jest.useRealTimers());

  async function renderArroba(dados: DadosDoCadastro | null = DADOS) {
    return renderComApi(
      <ComDados dados={dados}>
        <ArrobaScreen />
      </ComDados>,
      api,
    );
  }

  async function digitarRapido(texto: string) {
    // Uma tecla por vez, mais rápido que a espera da digitação.
    for (let i = 1; i <= texto.length; i++) {
      await fireEvent.changeText(
        screen.getByLabelText("Arroba"),
        texto.slice(0, i),
      );
      await act(async () => {
        jest.advanceTimersByTime(100);
      });
    }
  }

  it("sem o passo 1 em memória, volta ao começo", async () => {
    await renderArroba(null);

    expect(await screen.findByText("redireciona para /cadastro")).toBeTruthy();
  });

  it("a sugestão aparece pronta e livre, sem consultar a API", async () => {
    await renderArroba();
    await act(async () => {
      jest.advanceTimersByTime(ESPERA_DA_DIGITACAO_MS * 2);
    });

    expect(screen.getByLabelText("Arroba").props.value).toBe("anaSouza");
    expect(await screen.findByText("@anaSouza-CC")).toBeTruthy();
    expect(await screen.findByText("Está livre.")).toBeTruthy();
    expect(api).not.toHaveBeenCalled();
  });

  it("digitação rápida faz uma consulta só, com o que ficou no campo", async () => {
    api.mockResolvedValue(json(200, { disponivel: true }));
    await renderArroba();

    await digitarRapido("anaPaula");
    expect(api).not.toHaveBeenCalled();
    await act(async () => {
      jest.advanceTimersByTime(ESPERA_DA_DIGITACAO_MS);
    });
    await act(async () => {});

    expect(caminhos(api)).toEqual(["/api/v1/auth/cadastro/disponibilidade"]);
    expect(await api.mock.calls[0]![0].json()).toMatchObject({
      prefixo: "anaPaula",
    });
    expect(await screen.findByText("Está livre.")).toBeTruthy();
  });

  it("arroba fora da forma não gasta consulta e fica no campo", async () => {
    await renderArroba();

    await fireEvent.changeText(screen.getByLabelText("Arroba"), "ana-paula");
    await act(async () => {
      jest.advanceTimersByTime(ESPERA_DA_DIGITACAO_MS * 2);
    });

    expect(api).not.toHaveBeenCalled();
    expect(screen.getByLabelText("Arroba").props["aria-invalid"]).toBe(true);
    expect(screen.getByRole("button", { name: "Continuar" })).toBeDisabled();
  });

  it("arroba ocupado é recusado no campo", async () => {
    api.mockResolvedValue(json(200, { disponivel: false }));
    await renderArroba();

    await fireEvent.changeText(screen.getByLabelText("Arroba"), "ocupado");
    await act(async () => {
      jest.advanceTimersByTime(ESPERA_DA_DIGITACAO_MS);
    });
    await act(async () => {});

    expect(
      await screen.findByText("Esse arroba já tem dono. Tente outro."),
    ).toBeTruthy();
    expect(screen.getByRole("button", { name: "Continuar" })).toBeDisabled();
  });

  it("429 na consulta não trava: o cadastro confere de novo ao criar a conta", async () => {
    api.mockResolvedValue(json(429, { codigo: "limite-de-taxa" }));
    await renderArroba();

    await fireEvent.changeText(screen.getByLabelText("Arroba"), "anaPaula");
    await act(async () => {
      jest.advanceTimersByTime(ESPERA_DA_DIGITACAO_MS);
    });
    await act(async () => {});

    expect(
      await screen.findByText(
        "Não deu para conferir agora. Conferimos de novo ao criar a conta.",
      ),
    ).toBeTruthy();
    expect(screen.getByRole("button", { name: "Continuar" })).toBeEnabled();
  });

  it("nome em branco é apontado no campo; com nome, segue para o termo", async () => {
    await renderArroba();

    await apertar("Continuar");
    expect(
      await screen.findByText("O nome precisa ter de 1 a 60 caracteres."),
    ).toBeTruthy();
    expect(mockRoteador.push).not.toHaveBeenCalled();

    await fireEvent.changeText(
      screen.getByLabelText("Nome de exibição"),
      "Ana",
    );
    await apertar("Continuar");
    expect(mockRoteador.push).toHaveBeenCalledWith("/cadastro/termo");
  });
});

describe("passo 3: termo", () => {
  const COMPLETO = { ...DADOS, nomeDeExibicao: "Ana", prefixo: "anaPaula" };
  const TERMO = {
    versao: 1,
    texto:
      "Primeiro parágrafo.\n\n## Fotos\n\nFoto pública para quem tem o link.",
  };

  it("mostra o termo vigente com títulos e parágrafos", async () => {
    api.mockResolvedValue(json(200, TERMO));
    await renderComApi(
      <ComDados dados={COMPLETO}>
        <TermoScreen />
      </ComDados>,
      api,
    );

    expect(await screen.findByRole("heading", { name: "Fotos" })).toBeTruthy();
    expect(
      await screen.findByText("Foto pública para quem tem o link."),
    ).toBeTruthy();
    expect(await screen.findByText("3 de 3")).toBeTruthy();
  });

  it("aceitar cria a conta com tudo dos três passos e a versão lida, e abre a sessão", async () => {
    api
      .mockResolvedValueOnce(json(200, TERMO))
      .mockResolvedValueOnce(json(201, SESSAO));
    const { gerenciador } = await renderComApi(
      <ComDados dados={COMPLETO}>
        <TermoScreen />
      </ComDados>,
      api,
    );

    await apertar("Aceitar e criar conta");

    expect(caminhos(api)).toEqual([
      "/api/v1/termo-de-uso",
      "/api/v1/auth/cadastro",
    ]);
    expect(await api.mock.calls[1]![0].json()).toEqual({
      ra: "2023001",
      email: "ana.souza@al.unieduk.com.br",
      nomeDeExibicao: "Ana",
      prefixo: "anaPaula",
      senha: "senha-boa-123",
      versaoDoTermo: 1,
    });
    expect(gerenciador.estado).toBe("autenticado");
  });

  it("termo que mudou durante a leitura é trocado pelo novo, e o aceite é pedido de novo", async () => {
    api
      .mockResolvedValueOnce(json(200, TERMO))
      .mockResolvedValueOnce(json(409, { codigo: "termo-desatualizado" }))
      .mockResolvedValueOnce(json(200, { versao: 2, texto: "Texto novo." }))
      .mockResolvedValueOnce(json(201, SESSAO));
    await renderComApi(
      <ComDados dados={COMPLETO}>
        <TermoScreen />
      </ComDados>,
      api,
    );

    await apertar("Aceitar e criar conta");
    expect(
      await screen.findByText(
        "O termo de uso mudou. Leia a versão nova para continuar.",
      ),
    ).toBeTruthy();
    expect(await screen.findByText("Texto novo.")).toBeTruthy();

    await apertar("Aceitar e criar conta");
    expect(await api.mock.calls[3]![0].json()).toMatchObject({
      versaoDoTermo: 2,
    });
  });

  it("arroba tomado no último instante leva de volta para escolher outro", async () => {
    api
      .mockResolvedValueOnce(json(200, TERMO))
      .mockResolvedValueOnce(json(409, { codigo: "prefixo-indisponivel" }));
    await renderComApi(
      <ComDados dados={COMPLETO}>
        <TermoScreen />
      </ComDados>,
      api,
    );

    await apertar("Aceitar e criar conta");
    expect(
      await screen.findByText("Esse arroba já tem dono. Tente outro."),
    ).toBeTruthy();
    await apertar("Escolher outro arroba");

    expect(mockRoteador.back).toHaveBeenCalled();
  });
});
