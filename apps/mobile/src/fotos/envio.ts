import type { ApiClient } from "@maxx/api-client";
import * as ImagePicker from "expo-image-picker";

import { FalhaDaApi } from "../api/FalhaDaApi";
import { TIPO_DA_FOTO, type Derivadas, type FotoEscolhida } from "./derivadas";

export type Finalidade = "post" | "fotoDePerfil";

// A galeria do aparelho, ou o seletor de arquivo na web. Sem editor: o recorte é o quadrado
// central (decisão D2). Nulo quando a pessoa desiste.
export async function escolherFoto(): Promise<FotoEscolhida | null> {
  const resultado = await ImagePicker.launchImageLibraryAsync({
    mediaTypes: ["images"],
    allowsEditing: false,
    quality: 1,
    exif: false,
  });
  const foto = resultado.canceled ? undefined : resultado.assets[0];
  return foto
    ? { uri: foto.uri, width: foto.width, height: foto.height }
    : null;
}

// O envio da foto (ADR 0002): a API assina com o tipo e o tamanho exatos de cada derivada, e
// o app envia direto ao bucket. Sem token nem cabeçalho do Maxx no PUT: o bucket não é a API.
// Devolve o id do upload, que o post ou a foto de perfil confirma depois.
export async function enviarFoto(
  client: ApiClient,
  finalidade: Finalidade,
  derivadas: Derivadas,
  put: typeof fetch = fetch,
): Promise<string> {
  const { data, error } = await client.POST("/api/v1/uploads", {
    body: {
      finalidade,
      tipoDeConteudo: TIPO_DA_FOTO,
      tamanhoDoFeed: derivadas.feed.tamanho,
      tamanhoDaGrade: derivadas.grade.tamanho,
    },
  });
  if (!data) {
    throw FalhaDaApi.doCorpo(error);
  }

  for (const [envio, arquivo] of [
    [data.feed, derivadas.feed],
    [data.grade, derivadas.grade],
  ] as const) {
    const resposta = await put(envio.url, {
      method: "PUT",
      headers: { "Content-Type": envio.tipoDeConteudo },
      body: arquivo.blob,
    });
    if (!resposta.ok) {
      // O bucket recusou (tamanho, tipo ou assinatura vencida): a mesma mensagem de quando
      // a API não acha a foto no bucket.
      throw new FalhaDaApi("foto-nao-enviada");
    }
  }

  return data.id;
}
