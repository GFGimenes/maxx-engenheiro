import { screen, within } from "@testing-library/react-native";

import { CartaoDePost } from "../feed/CartaoDePost";
import type { Post } from "../feed/api";
import { json, renderComApi } from "../test/renderComApi";
import { TrofeusScreen } from "../trofeus/TrofeusScreen";

const mockRoteador = { push: jest.fn(), navigate: jest.fn() };
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

const ANA = "0193a000-0000-7000-8000-0000000000b1";

function postCom(estado: string | null): Post {
  return {
    id: "0193a000-0000-7000-8000-0000000000f1",
    autor: {
      id: ANA,
      nomeDeExibicao: "Ana Souza",
      arroba: "@anaSouza-CC",
      curso: "CC",
      semestre: 3,
      foto: null,
    },
    legenda: "Na semana acadêmica.",
    foto: {
      feed: "http://cdn.test/feed.jpg",
      grade: "http://cdn.test/grade.jpg",
    },
    premio: {
      tipo: "premioParticipacao",
      id: "0193a000-0000-7000-8000-0000000000d1",
      item: "P06",
      nome: "Semana acadêmica",
      tier: "bronze",
      pontos: 30,
      raridade: null,
      materia: null,
      bimestre: null,
      estado,
      evento: "Semana Acadêmica de Computação",
    },
    criadoEm: new Date().toISOString(),
    curtidas: 0,
    comentarios: 0,
    curtiu: false,
  };
}

const api = jest.fn(
  async (_pedido: Request) => new Response(null, { status: 404 }),
);

describe("marcas do prêmio de participação no cartão", () => {
  it("pendente: selo, marca Pendente e a faixa com o evento e Aguardando aprovação", async () => {
    await renderComApi(
      <CartaoDePost
        post={postCom("reivindicado")}
        leitor={{ id: "outro", papeis: ["aluno"] }}
      />,
      api,
    );

    expect(await screen.findByTestId("marca-reivindicado")).toHaveTextContent(
      "Pendente",
    );
    expect(screen.getByTestId("selo")).toBeOnTheScreen();
    const faixa = screen.getByTestId("faixa");
    expect(
      within(faixa).getByText("Semana Acadêmica de Computação"),
    ).toBeOnTheScreen();
    expect(within(faixa).getByText("Aguardando aprovação")).toBeOnTheScreen();
  });

  it("em averiguação: só a marca, sem metal, sem tier e sem pontos", async () => {
    await renderComApi(
      <CartaoDePost
        post={postCom("emAveriguacao")}
        leitor={{ id: ANA, papeis: ["aluno"] }}
      />,
      api,
    );

    expect(await screen.findByTestId("marca-emAveriguacao")).toHaveTextContent(
      "Em averiguação",
    );
    expect(screen.queryByTestId("selo")).toBeNull();
    expect(screen.queryByTestId("faixa")).toBeNull();
    expect(screen.queryByText("+30")).toBeNull();
  });

  it("válido: selo e faixa com os pontos, sem marca", async () => {
    await renderComApi(
      <CartaoDePost post={postCom("valido")} leitor={undefined} />,
      api,
    );

    expect(await screen.findByTestId("selo")).toBeOnTheScreen();
    expect(screen.getByText("+30")).toBeOnTheScreen();
    expect(screen.queryByTestId("marca-valido")).toBeNull();
    expect(screen.queryByText("Aguardando aprovação")).toBeNull();
  });
});

describe("seção Reivindicações na aba Troféus", () => {
  const reivindicacao = (
    premioId: string,
    estado: string,
    motivo: string | null,
  ) => ({
    premioId,
    postId: estado === "revogado" ? null : `post-${premioId}`,
    item: "P01",
    nomeDoItem: "Palestra obrigatória",
    tier: "bronze",
    pontos: 10,
    evento: `Palestra ${premioId}`,
    materia: null,
    estado,
    motivo,
    desde: new Date().toISOString(),
  });

  const comReivindicacoes = (lista: unknown[]) =>
    jest.fn(async (pedido: Request) => {
      const caminho = new URL(pedido.url).pathname;
      if (caminho === "/api/v1/eu") {
        return json(200, {
          id: ANA,
          papeis: ["aluno"],
          arroba: "@anaSouza-CC",
          nomeDeExibicao: "Ana Souza",
          foto: null,
        });
      }
      if (caminho === "/api/v1/eu/premios")
        return json(200, { pontos: 0, familias: [], itens: [] });
      if (caminho === "/api/v1/eu/reivindicacoes") return json(200, lista);
      return new Response(null, { status: 404 });
    });

  it("mostra os quatro estados e o motivo da reprovada e da revogada", async () => {
    await renderComApi(
      <TrofeusScreen />,
      comReivindicacoes([
        reivindicacao("a", "reivindicado", null),
        reivindicacao("b", "emAveriguacao", null),
        reivindicacao("c", "reprovado", "Foto ilegível."),
        reivindicacao("d", "revogado", "Outro evento."),
      ]),
    );

    expect(
      await screen.findByRole("heading", { name: "Reivindicações" }),
    ).toBeOnTheScreen();
    expect(
      within(screen.getByTestId("reivindicacao-a")).getByText("Pendente"),
    ).toBeOnTheScreen();
    expect(
      within(screen.getByTestId("reivindicacao-b")).getByText("Em averiguação"),
    ).toBeOnTheScreen();
    expect(
      within(screen.getByTestId("reivindicacao-c")).getByText("Reprovada"),
    ).toBeOnTheScreen();
    expect(
      within(screen.getByTestId("reivindicacao-c")).getByText(
        "Motivo: Foto ilegível.",
      ),
    ).toBeOnTheScreen();
    expect(
      within(screen.getByTestId("reivindicacao-d")).getByText(
        "Motivo: Outro evento.",
      ),
    ).toBeOnTheScreen();
    expect(
      within(screen.getByTestId("reivindicacao-a")).queryByText(/Motivo/),
    ).toBeNull();
  });

  it("sem reivindicação, a seção não aparece", async () => {
    await renderComApi(<TrofeusScreen />, comReivindicacoes([]));

    expect(await screen.findByText("Mostrar não obtidos")).toBeOnTheScreen();
    expect(
      screen.queryByRole("heading", { name: "Reivindicações" }),
    ).toBeNull();
  });
});
