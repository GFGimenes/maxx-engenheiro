using Maxx.Domain.Fotos;

namespace Maxx.Domain.Tests.Fotos;

public class UlidTests
{
    [Fact]
    public void Tem_26_caracteres_de_crockford_e_comeca_pelo_instante()
    {
        var agora = new DateTimeOffset(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);

        var ulid = Ulid.Novo(agora);

        Assert.True(Ulid.EhValido(ulid));
        Assert.Matches("^[0-7][0-9A-HJKMNP-TV-Z]{25}$", ulid);

        // Os 10 primeiros caracteres são os 48 bits do instante: o mesmo milissegundo, o
        // mesmo prefixo.
        Assert.Equal(ulid[..10], Ulid.Novo(agora)[..10]);
    }

    [Fact]
    public void A_parte_aleatoria_nao_se_repete()
    {
        var agora = DateTimeOffset.UtcNow;

        var ulids = Enumerable.Range(0, 1000).Select(_ => Ulid.Novo(agora)).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(1000, ulids.Count);
    }

    [Fact]
    public void Ordena_pelo_instante()
    {
        var antes = Ulid.Novo(new DateTimeOffset(2026, 11, 10, 13, 0, 0, TimeSpan.Zero));
        var depois = Ulid.Novo(new DateTimeOffset(2026, 11, 10, 13, 0, 1, TimeSpan.Zero));

        Assert.True(string.CompareOrdinal(antes, depois) < 0);
    }

    [Fact]
    public void Codifica_o_instante_em_big_endian()
    {
        // O epoch (instante zero) dá dez zeros na frente.
        Assert.StartsWith("0000000000", Ulid.Novo(DateTimeOffset.UnixEpoch), StringComparison.Ordinal);

        // 1 ms: o último caractere do instante vale 1.
        Assert.StartsWith("0000000001", Ulid.Novo(DateTimeOffset.UnixEpoch.AddMilliseconds(1)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FA")]
    [InlineData("81ARZ3NDEKTSV4RRFFQ69G5FAV")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAU")]
    [InlineData("01arz3ndektsv4rrffq69g5fav")]
    public void Recusa_o_que_nao_e_ulid(string? texto) => Assert.False(Ulid.EhValido(texto));
}
