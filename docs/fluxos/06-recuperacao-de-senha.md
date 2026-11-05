# Recuperação de senha e primeiro acesso

O mesmo fluxo serve a dois casos: o aluno que esqueceu a senha e o primeiro acesso de
professor e instituição, cuja conta nasce do seed sem senha. Implementado no passo 3; a
tela chega no passo 5.

```mermaid
sequenceDiagram
  participant App as App (web ou nativo)
  participant API
  participant DB as Postgres
  participant Envio as Envio em segundo plano
  participant SMTP

  App->>API: pede recuperacao (e-mail)
  Note over API: limite de taxa por e-mail, por replica
  API->>DB: busca o usuario pelo e-mail
  alt usuario existe
    API->>DB: grava o hash do token T, validade de 30 minutos
    API->>Envio: enfileira o e-mail com o link de T
  end
  API-->>App: 202, a mesma resposta exista o e-mail ou nao

  Envio->>SMTP: envia o link com T
  Note over Envio: fora da requisicao; fila em memoria ate o passo 6

  App->>API: confirma (T + senha nova)
  API->>DB: busca o token pelo hash de T
  alt T inexistente, expirado, ja usado ou invalidado
    API-->>App: recusado
  else T valido
    API->>API: hash da senha nova com Argon2id
    API->>DB: grava a senha, marca T como usado, revoga todas as familias de refresh
    API->>DB: invalida os outros pedidos pendentes do usuario
    API-->>App: senha redefinida, entrar de novo
  end
```

## De onde vem cada passo

| Passo | Fonte |
|---|---|
| Recuperação por e-mail desde a fase 1 | `produto.md` 2.5 |
| Primeiro acesso de professor e instituição por este fluxo | `produto.md` 2.5; `decisoes/README.md` (Autenticação) |
| Mesma resposta exista o e-mail ou não | ADR 0006 (enumeração de usuário); `decisoes/README.md` (Autenticação) |
| Token de 30 minutos, uso único, guardado só como hash | `decisoes/README.md` (Autenticação) |
| Limite de taxa por e-mail | `decisoes/README.md` (Autenticação) |
| Envio fora da requisição, em segundo plano até a fila do passo 6 | `decisoes/README.md` (Autenticação) |
| Senha nova revoga todas as famílias de refresh | `decisoes/README.md` (Autenticação) |
| Concluir um pedido invalida os outros pendentes do mesmo usuário: um link antigo não redefine a senha de novo | `decisoes/README.md` (Autenticação) |
| Argon2id; senha e token nunca em log | ADR 0006; `CLAUDE.md` (Segurança) |

## Códigos HTTP

Como no [`01-login.md`](01-login.md), toda recusa sai em ProblemDetails com `codigo`.

| Rota | Sucesso | Recusa |
|---|---|---|
| `POST /api/v1/auth/recuperacao` `{email}` | 202, sem corpo, sempre | 400 `dados-invalidos`; 429 `limite-de-taxa` |
| `POST /api/v1/auth/recuperacao/confirmar` `{token, senha}` | 204, sem sessão | 400 `recuperacao-invalida`; 400 `senha-fora-da-politica`; 400 `dados-invalidos` |

- **Pedido:** recebe o e-mail só quem tem conta. Aluno da base ainda sem conta, e-mail
  inexistente e aluno de demonstração recebem o mesmo 202, sem e-mail. O link vai para o
  endereço gravado na base, não para o digitado.
- **Link:** `<App__UrlPublica>/redefinir-senha#token=...`, com o token no fragmento, que
  o navegador não manda a servidor nenhum. A tela é do passo 5.
- **`recuperacao-invalida`:** token desconhecido, expirado, já usado ou invalidado por
  outro pedido concluído. Duas confirmações simultâneas do mesmo link deixam um 204 e um
  400, como em sequência.
- **`senha-fora-da-politica`:** conferida antes do token, e o link continua valendo
  para uma senha que caiba na política.
- **Sem sessão:** a senha nova acabou de derrubar todas, e o cliente leva ao login.
- **Limite:** 3 pedidos a cada 15 minutos por e-mail, exista ele ou não, com
  `Retry-After`. A confirmação não tem limite: o token tem 256 bits.
