using Maxx.Application.Fila;
using Maxx.Domain;
using Maxx.Domain.Identidade;

namespace Maxx.Application.Autenticacao.Recuperacao;

// Recuperação de senha e primeiro acesso de quem vem do seed
// (fluxos/06-recuperacao-de-senha.md).
public sealed class RecuperacaoHandler(
    IUsuarioRepository usuarios,
    IRecuperacaoDeSenhaRepository recuperacoes,
    IFamiliaDeSessaoRepository familias,
    ITokenGenerator tokens,
    ISenhaHasher senhas,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    // Quem chama responde o mesmo exista o e-mail ou não: nada aqui volta para o cliente.
    public async Task SolicitarAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        // Aluno que está na base mas ainda não criou conta não recebe nada: o caminho dele é
        // o cadastro. Aluno de demonstração também não: não tem senha, e o e-mail dele é de
        // um domínio que não existe.
        var usuario = await usuarios.BuscarPorEmailAsync(email.Trim(), cancellationToken);
        if (usuario is null || usuario.EhDemonstracao)
        {
            return;
        }

        // O pedido e o trabalho do e-mail gravam juntos. O token só nasce no worker, na hora
        // do envio: a fila leva o id do pedido, nunca um segredo.
        var recuperacao = RecuperacaoDeSenha.Solicitar(usuario.Id, relogio.GetUtcNow());
        recuperacoes.Adicionar(recuperacao);
        fila.Enfileirar(new EmailDeRecuperacao(recuperacao.Id));
        await unitOfWork.SalvarAsync(cancellationToken);
    }

    public async Task ConfirmarAsync(NovaSenha pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        try
        {
            PoliticaDeSenha.Validar(pedido.Senha);
        }
        catch (DomainException excecao)
        {
            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDeRecusa.SenhaForaDaPolitica, excecao.Message);
        }

        // Argon2id só para token que existe, e uma vez só, mesmo que a gravação se repita.
        var hashDaSenha = new Lazy<string>(() => senhas.Hash(pedido.Senha));
        try
        {
            await ConcluirAsync(pedido.Token, hashDaSenha, cancellationToken);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outro pedido usou o mesmo link entre a leitura e a gravação. Relido, ele já
            // aparece usado: a resposta é a de um link usado duas vezes em sequência.
            unitOfWork.Descartar();
            await ConcluirAsync(pedido.Token, hashDaSenha, cancellationToken);
        }
    }

    private async Task ConcluirAsync(string token, Lazy<string> hashDaSenha, CancellationToken cancellationToken)
    {
        var recuperacao = await recuperacoes.BuscarPorHashAsync(tokens.Hash(token.Trim()), cancellationToken)
            ?? throw Invalida("token desconhecido");
        var usuario = await usuarios.BuscarAsync(recuperacao.UsuarioId, cancellationToken)
            ?? throw new InvalidOperationException($"A recuperação {recuperacao.Id} cita um usuário que não existe.");

        var resultado = recuperacao.Concluir(
            usuario,
            hashDaSenha.Value,
            await familias.AtivasDoUsuarioAsync(usuario.Id, cancellationToken),
            await recuperacoes.PendentesDoUsuarioAsync(usuario.Id, cancellationToken),
            relogio.GetUtcNow());

        if (resultado != ResultadoDaRecuperacao.Concluida)
        {
            throw Invalida($"pedido {recuperacao.Id}: {resultado}");
        }

        await unitOfWork.SalvarAsync(cancellationToken);
    }

    private static RecusaException Invalida(string motivo) =>
        new(TipoDeRecusa.Invalido, CodigosDeRecusa.RecuperacaoInvalida, $"Recuperação recusada: {motivo}.");
}

// O trabalho de mandar o link de um pedido de recuperação.
public sealed record EmailDeRecuperacao(Guid RecuperacaoId) : ICargaDeTrabalho
{
    public static string Tipo => "email-de-recuperacao";
}

// No worker: emite o token do pedido, grava o hash e manda o e-mail, na transação do
// trabalho. Falha no envio desfaz o hash e volta com recuo; a retentativa emite outro token.
// Se o envio sai e a transação não fecha, o link que chegou não vale: o usuário pede outro.
public sealed class EmailDeRecuperacaoHandler(
    IUsuarioRepository usuarios,
    ICadastroElegivelRepository cadastros,
    IRecuperacaoDeSenhaRepository recuperacoes,
    ITokenGenerator tokens,
    ILinkDoApp links,
    IEmailSender emails,
    IUnitOfWork unitOfWork,
    TimeProvider relogio) : ITrabalhoHandler<EmailDeRecuperacao>
{
    public async Task ExecutarAsync(EmailDeRecuperacao carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);

        // Pedido encerrado antes do envio (outro link já trocou a senha) ou apagado com o
        // usuário: não há o que mandar.
        var recuperacao = await recuperacoes.BuscarAsync(carga.RecuperacaoId, cancellationToken);
        if (recuperacao is null || !recuperacao.EstaAberta)
        {
            return;
        }

        var usuario = await usuarios.BuscarAsync(recuperacao.UsuarioId, cancellationToken)
            ?? throw new InvalidOperationException($"A recuperação {recuperacao.Id} cita um usuário que não existe.");
        var para = await cadastros.EmailAsync(usuario.CadastroElegivelId, cancellationToken);

        var token = tokens.Generate();
        recuperacao.EmitirToken(token.Hash, relogio.GetUtcNow());
        await unitOfWork.SalvarAsync(cancellationToken);

        await emails.EnviarAsync(Mensagem(para, usuario, links.RedefinicaoDeSenha(token.Valor)), cancellationToken);
    }

    // O mesmo texto serve ao esquecimento e ao primeiro acesso.
    private static MensagemDeEmail Mensagem(string para, Usuario usuario, Uri link) => new(
        para,
        "Maxx: defina sua senha",
        $"""
        Olá, {usuario.NomeDeExibicao}.

        Para definir uma senha nova no Maxx, abra o link abaixo. Ele vale por {(int)RecuperacaoDeSenha.Validade.TotalMinutes} minutos e funciona uma vez só.

        {link}

        Se não foi você quem pediu, ignore este e-mail: sua senha atual continua valendo.
        """);
}

// Onde o app abre a tela de senha nova. A URL pública vem da configuração da borda.
public interface ILinkDoApp
{
    Uri RedefinicaoDeSenha(string token);
}

// A senha em claro existe só neste pedido, a caminho do hash.
public sealed record NovaSenha(string Token, string Senha)
{
    // Nem senha nem token num log de objeto.
    public override string ToString() => "NovaSenha { }";
}
