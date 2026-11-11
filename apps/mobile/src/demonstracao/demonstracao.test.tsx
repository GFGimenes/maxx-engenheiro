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

// Da escolha inicial (passo 14) até o registro de um papel.
async function abrir(papel: "aluno" | "professor" | "coordenação") {
  await apertar(`Entrar como ${papel}`);
}

// Sem o registro aberto, abre o do aluno.
async function ateOTermo(nome = "Ana Paula") {
  await screen.findByRole("heading");
  if (!screen.queryByLabelText("Nome")) {
    await abrir("aluno");
  }
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

  await abrir("aluno");
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

  await abrir("aluno");
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
    { chave: "chave-1", nome: "Ana Paula", curso: "CC", papel: "aluno" },
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
        { chave: "chave-1", nome: "Ana Paula", curso: "CC", papel: "aluno" },
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
    { chave: "velha", nome: "Ana Paula", curso: "CC", papel: "aluno" },
    { chave: "boa", nome: "Bruno", curso: "ADM", papel: "aluno" },
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
    { chave: "boa", nome: "Bruno", curso: "ADM", papel: "aluno" },
  ]);
});

test("o que estiver guardado fora da forma vira lista vazia, nunca tela quebrada", () => {
  expect(analisarContas(null)).toEqual([]);
  expect(analisarContas("{não é json")).toEqual([]);
  expect(analisarContas('{"chave":"x"}')).toEqual([]);
  expect(
    analisarContas(
      '[{"chave":"a","nome":"Ana","curso":"CC"},{"chave":1},{"chave":"b","nome":"Bia","curso":"","papel":"diretor"}]',
    ),
  ).toEqual([{ chave: "a", nome: "Ana", curso: "CC", papel: "aluno" }]);
});

// Passo 14: os três papéis da demonstração.
describe("três papéis", () => {
  let professores: jest.Mock<Promise<Response>, [Request]>;
  let coordenacoes: jest.Mock<Promise<Response>, [Request]>;
  beforeEach(() => {
    professores = jest.fn(async (_pedido: Request) =>
      json(201, { chaveDoDispositivo: "chave-p", sessao: SESSAO }),
    );
    coordenacoes = jest.fn(async (_pedido: Request) =>
      json(201, { chaveDoDispositivo: "chave-c", sessao: SESSAO }),
    );
    const anterior = api.getMockImplementation()!;
    api.mockImplementation(async (pedido: Request) => {
      const caminho = new URL(pedido.url).pathname;
      if (caminho === "/api/v1/demo/professores") return professores(pedido);
      if (caminho === "/api/v1/demo/coordenacoes") return coordenacoes(pedido);
      return anterior(pedido);
    });
  });

  test("a escolha mostra as três entradas, cada uma com a linha de apoio", async () => {
    await renderComApi(
      <DemonstracaoScreen contasDoDispositivo={aparelho()} />,
      api,
    );

    expect(
      await screen.findByText(
        "Escolha como entrar, sem senha. As contas de teste são apagadas ao fim da avaliação do projeto.",
      ),
    ).toBeTruthy();
    for (const [botao, apoio] of [
      ["Entrar como aluno", "Posta, reivindica prêmios e aparece no ranking."],
      [
        "Entrar como professor",
        "Concede medalhas e decide comprovações das turmas de um curso.",
      ],
      // O Jest roda como nativo: no aparelho, a coordenação já sabe que a área abre no
      // navegador.
      [
        "Entrar como coordenação",
        "Cuida da faculdade inteira, pela área web. Abre no navegador.",
      ],
    ] as const) {
      expect(screen.getByRole("button", { name: botao })).toBeTruthy();
      expect(screen.getByText(apoio)).toBeTruthy();
    }
    expect(screen.queryByLabelText("Nome")).toBeNull();
  });

  test("o professor registra com nome e curso, sem foto, e a conta lembra o papel", async () => {
    const dispositivo = aparelho();
    const { gerenciador } = await renderComApi(
      <DemonstracaoScreen contasDoDispositivo={dispositivo} />,
      api,
    );

    await abrir("professor");
    expect(
      screen.getByRole("heading", { name: "Professor de teste" }),
    ).toBeTruthy();
    expect(
      screen.getByText("As turmas deste curso no semestre ficam com você."),
    ).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Escolher foto" })).toBeNull();
    await apertar("Continuar");
    expect(await screen.findByText("Escolha um curso.")).toBeTruthy();

    await ateOTermo(" Rita Lima ");
    await apertar("Aceitar e entrar");

    expect(await professores.mock.calls[0]![0].json()).toEqual({
      nome: "Rita Lima",
      curso: "CC",
      versaoDoTermo: 1,
    });
    expect(registro).not.toHaveBeenCalled();
    expect(dispositivo.guardadas()).toEqual([
      { chave: "chave-p", nome: "Rita Lima", curso: "CC", papel: "professor" },
    ]);
    expect(gerenciador.estado).toBe("autenticado");
  });

  test("a coordenação registra só com o nome, depois do aviso dos poderes", async () => {
    const dispositivo = aparelho();
    const { gerenciador } = await renderComApi(
      <DemonstracaoScreen contasDoDispositivo={dispositivo} />,
      api,
    );

    await abrir("coordenação");
    expect(
      screen.getByRole("heading", { name: "Coordenação de teste" }),
    ).toBeTruthy();
    expect(
      screen.getByText(
        "A coordenação tem todos os poderes da instituição sobre a base de teste, inclusive apagar as contas de teste.",
      ),
    ).toBeTruthy();
    expect(screen.queryByRole("radiogroup", { name: "Curso" })).toBeNull();
    await fireEvent.changeText(screen.getByLabelText("Nome"), "Caio Reis");
    await apertar("Continuar");
    await apertar("Aceitar e entrar");

    expect(await coordenacoes.mock.calls[0]![0].json()).toEqual({
      nome: "Caio Reis",
      versaoDoTermo: 1,
    });
    expect(dispositivo.guardadas()).toEqual([
      { chave: "chave-c", nome: "Caio Reis", curso: "", papel: "coordenacao" },
    ]);
    expect(gerenciador.estado).toBe("autenticado");
  });

  test("voltar do termo leva ao registro do mesmo papel, e voltar do registro à escolha", async () => {
    await renderComApi(
      <DemonstracaoScreen contasDoDispositivo={aparelho()} />,
      api,
    );

    await abrir("coordenação");
    await fireEvent.changeText(screen.getByLabelText("Nome"), "Caio Reis");
    await apertar("Continuar");
    expect(
      await screen.findByRole("heading", { name: "Termo de uso" }),
    ).toBeTruthy();
    await act(async () => {
      fireEvent.press(screen.getByRole("link", { name: "Voltar" }));
    });
    expect(
      screen.getByRole("heading", { name: "Coordenação de teste" }),
    ).toBeTruthy();
    await act(async () => {
      fireEvent.press(screen.getByRole("link", { name: "Voltar" }));
    });
    expect(
      screen.getByRole("button", { name: "Entrar como professor" }),
    ).toBeTruthy();
  });

  test("a lista do aparelho diz o papel de cada conta", async () => {
    entrada.mockResolvedValue(json(200, SESSAO));
    await renderComApi(
      <DemonstracaoScreen
        contasDoDispositivo={aparelho([
          { chave: "a", nome: "Ana", curso: "CC", papel: "aluno" },
          { chave: "p", nome: "Rita", curso: "ADM", papel: "professor" },
          { chave: "c", nome: "Caio", curso: "", papel: "coordenacao" },
        ])}
      />,
      api,
    );

    expect(
      await screen.findByRole("button", { name: "Entrar como Ana · CC" }),
    ).toBeTruthy();
    expect(
      screen.getByRole("button", {
        name: "Entrar como Rita · Professor de ADM",
      }),
    ).toBeTruthy();
    await apertar("Entrar como Caio · Coordenação");
    expect(await entrada.mock.calls[0]![0].json()).toEqual({
      chaveDoDispositivo: "c",
    });
  });
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

    await abrir("aluno");
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

    await abrir("aluno");
    await apertar("Escolher foto");
    await ateOTermo();
    await apertar("Aceitar e entrar");

    expect(gerenciador.estado).toBe("autenticado");
    expect(edicao).not.toHaveBeenCalled();
  });
});
