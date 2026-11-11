using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

public class UsuarioTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 3, 15, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);
    private static readonly Curso _si = new("Sistemas de Informação", "SI", 8);

    private static CadastroElegivel AnaNaBase() =>
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3);

    private static Usuario Ana() => Usuario.CadastrarAluno(AnaNaBase(), _cc, "Ana", "anaSouza", "hash", _agora);

    [Fact]
    public void Aluno_cadastrado_tem_sufixo_do_curso_e_senha()
    {
        var cadastro = AnaNaBase();

        var ana = Usuario.CadastrarAluno(cadastro, _cc, " Ana S. ", "anaSouza", "hash", _agora);

        Assert.Equal(cadastro.Id, ana.CadastroElegivelId);
        Assert.Equal(Papeis.Aluno, ana.Papeis);
        Assert.Equal("Ana S.", ana.NomeDeExibicao);
        Assert.Equal("@anaSouza-CC", ana.Arroba.ToString());
        Assert.True(ana.TemSenha);
        Assert.False(ana.EhExAluno);
        Assert.Equal(_agora, ana.CriadoEm);
    }

    [Fact]
    public void O_prefixo_do_cadastro_ja_nasce_reservado_ao_dono()
    {
        var ana = Ana();

        var reservado = Assert.Single(ana.PrefixosReservados);
        Assert.Equal("anaSouza", reservado.Prefixo);
        Assert.Equal(ana.Id, reservado.UsuarioId);
        Assert.Equal(_agora, reservado.ReservadoEm);
    }

    [Fact]
    public void Cadastro_de_aluno_exige_cadastro_elegivel_de_aluno()
    {
        var professor = CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque");

        Assert.Throws<DomainException>(() => Usuario.CadastrarAluno(professor, _cc, "Renata", "renataAlbuquerque", "hash", _agora));
    }

    [Fact]
    public void Cadastro_de_aluno_exige_o_curso_do_cadastro_elegivel()
    {
        Assert.Throws<DomainException>(() => Usuario.CadastrarAluno(AnaNaBase(), _si, "Ana", "anaSouza", "hash", _agora));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_de_exibicao_e_obrigatorio(string nome)
    {
        Assert.Throws<DomainException>(() => Usuario.CadastrarAluno(AnaNaBase(), _cc, nome, "anaSouza", "hash", _agora));
    }

    [Fact]
    public void Nome_de_exibicao_tem_no_maximo_60_caracteres()
    {
        var ana = Usuario.CadastrarAluno(AnaNaBase(), _cc, new string('a', 60), "anaSouza", "hash", _agora);

        Assert.Equal(60, ana.NomeDeExibicao.Length);
        Assert.Throws<DomainException>(
            () => Usuario.CadastrarAluno(AnaNaBase(), _cc, new string('a', 61), "anaSouza", "hash", _agora));
    }

    [Fact]
    public void Nome_de_exibicao_conta_caractere_e_nao_unidade_de_utf16()
    {
        // Sessenta emojis são 120 unidades de UTF-16, mas 60 caracteres.
        var sessentaEmojis = string.Concat(Enumerable.Repeat("🎓", 60));

        var ana = Usuario.CadastrarAluno(AnaNaBase(), _cc, sessentaEmojis, "anaSouza", "hash", _agora);

        Assert.Equal(sessentaEmojis, ana.NomeDeExibicao);
    }

    [Fact]
    public void Prefixo_fora_do_formato_e_recusado_no_cadastro()
    {
        Assert.Throws<DomainException>(() => Usuario.CadastrarAluno(AnaNaBase(), _cc, "Ana", "ana-souza", "hash", _agora));
    }

    [Fact]
    public void Aluno_sempre_tem_senha_no_cadastro()
    {
        Assert.Throws<DomainException>(() => Usuario.CadastrarAluno(AnaNaBase(), _cc, "Ana", "anaSouza", " ", _agora));
    }

    [Fact]
    public void Professor_vem_do_seed_sem_senha_e_com_sufixo_prof()
    {
        var cadastro = CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque");

        var renata = Usuario.CriarPeloSeed(cadastro, "renataAlbuquerque", _agora);

        Assert.Equal("@renataAlbuquerque-PROF", renata.Arroba.ToString());
        Assert.Equal("Renata Albuquerque", renata.NomeDeExibicao);
        Assert.False(renata.TemSenha);
        Assert.Null(renata.HashDaSenha);
    }

    [Fact]
    public void Professor_que_acumula_instituicao_usa_prof()
    {
        var cadastro = CadastroElegivel.Professor("bruno@unieduk.com.br", "Bruno Lima", tambemInstituicao: true);

        var bruno = Usuario.CriarPeloSeed(cadastro, "brunoLima", _agora);

        Assert.Equal("PROF", bruno.Arroba.Sufixo);
        Assert.Equal(Papeis.Professor | Papeis.Instituicao, bruno.Papeis);
    }

    [Fact]
    public void Conta_so_de_instituicao_usa_unimax()
    {
        var cadastro = CadastroElegivel.Instituicao("coordenacao@unieduk.com.br", "Coordenação UniMax");

        var coordenacao = Usuario.CriarPeloSeed(cadastro, "coordenacaoUnimax", _agora);

        Assert.Equal("@coordenacaoUnimax-UNIMAX", coordenacao.Arroba.ToString());
    }

    [Fact]
    public void Aluno_nao_e_criado_pelo_seed()
    {
        Assert.Throws<DomainException>(() => Usuario.CriarPeloSeed(AnaNaBase(), "anaSouza", _agora));
    }

    [Fact]
    public void Primeiro_acesso_define_a_senha()
    {
        var renata = Usuario.CriarPeloSeed(
            CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), "renataAlbuquerque", _agora);

        renata.DefinirSenha("hash-novo");

        Assert.True(renata.TemSenha);
        Assert.Equal("hash-novo", renata.HashDaSenha);
    }

    [Fact]
    public void Editar_o_prefixo_troca_o_arroba_e_reserva_o_antigo()
    {
        var ana = Ana();
        var depois = _agora.AddDays(10);

        ana.EditarPrefixo("aninha", depois);

        Assert.Equal("@aninha-CC", ana.Arroba.ToString());
        Assert.Equal(["anaSouza", "aninha"], ana.PrefixosReservados.Select(reservado => reservado.Prefixo));
        Assert.Equal(depois, ana.PrefixosReservados.Last().ReservadoEm);
    }

    [Fact]
    public void O_dono_pode_voltar_a_um_prefixo_antigo_sem_reservar_de_novo()
    {
        var ana = Ana();
        ana.EditarPrefixo("aninha", _agora.AddDays(1));

        ana.EditarPrefixo("anaSouza", _agora.AddDays(2));

        Assert.Equal("anaSouza", ana.Prefixo);
        Assert.Equal(2, ana.PrefixosReservados.Count);
    }

    [Fact]
    public void Mudar_so_as_maiusculas_troca_a_exibicao_sem_reservar_outro()
    {
        var ana = Ana();

        ana.EditarPrefixo("AnaSouza", _agora.AddDays(1));

        Assert.Equal("@AnaSouza-CC", ana.Arroba.ToString());
        Assert.Single(ana.PrefixosReservados);
    }

    [Fact]
    public void Editar_para_prefixo_fora_do_formato_e_recusado()
    {
        var ana = Ana();

        Assert.Throws<DomainException>(() => ana.EditarPrefixo("an", _agora));
        Assert.Equal("anaSouza", ana.Prefixo);
    }

    [Fact]
    public void Possui_prefixo_responde_pelos_reservados_sem_diferenciar_maiusculas()
    {
        var ana = Ana();
        ana.EditarPrefixo("aninha", _agora);

        Assert.True(ana.PossuiPrefixo("ANASOUZA"));
        Assert.True(ana.PossuiPrefixo("aninha"));
        Assert.False(ana.PossuiPrefixo("anaSouza2"));
    }

    [Fact]
    public void Troca_de_curso_muda_so_o_sufixo()
    {
        var ana = Ana();

        ana.TrocarDeCurso(_si);

        Assert.Equal("@anaSouza-SI", ana.Arroba.ToString());
        Assert.Single(ana.PrefixosReservados);
    }

    [Fact]
    public void Marcar_ex_aluno_troca_o_sufixo_para_ex_e_guarda_o_instante()
    {
        var ana = Ana();
        var marcacao = _agora.AddYears(4);

        ana.MarcarExAluno(ana.Id, marcacao);

        Assert.True(ana.EhExAluno);
        Assert.Equal(marcacao, ana.ExAlunoDesde);
        Assert.Equal(ana.Id, ana.ExAlunoMarcadoPorId);
        Assert.Equal("@anaSouza-EX", ana.Arroba.ToString());
        Assert.Single(ana.PrefixosReservados);
    }

    [Fact]
    public void Ex_aluno_nao_e_marcado_duas_vezes_nem_troca_de_curso()
    {
        var ana = Ana();
        ana.MarcarExAluno(ana.Id, _agora);

        Assert.Throws<DomainException>(() => ana.MarcarExAluno(ana.Id, _agora));
        Assert.Throws<DomainException>(() => ana.TrocarDeCurso(_si));
    }

    [Fact]
    public void Ex_aluno_continua_editando_o_prefixo_e_mantem_o_sufixo_ex()
    {
        var ana = Ana();
        ana.MarcarExAluno(ana.Id, _agora);

        ana.EditarPrefixo("aninha", _agora);

        Assert.Equal("@aninha-EX", ana.Arroba.ToString());
    }

    [Fact]
    public void So_aluno_troca_de_curso_ou_vira_ex_aluno()
    {
        var renata = Usuario.CriarPeloSeed(
            CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), "renataAlbuquerque", _agora);

        Assert.Throws<DomainException>(() => renata.TrocarDeCurso(_cc));
        Assert.Throws<DomainException>(() => renata.MarcarExAluno(renata.Id, _agora));
    }

    [Fact]
    public void Aluno_do_mundo_nasce_do_seed_sem_senha_com_o_nome_completo_e_o_instante_dado()
    {
        var cadastro = AnaNaBase();
        var inicio = new DateTimeOffset(2026, 1, 26, 3, 0, 0, TimeSpan.Zero);

        var ana = Usuario.CriarAlunoPeloSeed(cadastro, _cc, "anaSouza", inicio);

        Assert.Equal(("Ana Souza", "@anaSouza-CC", inicio), (ana.NomeDeExibicao, ana.Arroba.ToString(), ana.CriadoEm));
        Assert.False(ana.TemSenha);
        Assert.Empty(ana.AceitesDeTermo);
        Assert.Throws<DomainException>(() => Usuario.CriarAlunoPeloSeed(AnaNaBase(), _si, "anaSouza", inicio));
        Assert.Throws<DomainException>(() => Usuario.CriarAlunoPeloSeed(
            CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), _cc, "renataA", inicio));
        Assert.Throws<DomainException>(() => Usuario.CriarAlunoPeloSeed(
            CadastroElegivel.AlunoDeDemonstracao("DEMOABCD1234", "Dani Demo", _cc), _cc, "daniDemo", inicio));
    }

    [Fact]
    public void Aluno_de_demonstracao_entra_sem_senha_com_lote_e_chave()
    {
        var cadastro = CadastroElegivel.AlunoDeDemonstracao("DEMOABCD2345", "Ana Paula", _cc);

        var ana = Usuario.EntrarNaDemonstracao(cadastro, _cc, "anaPaula", "hash-da-chave", "banca-1", _agora);

        Assert.True(cadastro.Demonstracao);
        Assert.Equal("demoabcd2345@demonstracao.invalid", cadastro.Email);
        Assert.Equal(1, cadastro.Semestre);
        Assert.Equal("Ana Paula", ana.NomeDeExibicao);
        Assert.Equal("@anaPaula-CC", ana.Arroba.ToString());
        Assert.False(ana.TemSenha);
        Assert.True(ana.EhDemonstracao);
        Assert.Equal("banca-1", ana.LoteDaDemonstracao);
        Assert.Equal("hash-da-chave", ana.HashDaChaveDoDispositivo);
    }

    [Fact]
    public void So_linha_de_demonstracao_entra_pela_demonstracao()
    {
        Assert.Throws<DomainException>(() => Usuario.EntrarNaDemonstracao(AnaNaBase(), _cc, "anaSouza", "hash", "banca-1", _agora));
    }

    [Fact]
    public void Demonstracao_exige_lote_e_chave()
    {
        var cadastro = CadastroElegivel.AlunoDeDemonstracao("DEMOABCD2345", "Ana Paula", _cc);

        Assert.Throws<DomainException>(() => Usuario.EntrarNaDemonstracao(cadastro, _cc, "anaPaula", "hash", " ", _agora));
        Assert.Throws<DomainException>(() => Usuario.EntrarNaDemonstracao(cadastro, _cc, "anaPaula", "", "banca-1", _agora));
    }

    [Fact]
    public void Professor_e_coordenacao_de_demonstracao_entram_sem_curso_com_o_sufixo_do_papel()
    {
        var professor = Usuario.EntrarNaDemonstracao(
            CadastroElegivel.ProfessorDeDemonstracao("DEMOPROF0001", "Rita Lima"), null, "ritaLima", "hash-1", "banca-1", _agora);
        var coordenacao = Usuario.EntrarNaDemonstracao(
            CadastroElegivel.CoordenacaoDeDemonstracao("DEMOCOOR0001", "Caio Reis"), null, "caioReis", "hash-2", "banca-1", _agora);

        Assert.Equal((Papeis.Professor, "@ritaLima-PROF", "Rita Lima"), (professor.Papeis, professor.Arroba.ToString(), professor.NomeDeExibicao));
        Assert.Equal((Papeis.Instituicao, "@caioReis-UNIMAX"), (coordenacao.Papeis, coordenacao.Arroba.ToString()));
        Assert.All([professor, coordenacao], conta =>
        {
            Assert.True(conta.EhDemonstracao);
            Assert.False(conta.TemSenha);
            Assert.False(conta.EhEncerrada);
        });
    }

    [Fact]
    public void Curso_so_acompanha_o_aluno_de_demonstracao()
    {
        var aluno = CadastroElegivel.AlunoDeDemonstracao("DEMOABCD2345", "Ana Paula", _cc);
        var professor = CadastroElegivel.ProfessorDeDemonstracao("DEMOPROF0001", "Rita Lima");

        Assert.Throws<DomainException>(() => Usuario.EntrarNaDemonstracao(aluno, null, "anaPaula", "hash", "banca-1", _agora));
        Assert.Throws<DomainException>(() => Usuario.EntrarNaDemonstracao(aluno, _si, "anaPaula", "hash", "banca-1", _agora));
        Assert.Throws<DomainException>(() => Usuario.EntrarNaDemonstracao(professor, _cc, "ritaLima", "hash", "banca-1", _agora));
    }

    [Fact]
    public void Professor_e_instituicao_do_seed_nao_entram_pela_demonstracao()
    {
        Assert.Throws<DomainException>(() => Usuario.EntrarNaDemonstracao(
            CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), null, "renataA", "hash", "banca-1", _agora));
        Assert.Throws<DomainException>(() => Usuario.EntrarNaDemonstracao(
            CadastroElegivel.Instituicao("coord@unieduk.com.br", "Coordenação"), null, "coordenacao", "hash", "banca-1", _agora));
    }

    [Fact]
    public void Encerrar_tira_a_chave_e_guarda_o_primeiro_instante()
    {
        var professor = Usuario.EntrarNaDemonstracao(
            CadastroElegivel.ProfessorDeDemonstracao("DEMOPROF0001", "Rita Lima"), null, "ritaLima", "hash-1", "banca-1", _agora);

        professor.Encerrar(_agora.AddDays(1));
        professor.Encerrar(_agora.AddDays(2));

        Assert.True(professor.EhEncerrada);
        Assert.Equal(_agora.AddDays(1), professor.EncerradaEm);
        Assert.Null(professor.HashDaChaveDoDispositivo);
        Assert.Equal("banca-1", professor.LoteDaDemonstracao);
        Assert.Equal("Rita Lima", professor.NomeDeExibicao);
    }

    [Fact]
    public void So_professor_e_coordenacao_de_demonstracao_sao_encerrados()
    {
        var aluno = Usuario.EntrarNaDemonstracao(
            CadastroElegivel.AlunoDeDemonstracao("DEMOABCD2345", "Ana Paula", _cc), _cc, "anaPaula", "hash", "banca-1", _agora);
        var professorDoSeed = Usuario.CriarPeloSeed(
            CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), "renataA", _agora);

        Assert.Throws<DomainException>(() => aluno.Encerrar(_agora));
        Assert.Throws<DomainException>(() => professorDoSeed.Encerrar(_agora));
        Assert.Throws<DomainException>(() => Ana().Encerrar(_agora));
    }

    [Fact]
    public void Aluno_cadastrado_pelo_app_nao_e_demonstracao()
    {
        var ana = Ana();

        Assert.False(ana.EhDemonstracao);
        Assert.Null(ana.HashDaChaveDoDispositivo);
    }

    [Fact]
    public void Trofeus_vistos_comeca_vazio_e_so_anda_para_a_frente()
    {
        var ana = Ana();
        Assert.Null(ana.TrofeusVistoEm);

        ana.MarcarTrofeusVistos(_agora.AddMinutes(5));
        Assert.Equal(_agora.AddMinutes(5), ana.TrofeusVistoEm);

        // Um pedido atrasado não volta o marcador e não reacende prêmio já visto.
        ana.MarcarTrofeusVistos(_agora.AddMinutes(1));
        Assert.Equal(_agora.AddMinutes(5), ana.TrofeusVistoEm);

        ana.MarcarTrofeusVistos(_agora.AddMinutes(9));
        Assert.Equal(_agora.AddMinutes(9), ana.TrofeusVistoEm);
    }
}
