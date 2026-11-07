import { RedefinirSenhaScreen } from "../recuperacao/RedefinirSenhaScreen";
import { tokenDoLinkDeRecuperacao } from "../recuperacao/tokenDoFragmento";

// Fora dos grupos: o link do e-mail pode ser aberto com ou sem sessão. O token foi lido
// quando a página abriu, e já saiu da URL.
export default function RedefinirSenhaRoute() {
  return <RedefinirSenhaScreen token={tokenDoLinkDeRecuperacao()} />;
}
