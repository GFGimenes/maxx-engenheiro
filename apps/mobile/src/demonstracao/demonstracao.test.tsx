import { act, fireEvent, screen } from "@testing-library/react-native";

import { caminhos, json, renderComApi } from "../test/renderComApi";
import { analisarContas, type ContaDeDemonstracao } from "./contas";
import { DemonstracaoScreen } from "./DemonstracaoScreen";

const mockRoteador = { push: jest.fn(), replace: jest.fn(), back: jest.fn() };
jest.mock("expo-router", () => ({
  useRouter: () => mockRoteador,
  Redirect: ({ href }: { href: string }) => {
    const { Text } = jest.requireActual("react-native");
    return <Text>{`redireciona para ${href}`}</Text>;
  },
}));
jest.mock("expo-image-picker", () => ({
  launchImageLibraryAsync: jest.fn(async () => ({
    canceled: false,
    assets: [{ uri: "file:///galeria.jpg", width: 800, height: 800 }],
  })),
}));
jest.mock("expo-image-manipulator", () => ({
  SaveFormat: { JPEG: "jpeg" },
  ImageManipulator: {
    manipulate: () => {
      const contexto = {
        crop: () => contexto,
        resize: () => contexto,
        renderAsync: async () => ({
          saveAsync: async () => ({
            uri: "file:///derivada.jpg",
            width: 0,
            height: 0,
          }),
        }),
      };
      return contexto;
    },
  },
}));

const CURSOS = [
  { sigla: "CC", nome: "Ciência da Computação" },
  { sigla: "ADM", nome: "Administração" },
];
const TERMO = { versao: 1, texto: "## Demonstração\n\nContas de teste." };
const SESSAO = {
  accessToken: "a1",
  accessTokenExpiraEm: "2999-01-01T00:00:00Z",
  refreshToken: "r1",
  refreshTokenExpiraEm: "2999-01-01T00:00:00Z",
};

// A API simulada por rota: cada teste diz o que o registro e a volta respondem.
let registro: jest.Mock<Promise<Response>, [Request]>;
let entrada: jest.Mock<Promise<Response>, [Request]>;
let demo: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;
let uploads: jest.Mock<Promise<Response>, [Request]>;
let edicao: jest.Mock<Promise<Response>, [Request]>;

function aparelho(contas: ContaDeDemonstracao[] = []) {
  let guardadas = contas;
  return {
    lerContas: jest.fn(async () => guardadas),
    gravarContas: jest.fn(async (novas: ContaDeDemonstracao[]) => {
      guardadas = novas;
    }),
    guardadas: () => guardadas,
  };
}

beforeEach(() => {
  jest.clearAllMocks();
  registro = jest.fn();
  entrada = jest.fn();
  uploads = jest.fn(async (_pedido: Request) =>
    json(201, {
      id: "upload-1",
      expiraEm: "2999-01-01T00:00:00Z",
      feed: {
        url: "http://bucket.test/f.jpg",
        tipoDeConteudo: "image/jpeg",
        tamanho: 100,
      },
      grade: {
        url: "http://bucket.test/g.jpg",
        tipoDeConteudo: "image/jpeg",
        tamanho: 100,
      },
    }),
  );
  edicao = jest.fn(async (_pedido: Request) => json(200, {}));
  demo = jest.fn(async (_pedido: Request) => json(200, { cursos: CURSOS }));
  api = jest.fn(async (pedido: Request) => {
    const caminho = new URL(pedido.url).pathname;
    if (caminho === "/api/v1/demo") return demo(pedido);
    if (caminho === "/api/v1/termo-de-uso") return json(200, TERMO);
    if (caminho === "/api/v1/demo/alunos") return registro(pedido);
    if (caminho === "/api/v1/demo/entrar") return entrada(pedido);
    if (caminho === "/api/v1/uploads") return uploads(pedido);
    if (caminho === "/api/v1/eu/perfil") return edicao(pedido);
    return new Response(null, { status: 404 });
  });
});

async function apertar(nome: string) {
  await act(async () => {
    fireEvent.press(await screen.findByRole("button", { name: nome }));
  });
  await act(async () => {});
}

async function ateOTermo(nome = "Ana Paula") {
  await fireEvent.changeText(await screen.findByLabelText("Nome"), nome);
  await fireEvent.press(
    screen.getByRole("radio", { name: "Ciência da Computação" }),
  );
  await apertar("Continuar");
}

test("sem a flag, a API responde 404 e a tela volta ao login", async () => {
  demo.mockResolvedValue(new Response(null, { status: 404 }));
  await renderComApi(
    <DemonstracaoScreen contasDoDispositivo={aparelho()} />,
    api,
  );

  expect(await screen.findByText("redireciona para /login")).toBeTruthy();
});

test("mostra os cursos da API como escolha única", async () => {
  await renderComApi(
    <DemonstracaoScreen contasDoDispositivo={aparelho()} />,
    api,
  );

  await screen.findByRole("radio", { name: "Administração" });
  await fireEvent.press(screen.getByRole("radio", { name: "Administração" }));
  await fireEvent.press(
    screen.getByRole("radio", { name: "Ciência da Computação" }),
  );

  expect(
    screen.getByRole("radio", { name: "Ciência da Computação", checked: true }),
  ).toBeTruthy();
  expect(
    screen.getByRole("radio", { name: "Administração", checked: false }),
  ).toBeTruthy();
});

test("nome e curso faltando são apontados antes do termo", async () => {
  await renderComApi(
    <DemonstracaoScreen contasDoDispositivo={aparelho()} />,
    api,
  );

  await apertar("Continuar");

  expect(
    await screen.findByText("O nome precisa ter de 1 a 60 caracteres."),
  ).toBeTruthy();
  expect(screen.getByText("Escolha um curso.")).toBeTruthy();
  expect(screen.queryByText("Termo de uso")).toBeNull();
});

test("registra com nome, curso e a versão do termo, guarda a chave e entra", async () => {
  registro.mockResolvedValue(
    json(201, { chaveDoDispositivo: "chave-1", sessao: SESSAO }),
  );
  const dispositivo = aparelho();
  const { gerenciador } = await renderComApi(
    <DemonstracaoScreen contasDoDispositivo={dispositivo} />,
    api,
  );

  await ateOTermo(" Ana Paula ");
  expect(
    await screen.findByRole("heading", { name: "Demonstração" }),
  ).toBeTruthy();
  await apertar("Aceitar e entrar");

  expect(await registro.mock.calls[0]![0].json()).toEqual({
    nome: "Ana Paula",
    curso: "CC",
    versaoDoTermo: 1,
  });
  expect(dispositivo.guardadas()).toEqual([
    { chave: "chave-1", nome: "Ana Paula", curso: "CC" },
  ]);
  expect(gerenciador.estado).toBe("autenticado");
});

test("registro simultâneo mostra a mensagem amigável e deixa tentar de novo", async () => {
  registro
    .mockResolvedValueOnce(json(409, { codigo: "registro-simultaneo" }))
    .mockResolvedValueOnce(
      json(201, { chaveDoDispositivo: "chave-2", sessao: SESSAO }),
    );
  const { gerenciador } = await renderComApi(
    <DemonstracaoScreen contasDoDispositivo={aparelho()} />,
    api,
  );

  await ateOTermo();
  await apertar("Aceitar e entrar");

  expect(
    await screen.findByText(
      "Muita gente entrou com esse nome agora. Tente de novo em instantes.",
    ),
  ).toBeTruthy();
  expect(gerenciador.estado).not.toBe("autenticado");

  await apertar("Tentar de novo");
  expect(registro).toHaveBeenCalledTimes(2);
  expect(gerenciador.estado).toBe("autenticado");
});

test("a conta lembrada no aparelho entra sem senha, pela chave", async () => {
  entrada.mockResolvedValue(json(200, SESSAO));
  const { gerenciador } = await renderComApi(
    <DemonstracaoScreen
      contasDoDispositivo={aparelho([
        { chave: "chave-1", nome: "Ana Paula", curso: "CC" },
      ])}
    />,
    api,
  );

  expect(await screen.findByText("Neste aparelho")).toBeTruthy();
  await apertar("Entrar como Ana Paula · CC");

  expect(await entrada.mock.calls[0]![0].json()).toEqual({
    chaveDoDispositivo: "chave-1",
  });
  expect(gerenciador.estado).toBe("autenticado");
  expect(caminhos(api)).not.toContain("/api/v1/demo/alunos");
});

test("chave que não abre mais conta some da lista, com a mensagem", async () => {
  entrada.mockResolvedValue(json(401, { codigo: "chave-invalida" }));
  const dispositivo = aparelho([
    { chave: "velha", nome: "Ana Paula", curso: "CC" },
    { chave: "boa", nome: "Bruno", curso: "ADM" },
  ]);
  await renderComApi(
    <DemonstracaoScreen contasDoDispositivo={dispositivo} />,
    api,
  );

  await apertar("Entrar como Ana Paula · CC");

  expect(
    await screen.findByText("Essa conta de demonstração não existe mais."),
  ).toBeTruthy();
  expect(
    screen.queryByRole("button", { name: "Entrar como Ana Paula · CC" }),
  ).toBeNull();
  expect(dispositivo.guardadas()).toEqual([
    { chave: "boa", nome: "Bruno", curso: "ADM" },
  ]);
});

test("o que estiver guardado fora da forma vira lista vazia, nunca tela quebrada", () => {
  expect(analisarContas(null)).toEqual([]);
  expect(analisarContas("{não é json")).toEqual([]);
  expect(analisarContas('{"chave":"x"}')).toEqual([]);
  expect(
    analisarContas('[{"chave":"a","nome":"Ana","curso":"CC"},{"chave":1}]'),
  ).toEqual([{ chave: "a", nome: "Ana", curso: "CC" }]);
});

describe("foto opcional do registro", () => {
  let rede: jest.SpyInstance;
  beforeEach(() => {
    rede = jest
      .spyOn(global, "fetch")
      .mockImplementation(async (entrada: RequestInfo | URL) =>
        String(entrada).startsWith("file:///")
          ? new Response(new Blob([new Uint8Array(100)]))
          : new Response(null, { status: 200 }),
      );
  });
  afterEach(() => rede.mockRestore());

  test("a foto escolhida sobe como foto de perfil depois que a conta nasce", async () => {
    registro.mockResolvedValue(
      json(201, { chaveDoDispositivo: "chave-1", sessao: SESSAO }),
    );
    const { gerenciador } = await renderComApi(
      <DemonstracaoScreen contasDoDispositivo={aparelho()} />,
      api,
    );

    await apertar("Escolher foto");
    expect(
      await screen.findByRole("button", { name: "Trocar foto" }),
    ).toBeTruthy();
    await ateOTermo();
    await apertar("Aceitar e entrar");

    const ordem = caminhos(api).filter((caminho) =>
      ["/api/v1/demo/alunos", "/api/v1/uploads", "/api/v1/eu/perfil"].includes(
        caminho,
      ),
    );
    expect(ordem).toEqual([
      "/api/v1/demo/alunos",
      "/api/v1/uploads",
      "/api/v1/eu/perfil",
    ]);
    expect((await uploads.mock.calls[0]![0].clone().json()).finalidade).toBe(
      "fotoDePerfil",
    );
    expect((await edicao.mock.calls[0]![0].clone().json()).fotoUploadId).toBe(
      "upload-1",
    );
    expect(gerenciador.estado).toBe("autenticado");
  });

  test("se a foto não sobe, a pessoa entra do mesmo jeito", async () => {
    registro.mockResolvedValue(
      json(201, { chaveDoDispositivo: "chave-1", sessao: SESSAO }),
    );
    uploads.mockResolvedValue(json(429, { codigo: "limite-de-taxa" }));
    const { gerenciador } = await renderComApi(
      <DemonstracaoScreen contasDoDispositivo={aparelho()} />,
      api,
    );

    await apertar("Escolher foto");
    await ateOTermo();
    await apertar("Aceitar e entrar");

    expect(gerenciador.estado).toBe("autenticado");
    expect(edicao).not.toHaveBeenCalled();
  });
});
