# Post com comprovação de prêmio de participação

A reivindicação de prêmio de participação é um post com foto, e a foto é a comprovação.
Por isso o post (passo 7) vem antes do prêmio de participação (passo 8). Este diagrama
vai da foto até a aprovação ou reprovação; o que acontece depois da aprovação está em
[`04-contestacao.md`](04-contestacao.md).

```mermaid
sequenceDiagram
  participant Aluno as Aluno (app)
  participant API
  participant DB as Postgres
  participant R2 as Bucket R2
  participant Fila
  participant Aprov as Professor da turma ou instituicao

  Aluno->>Aluno: reencoda a foto em duas derivadas (feed e grade), sem EXIF
  Aluno->>API: pedido de upload (tipo e tamanho das duas)
  API->>API: valida tipo e tamanho
  API->>DB: registra upload pendente com chaves aleatorias
  API-->>Aluno: duas URLs assinadas, content-type preso na assinatura
  Aluno->>R2: PUT das duas derivadas, direto no bucket

  Aluno->>API: cria o post de reivindicacao (item, evento, chaves das fotos)
  API->>DB: confere a deduplicacao do premio
  API->>DB: confirma o upload pendente
  API->>DB: grava o post e o premio Reivindicado, com a Comprovacao
  Note over DB: post publico com marca pendente<br/>o premio ainda nao pontua
  API-->>Aluno: post criado

  Aprov->>API: abre a fila de comprovacoes
  API-->>Aprov: mais antigas primeiro, marca as que passaram de 7 dias
  Aprov->>API: aprova ou reprova
  Note over API: professor tem o escopo de turma checado no banco
  alt chegou primeiro e aprovou
    API->>DB: premio Valido, pontua na hora, abre a janela de 15 dias
    API->>DB: grava quem aprovou e quando
    API->>Fila: notificacao de aprovacao e recalculo de ranking
    API-->>Aprov: aprovado
  else chegou primeiro e reprovou, com motivo
    API->>DB: premio reprovado, post perde o vinculo e fica como post comum
    API->>DB: grava quem reprovou, quando e o motivo
    API->>Fila: notificacao de reprovacao
    API-->>Aprov: reprovado
    Note over Aluno: para tentar de novo, publica outro post<br/>de reivindicacao com outra foto
  else outro aprovador ja decidiu
    API-->>Aprov: a comprovacao ja foi decidida
  end
```

## De onde vem cada passo

| Passo | Fonte |
|---|---|
| Duas derivadas geradas no cliente (feed ~1080², grade ~320²), sem metadado de localização | ADR 0003 |
| API valida tipo e tamanho antes de assinar; assinatura prende o `content-type`; foto nunca passa pela API | ADR 0002; `CLAUDE.md` (Segurança) |
| Chave de objeto aleatória, porque a leitura é pública para quem tem o link | ADR 0002; `produto.md` 9.2 |
| Upload pendente registrado e confirmado ao criar o post; limpeza do abandonado na fila | ADR 0002; `plano-mestre.md` passo 7 |
| Um prêmio por evento; evento em texto livre bloqueia o mesmo item na mesma data; apresentação em sala deduplica por item, data e matéria | `produto.md` 4.6 |
| Post público desde o envio, com marca "pendente", sem pontuar | `produto.md` 4.7 e 9.1 |
| Aprovam o professor de qualquer turma do aluno no semestre vigente ou a instituição; vale quem chega primeiro | `produto.md` 4.7 |
| Fila ordenada por mais antigo, com marca após 7 dias, sem decisão automática | `produto.md` 4.7 e 12.6 |
| Escopo de turma do professor checado no banco, nunca lido do token | ADR 0006; `CLAUDE.md` (Segurança) |
| Aprovado pontua na hora e abre a janela de 15 dias | `produto.md` 4.7 |
| Reprovação com motivo; nova tentativa é outro post de reivindicação, com outra foto; cada reivindicação tem um post e uma foto só | `produto.md` 4.7; `plano-mestre.md` passo 8 |
| Reprovado: o post perde o vínculo com o prêmio e permanece como post comum | `produto.md` 4.7 (parágrafo final) |
| Auditoria de aprovação e reprovação | `produto.md` 4.10 |
| Notificação de aprovação e reprovação, assíncrona | `produto.md` 8; ADR 0008 |
| Recálculo de ranking enfileirado pela aprovação, na mesma transação | ADR 0005 (eventos que enfileiram o recálculo); ADR 0008 |
| `Comprovacao` filtrável por evento, pensando no lote da fase 2 | `produto.md` 13 |

A instituição também pode conceder prêmio de participação direto, já aprovado e sem
comprovação (`produto.md` 4.7). Esse caminho não tem foto nem fila e fica fora deste
diagrama.
