using Maxx.Domain.Identidade;
using Maxx.Domain.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// Curtida, comentário e seguimento. O que pende de um post sai com ele quando o autor o
// apaga (cascata no post); o que pende de um usuário, não: a purga da demonstração apaga
// antes do usuário, como o resto.
internal sealed class CurtidaConfiguration : IEntityTypeConfiguration<Curtida>
{
    public void Configure(EntityTypeBuilder<Curtida> builder)
    {
        builder.ToTable("curtida");

        // Uma por pessoa e post: curtir de novo não duplica, nem na corrida.
        builder.HasKey(curtida => new { curtida.PostId, curtida.UsuarioId }).HasName("pk_curtida");
        builder.HasIndex(curtida => curtida.UsuarioId);

        builder.HasOne<Post>().WithMany().HasForeignKey(curtida => curtida.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(curtida => curtida.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ComentarioConfiguration : IEntityTypeConfiguration<Comentario>
{
    public void Configure(EntityTypeBuilder<Comentario> builder)
    {
        builder.ToTable("comentario", table =>
        {
            table.HasCheckConstraint("ck_comentario_texto", "length(texto) > 0");
            table.HasCheckConstraint("ck_comentario_remocao", "(removido_em IS NULL) = (removido_por_id IS NULL)");
        });
        builder.HasKey(comentario => comentario.Id);
        builder.Property(comentario => comentario.Id).ValueGeneratedNever();

        // Os comentários de um post, do mais antigo ao mais novo: a lista anda por aqui.
        builder.HasIndex(comentario => new { comentario.PostId, comentario.CriadoEm, comentario.Id });
        builder.HasIndex(comentario => comentario.AutorId);

        builder.HasOne<Post>().WithMany().HasForeignKey(comentario => comentario.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(comentario => comentario.AutorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(comentario => comentario.RemovidoPorId).OnDelete(DeleteBehavior.Restrict);

        // Duas remoções ao mesmo tempo: o xmin deixa só a primeira gravar.
        builder.Property<uint>("Versao").IsRowVersion();

        builder.Ignore(comentario => comentario.EstaRemovido);
    }
}

internal sealed class SeguimentoConfiguration : IEntityTypeConfiguration<Seguimento>
{
    public void Configure(EntityTypeBuilder<Seguimento> builder)
    {
        builder.ToTable("seguimento", table => table.HasCheckConstraint("ck_seguimento_outro", "seguidor_id <> seguido_id"));

        // Um seguimento por par; a chave começa pelo seguidor, que é por onde o feed lê
        // "quem eu sigo". O índice do seguido conta os seguidores no perfil.
        builder.HasKey(seguimento => new { seguimento.SeguidorId, seguimento.SeguidoId }).HasName("pk_seguimento");
        builder.HasIndex(seguimento => seguimento.SeguidoId);

        builder.HasOne<Usuario>().WithMany().HasForeignKey(seguimento => seguimento.SeguidorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(seguimento => seguimento.SeguidoId).OnDelete(DeleteBehavior.Restrict);
    }
}
