# Maxx · regras de produto

Documento canônico das regras de produto da fase 1. Toda sessão de trabalho no Maxx
parte deste arquivo. Decisão que não está aqui não foi tomada: pergunte antes de
assumir.

Identidade visual e telas: `design/tokens.json`, `design/identidade-visual.md`,
`design/telas/`. Catálogo de prêmios: `data/catalogo/`. Decisões técnicas:
`docs/decisoes/`. Plano de execução: `docs/plano-mestre.md`.

---

## 1. O que é o Maxx

Aplicativo da UniMax (Indaiatuba/SP, rede UniEduk, ao lado da UniFaj) para alunos,
professores e a própria instituição. É uma rede social fechada e gamificada: o aluno
conquista prêmios por mérito em aula e por participação na vida acadêmica, cada prêmio
vale pontos, os pontos definem posição em rankings, e o feed é onde as conquistas
aparecem.

A estrutura é a do Instagram, sem stories, sem reels e sem mensagem direta entre
usuários. Ninguém de fora entra: não existe cadastro público nem login social. A única
exceção é o modo de demonstração da seção 11, que existe apenas com uma flag de
configuração ligada.

A fase 1 é o TCC, avaliado por uma banca rotativa em avaliação remota, sem apresentação
presencial: cada avaliador entra pela própria conexão. A web é o acesso principal dessa
fase; o nativo fica pronto para publicar, sem publicação em loja.

---

## 2. Papéis e acesso

### 2.1 Os três papéis

| Papel | Pode |
|---|---|
| **Aluno** | Postar, reivindicar prêmio de participação, receber medalha e conquista, pontuar, aparecer nos rankings, seguir alunos, curtir, comentar, ler o mural |
| **Professor** | Conceder medalha aos alunos das suas turmas do semestre vigente, aprovar ou reprovar comprovação desses alunos, contestar prêmio desses alunos, publicar aviso para suas turmas, seguir seus alunos, curtir, comentar |
| **Instituição** | Conceder prêmio de participação já aprovado, aprovar ou reprovar comprovação, contestar, decidir contestação, anular medalha, publicar aviso de faculdade e de curso, remover post e comentário impróprios, corrigir e-mail de aluno, marcar ex-aluno, purgar dados de demonstração |

Cada aluno tem conta própria, e aluno nunca acumula papel: quem é aluno de pós e também
dá aula tem duas contas e dois arrobas, como quem tem um ID de docente e outro de
pós-graduando. O único acúmulo possível é professor e instituição na mesma conta, que
usa o sufixo `-PROF`. Na fase 1 "instituição" é um papel único; coordenação e secretaria
como sub-papéis ficam para depois, se houver necessidade.

A instituição não tem perfil social: não posta no feed, não é seguida, não aparece em
ranking. A voz dela é o mural.

O professor tem perfil, mas não pontua nem aparece em ranking. O perfil dele mostra as
matérias e turmas do semestre, os avisos que publicou e as medalhas que concedeu. Ele
segue os alunos dele para acompanhar as conquistas, não é seguido por ninguém e não
posta no feed.

### 2.2 Ex-aluno

A instituição marca o aluno como ex-aluno. A partir daí ele continua entrando,
seguindo, lendo o mural, curtindo e comentando, e mantém os posts que já tinha. Não
posta, não recebe prêmio e não pontua. Sai do ranking do semestre no instante da
marcação e permanece no ranking histórico. Nesse mesmo instante o arroba troca o sufixo
para `-EX`.

### 2.3 Identidade do usuário

| Dado | Regra |
|---|---|
| **RA** | Identidade interna do aluno e credencial de login. É dado pessoal: nunca aparece para outros alunos e não é buscável |
| **E-mail institucional** | Vem da base abastecida pela faculdade. O aluno não escolhe e não edita; só a instituição corrige, pela área web |
| **Arroba** | Identificador público e único. Formato `@primeiroNomeUltimoNome-SUFIXO`. Sufixos: a sigla do curso atual para o aluno (`@guilhermeGimenes-CC`), `-EX` para o ex-aluno, `-PROF` para professor (inclusive quando a conta acumula professor e instituição) e `-UNIMAX` para a conta só de instituição. O cadastro sugere a partir do nome; em colisão, a sugestão inclui o nome do meio quando houver e, se ainda colidir, acrescenta número sequencial. O usuário edita apenas a parte antes do hífen, no cadastro e depois no perfil. O sufixo muda sozinho quando o aluno troca de curso ou é marcado como ex-aluno, e o arroba antigo fica reservado permanentemente ao dono, sem reuso por terceiros. Unicidade sem diferenciar maiúsculas; exibição como o usuário escreveu. Regras de formação na seção 2.3.1 |
| **Nome de exibição** | Texto livre, editável, não precisa ser único |
| **Foto de perfil** | Opcional, de aluno e de professor; segue as mesmas regras de assinatura e de derivadas da foto de post. Sem foto, o avatar são as iniciais |

#### 2.3.1 Formação e unicidade do arroba

- **A unicidade é do prefixo**, a parte antes do hífen, sem diferenciar maiúsculas. O
  sufixo não conta: `@anaSouza-CC` e `@anaSouza-SI` não convivem. Por isso a troca
  automática de sufixo (curso novo ou ex-aluno) nunca colide com ninguém.
- Consequência aceita: quem tem duas contas (pós-graduando e docente) recebe prefixos
  diferentes nelas, porque a segunda cai na regra de colisão.
- **Formato do prefixo:** letras ASCII e dígitos, de 3 a 30 caracteres, começando por
  letra. Sem acento, sem cedilha, sem ponto, sem hífen (o hífen separa o sufixo).
- **Sugestão a partir do nome completo:** acentos e cedilha removidos (`João` vira
  `joao`), partículas `da`, `de`, `do`, `das`, `dos` e `e` ignoradas, primeiro nome em
  minúsculas e os seguintes com inicial maiúscula. `Maria da Silva` vira `mariaSilva`.
- Hífen e apóstrofo dentro de um nome são descartados, sem separar palavras:
  `D'Ávila` vira `Davila`, `Ana-Maria` vira `anamaria`.
- **Colisão na sugestão:** primeiro nome e último nome; se ocupado, primeiro nome, o
  primeiro nome do meio e o último nome; se ainda ocupado, primeiro e último nome com
  número sequencial a partir de 2 (`mariaSilva2`).
- **Nome curto ou longo:** sugestão com menos de 3 caracteres já sai numerada (`Li` vira
  `li2`). Sugestão com mais de 30 é cortada em 30, e o número, quando houver, entra
  dentro do limite.
- **Reserva permanente:** todo prefixo que o usuário já teve fica reservado a ele, sem
  reuso por terceiros. Vale para a troca automática de sufixo e também para a edição
  do prefixo no perfil. O dono pode voltar a um prefixo antigo seu.
- **Nome de exibição:** de 1 a 60 caracteres, contando um emoji como um. Para professor e
  instituição, que vêm do seed, nasce como o nome completo do cadastro elegível.

### 2.4 Domínios de e-mail

| Domínio | Quem |
|---|---|
| `@al.unieduk.com.br` | Aluno |
| `@prof.unieduk.com.br` | Professor |
| `@unieduk.com.br` | Interno: administração, funcionários, instituição |

A lista de domínios aceitos é configuração do seed. O papel do usuário vem do cadastro
na base, nunca do domínio do e-mail.

### 2.5 Cadastro e login

A faculdade abastece uma base de elegíveis com RA, e-mail institucional e curso de
cada aluno. Existir nessa base não é ter conta.

O cadastro exige e-mail e RA **na mesma linha** da base, mais o nome de exibição e a
escolha do arroba. Um RA aceita uma conta só: a segunda tentativa cai em recuperação
de senha. Login do aluno por RA ou por e-mail; do professor e da instituição, por
e-mail. Senha própria, com recuperação por e-mail. Verificação de e-mail no cadastro
fica para a fase 2: a base já traz o e-mail correto, e por isso a recuperação funciona
desde a fase 1.

Professores são cadastrados a cada semestre, por matéria e turma, como já acontece no
app de chamada da UniMax. Contas de instituição vêm do seed.

**Professor e instituição não se autocadastram.** O cadastro pelo app é só do aluno,
porque RA e e-mail na mesma linha são a prova de identidade dele, e professor e
instituição não têm RA obrigatório. A conta deles nasce do seed, já com arroba e sem
senha, e o primeiro acesso é pela recuperação de senha: o link que chega ao e-mail
institucional é o que prova a posse da conta. Sem isso, e sem verificação de e-mail na
fase 1, quem soubesse o e-mail de um professor tomaria a conta dele.

**Senha:** de 8 a 128 caracteres, sem regra de composição. Qualquer caractere vale.

Na fase 1 tudo isso entra por seed. No uso real, por CSV. Não há integração com o
sistema acadêmico.

### 2.6 Termo de uso

O cadastro tem uma tela simples de aceite, dentro da identidade visual. O aceite é
versionado: fica registrado qual versão o usuário aceitou e quando. O termo cobre a
exposição pública de nome, arroba, curso, prêmios e posição no ranking, e o fato de que
a foto é pública para quem tem o link (seção 9.2).

Aceitam o termo o aluno, no cadastro, e quem entra pela demonstração, no registro.
Professor e instituição não aceitam no primeiro acesso, e uma versão nova do termo não
pede novo aceite na fase 1.

---

## 3. Estrutura acadêmica e calendário

| Entidade | Regra |
|---|---|
| **Curso** | Tem sigla, usada no sufixo do arroba |
| **Ano letivo** | Dois semestres |
| **Semestre letivo** | Dois bimestres, com data de início e de fim; descontados férias e recesso. É o que corta o ranking |
| **Bimestre** | Data de início e de fim. É o que delimita as conquistas de bimestre |
| **Matéria** | Pertence a um curso |
| **Turma** | Matéria + semestre letivo. Professores vinculados por turma; alunos matriculados por turma |

A matrícula por turma é a única fonte de "quantas matérias o aluno tem no período", e
sem ela as conquistas de alvo dinâmico (todas as matérias) não existem.

Atenção a dois sentidos da palavra semestre: o **semestre letivo** (2026/2) corta
ranking e conquistas; o **semestre do aluno** (do 1º ao último semestre do curso) é
informação de perfil, vem do seed e não entra em pontuação nem em conquista: serve para
exibição e para o filtro da Busca.

Datas e horas são guardadas em UTC; as fronteiras de bimestre, semestre e ano são
interpretadas em `America/Sao_Paulo`.

---

## 4. Prêmios

### 4.1 Os três tipos

| Tipo | Quem concede | Base | Comprovação | Contestável | Anulável |
|---|---|---|---|---|---|
| **Medalha** | Professor da turma, no semestre vigente | Nota e presença: fatos que o sistema da UniMax registra | Não | Não | Sim, pela instituição |
| **Prêmio de participação** | Aluno reivindica e alguém aprova; ou a instituição concede já aprovado | Eventos, palestras, atividades opcionais, apresentação em sala | Foto, quando reivindicado | Sim, por 15 dias | Não |
| **Conquista** | O sistema, automaticamente | Acúmulo de medalhas e prêmios de participação dentro do período da própria conquista | Não | Não | Não (recalculada) |

Na interface, **troféu** é o rótulo que cobre prêmio de participação e conquista; o
glifo do medalhão é taça para troféu e fita para medalha. Nos dados, os três tipos são
distintos e nunca se misturam.

Medalha é, por definição, concessão humana do professor. Tudo que for automático é
conquista, inclusive presença, quando a integração com a chamada chegar.

Não existe medalha discricionária: nada de "participação em aula" ou "ajuda a colegas",
porque não há fato registrado por trás e é o que mais infla pontuação. Apresentação de
trabalho em sala existe, como prêmio de participação com foto.

### 4.2 Tier e pontos

O tier é o metal do medalhão e define a faixa de pontos:

| Tier | Faixa |
|---|---|
| Bronze | 1 a 30 |
| Prata | 31 a 70 |
| Ouro | 71 a 100 |
| Diamante | 101 a 150 |

Cada item do catálogo tem tier e valor **fixos**. Quem concede escolhe o item; ninguém
digita valor nem tier. Não existe sub-tier: "Diamante baixo" é um item Diamante com
valor próximo de 101.

Medalha é sempre Bronze, com uma exceção: as duas medalhas de fechamento de matéria
(média final 10 e presença integral no semestre) são Prata. A regra de fundo é fato
isolado em Bronze, acúmulo em Prata ou acima.

O prêmio guarda o tier e os pontos **do momento da concessão**, mais a versão do item
do catálogo. Reajuste de valor no catálogo não reescreve o histórico.

O aluno não tem tier: tem soma de pontos e posição numérica. Nunca pintar avatar ou
nome de aluno com cor de tier.

### 4.3 Raridade

Raridade é o segundo eixo do medalhão, mostrado como ornamento (sem anel, anel simples,
anel duplo, anel com raios) e como texto depois do tier ("Diamante · Lendário"). Os
dois eixos nunca compartilham variável visual.

Raridade é **derivada da escassez**, não é campo do catálogo: é quantos alunos ganharam
o item em relação a quantos podiam ganhar.

| Raridade | Percentual |
|---|---|
| Lendário | abaixo de 1% |
| Épico | abaixo de 5% |
| Raro | abaixo de 20% |
| Comum | 20% ou mais |

O numerador são os alunos **distintos** que têm ao menos um prêmio daquele item, ativos
ou ex-alunos; conquista repetível conta uma vez por aluno. O divisor são todos os alunos
que já passaram pela base, ativos e ex-alunos, entre os que podiam ganhar: o campus
inteiro, para item aberto; os alunos do curso, para item exclusivo de curso. Contar
ex-aluno nos dois lados é o que permite a um aluno do 3º semestre ver que percentual
consegue, por exemplo, 15 notas 10 ao longo do curso inteiro. Item que ninguém tem
aparece como Lendário com 0%. O texto exibido é "x% dos alunos".

A raridade **não congela** no prêmio: é propriedade do item, sempre a atual, fonte
única. Um prêmio Lendário hoje pode ser Comum no ano que vem, e o selo em posts antigos
acompanha. O recálculo roda no mesmo worker do ranking.

### 4.4 Famílias de trilha

Itens do catálogo podem declarar família e ordem. A trilha liga os itens obtidos da
família e mostra o próximo em círculo tracejado com cadeado. A escalada é por período e
por volume (bimestre, semestre, ano, curso), não por tier automático.

Não existe mais a regra antiga de "a mesma conquista repetida rende um prêmio de tier
acima": ela foi substituída pelos três tipos e pelas conquistas repetíveis por período.
A trilha do design continua valendo, agora como caminho de família.

### 4.5 Catálogo

O catálogo é dado versionado em `data/catalogo/`, carregado por seeder idempotente pelo
código do item (`M01`, `P03`, `C12`). São 11 medalhas, 17 prêmios de participação e 18
conquistas.

Cada item tem código, nome, descrição, tipo, tier, pontos, período de referência,
gatilho (quando conquista), família e ordem, restrição de curso quando houver, vigência
por período e regra de deduplicação.

O catálogo é global, com itens marcados por curso quando for o caso. Não existe prêmio
secreto: o aluno vê o que falta e o critério. Conquista mostra progresso ("2 de 3").
Conquistados são públicos; faltantes e progresso só para o próprio aluno.

### 4.6 Eventos

Prêmio de participação se amarra a um evento, com nome, data de início e data de fim. O
evento vem do seed ou é digitado livremente quando não houver. Atividades de duração
(iniciação científica, monitoria, extensão) são eventos com período, não com data única.

Só se reivindica evento que **já começou** (data de início até hoje, em São Paulo) e que
**começou depois do fim do último semestre letivo encerrado**: o ranking corta por semestre
letivo, e uma reivindicação retroativa pontuaria num semestre a que não pertence. O limite
é o fim do semestre anterior, e não o início do vigente, para que o evento que começa no
recesso continue reivindicável e conte para o semestre seguinte; assim a reivindicação não
para no recesso. A data de fim pode estar adiante, para a atividade de duração que ainda
corre.

Um aluno tem **um prêmio por evento**. Para evento em texto livre, o sistema bloqueia
duas reivindicações do mesmo item do catálogo na mesma data; o resto fica a cargo de
quem aprova. Exceção: apresentação de trabalho em sala deduplica por item + data +
matéria, e o aprovador natural é o professor daquela matéria.

### 4.7 Ciclo de vida do prêmio de participação

```
reivindicado ──aprovado──> válido ──contestado──> em averiguação ──confirma──> definitivo
     │                       │                          │
     │                       │                          └──revoga──> revogado
     │                       └──15 dias sem contestação──> definitivo
     └──reprovado──> post comum (nova tentativa é outro post de reivindicação)
```

| Estado | Pontua | Visível para |
|---|---|---|
| Reivindicado | Não | Todos (post com marca "pendente") |
| Válido | Sim | Todos |
| Em averiguação | Não | Aluno e instituição |
| Definitivo | Sim | Todos |
| Revogado | Não | Aluno e instituição, com o motivo |

Regras do ciclo:

- O aluno reivindica com um post com foto, que serve de comprovação e é público desde o
  envio, com marca "pendente".
- Aprovam o professor de qualquer turma do aluno no semestre vigente ou a instituição.
  A comprovação cai na fila de todos eles, com turma e aluno visíveis. **Vale quem chega
  primeiro**; não há conflito de alçada.
- Reprovada, a reivindicação se encerra: o post perde o vínculo com o prêmio e permanece
  no feed como post comum. Para tentar de novo, o aluno publica **outro post**, com outra
  foto, e reivindica de novo, quantas vezes precisar. Cada reivindicação tem um post e
  uma foto só; a foto de um post reprovado não é trocada.
- Aprovado, o prêmio pontua na hora.
- Da aprovação correm **15 dias de janela de contestação**. Contestam o professor da
  turma (inclusive quem aprovou) ou a instituição, sempre com motivo. **Uma contestação
  por prêmio.**
- Contestado, o prêmio vai a "em averiguação", para de pontuar e fica visível ao aluno
  com essa marca. Quem decide é sempre a instituição.
- Confirmado, a janela encerra na hora: o prêmio volta a pontuar, fica definitivo e não
  pode mais ser contestado.
- Revogado, sai da pontuação e fica no histórico com o motivo.
- Passados os 15 dias sem contestação, o prêmio fica definitivo.
- Só prêmio **definitivo** conta para conquista. É por isso que conquista nunca é
  revogada.
- A fila de comprovações e de contestações é ordenada por mais antigo e marca o que
  passou de 7 dias. Não há decisão automática por decurso de prazo.
- A instituição pode conceder prêmio de participação direto, já aprovado e sem
  comprovação. Nunca concede medalha.

Quando um prêmio é reprovado ou revogado, o post perde o vínculo com o prêmio e
permanece como post comum; o aluno apaga se quiser. Se o post era só o prêmio, sem foto e
sem legenda (como na concessão direta pela instituição), ele é apagado, porque post vazio
não existe. Em averiguação, o post continua
visível, sem exibir pontos nem tier.

O post de reivindicação é a comprovação: enquanto o prêmio está reivindicado, válido ou em
averiguação, o autor não o apaga, porque a foto sairia junto e a aprovação e a contestação
dependem dela. Definitivo, o post é como outro qualquer.

A instituição remove o post de reivindicação a qualquer momento e, no mesmo ato, resolve o
prêmio com o motivo "Removido pela instituição.": reprova se pendente, revoga se válido e,
se em averiguação, decide a contestação revogando. Só então o post sai. A instituição não
depende da fila para tirar do ar uma foto imprópria.

Em averiguação, para quem não é o aluno nem a instituição, o post aparece sem o prêmio: sem
pontos, sem tier e sem a marca.

### 4.8 Medalha: concessão e anulação

O professor concede escolhendo o item do catálogo. No passo 6, a concessão entra pela
lista de alunos da turma do professor; a partir do passo 7, o perfil do aluno passa a ser
outra entrada para a mesma concessão. A medalha grava
turma e matéria (do professor que concede) e bimestre (da data). Só nas turmas do
semestre vigente.

Só recebe medalha o aluno com conta. Quem está na base e matriculado na turma, mas ainda
não se cadastrou, não aparece na lista de alunos da turma do professor e não recebe
medalha; o ex-aluno também não aparece nem recebe. A medalha é sempre de uma conta, nunca
de uma linha do cadastro elegível.

Medalha não é contestável e não é revogável. A instituição pode **anular** uma medalha:
é ato administrativo, sem motivo público e sem passar por averiguação, para corrigir
erro de concessão ou nota revista. A anulação recalcula as conquistas que dependiam
daquela medalha. O post da medalha anulada perde o vínculo e fica como post comum; se ele era
só a medalha, sem foto e sem legenda, é apagado, porque post vazio não existe. O post que a
instituição já tinha removido fica como estava.

### 4.9 Conquistas

Automáticas, calculadas pelo sistema. Contam medalhas e prêmios de participação
definitivos, e **nunca contam outra conquista**: o grafo não tem ciclo e o recálculo é
simples. Se um dia houver troféu de formatura, ele é formulado em medalhas e prêmios.

A contagem **nunca atravessa o período** da conquista. "Três notas 10 no bimestre" só
olha dentro de um bimestre; "cinco no semestre" olha os dois bimestres do semestre. Duas
no bimestre 1 e uma no bimestre 2 somam três no semestre e **não** fecham a conquista de
bimestre.

Tudo soma: a conquista não substitui o que a originou. Três notas 10 em provas no
bimestre valem as três medalhas de 30 mais a conquista Prata de 40, total 130.

Períodos possíveis: bimestre, semestre, ano, curso. Cada item declara o gatilho:

| Gatilho | Quando avalia |
|---|---|
| `porFato` | A cada fato que entra na contagem |
| `fechamentoDePeriodo` | No fechamento do período, porque o alvo só existe completo (ex.: "todas as matérias do bimestre") |

Conquista de período é **repetível uma vez por instância do período**: a mesma conquista
dispara em cada bimestre que fechar o critério. No período `curso`, a instância é o
próprio aluno: a contagem é a vida dele no Maxx e não reinicia na troca de curso.

A conquista pontua no semestre **do período a que se refere**, mesmo quando calculada
depois. O prêmio de participação conta no semestre **do evento**, não no da aprovação.

Cada conquista concedida guarda as **evidências**: os prêmios que a compuseram. É o que
torna a anulação recalculável e o que responde ao aluno por que ele ganhou aquilo.

A especificação das regras é declarativa, no próprio catálogo, com três formas de
contagem. Ver `docs/regras-conquista.md`.

### 4.10 Auditoria

Toda concessão, aprovação, reprovação, contestação, decisão, revogação e anulação grava
quem fez e quando.

---

## 5. Pontuação e rankings

A pontuação do aluno é a soma dos pontos dos prêmios que pontuam. Prêmio em averiguação
ou revogado não pontua.

São quatro visões, combinando escopo e período:

| | Semestre atual | Histórico |
|---|---|---|
| **Geral** | sim | sim |
| **Por curso** | sim | sim |

- O ranking é só posição numérica. O aluno não tem tier.
- Cada tela mostra o topo e a posição do próprio aluno, com a linha dele destacada.
- O filtro entre semestre atual e histórico fica dentro da tela de ranking.
- **Desempate:** o instante da última concessão que compõe a pontuação atual. Quem parou
  de pontuar antes fica na frente.
- O ranking do semestre **congela 30 dias depois do fim do semestre**. Aprovação
  posterior ao congelamento entra só no histórico.
- Na virada do semestre, o ranking do semestre começa do zero e o histórico acumula.
- Ninguém sai do ranking: não há opção de privacidade.
- Ex-aluno aparece só no histórico.
- Aluno que troca de curso leva os pontos para o ranking do curso novo.
- O ranking é pré-calculado por worker, nas quatro visões.

---

## 6. Social

### 6.1 Posts

- Todo post precisa de pelo menos um entre prêmio, foto e legenda. Foto e legenda são
  opcionais; post vazio não existe.
- O post normalmente é sobre um prêmio: ao ganhar, o aluno pode postar só o prêmio ou o
  prêmio com foto. Um prêmio rende **um** post.
- A reivindicação de prêmio de participação é um post com foto, que serve de
  comprovação (seção 4.7).
- Post não gera ponto.
- Curtidas e comentários existem. Não existe mensagem direta.
- "Enviar" abre a folha de compartilhamento do sistema com o link do post. O link é um
  deep link `/post/:id`; quem não tem conta cai no login e, depois de entrar, chega ao
  post.
- "Salvar" não existe na fase 1.
- A instituição remove post impróprio. Denúncia pelo usuário fica para depois.
- Comentário é apagado pelo autor do comentário ou pela instituição. O dono do post não
  apaga comentário de terceiro na fase 1.
- Ex-aluno não posta, mas mantém os posts antigos.

### 6.2 Feed

Mostra sempre os posts de quem o aluno segue, mais recomendações aleatórias de outros
alunos. Aluno novo (e qualquer avaliador) vê recomendações desde o primeiro
acesso, e nunca um feed vazio.

### 6.3 Seguir

Livre, sem aprovação. Aluno segue aluno. Professor segue os alunos dele e não é seguido.
A instituição não participa.

### 6.4 Busca

Procura alunos por nome de exibição e por arroba, com filtros de curso e de semestre. O
RA não é buscável. A Busca também traz os atalhos para o ranking geral e para o ranking
do curso. Nada além disso na fase 1.

### 6.5 Perfil

Mostra nome, arroba, curso, pontos, posição e prêmios, mais a grade de posts em três
colunas sem legenda. Tocar na própria posição abre o ranking. Editar perfil altera foto,
nome de exibição e a parte do arroba antes do hífen.

---

## 7. Mural de avisos

Três escopos: faculdade, curso e turma. A instituição publica para faculdade e para
curso; o professor publica para uma ou mais turmas suas, na mesma publicação. O aluno vê
o mural combinado, filtrado pelos vínculos dele.

Aviso é texto com uma imagem opcional, somente leitura: não tem comentário, não tem
fixação e não tem expiração na fase 1. Gera notificação. A leitura é registrada, e é o
que apaga a bolinha dourada da aba Mural.

---

## 8. Notificações

Registro no banco, mostrado no sininho do cabeçalho do feed, em qualquer plataforma.
Push nativo é evolução de fase 2.

Geram notificação: medalha recebida, comprovação aprovada ou reprovada, contestação
aberta, decisão de contestação, prêmio revogado sem contestação (pela remoção do post pela
instituição, seção 4.7), aviso novo. Bolinha dourada de novidade aparece nas abas
Mural (aviso não lido) e Troféus (prêmio novo).

---

## 9. Visibilidade e privacidade

### 9.1 Quem vê o quê

| Informação | Visível para |
|---|---|
| Perfil, arroba, curso, pontos, posição, prêmios conquistados | Qualquer usuário logado |
| Faltantes e progresso de conquista | Só o próprio aluno |
| Prêmio em averiguação | Aluno e instituição |
| Prêmio revogado e o motivo | Aluno e instituição |
| RA | Ninguém além do próprio aluno e da instituição; não é buscável |
| Post de comprovação pendente | Todos, com marca "pendente" |

### 9.2 Fotos

A foto é enviada direto para o bucket pelo aplicativo e lida pela CDN. A chave é
aleatória e a leitura é **pública para quem tem o link**: decisão consciente, em troca
de cache e custo, documentada no TCC e coberta pelo termo de uso.

---

## 10. Telas e navegação

Navegação inferior com cinco abas, na ordem do design: **Home, Busca, Mural, Troféus,
Perfil**. Rankings não tem aba: abre tocando na própria posição no perfil e pelos
atalhos da Busca.

Telas desenhadas, em claro e escuro, em `design/telas/`: login, cadastro, feed, mural,
perfil, rankings, troféus.

### 10.1 Superfícies sem desenho

Seguem a identidade visual, sem prancheta:

1. Entrada do modo de demonstração no login
2. Aceite do termo de uso no cadastro
3. Busca
4. Editar perfil
5. Perfil do professor
6. Lista de turmas e alunos do professor
7. Modal de concessão de medalha
8. Área web da instituição

### 10.2 Área web da instituição

Só no navegador, pequena e funcional: fila de comprovações e fila de contestações
(aprovar, reprovar, confirmar, revogar), conceder prêmio de participação, publicar
aviso, corrigir e-mail de aluno, marcar ex-aluno, anular medalha, purgar dados de
demonstração.

Catálogo, cursos, turmas, matérias e calendário entram por seed. Não há tela de cadastro
desses dados na fase 1.

---

## 11. Modo de demonstração

Existe só na avaliação do TCC e só com uma flag de configuração ligada. A flag vem
desligada por padrão e não existe na configuração de produção; desligada, as rotas de
demonstração respondem 404 e o botão não aparece no login.

- O botão de demonstração aparece na tela de login quando a flag está ligada.
- O registro é simples: foto opcional, nome e escolha de um dos quatro cursos de
  demonstração, já populados.
- Quem entra vira um aluno de demonstração, numa turma de demonstração daquele curso,
  com tudo que um aluno tem: posta, reivindica, curte, comenta, segue, pontua, aparece
  nos rankings.
- Voltando pelo mesmo dispositivo, a pessoa pode entrar no usuário que criou ou criar
  outro. A lembrança é local, sem senha, e se perde ao trocar de aparelho.
- O aluno de demonstração entra nos mesmos rankings dos alunos semeados, e os avaliadores
  veem os posts uns dos outros.
- Dados de demonstração têm marcação por flag e por lote, para purga manual depois da
  avaliação.
- Na fase 1 existe só o mundo do seed: nenhuma conta real.
- O seed traz os professores dos quatro cursos de demonstração e a conta de instituição
  usada para conceder durante a avaliação.

Como isso se traduz no sistema:

- **Lembrança sem senha:** o registro devolve uma chave aleatória do dispositivo, que o
  app guarda localmente. O banco guarda só o hash dela. Entrar de novo é apresentar a
  chave, que abre uma sessão comum, com o mesmo refresh rotativo de qualquer usuário.
- **Cadastro do aluno de demonstração:** o registro cria uma linha no cadastro elegível
  marcada como demonstração, com RA gerado (a matrícula aponta para o cadastro
  elegível), e matricula o aluno nas turmas do curso escolhido no semestre vigente.
- **Lote:** valor de configuração, gravado em cada usuário de demonstração criado
  enquanto ele vale. É a chave da purga.
- **Foto opcional:** chega com o upload, no passo 7. Até lá, o registro é nome e curso.
- O registro de demonstração pede o aceite do termo de uso, como o cadastro.

---

## 12. Decisões assumidas de propósito

Registradas porque são as primeiras perguntas que um avaliador faz, e a resposta é "sim, de
propósito":

1. **O ranking histórico favorece quem está há mais semestres.** É um placar acumulado.
   A disputa justa é o ranking do semestre e o ranking por curso.
2. **Curso com mais matérias rende mais medalha.** Medalha é por matéria e bimestre. Sem
   normalização na fase 1; o ranking por curso é o que neutraliza.
3. **A foto é pública para quem tem o link.** Em troca de cache, custo e simplicidade.
4. **Pontos congelam na concessão.** Reajuste de catálogo não reescreve histórico.
5. **Raridade não congela.** É propriedade do item, não do prêmio.
6. **Não há decisão automática de contestação por decurso de prazo.** Prêmio parado em
   averiguação é problema de fila, resolvido por ordenação e marca de 7 dias.

---

## 13. Fora da fase 1

Aprovação e concessão em lote por evento, importação CSV, integração com o sistema de
chamada, push nativo, denúncia de post pelo usuário, salvos, sub-papéis de coordenação e
secretaria, verificação de e-mail no cadastro, publicação nas lojas.

No termo de uso, ficam pendentes a base legal da LGPD e os direitos do titular, o canal
de contato e o prazo de guarda dos dados.

A `Comprovacao` já nasce filtrável por evento, porque a aprovação em lote é a primeira
coisa que o uso real vai exigir.
