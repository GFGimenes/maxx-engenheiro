// Todo erro em tela vira mensagem amigável (CLAUDE.md, Interface). A API manda o código
// estável em `codigo` (decisoes/README.md, Formato de erro); aqui ele vira texto.

const MENSAGENS = {
  // Autenticação (CodigosDeRecusa.cs)
  "cadastro-nao-encontrado":
    "Não achamos esse RA com esse e-mail. Confira os dois.",
  "conta-existente":
    "Esse RA já tem conta. Use Esqueci minha senha para entrar.",
  "prefixo-invalido": "Use de 3 a 30 letras ou números, começando por letra.",
  "prefixo-indisponivel": "Esse arroba já tem dono. Tente outro.",
  "senha-fora-da-politica": "A senha precisa ter de 8 a 128 caracteres.",
  "nome-de-exibicao-invalido": "O nome precisa ter de 1 a 60 caracteres.",
  "termo-indisponivel": "Não deu para carregar o termo de uso agora.",
  "termo-desatualizado":
    "O termo de uso mudou. Leia a versão nova para continuar.",
  "credenciais-invalidas": "RA, e-mail ou senha não conferem.",
  "sessao-invalida": "Sua sessão terminou. Entre de novo.",
  "recuperacao-invalida": "Esse link não vale mais. Peça um link novo.",
  "curso-indisponivel": "Esse curso não está disponível agora. Escolha outro.",
  "registro-simultaneo":
    "Muita gente entrou com esse nome agora. Tente de novo em instantes.",
  "chave-invalida": "Essa conta de demonstração não existe mais.",
  // Medalhas (CodigosDaMedalha.cs)
  "fora-do-escopo": "Essa turma não é sua neste semestre.",
  "aluno-fora-da-turma": "Esse aluno não está nesta turma.",
  "item-nao-encontrado": "Esse prêmio não está no catálogo.",
  "ex-aluno": "Ex-aluno não recebe prêmio.",
  "fora-do-periodo": "Não há bimestre em andamento para esta turma hoje.",
  "item-fora-de-vigencia": "Esse prêmio não vale nessa data.",
  "item-de-outro-curso": "Esse prêmio é de outro curso.",
  "limite-atingido":
    "Esse aluno já recebeu essa medalha o máximo de vezes no período.",
  "medalha-nao-encontrada": "Essa medalha não existe mais.",
  "sem-conta-de-instituicao": "Só a instituição pode fazer isso.",
  // Fotos (CodigosDaFoto.cs)
  "envio-nao-permitido": "Você não pode enviar foto aqui.",
  "tipo-de-foto-nao-aceito": "Não deu para usar essa foto. Escolha outra.",
  "tamanho-de-foto-invalido": "Essa foto ficou grande demais. Escolha outra.",
  // Posts (CodigosDoPost.cs)
  "post-vazio": "Escreva uma legenda, escolha uma foto ou um prêmio.",
  "legenda-longa": "A legenda passou de 1.000 caracteres.",
  "post-nao-permitido": "Só aluno publica no feed.",
  "upload-nao-encontrado": "A foto se perdeu no caminho. Escolha de novo.",
  "upload-ja-usado": "Essa foto já está em outro post.",
  "foto-nao-enviada": "A foto não terminou de subir. Tente de novo.",
  "premio-nao-encontrado": "Esse prêmio não está mais disponível.",
  "premio-anulado": "Esse prêmio foi anulado.",
  "premio-ja-postado": "Esse prêmio já tem um post.",
  "conquista-removida": "Essa conquista foi removida.",
  "mais-de-um-premio": "Escolha um prêmio só para o post.",
  "post-nao-encontrado": "Esse post não existe mais.",
  "remocao-nao-permitida": "Você não pode apagar este post.",
  "foto-obrigatoria": "Escolha uma foto: ela é a comprovação do prêmio.",
  "comprovacao-em-andamento":
    "Esse post comprova um prêmio. Ele só sai quando o prêmio ficar definitivo.",
  "evento-obrigatorio": "Escolha um evento ou digite o nome dele.",
  "evento-ambiguo": "Escolha um evento da lista ou digite outro, não os dois.",
  "evento-invalido": "Confira o nome e as datas do evento.",
  "evento-nao-encontrado": "Esse evento não está disponível para você.",
  "evento-nao-aceita-o-item": "Esse evento não vale para esse prêmio.",
  "evento-nao-comecou": "Esse evento ainda não começou.",
  "evento-de-semestre-encerrado":
    "Esse evento é de um semestre que já terminou.",
  "materia-obrigatoria": "Escolha a matéria da apresentação.",
  "materia-nao-se-aplica": "Esse prêmio não pede matéria.",
  "materia-nao-encontrada": "Você não cursava essa matéria na data do evento.",
  "premio-ja-reivindicado": "Você já reivindicou esse prêmio para esse evento.",
  "comprovacao-nao-encontrada": "Essa comprovação não existe mais.",
  "comprovacao-ja-decidida": "Outra pessoa já decidiu essa comprovação.",
  "motivo-invalido": "Escreva o motivo, com até 500 caracteres.",
  "premio-nao-contestavel": "Só o prêmio aprovado pode ser contestado.",
  "premio-ja-contestado": "Esse prêmio já foi contestado.",
  "fora-da-janela-de-contestacao": "O prazo de 15 dias para contestar acabou.",
  "contestacao-nao-encontrada": "Essa contestação não existe mais.",
  "contestacao-ja-decidida": "Essa contestação já foi decidida.",
  // Curtida, comentário e seguir (CodigosSociais.cs)
  "interacao-nao-permitida": "A instituição não curte nem comenta.",
  "comentario-vazio": "Escreva alguma coisa antes de enviar.",
  "comentario-longo": "O comentário passou de 500 caracteres.",
  "comentario-nao-encontrado": "Esse comentário não existe mais.",
  "remocao-de-comentario-nao-permitida":
    "Você não pode apagar este comentário.",
  "usuario-nao-encontrado": "Essa pessoa não está mais no Maxx.",
  "seguir-nao-permitido": "Não dá para seguir este perfil.",
  // Perfil e busca (CodigosDoPerfil.cs)
  "busca-sem-criterio": "Digite ao menos 2 letras ou escolha um filtro.",
  "foto-de-perfil-ambigua": "Escolha trocar ou remover a foto.",
  // Mural (CodigosDoMural.cs); escopo, upload e foto repetem os da medalha e do post.
  "aviso-nao-permitido": "Você não pode publicar aviso aqui.",
  "alvos-do-aviso-invalidos": "Escolha para quem vai o aviso.",
  "curso-nao-encontrado": "Esse curso não existe mais.",
  "titulo-do-aviso-invalido": "O título precisa ter de 3 a 120 caracteres.",
  "texto-do-aviso-invalido": "O texto precisa ter de 1 a 2.000 caracteres.",
  "aviso-nao-encontrado": "Esse aviso não está mais no seu mural.",
  // Área da instituição (CodigosDaInstituicao.cs); ex-aluno, curso e busca repetem os de
  // cima.
  "aluno-nao-encontrado": "Esse aluno não está na base.",
  "aluno-sem-conta": "Esse aluno ainda não criou a conta.",
  "email-invalido": "Confira o e-mail digitado.",
  "email-em-uso": "Esse e-mail já é de outra pessoa da base.",
  "mesmo-curso": "O aluno já é desse curso.",
  "semestre-invalido": "Esse curso não tem esse semestre.",
  // Paginação (CodigosDaPaginacao.cs)
  "cursor-invalido": "Não deu para carregar mais. Puxe para atualizar.",
  // Genéricos, pelo status (DomainExceptionHandler.cs)
  "requisicao-invalida": "Confira os dados e tente de novo.",
  "dados-invalidos": "Confira os dados e tente de novo.",
  "nao-autenticado": "Sua sessão terminou. Entre de novo.",
  "sem-permissao": "Você não tem acesso a isso.",
  "nao-encontrado": "Não encontramos o que você procurou.",
  "limite-de-taxa":
    "Muitas tentativas seguidas. Espere um pouco e tente de novo.",
  indisponivel: "O Maxx está fora do ar agora. Tente de novo em instantes.",
  "erro-interno": "Não deu certo agora. Tente de novo em instantes.",
  // Do cliente: o pedido nem chegou à API.
  "sem-conexao": "Sem conexão. Confira a internet e tente de novo.",
} as const;

export type CodigoDeErro = keyof typeof MENSAGENS;

export const codigosConhecidos = Object.keys(MENSAGENS) as CodigoDeErro[];

const PADRAO = MENSAGENS["erro-interno"];

// O `codigo` de um ProblemDetails, se o corpo for um.
export function codigoDoErro(corpo: unknown): string | undefined {
  if (typeof corpo === "object" && corpo !== null && "codigo" in corpo) {
    const codigo = (corpo as { codigo: unknown }).codigo;
    return typeof codigo === "string" ? codigo : undefined;
  }
  return undefined;
}

// Código desconhecido (API mais nova que o app instalado) cai na mensagem padrão.
export function mensagemDeErro(codigo: string | undefined): string {
  return codigo !== undefined && Object.hasOwn(MENSAGENS, codigo)
    ? MENSAGENS[codigo as CodigoDeErro]
    : PADRAO;
}

// Para o catch de uma chamada: falha de rede vira "sem-conexao", o resto, a mensagem
// padrão. Nunca o erro cru.
export function mensagemDaFalha(erro: unknown): string {
  return erro instanceof TypeError ? MENSAGENS["sem-conexao"] : PADRAO;
}
