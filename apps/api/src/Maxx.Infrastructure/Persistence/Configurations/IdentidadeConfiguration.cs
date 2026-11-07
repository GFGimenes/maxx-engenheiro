using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// Conta, sessão e recuperação. Os invariantes do domínio repetidos no banco, como no
// modelo acadêmico. Dentro do agregado apaga em cascata (usuário e seus prefixos, família
// e seus refresh); entre agregados, nunca.
internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    // A forma do PrefixoDeArroba.Validar.
    internal const string FormaDoPrefixo = "'^[A-Za-z][A-Za-z0-9]{2,29}$'";

    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario", table =>
        {
            table.HasCheckConstraint("ck_usuario_papeis", "papeis IN (1, 2, 4, 6)");
            table.HasCheckConstraint("ck_usuario_prefixo", $"prefixo ~ {FormaDoPrefixo}");

            // Só aluno vira ex-aluno, e o ex-aluno usa o sufixo EX.
            table.HasCheckConstraint(
                "ck_usuario_ex_aluno",
                "(ex_aluno_desde IS NULL AND sufixo <> 'EX') OR (ex_aluno_desde IS NOT NULL AND papeis = 1 AND sufixo = 'EX')");

            // Aluno de demonstração: lote e chave juntos, sem senha (produto.md 11).
            table.HasCheckConstraint(
                "ck_usuario_demonstracao",
                "(lote_da_demonstracao IS NULL AND hash_da_chave_do_dispositivo IS NULL) " +
                "OR (lote_da_demonstracao IS NOT NULL AND hash_da_chave_do_dispositivo IS NOT NULL AND papeis = 1 AND hash_da_senha IS NULL)");
        });
        builder.HasKey(usuario => usuario.Id);
        builder.Property(usuario => usuario.Id).ValueGeneratedNever();

        // Um RA, uma conta: a linha do cadastro elegível aceita um usuário só.
        builder.HasIndex(usuario => usuario.CadastroElegivelId).IsUnique();
        builder.HasOne<CadastroElegivel>()
            .WithMany()
            .HasForeignKey(usuario => usuario.CadastroElegivelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(usuario => usuario.Papeis).HasConversion<int>();
        // varchar(n) no Postgres conta caractere, como o domínio: um emoji é um.
        builder.Property(usuario => usuario.NomeDeExibicao).HasMaxLength(Usuario.TamanhoMaximoDoNomeDeExibicao).IsRequired();

        // A unicidade do prefixo mora em prefixo_reservado; aqui é só o atual, como escrito.
        builder.Property(usuario => usuario.Prefixo).HasColumnType("citext").IsRequired();
        builder.Property(usuario => usuario.Sufixo).IsRequired();
        builder.Property(usuario => usuario.HashDaSenha);

        // A purga apaga por lote; a volta pelo dispositivo busca pelo hash da chave.
        builder.Property(usuario => usuario.LoteDaDemonstracao).HasMaxLength(64);
        builder.HasIndex(usuario => usuario.LoteDaDemonstracao).HasFilter("lote_da_demonstracao IS NOT NULL");
        builder.HasIndex(usuario => usuario.HashDaChaveDoDispositivo).IsUnique().HasFilter("hash_da_chave_do_dispositivo IS NOT NULL");

        builder.HasMany(usuario => usuario.PrefixosReservados)
            .WithOne()
            .HasForeignKey(reservado => reservado.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(usuario => usuario.PrefixosReservados).HasField("_prefixosReservados");

        builder.HasMany(usuario => usuario.AceitesDeTermo)
            .WithOne()
            .HasForeignKey(aceite => aceite.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(usuario => usuario.AceitesDeTermo).HasField("_aceitesDeTermo");

        builder.Ignore(usuario => usuario.Arroba);
        builder.Ignore(usuario => usuario.TemSenha);
        builder.Ignore(usuario => usuario.EhExAluno);
        builder.Ignore(usuario => usuario.EhDemonstracao);
    }
}

internal sealed class PrefixoReservadoConfiguration : IEntityTypeConfiguration<PrefixoReservado>
{
    public void Configure(EntityTypeBuilder<PrefixoReservado> builder)
    {
        builder.ToTable("prefixo_reservado", table =>
            table.HasCheckConstraint("ck_prefixo_reservado_prefixo", $"prefixo ~ {UsuarioConfiguration.FormaDoPrefixo}"));

        // A chave é o próprio prefixo, em citext: um dono só, para sempre, sem diferenciar
        // maiúsculas (produto.md 2.3.1). É o que decide a corrida entre dois cadastros.
        builder.HasKey(reservado => reservado.Prefixo);
        builder.Property(reservado => reservado.Prefixo).HasColumnType("citext");
        builder.HasIndex(reservado => reservado.UsuarioId);
    }
}

internal sealed class TermoDeUsoConfiguration : IEntityTypeConfiguration<TermoDeUso>
{
    public void Configure(EntityTypeBuilder<TermoDeUso> builder)
    {
        builder.ToTable("termo_de_uso", table => table.HasCheckConstraint("ck_termo_de_uso_versao", "versao >= 1"));
        builder.HasKey(termo => termo.Versao);

        // A versão vem do nome do arquivo, nunca de uma sequência do banco.
        builder.Property(termo => termo.Versao).ValueGeneratedNever();
        builder.Property(termo => termo.Conteudo).IsRequired();
    }
}

internal sealed class AceiteDeTermoConfiguration : IEntityTypeConfiguration<AceiteDeTermo>
{
    public void Configure(EntityTypeBuilder<AceiteDeTermo> builder)
    {
        builder.ToTable("aceite_de_termo");
        builder.HasKey(aceite => new { aceite.UsuarioId, aceite.Versao });

        // Uma versão aceita por alguém não sai do banco.
        builder.HasOne<TermoDeUso>()
            .WithMany()
            .HasForeignKey(aceite => aceite.Versao)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FamiliaDeSessaoConfiguration : IEntityTypeConfiguration<FamiliaDeSessao>
{
    public void Configure(EntityTypeBuilder<FamiliaDeSessao> builder)
    {
        builder.ToTable("familia_sessao", table =>
            table.HasCheckConstraint(
                "ck_familia_sessao_revogacao",
                "(revogada_em IS NULL AND motivo_da_revogacao IS NULL) " +
                "OR (revogada_em IS NOT NULL AND motivo_da_revogacao IN (1, 2, 3))"));
        builder.HasKey(familia => familia.Id);
        builder.Property(familia => familia.Id).ValueGeneratedNever();
        builder.Property(familia => familia.MotivoDaRevogacao).HasConversion<int?>();

        // "As famílias ativas do usuário", que a senha redefinida revoga.
        builder.HasIndex(familia => familia.UsuarioId).HasFilter("revogada_em IS NULL");
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(familia => familia.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(familia => familia.Tokens)
            .WithOne()
            .HasForeignKey(token => token.FamiliaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(familia => familia.Tokens).HasField("_tokens");

        builder.Ignore(familia => familia.EstaAtiva);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_token", table =>
            table.HasCheckConstraint("ck_refresh_token_validade", "expira_em > emitido_em"));
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        // O refresh chega pelo hash: é a busca de toda renovação.
        builder.Property(token => token.Hash).IsRequired();
        builder.HasIndex(token => token.Hash).IsUnique();

        // Duas renovações simultâneas com o mesmo refresh: as duas marcam a mesma linha
        // como rotacionada, e o xmin deixa só a primeira gravar.
        builder.Property<uint>("Versao").IsRowVersion();
    }
}

internal sealed class RecuperacaoDeSenhaConfiguration : IEntityTypeConfiguration<RecuperacaoDeSenha>
{
    public void Configure(EntityTypeBuilder<RecuperacaoDeSenha> builder)
    {
        builder.ToTable("recuperacao_senha", table =>
        {
            table.HasCheckConstraint("ck_recuperacao_senha_validade", "expira_em > solicitada_em");
            table.HasCheckConstraint(
                "ck_recuperacao_senha_encerramento", "usada_em IS NULL OR invalidada_em IS NULL");

            // Só se usa um pedido cujo token foi emitido.
            table.HasCheckConstraint("ck_recuperacao_senha_token", "usada_em IS NULL OR hash IS NOT NULL");
        });
        builder.HasKey(recuperacao => recuperacao.Id);
        builder.Property(recuperacao => recuperacao.Id).ValueGeneratedNever();

        // Nulo até o worker emitir o token; o índice único ignora os nulos.
        builder.HasIndex(recuperacao => recuperacao.Hash).IsUnique();

        // "Os outros pedidos pendentes do usuário", que a conclusão invalida.
        builder.HasIndex(recuperacao => recuperacao.UsuarioId).HasFilter("usada_em IS NULL AND invalidada_em IS NULL");
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(recuperacao => recuperacao.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // O mesmo link usado duas vezes ao mesmo tempo: só um grava.
        builder.Property<uint>("Versao").IsRowVersion();
    }
}
