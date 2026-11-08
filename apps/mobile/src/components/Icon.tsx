import { View } from "react-native";
import Svg, { Circle, Path, Rect } from "react-native-svg";

import { tamanhos } from "../theme/tokens";

// Caminhos do Lucide 1.70.0 (LICENSE-lucide.txt), em grade de 24. `taca` é o trophy e `fita` é
// o medal do Lucide. Só entra aqui o ícone que a identidade visual usa.

// Como a parte se comporta no ícone preenchido: por padrão, vira massa cheia.
// `aberto`: traço aberto, que preenchido viraria uma mancha (alça da taça).
// `detalhe`: desenho interno, recortado na cor de `detailColor` (porta da casa, linhas do jornal).
type Modo = "aberto" | "detalhe";

type Parte = { modo?: Modo } & (
  | { d: string }
  | { cx: number; cy: number; r: number }
  | { x: number; y: number; width: number; height: number; rx: number }
);

const ICONES = {
  home: [
    {
      d: "M3 10a2 2 0 0 1 .709-1.528l7-6a2 2 0 0 1 2.582 0l7 6A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
    },
    { d: "M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8", modo: "detalhe" },
  ],
  search: [
    { cx: 11, cy: 11, r: 8 },
    { d: "m21 21-4.34-4.34", modo: "aberto" },
  ],
  newspaper: [
    {
      d: "M4 22h16a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H8a2 2 0 0 0-2 2v16a2 2 0 0 1-4 0v-9a2 2 0 0 1 2-2h2",
    },
    { d: "M15 18h-5", modo: "detalhe" },
    { d: "M18 14h-8", modo: "detalhe" },
    { x: 10, y: 6, width: 8, height: 4, rx: 1, modo: "detalhe" },
  ],
  taca: [
    { d: "M6 9a6 6 0 0 0 12 0V3a1 1 0 0 0-1-1H7a1 1 0 0 0-1 1z" },
    { d: "M10 14.66V17a1 1 0 0 1-1 1 2 2 0 0 0-2 2v2", modo: "aberto" },
    { d: "M14 14.66V17a1 1 0 0 0 1 1 2 2 0 0 1 2 2v2", modo: "aberto" },
    {
      d: "M17.916 10H19.5A2.5 2.5 0 0 0 22 7.5V5a1 1 0 0 0-1-1h-3",
      modo: "aberto",
    },
    { d: "M4 22h16", modo: "aberto" },
    {
      d: "M6.084 10H4.5A2.5 2.5 0 0 1 2 7.5V5a1 1 0 0 1 1-1h3",
      modo: "aberto",
    },
  ],
  fita: [
    {
      d: "M7.21 15 2.66 7.14a2 2 0 0 1 .13-2.2L4.4 2.8A2 2 0 0 1 6 2h12a2 2 0 0 1 1.6.8l1.6 2.14a2 2 0 0 1 .14 2.2L16.79 15",
      modo: "aberto",
    },
    { d: "M11 12 5.12 2.2", modo: "aberto" },
    { d: "m13 12 5.88-9.8", modo: "aberto" },
    { d: "M8 7h8", modo: "aberto" },
    { cx: 12, cy: 17, r: 5 },
    { d: "M12 18v-2h-.5", modo: "detalhe" },
  ],
  user: [
    { d: "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" },
    { cx: 12, cy: 7, r: 4 },
  ],
  heart: [
    {
      d: "M2 9.5a5.5 5.5 0 0 1 9.591-3.676.56.56 0 0 0 .818 0A5.49 5.49 0 0 1 22 9.5c0 2.29-1.5 4-3 5.5l-5.492 5.313a2 2 0 0 1-3 .019L5 15c-1.5-1.5-3-3.2-3-5.5",
    },
  ],
  message: [
    {
      d: "M2.992 16.342a2 2 0 0 1 .094 1.167l-1.065 3.29a1 1 0 0 0 1.236 1.168l3.413-.998a2 2 0 0 1 1.099.092 10 10 0 1 0-4.777-4.719",
    },
  ],
  send: [
    {
      d: "M14.536 21.686a.5.5 0 0 0 .937-.024l6.5-19a.496.496 0 0 0-.635-.635l-19 6.5a.5.5 0 0 0-.024.937l7.93 3.18a2 2 0 0 1 1.112 1.11z",
    },
    { d: "m21.854 2.147-10.94 10.939", modo: "detalhe" },
  ],
  bookmark: [
    {
      d: "M17 3a2 2 0 0 1 2 2v15a1 1 0 0 1-1.496.868l-4.512-2.578a2 2 0 0 0-1.984 0l-4.512 2.578A1 1 0 0 1 5 20V5a2 2 0 0 1 2-2z",
    },
  ],
  eye: [
    {
      d: "M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0",
    },
    { cx: 12, cy: 12, r: 3, modo: "detalhe" },
  ],
  eyeOff: [
    {
      d: "M10.733 5.076a10.744 10.744 0 0 1 11.205 6.575 1 1 0 0 1 0 .696 10.747 10.747 0 0 1-1.444 2.49",
      modo: "aberto",
    },
    { d: "M14.084 14.158a3 3 0 0 1-4.242-4.242", modo: "aberto" },
    {
      d: "M17.479 17.499a10.75 10.75 0 0 1-15.417-5.151 1 1 0 0 1 0-.696 10.75 10.75 0 0 1 4.446-5.143",
      modo: "aberto",
    },
    { d: "m2 2 20 20", modo: "aberto" },
  ],
  lock: [
    { x: 3, y: 11, width: 18, height: 11, rx: 2 },
    { d: "M7 11V7a5 5 0 0 1 10 0v4", modo: "aberto" },
  ],
  // O check do Lucide: confirmação curta, como o arroba livre no cadastro.
  check: [{ d: "M20 6 9 17l-5-5", modo: "aberto" }],
  alert: [
    { cx: 12, cy: 12, r: 10 },
    { d: "M12 8v4", modo: "detalhe" },
    { d: "M12 16h.01", modo: "detalhe" },
  ],
  // Voltar no cabeçalho, entrar numa linha da lista e fechar a folha: chevron-left,
  // chevron-right e x do Lucide.
  voltar: [{ d: "m15 18-6-6 6-6", modo: "aberto" }],
  avancar: [{ d: "m9 18 6-6-6-6", modo: "aberto" }],
  fechar: [
    { d: "M18 6 6 18", modo: "aberto" },
    { d: "m6 6 12 12", modo: "aberto" },
  ],
  // image do Lucide: a moldura vira massa no preenchido, sol e montanha são desenho interno.
  imagem: [
    { x: 3, y: 3, width: 18, height: 18, rx: 2 },
    { cx: 9, cy: 9, r: 2, modo: "detalhe" },
    { d: "m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21", modo: "detalhe" },
  ],
  // square-plus do Lucide: o "+" do cabeçalho do feed, que abre o novo post.
  novo: [
    { x: 3, y: 3, width: 18, height: 18, rx: 2 },
    { d: "M8 12h8", modo: "detalhe" },
    { d: "M12 8v8", modo: "detalhe" },
  ],
  // ellipsis do Lucide: o "..." do cabeçalho do post.
  opcoes: [
    { cx: 12, cy: 12, r: 1 },
    { cx: 19, cy: 12, r: 1 },
    { cx: 5, cy: 12, r: 1 },
  ],
  inbox: [
    {
      d: "M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z",
    },
    { d: "M22 12h-6l-2 3h-4l-2-3H2", modo: "detalhe" },
  ],
} satisfies Record<string, Parte[]>;

export type IconName = keyof typeof ICONES;
export const iconNames = Object.keys(ICONES) as IconName[];

interface IconProps {
  name: IconName;
  // Sempre um token do tema: o ícone não tem cor própria.
  color: string;
  size?: number;
  // Preenchido, como a aba ativa e a curtida; sem isso, contorno.
  filled?: boolean;
  // Cor do desenho interno no preenchido: o fundo sobre o qual o ícone está.
  detailColor?: string;
  // Sem rótulo, o ícone é decorativo: quem lê a tela usa o rótulo do botão em volta.
  label?: string;
}

export function Icon({
  name,
  color,
  size = 24,
  filled = false,
  detailColor,
  label,
}: IconProps) {
  const partes: Parte[] = ICONES[name];
  const acessibilidade = label
    ? { accessible: true, role: "img" as const, "aria-label": label }
    : { "aria-hidden": true };

  return (
    <View testID={`icone-${name}`} {...acessibilidade}>
      <Svg
        width={size}
        height={size}
        viewBox="0 0 24 24"
        fill="none"
        strokeWidth={tamanhos.tracoIcone}
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        {partes.map((parte, indice) => {
          const props = {
            stroke:
              filled && parte.modo === "detalhe"
                ? (detailColor ?? color)
                : color,
            fill: filled && parte.modo === undefined ? color : "none",
          };
          if ("d" in parte) return <Path key={indice} d={parte.d} {...props} />;
          if ("r" in parte) {
            return (
              <Circle
                key={indice}
                cx={parte.cx}
                cy={parte.cy}
                r={parte.r}
                {...props}
              />
            );
          }
          return (
            <Rect
              key={indice}
              x={parte.x}
              y={parte.y}
              width={parte.width}
              height={parte.height}
              rx={parte.rx}
              {...props}
            />
          );
        })}
      </Svg>
    </View>
  );
}
