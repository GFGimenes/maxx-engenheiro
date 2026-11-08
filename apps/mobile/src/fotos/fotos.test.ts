import { ImageManipulator, SaveFormat } from "expo-image-manipulator";

import { DERIVADAS, FotoGrandeDemais, gerarDerivadas } from "./derivadas";

jest.mock("expo-image-manipulator", () => ({
  SaveFormat: { JPEG: "jpeg" },
  ImageManipulator: { manipulate: jest.fn() },
}));

interface Passos {
  crop: jest.Mock;
  resize: jest.Mock;
  saveAsync: jest.Mock;
}

// Cada manipulação devolve a cadeia crop → resize → render → save, e grava o que pediu.
function simularManipulador(): Passos[] {
  const chamadas: Passos[] = [];
  jest.mocked(ImageManipulator.manipulate).mockImplementation(() => {
    const passos: Passos = {
      crop: jest.fn(),
      resize: jest.fn(),
      saveAsync: jest.fn(async () => ({
        uri: `file:///derivada-${chamadas.length}.jpg`,
        width: 0,
        height: 0,
      })),
    };
    chamadas.push(passos);
    const contexto = {
      crop: (recorte: unknown) => {
        passos.crop(recorte);
        return contexto;
      },
      resize: (tamanho: unknown) => {
        passos.resize(tamanho);
        return contexto;
      },
      renderAsync: async () => ({ saveAsync: passos.saveAsync }),
    };
    return contexto as never;
  });
  return chamadas;
}

const blobDe = (bytes: number) => new Blob([new Uint8Array(bytes)]);

beforeEach(() => jest.clearAllMocks());

describe("derivadas da foto", () => {
  it("recorta o quadrado central e gera o feed em 1080 e a grade em 320, em JPEG", async () => {
    const chamadas = simularManipulador();

    const derivadas = await gerarDerivadas(
      { uri: "file:///foto.jpg", width: 4000, height: 3000 },
      async () => blobDe(1000),
    );

    expect(chamadas).toHaveLength(2);
    for (const passos of chamadas) {
      expect(passos.crop).toHaveBeenCalledWith({
        originX: 500,
        originY: 0,
        width: 3000,
        height: 3000,
      });
    }
    expect(chamadas[0]?.resize).toHaveBeenCalledWith({
      width: 1080,
      height: 1080,
    });
    expect(chamadas[1]?.resize).toHaveBeenCalledWith({
      width: 320,
      height: 320,
    });
    expect(chamadas[0]?.saveAsync).toHaveBeenCalledWith({
      format: SaveFormat.JPEG,
      compress: DERIVADAS.feed.qualidade,
    });
    expect(derivadas.feed.tamanho).toBe(1000);
    expect(derivadas.grade.uri).toBe("file:///derivada-2.jpg");
  });

  it("foto menor que a derivada não é ampliada", async () => {
    const chamadas = simularManipulador();

    await gerarDerivadas(
      { uri: "file:///pequena.jpg", width: 200, height: 300 },
      async () => blobDe(1000),
    );

    expect(chamadas[0]?.crop).toHaveBeenCalledWith({
      originX: 0,
      originY: 50,
      width: 200,
      height: 200,
    });
    expect(chamadas[0]?.resize).toHaveBeenCalledWith({
      width: 200,
      height: 200,
    });
    expect(chamadas[1]?.resize).toHaveBeenCalledWith({
      width: 200,
      height: 200,
    });
  });

  it("acima do limite, tenta de novo mais comprimida", async () => {
    const chamadas = simularManipulador();
    const tamanhos = [DERIVADAS.feed.maximo + 1, 900_000, 1000];

    const derivadas = await gerarDerivadas(
      { uri: "file:///foto.jpg", width: 3000, height: 3000 },
      async () => blobDe(tamanhos.shift() ?? 1000),
    );

    expect(chamadas[1]?.saveAsync).toHaveBeenCalledWith({
      format: SaveFormat.JPEG,
      compress: DERIVADAS.feed.qualidade - 0.25,
    });
    expect(derivadas.feed.tamanho).toBe(900_000);
  });

  it("se nem assim couber, diz que a foto ficou grande demais", async () => {
    simularManipulador();

    await expect(
      gerarDerivadas(
        { uri: "file:///foto.jpg", width: 3000, height: 3000 },
        async () => blobDe(DERIVADAS.feed.maximo + 1),
      ),
    ).rejects.toBeInstanceOf(FotoGrandeDemais);
  });
});
