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
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    // Quantos candidatos de sugestão por consulta ao banco.
    private const int _loteDeSugestoes = 20;

    public async Task<Arroba> SugerirAsync(string ra, string email, CancellationToken cancellationToken)
    {
        var (cadastro, curso) = await BuscarSemContaAsync(ra, email, cancellationToken);

        foreach (var lote in PrefixoDeArroba.Sugestoes(cadastro.NomeCompleto).Chunk(_loteDeSugestoes))
        {
            var ocupados = await usuarios.PrefixosOcupadosAsync(lote, cancellationToken);
            var livre = lote.FirstOrDefault(candidato => !ocupados.Contains(candidato));
            if (livre is not null)
            {
                return new Arroba(livre, Sufixo.Para(cadastro.Papeis, curso, exAluno: false));
            }
        }

        throw new InvalidOperationException($"Nenhuma sugestão de arroba livre para o cadastro {cadastro.Id}.");
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

        var (cadastro, curso) = await BuscarSemContaAsync(pedido.Ra, pedido.Email, cancellationToken);
        if ((await usuarios.PrefixosOcupadosAsync([pedido.Prefixo], cancellationToken)).Count > 0)
        {
            throw PrefixoIndisponivel();
        }

        var usuario = Usuario.CadastrarAluno(
            cadastro, curso, pedido.NomeDeExibicao, pedido.Prefixo, senhas.Hash(pedido.Senha), relogio.GetUtcNow());
        usuarios.Adicionar(usuario);
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
public sealed record NovoCadastro(string Ra, string Email, string NomeDeExibicao, string Prefixo, string Senha)
{
    // Nunca imprimir a senha, nem por acidente num log de objeto.
    public override string ToString() => $"NovoCadastro {{ Ra = {Ra}, Prefixo = {Prefixo} }}";
}
