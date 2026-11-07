// As formas que o domínio confere (PoliticaDeSenha, PrefixoDeArroba, Usuario), repetidas
// aqui só para responder na hora e não gastar consulta à toa. Quem decide é a API.

// Conta caractere, não unidade de UTF-16, como o domínio: um emoji é um.
const tamanho = (texto: string) => Array.from(texto).length;

export function erroDaSenha(senha: string): string | undefined {
  const n = tamanho(senha);
  return n < 8 || n > 128
    ? "A senha precisa ter de 8 a 128 caracteres."
    : undefined;
}

const FORMA_DO_PREFIXO = /^[A-Za-z][A-Za-z0-9]{2,29}$/;

export function erroDoPrefixo(prefixo: string): string | undefined {
  return FORMA_DO_PREFIXO.test(prefixo)
    ? undefined
    : "Use de 3 a 30 letras sem acento ou números, começando por letra.";
}

export function erroDoNome(nome: string): string | undefined {
  const n = tamanho(nome.trim());
  return n < 1 || n > 60
    ? "O nome precisa ter de 1 a 60 caracteres."
    : undefined;
}
