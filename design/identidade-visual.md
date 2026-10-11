# MAXX · identidade visual (Marinho e Ouro)

Referência para implementar as telas. Os valores estão em `tokens.json`; as telas
finais, em claro e escuro, em `telas/`; a prancheta de padrão visual em
`telas/padrao-visual.png`. Logos em `assets/`.

## Ideia

Institucional e cerimonial: o marinho dá confiança, o ouro aparece só onde há
conquista, e os selos de prêmio são medalhões metálicos, como os de uma colação. A
estrutura das telas segue o Instagram: na dúvida entre um componente de aplicativo e o
equivalente do Instagram, use o do Instagram. Foto do post em largura total, ações
coração, comentário, enviar e salvar; perfil com grade de 3 colunas sem legenda e abas
só com ícone; botões de perfil baixos e discretos.

## Temas

Claro e escuro, sempre os dois. No escuro, a ação primária vira ouro e o texto sobre
ela vira marinho; o ornamento de raridade fica claro (`rarityRing`) para não sumir no
ouro. Texto principal em `ink`, secundário em `ink2`, links em `link`. Nunca escrever
texto em ouro sobre fundo claro; para o nome do tier Ouro em texto, usar `goldText`.

## Prêmios: tier e raridade

Tier é o metal e define a faixa de pontos: Bronze 1 a 30, Prata 31 a 70, Ouro 71 a
100, Diamante 101 a 150. O medalhão é um círculo com gradiente radial do metal, borda
fina mais escura e glifo escuro (taça para troféu, fita para medalha). Raridade
(Comum, Raro, Épico, Lendário) é o ornamento em volta do medalhão, na cor
`rarityRing`: sem anel, anel simples, anel duplo, anel com raios. Os dois eixos nunca
usam a mesma variável visual. Nas listas, a raridade também aparece em texto depois
do tier ("Diamante · Lendário").

Selo compacto do post: pílula com o gradiente do metal, medalhão pequeno e o nome do
prêmio em texto escuro, na segunda linha do cabeçalho do post, abaixo do nome.
Abaixo da foto, a faixa do prêmio: medalhão, nome, tier e raridade, pontos à direita.

Progressão: a mesma conquista repetida rende um prêmio de tier acima. A trilha liga os
tiers obtidos e mostra o próximo em círculo tracejado com cadeado.

O aluno não tem tier: só pontos e posição no ranking. Nunca pintar avatar ou nome de
aluno com cor de tier. A posição (#29) aparece ao lado do nome.

## Tipografia

Archivo semi-expandida (largura óptica 112%) para números, nomes de usuário e
títulos; pontos em itálico 800 com algarismos tabulares. Instrument Sans para o
resto. Escala em `tokens.json`. Números no formato brasileiro: 15.020, #29.

## Componentes

- Botões discretos: 32 px de altura visual, raio 8, texto 13, com 44 px de área de
  toque. O Entrar do login é o único botão grande (50 px, raio 8). Estados: padrão,
  pressionado, foco (anel em `focus`), desativado.
- Campos: 48 px, raio 8, borda `fieldLine`; em foco, borda de 1.5px em `focus`;
  placeholder em `ink3`. Senha com botão de mostrar.
- Controle segmentado (Entrar / Cadastrar): trilho em `surface` com raio 10, segmento
  ativo em `segmentOn` com raio 8.
- Chips de filtro: raio 8, ativo em `primaryBg` com texto `primaryFg`, inativos em
  contorno.
- Navegação inferior: 84 px, borda superior de 1px em `line`, cinco abas na ordem
  Home, Busca, Mural (jornal), Troféus (taça), Perfil (avatar). A aba ativa tem o
  ícone preenchido em `ink` sobre um realce arredondado de 44 x 44 e raio 14 em
  `navHighlight`; as outras, ícone em contorno em `ink2`. Nenhuma aba é elevada.
  Novidade: bolinha de 8 px em `gold` no canto superior direito do ícone (Mural com
  aviso não lido, Troféus com prêmio novo), em qualquer estado.
- Não lido: bolinha dourada sempre acompanhada de texto em negrito ou da palavra
  "não lido"; o ouro sozinho não tem contraste no branco.
- Curtida preenchida em `like`, só como ícone.
- Sem sombras na interface (exceto o controle segmentado no claro); separação por
  `line` de 1px e por `surface`. Sem gradientes, exceto o metal dos prêmios.
- Ícones de traço 1.8 px em grade de 24, pontas arredondadas, estilo Lucide; 26 px na
  navegação.

## Espaçamento e forma

Grade de 4 px (4, 8, 12, 16, 24, 32). Margem lateral de toda tela: 16. Listas
terminam com 24 px de respiro antes da navegação. Raios: 8 em filtros, campos e
botões; 12 e 16 em cards; pill só nos selos de tier.

## Logo e imagens

`assets/maxx-ouro.png` sobre fundos claros e sobre marinho; `assets/maxx-branco.png`
só sobre fotos; `assets/unimax.png` é o selo da UniMax para o rodapé do login
("Rede UniEduk · UniMax e UniFaj"). Altura mínima do logo: 22 px. Avatares de exemplo
são iniciais sobre `avatarBg`/`avatarFg` até haver fotos.

## Texto

Português do Brasil, frases curtas, segunda pessoa implícita ("Editar perfil", "Ler
todos"). Sem emoji na interface.
