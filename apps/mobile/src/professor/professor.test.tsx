import { act, fireEvent, screen } from "@testing-library/react-native";

import { InicioProvisorio } from "../inicio/InicioProvisorio";
import { caminhos, json, renderComApi } from "../test/renderComApi";
import { TurmaScreen } from "./TurmaScreen";
import { TurmasScreen } from "./TurmasScreen";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

const TURMA = "0193a000-0000-7000-8000-0000000000aa";
const ANA = "0193a000-0000-7000-8000-0000000000b1";
const TURMAS = [
  {
    id: TURMA,
    materia: "Banco de Dados",
    curso: "CC",
    semestre: "2026/2",
    alunos: 1,
  },
];
const ALUNOS = [
  {
    id: ANA,
    nomeDeExibicao: "Ana Souza",
    arroba: "@anaSouza-CC",
    medalhas: [],
  },
];
const MEDALHAS = [
  {
    codigo: "M01",
    nome: "Nota 10 em prova",
    descricao: "Tirar 10 em uma prova da matéria.",
    tier: "bronze",
    pontos: 30,
    base: "nota",
    periodo: "bimestre",
    limitePorPeriodo: 2,
  },
  {
    codigo: "M10",
    nome: "Média final 10 na matéria",
    descricao: "Fechar a matéria com média final 10.",
    tier: "prata",
    pontos: 50,
    base: "nota",
    periodo: "semestre",
    limitePorPeriodo: 1,
  },
];

let turmas: jest.Mock<Promise<Response>, [Request]>;
let alunos: jest.Mock<Promise<Response>, [Request]>;
let concessao: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  turmas = jest.fn(async (_pedido: Request) => json(200, TURMAS));
  alunos = jest.fn(async (_pedido: Request) => json(200, ALUNOS));
  concessao = jest.fn(async (_pedido: Request) =>
    json(201, {
      id: "0193a000-0000-7000-8000-0000000000c1",
      item: "M01",
      tier: "bronze",
      pontos: 30,
      ocorrencia: 1,
      concedidaEm: "2026-11-10T13:00:00Z",
    }),
  );
  api = jest.fn(async (pedido: Request) => {
    const caminho = new URL(pedido.url).pathname;
    if (caminho === "/api/v1/professor/turmas") return turmas(pedido);
    if (caminho === `/api/v1/professor/turmas/${TURMA}/alunos`)
      return alunos(pedido);
    if (caminho === "/api/v1/professor/medalhas") return json(200, MEDALHAS);
    if (caminho === `/api/v1/professor/turmas/${TURMA}/medalhas`)
      return concessao(pedido);
    return new Response(null, { status: 404 });
  });
});

async function apertar(nome: string | RegExp) {
  await act(async () => {
    fireEvent.press(await screen.findByLabelText(nome));
  });
}

// Botão pelo nome acessível, que no Button é o próprio rótulo.
async function apertarBotao(nome: string) {
  await act(async () => {
    fireEvent.press(await screen.findByRole("button", { name: nome }));
  });
}

describe("lista de turmas", () => {
  it("mostra cada turma vigente com curso, semestre e alunos e abre a turma", async () => {
    await renderComApi(<TurmasScreen />, api);

    expect(await screen.findByText("Banco de Dados")).toBeOnTheScreen();
    expect(screen.getByText("CC · 2026/2 · 1 aluno")).toBeOnTheScreen();

    await apertar(/^Banco de Dados/);
    expect(mockRoteador.push).toHaveBeenCalledWith(`/turmas/${TURMA}`);
  });

  it("sem turma no semestre, diz isso em vez de uma lista vazia", async () => {
    turmas.mockResolvedValue(json(200, []));
    await renderComApi(<TurmasScreen />, api);

    expect(
      await screen.findByText("Nenhuma turma neste semestre."),
    ).toBeOnTheScreen();
  });

  it("falha vira mensagem amigável com tentar de novo", async () => {
    turmas.mockRejectedValueOnce(new TypeError("Network request failed"));
    await renderComApi(<TurmasScreen />, api);

    expect(
      await screen.findByText(
        "Sem conexão. Confira a internet e tente de novo.",
      ),
    ).toBeOnTheScreen();
    await apertarBotao("Tentar de novo");
    expect(await screen.findByText("Banco de Dados")).toBeOnTheScreen();
  });
});

describe("turma e concessão", () => {
  it("lista os alunos com conta e as medalhas que já têm na turma", async () => {
    alunos.mockResolvedValue(
      json(200, [
        {
          ...ALUNOS[0],
          medalhas: [
            {
              id: "m1",
              item: "M01",
              bimestre: 4,
              concedidaEm: "2026-11-10T13:00:00Z",
            },
            {
              id: "m2",
              item: "M03",
              bimestre: 4,
              concedidaEm: "2026-11-10T14:00:00Z",
            },
          ],
        },
      ]),
    );
    await renderComApi(<TurmaScreen turmaId={TURMA} />, api);

    expect(await screen.findByText("Ana Souza")).toBeOnTheScreen();
    expect(screen.getByText("@anaSouza-CC")).toBeOnTheScreen();
    expect(screen.getByText("2 medalhas nesta turma")).toBeOnTheScreen();
    expect(
      await screen.findByRole("heading", { name: "Banco de Dados" }),
    ).toBeOnTheScreen();
  });

  it("concede pelo catálogo, sem digitar valor, e confirma na tela", async () => {
    await renderComApi(<TurmaScreen turmaId={TURMA} />, api);

    await apertar("Conceder medalha a Ana Souza");
    expect(await screen.findByText("Para Ana Souza")).toBeOnTheScreen();
    expect(
      screen.getByText("Até 2 por bimestre, por matéria"),
    ).toBeOnTheScreen();
    expect(
      screen.getByText("Uma vez no semestre, por matéria"),
    ).toBeOnTheScreen();

    await apertar(/^Nota 10 em prova, Bronze, 30 pontos/);
    await apertarBotao("Conceder");

    expect(concessao).toHaveBeenCalledTimes(1);
    expect(await concessao.mock.calls[0]![0].json()).toEqual({
      alunoId: ANA,
      item: "M01",
    });
    expect(
      await screen.findByText(
        "Medalha Nota 10 em prova concedida a Ana Souza.",
      ),
    ).toBeOnTheScreen();
    // A lista da turma é lida de novo, com a medalha nova.
    expect(
      caminhos(api).filter((c) => c.endsWith("/alunos")).length,
    ).toBeGreaterThan(1);
  });

  it("o botão de conceder só vale depois de escolher a medalha", async () => {
    await renderComApi(<TurmaScreen turmaId={TURMA} />, api);
    await apertar("Conceder medalha a Ana Souza");
    await screen.findByText("Para Ana Souza");

    await apertarBotao("Conceder");

    expect(concessao).not.toHaveBeenCalled();
  });

  it("limite atingido fica no modal, com a mensagem amigável", async () => {
    concessao.mockResolvedValue(
      json(409, { status: 409, codigo: "limite-atingido" }),
    );
    await renderComApi(<TurmaScreen turmaId={TURMA} />, api);
    await apertar("Conceder medalha a Ana Souza");
    await apertar(/^Nota 10 em prova/);

    await apertarBotao("Conceder");

    expect(
      await screen.findByText(
        "Esse aluno já recebeu essa medalha o máximo de vezes no período.",
      ),
    ).toBeOnTheScreen();
    expect(screen.getByText("Para Ana Souza")).toBeOnTheScreen();
  });

  it("sem conexão na concessão, avisa sem fechar o modal", async () => {
    concessao.mockRejectedValue(new TypeError("Network request failed"));
    await renderComApi(<TurmaScreen turmaId={TURMA} />, api);
    await apertar("Conceder medalha a Ana Souza");
    await apertar(/^Média final 10/);

    await apertarBotao("Conceder");

    expect(
      await screen.findByText(
        "Sem conexão. Confira a internet e tente de novo.",
      ),
    ).toBeOnTheScreen();
  });

  it("turma de outro professor mostra o motivo, não uma tela quebrada", async () => {
    alunos.mockResolvedValue(
      json(403, { status: 403, codigo: "fora-do-escopo" }),
    );
    await renderComApi(<TurmaScreen turmaId={TURMA} />, api);

    expect(
      await screen.findByText("Essa turma não é sua neste semestre."),
    ).toBeOnTheScreen();
  });

  it("turma sem aluno com conta explica por quê", async () => {
    alunos.mockResolvedValue(json(200, []));
    await renderComApi(<TurmaScreen turmaId={TURMA} />, api);

    expect(
      await screen.findByText("Nenhum aluno com conta nesta turma."),
    ).toBeOnTheScreen();
  });
});

describe("entrada pelo início", () => {
  it("o professor vê Minhas turmas; o aluno, não", async () => {
    const comPapeis = (papeis: string[]) =>
      jest.fn(async (pedido: Request) =>
        new URL(pedido.url).pathname === "/api/v1/eu"
          ? json(200, { id: ANA, papeis, arroba: "@renataA-PROF" })
          : new Response(null, { status: 404 }),
      );

    const professor = await renderComApi(
      <InicioProvisorio />,
      comPapeis(["professor"]),
    );
    await apertarBotao("Minhas turmas");
    expect(mockRoteador.push).toHaveBeenCalledWith("/turmas");
    professor.unmount();

    await renderComApi(<InicioProvisorio />, comPapeis(["aluno"]));
    expect(await screen.findByTestId("arroba")).toBeOnTheScreen();
    expect(screen.queryByRole("button", { name: "Minhas turmas" })).toBeNull();
  });
});
