using Maxx.Domain.Academico;
using Maxx.Domain.Fotos;

namespace Maxx.Domain.Identidade;

// A conta. Liga-se a uma linha do cadastro elegível, uma para uma (um RA, uma conta), e é
// de lá que vem o papel. Aluno se cadastra pelo app; professor e instituição nascem do
// seed sem senha e definem a primeira pela recuperação (produto.md 2.5). Desde o passo 12, o
// aluno do mundo da demonstração também nasce do seed, sem senha (CriarAlunoPeloSeed).
public sealed class Usuario
{
    private readonly List<PrefixoReservado> _prefixosReservados = [];
    private readonly List<AceiteDeTermo> _aceitesDeTermo = [];

    // Para o EF Core materializar do banco.
    private Usuario()
    {
        NomeDeExibicao = null!;
        Prefixo = null!;
        Sufixo = null!;
    }

    private Usuario(
        CadastroElegivel cadastro, string nomeDeExibicao, string prefixo, string sufixo, string? hashDaSenha, DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        CadastroElegivelId = cadastro.Id;
        Papeis = cadastro.Papeis;
        NomeDeExibicao = ValidarNomeDeExibicao(nomeDeExibicao);
        Prefixo = PrefixoDeArroba.Validar(prefixo);
        Sufixo = sufixo;
        HashDaSenha = hashDaSenha;
        CriadoEm = agora;
        _prefixosReservados.Add(new PrefixoReservado(Id, Prefixo, agora));
    }

    // Em caracteres, não em unidades de UTF-16, como na senha.
    public const int TamanhoMaximoDoNomeDeExibicao = 60;

    public Guid Id { get; private set; }

    public Guid CadastroElegivelId { get; private set; }

    public Papeis Papeis { get; private set; }

    public string NomeDeExibicao { get; private set; }

    // Como o usuário escreveu; a unicidade não diferencia maiúsculas.
    public string Prefixo { get; private set; }

    public string Sufixo { get; private set; }

    public Arroba Arroba => new(Prefixo, Sufixo);

    // Hash Argon2id no formato da infraestrutura. Nulo até o primeiro acesso de quem vem
    // do seed.
    public string? HashDaSenha { get; private set; }

    public bool TemSenha => HashDaSenha is not null;

    public DateTimeOffset? ExAlunoDesde { get; private set; }

    public Guid? ExAlunoMarcadoPorId { get; private set; }

    public bool EhExAluno => ExAlunoDesde is not null;

    public DateTimeOffset CriadoEm { get; private set; }

    // Só da conta de demonstração, de qualquer papel (produto.md 11): o lote da
    // apresentação, chave da purga, e o SHA-256 da chave do dispositivo, que é como ela volta
    // sem senha. A conta encerrada perde a chave.
    public string? LoteDaDemonstracao { get; private set; }

    public string? HashDaChaveDoDispositivo { get; private set; }

    // As duas derivadas da foto de perfil (produto.md 2.3: as mesmas regras de assinatura e
    // de derivadas da foto de post). Sem foto, o avatar são as iniciais.
    public string? ChaveDaFotoDoFeed { get; private set; }

    public string? ChaveDaFotoDaGrade { get; private set; }

    public bool TemFoto => ChaveDaFotoDoFeed is not null;

    public bool EhDemonstracao => LoteDaDemonstracao is not null;

    // Professor e coordenação de demonstração encerrados pela purga do lote (passo 14, P1):
    // a conta não entra mais e fica só como autora dos atos na auditoria.
    public DateTimeOffset? EncerradaEm { get; private set; }

    public bool EhEncerrada => EncerradaEm is not null;

    // Quando o usuário abriu a aba Troféus pela última vez (passo 10, pergunta 5). Prêmio
    // obtido depois disso acende a bolinha da aba; é um marcador próprio, separado do
    // sininho: abrir Troféus não lê notificação nenhuma.
    public DateTimeOffset? TrofeusVistoEm { get; private set; }

    // Todo prefixo que o usuário já teve, o atual inclusive. Nenhum volta a ser de outro.
    public IReadOnlyCollection<PrefixoReservado> PrefixosReservados => _prefixosReservados.AsReadOnly();

    public IReadOnlyCollection<AceiteDeTermo> AceitesDeTermo => _aceitesDeTermo.AsReadOnly();

    public static Usuario CadastrarAluno(
        CadastroElegivel cadastro, Curso curso, string nomeDeExibicao, string prefixo, string hashDaSenha, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(cadastro);
        ArgumentNullException.ThrowIfNull(curso);

        if (!cadastro.EhAluno)
        {
            throw new DomainException($"Só aluno se cadastra pelo app; {cadastro.Email} é {cadastro.Papeis}.");
        }

        if (cadastro.CursoId != curso.Id)
        {
            throw new DomainException($"O curso {curso.Sigla} não é o do cadastro de {cadastro.Email}.");
        }

        return new Usuario(
            cadastro,
            nomeDeExibicao,
            prefixo,
            Identidade.Sufixo.Para(cadastro.Papeis, curso, exAluno: false),
            Texto.Obrigatorio(hashDaSenha, "O hash da senha"),
            agora);
    }

    // Sem senha: entra só pela chave do dispositivo. O aluno traz o curso do cadastro; o
    // professor e a coordenação de demonstração (passo 14) não têm curso.
    public static Usuario EntrarNaDemonstracao(
        CadastroElegivel cadastro, Curso? curso, string prefixo, string hashDaChaveDoDispositivo, string lote, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(cadastro);

        if (!cadastro.Demonstracao
            || cadastro.Papeis is not (Papeis.Aluno or Papeis.Professor or Papeis.Instituicao))
        {
            throw new DomainException($"Só linha de demonstração de papel simples entra pela demonstração: {cadastro.Email}.");
        }

        if (cadastro.EhAluno && (curso is null || cadastro.CursoId != curso.Id))
        {
            throw new DomainException($"O curso {curso?.Sigla} não é o do cadastro de {cadastro.Email}.");
        }

        if (!cadastro.EhAluno && curso is not null)
        {
            throw new DomainException($"Só o aluno de demonstração tem curso; {cadastro.Email} é {cadastro.Papeis}.");
        }

        return new Usuario(
            cadastro,
            cadastro.NomeCompleto,
            prefixo,
            Identidade.Sufixo.Para(cadastro.Papeis, curso, exAluno: false),
            hashDaSenha: null,
            agora)
        {
            LoteDaDemonstracao = Texto.Obrigatorio(lote, "O lote da demonstração"),
            HashDaChaveDoDispositivo = Texto.Obrigatorio(hashDaChaveDoDispositivo, "O hash da chave do dispositivo"),
        };
    }

    public static Usuario CriarPeloSeed(CadastroElegivel cadastro, string prefixo, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(cadastro);

        if (cadastro.EhAluno)
        {
            throw new DomainException($"Aluno se cadastra pelo app, não pelo seed: {cadastro.Email}.");
        }

        return new Usuario(
            cadastro,
            cadastro.NomeCompleto,
            prefixo,
            Identidade.Sufixo.Para(cadastro.Papeis, curso: null, exAluno: false),
            hashDaSenha: null,
            agora);
    }

    // O aluno do mundo da demonstração (passo 12, P8): nasce do seed, já com conta, sem senha e
    // sem aceite do termo, com o nome completo como nome de exibição e o instante de criação
    // retroativo (o início do primeiro semestre que cursou). O e-mail dele fica num domínio
    // reservado, então ninguém entra nessa conta: ela existe para o mundo ter quem pontua,
    // posta e aparece no ranking. O aluno de verdade continua se cadastrando pelo app.
    public static Usuario CriarAlunoPeloSeed(CadastroElegivel cadastro, Curso curso, string prefixo, DateTimeOffset criadoEm)
    {
        ArgumentNullException.ThrowIfNull(cadastro);
        ArgumentNullException.ThrowIfNull(curso);

        if (!cadastro.EhAluno || cadastro.Demonstracao)
        {
            throw new DomainException($"Só aluno do mundo da demonstração nasce do seed: {cadastro.Email}.");
        }

        if (cadastro.CursoId != curso.Id)
        {
            throw new DomainException($"O curso {curso.Sigla} não é o do cadastro de {cadastro.Email}.");
        }

        return new Usuario(
            cadastro,
            cadastro.NomeCompleto,
            prefixo,
            Identidade.Sufixo.Para(cadastro.Papeis, curso, exAluno: false),
            hashDaSenha: null,
            criadoEm);
    }

    // A purga encerra, em vez de apagar, o professor e a coordenação de demonstração: o que
    // fizeram em contas fora do lote fica, com o autor (passo 14, P1). Sem a chave, ninguém
    // entra. Encerrar de novo não muda o instante.
    public void Encerrar(DateTimeOffset agora)
    {
        if (!EhDemonstracao || Papeis == Papeis.Aluno)
        {
            throw new DomainException($"Só professor e coordenação de demonstração são encerrados; @{Prefixo} não é.");
        }

        EncerradaEm ??= agora;
        HashDaChaveDoDispositivo = null;
    }

    // Primeiro acesso e recuperação de senha. A política já passou sobre a senha em claro.
    public void DefinirSenha(string hashDaSenha) => HashDaSenha = Texto.Obrigatorio(hashDaSenha, "O hash da senha");

    // Uma vez por versão: o primeiro aceite é o que vale como registro.
    public void AceitarTermo(TermoDeUso termo, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(termo);

        if (_aceitesDeTermo.All(aceite => aceite.Versao != termo.Versao))
        {
            _aceitesDeTermo.Add(new AceiteDeTermo(Id, termo.Versao, agora));
        }
    }

    public bool PossuiPrefixo(string prefixo) =>
        _prefixosReservados.Any(reservado => PrefixoDeArroba.Mesmo(reservado.Prefixo, prefixo));

    // Só a forma e a reserva do próprio dono. Se o prefixo está livre de terceiros é
    // pergunta ao banco, feita antes por quem chama e garantida pelo índice único.
    public void EditarPrefixo(string novo, DateTimeOffset agora)
    {
        novo = PrefixoDeArroba.Validar(novo);

        if (!PossuiPrefixo(novo))
        {
            _prefixosReservados.Add(new PrefixoReservado(Id, novo, agora));
        }

        Prefixo = novo;
    }

    public void EditarNomeDeExibicao(string nome) => NomeDeExibicao = ValidarNomeDeExibicao(nome);

    // A foto nova confirma o upload; a antiga, se havia, volta para quem chama tirar do
    // bucket, porque a leitura é pública para quem tem o link (ADR 0002).
    public ResultadoDaTrocaDeFoto TrocarFoto(UploadPendente upload, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(upload);

        if (upload.UsuarioId != Id || upload.Finalidade != FinalidadeDoUpload.FotoDePerfil)
        {
            return new([], RecusaDaTrocaDeFoto.UploadNaoEncontrado);
        }

        if (upload.EstaConfirmado)
        {
            return new([], RecusaDaTrocaDeFoto.UploadJaUsado);
        }

        var antigas = ChavesDaFoto;
        upload.Confirmar(agora);
        ChaveDaFotoDoFeed = upload.ChaveDoFeed;
        ChaveDaFotoDaGrade = upload.ChaveDaGrade;
        return new(antigas, null);
    }

    // As chaves que saíram; nenhuma, se não havia foto.
    public IReadOnlyList<string> RemoverFoto()
    {
        var antigas = ChavesDaFoto;
        ChaveDaFotoDoFeed = null;
        ChaveDaFotoDaGrade = null;
        return antigas;
    }

    private IReadOnlyList<string> ChavesDaFoto => TemFoto ? [ChaveDaFotoDoFeed!, ChaveDaFotoDaGrade!] : [];

    // O prefixo é do dono; o sufixo novo nunca colide com ninguém.
    public void TrocarDeCurso(Curso curso)
    {
        ArgumentNullException.ThrowIfNull(curso);
        GarantirAlunoAtivo("trocar de curso");

        Sufixo = Identidade.Sufixo.Para(Papeis, curso, exAluno: false);
    }

    // Ato da instituição (produto.md 2.2), sem volta na fase 1: quem marcou e quando ficam na
    // conta (passo 12, P5). Quem tem o papel de instituição, a aplicação confere.
    public void MarcarExAluno(Guid marcadoPorId, DateTimeOffset agora)
    {
        GarantirAlunoAtivo("virar ex-aluno");

        ExAlunoDesde = agora;
        ExAlunoMarcadoPorId = marcadoPorId;
        Sufixo = Identidade.Sufixo.ExAluno;
    }

    // Só anda para a frente: um pedido atrasado, de outra aba ou de outro aparelho, não
    // reacende a bolinha de um prêmio já visto.
    public void MarcarTrofeusVistos(DateTimeOffset agora)
    {
        if (TrofeusVistoEm is null || agora > TrofeusVistoEm)
        {
            TrofeusVistoEm = agora;
        }
    }

    // Público para a borda conferir antes de montar o usuário.
    public static string ValidarNomeDeExibicao(string nome)
    {
        nome = Texto.Obrigatorio(nome, "O nome de exibição");
        if (nome.EnumerateRunes().Count() > TamanhoMaximoDoNomeDeExibicao)
        {
            throw new DomainException($"O nome de exibição tem no máximo {TamanhoMaximoDoNomeDeExibicao} caracteres.");
        }

        return nome;
    }

    private void GarantirAlunoAtivo(string acao)
    {
        if (Papeis != Papeis.Aluno)
        {
            throw new DomainException($"Só aluno pode {acao}; @{Prefixo} é {Papeis}.");
        }

        if (EhExAluno)
        {
            throw new DomainException($"@{Prefixo} já é ex-aluno e não pode {acao}.");
        }
    }
}

// Um prefixo que já foi do usuário. A unicidade do prefixo no sistema mora aqui.
public sealed class PrefixoReservado
{
    // Para o EF Core materializar do banco.
    private PrefixoReservado()
    {
        Prefixo = null!;
    }

    internal PrefixoReservado(Guid usuarioId, string prefixo, DateTimeOffset reservadoEm)
    {
        UsuarioId = usuarioId;
        Prefixo = prefixo;
        ReservadoEm = reservadoEm;
    }

    public Guid UsuarioId { get; private set; }

    public string Prefixo { get; private set; }

    public DateTimeOffset ReservadoEm { get; private set; }
}

public enum RecusaDaTrocaDeFoto
{
    // Não é do usuário ou não é de foto de perfil: para quem pede, dá no mesmo.
    UploadNaoEncontrado,
    UploadJaUsado,
}

public sealed record ResultadoDaTrocaDeFoto(IReadOnlyList<string> ChavesAntigas, RecusaDaTrocaDeFoto? Recusa);
