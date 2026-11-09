using Maxx.Application.Conquistas;
using Maxx.Application.Fila;
using Maxx.Application.Fotos;
using Maxx.Application.Mural;
using Maxx.Application.PremiosParticipacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
using Maxx.Domain.Mural;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Persistence;

namespace Maxx.Infrastructure.Seed;

// A atividade do mundo de demonstração (passo 12): medalhas, prêmios de participação, a
// contestação aberta e os avisos, gerados de data/demo/atividade.json quando as contas dos
// alunos nascem. Tudo passa pelas regras do domínio, com o instante do fato no passado: a
// concessão de medalha com limite por período, a concessão direta de prêmio com as regras do
// evento, a contestação dentro da janela, o aviso com título e texto conferidos.
//
// As conquistas não são gravadas aqui: o seed enfileira a reavaliação por fato (uma por aluno
// e bimestre com medalha, e por aluno e semestre com prêmio definitivo) e o fechamento de cada
// período já encerrado, e o worker as concede, com evidências e progresso. A notificação de
// aviso novo também vai pelo worker, aos destinatários do instante da publicação.
//
// Determinístico: a mesma semente e a mesma ordem (alunos pelo RA, itens pelo código) geram os
// mesmos fatos a cada banco novo, dada a mesma data de corte.
public sealed partial class DatabaseSeeder
{
    // Fatos até a véspera, e nunca depois de 06/11/2026: o mundo foi desenhado até ali.
    private static readonly DateOnly _ultimoDiaDosFatos = new(2026, 11, 6);

    private sealed class GeradorDeAtividade(
        MaxxDbContext db,
        IFilaDeTrabalhos fila,
        IArmazenamentoDeFotos armazenamento,
        MundoDaAtividade mundo,
        IReadOnlyList<FotoDemo> fotos,
        DateTimeOffset agora)
    {
        private const string _jpeg = "image/jpeg";

        private readonly Random _sorte = new(20262);
        private readonly DateOnly _corte = Min(_ultimoDiaDosFatos, FusoHorario.DataLocal(agora).AddDays(-1));
        private readonly Dictionary<(Guid Aluno, string Item), List<Medalha>> _medalhas = [];
        private readonly HashSet<string> _chavesDePremio = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, double> _empenho = [];
        private readonly List<Post> _posts = [];
        private AtividadeDemo _atividade = null!;
        private int _reivindicacoesComFoto;

        public int Medalhas { get; private set; }

        public int Premios { get; private set; }

        public int Avisos { get; private set; }

        public int Posts => _posts.Count;

        public int Fotos { get; private set; }

        public async Task GerarAsync(AtividadeDemo atividade, CancellationToken cancellationToken)
        {
            _atividade = atividade;
            _reivindicacoesComFoto = atividade.Posts.ReivindicacoesComFoto;
            foreach (var aluno in mundo.Alunos)
            {
                // Empenho de 0,15 a 1, concentrado embaixo: poucos alunos ganham muito.
                _empenho[aluno.Conta.Id] = 0.15 + (0.85 * Math.Pow(_sorte.NextDouble(), 2));
            }

            // Os fatos explícitos primeiro: seguram a chave de deduplicação do prêmio antes do
            // sorteio.
            foreach (var pendente in atividade.ComprovacoesPendentes)
            {
                await ComprovacaoPendenteAsync(pendente, cancellationToken);
            }

            if (atividade.Contestacao is { } contestacao)
            {
                Contestacao(contestacao);
            }

            foreach (var (codigo, alunos) in atividade.Medalhas.OrderBy(par => par.Key, StringComparer.Ordinal))
            {
                MedalhasDoItem(codigo, alunos);
            }

            foreach (var (codigo, alunos) in atividade.PremiosParticipacao.OrderBy(par => par.Key, StringComparer.Ordinal))
            {
                await PremiosDoItemAsync(codigo, alunos, cancellationToken);
            }

            await PostsDeMedalhaAsync(atividade.Posts.MedalhasComFoto, cancellationToken);
            await PostsLivresAsync(atividade.Posts.Livres, cancellationToken);

            foreach (var aviso in atividade.Avisos)
            {
                Publicar(aviso);
            }

            Social(atividade.Social);
            PedirConquistas();
        }

        private void MedalhasDoItem(string codigo, int alunos)
        {
            if (!mundo.ItensDeMedalha.TryGetValue(codigo, out var doCatalogo))
            {
                throw Erro($"{codigo} não é medalha do catálogo.");
            }

            var (item, versao) = doCatalogo;
            var deSemestre = item.PeriodoDeReferencia == Periodo.Semestre;
            var ganhadores = Sorteio(aluno => Vagas(aluno, deSemestre).Count > 0).Take(alunos).ToList();
            if (ganhadores.Count < alunos)
            {
                throw Erro($"{codigo}: {alunos} alunos pedidos, só {ganhadores.Count} cursaram turma em período encerrado ou em curso.");
            }

            foreach (var aluno in ganhadores)
            {
                // A de semestre (média final, presença integral) é uma por matéria; as de
                // bimestre se repetem conforme o empenho.
                var quantas = deSemestre ? 1 : 1 + (int)Math.Floor(_empenho[aluno.Conta.Id] * 3.5);
                var concedidas = 0;
                for (var tentativa = 0; concedidas < quantas && tentativa < quantas * 4; tentativa++)
                {
                    var vagas = Vagas(aluno, deSemestre);
                    var (turma, inicio, fim) = vagas[_sorte.Next(vagas.Count)];
                    var instante = Instante(inicio, fim);
                    var semestre = mundo.Semestres.Single(candidato => candidato.Id == turma.SemestreLetivoId);
                    var existentes = _medalhas.TryGetValue((aluno.Conta.Id, codigo), out var lista) ? lista : [];
                    var resultado = Medalha.Conceder(
                        item, versao, aluno.Conta, aluno.Curso.Sigla, turma, semestre, mundo.ProfessorDaTurma[turma.Id], existentes, instante);
                    if (resultado.Medalha is not { } medalha)
                    {
                        continue;
                    }

                    db.Add(medalha);
                    _medalhas[(aluno.Conta.Id, codigo)] = [.. existentes, medalha];
                    concedidas++;
                    Medalhas++;
                }
            }
        }

        // As vagas de medalha do aluno: cada turma dele com os bimestres (ou o semestre) que
        // começaram até o corte. A medalha de semestre só no semestre já encerrado.
        private List<(Turma Turma, DateOnly Inicio, DateOnly Fim)> Vagas(AlunoDaAtividade aluno, bool deSemestre)
        {
            var vagas = new List<(Turma, DateOnly, DateOnly)>();
            foreach (var turma in aluno.Turmas)
            {
                var semestre = mundo.Semestres.Single(candidato => candidato.Id == turma.SemestreLetivoId);
                if (deSemestre)
                {
                    if (semestre.Fim <= _corte)
                    {
                        // Média final e presença do semestre: a última semana de aula.
                        vagas.Add((turma, semestre.Fim.AddDays(-6), semestre.Fim));
                    }

                    continue;
                }

                foreach (var bimestre in semestre.Bimestres.Where(bimestre => bimestre.Inicio <= _corte))
                {
                    vagas.Add((turma, bimestre.Inicio, Min(bimestre.Fim, _corte)));
                }
            }

            return vagas;
        }

        private async Task PremiosDoItemAsync(string codigo, int alunos, CancellationToken cancellationToken)
        {
            if (!mundo.ItensDePremio.TryGetValue(codigo, out var doCatalogo))
            {
                throw Erro($"{codigo} não é prêmio de participação do catálogo.");
            }

            var (item, versao) = doCatalogo;
            var eventos = mundo.Eventos.Values
                .Where(evento => evento.Aceita(item) && evento.Inicio <= _corte)
                .OrderBy(evento => evento.Codigo, StringComparer.Ordinal)
                .ToList();
            var concedidos = 0;
            foreach (var aluno in Sorteio(_ => true))
            {
                if (concedidos == alunos)
                {
                    break;
                }

                var possiveis = eventos.Where(evento => PodeTer(aluno, evento)).ToList();
                if (possiveis.Count == 0)
                {
                    continue;
                }

                var evento = possiveis[_sorte.Next(possiveis.Count)];
                var aprovadoEm = FusoHorario.Instante(Min(Min(evento.Fim.AddDays(2), evento.Inicio.AddDays(20)), _corte), new TimeOnly(15, 0));

                // Parte dos prêmios do semestre em curso nasce como o app faz: o aluno reivindica
                // com um post com foto e um professor dele aprova. O resto, concessão direta da
                // instituição, sem post.
                var reivindica = _reivindicacoesComFoto > 0 && !aluno.ExAluno && SemestreDoEvento(evento.Inicio) == SemestreEmCurso;
                var premio = reivindica
                    ? await ReivindicadoComFotoAsync(aluno, item, versao, evento, Reivindicacao(evento, aprovadoEm), aprovadoEm, Sorteada(_atividade.Legendas.Reivindicacao), cancellationToken)
                    : Conceder(aluno, item, versao, evento, aprovadoEm);
                if (premio is null)
                {
                    continue;
                }

                if (reivindica)
                {
                    _reivindicacoesComFoto--;
                }

                // Passada a janela de 15 dias, o prêmio é definitivo pela regra do próprio
                // domínio, no instante em que a janela fechou; senão, o fim dela fica agendado.
                if (premio.FimDaJanela is { } fimDaJanela && fimDaJanela <= agora)
                {
                    premio.TornarDefinitivoSeAJanelaFechou(fimDaJanela);
                }
                else
                {
                    fila.Enfileirar(new TornarPremioDefinitivo(premio.Id), premio.FimDaJanela);
                }

                concedidos++;
            }

            if (concedidos < alunos)
            {
                throw Erro($"{codigo}: {alunos} alunos pedidos, só {concedidos} podiam ter um evento que o aceita.");
            }
        }

        private PremioParticipacao? Conceder(AlunoDaAtividade aluno, ItemPremioParticipacao item, int versao, Evento evento, DateTimeOffset aprovadoEm)
        {
            var dia = FusoHorario.DataLocal(aprovadoEm);
            var ultimoEncerrado = mundo.Semestres.Where(semestre => semestre.Fim < dia).MaxBy(semestre => semestre.Fim);
            var resultado = PremioParticipacao.ConcederPelaInstituicao(
                item, versao, aluno.Conta, aluno.Curso, evento, null, ultimoEncerrado, mundo.Instituicao, aprovadoEm);
            if (resultado.Premio is not { } premio || !_chavesDePremio.Add(premio.ChaveDeDeduplicacao))
            {
                return null;
            }

            db.Add(premio);
            Premios++;
            return premio;
        }

        // A reivindicação pelo app (produto.md 4.7): post com foto, comprovação e, quando aprovada,
        // a aprovação de um professor do aluno naquele semestre. Sem aprovar, fica na fila.
        private async Task<PremioParticipacao?> ReivindicadoComFotoAsync(
            AlunoDaAtividade aluno,
            ItemPremioParticipacao item,
            int versao,
            Evento evento,
            DateTimeOffset reivindicadoEm,
            DateTimeOffset? aprovadoEm,
            string legenda,
            CancellationToken cancellationToken)
        {
            var dia = FusoHorario.DataLocal(reivindicadoEm);
            var ultimoEncerrado = mundo.Semestres.Where(semestre => semestre.Fim < dia).MaxBy(semestre => semestre.Fim);
            var reivindicado = PremioParticipacao.Reivindicar(item, versao, aluno.Conta, aluno.Curso, evento, null, ultimoEncerrado, reivindicadoEm);
            if (reivindicado.Premio is not { } premio || _chavesDePremio.Contains(premio.ChaveDeDeduplicacao))
            {
                return null;
            }

            var upload = await FotoAsync(aluno, reivindicadoEm, cancellationToken);
            var post = Post.Reivindicar(aluno.Conta, legenda, upload, premio, reivindicadoEm).Post
                ?? throw Erro($"post de reivindicação de {aluno.Cadastro.Ra} recusado.");
            var comprovacao = Comprovacao.De(premio, post.Id);
            if (aprovadoEm is { } instante)
            {
                var semestre = SemestreDoEvento(evento.Inicio);
                var turma = aluno.Turmas.First(turma => turma.SemestreLetivoId == semestre?.Id);
                if (premio.Aprovar(aluno.Conta, mundo.ProfessorDaTurma[turma.Id], instante) is { } recusa)
                {
                    throw Erro($"aprovação da reivindicação de {aluno.Cadastro.Ra} recusada: {recusa}.");
                }
            }

            _chavesDePremio.Add(premio.ChaveDeDeduplicacao);
            db.AddRange(premio, post, comprovacao);
            _posts.Add(post);
            Premios++;
            return premio;
        }

        private async Task ComprovacaoPendenteAsync(ComprovacaoPendenteDemo json, CancellationToken cancellationToken)
        {
            var reivindicadoEm = Local(json.ReivindicadoEm);
            if (reivindicadoEm > agora)
            {
                return;
            }

            var aluno = mundo.Alunos.SingleOrDefault(candidato => candidato.Cadastro.Ra == json.Ra)
                ?? throw Erro($"comprovação pendente: o RA {json.Ra} não é de aluno do arquivo.");
            if (!mundo.ItensDePremio.TryGetValue(json.Item, out var doCatalogo))
            {
                throw Erro($"comprovação pendente: {json.Item} não é prêmio de participação do catálogo.");
            }

            var evento = mundo.Eventos.GetValueOrDefault(json.Evento) ?? throw Erro($"comprovação pendente: o evento {json.Evento} não existe.");
            _ = await ReivindicadoComFotoAsync(aluno, doCatalogo.Item, doCatalogo.Versao, evento, reivindicadoEm, null, json.Legenda, cancellationToken)
                ?? throw Erro($"comprovação pendente de {json.Ra} em {json.Evento} recusada pelas regras do evento.");
        }

        // Medalhas do semestre em curso que o aluno postou com foto, horas depois de ganhar.
        private async Task PostsDeMedalhaAsync(int quantos, CancellationToken cancellationToken)
        {
            var candidatas = _medalhas.Values.SelectMany(lista => lista)
                .Where(medalha => medalha.SemestreLetivoId == SemestreEmCurso?.Id)
                .OrderBy(medalha => medalha.ConcedidaEm)
                .ToList();
            foreach (var medalha in candidatas.OrderBy(_ => _sorte.Next()).Take(quantos).OrderBy(medalha => medalha.ConcedidaEm))
            {
                var aluno = mundo.Alunos.Single(candidato => candidato.Conta.Id == medalha.AlunoId);
                var instante = Min(medalha.ConcedidaEm.AddHours(3), agora.AddDays(-1));
                var upload = await FotoAsync(aluno, instante, cancellationToken);
                var post = Post.Publicar(aluno.Conta, Sorteada(_atividade.Legendas.Medalha), upload, medalha, instante).Post
                    ?? throw Erro($"post da medalha de {aluno.Cadastro.Ra} recusado.");
                db.Add(post);
                _posts.Add(post);
            }
        }

        // Posts só com foto e legenda, dos alunos ativos, nos últimos dois meses.
        private async Task PostsLivresAsync(int quantos, CancellationToken cancellationToken)
        {
            var ativos = mundo.Alunos.Where(aluno => !aluno.ExAluno).ToList();
            for (var i = 0; i < quantos; i++)
            {
                var aluno = ativos[_sorte.Next(ativos.Count)];
                var instante = Instante(_corte.AddDays(-60), _corte);
                var upload = await FotoAsync(aluno, instante, cancellationToken);
                var post = Post.Publicar(aluno.Conta, Sorteada(_atividade.Legendas.Livre), upload, null, instante).Post
                    ?? throw Erro($"post livre de {aluno.Cadastro.Ra} recusado.");
                db.Add(post);
                _posts.Add(post);
            }
        }

        // Uma foto do curso do aluno, enviada ao bucket com chave aleatória nova, como o upload
        // do app: o pedido de upload, as duas derivadas e, ao publicar, a confirmação.
        private async Task<UploadPendente> FotoAsync(AlunoDaAtividade aluno, DateTimeOffset instante, CancellationToken cancellationToken)
        {
            var doCurso = fotos.Where(foto => foto.CabeEm(aluno.Curso.Sigla)).ToList();
            var foto = doCurso[_sorte.Next(doCurso.Count)];
            var upload = UploadPendente.Pedir(aluno.Conta, FinalidadeDoUpload.Post, _jpeg, foto.Feed.Length, foto.Grade.Length, instante).Upload
                ?? throw Erro($"upload da foto {foto.Nome} recusado.");
            await armazenamento.EnviarAsync(upload.ChaveDoFeed, foto.Feed, _jpeg, cancellationToken);
            await armazenamento.EnviarAsync(upload.ChaveDaGrade, foto.Grade, _jpeg, cancellationToken);
            db.Add(upload);
            Fotos += 2;
            return upload;
        }

        // Seguimentos entre colegas de curso, e curtidas e comentários nos posts com foto. Só
        // de alunos ativos, que é quem interage pelo app.
        private void Social(SocialDemo social)
        {
            var ativos = mundo.Alunos.Where(aluno => !aluno.ExAluno).ToList();
            var porCurso = mundo.Alunos.GroupBy(aluno => aluno.Curso.Id).ToDictionary(grupo => grupo.Key, grupo => grupo.ToList());
            var pares = new HashSet<(Guid, Guid)>();
            foreach (var aluno in ativos)
            {
                var colegas = porCurso[aluno.Curso.Id].Where(colega => colega.Conta.Id != aluno.Conta.Id).ToList();
                var quantos = _sorte.Next(social.SeguimentosPorAluno[0], social.SeguimentosPorAluno[1] + 1);
                foreach (var colega in colegas.OrderBy(_ => _sorte.Next()).Take(quantos))
                {
                    var instante = Instante(_corte.AddDays(-90), _corte);
                    if (pares.Add((aluno.Conta.Id, colega.Conta.Id))
                        && Seguimento.Seguir(aluno.Conta, colega.Conta, professorDoSeguido: false, instante) is { } seguimento)
                    {
                        db.Add(seguimento);
                    }
                }
            }

            foreach (var post in _posts)
            {
                var autor = mundo.Alunos.Single(aluno => aluno.Conta.Id == post.AutorId);
                var quantas = _sorte.Next(social.CurtidasPorPost[0], social.CurtidasPorPost[1] + 1);
                foreach (var quem in porCurso[autor.Curso.Id].Where(aluno => !aluno.ExAluno && aluno != autor).OrderBy(_ => _sorte.Next()).Take(quantas))
                {
                    if (Curtida.Curtir(quem.Conta, post, Min(post.CriadoEm.AddMinutes(_sorte.Next(5, 600)), agora)) is { } curtida)
                    {
                        db.Add(curtida);
                    }
                }
            }

            for (var i = 0; i < social.Comentarios && _posts.Count > 0; i++)
            {
                var post = _posts[_sorte.Next(_posts.Count)];
                var autor = mundo.Alunos.Single(aluno => aluno.Conta.Id == post.AutorId);
                var colegas = porCurso[autor.Curso.Id].Where(aluno => !aluno.ExAluno && aluno != autor).ToList();
                var quem = colegas[_sorte.Next(colegas.Count)];
                var instante = Min(post.CriadoEm.AddMinutes(_sorte.Next(10, 900)), agora);
                if (Comentario.Escrever(quem.Conta, post, Sorteada(_atividade.Comentarios), instante).Comentario is { } comentario)
                {
                    db.Add(comentario);
                }
            }
        }

        // O semestre que contém o último dia dos fatos: o que está em curso no mundo.
        private SemestreLetivo? SemestreEmCurso => mundo.Semestres.FirstOrDefault(semestre => semestre.Contem(_corte));

        // O aluno reivindica um dia antes da aprovação, e nunca antes de o evento começar.
        private static DateTimeOffset Reivindicacao(Evento evento, DateTimeOffset aprovadoEm)
        {
            var umDiaAntes = aprovadoEm.AddDays(-1).AddHours(5);
            var inicio = FusoHorario.Instante(evento.Inicio, new TimeOnly(20, 0));
            var reivindicadoEm = umDiaAntes > inicio ? umDiaAntes : inicio;
            return reivindicadoEm < aprovadoEm ? reivindicadoEm : aprovadoEm.AddHours(-1);
        }

        private string Sorteada(List<string> opcoes) => opcoes[_sorte.Next(opcoes.Count)];

        // O evento é do curso do aluno e cai num semestre que ele cursou.
        private bool PodeTer(AlunoDaAtividade aluno, Evento evento) =>
            evento.AbertoAoCurso(aluno.Curso.Id)
            && SemestreDoEvento(evento.Inicio) is { } semestre
            && aluno.Turmas.Any(turma => turma.SemestreLetivoId == semestre.Id);

        private SemestreLetivo? SemestreDoEvento(DateOnly inicio) =>
            mundo.Semestres.FirstOrDefault(semestre => semestre.Contem(inicio))
            ?? mundo.Semestres.Where(semestre => semestre.Inicio > inicio).MinBy(semestre => semestre.Inicio);

        private void Contestacao(ContestacaoDemo json)
        {
            var aluno = mundo.Alunos.SingleOrDefault(candidato => candidato.Cadastro.Ra == json.Ra)
                ?? throw Erro($"contestação: o RA {json.Ra} não é de aluno do arquivo.");
            if (!mundo.ItensDePremio.TryGetValue(json.Item, out var doCatalogo))
            {
                throw Erro($"contestação: {json.Item} não é prêmio de participação do catálogo.");
            }

            var (item, versao) = doCatalogo;
            var evento = mundo.Eventos.GetValueOrDefault(json.Evento) ?? throw Erro($"contestação: o evento {json.Evento} não existe.");
            var professor = mundo.Contas.GetValueOrDefault(json.ContestadaPor)
                ?? throw Erro($"contestação: {json.ContestadaPor} não tem conta.");
            var contestadaEm = Local(json.ContestadaEm);
            if (contestadaEm > agora)
            {
                return;
            }

            var premio = Conceder(aluno, item, versao, evento, Local(json.ConcedidoEm))
                ?? throw Erro($"contestação: o prêmio {json.Item} de {json.Ra} em {json.Evento} foi recusado pelas regras do evento.");
            var contestada = premio.Contestar(professor, json.Motivo, contestadaEm);
            db.Add(contestada.Contestacao ?? throw Erro($"contestação recusada: {contestada.Recusa}."));
        }

        private void Publicar(AvisoDemo json)
        {
            var publicadoEm = Local(json.PublicadoEm);
            if (publicadoEm > agora)
            {
                return;
            }

            var autor = mundo.Contas.GetValueOrDefault(json.Autor) ?? throw Erro($"aviso \"{json.Titulo}\": {json.Autor} não tem conta.");
            var (escopo, alvos) = json.Escopo switch
            {
                "faculdade" => (EscopoDoAviso.Faculdade, new List<Guid>()),
                "curso" => (EscopoDoAviso.Curso, (json.Cursos ?? []).Select(sigla =>
                    mundo.Cursos.GetValueOrDefault(sigla)?.Id ?? throw Erro($"aviso \"{json.Titulo}\": o curso {sigla} não existe.")).ToList()),
                _ => (EscopoDoAviso.Turma, (json.Turmas ?? []).Select(turma =>
                    mundo.Turmas.GetValueOrDefault((turma.Curso, turma.Materia, turma.Semestre))?.Id
                    ?? throw Erro($"aviso \"{json.Titulo}\": não há turma de {turma.Materia} ({turma.Curso}) em {turma.Semestre}.")).ToList()),
            };

            var resultado = Aviso.Publicar(autor, escopo, alvos, json.Titulo, json.Texto, null, publicadoEm);
            var aviso = resultado.Aviso ?? throw Erro($"aviso \"{json.Titulo}\" recusado: {resultado.Recusa}.");
            db.Add(aviso);
            fila.Enfileirar(new NotificarAvisoNovo(aviso.Id));
            Avisos++;
        }

        // A reavaliação por fato alcança a instância do fato e as que a contêm (semestre, ano,
        // curso): basta um fato por aluno e bimestre, e um prêmio definitivo por aluno e
        // semestre do evento. O fechamento de cada período encerrado, por aluno matriculado
        // nele, concede as de alvo dinâmico; roda mesmo depois do congelamento (D8 do passo 9).
        private void PedirConquistas()
        {
            var hoje = FusoHorario.DataLocal(agora);
            var porBimestre = _medalhas.Values.SelectMany(lista => lista)
                .GroupBy(medalha => (medalha.AlunoId, medalha.BimestreId))
                .Select(grupo => grupo.OrderBy(medalha => medalha.ConcedidaEm).First());
            foreach (var medalha in porBimestre.OrderBy(medalha => medalha.ConcedidaEm))
            {
                fila.Enfileirar(ReavaliarConquistas.PelaMedalha(medalha.AlunoId, medalha.Id));
            }

            var definitivos = db.ChangeTracker.Entries<PremioParticipacao>()
                .Select(entrada => entrada.Entity)
                .Where(premio => premio.Estado == EstadoDoPremioParticipacao.Definitivo)
                .GroupBy(premio => (premio.AlunoId, SemestreDoEvento(mundo.Eventos.Values.Single(evento => evento.Id == premio.EventoId).Inicio)?.Id))
                .Select(grupo => grupo.OrderBy(premio => premio.AprovadoEm).First());
            foreach (var premio in definitivos.OrderBy(premio => premio.AprovadoEm))
            {
                fila.Enfileirar(ReavaliarConquistas.PeloPremio(premio.AlunoId, premio.Id));
            }

            foreach (var semestre in mundo.Semestres.OrderBy(semestre => semestre.Inicio))
            {
                var cursaram = mundo.Alunos.Where(aluno => aluno.Turmas.Any(turma => turma.SemestreLetivoId == semestre.Id)).ToList();
                var encerrados = semestre.Bimestres.Where(bimestre => bimestre.Fim < hoje)
                    .Select(bimestre => (Periodo.Bimestre, bimestre.Id))
                    .Concat(semestre.Fim < hoje ? [(Periodo.Semestre, semestre.Id)] : []);
                foreach (var (periodo, referencia) in encerrados)
                {
                    foreach (var aluno in cursaram)
                    {
                        fila.Enfileirar(new AvaliarFechamentoDoAluno(periodo, referencia, aluno.Conta.Id));
                    }
                }
            }
        }

        // Alunos em ordem de sorteio, pesada pelo empenho: o mais empenhado tende a vir antes.
        private List<AlunoDaAtividade> Sorteio(Func<AlunoDaAtividade, bool> podeTer) =>
            mundo.Alunos
                .Select(aluno => (aluno, chave: _empenho[aluno.Conta.Id] * _sorte.NextDouble()))
                .OrderByDescending(par => par.chave)
                .Select(par => par.aluno)
                .Where(podeTer)
                .ToList();

        // Um dia útil entre as datas, das 8h às 21h em São Paulo.
        private DateTimeOffset Instante(DateOnly inicio, DateOnly fim)
        {
            var dias = Enumerable.Range(0, fim.DayNumber - inicio.DayNumber + 1)
                .Select(inicio.AddDays)
                .Where(dia => dia.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                .ToList();
            var dia = dias.Count > 0 ? dias[_sorte.Next(dias.Count)] : inicio;
            return FusoHorario.Instante(dia, new TimeOnly(_sorte.Next(8, 21), _sorte.Next(0, 4) * 15));
        }

        private static DateTimeOffset Local(string texto)
        {
            AtividadeDemoReader.Instante(texto, out var local);
            return FusoHorario.Instante(DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local));
        }

        private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

        private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

        private static SeedException Erro(string mensagem) => new($"{AtividadeDemoReader.Arquivo}: {mensagem} Nada foi gravado.");
    }

    // O que a atividade precisa do mundo já gravado nesta rodada.
    private sealed record MundoDaAtividade(
        IReadOnlyList<AlunoDaAtividade> Alunos,
        IReadOnlyDictionary<Guid, Usuario> ProfessorDaTurma,
        IReadOnlyDictionary<string, Usuario> Contas,
        IReadOnlyDictionary<(string Curso, string Materia, string Semestre), Turma> Turmas,
        IReadOnlyDictionary<string, Curso> Cursos,
        IReadOnlyDictionary<string, Evento> Eventos,
        IReadOnlyList<SemestreLetivo> Semestres,
        IReadOnlyDictionary<string, (ItemMedalha Item, int Versao)> ItensDeMedalha,
        IReadOnlyDictionary<string, (ItemPremioParticipacao Item, int Versao)> ItensDePremio,
        Usuario Instituicao);

    private sealed record AlunoDaAtividade(Usuario Conta, CadastroElegivel Cadastro, Curso Curso, IReadOnlyList<Turma> Turmas, bool ExAluno);

    // O aluno do arquivo que ganhou conta nesta rodada, com as turmas que cursa e o último
    // semestre dele, que decide quando o ex-aluno é marcado.
    private sealed record AlunoNovo(
        Usuario Conta, CadastroElegivel Cadastro, Curso Curso, IReadOnlyList<Turma> Turmas, bool ExAluno, SemestreLetivo UltimoSemestre);
}
