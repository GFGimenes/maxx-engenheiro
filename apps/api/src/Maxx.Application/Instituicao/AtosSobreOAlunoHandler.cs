using Maxx.Application.Autenticacao;
using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Application.Mural;
using Maxx.Application.Perfil;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Application.Instituicao;

// Os atos da instituição sobre a linha do aluno (produto.md 2.1 e 2.2, passo 12): corrigir
// o e-mail, marcar ex-aluno e trocar de curso, mais a busca e a ficha que levam a eles. O
// papel vem do banco, não só do token. Cada ato grava quem fez e quando (P5). Ex-aluno e
// troca de curso mudam as visões do ranking e o divisor da raridade, e pedem os dois
// recálculos na mesma transação do ato (passo 11, proposta F).
public sealed class AtosSobreOAlunoHandler(
    IUsuarioRepository usuarios,
    IAlunosDaInstituicaoRepository alunos,
    IConsultaDaInstituicao consulta,
    IRecuperacaoDeSenhaRepository recuperacoes,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public const int LimiteDaBusca = 20;

    public async Task<IReadOnlyList<AlunoDaBase>> BuscarAsync(Guid quemId, string? termo, CancellationToken cancellationToken)
    {
        await InstituicaoAsync(usuarios, quemId, cancellationToken);
        termo = termo?.Trim() ?? string.Empty;
        if (termo.Length < 2)
        {
            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDoPerfil.BuscaSemCriterio, "Busca da instituição sem termo.");
        }

        return await consulta.BuscarAlunosAsync(termo, LimiteDaBusca, cancellationToken);
    }

    public async Task<FichaDoAluno> FichaAsync(Guid quemId, Guid cadastroId, CancellationToken cancellationToken)
    {
        await InstituicaoAsync(usuarios, quemId, cancellationToken);
        return await consulta.FichaAsync(cadastroId, cancellationToken) ?? throw AlunoNaoEncontrado(cadastroId);
    }

    // Com conta ou sem: o caso principal é o aluno que não consegue se cadastrar porque o
    // e-mail da base está errado (P3). Com conta, os pedidos de recuperação abertos são
    // invalidados, porque o link pode ter ido para o endereço errado; as sessões ficam.
    public async Task CorrigirEmailAsync(Guid quemId, Guid cadastroId, string? email, CancellationToken cancellationToken)
    {
        var quem = await InstituicaoAsync(usuarios, quemId, cancellationToken);
        var cadastro = await alunos.CadastroDeAlunoAsync(cadastroId, cancellationToken) ?? throw AlunoNaoEncontrado(cadastroId);
        var agora = relogio.GetUtcNow();

        var resultado = cadastro.CorrigirEmail(email, quem.Id, agora);
        if (resultado.Recusa is RecusaDaCorrecaoDeEmail.EmailInvalido)
        {
            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDaInstituicao.EmailInvalido, "E-mail fora da forma.");
        }

        if (resultado.Correcao is not { } correcao)
        {
            return;
        }

        if (await alunos.EmailDeOutraLinhaAsync(correcao.EmailNovo, cadastro.Id, cancellationToken))
        {
            throw EmailEmUso(cadastroId);
        }

        alunos.Adicionar(correcao);
        if (await alunos.ContaAsync(cadastro.Id, cancellationToken) is { } conta)
        {
            foreach (var pedido in await recuperacoes.PendentesDoUsuarioAsync(conta.Id, cancellationToken))
            {
                pedido.Invalidar(agora);
            }
        }

        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.EmailDoCadastro)
        {
            throw EmailEmUso(cadastroId);
        }
    }

    // Sem volta na fase 1 (P4). Pede conta: é a conta que sai do ranking do semestre.
    public async Task MarcarExAlunoAsync(Guid quemId, Guid cadastroId, CancellationToken cancellationToken)
    {
        var quem = await InstituicaoAsync(usuarios, quemId, cancellationToken);
        var conta = await ContaDoAlunoAsync(alunos, cadastroId, cancellationToken);
        if (conta.EhExAluno)
        {
            throw ExAluno(cadastroId);
        }

        conta.MarcarExAluno(quem.Id, relogio.GetUtcNow());
        fila.PedirRecalculos();
        await SalvarAsync(cadastroId, cancellationToken);
    }

    // Curso e semestre novos; as matrículas antigas ficam (P4). Com conta, o sufixo do arroba
    // segue o curso; ex-aluno não troca.
    public async Task TrocarDeCursoAsync(Guid quemId, Guid cadastroId, Guid cursoId, int semestre, CancellationToken cancellationToken)
    {
        var quem = await InstituicaoAsync(usuarios, quemId, cancellationToken);
        var cadastro = await alunos.CadastroDeAlunoAsync(cadastroId, cancellationToken) ?? throw AlunoNaoEncontrado(cadastroId);
        var conta = await alunos.ContaAsync(cadastroId, cancellationToken);
        if (conta is { EhExAluno: true })
        {
            throw ExAluno(cadastroId);
        }

        var curso = await alunos.CursoAsync(cursoId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDoMural.CursoNaoEncontrado, $"Curso {cursoId} não existe.");

        var resultado = cadastro.TrocarDeCurso(curso, semestre, quem.Id, relogio.GetUtcNow());
        if (resultado.Troca is not { } troca)
        {
            throw resultado.Recusa switch
            {
                RecusaDaTrocaDeCurso.MesmoCurso => new RecusaException(
                    TipoDeRecusa.Conflito, CodigosDaInstituicao.MesmoCurso, $"Cadastro {cadastroId} já é do curso {curso.Sigla}."),
                RecusaDaTrocaDeCurso.SemestreForaDoCurso => new RecusaException(
                    TipoDeRecusa.Invalido, CodigosDaInstituicao.SemestreInvalido, $"{curso.Sigla} não tem o semestre {semestre}."),
                _ => new InvalidOperationException($"Recusa sem tratamento: {resultado.Recusa}."),
            };
        }

        alunos.Adicionar(troca);
        conta?.TrocarDeCurso(curso);
        fila.PedirRecalculos();
        await SalvarAsync(cadastroId, cancellationToken);
    }

    // A conta que pede o ato, com o papel de instituição lido do banco.
    internal static async Task<Usuario> InstituicaoAsync(IUsuarioRepository usuarios, Guid quemId, CancellationToken cancellationToken)
    {
        var quem = await usuarios.BuscarAsync(quemId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {quemId} não existe.");
        return quem.Papeis.HasFlag(Papeis.Instituicao)
            ? quem
            : throw new RecusaException(TipoDeRecusa.Proibido, CodigosDaMedalha.ForaDoEscopo, $"{quemId} não é da instituição.");
    }

    // A conta do aluno da linha: 404 sem a linha, 409 sem conta. Prêmio e ex-aluno pedem conta.
    internal static async Task<Usuario> ContaDoAlunoAsync(IAlunosDaInstituicaoRepository alunos, Guid cadastroId, CancellationToken cancellationToken)
    {
        _ = await alunos.CadastroDeAlunoAsync(cadastroId, cancellationToken) ?? throw AlunoNaoEncontrado(cadastroId);
        return await alunos.ContaAsync(cadastroId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.Conflito, CodigosDaInstituicao.AlunoSemConta, $"Cadastro {cadastroId} sem conta.");
    }

    private async Task SalvarAsync(Guid cadastroId, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outro ato sobre a mesma conta gravou primeiro: a outra marcação de ex-aluno.
            throw ExAluno(cadastroId);
        }
    }

    private static RecusaException AlunoNaoEncontrado(Guid cadastroId) =>
        new(TipoDeRecusa.NaoEncontrado, CodigosDaInstituicao.AlunoNaoEncontrado, $"Cadastro de aluno {cadastroId} não existe.");

    private static RecusaException EmailEmUso(Guid cadastroId) =>
        new(TipoDeRecusa.Conflito, CodigosDaInstituicao.EmailEmUso, $"E-mail de outra linha, para o cadastro {cadastroId}.");

    private static RecusaException ExAluno(Guid cadastroId) =>
        new(TipoDeRecusa.Conflito, CodigosDaMedalha.ExAluno, $"Cadastro {cadastroId} já é de ex-aluno.");
}
