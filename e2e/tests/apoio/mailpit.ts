import { expect, type APIRequestContext } from "@playwright/test";

// A caixa do Mailpit do compose, que recebe todo e-mail que a API manda. Só na máquina
// local e na CI: na VPS não há Mailpit.
const MAILPIT = `http://localhost:${process.env.MAXX_MAILPIT_PORT ?? "8025"}`;

// O link de senha nova do e-mail mais recente para este endereço. O envio é fora da
// requisição, então espera chegar.
export async function linkDeRecuperacao(
  request: APIRequestContext,
  email: string,
): Promise<string> {
  let link: string | undefined;

  await expect
    .poll(
      async () => {
        const busca = await request.get(`${MAILPIT}/api/v1/search`, {
          params: { query: `to:"${email}"` },
        });
        const { messages } = (await busca.json()) as {
          messages: { ID: string }[];
        };
        if (messages.length === 0) {
          return undefined;
        }

        // A busca vem da mais nova para a mais antiga.
        const mensagem = await request.get(
          `${MAILPIT}/api/v1/message/${messages[0]!.ID}`,
        );
        const { Text } = (await mensagem.json()) as { Text: string };
        link = Text.match(/https?:\/\/\S+\/redefinir-senha#token=[\w-]+/)?.[0];
        return link;
      },
      { timeout: 20_000, message: `e-mail de recuperação para ${email}` },
    )
    .toBeDefined();

  return link!;
}

export async function quantosEmails(
  request: APIRequestContext,
  email: string,
): Promise<number> {
  const busca = await request.get(`${MAILPIT}/api/v1/search`, {
    params: { query: `to:"${email}"` },
  });
  return ((await busca.json()) as { messages: unknown[] }).messages.length;
}
