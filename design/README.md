# Design do Maxx

Referência visual da fase 1: as telas, os valores de cor e tipografia e as regras da
identidade visual. Insumo do projeto, produzido antes de qualquer código.

## O que há aqui

| Pasta ou arquivo | Conteúdo |
|---|---|
| `telas/` | As seis telas (login, feed, perfil, mural, troféus e medalhas, rankings) mais cadastro, cada uma em tema claro e escuro, em PNG de 780 x 1688 (390 x 844 em escala 2x), e `padrao-visual.png` (1280 x 1400 em 2x) com a prancheta do padrão visual |
| `tokens.json` | Cores por tema, tipografia, espaçamentos, raios, estilos de tier e raridade, extraídos das telas finais |
| `identidade-visual.md` | As regras da identidade: paleta Marinho e Ouro, fontes, medalhões, navegação, estados, o que nunca fazer |
| `assets/` | Logos (`maxx-ouro.png`, `maxx-branco.png`) e selo `unimax.png` |

Telas sem desenho (entrada da demonstração, aceite de termos, busca, editar perfil,
perfil do professor, turmas do professor, modal de medalha, área web da instituição)
seguem `identidade-visual.md`.

## Procedência

As telas foram desenhadas pelo autor com o Claude Design (Anthropic) em três sessões
paralelas, em 26 e 27 de setembro de 2026, a partir de rascunhos à mão feitos em agosto:
uma sessão em que o modelo propôs o padrão visual, uma com padrão fixo e a regra de
seguir o Instagram quando houver dúvida, e uma exploração livre de identidade em três
direções, da qual saiu a paleta Marinho e Ouro. As três convergiram na primeira sessão,
que recebeu a paleta e os medalhões da terceira e a regra do Instagram da segunda.
Catorze prompts no total; a navegação inferior foi corrigida no mesmo dia 27 (o botão do
mural, antes elevado e com anel, lia como aba ativa em qualquer tela) e a versão final é
a `1790471156-05a5` dessa sessão.

Os PNGs foram renderizados das pranchetas finais com Chromium, em 390 x 844 e escala 2x;
o `tokens.json` foi extraído das variáveis CSS e dos estilos das mesmas pranchetas, e o
`identidade-visual.md` reescreve as regras do padrão visual na forma final. Montagem e
extração em 10 de outubro de 2026. A exportação completa das sessões (código-fonte das
pranchetas, componentes, fontes, histórico dos prompts) fica fora deste repositório.

Tipografia: Archivo com largura óptica 112% para títulos e números, Instrument Sans para
texto. Paleta clara: fundo `#FFFFFF`, superfície `#F3F5F9`, tinta `#0B1B33`, marinho
`#0B2A55`, ouro `#F5B400`. Paleta escura: fundo `#06142A`, superfície `#0D2142`, tinta
`#F2F5FA`, ouro `#F5B400`. Valores completos em `tokens.json`.
