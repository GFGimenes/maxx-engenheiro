import { renderHook } from "@testing-library/react-native";

import {
  destinoSeguro,
  loginVoltandoPara,
  useDestinoDepoisDoLogin,
} from "../navegacao/voltar";
import {
  descreverAutor,
  descreverComentarios,
  descreverCurtidas,
  descreverMedalha,
  formatarNumero,
  tempoDesde,
  usuarioDoArroba,
} from "./textos";

describe("textos do feed", () => {
  it("números no formato brasileiro", () => {
    expect(formatarNumero(7)).toBe("7");
    expect(formatarNumero(1234)).toBe("1.234");
    expect(formatarNumero(15020)).toBe("15.020");
    expect(formatarNumero(1234567)).toBe("1.234.567");
  });

  it("curtidas e comentários no singular, no plural e escondidos quando zero", () => {
    expect(descreverCurtidas(0)).toBeNull();
    expect(descreverCurtidas(1)).toBe("1 curtida");
    expect(descreverCurtidas(1280)).toBe("1.280 curtidas");
    expect(descreverComentarios(0)).toBeNull();
    expect(descreverComentarios(1)).toBe("Ver 1 comentário");
    expect(descreverComentarios(12)).toBe("Ver todos os 12 comentários");
  });

  it("tempo desde a publicação, e a data depois de uma semana", () => {
    const agora = new Date("2026-11-10T15:00:00Z");

    expect(tempoDesde("2026-11-10T14:59:30Z", agora)).toBe("agora");
    expect(tempoDesde("2026-11-10T14:59:00Z", agora)).toBe("há 1 minuto");
    expect(tempoDesde("2026-11-10T13:00:00Z", agora)).toBe("há 2 horas");
    expect(tempoDesde("2026-11-07T15:00:00Z", agora)).toBe("há 3 dias");
    expect(tempoDesde("2026-10-01T15:00:00Z", agora)).toBe("1 de out.");
    expect(tempoDesde("2025-12-24T15:00:00Z", agora)).toBe(
      "24 de dez. de 2025",
    );
  });

  it("o nome de usuário é o arroba sem @ e sem sufixo, e o curso vem ao lado", () => {
    expect(usuarioDoArroba("@anaSouza-CC")).toBe("anaSouza");
    expect(usuarioDoArroba("@carlaDias-EX")).toBe("carlaDias");
    expect(descreverAutor({ curso: "CC", semestre: 3 })).toBe(
      "CC · 3º semestre",
    );
    expect(descreverAutor({ curso: null, semestre: null })).toBe("");
    expect(descreverMedalha({ materia: "Banco de Dados", bimestre: 2 })).toBe(
      "Banco de Dados · 2º bimestre",
    );
    expect(descreverMedalha({})).toBeUndefined();
  });
});

describe("voltar depois do login", () => {
  it("aceita só o post e os comentários dele", () => {
    expect(destinoSeguro("/post/0193a000-0000-7000-8000-0000000000aa")).toBe(
      "/post/0193a000-0000-7000-8000-0000000000aa",
    );
    expect(destinoSeguro("/post/abc/comentarios")).toBe(
      "/post/abc/comentarios",
    );
    for (const ruim of [
      "/perfil",
      "/post/a/b",
      "//maligno.example",
      "https://maligno.example",
      "/post?x=1",
      "javascript:alert(1)",
      "/",
      undefined,
      ["/post/a"],
    ]) {
      expect(destinoSeguro(ruim)).toBeNull();
    }
  });

  it("monta o login com o destino, ou o login puro", () => {
    expect(loginVoltandoPara("/post/abc")).toBe("/login?voltar=%2Fpost%2Fabc");
    expect(loginVoltandoPara("/")).toBe("/login");
  });

  it("guarda o destino quando a URL perde o voltar ao entrar", async () => {
    const { result, rerender } = await renderHook(
      ({ autenticado, voltar }: { autenticado: boolean; voltar?: string }) =>
        useDestinoDepoisDoLogin(autenticado, voltar),
      { initialProps: { autenticado: false, voltar: "/post/abc" } },
    );
    expect(result.current).toBe("/post/abc");

    // Na web, a URL do login fica sem o voltar logo que a sessão abre.
    await rerender({ autenticado: true, voltar: undefined });
    expect(result.current).toBe("/post/abc");
  });

  it("sem voltar, ou com um voltar que não é post, não há destino", async () => {
    const { result, rerender } = await renderHook(
      ({ autenticado, voltar }: { autenticado: boolean; voltar?: string }) =>
        useDestinoDepoisDoLogin(autenticado, voltar),
      { initialProps: { autenticado: false, voltar: "//maligno.example" } },
    );
    expect(result.current).toBeNull();
    await rerender({ autenticado: true, voltar: undefined });
    expect(result.current).toBeNull();
  });
});
