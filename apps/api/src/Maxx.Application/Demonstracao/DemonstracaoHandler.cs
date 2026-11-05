using System.Security.Cryptography;
using Maxx.Application.Autenticacao;
using Maxx.Domain;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Application.Demonstracao;

// O modo de demonstração da apresentação (produto.md 11). Só existe com a flag ligada; a
// borda nem mapeia as rotas sem ela.
public sealed class DemonstracaoHandler(
    IDemonstracaoRepository demonstracao,
    IUsuarioRepository usuarios,
    SugestaoDeArroba sugestoes,
    SessaoService sessoes,
    ITokenGenerator tokens,
    IUnitOfWork unitOfWork,
    ConfiguracaoDaDemonstracao configuracao,
    TimeProvider relogio)
{
    // Um nome igual de outra pessoa da banca, registrado no mesmo instante, pode pegar o
    // mesmo prefixo: a gravação recusa, e a sugestão é refeita.
    private const int _tentativas = 3;

    private const string _alfabetoDoRa = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // Os cursos em que dá para entrar agora: os que têm turma no semestre vigente.
    public Task<IReadOnlyList<Curso>> CursosAsync(CancellationToken cancellationToken) =>
        demonstracao.CursosComTurmaVigenteAsync(Hoje, cancellationToken);

    public async Task<RegistroDeDemonstracao> RegistrarAsync(string nome, string siglaDoCurso, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(siglaDoCurso);

        try
        {
            nome = Usuario.ValidarNomeDeExibicao(nome);
        }
        catch (DomainException excecao)
        {
            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDeRecusa.NomeDeExibicaoInvalido, excecao.Message);
        }

        var (curso, turmas) = await demonstracao.TurmasVigentesAsync(siglaDoCurso.Trim(), Hoje, cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosDeRecusa.CursoDeDemonstracaoIndisponivel, $"Sem turma vigente em {siglaDoCurso}.");

        for (var tentativa = 1; ; tentativa++)
        {
            var chave = tokens.Generate();
            var cadastro = CadastroElegivel.AlunoDeDemonstracao(GerarRa(), nome, curso);
            var usuario = Usuario.EntrarNaDemonstracao(
                cadastro,
                curso,
                await sugestoes.PrimeiroLivreAsync(nome, cancellationToken),
                chave.Hash,
                configuracao.Lote,
                relogio.GetUtcNow());

            demonstracao.Adicionar(cadastro);
            foreach (var turma in turmas)
            {
                demonstracao.Adicionar(new Matricula(cadastro, turma));
            }

            usuarios.Adicionar(usuario);
            var sessao = sessoes.Abrir(usuario);

            try
            {
                await unitOfWork.SalvarAsync(cancellationToken);
                return new RegistroDeDemonstracao(chave.Valor, sessao);
            }
            catch (ConflitoDeUnicidadeException) when (tentativa < _tentativas)
            {
                unitOfWork.Descartar();
                (curso, turmas) = (await demonstracao.TurmasVigentesAsync(curso.Sigla, Hoje, cancellationToken))!.Value;
            }
        }
    }

    // Voltar pelo mesmo dispositivo abre uma sessão comum, com o refresh rotativo de todo
    // mundo.
    public async Task<Sessao> EntrarAsync(string chaveDoDispositivo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chaveDoDispositivo);

        var usuario = await demonstracao.BuscarPorChaveAsync(tokens.Hash(chaveDoDispositivo.Trim()), cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.ChaveDoDispositivoInvalida, "Chave do dispositivo desconhecida.");

        var sessao = sessoes.Abrir(usuario);
        await unitOfWork.SalvarAsync(cancellationToken);
        return sessao;
    }

    private DateOnly Hoje => FusoHorario.DataLocal(relogio.GetUtcNow());

    // Fora do formato numérico dos RAs da faculdade, para nunca colidir com um real.
    private static string GerarRa() => $"DEMO{RandomNumberGenerator.GetString(_alfabetoDoRa, 8)}";
}

// O lote vale para todo aluno de demonstração criado enquanto ele estiver configurado: é a
// chave da purga depois da apresentação.
public sealed record ConfiguracaoDaDemonstracao(string Lote);

// A chave do dispositivo existe só aqui, a caminho do app: o banco guarda o hash.
public sealed record RegistroDeDemonstracao(string ChaveDoDispositivo, Sessao Sessao)
{
    public override string ToString() => "RegistroDeDemonstracao { }";
}

public interface IDemonstracaoRepository
{
    Task<IReadOnlyList<Curso>> CursosComTurmaVigenteAsync(DateOnly hoje, CancellationToken cancellationToken);

    // O curso pela sigla, com as turmas dele no semestre vigente. Nulo se não houver
    // nenhuma.
    Task<(Curso Curso, IReadOnlyList<Turma> Turmas)?> TurmasVigentesAsync(
        string sigla, DateOnly hoje, CancellationToken cancellationToken);

    Task<Usuario?> BuscarPorChaveAsync(string hashDaChave, CancellationToken cancellationToken);

    void Adicionar(CadastroElegivel cadastro);

    void Adicionar(Matricula matricula);
}
