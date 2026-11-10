import { act, fireEvent, screen } from "@testing-library/react-native";
import { launchImageLibraryAsync } from "expo-image-picker";

import { NovoPostScreen } from "../posts/NovoPostScreen";
import { json, renderComApi } from "../test/renderComApi";
import { eventoDaEscolha, ESCOLHA_VAZIA } from "./EscolhaDaReivindicacao";
import { lerData, periodo } from "./textos";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  dismissTo: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

jest.mock("expo-image-picker", () => ({
  launchImageLibraryAsync: jest.fn(),
}));

jest.mock("expo-image-manipulator", () => {
  let gerada = 0;
  return {
    SaveFormat: { JPEG: "jpeg" },
    ImageManipulator: {
      manipulate: () => {
        const contexto = {
          crop: () => contexto,
          resize: () => contexto,
          renderAsync: async () => ({
            saveAsync: async () => ({
              uri: `file:///derivada-${++gerada}.jpg`,
              width: 0,
              height: 0,
            }),
          }),
        };
        return contexto;
      },
    },
  };
});

const EU_ALUNA = {
  id: "0193a000-0000-7000-8000-0000000000b1",
  papeis: ["aluno"],
  arroba: "@anaSouza-CC",
  nomeDeExibicao: "Ana Souza",
  foto: null,
};

const SEMANA = "0193a000-0000-7000-8000-0000000000a1";
const BANCO_DE_DADOS = "0193a000-0000-7000-8000-0000000000c1";
const UPLOAD = "0193a000-0000-7000-8000-0000000000e1";

const participacao = (codigo: string, nome: string, exigeMateria = false) => ({
  codigo,
  tipo: "premioParticipacao",
  nome,
  criterio: `Critério de ${nome}.`,
  tier: "bronze",
  pontos: 30,
  familia: null,
  ordemNaFamilia: null,
  raridade: null,
  obtidos: [],
  exigeMateria,
});

const PREMIOS = {
  pontos: 0,
  familias: [],
  itens: [
    participacao("P06", "Semana acadêmica"),
    participacao("P17", "Apresentação de trabalho em sala", true),
    {
      ...participacao("M01", "Nota 10 em prova"),
      tipo: "medalha",
    },
  ],
};

const ASSINADO = {
  id: UPLOAD,
  expiraEm: "2026-11-10T13:15:00Z",
  feed: {
    url: "http://bucket.test/maxx-fotos/X/feed.jpg?assinado",
    tipoDeConteudo: "image/jpeg",
    tamanho: 300,
  },
  grade: {
    url: "http://bucket.test/maxx-fotos/X/grade.jpg?assinado",
    tipoDeConteudo: "image/jpeg",
    tamanho: 40,
  },
};

let reivindicacoes: jest.Mock<Promise<Response>, [Request]>;
let eventos: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;
let rede: jest.SpyInstance;

beforeEach(() => {
  jest.clearAllMocks();
  reivindicacoes = jest.fn(async (_pedido: Request) =>
    json(201, { id: "novo" }),
  );
  eventos = jest.fn(async (_pedido: Request) =>
    json(200, [
      {
        id: SEMANA,
        nome: "Semana Acadêmica de Computação",
        inicio: "2026-09-14",
        fim: "2026-09-18",
        itens: ["P06"],
      },
    ]),
  );
  api = jest.fn(async (pedido: Request) => {
    const caminho = new URL(pedido.url).pathname;
    if (caminho === "/api/v1/eu") return json(200, EU_ALUNA);
    if (caminho === "/api/v1/eu/premios") return json(200, PREMIOS);
    if (caminho === "/api/v1/eventos") return eventos(pedido);
    if (caminho === "/api/v1/eu/materias")
      return json(200, [{ id: BANCO_DE_DADOS, nome: "Banco de Dados" }]);
    if (caminho === "/api/v1/uploads") return json(201, ASSINADO);
    if (caminho === "/api/v1/reivindicacoes") return reivindicacoes(pedido);
    return new Response(null, { status: 404 });
  });

  rede = jest
    .spyOn(global, "fetch")
    .mockImplementation(async (entrada: RequestInfo | URL) => {
      const url = String(entrada);
      if (url.startsWith("file:///derivada-")) {
        const numero = Number(url.match(/derivada-(\d+)/)?.[1]);
        return new Response(
          new Blob([new Uint8Array(numero % 2 === 1 ? 300 : 40)]),
        );
      }
      return new Response(null, { status: 200 });
    });
  jest.mocked(launchImageLibraryAsync).mockResolvedValue({
    canceled: false,
    assets: [{ uri: "file:///galeria.jpg", width: 4000, height: 3000 }],
  } as never);
});

afterEach(() => rede.mockRestore());

async function apertar(nome: string | RegExp, papel = "button") {
  await act(async () => {
    fireEvent.press(await screen.findByRole(papel, { name: nome }));
  });
}

async function escrever(rotulo: string, texto: string) {
  await act(async () => {
    fireEvent.changeText(await screen.findByLabelText(rotulo), texto);
  });
}

async function noModoDeReivindicacao() {
  await renderComApi(<NovoPostScreen />, api);
  await apertar("Reivindicar prêmio", "radio");
}

const corpo = async () =>
  (reivindicacoes.mock.calls[0]?.[0] as Request).clone().json();

describe("datas do evento", () => {
  it("lê dd/mm/aaaa de uma data que existe e recusa o resto", () => {
    expect(lerData("5/10/2026")).toBe("2026-10-05");
    expect(lerData(" 14/09/2026 ")).toBe("2026-09-14");
    expect(lerData("31/02/2026")).toBeNull();
    expect(lerData("2026-09-14")).toBeNull();
    expect(lerData("")).toBeNull();
  });

  it("mostra o dia só ou o período", () => {
    expect(periodo("2026-10-05", "2026-10-05")).toBe("05/10");
    expect(periodo("2026-09-14", "2026-09-18")).toBe("14/09 a 18/09");
  });

  it("o evento digitado sem fim é de um dia só", () => {
    expect(
      eventoDaEscolha(
        {
          ...ESCOLHA_VAZIA,
          item: "P01",
          eventoId: "outro",
          nome: " Palestra ",
          inicio: "05/10/2026",
        },
        false,
      ),
    ).toEqual({
      tipo: "digitado",
      nome: "Palestra",
      inicio: "2026-10-05",
      fim: "2026-10-05",
    });
  });
});

describe("novo post no modo de reivindicação", () => {
  it("oferece só os prêmios de participação e pede foto antes de Reivindicar", async () => {
    await noModoDeReivindicacao();

    expect(
      await screen.findByRole("radio", { name: /Semana acadêmica/ }),
    ).toBeOnTheScreen();
    expect(
      screen.queryByRole("radio", { name: /Nota 10 em prova/ }),
    ).toBeNull();

    await apertar(/Semana acadêmica/, "radio");
    await apertar(/Semana Acadêmica de Computação/, "radio");
    expect(screen.getByRole("button", { name: "Reivindicar" })).toBeDisabled();

    await apertar("Escolher foto");
    expect(await screen.findByTestId("previa-da-foto")).toBeOnTheScreen();
    expect(screen.getByRole("button", { name: "Reivindicar" })).toBeEnabled();
  });

  it("reivindica com o evento da lista, envia a foto e volta ao início", async () => {
    await noModoDeReivindicacao();

    await apertar(/Semana acadêmica/, "radio");
    await apertar(/Semana Acadêmica de Computação/, "radio");
    await apertar("Escolher foto");
    await escrever("Legenda", "Na semana acadêmica.");
    await apertar("Reivindicar");

    expect(new URL(eventos.mock.calls[0]![0].url).search).toBe("?item=P06");
    expect(await corpo()).toEqual({
      item: "P06",
      eventoId: SEMANA,
      eventoDigitado: null,
      materiaId: null,
      uploadId: UPLOAD,
      legenda: "Na semana acadêmica.",
    });
    expect(mockRoteador.dismissTo).toHaveBeenCalledWith("/");
  });

  it("com Outro evento, manda nome e datas e avisa da data fora do formato", async () => {
    await noModoDeReivindicacao();

    await apertar(/Semana acadêmica/, "radio");
    await apertar("Outro evento", "radio");
    await apertar("Escolher foto");
    await escrever("Nome do evento", "Semana de Inovação");
    await escrever("Data de início", "2026-10-05");

    expect(
      await screen.findByText("Use o formato dd/mm/aaaa."),
    ).toBeOnTheScreen();
    expect(screen.getByRole("button", { name: "Reivindicar" })).toBeDisabled();

    await escrever("Data de início", "05/10/2026");
    await escrever("Data de fim, se durou mais de um dia", "09/10/2026");
    await apertar("Reivindicar");

    expect(await corpo()).toMatchObject({
      item: "P06",
      eventoId: null,
      eventoDigitado: {
        nome: "Semana de Inovação",
        inicio: "2026-10-05",
        fim: "2026-10-09",
      },
      materiaId: null,
    });
  });

  it("na apresentação em sala, não lista eventos e exige a matéria", async () => {
    await noModoDeReivindicacao();

    await apertar(/Apresentação de trabalho em sala/, "radio");
    await apertar("Escolher foto");
    await escrever("O que você apresentou", "Seminário de normalização");
    await escrever("Data de início", "03/11/2026");

    expect(screen.queryByRole("radio", { name: "Outro evento" })).toBeNull();
    expect(eventos).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Reivindicar" })).toBeDisabled();

    await apertar("Banco de Dados", "radio");
    await apertar("Reivindicar");

    expect(await corpo()).toMatchObject({
      item: "P17",
      eventoDigitado: {
        nome: "Seminário de normalização",
        inicio: "2026-11-03",
        fim: "2026-11-03",
      },
      materiaId: BANCO_DE_DADOS,
    });
  });

  it("recusa da API vira mensagem e a tela continua", async () => {
    reivindicacoes.mockResolvedValue(
      json(409, { status: 409, codigo: "premio-ja-reivindicado" }),
    );
    await noModoDeReivindicacao();

    await apertar(/Semana acadêmica/, "radio");
    await apertar(/Semana Acadêmica de Computação/, "radio");
    await apertar("Escolher foto");
    await apertar("Reivindicar");

    expect(
      await screen.findByText(
        "Você já reivindicou esse prêmio para esse evento.",
      ),
    ).toBeOnTheScreen();
    expect(mockRoteador.dismissTo).not.toHaveBeenCalled();
  });

  it("escolhido o prêmio, a lista recolhe para ele, e Trocar prêmio a devolve", async () => {
    await noModoDeReivindicacao();

    await apertar(/Semana acadêmica/, "radio");
    expect(
      screen.queryByRole("radio", { name: /Apresentação de trabalho em sala/ }),
    ).toBeNull();

    await apertar("Trocar prêmio", "link");
    expect(
      await screen.findByRole("radio", {
        name: /Apresentação de trabalho em sala/,
      }),
    ).toBeOnTheScreen();
    expect(screen.queryByRole("heading", { name: "Evento" })).toBeNull();
  });

  // Achado do ensaio (passo 13): no iPhone, tocar no prêmio escolhido não reabria a lista, e
  // o "Trocar prêmio" passou despercebido. Tocar nele de novo também devolve a lista.
  it("tocar de novo no prêmio escolhido devolve a lista para trocar", async () => {
    await noModoDeReivindicacao();

    await apertar(/Semana acadêmica/, "radio");
    expect(
      screen.queryByRole("radio", { name: /Apresentação de trabalho em sala/ }),
    ).toBeNull();

    await apertar(/Semana acadêmica/, "radio");
    expect(
      await screen.findByRole("radio", {
        name: /Apresentação de trabalho em sala/,
      }),
    ).toBeOnTheScreen();
    expect(
      screen.getByRole("radio", { name: /Semana acadêmica/ }),
    ).not.toBeChecked();
    expect(screen.queryByRole("heading", { name: "Evento" })).toBeNull();
  });

  it("voltar ao modo Post mostra as medalhas e o botão Publicar", async () => {
    await noModoDeReivindicacao();
    await apertar("Post", "radio");

    expect(
      await screen.findByRole("button", { name: "Publicar" }),
    ).toBeOnTheScreen();
    expect(screen.queryByText("O prêmio que a foto comprova.")).toBeNull();
  });
});
