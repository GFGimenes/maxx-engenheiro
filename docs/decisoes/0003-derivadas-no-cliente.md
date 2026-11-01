# ADR 0003 · As derivadas de imagem são geradas no cliente

Status: aceito · 01/11/2026

## Contexto

A foto do post ocupa 390 × 390 em largura total, e o perfil mostra uma grade de três
colunas. Uma foto de câmera de celular tem de 3 a 5 MB e dimensão muito maior do que
qualquer um dos dois usos. Servir o original para uma célula de 130 px é o que estoura o
carregamento, não a API. Como a API não toca nos bytes (ADR 0002), alguém mais precisa
gerar os tamanhos.

## Opções

| Opção | Peso |
|---|---|
| **Cliente gera e envia duas derivadas** (feed ~1080², grade ~320²) | **9** |
| Redimensionamento na borda da CDN | 6 |
| Worker que baixa, redimensiona e grava de volta | 4 |

## Decisão

O cliente reencoda a imagem antes de subir e envia duas derivadas. A API assina as duas
chaves no mesmo pedido, e o post guarda as duas. Reencodar no cliente também resolve
orientação EXIF e remove metadado de localização sem trabalho extra.

## Consequências

- Custo de servidor zero para imagem, e nenhuma dependência paga de transformação.
- O metadado de GPS da foto não chega ao bucket, o que é um ganho de privacidade que não
  teríamos se subíssemos o original.
- **Negativa:** o mesmo trabalho existe três vezes (canvas na web, biblioteca nativa no
  iOS e no Android), e qualquer divergência entre as plataformas aparece como qualidade
  de imagem diferente no mesmo feed.
- **Negativa:** confiamos no cliente para respeitar dimensão e qualidade. A assinatura
  prende tipo e tamanho máximo, não o conteúdo: um cliente adulterado pode enviar uma
  derivada de 1 px ou duas imagens diferentes. O dano é limitado à própria conta.
- **Negativa:** um terceiro tamanho no futuro não existe para as fotos já enviadas, e o
  app antigo instalado continua gerando só dois.
