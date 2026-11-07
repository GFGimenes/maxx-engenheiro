import { act, fireEvent, screen } from "@testing-library/react-native";

import { caminhos, json, renderComApi } from "../test/renderComApi";
import { EsqueciSenhaScreen } from "./EsqueciSenhaScreen";
import { RedefinirSenhaScreen } from "./RedefinirSenhaScreen";

const mockRoteador = { push: jest.fn(), replace: jest.fn(), back: jest.fn() };
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  api = jest.fn();
});

async function apertar(nome: string) {
  await act(async () => {
    fireEvent.press(await screen.findByRole("button", { name: nome }));
  });
  await act(async () => {});
}

describe("esqueci minha senha", () => {
  it("pede o link pelo e-mail e diz o mesmo exista a conta ou não", async () => {
    api.mockResolvedValue(new Response(null, { status: 202 }));
    await renderComApi(<EsqueciSenhaScreen />, api);

    await fireEvent.changeText(
      screen.getByLabelText("E-mail institucional"),
      " renata@prof.unieduk.com.br ",
    );
    await apertar("Enviar link");

    expect(caminhos(api)).toEqual(["/api/v1/auth/recuperacao"]);
    expect(await api.mock.calls[0]![0].json()).toEqual({
      email: "renata@prof.unieduk.com.br",
    });
    expect(await screen.findByText("Confira o seu e-mail.")).toBeTruthy();
    expect(
      screen.getByText(
        "Se esse e-mail tiver conta no Maxx, o link chega em instantes. Ele vale por 30 minutos.",
      ),
    ).toBeTruthy();
  });

  it("e-mail vazio é apontado no campo, sem ir à API", async () => {
    await renderComApi(<EsqueciSenhaScreen />, api);

    await apertar("Enviar link");

    expect(
      await screen.findByText("Informe o e-mail institucional."),
    ).toBeTruthy();
    expect(api).not.toHaveBeenCalled();
  });

  it("muitos pedidos mostram a mensagem do limite", async () => {
    api.mockResolvedValue(json(429, { codigo: "limite-de-taxa" }));
    await renderComApi(<EsqueciSenhaScreen />, api);

    await fireEvent.changeText(
      screen.getByLabelText("E-mail institucional"),
      "ana@al.unieduk.com.br",
    );
    await apertar("Enviar link");

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Muitas tentativas seguidas. Espere um pouco e tente de novo.",
    );
  });
});

describe("senha nova", () => {
  async function salvar(senha: string) {
    await fireEvent.changeText(screen.getByLabelText("Senha nova"), senha);
    await apertar("Salvar senha");
  }

  it("salva com o token do link e leva ao login", async () => {
    api.mockResolvedValue(new Response(null, { status: 204 }));
    await renderComApi(<RedefinirSenhaScreen token="tok-123" />, api);

    await salvar("senha-nova-123");

    expect(caminhos(api)).toEqual(["/api/v1/auth/recuperacao/confirmar"]);
    expect(await api.mock.calls[0]![0].json()).toEqual({
      token: "tok-123",
      senha: "senha-nova-123",
    });
    expect(await screen.findByText("Senha nova salva.")).toBeTruthy();
    await apertar("Entrar");
    expect(mockRoteador.replace).toHaveBeenCalledWith("/login");
  });

  it("senha curta fica no campo, sem gastar o link", async () => {
    await renderComApi(<RedefinirSenhaScreen token="tok-123" />, api);

    await salvar("curta");

    expect(
      await screen.findByText("A senha precisa ter de 8 a 128 caracteres."),
    ).toBeTruthy();
    expect(api).not.toHaveBeenCalled();
  });

  it("senha recusada pela API fica no campo, e o formulário continua", async () => {
    api.mockResolvedValue(json(400, { codigo: "senha-fora-da-politica" }));
    await renderComApi(<RedefinirSenhaScreen token="tok-123" />, api);

    await salvar("12345678");

    expect(screen.getByLabelText("Senha nova").props["aria-invalid"]).toBe(
      true,
    );
    expect(screen.getByRole("button", { name: "Salvar senha" })).toBeTruthy();
  });

  it("link usado ou vencido oferece pedir outro", async () => {
    api.mockResolvedValue(json(400, { codigo: "recuperacao-invalida" }));
    await renderComApi(<RedefinirSenhaScreen token="tok-velho" />, api);

    await salvar("senha-nova-123");

    expect(await screen.findByText("Esse link não vale mais.")).toBeTruthy();
    await apertar("Pedir link novo");
    expect(mockRoteador.replace).toHaveBeenCalledWith("/esqueci-senha");
  });

  it("sem token, nem mostra o formulário", async () => {
    await renderComApi(<RedefinirSenhaScreen token={null} />, api);

    expect(await screen.findByText("Esse link está incompleto.")).toBeTruthy();
    expect(screen.queryByLabelText("Senha nova")).toBeNull();
  });

  it("com uma sessão aberta no aparelho, a senha nova a encerra", async () => {
    // Confirmação e logout respondem 204.
    api.mockImplementation(async () => new Response(null, { status: 204 }));
    const { gerenciador } = await renderComApi(
      <RedefinirSenhaScreen token="tok-123" />,
      api,
    );
    await act(() =>
      gerenciador.iniciar({
        accessToken: "a1",
        accessTokenExpiraEm: "2999-01-01T00:00:00Z",
        refreshToken: "r1",
        refreshTokenExpiraEm: "2999-01-01T00:00:00Z",
      }),
    );

    await salvar("senha-nova-123");

    expect(caminhos(api)).toEqual([
      "/api/v1/auth/recuperacao/confirmar",
      "/api/v1/auth/logout",
    ]);
    expect(gerenciador.estado).toBe("anonimo");
  });
});
