using System.Security.Cryptography;
using Maxx.Application.Autenticacao;
using Maxx.Application.Autenticacao.Termos;
using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
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
    TermoDeUsoHandler termos,
    ITokenGenerator tokens,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    ConfiguracaoDaDemonstracao configuracao,
    TimeProvider relogio)
{
    // Um nome igual de outra pessoa da banca, registrado no mesmo instante, pode pegar o
    // mesmo prefixo: a gravação recusa, e o registro avança para a próxima sugestão livre.
    // Cada recusa de prefixo vem de outro pedido que gravou, então 10 tentativas cobrem até
    // 10 pedidos simultâneos com o mesmo nome.
    private const int _tentativas = 10;

    private const string _alfabetoDoRa = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // Os cursos em que dá para entrar agora: os que têm turma no semestre vigente.
    public Task<IReadOnlyList<Curso>> CursosAsync(CancellationToken cancellationToken) =>
        demonstracao.CursosComTurmaVigenteAsync(Hoje, cancellationToken);

    public Task<RegistroDeDemonstracao> RegistrarAsync(
        string nome, string siglaDoCurso, int versaoDoTermo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(siglaDoCurso);

        return GravarAsync(nome, versaoDoTermo, async (identificador, nomeValido, primeira) =>
        {
            var (curso, turmas) = await TurmasAsync(siglaDoCurso, primeira, cancellationToken);
            var cadastro = CadastroElegivel.AlunoDeDemonstracao(identificador, nomeValido, curso);
            foreach (var turma in turmas)
            {
                demonstracao.Adicionar(new Matricula(cadastro, turma));
            }

            // Aluno novo muda o divisor da raridade (ADR 0005). Na nova tentativa, o
            // Descartar leva o pedido junto, e ele é feito de novo aqui.
            fila.Enfileirar(new RecalcularRaridade());
            return (cadastro, curso);
        }, cancellationToken);
    }

    // Professor de demonstração (passo 14): vinculado às turmas do curso no semestre vigente,
    // a mesma fonte do escopo de qualquer professor. Não pontua, então não mexe na raridade.
    public Task<RegistroDeDemonstracao> RegistrarProfessorAsync(
        string nome, string siglaDoCurso, int versaoDoTermo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(siglaDoCurso);

        return GravarAsync(nome, versaoDoTermo, async (identificador, nomeValido, primeira) =>
        {
            var (_, turmas) = await TurmasAsync(siglaDoCurso, primeira, cancellationToken);
            var cadastro = CadastroElegivel.ProfessorDeDemonstracao(identificador, nomeValido);
            foreach (var turma in turmas)
            {
                demonstracao.Adicionar(new VinculoProfessor(cadastro, turma));
            }

            return (cadastro, (Curso?)null);
        }, cancellationToken);
    }

    // Coordenação de demonstração (passo 14): o papel de instituição, sem curso nem turma.
    public Task<RegistroDeDemonstracao> RegistrarCoordenacaoAsync(string nome, int versaoDoTermo, CancellationToken cancellationToken) =>
        GravarAsync(
            nome,
            versaoDoTermo,
            (identificador, nomeValido, _) => Task.FromResult((CadastroElegivel.CoordenacaoDeDemonstracao(identificador, nomeValido), (Curso?)null)),
            cancellationToken);

    // O que os três registros têm em comum: nome, termo, arroba livre, chave do dispositivo,
    // lote, sessão e a linha da base. Montar cria a linha e grava o que pende dela (matrícula
    // ou vínculo), e é chamado de novo a cada tentativa, porque o Descartar leva tudo junto.
    private async Task<RegistroDeDemonstracao> GravarAsync(
        string nome,
        int versaoDoTermo,
        Func<string, string, bool, Task<(CadastroElegivel Cadastro, Curso? Curso)>> montar,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nome);

        try
        {
            nome = Usuario.ValidarNomeDeExibicao(nome);
        }
        catch (DomainException excecao)
        {
            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDeRecusa.NomeDeExibicaoInvalido, excecao.Message);
        }

        var termo = await termos.ConferirAceiteAsync(versaoDoTermo, cancellationToken);

        var recusados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var tentativa = 1; ; tentativa++)
        {
            var (cadastro, curso) = await montar(GerarIdentificador(), nome, tentativa == 1);
            demonstracao.Adicionar(cadastro);
            var prefixo = await sugestoes.PrimeiroLivreAsync(nome, recusados, cancellationToken);
            var chave = tokens.Generate();
            var usuario = Usuario.EntrarNaDemonstracao(
                cadastro,
                curso,
                prefixo,
                chave.Hash,
                configuracao.Lote,
                relogio.GetUtcNow());
            usuario.AceitarTermo(termo, relogio.GetUtcNow());
            usuarios.Adicionar(usuario);
            var sessao = sessoes.Abrir(usuario);

            try
            {
                await unitOfWork.SalvarAsync(cancellationToken);
                return new RegistroDeDemonstracao(chave.Valor, sessao);
            }
            catch (ConflitoDeUnicidadeException) when (tentativa == _tentativas)
            {
                throw new RecusaException(
                    TipoDeRecusa.Conflito, CodigosDeRecusa.RegistroSimultaneo, $"Sem arroba livre depois de {_tentativas} tentativas.");
            }
            catch (ConflitoDeUnicidadeException conflito)
            {
                unitOfWork.Descartar();
                if (conflito.Unicidade == Unicidade.Prefixo)
                {
                    recusados.Add(prefixo);
                }
            }
        }
    }

    // O curso e as turmas dele no semestre vigente. Na primeira tentativa, a falta delas é
    // recusa; nas seguintes, é a mesma consulta de novo, depois do Descartar.
    private async Task<(Curso Curso, IReadOnlyList<Turma> Turmas)> TurmasAsync(
        string siglaDoCurso, bool primeira, CancellationToken cancellationToken) =>
        await demonstracao.TurmasVigentesAsync(siglaDoCurso.Trim(), Hoje, cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado,
                CodigosDeRecusa.CursoDeDemonstracaoIndisponivel,
                primeira ? $"Sem turma vigente em {siglaDoCurso}." : $"As turmas vigentes de {siglaDoCurso} sumiram no meio do registro.");

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

    // O RA do aluno e o começo do e-mail de todos: fora do formato numérico dos RAs da
    // faculdade, para nunca colidir com um real.
    private static string GerarIdentificador() => $"DEMO{RandomNumberGenerator.GetString(_alfabetoDoRa, 8)}";
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

    void Adicionar(VinculoProfessor vinculo);
}
