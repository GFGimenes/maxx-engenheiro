using Maxx.Infrastructure.Fila;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

internal sealed class TrabalhoConfiguration : IEntityTypeConfiguration<Trabalho>
{
    public void Configure(EntityTypeBuilder<Trabalho> builder)
    {
        builder.ToTable("trabalho", table =>
        {
            table.HasCheckConstraint("ck_trabalho_estado", "estado IN ('pendente', 'falho')");
            table.HasCheckConstraint(
                "ck_trabalho_tentativas", $"tentativas BETWEEN 0 AND {PoliticaDeRetentativa.MaximoDeTentativas}");
        });
        builder.HasKey(trabalho => trabalho.Id);
        builder.Property(trabalho => trabalho.Id).UseIdentityAlwaysColumn();

        builder.Property(trabalho => trabalho.Tipo).HasMaxLength(100).IsRequired();
        builder.Property(trabalho => trabalho.Carga).HasColumnType("jsonb").IsRequired();
        builder.Property(trabalho => trabalho.Estado)
            .HasConversion(estado => estado == EstadoDoTrabalho.Falho ? "falho" : "pendente",
                valor => valor == "falho" ? EstadoDoTrabalho.Falho : EstadoDoTrabalho.Pendente)
            .HasMaxLength(20);
        builder.Property(trabalho => trabalho.UltimoErro).HasMaxLength(ConsumidorDeTrabalhos.TamanhoMaximoDoErro);

        // A busca do worker: o próximo pendente já disponível, na ordem de chegada.
        builder.HasIndex(trabalho => new { trabalho.DisponivelEm, trabalho.Id })
            .HasDatabaseName("ix_trabalho_pendente")
            .HasFilter("estado = 'pendente'");
    }
}
