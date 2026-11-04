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
  alt T inexistente, expirado ou ja usado
    API-->>App: recusado
  else T valido
    API->>API: hash da senha nova com Argon2id
    API->>DB: grava a senha, marca T como usado, revoga todas as familias de refresh
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
| Argon2id; senha e token nunca em log | ADR 0006; `CLAUDE.md` (Segurança) |

Os códigos HTTP além do 202 ficam para o fechamento do passo 3, junto com os do
[`01-login.md`](01-login.md).
