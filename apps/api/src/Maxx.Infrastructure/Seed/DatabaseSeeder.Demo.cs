using Maxx.Domain;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Seed;

// O perfil demo: cursos, matérias, turmas e pessoas de data/demo/mundo.json.
//
// Diferente do base, o demo só acrescenta. Linha no banco que não está no arquivo fica
// (é onde vão morar os alunos de demonstração criados pela banca, no passo 5). Linha que
// está nos dois e diverge faz o seed falhar: corrigir cadastro é ato da instituição, não
// do seed.
public sealed partial class DatabaseSeeder
{
    private static readonly string _tipoPremioParticipacao = NomeJson.De(TipoDeItem.PremioParticipacao);

    public async Task<ResultadoDoSeedDemo> SeedDemoAsync(CancellationToken cancellationToken)
    {
        var mundo = MundoDemoReader.Ler(Path.Combine(Dados, "demo"));
        var eventos = EventosDemoReader.Ler(Path.Combine(Dados, "demo"));

        await using var transacao = await AbrirComLockAsync(cancellationToken);

        var semestres = (await db.Set<SemestreLetivo>().ToListAsync(cancellationToken))
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

        demo.ContasDoSeed(relogio.GetUtcNow());

        var itens = (await db.Set<ItemCatalogoRow>().AsNoTracking()
                .Where(item => item.Tipo == _tipoPremioParticipacao)
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.Codigo, CatalogoRowMapper.ParaPremioParticipacao, StringComparer.Ordinal);
        foreach (var evento in eventos.Eventos)
        {
            demo.EventoSemeado(evento, itens);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        LogDone(logger, PerfilDeSeed.Demo, demo.Resultado);
        return demo.Resultado;
    }

    private static void GravarCurso(Gravacao demo, CursoDemo json, Dictionary<string, SemestreLetivo> semestres)
    {
        var curso = demo.Curso(json);

        var turmasPorMateria = new Dictionary<string, List<Turma>>(StringComparer.Ordinal);
        foreach (var materiaJson in json.Materias)
        {
            var materia = demo.Materia(curso, materiaJson.Nome);
            turmasPorMateria[materia.Nome] = [.. materiaJson.Turmas.Select(codigo =>
                demo.Turma(materia, semestres.GetValueOrDefault(codigo)
                    ?? throw new SeedException(
                        $"{json.Sigla}, {materia.Nome}: o semestre {codigo} não está no calendário. Rode `seed base` com ele.")))];
        }

        foreach (var professorJson in json.Professores)
        {
            var professor = demo.Cadastro(
                professorJson.Email, () => CadastroElegivel.Professor(professorJson.Email, professorJson.NomeCompleto));
            foreach (var turma in professorJson.Materias.SelectMany(nome => turmasPorMateria[nome]))
            {
                demo.Vinculo(professor, turma);
            }
        }

        foreach (var alunoJson in json.Alunos)
        {
            var aluno = demo.Cadastro(
                alunoJson.Email,
                () => CadastroElegivel.Aluno(alunoJson.Ra, alunoJson.Email, alunoJson.NomeCompleto, curso, alunoJson.Semestre));
            foreach (var turma in alunoJson.Materias.SelectMany(nome => turmasPorMateria[nome]))
            {
                demo.Matricula(aluno, turma);
            }
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

        private int _cursosNovos;
        private int _materiasNovas;
        private int _turmasNovas;
        private int _cadastrosNovos;
        private int _matriculasNovas;
        private int _vinculosNovos;
        private int _contasNovas;
        private int _eventosNovos;

        public IEnumerable<string> Siglas => _cursos.Keys;

        public ResultadoDoSeedDemo Resultado => new(
            _cursosNovos, _materiasNovas, _turmasNovas, _cadastrosNovos, _matriculasNovas, _vinculosNovos, _contasNovas, _eventosNovos);

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

        // Professor e instituição não se cadastram: nascem aqui com conta e sem senha, e o
        // primeiro acesso é pela recuperação (produto.md 2.5). Vale para toda linha deles no
        // banco, não só as do arquivo. Em ordem de e-mail, para o arroba sair igual a cada
        // banco novo.
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

        public void ContasDoSeed(DateTimeOffset agora)
        {
            var semConta = _cadastros.Values
                .Where(cadastro => !cadastro.EhAluno && !_cadastrosComConta.Contains(cadastro.Id))
                .OrderBy(cadastro => cadastro.Email, StringComparer.OrdinalIgnoreCase);

            foreach (var cadastro in semConta)
            {
                var prefixo = PrefixoDeArroba.Sugerir(cadastro.NomeCompleto, candidato => !_prefixos.Contains(candidato));
                db.Add(Construir(cadastro.Email, () => Usuario.CriarPeloSeed(cadastro, prefixo, agora)));
                _cadastrosComConta.Add(cadastro.Id);
                _prefixos.Add(prefixo);
                _contasNovas++;
            }
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
