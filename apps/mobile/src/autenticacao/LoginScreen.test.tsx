import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen } from "@testing-library/react-native";
import { SafeAreaProvider } from "react-native-safe-area-context";

import { ApiClientProvider, createApiClient } from "../api/ApiClientProvider";
import { GerenciadorDeSessao } from "../sessao/GerenciadorDeSessao";
import { SessaoProvider } from "../sessao/SessaoProvider";
import { ThemeProvider } from "../theme/ThemeProvider";
import { LoginScreen } from "./LoginScreen";

const mockRoteador = { push: jest.fn(), replace: jest.fn() };
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

const BASE = "http://maxx.test";
const sessao = {
  accessToken: "a1",
  accessTokenExpiraEm: "2999-01-01T00:00:00Z",
  refreshToken: "r1",
  refreshTokenExpiraEm: "2999-01-01T00:00:00Z",
};

function json(status: number, corpo: unknown) {
  return new Response(JSON.stringify(corpo), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

// A API simulada: o login responde o que cada teste mandar; a demonstração, 404 (flag
// desligada), salvo quando o teste a liga.
let login: jest.Mock<Promise<Response>, [Request]>;
let demo: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;
let gerenciador: GerenciadorDeSessao;

beforeEach(() => {
  jest.clearAllMocks();
  login = jest.fn();
  demo = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 404 }),
  );
  api = jest.fn((pedido: Request) =>
    new URL(pedido.url).pathname === "/api/v1/demo"
      ? demo(pedido)
      : login(pedido),
  );
  gerenciador = new GerenciadorDeSessao({
    baseUrl: BASE,
    web: false,
    armazenamento: {
      ler: async () => null,
      gravar: async () => {},
      apagar: async () => {},
    },
    trava: (renovacao) => renovacao(),
    fetch: api,
  });
});

async function renderizar() {
  await render(
    <SafeAreaProvider
      initialMetrics={{
        insets: { top: 0, bottom: 0, left: 0, right: 0 },
        frame: { x: 0, y: 0, width: 390, height: 844 },
      }}
    >
      <QueryClientProvider client={new QueryClient()}>
        <SessaoProvider gerenciador={gerenciador}>
          <ApiClientProvider
            client={createApiClient(BASE, { fetch: gerenciador.fetch })}
          >
            <ThemeProvider>
              <LoginScreen />
            </ThemeProvider>
          </ApiClientProvider>
        </SessaoProvider>
      </QueryClientProvider>
    </SafeAreaProvider>,
  );
  await act(async () => {});
}

async function preencherEEntrar(identificador: string, senha: string) {
  await fireEvent.changeText(
    screen.getByLabelText("RA ou e-mail"),
    identificador,
  );
  await fireEvent.changeText(screen.getByLabelText("Senha"), senha);
  await act(async () => {
    fireEvent.press(screen.getByRole("button", { name: "Entrar" }));
  });
  await act(async () => {});
}

test("tem o que a prancheta tem, com o texto do primeiro acesso sem link", async () => {
  await renderizar();

  expect(
    screen.getByRole("radio", { name: "Entrar", checked: true }),
  ).toBeTruthy();
  expect(screen.getByRole("radio", { name: "Cadastrar" })).toBeTruthy();
  expect(
    screen.getByRole("link", { name: "Esqueci minha senha" }),
  ).toBeTruthy();
  expect(
    screen.getByText(
      "Primeiro acesso? Aluno cria a conta em Cadastrar. Professor define a senha em Esqueci minha senha.",
    ),
  ).toBeTruthy();
  expect(screen.queryByText(/Portal UniMax/)).toBeNull();
});

test("campos vazios são apontados no próprio campo, sem ir à API", async () => {
  await renderizar();

  await preencherEEntrar("  ", "");

  expect(screen.getByText("Informe o RA ou o e-mail.")).toBeTruthy();
  expect(screen.getByText("Informe a senha.")).toBeTruthy();
  expect(login).not.toHaveBeenCalled();
});

test("entra com o identificador sem espaços e abre a sessão", async () => {
  login.mockResolvedValue(json(200, sessao));
  await renderizar();

  await preencherEEntrar(" 2023001 ", "senha-boa-123");

  const pedido = login.mock.calls[0]![0];
  expect(new URL(pedido.url).pathname).toBe("/api/v1/auth/login");
  expect(await pedido.json()).toEqual({
    identificador: "2023001",
    senha: "senha-boa-123",
  });
  expect(gerenciador.estado).toBe("autenticado");
});

test("credencial errada aparece no campo da senha, com a mensagem amigável", async () => {
  login.mockResolvedValue(json(401, { codigo: "credenciais-invalidas" }));
  await renderizar();

  await preencherEEntrar("2023001", "senha-errada");

  expect(screen.getByLabelText("Senha").props["aria-invalid"]).toBe(true);
  expect(screen.getByText("RA, e-mail ou senha não conferem.")).toBeTruthy();
  expect(gerenciador.estado).not.toBe("autenticado");
});

test("muitas tentativas aparecem abaixo do botão", async () => {
  login.mockResolvedValue(json(429, { codigo: "limite-de-taxa" }));
  await renderizar();

  await preencherEEntrar("2023001", "senha-boa-123");

  expect(screen.getByRole("alert")).toHaveTextContent(
    "Muitas tentativas seguidas. Espere um pouco e tente de novo.",
  );
  expect(screen.getByLabelText("Senha").props["aria-invalid"]).toBe(false);
});

test("sem rede, diz que está sem conexão em vez de travar", async () => {
  login.mockRejectedValue(new TypeError("Network request failed"));
  await renderizar();

  await preencherEEntrar("2023001", "senha-boa-123");

  expect(
    screen.getByText("Sem conexão. Confira a internet e tente de novo."),
  ).toBeTruthy();
  expect(screen.getByRole("button", { name: "Entrar" })).toBeEnabled();
});

test("Cadastrar e Esqueci minha senha levam às rotas deles", async () => {
  await renderizar();

  await fireEvent.press(screen.getByRole("radio", { name: "Cadastrar" }));
  await fireEvent.press(
    screen.getByRole("link", { name: "Esqueci minha senha" }),
  );

  expect(mockRoteador.replace).toHaveBeenCalledWith("/cadastro");
  expect(mockRoteador.push).toHaveBeenCalledWith("/esqueci-senha");
});

test("com a demonstração desligada, o botão dela não aparece", async () => {
  await renderizar();

  expect(demo).toHaveBeenCalled();
  expect(
    screen.queryByRole("button", { name: "Entrar na demonstração" }),
  ).toBeNull();
});

test("com a demonstração ligada, o botão aparece e leva a ela", async () => {
  demo.mockResolvedValue(
    json(200, { cursos: [{ sigla: "CC", nome: "Ciência da Computação" }] }),
  );
  await renderizar();

  await fireEvent.press(
    await screen.findByRole("button", { name: "Entrar na demonstração" }),
  );

  expect(mockRoteador.push).toHaveBeenCalledWith("/demonstracao");
});
