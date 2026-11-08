using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// O pedido de envio de foto, do registro até a confirmação ou a limpeza pela fila
// (ADR 0002). Os limites de tipo e tamanho do domínio repetidos no banco, como última
// barreira.
internal sealed class UploadPendenteConfiguration : IEntityTypeConfiguration<UploadPendente>
{
    public void Configure(EntityTypeBuilder<UploadPendente> builder)
    {
        builder.ToTable("upload_pendente", table =>
        {
            table.HasCheckConstraint(
                "ck_upload_pendente_finalidade",
                $"finalidade IN ({string.Join(", ", Enum.GetValues<FinalidadeDoUpload>().Select(valor => $"'{NomeJson.De(valor)}'"))})");
            table.HasCheckConstraint("ck_upload_pendente_tipo", $"tipo_de_conteudo = '{UploadPendente.TipoAceito}'");
            table.HasCheckConstraint(
                "ck_upload_pendente_tamanho_do_feed", $"tamanho_do_feed BETWEEN 1 AND {UploadPendente.TamanhoMaximoDoFeed}");
            table.HasCheckConstraint(
                "ck_upload_pendente_tamanho_da_grade", $"tamanho_da_grade BETWEEN 1 AND {UploadPendente.TamanhoMaximoDaGrade}");
        });
        builder.HasKey(upload => upload.Id);
        builder.Property(upload => upload.Id).ValueGeneratedNever();
        builder.Property(upload => upload.Finalidade)
            .HasConversion(finalidade => NomeJson.De(finalidade), texto => Enum.Parse<FinalidadeDoUpload>(texto, ignoreCase: true));

        // Uma chave, um objeto: nenhum pedido aponta para a foto de outro.
        builder.HasIndex(upload => upload.ChaveDoFeed).IsUnique();
        builder.HasIndex(upload => upload.ChaveDaGrade).IsUnique();
        builder.HasIndex(upload => upload.UsuarioId);

        // Sem cascata, como o resto do que o usuário grava: a purga da demonstração apaga
        // os uploads antes do usuário.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(upload => upload.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Confirmação e limpeza na mesma linha ao mesmo tempo: o xmin deixa só a primeira
        // gravar (LimparUploadAbandonadoHandler).
        builder.Property<uint>("Versao").IsRowVersion();

        builder.Ignore(upload => upload.EstaConfirmado);
        builder.Ignore(upload => upload.AssinaturaExpiraEm);
        builder.Ignore(upload => upload.LimparEm);
        builder.Ignore(upload => upload.Chaves);
    }
}
