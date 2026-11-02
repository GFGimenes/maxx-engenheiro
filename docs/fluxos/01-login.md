# Login, refresh rotativo e logout

Login próprio, sem cadastro público nem login social. O aluno entra por RA ou por
e-mail; professor e instituição, por e-mail. Implementado no passo 3.

```mermaid
sequenceDiagram
  participant App as App (web ou nativo)
  participant API
  participant DB as Postgres

  App->>API: login (RA ou e-mail + senha)
  Note over API: limite de taxa no login, por replica
  API->>DB: busca o usuario pelo RA ou pelo e-mail
  API->>API: verifica a senha com Argon2id
  alt credencial invalida
    API-->>App: recusado
  else credencial valida
    API->>DB: cria a familia F e o refresh R1
    API-->>App: access curto (sub e papeis) + refresh R1
    Note over App: web guarda o refresh em cookie HttpOnly<br/>nativo guarda no secure store<br/>access fica so em memoria
  end

  App->>API: refresh com R1
  API->>DB: marca R1 como rotacionado, emite R2 na familia F
  API-->>App: access novo + refresh R2

  App->>API: refresh com R1 de novo (roubo ou replay)
  API->>DB: R1 ja rotacionado, revoga a familia F inteira
  API-->>App: 401, sessao encerrada em todos os dispositivos

  App->>API: logout
  API->>DB: revoga a familia do refresh atual
  API-->>App: sessao encerrada
```

## De onde vem cada passo

| Passo | Fonte |
|---|---|
| RA ou e-mail para aluno; só e-mail para professor e instituição | `produto.md` 2.5 |
| Limite de taxa no login, por réplica, com o limitador nativo | `decisoes/README.md` (Limite de taxa); `plano-mestre.md` passo 3 |
| Argon2id; senha nunca em texto nem em log | ADR 0006; `CLAUDE.md` (Segurança) |
| Access curto com `sub` e papéis, sem a lista de turmas do professor | ADR 0006 |
| Refresh em cookie `HttpOnly` na web e no secure store no nativo | ADR 0006; cookie first-party pela origem única, ADR 0001 |
| Rotação por família; reuso de token já rotacionado derruba a família inteira | ADR 0006; `CLAUDE.md` (Segurança) |
| Logout revogando | `plano-mestre.md` passo 3 |

O papel do usuário vem do cadastro na base, nunca do domínio do e-mail (`produto.md`
2.4). Os códigos HTTP e o texto das respostas ficam para o passo 3; o único fixado aqui
é o 401 do reuso de refresh, que vem do ADR 0006. A resposta de credencial inválida que
não permita enumerar usuários é responsabilidade registrada no mesmo ADR.
