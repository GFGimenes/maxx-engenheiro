import type { IconName } from "../components/Icon";

// O texto de cada notificação é do app, pelo tipo (decisoes/README.md, Notificação). A API
// manda só o que a frase pede: o nome do item, o motivo, a decisão, o post, o título do
// aviso.
export interface DadosDaNotificacao {
  tipo: string;
  referenciaId: string;
  item?: string | null;
  motivo?: string | null;
  decisao?: string | null;
  postId?: string | null;
  tituloDoAviso?: string | null;
}

export interface Apresentacao {
  texto: string;
  icone: IconName;
  // Para onde o toque leva (passo 10, pergunta 6); nulo, nenhum destino.
  destino: string | null;
}

// O nome do item, ou o genérico quando a referência já não existe.
const nome = (item: string | null | undefined, generico: string) =>
  item ?? generico;

// O motivo, quando há, entra depois de dois pontos, com um ponto final só.
const comMotivo = (frase: string, motivo?: string | null) =>
  motivo ? `${frase}: ${motivo.trim().replace(/\.+$/, "")}.` : `${frase}.`;

// O post do prêmio, quando ainda está no ar; senão, Troféus.
const doPost = (postId?: string | null) =>
  postId ? `/post/${postId}` : "/trofeus";

// Um tipo por linha; o teste confere que todo tipo da API tem a sua. Um tipo que o app
// instalado ainda não conhece (a API anda na frente) cai no texto genérico.
export const APRESENTACOES: Record<
  string,
  (dados: DadosDaNotificacao) => Apresentacao
> = {
  medalhaRecebida: ({ item }) => ({
    texto: `Você recebeu a medalha ${nome(item, "de um professor")}.`,
    icone: "fita",
    destino: "/trofeus",
  }),
  comprovacaoAprovada: ({ item, postId }) => ({
    texto: `Sua comprovação de ${nome(item, "prêmio")} foi aprovada.`,
    icone: "taca",
    destino: doPost(postId),
  }),
  comprovacaoReprovada: ({ item, motivo }) => ({
    texto: comMotivo(
      `Sua comprovação de ${nome(item, "prêmio")} foi reprovada`,
      motivo,
    ),
    icone: "taca",
    destino: "/trofeus",
  }),
  contestacaoAberta: ({ item, postId }) => ({
    texto: `Seu prêmio ${nome(item, "de participação")} foi contestado e está em averiguação.`,
    icone: "taca",
    destino: doPost(postId),
  }),
  decisaoDeContestacao: ({ item, decisao, motivo, postId }) => {
    const premio = nome(item, "seu prêmio");
    return {
      texto:
        decisao === "revogada"
          ? comMotivo(
              `A contestação de ${premio} terminou e o prêmio foi revogado`,
              motivo,
            )
          : `A contestação de ${premio} terminou e o prêmio foi confirmado.`,
      icone: "taca",
      destino: doPost(postId),
    };
  },
  premioRevogado: ({ item, motivo }) => ({
    texto: comMotivo(
      `Seu prêmio ${nome(item, "de participação")} foi revogado`,
      motivo,
    ),
    icone: "taca",
    destino: "/trofeus",
  }),
  conquistaObtida: ({ item }) => ({
    texto: `Você obteve a conquista ${nome(item, "nova")}.`,
    icone: "taca",
    destino: "/trofeus",
  }),
  conquistaRemovida: ({ item }) => ({
    texto: `A conquista ${nome(item, "que você tinha")} foi removida no recálculo dos seus prêmios.`,
    icone: "taca",
    destino: "/trofeus",
  }),
  avisoNovo: ({ referenciaId, tituloDoAviso }) => ({
    texto: tituloDoAviso
      ? `Aviso novo no Mural: ${tituloDoAviso}`
      : "Aviso novo no Mural.",
    icone: "newspaper",
    destino: `/aviso/${referenciaId}`,
  }),
};

export function apresentar(dados: DadosDaNotificacao): Apresentacao {
  const montar = APRESENTACOES[dados.tipo];
  return montar
    ? montar(dados)
    : { texto: "Você tem uma novidade.", icone: "inbox", destino: null };
}
