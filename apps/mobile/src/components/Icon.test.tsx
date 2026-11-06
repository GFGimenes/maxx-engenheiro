import { render, screen } from "@testing-library/react-native";

import { tamanhos } from "../theme/tokens";
import { Icon, iconNames } from "./Icon";

// O desenho do SVG é nativo: aqui, cada elemento vira uma View com os mesmos props.
jest.mock("react-native-svg", () => {
  const { createElement } = jest.requireActual("react");
  const { View } = jest.requireActual("react-native");
  function elemento(tipo: string) {
    return function ElementoSvg(props: object) {
      return createElement(View, { testID: tipo, ...props });
    };
  }
  return {
    __esModule: true,
    default: elemento("svg"),
    Path: elemento("path"),
    Circle: elemento("circle"),
    Rect: elemento("rect"),
  };
});

const OCULTOS = { includeHiddenElements: true };

test("usa a grade de 24, traço 1.8 e pontas arredondadas, em qualquer tamanho", async () => {
  await render(<Icon name="home" color="ink" size={tamanhos.iconeNav} />);

  expect(screen.getByTestId("svg", OCULTOS).props).toMatchObject({
    width: 26,
    height: 26,
    viewBox: "0 0 24 24",
    strokeWidth: 1.8,
    strokeLinecap: "round",
    strokeLinejoin: "round",
  });
});

test.each(iconNames)("%s desenha nos dois modos", async (name) => {
  await render(<Icon name={name} color="ink" />);
  await render(<Icon name={name} color="ink" filled detailColor="fundo" />);

  expect(screen.getByTestId(`icone-${name}`, OCULTOS)).toBeTruthy();
});

test("em contorno, nada é preenchido e tudo sai na cor pedida", async () => {
  await render(<Icon name="newspaper" color="ink" />);

  const partes = [
    ...screen.getAllByTestId("path", OCULTOS),
    ...screen.getAllByTestId("rect", OCULTOS),
  ];
  expect(partes).toHaveLength(4);
  for (const parte of partes) {
    expect(parte.props).toMatchObject({ fill: "none", stroke: "ink" });
  }
});

test("preenchido, o desenho interno sai recortado", async () => {
  await render(
    <Icon name="newspaper" color="ink" filled detailColor="fundo" />,
  );

  const [folha, linha] = screen.getAllByTestId("path", OCULTOS);
  expect(folha?.props).toMatchObject({ fill: "ink", stroke: "ink" });
  expect(linha?.props).toMatchObject({ fill: "none", stroke: "fundo" });
});

test("preenchido, o traço aberto não vira mancha", async () => {
  await render(<Icon name="taca" color="ink" filled />);

  const [bojo, haste] = screen.getAllByTestId("path", OCULTOS);
  expect(bojo?.props).toMatchObject({ fill: "ink", stroke: "ink" });
  expect(haste?.props).toMatchObject({ fill: "none", stroke: "ink" });
});

test("com rótulo, é imagem para o leitor de tela", async () => {
  await render(<Icon name="heart" color="like" label="Curtir" />);

  expect(screen.getByRole("img", { name: "Curtir" })).toBeTruthy();
});

test("sem rótulo, fica fora da leitura de tela", async () => {
  await render(<Icon name="heart" color="like" />);

  expect(screen.queryByTestId("icone-heart")).toBeNull();
  expect(screen.getByTestId("icone-heart", OCULTOS)).toBeTruthy();
});
