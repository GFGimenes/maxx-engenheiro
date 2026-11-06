import type { Raridade, Tier } from "../theme/tokens";

export const rotulosDeTier: Record<Tier, string> = {
  bronze: "Bronze",
  prata: "Prata",
  ouro: "Ouro",
  diamante: "Diamante",
};

export const rotulosDeRaridade: Record<Raridade, string> = {
  comum: "Comum",
  raro: "Raro",
  epico: "Épico",
  lendario: "Lendário",
};
