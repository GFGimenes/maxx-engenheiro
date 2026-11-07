using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Infrastructure.RaridadeDosItens;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

internal sealed class NotificacaoConfiguration : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("notificacao", table =>
            table.HasCheckConstraint("ck_notificacao_tipo", $"tipo IN ({Lista<TipoDeNotificacao>()})"));
        builder.HasKey(notificacao => notificacao.Id);
        builder.Property(notificacao => notificacao.Id).ValueGeneratedNever();
        builder.Property(notificacao => notificacao.Tipo)
            .HasConversion(tipo => NomeJson.De(tipo), texto => Enum.Parse<TipoDeNotificacao>(texto, ignoreCase: true));

        // Uma notificação por fato e por usuário: o trabalho que roda de novo não duplica.
        builder.HasIndex(notificacao => new { notificacao.UsuarioId, notificacao.Tipo, notificacao.ReferenciaId }).IsUnique();

        // A referência é genérica (medalha hoje; aprovação, aviso depois): sem chave
        // estrangeira. A purga apaga as notificações do usuário antes dele.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(notificacao => notificacao.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }

    private static string Lista<T>()
        where T : struct, Enum => string.Join(", ", Enum.GetValues<T>().Select(valor => $"'{NomeJson.De(valor)}'"));
}

internal sealed class RaridadeDoItemConfiguration : IEntityTypeConfiguration<RaridadeDoItemRow>
{
    public void Configure(EntityTypeBuilder<RaridadeDoItemRow> builder)
    {
        builder.ToTable("raridade_do_item", table =>
        {
            table.HasCheckConstraint(
                "ck_raridade_do_item_raridade",
                $"raridade IN ({string.Join(", ", Enum.GetValues<Raridade>().Select(valor => $"'{NomeJson.De(valor)}'"))})");
            table.HasCheckConstraint(
                "ck_raridade_do_item_contagem", "alunos_com_item >= 0 AND alunos_com_item <= alunos_elegiveis");
        });
        builder.HasKey(linha => linha.ItemCodigo);

        // A raridade é do item: sai com ele, se um dia o item sair do catálogo.
        builder.HasOne<ItemCatalogoRow>().WithOne().HasForeignKey<RaridadeDoItemRow>(linha => linha.ItemCodigo)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RecalculoConfiguration : IEntityTypeConfiguration<RecalculoRow>
{
    public void Configure(EntityTypeBuilder<RecalculoRow> builder)
    {
        builder.ToTable("recalculo");
        builder.HasKey(linha => linha.Nome);
        builder.Property(linha => linha.Nome).HasMaxLength(50);
    }
}
