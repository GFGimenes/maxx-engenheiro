# Fluxos do Maxx

Diagramas de sequência dos seis fluxos que atravessam mais de uma peça do sistema. São
o desenho decidido, escrito antes do código: cada passo de cada diagrama aponta para a
regra de onde saiu, em `docs/produto.md`, nos ADRs ou em `docs/regras-conquista.md`.
Nenhum deles cria regra nova.

| Fluxo | Arquivo | Implementado no passo |
|---|---|---|
| Login, refresh rotativo e logout | [`01-login.md`](01-login.md) | 3 |
| Post com comprovação de prêmio de participação | [`02-post-com-comprovacao.md`](02-post-com-comprovacao.md) | 7 e 8 |
| Concessão de medalha com recálculo de conquistas | [`03-concessao-de-medalha.md`](03-concessao-de-medalha.md) | 6 e 9 |
| Contestação de prêmio de participação | [`04-contestacao.md`](04-contestacao.md) | 8 |
| Ranking | [`05-ranking.md`](05-ranking.md) | 11 |
| Recuperação de senha e primeiro acesso | [`06-recuperacao-de-senha.md`](06-recuperacao-de-senha.md) | 3 |

Convenções dos diagramas, as mesmas dos ADRs:

- O texto dentro do Mermaid vai sem acento, como nos diagramas de `docs/decisoes/`.
- "Fila" é a tabela de trabalhos do Postgres (ADR 0008). Todo enfileiramento acontece na
  mesma transação da escrita que o originou.
- "Worker" é o consumidor da fila, com `FOR UPDATE SKIP LOCKED`. Nada que ele faz roda no
  caminho da requisição do usuário.
- Toda chamada da API passa pelo Caddy, em `/api` (ADR 0001). O Caddy fica fora dos
  diagramas para não repetir o mesmo salto em todos eles.
