// Para onde voltar depois do login: o deep link /post/:id fica atrás do login (produto.md
// 6.1), e quem chega sem sessão entra e cai no post. Só o post e os comentários dele: nada
// de esquema, host ou "//", e um link de fora não manda ninguém para outro site nem para
// outra tela. Sair da conta em qualquer outra tela leva ao login puro.
const DO_POST = /^\/post\/[A-Za-z0-9-]+(\/comentarios)?$/;

export function destinoSeguro(voltar: unknown): string | null {
  return typeof voltar === "string" && DO_POST.test(voltar) ? voltar : null;
}

export function loginVoltandoPara(caminho: string): string {
  const destino = destinoSeguro(caminho);
  return destino ? `/login?voltar=${encodeURIComponent(destino)}` : "/login";
}
