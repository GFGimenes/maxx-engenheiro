import type { ReactElement } from "react";

// A rota decide pelo __DEV__ ao carregar o módulo: cada teste a carrega num registro limpo.
function carregarRota(dev: boolean) {
  const anterior = __DEV__;
  (globalThis as unknown as { __DEV__: boolean }).__DEV__ = dev;
  let rota!: () => ReactElement<{ href?: string }>;
  jest.isolateModules(() => {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    rota = require("../app/vitrine").default;
  });
  (globalThis as unknown as { __DEV__: boolean }).__DEV__ = anterior;
  return rota;
}

afterEach(() => jest.dontMock("./VitrineScreen"));

test("em produção, a rota volta para o início e nem carrega a vitrine", () => {
  jest.doMock("./VitrineScreen", () => {
    throw new Error("a vitrine não pode carregar em produção");
  });

  const elemento = carregarRota(false)();

  expect(elemento.props.href).toBe("/");
});

test("em desenvolvimento, a rota mostra a vitrine", () => {
  const Marcador = () => null;
  jest.doMock("./VitrineScreen", () => ({ VitrineScreen: Marcador }));

  expect(carregarRota(true)().type).toBe(Marcador);
});
