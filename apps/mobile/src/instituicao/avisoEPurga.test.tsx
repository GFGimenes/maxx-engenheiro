import { act, fireEvent, screen, within } from "@testing-library/react-native";
import { Platform } from "react-native";

import { json, renderComApi } from "../test/renderComApi";
import { NovoAvisoDaInstituicaoScreen } from "./NovoAvisoDaInstituicaoScreen";
import { PurgaScreen } from "./PurgaScreen";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  navigate: jest.fn(),
  back: jest.fn(),
  dismissTo: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

const COORDENACAO = {
  id: "0193a000-0000-7000-8000-0000000000f1",
  papeis: ["instituicao"],
  arroba: "@coordenacaoUnimax-UNIMAX",
  nomeDeExibicao: "Coordenação UniMax",
  foto: null,
};

const CC = "0193a000-0000-7000-8000-0000000000c1";
const MED = "0193a000-0000-7000-8000-0000000000c3";

let publicacao: jest.Mock<Promise<Response>, [Request]>;
let lotes: jest.Mock<Promise<Response>, [Request]>;
let purga: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  jest.replaceProperty(Platform, "OS", "web");
  publicacao = jest.fn(async (_pedido: Request) =>
    json(201, { id: "aviso-1" }),
  );
  lotes = jest.fn(async (_pedido: Request) =>
    json(200, [
      {
        lote: "banca-2",
        alunos: 12,
        primeiroEm: "2026-11-20T13:00:00Z",
        ultimoEm: "2026-11-27T18:00:00Z",
        vigente: true,
      },
      {
        lote: "ensaio",
        alunos: 1,
        primeiroEm: "2026-11-10T13:00:00Z",
        ultimoEm: "2026-11-10T13:00:00Z",
        vigente: false,
      },
    ]),
  );
  purga = jest.fn(async (_pedido: Request) =>
    json(200, { lote: "ensaio", alunos: 1, fotos: 4 }),
  );
  api = jest.fn(async (pedido: Request) => {
    const caminho = new URL(pedido.url).pathname;
    if (caminho === "/api/v1/eu") return json(200, COORDENACAO);
    if (caminho === "/api/v1/cursos")
      return json(200, [
        {
          id: CC,
          sigla: "CC",
          nome: "Ciência da Computação",
          duracaoEmSemestres: 8,
        },
        { id: MED, sigla: "MED", nome: "Medicina", duracaoEmSemestres: 12 },
      ]);
    if (caminho === "/api/v1/avisos") return publicacao(pedido);
    if (caminho === "/api/v1/instituicao/demonstracao/lotes")
      return lotes(pedido);
    if (caminho.endsWith("/purga")) return purga(pedido);
    return new Response(null, { status: 404 });
  });
});

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

async function corpo(mock: jest.Mock<Promise<Response>, [Request]>) {
  return (mock.mock.calls.at(-1)![0] as Request).clone().json();
}

describe("novo aviso da instituição", () => {
  it("publica para a faculdade, que é o escopo de partida, e volta ao mural", async () => {
    await renderComApi(<NovoAvisoDaInstituicaoScreen />, api);

    expect(
      await screen.findByRole("radio", { name: "Faculdade", checked: true }),
    ).toBeOnTheScreen();
    expect(
      screen.getByRole("button", { name: "Publicar aviso" }),
    ).toBeDisabled();
    await escrever("Título", "Semana de provas");
    await escrever("Texto", "De 23/11 a 04/12.");
    await apertar("Publicar aviso");

    expect(await corpo(publicacao)).toEqual({
      escopo: "faculdade",
      titulo: "Semana de provas",
      texto: "De 23/11 a 04/12.",
      uploadId: null,
    });
    expect(mockRoteador.dismissTo).toHaveBeenCalledWith("/mural");
  });

  it("para cursos, pede ao menos um e manda os escolhidos", async () => {
    await renderComApi(<NovoAvisoDaInstituicaoScreen />, api);

    await apertar("Cursos", "radio");
    await escrever("Título", "Laboratório fechado");
    await escrever("Texto", "Aulas práticas no laboratório 5.");
    expect(
      screen.getByRole("button", { name: "Publicar aviso" }),
    ).toBeDisabled();
    await apertar("CC", "checkbox");
    await apertar("MED", "checkbox");
    await apertar("Publicar aviso");

    expect(await corpo(publicacao)).toEqual({
      escopo: "curso",
      cursoIds: [CC, MED],
      titulo: "Laboratório fechado",
      texto: "Aulas práticas no laboratório 5.",
      uploadId: null,
    });
  });

  it("a recusa da API aparece sem perder o que foi digitado", async () => {
    publicacao.mockResolvedValueOnce(
      json(400, { status: 400, codigo: "titulo-do-aviso-invalido" }),
    );
    await renderComApi(<NovoAvisoDaInstituicaoScreen />, api);

    await escrever("Título", "Aviso");
    await escrever("Texto", "Texto do aviso.");
    await apertar("Publicar aviso");

    expect(
      await screen.findByText("O título precisa ter de 3 a 120 caracteres."),
    ).toBeOnTheScreen();
    expect(screen.getByLabelText("Título").props.value).toBe("Aviso");
    expect(mockRoteador.dismissTo).not.toHaveBeenCalled();
  });
});

describe("purga da demonstração", () => {
  it("lista os lotes com o vigente marcado", async () => {
    await renderComApi(<PurgaScreen />, api);

    const vigente = await screen.findByTestId("lote-banca-2");
    expect(within(vigente).getByText("Vigente")).toBeOnTheScreen();
    expect(
      within(vigente).getByText("12 alunos · de 20/11/2026 a 27/11/2026"),
    ).toBeOnTheScreen();
    expect(
      within(screen.getByTestId("lote-ensaio")).queryByText("Vigente"),
    ).toBeNull();
  });

  it("purgar pede o nome do lote digitado igual e mostra o resultado", async () => {
    await renderComApi(<PurgaScreen />, api);

    await apertar("Purgar o lote ensaio");
    const folha = await screen.findByTestId("folha-de-ato");
    const confirmar = within(folha).getByRole("button", {
      name: "Purgar lote",
    });
    await act(async () => {
      fireEvent.changeText(
        within(folha).getByLabelText("Nome do lote"),
        "ensai",
      );
    });
    expect(confirmar).toBeDisabled();
    await act(async () => {
      fireEvent.changeText(
        within(folha).getByLabelText("Nome do lote"),
        "ensaio",
      );
    });
    await act(async () => {
      fireEvent.press(confirmar);
    });

    expect(new URL(purga.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/instituicao/demonstracao/lotes/ensaio/purga",
    );
    expect(await corpo(purga)).toEqual({ confirmacao: "ensaio" });
    expect(await screen.findByTestId("purga-concluida")).toHaveTextContent(
      "Lote ensaio purgado: 1 aluno e 4 fotos a remover do bucket.",
    );
  });

  it("sem lote no banco, um estado vazio", async () => {
    lotes.mockResolvedValueOnce(json(200, []));
    await renderComApi(<PurgaScreen />, api);

    expect(
      await screen.findByText("Nenhum lote de demonstração no banco."),
    ).toBeOnTheScreen();
  });
});
