import { useRouter } from "expo-router";
import { useState } from "react";
import { Image, Pressable, StyleSheet, Text, View } from "react-native";

import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { Avatar } from "../components/Avatar";
import { Icon } from "../components/Icon";
import { FaixaPremio } from "../premios/FaixaPremio";
import { SeloPremio } from "../premios/SeloPremio";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import {
  espacamento,
  tamanhos,
  type Raridade,
  type Tier,
} from "../theme/tokens";
import { useCurtir, type Post } from "./api";
import { enviarPost } from "./compartilhar";
import { MenuDoPost, type AcaoDoPost } from "./MenuDoPost";
import {
  descreverAutor,
  descreverComentarios,
  descreverCurtidas,
  descreverMedalha,
  tempoDesde,
  usuarioDoArroba,
} from "./textos";

interface Curtidas {
  curtidas: number;
  curtiu: boolean;
}

export interface Leitor {
  id: string;
  papeis: string[];
}

interface CartaoDePostProps {
  post: Post;
  leitor: Leitor | undefined;
  // Depois de apagado ou removido: o feed some com ele; a tela do post volta.
  onRemovido?: () => void;
}

// O post no feed, como na prancheta: cabeçalho com avatar, nome, curso e semestre e o selo do
// prêmio; a foto em largura total; coração, comentário e enviar (salvar não existe na fase
// 1); a faixa do prêmio; curtidas, legenda, comentários e tempo. A posição (#29) chega com o
// ranking, no passo 11.
export function CartaoDePost({ post, leitor, onRemovido }: CartaoDePostProps) {
  const { colors } = useTheme();
  const router = useRouter();
  const curtir = useCurtir(post.id);
  // A curtida da tela vale enquanto o post não for relido: ela guarda de que contagem do
  // servidor partiu, e o post relido com outra contagem (outra página, outra tela) manda.
  const [local, setLocal] = useState<{
    base: Curtidas;
    valor: Curtidas;
  } | null>(null);
  const doServidor = { curtidas: post.curtidas, curtiu: post.curtiu };
  const curtida =
    local &&
    local.base.curtidas === doServidor.curtidas &&
    local.base.curtiu === doServidor.curtiu
      ? local.valor
      : doServidor;
  const setCurtida = (valor: Curtidas) => setLocal({ base: doServidor, valor });
  const [aviso, setAviso] = useState<string | null>(null);
  const [menuAberto, setMenuAberto] = useState(false);

  const usuario = usuarioDoArroba(post.autor.arroba);
  const acao: AcaoDoPost | null =
    leitor?.id === post.autor.id
      ? "apagar"
      : leitor?.papeis.includes("instituicao")
        ? "remover"
        : null;
  // A instituição não curte nem comenta (produto.md 2.1).
  const interage =
    !!leitor &&
    (leitor.papeis.includes("aluno") || leitor.papeis.includes("professor"));
  const premio = post.premio;
  const glifo = premio?.tipo === "medalha" ? "fita" : "taca";

  const alternarCurtida = () => {
    const anterior = curtida;
    const vaiCurtir = !curtida.curtiu;
    setAviso(null);
    setCurtida({
      curtiu: vaiCurtir,
      curtidas: curtida.curtidas + (vaiCurtir ? 1 : -1),
    });
    curtir.mutate(vaiCurtir, {
      onSuccess: (estado) => setCurtida(estado),
      onError: () => {
        setCurtida(anterior);
        setAviso("Não deu para curtir agora. Tente de novo.");
      },
    });
  };

  const enviar = async () => {
    setAviso(null);
    try {
      if ((await enviarPost(post.id)) === "copiado") {
        setAviso("Link do post copiado.");
      }
    } catch {
      setAviso("Não deu para compartilhar agora. Tente de novo.");
    }
  };

  const abrirComentarios = () => router.push(`/post/${post.id}/comentarios`);

  return (
    <View
      testID={`post-${post.id}`}
      style={[styles.cartao, { borderBottomColor: colors.line }]}
    >
      <View style={styles.cabecalho}>
        <Avatar
          nome={post.autor.nomeDeExibicao}
          foto={post.autor.foto}
          size={44}
        />
        <View style={styles.quem}>
          <Text numberOfLines={1}>
            <Text style={[textStyles.secao, { color: colors.ink }]}>
              {usuario}
            </Text>
            <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
              {`  ${descreverAutor(post.autor)}`}
            </Text>
          </Text>
          {premio && (
            <View style={styles.selo}>
              <SeloPremio
                nome={premio.nome}
                tier={premio.tier as Tier}
                glifo={glifo}
              />
            </View>
          )}
        </View>
        {acao && (
          <Pressable
            role="button"
            accessibilityLabel="Mais opções"
            hitSlop={8}
            onPress={() => setMenuAberto(true)}
            style={styles.botaoDeIcone}
          >
            <Icon name="opcoes" color={colors.ink} size={24} />
          </Pressable>
        )}
      </View>

      {post.foto && (
        <Image
          testID="foto-do-post"
          source={{ uri: post.foto.feed }}
          style={styles.foto}
          accessibilityIgnoresInvertColors
          accessibilityLabel={`Foto de ${usuario}`}
        />
      )}

      <View style={styles.acoes}>
        {interage && (
          <Pressable
            role="button"
            accessibilityLabel={curtida.curtiu ? "Descurtir" : "Curtir"}
            aria-pressed={curtida.curtiu}
            onPress={alternarCurtida}
            style={styles.botaoDeIcone}
          >
            <Icon
              name="heart"
              size={tamanhos.iconeNav}
              filled={curtida.curtiu}
              color={curtida.curtiu ? colors.like : colors.ink}
            />
          </Pressable>
        )}
        <Pressable
          role="button"
          accessibilityLabel="Comentários"
          onPress={abrirComentarios}
          style={styles.botaoDeIcone}
        >
          <Icon name="message" size={tamanhos.iconeNav} color={colors.ink} />
        </Pressable>
        <Pressable
          role="button"
          accessibilityLabel="Enviar"
          onPress={() => void enviar()}
          style={styles.botaoDeIcone}
        >
          <Icon name="send" size={tamanhos.iconeNav} color={colors.ink} />
        </Pressable>
      </View>

      <View style={styles.texto}>
        {premio && (
          <FaixaPremio
            nome={premio.nome}
            tier={premio.tier as Tier}
            raridade={(premio.raridade?.nivel as Raridade | undefined) ?? null}
            glifo={glifo}
            pontos={premio.pontos}
            detalhe={descreverMedalha(premio)}
          />
        )}
        {descreverCurtidas(curtida.curtidas) && (
          <Text style={[textStyles.secao, { color: colors.ink }]}>
            {descreverCurtidas(curtida.curtidas)}
          </Text>
        )}
        {post.legenda && (
          <Text style={[textStyles.corpo, { color: colors.ink }]}>
            <Text style={textStyles.secao}>{usuario}</Text>
            {` ${post.legenda}`}
          </Text>
        )}
        {descreverComentarios(post.comentarios) && (
          <Pressable role="link" onPress={abrirComentarios}>
            <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
              {descreverComentarios(post.comentarios)}
            </Text>
          </Pressable>
        )}
        <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
          {tempoDesde(post.criadoEm)}
        </Text>
        {aviso && <AvisoDeErro mensagem={aviso} />}
      </View>

      <MenuDoPost
        postId={post.id}
        acao={acao}
        aberto={menuAberto}
        onFechar={() => setMenuAberto(false)}
        onRemovido={() => {
          setMenuAberto(false);
          onRemovido?.();
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  cartao: { borderBottomWidth: 1, paddingBottom: 16 },
  cabecalho: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    paddingHorizontal: espacamento.margemLateral,
    paddingVertical: 12,
  },
  quem: { flex: 1, gap: 4 },
  selo: { alignSelf: "flex-start" },
  botaoDeIcone: {
    width: tamanhos.alvoDeToque,
    height: tamanhos.alvoDeToque,
    alignItems: "center",
    justifyContent: "center",
  },
  foto: { width: "100%", aspectRatio: 1 },
  acoes: {
    flexDirection: "row",
    paddingHorizontal: espacamento.margemLateral - 10,
    paddingTop: 4,
  },
  texto: { paddingHorizontal: espacamento.margemLateral, gap: 6 },
});
