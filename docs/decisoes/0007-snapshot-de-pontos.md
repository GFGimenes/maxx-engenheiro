# ADR 0007 · Tier e pontos congelam na concessão; raridade não

Status: aceito · 01/11/2026

## Contexto

O catálogo é dado versionado e vai ser reajustado: um valor que se mostre alto ou baixo
demais depois do primeiro semestre de uso será corrigido. O ranking histórico, por outro
lado, é o placar acumulado da instituição e é o que a banca examina. Se o prêmio lesse o
valor atual do catálogo, mudar M01 de 30 para 25 reescreveria silenciosamente a posição de
todos os alunos de todos os semestres.

## Opções

| Opção | Peso |
|---|---|
| **Snapshot**: o prêmio guarda tier, pontos e a versão do item na concessão | **9** |
| Ler sempre do catálogo, na hora da exibição e do cálculo | 3 |

## Decisão

O prêmio guarda `tier`, `pontos` e `versaoDoItem` do momento da concessão. A **raridade
não é congelada**: ela é propriedade do item, derivada da escassez, e mostra sempre o
valor atual: um prêmio Lendário hoje pode ser Comum no ano que vem, e o selo em posts
antigos acompanha.

A assimetria é deliberada: pontos são o que o aluno ganhou, e isso é histórico; raridade é
quão difícil aquilo é **hoje**, e isso é presente.

## Consequências

- Reajuste de catálogo é seguro: nenhum ranking passado se move.
- Auditoria fica completa: o prêmio carrega o valor que valeu, não uma referência que
  mudou.
- **Negativa:** dois alunos com o mesmo prêmio podem ter pontuações diferentes, se o
  catálogo mudou entre as duas concessões. A tela precisa deixar claro que o valor exibido
  no prêmio é o da época, e o catálogo mostra o valor de agora.
- **Negativa:** corrigir um valor cadastrado errado não se resolve editando o catálogo:
  exige migração de dados explícita sobre os prêmios já concedidos, com decisão consciente
  sobre recalcular ou não o ranking.
- **Negativa:** a raridade não congelada faz o texto de um post antigo mudar sozinho com o
  tempo, o que pode confundir quem lembra de ter visto "Lendário" ali.
