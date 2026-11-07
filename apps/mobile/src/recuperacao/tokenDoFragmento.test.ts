import { Platform } from "react-native";

import { capturarTokenDoFragmento } from "./tokenDoFragmento";

// Um navegador mínimo: a barra de endereço e o histórico.
function navegador(url: string) {
  const local = new URL(url);
  const replaceState = jest.fn(
    (_estado: unknown, _titulo: string, nova: string) => {
      const proxima = new URL(nova, local);
      local.hash = proxima.hash;
      local.pathname = proxima.pathname;
      local.search = proxima.search;
    },
  );
  Object.defineProperty(globalThis, "window", {
    configurable: true,
    value: {
      location: local,
      history: { state: { chave: 1 }, replaceState },
    },
  });
  return { local, replaceState };
}

const plataformaOriginal = Platform.OS;

afterEach(() => {
  Object.defineProperty(Platform, "OS", { value: plataformaOriginal });
  delete (globalThis as { window?: unknown }).window;
});

function naWeb() {
  Object.defineProperty(Platform, "OS", { value: "web" });
}

test("lê o token do fragmento e o apaga da barra de endereço, mantendo o histórico", () => {
  naWeb();
  const { local, replaceState } = navegador(
    "http://localhost/redefinir-senha#token=abc_DEF-123",
  );

  expect(capturarTokenDoFragmento()).toBe("abc_DEF-123");
  expect(local.href).toBe("http://localhost/redefinir-senha");
  expect(replaceState).toHaveBeenCalledWith(
    { chave: 1 },
    "",
    "/redefinir-senha",
  );
});

test("fragmento sem token, ou vazio, não é token", () => {
  naWeb();
  navegador("http://localhost/redefinir-senha#outra=1");
  expect(capturarTokenDoFragmento()).toBeNull();

  navegador("http://localhost/redefinir-senha#token=");
  expect(capturarTokenDoFragmento()).toBeNull();
});

test("só lê na rota de senha nova", () => {
  naWeb();
  const { replaceState } = navegador("http://localhost/login#token=abc");

  expect(capturarTokenDoFragmento()).toBeNull();
  expect(replaceState).not.toHaveBeenCalled();
});

test("no nativo não há fragmento para ler", () => {
  Object.defineProperty(Platform, "OS", { value: "android" });

  expect(capturarTokenDoFragmento()).toBeNull();
});
