import { ImageManipulator, SaveFormat } from "expo-image-manipulator";

// As duas derivadas que o app gera antes de subir a foto (ADR 0003): a do feed, em largura
// total, e a da grade de três colunas do perfil. Reencodar aqui também endireita a
// orientação da câmera e tira o metadado de localização: o GPS da foto não chega ao bucket.
// Os limites são os da API (UploadPendente); a assinatura do bucket prende o tamanho exato.
export const DERIVADAS = {
  feed: { lado: 1080, qualidade: 0.82, maximo: 1024 * 1024 },
  grade: { lado: 320, qualidade: 0.8, maximo: 200 * 1024 },
} as const;

export const TIPO_DA_FOTO = "image/jpeg";

export interface FotoEscolhida {
  uri: string;
  width: number;
  height: number;
}

export interface Arquivo {
  uri: string;
  blob: Blob;
  tamanho: number;
}

export interface Derivadas {
  feed: Arquivo;
  grade: Arquivo;
}

// A foto não coube no limite nem com a qualidade reduzida: rara, mas não pode virar erro
// genérico.
export class FotoGrandeDemais extends Error {
  constructor() {
    super("Essa foto ficou grande demais. Escolha outra.");
    this.name = "FotoGrandeDemais";
  }
}

// Lê o arquivo gerado como Blob, que é o que o PUT envia e o que dá o tamanho em bytes. O
// mesmo caminho na web (blob: ou data:) e no nativo (file://).
export async function lerArquivo(uri: string): Promise<Blob> {
  const resposta = await fetch(uri);
  return resposta.blob();
}

// Recorte quadrado no centro (decisão D2): sem editor, nas três plataformas. Foto menor que
// o lado da derivada não é ampliada.
export async function gerarDerivadas(
  foto: FotoEscolhida,
  ler: (uri: string) => Promise<Blob> = lerArquivo,
): Promise<Derivadas> {
  const lado = Math.min(foto.width, foto.height);
  const recorte = {
    originX: Math.floor((foto.width - lado) / 2),
    originY: Math.floor((foto.height - lado) / 2),
    width: lado,
    height: lado,
  };

  const gerar = async (alvo: (typeof DERIVADAS)[keyof typeof DERIVADAS]) => {
    const tamanhoFinal = Math.min(lado, alvo.lado);
    // Se a qualidade padrão estourar o limite, uma segunda tentativa mais comprimida.
    for (const qualidade of [alvo.qualidade, alvo.qualidade - 0.25]) {
      const imagem = await ImageManipulator.manipulate(foto.uri)
        .crop(recorte)
        .resize({ width: tamanhoFinal, height: tamanhoFinal })
        .renderAsync();
      const salva = await imagem.saveAsync({
        format: SaveFormat.JPEG,
        compress: qualidade,
      });
      const blob = await ler(salva.uri);
      if (blob.size > 0 && blob.size <= alvo.maximo) {
        return { uri: salva.uri, blob, tamanho: blob.size };
      }
    }
    throw new FotoGrandeDemais();
  };

  return {
    feed: await gerar(DERIVADAS.feed),
    grade: await gerar(DERIVADAS.grade),
  };
}
