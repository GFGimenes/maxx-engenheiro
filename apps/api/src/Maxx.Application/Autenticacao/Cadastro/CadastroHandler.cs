using Maxx.Application.Autenticacao.Termos;
using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Domain;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Application.Autenticacao.Cadastro;

// O cadastro do aluno (produto.md 2.5): RA e e-mail na mesma linha do cadastro elegível,
// um RA uma conta, arroba único pelo prefixo. Professor e instituição não passam aqui.
public sealed class CadastroHandler(
    ICadastroElegivelRepository cadastros,
    IUsuarioRepository usuarios,
    ISenhaHasher senhas,
    SessaoService sessoes,
    SugestaoDeArroba sugestoes,
    TermoDeUsoHandler termos,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<Arroba> SugerirAsync(string ra, string email, CancellationToken cancellationToken)
    {
        var (cadastro, curso) = await BuscarSemContaAsync(ra, email, cancellationToken);

        return new Arroba(
            await sugestoes.PrimeiroLivreAsync(cadastro.NomeCompleto, cancellationToken),
            Sufixo.Para(cadastro.Papeis, curso, exAluno: false));
    }

    public async Task<bool> PrefixoDisponivelAsync(string ra, string email, string prefixo, CancellationToken cancellationToken)
    {
        Validar(() => PrefixoDeArroba.Validar(prefixo), CodigosDeRecusa.PrefixoInvalido);
        await BuscarSemContaAsync(ra, email, cancellationToken);

        return (await usuarios.PrefixosOcupadosAsync([prefixo], cancellationToken)).Count == 0;
    }

    public async Task<Sessao> CadastrarAsync(NovoCadastro pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        Validar(() => PoliticaDeSenha.Validar(pedido.Senha), CodigosDeRecusa.SenhaForaDaPolitica);
        Validar(() => PrefixoDeArroba.Validar(pedido.Prefixo), CodigosDeRecusa.PrefixoInvalido);
        Validar(() => Usuario.ValidarNomeDeExibicao(pedido.NomeDeExibicao), CodigosDeRecusa.NomeDeExibicaoInvalido);

        var termo = await termos.ConferirAceiteAsync(pedido.VersaoDoTermo, cancellationToken);
        var (cadastro, curso) = await BuscarSemContaAsync(pedido.Ra, pedido.Email, cancellationToken);
        if ((await usuarios.PrefixosOcupadosAsync([pedido.Prefixo], cancellationToken)).Count > 0)
        {
            throw PrefixoIndisponivel();
        }

        var usuario = Usuario.CadastrarAluno(
            cadastro, curso, pedido.NomeDeExibicao, pedido.Prefixo, senhas.Hash(pedido.Senha), relogio.GetUtcNow());
        usuario.AceitarTermo(termo, relogio.GetUtcNow());
        usuarios.Adicionar(usuario);

        // Aluno novo muda o divisor da raridade (ADR 0005): o recálculo grava junto.
        fila.Enfileirar(new RecalcularRaridade());
        var sessao = sessoes.Abrir(usuario);

        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.ContaPorCadastro)
        {
            throw ContaExistente();
        }
        catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.Prefixo)
        {
            throw PrefixoIndisponivel();
        }

        return sessao;
    }

    // Par que não está na mesma linha, RA de outra pessoa, e-mail de professor: a mesma
    // resposta, para não dizer qual dos dois errou. RA que já tem conta vai para a
    // recuperação de senha.
    private async Task<(CadastroElegivel Cadastro, Curso Curso)> BuscarSemContaAsync(
        string ra, string email, CancellationToken cancellationToken)
    {
        var encontrado = await cadastros.BuscarAlunoAsync(ra.Trim(), email.Trim(), cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDeRecusa.CadastroNaoEncontrado);

        if (await usuarios.ExisteParaCadastroAsync(encontrado.Cadastro.Id, cancellationToken))
        {
            throw ContaExistente();
        }

        return encontrado;
    }

    private static RecusaException ContaExistente() => new(TipoDeRecusa.Conflito, CodigosDeRecusa.ContaExistente);

    private static RecusaException PrefixoIndisponivel() => new(TipoDeRecusa.Conflito, CodigosDeRecusa.PrefixoIndisponivel);

    // A regra continua no domínio; aqui só ganha o código que o cliente entende.
    private static void Validar(Action regra, string codigo)
    {
        try
        {
            regra();
        }
        catch (DomainException excecao)
        {
            throw new RecusaException(TipoDeRecusa.Invalido, codigo, excecao.Message);
        }
    }
}

// A senha em claro existe só neste pedido, a caminho do hash.
public sealed record NovoCadastro(string Ra, string Email, string NomeDeExibicao, string Prefixo, string Senha, int VersaoDoTermo)
{
    // Nunca imprimir a senha, nem por acidente num log de objeto.
    public override string ToString() => $"NovoCadastro {{ Ra = {Ra}, Prefixo = {Prefixo} }}";
}
