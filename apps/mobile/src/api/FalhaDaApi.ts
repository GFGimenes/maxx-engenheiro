import { codigoDoErro, mensagemDaFalha, mensagemDeErro } from "./erros";

// Uma recusa da API, já com a mensagem amigável. O código fica para a tela decidir onde
// mostrar (num campo, abaixo do botão, levando a outra rota).
export class FalhaDaApi extends Error {
  constructor(readonly codigo: string | undefined) {
    super(mensagemDeErro(codigo));
    this.name = "FalhaDaApi";
  }

  static doCorpo(corpo: unknown): FalhaDaApi {
    return new FalhaDaApi(codigoDoErro(corpo));
  }
}

// O que mostrar para qualquer erro de uma chamada: a recusa da API ou a falha de rede.
export function falhaParaTela(erro: unknown): {
  codigo?: string;
  mensagem: string;
} {
  return erro instanceof FalhaDaApi
    ? { codigo: erro.codigo, mensagem: erro.message }
    : {
        codigo: erro instanceof TypeError ? "sem-conexao" : undefined,
        mensagem: mensagemDaFalha(erro),
      };
}
