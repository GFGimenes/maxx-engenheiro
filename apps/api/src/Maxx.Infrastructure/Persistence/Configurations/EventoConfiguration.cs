using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// O evento do prêmio de participação, semeado ou digitado. As duas origens dividem a tabela,
// e as constraints cobram o que cada uma tem: o semeado tem código e itens, e pode ter curso;
// o texto livre tem quem o digitou, e nada mais. Os itens aceitos são códigos do catálogo,
// conferidos pelo seed: um text[] não leva chave estrangeira.
internal sealed class EventoConfiguration : IEntityTypeConfiguration<Evento>
{
    public void Configure(EntityTypeBuilder<Evento> builder)
    {
        builder.ToTable("evento", table =>
        {
            table.HasCheckConstraint("ck_evento_fim_depois_do_inicio", "fim >= inicio");
            table.HasCheckConstraint(
                "ck_evento_origem",
                "(origem = 'semeado' AND codigo IS NOT NULL AND cardinality(itens_aceitos) > 0 AND criado_por_id IS NULL) " +
                "OR (origem = 'textoLivre' AND codigo IS NULL AND cardinality(itens_aceitos) = 0 AND curso_id IS NULL AND criado_por_id IS NOT NULL)");
        });
        builder.HasKey(evento => evento.Id);
        builder.Property(evento => evento.Id).ValueGeneratedNever();

        builder.Property(evento => evento.Codigo).HasMaxLength(Evento.TamanhoMaximoDoCodigo);
        builder.HasIndex(evento => evento.Codigo).IsUnique();
        builder.Property(evento => evento.Nome).IsRequired();
        builder.Property(evento => evento.Origem)
            .HasConversion(origem => NomeJson.De(origem), texto => Enum.Parse<OrigemDoEvento>(texto, ignoreCase: true));
        builder.Property(evento => evento.ItensAceitos).IsRequired();

        // A lista de eventos reivindicáveis lê os semeados pelo início.
        builder.HasIndex(evento => new { evento.Origem, evento.Inicio });

        builder.HasOne<Curso>().WithMany().HasForeignKey(evento => evento.CursoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(evento => evento.CriadoPorId).OnDelete(DeleteBehavior.Restrict);
    }
}
