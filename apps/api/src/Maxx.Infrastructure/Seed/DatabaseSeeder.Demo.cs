using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Application.Ranking;
using Maxx.Domain;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Seed;

// O perfil demo: cursos, matérias, turmas e pessoas de data/demo/mundo.json.
//
// Diferente do base, o demo só acrescenta. Linha no banco que não está no arquivo fica
// (é onde vão morar os alunos de demonstração criados pela banca, no passo 5). Linha que
// está nos dois e diverge faz o seed falhar: corrigir cadastro é ato da instituição, não
// do seed. Por isso o aluno é achado pelo RA, e o e-mail ou o curso que a instituição
// corrigiu pela área web (passo 12) vencem o arquivo.
//
// Desde o passo 12, todo aluno do arquivo nasce com conta, sem senha, criada no início do
// primeiro semestre que cursou; o ex-aluno é marcado pela instituição no dia seguinte ao fim
// do último semestre que cursou (produto.md 2.2).
public sealed partial class DatabaseSeeder
{
    private static readonly string _tipoPremioParticipacao = NomeJson.De(TipoDeItem.PremioParticipacao);

    public async Task<ResultadoDoSeedDemo> SeedDemoAsync(CancellationToken cancellationToken)
    {
        var mundo = MundoDemoReader.Ler(Path.Combine(Dados, "demo"));
        var eventos = EventosDemoReader.Ler(Path.Combine(Dados, "demo"));

        await using var transacao = await AbrirComLockAsync(cancellationToken);

        var semestres = (await db.Set<SemestreLetivo>().Include(semestre => semestre.Bimestres).ToListAsync(cancellationToken))
            .ToDictionary(semestre => semestre.Codigo, StringComparer.Ordinal);
        if (semestres.Count == 0 || !await db.Set<ItemCatalogoRow>().AnyAsync(cancellationToken))
        {
            throw new SeedException("O seed demo exige o base: rode `seed base` antes.");
        }

        var demo = new Gravacao(db);
        await demo.CarregarAsync(cancellationToken);

        foreach (var curso in mundo.Cursos)
        {
            GravarCurso(demo, curso, semestres);
        }

        foreach (var pessoa in mundo.Instituicao)
        {
            demo.Cadastro(pessoa.Email, () => CadastroElegivel.Instituicao(pessoa.Email, pessoa.NomeCompleto));
        }

        await FalharSeRestricaoSemCursoAsync(demo.Siglas, cancellationToken);

        var catalogo = await db.Set<ItemCatalogoRow>().AsNoTracking().ToListAsync(cancellationToken);
        var itens = catalogo
            .Where(item => item.Tipo == _tipoPremioParticipacao)
            .ToDictionary(row => row.Codigo, CatalogoRowMapper.ParaPremioParticipacao, StringComparer.Ordinal);
        foreach (var evento in eventos.Eventos)
        {
            demo.EventoSemeado(evento, itens);
        }

        var agora = relogio.GetUtcNow();
        var inicioDoCalendario = FusoHorario.Instante(semestres.Values.Min(semestre => semestre.Inicio));
        demo.ContasDoSeed(agora, mundo, inicioDoCalendario);
        var instituicao = demo.Conta(mundo.Instituicao[0].Email);
        var primeiraPopulacao = demo.NenhumAlunoDoArquivoTemConta;
        var novos = demo.ContasDosAlunos(semestres);

        // A atividade nasce com o mundo: na primeira vez que os alunos do arquivo ganham conta.
        // O aluno que entra no arquivo depois ganha a conta, mas não fatos inventados para ele.
        if (primeiraPopulacao && novos.Count > 0)
        {
            var atividade = AtividadeDemoReader.Ler(Path.Combine(Dados, "demo"));
            var fotos = FotosDemoReader.Ler(Path.Combine(Dados, "demo"));
            var gerador = new GeradorDeAtividade(
                db, fila, armazenamento, demo.ParaAtividade(novos, semestres, catalogo, instituicao), fotos, agora);
            await gerador.GerarAsync(atividade, cancellationToken);
            demo.Atividade(gerador.Medalhas, gerador.Premios, gerador.Avisos, gerador.Posts);
        }

        // Depois dos fatos: ex-aluno não recebe prêmio (produto.md 2.2), e os dele são do tempo
        // em que cursava.
        demo.MarcarExAlunos(instituicao, novos);

        // Aluno com conta muda o divisor da raridade; ex-aluno e fatos novos mudam o ranking.
        // Um pedido de cada, se ainda não houver um pendente.
        if (novos.Count > 0)
        {
            await PedirSeNaoHaPendenteAsync(new RecalcularRaridade(), cancellationToken);
            await PedirSeNaoHaPendenteAsync(new RecalcularRanking(), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        LogDone(logger, PerfilDeSeed.Demo, demo.Resultado);
        return demo.Resultado;
    }

    private static void GravarCurso(Gravacao demo, CursoDemo json, Dictionary<string, SemestreLetivo> semestres)
    {
        var curso = demo.Curso(json);

        var turmasPorMateria = new Dictionary<string, List<(string Semestre, Turma Turma)>>(StringComparer.Ordinal);
        foreach (var materiaJson in json.Materias)
        {
            var materia = demo.Materia(curso, materiaJson.Nome);
            turmasPorMateria[materia.Nome] = [.. materiaJson.Turmas.Select(codigo =>
                (codigo, demo.Turma(materia, semestres.GetValueOrDefault(codigo)
                    ?? throw new SeedException(
                        $"{json.Sigla}, {materia.Nome}: o semestre {codigo} não está no calendário. Rode `seed base` com ele."))))];
        }

        foreach (var professorJson in json.Professores)
        {
            var professor = demo.Cadastro(
                professorJson.Email, () => CadastroElegivel.Professor(professorJson.Email, professorJson.NomeCompleto));
            foreach (var (_, turma) in professorJson.Materias.SelectMany(nome => turmasPorMateria[nome]))
            {
                demo.Vinculo(professor, turma);
            }
        }

        foreach (var alunoJson in json.Alunos)
        {
            var aluno = demo.Aluno(alunoJson, curso);
            var cursadas = alunoJson.Materias.SelectMany(nome => turmasPorMateria[nome])
                .Where(turma => alunoJson.SemestresLetivos?.Contains(turma.Semestre, StringComparer.Ordinal) ?? true)
                .ToList();
            foreach (var (_, turma) in cursadas)
            {
                demo.Matricula(aluno, turma);
            }

            demo.AlunoDoArquivo(aluno, alunoJson, [.. cursadas.Select(turma => turma.Turma)]);
        }
    }

    private async Task PedirSeNaoHaPendenteAsync<TCarga>(TCarga carga, CancellationToken cancellationToken)
        where TCarga : ICargaDeTrabalho
    {
        var tipo = TCarga.Tipo;
        var pendente = await db.Set<Trabalho>()
            .AnyAsync(trabalho => trabalho.Tipo == tipo && trabalho.Estado == EstadoDoTrabalho.Pendente, cancellationToken);
        if (!pendente)
        {
            fila.Enfileirar(carga);
        }
    }

    // O banco não liga restricao_curso a curso (o catálogo entra antes dos cursos), então
    // a conferência é aqui: toda restrição do catálogo aponta para uma sigla existente.
    private async Task FalharSeRestricaoSemCursoAsync(IEnumerable<string> siglas, CancellationToken cancellationToken)
    {
        var existentes = siglas.ToHashSet(StringComparer.Ordinal);
        var orfas = (await db.Set<ItemCatalogoRow>()
                .Where(item => item.RestricaoCurso != null)
                .Select(item => new { item.Codigo, item.RestricaoCurso })
                .ToListAsync(cancellationToken))
            .Where(item => !existentes.Contains(item.RestricaoCurso!))
            .Select(item => $"{item.Codigo} ({item.RestricaoCurso})")
            .Order(StringComparer.Ordinal)
            .ToList();

        if (orfas.Count > 0)
        {
            throw new SeedException(
                $"restricaoCurso do catálogo aponta para curso inexistente: {string.Join(", ", orfas)}. Nada foi gravado.");
        }
    }

    // O que já está no banco, indexado pela chave natural, mais o que esta rodada acrescenta.
    private sealed class Gravacao(MaxxDbContext db)
    {
        private readonly Dictionary<string, Curso> _cursos = new(StringComparer.Ordinal);
        private readonly Dictionary<(Guid CursoId, string Nome), Materia> _materias = [];
        private readonly Dictionary<(Guid MateriaId, Guid SemestreId), Turma> _turmas = [];
        private readonly Dictionary<string, CadastroElegivel> _cadastros = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CadastroElegivel> _porRa = new(StringComparer.Ordinal);
        private readonly HashSet<(Guid AlunoId, Guid TurmaId)> _matriculas = [];
        private readonly HashSet<(Guid ProfessorId, Guid TurmaId)> _vinculos = [];
        private readonly HashSet<Guid> _cadastrosComConta = [];
        private readonly HashSet<string> _prefixos = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Evento> _eventos = new(StringComparer.Ordinal);
        private readonly HashSet<Guid> _comEmailCorrigido = [];
        private readonly HashSet<Guid> _comCursoTrocado = [];
        private readonly Dictionary<Guid, Usuario> _contasDeFuncionario = [];
        private readonly List<(CadastroElegivel Cadastro, AlunoDemo Json, List<Turma> Turmas)> _alunosDoArquivo = [];

        private int _cursosNovos;
        private int _materiasNovas;
        private int _turmasNovas;
        private int _cadastrosNovos;
        private int _matriculasNovas;
        private int _vinculosNovos;
        private int _contasNovas;
        private int _eventosNovos;
        private int _contasDeAlunoNovas;
        private int _exAlunosNovos;
        private int _medalhasNovas;
        private int _premiosNovos;
        private int _avisosNovos;
        private int _postsNovos;

        public IEnumerable<string> Siglas => _cursos.Keys;

        public ResultadoDoSeedDemo Resultado => new(
            _cursosNovos,
            _materiasNovas,
            _turmasNovas,
            _cadastrosNovos,
            _matriculasNovas,
            _vinculosNovos,
            _contasNovas,
            _eventosNovos,
            _contasDeAlunoNovas,
            _exAlunosNovos,
            _medalhasNovas,
            _premiosNovos,
            _avisosNovos,
            _postsNovos);

        public async Task CarregarAsync(CancellationToken cancellationToken)
        {
            foreach (var curso in await db.Set<Curso>().ToListAsync(cancellationToken))
            {
                _cursos[curso.Sigla] = curso;
            }

            foreach (var materia in await db.Set<Materia>().ToListAsync(cancellationToken))
            {
                _materias[(materia.CursoId, materia.Nome)] = materia;
            }

            foreach (var turma in await db.Set<Turma>().Include(turma => turma.Materia).ToListAsync(cancellationToken))
            {
                _turmas[(turma.MateriaId, turma.SemestreLetivoId)] = turma;
            }

            foreach (var cadastro in await db.Set<CadastroElegivel>().ToListAsync(cancellationToken))
            {
                _cadastros[cadastro.Email] = cadastro;
                if (cadastro.Ra is { } ra)
                {
                    _porRa[ra] = cadastro;
                }
            }

            _matriculas.UnionWith(await db.Set<Matricula>()
                .Select(matricula => new ValueTuple<Guid, Guid>(matricula.AlunoId, matricula.TurmaId))
                .ToListAsync(cancellationToken));
            _vinculos.UnionWith(await db.Set<VinculoProfessor>()
                .Select(vinculo => new ValueTuple<Guid, Guid>(vinculo.ProfessorId, vinculo.TurmaId))
                .ToListAsync(cancellationToken));
            _cadastrosComConta.UnionWith(await db.Set<Usuario>()
                .Select(usuario => usuario.CadastroElegivelId)
                .ToListAsync(cancellationToken));
            foreach (var evento in await db.Set<Evento>().Where(evento => evento.Codigo != null).ToListAsync(cancellationToken))
            {
                _eventos[evento.Codigo!] = evento;
            }

            _prefixos.UnionWith(await db.Set<PrefixoReservado>()
                .Select(reservado => reservado.Prefixo)
                .ToListAsync(cancellationToken));

            _comEmailCorrigido.UnionWith(await db.Set<CorrecaoDeEmail>().Select(correcao => correcao.CadastroElegivelId).ToListAsync(cancellationToken));
            _comCursoTrocado.UnionWith(await db.Set<TrocaDeCurso>().Select(troca => troca.CadastroElegivelId).ToListAsync(cancellationToken));
            foreach (var conta in await db.Set<Usuario>().Where(usuario => usuario.Papeis != Papeis.Aluno).ToListAsync(cancellationToken))
            {
                _contasDeFuncionario[conta.CadastroElegivelId] = conta;
            }
        }

        public Curso Curso(CursoDemo json)
        {
            var novo = Construir(json.Sigla, () => new Curso(json.Nome, json.Sigla, json.DuracaoEmSemestres));
            if (!_cursos.TryGetValue(novo.Sigla, out var atual))
            {
                db.Add(novo);
                _cursos[novo.Sigla] = novo;
                _cursosNovos++;
                return novo;
            }

            if ((atual.Nome, atual.DuracaoEmSemestres) != (novo.Nome, novo.DuracaoEmSemestres))
            {
                throw Divergencia($"curso {atual.Sigla}");
            }

            return atual;
        }

        public Materia Materia(Curso curso, string nome)
        {
            var nova = Construir($"{curso.Sigla}, {nome}", () => new Materia(curso, nome));
            if (_materias.TryGetValue((curso.Id, nova.Nome), out var atual))
            {
                return atual;
            }

            db.Add(nova);
            _materias[(curso.Id, nova.Nome)] = nova;
            _materiasNovas++;
            return nova;
        }

        public Turma Turma(Materia materia, SemestreLetivo semestre)
        {
            if (_turmas.TryGetValue((materia.Id, semestre.Id), out var atual))
            {
                return atual;
            }

            var nova = new Turma(materia, semestre);
            db.Add(nova);
            _turmas[(materia.Id, semestre.Id)] = nova;
            _turmasNovas++;
            return nova;
        }

        public CadastroElegivel Cadastro(string email, Func<CadastroElegivel> construir)
        {
            var novo = Construir(email, construir);
            if (_cadastros.TryGetValue(novo.Email, out var atual))
            {
                if ((atual.Papeis, atual.Ra, atual.CursoId, atual.Semestre, atual.NomeCompleto)
                    != (novo.Papeis, novo.Ra, novo.CursoId, novo.Semestre, novo.NomeCompleto))
                {
                    throw Divergencia($"cadastro {atual.Email}");
                }

                return atual;
            }

            if (novo.Ra is { } ra && _porRa.TryGetValue(ra, out var dono))
            {
                throw new SeedException($"O RA {ra} de {novo.Email} já pertence a {dono.Email}. Nada foi gravado.");
            }

            db.Add(novo);
            _cadastros[novo.Email] = novo;
            if (novo.Ra is { } novoRa)
            {
                _porRa[novoRa] = novo;
            }

            _cadastrosNovos++;
            return novo;
        }

        // O aluno é achado pelo RA, a identidade dele. Nome e papel têm de bater com o arquivo;
        // e-mail, curso e semestre também, salvo o que a instituição corrigiu pela área web
        // (passo 12): o ato dela vence o arquivo, e o seed não desfaz a correção.
        public CadastroElegivel Aluno(AlunoDemo json, Curso curso)
        {
            var novo = Construir(
                json.Email, () => CadastroElegivel.Aluno(json.Ra, json.Email, json.NomeCompleto, curso, json.Semestre));
            if (_porRa.TryGetValue(novo.Ra!, out var atual))
            {
                var emailDiverge = !string.Equals(atual.Email, novo.Email, StringComparison.OrdinalIgnoreCase)
                    && !_comEmailCorrigido.Contains(atual.Id);
                var cursoDiverge = (atual.CursoId, atual.Semestre) != (novo.CursoId, novo.Semestre) && !_comCursoTrocado.Contains(atual.Id);
                if (atual.Papeis != novo.Papeis || atual.NomeCompleto != novo.NomeCompleto || emailDiverge || cursoDiverge)
                {
                    throw Divergencia($"cadastro {atual.Email} (RA {atual.Ra})");
                }

                return atual;
            }

            if (_cadastros.TryGetValue(novo.Email, out var dono))
            {
                throw new SeedException($"O e-mail {novo.Email} do RA {novo.Ra} já pertence a outra linha ({dono.Ra ?? dono.Email}). Nada foi gravado.");
            }

            db.Add(novo);
            _cadastros[novo.Email] = novo;
            _porRa[novo.Ra!] = novo;
            _cadastrosNovos++;
            return novo;
        }

        // O aluno do arquivo e os semestres letivos em que cursa: é o que decide a conta.
        public void AlunoDoArquivo(CadastroElegivel cadastro, AlunoDemo json, List<Turma> turmas) =>
            _alunosDoArquivo.Add((cadastro, json, turmas));

        public void Matricula(CadastroElegivel aluno, Turma turma)
        {
            if (_matriculas.Add((aluno.Id, turma.Id)))
            {
                db.Add(Construir(aluno.Email, () => new Matricula(aluno, turma)));
                _matriculasNovas++;
            }
        }

        public void Vinculo(CadastroElegivel professor, Turma turma)
        {
            if (_vinculos.Add((professor.Id, turma.Id)))
            {
                db.Add(Construir(professor.Email, () => new VinculoProfessor(professor, turma)));
                _vinculosNovos++;
            }
        }

        // O evento semeado, com os itens e o curso conferidos contra o que está no banco: o
        // catálogo vem do base, e o curso, do mundo ou de uma rodada anterior.
        public void EventoSemeado(EventoDemo json, Dictionary<string, ItemPremioParticipacao> itens)
        {
            var desconhecidos = json.Itens.Where(codigo => !itens.ContainsKey(codigo)).ToList();
            if (desconhecidos.Count > 0)
            {
                throw new SeedException(
                    $"{EventosDemoReader.Arquivo}, {json.Codigo}: {string.Join(", ", desconhecidos)} não é prêmio de participação do catálogo. " +
                    "Nada foi gravado.");
            }

            Curso? curso = null;
            if (json.Curso is { } sigla && !_cursos.TryGetValue(sigla, out curso))
            {
                throw new SeedException($"{EventosDemoReader.Arquivo}, {json.Codigo}: o curso {sigla} não existe. Nada foi gravado.");
            }

            var novo = Construir(
                json.Codigo,
                () => Evento.Semeado(json.Codigo, json.Nome, json.Inicio, json.Fim, [.. json.Itens.Select(codigo => itens[codigo])], curso),
                EventosDemoReader.Arquivo);
            if (!_eventos.TryGetValue(novo.Codigo!, out var atual))
            {
                db.Add(novo);
                _eventos[novo.Codigo!] = novo;
                _eventosNovos++;
                return;
            }

            if ((atual.Nome, atual.Inicio, atual.Fim, atual.CursoId) != (novo.Nome, novo.Inicio, novo.Fim, novo.CursoId)
                || !atual.ItensAceitos.SequenceEqual(novo.ItensAceitos, StringComparer.Ordinal))
            {
                throw new SeedException(
                    $"O evento {atual.Codigo} já existe no banco com dados diferentes de {EventosDemoReader.Arquivo}. " +
                    "O seed demo só acrescenta: evento novo entra com outro código. Nada foi gravado.");
            }
        }

        // Professor e instituição sem conta, de toda linha deles no banco, e não só das do
        // arquivo. A conta de quem está no arquivo é criada no início do calendário: o mundo
        // existe desde o primeiro semestre, e a notificação de aviso alcança quem tinha conta
        // na publicação (decisão do passo 10). A de quem não está no arquivo, agora.
        public void ContasDoSeed(DateTimeOffset agora, MundoDemo mundo, DateTimeOffset inicioDoCalendario)
        {
            var doArquivo = mundo.Instituicao.Select(pessoa => pessoa.Email)
                .Concat(mundo.Cursos.SelectMany(curso => curso.Professores.Select(professor => professor.Email)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var semConta = _cadastros.Values
                .Where(cadastro => !cadastro.EhAluno && !_cadastrosComConta.Contains(cadastro.Id))
                .OrderBy(cadastro => cadastro.Email, StringComparer.OrdinalIgnoreCase);

            foreach (var cadastro in semConta)
            {
                var prefixo = PrefixoDeArroba.Sugerir(cadastro.NomeCompleto, candidato => !_prefixos.Contains(candidato));
                var criadaEm = doArquivo.Contains(cadastro.Email) ? inicioDoCalendario : agora;
                var conta = Construir(cadastro.Email, () => Usuario.CriarPeloSeed(cadastro, prefixo, criadaEm));
                db.Add(conta);
                _contasDeFuncionario[cadastro.Id] = conta;

                _cadastrosComConta.Add(cadastro.Id);
                _prefixos.Add(prefixo);
                _contasNovas++;
            }
        }

        // A conta da instituição do arquivo: é ela que marca os ex-alunos e concede os prêmios.
        public Usuario Conta(string email) =>
            _cadastros.TryGetValue(email, out var cadastro)
            && _contasDeFuncionario.TryGetValue(cadastro.Id, out var conta)
            && conta.Papeis.HasFlag(Papeis.Instituicao)
                ? conta
                : throw new SeedException($"{MundoDemoReader.Arquivo}: {email} não tem conta de instituição. Nada foi gravado.");

        // Antes das contas desta rodada: nenhum aluno do arquivo tinha conta. É quando a
        // atividade do mundo nasce.
        public bool NenhumAlunoDoArquivoTemConta =>
            _alunosDoArquivo.All(aluno => !_cadastrosComConta.Contains(aluno.Cadastro.Id));

        // A conta de cada aluno do arquivo que ainda não tem uma, em ordem de e-mail, para o
        // arroba sair igual a cada banco novo. Nasce no início do primeiro semestre que o
        // aluno cursou. A conta que já existe fica como está. O curso é o atual da linha: a
        // troca de curso feita pela instituição vence o arquivo.
        public List<AlunoNovo> ContasDosAlunos(Dictionary<string, SemestreLetivo> semestres)
        {
            var cursos = _cursos.Values.ToDictionary(curso => curso.Id);
            var porId = semestres.Values.ToDictionary(semestre => semestre.Id);
            var novos = new List<AlunoNovo>();
            foreach (var (cadastro, json, turmas) in _alunosDoArquivo.OrderBy(aluno => aluno.Cadastro.Email, StringComparer.OrdinalIgnoreCase))
            {
                if (_cadastrosComConta.Contains(cadastro.Id))
                {
                    continue;
                }

                var cursados = turmas.Select(turma => porId[turma.SemestreLetivoId]).Distinct().OrderBy(semestre => semestre.Inicio).ToList();
                if (cursados.Count == 0)
                {
                    throw new SeedException($"{MundoDemoReader.Arquivo}, {json.Email}: o aluno não cursa turma nenhuma. Nada foi gravado.");
                }

                var curso = cursos[cadastro.CursoId!.Value];
                var prefixo = PrefixoDeArroba.Sugerir(cadastro.NomeCompleto, candidato => !_prefixos.Contains(candidato));
                var conta = Construir(
                    json.Email, () => Usuario.CriarAlunoPeloSeed(cadastro, curso, prefixo, FusoHorario.Instante(cursados[0].Inicio)));

                db.Add(conta);
                _cadastrosComConta.Add(cadastro.Id);
                _prefixos.Add(prefixo);
                _contasDeAlunoNovas++;
                novos.Add(new AlunoNovo(conta, cadastro, curso, turmas, json.ExAluno, cursados[^1]));
            }

            return novos;
        }

        // O ex-aluno do arquivo, marcado pela instituição no dia seguinte ao fim do último
        // semestre que cursou (produto.md 2.2).
        public void MarcarExAlunos(Usuario instituicao, IEnumerable<AlunoNovo> novos)
        {
            foreach (var aluno in novos.Where(aluno => aluno.ExAluno))
            {
                aluno.Conta.MarcarExAluno(instituicao.Id, FusoHorario.Instante(aluno.UltimoSemestre.Fim.AddDays(1)));
                _exAlunosNovos++;
            }
        }

        // O mundo já gravado nesta rodada, como a atividade o usa.
        public MundoDaAtividade ParaAtividade(
            List<AlunoNovo> novos, Dictionary<string, SemestreLetivo> semestres, List<ItemCatalogoRow> catalogo, Usuario instituicao)
        {
            var contas = _cadastros.Values
                .Where(cadastro => _contasDeFuncionario.ContainsKey(cadastro.Id))
                .ToDictionary(cadastro => cadastro.Email, cadastro => _contasDeFuncionario[cadastro.Id], StringComparer.OrdinalIgnoreCase);
            var professorDaTurma = new Dictionary<Guid, Usuario>();
            foreach (var (professorId, turmaId) in _vinculos.OrderBy(vinculo => vinculo.ProfessorId))
            {
                if (_contasDeFuncionario.TryGetValue(professorId, out var professor))
                {
                    professorDaTurma.TryAdd(turmaId, professor);
                }
            }

            var codigoDoSemestre = semestres.ToDictionary(par => par.Value.Id, par => par.Key);
            var cursos = _cursos.Values.ToDictionary(curso => curso.Id);
            var turmas = _turmas.Values.ToDictionary(
                turma => (cursos[turma.Materia.CursoId].Sigla, turma.Materia.Nome, codigoDoSemestre[turma.SemestreLetivoId]));
            var medalhas = catalogo.Where(row => row.Tipo == NomeJson.De(TipoDeItem.Medalha))
                .ToDictionary(row => row.Codigo, row => (CatalogoRowMapper.ParaMedalha(row), row.Versao), StringComparer.Ordinal);
            var premios = catalogo.Where(row => row.Tipo == NomeJson.De(TipoDeItem.PremioParticipacao))
                .ToDictionary(row => row.Codigo, row => (CatalogoRowMapper.ParaPremioParticipacao(row), row.Versao), StringComparer.Ordinal);

            return new MundoDaAtividade(
                [.. novos.OrderBy(aluno => aluno.Cadastro.Ra, StringComparer.Ordinal)
                    .Select(aluno => new AlunoDaAtividade(aluno.Conta, aluno.Cadastro, aluno.Curso, aluno.Turmas, aluno.ExAluno))],
                professorDaTurma,
                contas,
                turmas,
                _cursos,
                _eventos,
                [.. semestres.Values.OrderBy(semestre => semestre.Inicio)],
                medalhas,
                premios,
                instituicao);
        }

        public void Atividade(int medalhas, int premios, int avisos, int posts)
        {
            _medalhasNovas += medalhas;
            _premiosNovos += premios;
            _avisosNovos += avisos;
            _postsNovos += posts;
        }

        // A regra é a do domínio; aqui só se acrescenta de onde veio o dado recusado.
        private static T Construir<T>(string origem, Func<T> construir, string arquivo = MundoDemoReader.Arquivo)
        {
            try
            {
                return construir();
            }
            catch (DomainException ex)
            {
                throw new SeedException($"{arquivo}, {origem}: {ex.Message} Nada foi gravado.", ex);
            }
        }

        private static SeedException Divergencia(string oQue) =>
            new($"O {oQue} já existe no banco com dados diferentes de {MundoDemoReader.Arquivo}. " +
                "O seed demo só acrescenta; corrigir cadastro é ato da instituição. Nada foi gravado.");
    }
}
