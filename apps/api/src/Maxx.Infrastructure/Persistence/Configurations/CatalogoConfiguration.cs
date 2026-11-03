using Maxx.Domain.Catalogo;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// O catálogo entra só pelo seeder, nunca por HasData em migration. As checks repetem no
// banco o que o validador do domínio garante; as de tier e faixa saem das próprias
// enumerações do domínio, para não haver uma segunda cópia das faixas escrita à mão.
internal sealed class ItemCatalogoConfiguration : IEntityTypeConfiguration<ItemCatalogoRow>
{
    private static readonly string _medalha = NomeJson.De(TipoDeItem.Medalha);
    private static readonly string _premio = NomeJson.De(TipoDeItem.PremioParticipacao);
    private static readonly string _conquista = NomeJson.De(TipoDeItem.Conquista);

    public void Configure(EntityTypeBuilder<ItemCatalogoRow> builder)
    {
        builder.ToTable("item_catalogo", table =>
        {
            table.HasCheckConstraint("ck_item_catalogo_tipo", $"tipo IN ({Lista<TipoDeItem>()})");
            table.HasCheckConstraint("ck_item_catalogo_tier", $"tier IN ({Lista<Tier>()})");
            table.HasCheckConstraint("ck_item_catalogo_pontos_na_faixa", PontosNaFaixa());
            table.HasCheckConstraint("ck_item_catalogo_versao", "versao >= 1");
            table.HasCheckConstraint(
                "ck_item_catalogo_familia", "(familia_codigo IS NULL) = (ordem_na_familia IS NULL)");
            table.HasCheckConstraint(
                "ck_item_catalogo_vigencia", "vigencia_inicio IS NULL OR vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio");
            table.HasCheckConstraint("ck_item_catalogo_campos_por_tipo", CamposPorTipo());
        });

        builder.HasKey(item => item.Codigo);
        builder.Property(item => item.Regra).HasColumnType("jsonb");
        builder.Property(item => item.Versao).HasDefaultValue(1);

        // Um passo por posição na trilha.
        builder.HasIndex(item => new { item.FamiliaCodigo, item.OrdemNaFamilia }).IsUnique();

        builder.HasOne<FamiliaRow>()
            .WithMany()
            .HasForeignKey(item => item.FamiliaCodigo)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string Lista<T>()
        where T : struct, Enum =>
        string.Join(", ", Enum.GetValues<T>().Select(valor => $"'{NomeJson.De(valor)}'"));

    private static string PontosNaFaixa() =>
        string.Join(" OR ", Enum.GetValues<Tier>().Select(tier =>
        {
            var faixa = Tiers.Faixa(tier);
            return $"(tier = '{NomeJson.De(tier)}' AND pontos BETWEEN {faixa.Minimo} AND {faixa.Maximo})";
        }));

    // Cada tipo preenche os seus campos e deixa os dos outros nulos.
    private static string CamposPorTipo()
    {
        const string medalha = "base, repetivel, limite_por_periodo";
        const string premio = "tipo_de_evento, forma_do_evento, exige_foto";
        const string conquista = "gatilho, regra";

        static string Todos(string campos, string condicao) =>
            string.Join(" AND ", campos.Split(", ").Select(campo => $"{campo} {condicao}"));

        return
            $"(tipo = '{_medalha}' AND {Todos(medalha + ", chave_de_deduplicacao", "IS NOT NULL")} " +
            $"AND {Todos(premio + ", " + conquista, "IS NULL")}) " +
            $"OR (tipo = '{_premio}' AND {Todos(premio + ", chave_de_deduplicacao", "IS NOT NULL")} " +
            $"AND {Todos(medalha + ", " + conquista, "IS NULL")}) " +
            $"OR (tipo = '{_conquista}' AND {Todos(conquista, "IS NOT NULL")} " +
            $"AND {Todos(medalha + ", " + premio + ", chave_de_deduplicacao", "IS NULL")})";
    }
}

internal sealed class FamiliaConfiguration : IEntityTypeConfiguration<FamiliaRow>
{
    public void Configure(EntityTypeBuilder<FamiliaRow> builder)
    {
        builder.ToTable("familia", table => table.HasCheckConstraint("ck_familia_passos", "passos >= 1"));
        builder.HasKey(familia => familia.Codigo);
    }
}
