import json from "../../../../design/tokens.json";

// Camada tipada sobre design/tokens.json, a fonte dos valores. Cores e tiers vêm direto do
// JSON. Tamanhos, raios e tipografia estão lá em texto livre ("28/32, Archivo 800"), então
// viram números aqui; tokens.test.ts confere cada um contra o texto do JSON.

export type ThemeName = "claro" | "escuro";
export type ThemeColors = typeof json.cores.claro;

export const cores: Record<ThemeName, ThemeColors> = {
  claro: json.cores.claro,
  escuro: json.cores.escuro,
};

export type Tier = "bronze" | "prata" | "ouro" | "diamante";
export type Raridade = "comum" | "raro" | "epico" | "lendario";

export interface TierStyle {
  pontos: string;
  medalhao: [string, string, string];
  borda: string;
  glifo: string;
  pill: [string, string, string, string, string];
  pillTexto: string;
  texto: Record<ThemeName, string>;
  fundoGrade: Record<ThemeName, [string, string]>;
}

export const tiers = {
  bronze: json.tiers.bronze,
  prata: json.tiers.prata,
  ouro: json.tiers.ouro,
  diamante: json.tiers.diamante,
} as Record<Tier, TierStyle>;

export const espacamento = {
  base: 4,
  escala: [4, 8, 12, 16, 24, 32],
  margemLateral: 16,
  respiroAntesDaNavegacao: 24,
} as const;

export const raios = {
  filtros: 8,
  campos: 8,
  botoes: 8,
  controleSegmentado: 10,
  // Segmento ativo dentro do trilho (identidade-visual.md); o JSON só traz o raio do trilho.
  segmentoAtivo: 8,
  cardMenor: 12,
  cardMaior: 16,
  realceNav: 14,
  pill: 999,
} as const;

export const tamanhos = {
  alvoDeToque: 44,
  botaoDiscreto: { altura: 32, texto: 13, toque: 44 },
  botaoGrande: 50,
  campo: 48,
  navegacaoInferior: 84,
  iconeNav: 26,
  realceAbaAtiva: 44,
  avatarNav: 30,
  avatarPost: 36,
  medalhaoLista: 48,
  bolinhaNovidade: { diametro: 8, halo: 2 },
  fotoPost: 390,
  tracoIcone: 1.8,
} as const;

export interface EstiloDeTexto {
  familia: "Archivo" | "Instrument Sans";
  tamanho: number;
  // A escala não define altura de linha para os pontos.
  alturaLinha?: number;
  peso: 400 | 500 | 600 | 700 | 800;
  italico?: boolean;
  caixaAlta?: boolean;
  // Em em, como no JSON.
  espacamentoLetras?: number;
  // Vem do uso em tipografia.display no JSON: "pontos em itálico 800 com algarismos tabulares".
  algarismosTabulares?: boolean;
}

export const tipografia = {
  display: { familia: "Archivo", tamanho: 28, alturaLinha: 32, peso: 800 },
  titulo: { familia: "Archivo", tamanho: 22, alturaLinha: 26, peso: 700 },
  pontos: {
    familia: "Archivo",
    tamanho: 18,
    peso: 800,
    italico: true,
    algarismosTabulares: true,
  },
  secao: { familia: "Archivo", tamanho: 15, alturaLinha: 19, peso: 700 },
  corpo: {
    familia: "Instrument Sans",
    tamanho: 14,
    alturaLinha: 20,
    peso: 400,
  },
  legenda: {
    familia: "Instrument Sans",
    tamanho: 12,
    alturaLinha: 16,
    peso: 500,
  },
  rotulo: {
    familia: "Instrument Sans",
    tamanho: 11,
    alturaLinha: 14,
    peso: 600,
    caixaAlta: true,
    espacamentoLetras: 0.04,
  },
} as const satisfies Record<string, EstiloDeTexto>;
