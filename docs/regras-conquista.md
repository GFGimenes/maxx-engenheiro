# Regras de conquista

Especificação do avaliador declarativo que decide quando uma conquista é concedida. A
regra de cada conquista é dado, não código: mora em `data/catalogo/conquistas.json`, e o
avaliador é um só.

Regras de produto por trás disto: `docs/produto.md`, seção 4.9.

---

## 1. Duas formas de contagem, não três

Ao escrever as 18 conquistas da fase 1, as três formas previstas no plano colapsaram em
duas. "Contagem distinta com alvo dinâmico" e "contagem de tipos distintos" são a mesma
forma com parâmetros diferentes: o que muda é por onde se agrupa e se o alvo é fixo ou
calculado.

| Forma | O que conta | Usada por |
|---|---|---|
| `contagem` | Quantos prêmios contáveis existem no período | C01, C02, C05, C07, C08, C09, C12, C13, C16, C17, C18 |
| `contagemDistinta` | Quantos valores distintos de um campo aparecem entre os prêmios contáveis do período | C03, C04, C06, C10, C11, C14, C15 |

A conquista é concedida quando a contagem **alcança ou passa** o alvo.

## 2. Campos da regra

```
regra: {
  forma:       "contagem" | "contagemDistinta"
  fonte:       "medalha" | "premioParticipacao" | "ambos"
  filtroItens: null (qualquer item da fonte) | ["M01", ...] (só esses códigos)
  agruparPor:  null (só em contagem) | "materia" | "bimestre" | "tipoDeEvento"
  alvo:        inteiro | "todasAsMatriculasDoPeriodo"
}
```

`fonte: "ambos"` é suportado pelo avaliador e não é usado por nenhuma conquista da fase 1.

`agruparPor` só aceita campos que o próprio prêmio carrega: `materia` e `bimestre` vêm da
medalha; `tipoDeEvento` vem do item do catálogo do prêmio de participação. O avaliador
nunca navega além do prêmio e do item dele.

`alvo: "todasAsMatriculasDoPeriodo"` resolve para o número de **matérias distintas** das
turmas em que o aluno está matriculado nos semestres que a instância cobre, e só faz
sentido com `agruparPor: "materia"`. É o que permite "todas as matérias" sem o catálogo
saber quantas matérias existem. Só contam os fatos dessas matérias. Matéria distinta, e
não matrícula, porque quem cursa a mesma matéria nos dois semestres do ano nunca fecharia
a C15 contando matrículas. Sem matrícula no período, o alvo é zero, e alvo zero **nunca**
é satisfeito (decisão do passo 9).

Conquista de `periodo: "bimestre"` só conta medalha: o prêmio de participação conta no
semestre do evento e não tem bimestre. O validador do catálogo recusa a combinação.

## 3. O que entra na contagem

Um prêmio é contável quando:

- é do tipo pedido em `fonte` e o código do item está em `filtroItens`, quando houver;
- pertence ao aluno;
- cai dentro da instância de período avaliada (seção 4);
- e, sendo prêmio de participação, **já é definitivo**: aprovado e com a janela de 15
  dias encerrada, por decurso ou por confirmação de contestação.

Conquista nunca conta outra conquista. O grafo não tem ciclo, e é por isso que o
recálculo é simples e que conquista não precisa ser revogável.

Medalha anulada deixa de ser contável no instante da anulação.

## 4. Instâncias de período

A conquista é repetível **uma vez por instância do período**: a mesma conquista dispara
em cada bimestre que fechar o critério.

| `periodo` | Instância | Fronteira |
|---|---|---|
| `bimestre` | Um bimestre do calendário | datas do bimestre |
| `semestre` | Um semestre letivo | datas do semestre |
| `ano` | Um ano letivo | datas dos dois semestres |
| `curso` | O próprio aluno | sem fronteira de data |

A instância do período `curso` é o aluno, não o vínculo dele com um curso: a contagem é a
vida inteira do aluno no Maxx e **não reinicia quando ele troca de curso**, pela mesma
razão que os pontos acompanham o aluno para o ranking do curso novo.

A C14 é a única regra do catálogo que depende do formato do calendário: usa o literal 4
para "os quatro bimestres do ano". Calendário com outro número de bimestres exige ajustar
esse alvo.

A contagem **nunca atravessa a instância**. Duas medalhas no bimestre 1 e uma no
bimestre 2 fecham C08 (cinco no semestre, se houver mais duas) e **não** fecham C02 (três
no bimestre).

A instância é determinada pelo período de referência do prêmio: a medalha carrega
bimestre e semestre; o prêmio de participação é atribuído ao semestre do evento, o
semestre letivo que contém a data de início dele; o evento que começa no recesso conta no
semestre seguinte. Enquanto o calendário não tem esse semestre seguinte, o prêmio ainda
não entra em contagem nenhuma (decisões do passo 9).

Unicidade garantida por índice: `(aluno, item, tipoDePeriodo, referenciaDoPeriodo)`, só
entre as conquistas não removidas. Quem impede o disparo duplo quando dois fatos chegam
juntos é uma trava consultiva por aluno no worker, que serializa as avaliações dele; o
índice é a rede de segurança.

## 5. Quando o avaliador roda

| `gatilho` | Quando |
|---|---|
| `porFato` | Assim que um fato se torna contável: medalha concedida, ou prêmio de participação virando definitivo |
| `fechamentoDePeriodo` | No fechamento da instância do período, porque o alvo só existe completo: não se sabe se foram "todas as matérias" antes do período terminar |

Conquista de fechamento é **reavaliada** sempre que um fato daquele período se torna
contável depois do fechamento, até o congelamento do ranking do semestre (30 dias após o
fim do semestre). É o que cobre o prêmio de participação que vira definitivo já na
virada: a conquista pontua no semestre do período a que se refere, não no da conta.

A reavaliação vale até a véspera do congelamento: no dia do fim do semestre mais 30, a
instância já está congelada. Para a instância de bimestre, conta o fim do semestre dela;
para a de ano, o fim do segundo semestre.

A avaliação acontece no worker, acionada pela fila. Nunca no caminho da requisição do
usuário.

O fechamento é um ato do worker, não uma data. A verificação de fechamentos roda de hora
em hora: fecha cada instância encerrada (meia-noite de São Paulo do dia seguinte ao fim)
que ainda não tem registro de fechamento, e abre um trabalho por aluno matriculado no
período. Uma instância encerrada sem registro ainda não está fechada. O primeiro
fechamento roda mesmo depois do congelamento: o limite vale para a reavaliação, não para o
fechamento.

## 6. Evidências e recálculo

Toda conquista concedida guarda as **evidências**: os prêmios que a compuseram. Serve
para duas coisas: responder ao aluno por que ele ganhou aquilo, e tornar o recálculo
barato.

Quando a instituição anula uma medalha, o worker reavalia apenas as conquistas que têm
aquela medalha nas evidências, sem limite de data. Se o critério deixou de ser satisfeito,
a conquista é removida, os pontos saem, e o aluno é notificado. A remoção é lógica: a
conquista fica no histórico, fora do índice de unicidade, e, se o critério voltar a ser
satisfeito, nasce outra. Se continua satisfeita, as evidências passam a ser o conjunto
atual, para a próxima anulação achar a conquista pela medalha certa. A anulação nunca
concede, e mudança de catálogo nunca remove conquista concedida.

Prêmio de participação revogado nunca chega a ser evidência, porque só conta depois de
definitivo e, depois de definitivo, não pode mais ser contestado.

## 7. Escape para regra em C#

O catálogo pode declarar `regra: { "forma": "customizada", "implementacao": "<nome>" }`,
e o avaliador delega a uma classe registrada. Nenhuma conquista da fase 1 usa isso. O
escape existe para não travar o catálogo numa regra que as duas formas não alcancem,
não para ser o caminho padrão.

Limite conhecido (passo 9): a regra customizada não declara fonte, então nenhum fato a
dispara e ela não tem progresso. Quem a usar precisa dar a ela um gatilho próprio.

## 8. Plano de teste

- **Por forma:** um teste de `contagem` e um de `contagemDistinta`, cobrindo alvo fixo e
  alvo dinâmico.
- **Por item:** um teste por conquista, com o cenário mínimo que fecha o critério e um
  cenário que fica um fato abaixo do alvo.
- **Por fronteira:** contagem que não atravessa bimestre; contagem que atravessa
  bimestres dentro do semestre; prêmio de participação antes e depois de ficar
  definitivo; medalha anulada derrubando conquista; fato que chega depois do fechamento e
  antes do congelamento.
- **Por idempotência:** dois fatos simultâneos não concedem a mesma conquista duas vezes
  na mesma instância de período.

Onde cada um mora, desde o passo 9:

| Plano | Testes |
|---|---|
| Por forma | `Maxx.Domain.Tests/Conquistas/AvaliadorDeConquistaTests.cs` |
| Por item | `Maxx.Api.IntegrationTests/Conquistas/ConquistasDoCatalogoTests.cs`, sem banco, lendo o catálogo como o seeder lê |
| Por fronteira, no domínio | `AvaliadorDeConquistaTests.cs`, `FatoContavelTests.cs` e `InstanciaENotificacaoTests.cs` |
| Por fronteira, com Postgres | `ReavaliacaoDeConquistasTests.cs` (prêmio antes e depois de definitivo), `AnulacaoEConquistasTests.cs` (medalha anulada), `FechamentoDePeriodoTests.cs` (fato depois do fechamento, até a véspera do congelamento) |
| Por idempotência | `ReavaliacaoDeConquistasTests.cs`: três consumidores ao mesmo tempo concedem uma conquista só, sem trabalho falho |
