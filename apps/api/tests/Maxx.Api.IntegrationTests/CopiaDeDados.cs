using System.Text.Json.Nodes;

namespace Maxx.Api.IntegrationTests;

// Uma cópia temporária de data/, para o teste mudar arquivo sem tocar no repositório.
internal sealed class CopiaDeDados : IDisposable
{
    public CopiaDeDados()
    {
        Diretorio = Path.Combine(Path.GetTempPath(), $"maxx-dados-{Guid.NewGuid():N}");
        foreach (var arquivo in Directory.GetFiles(Repositorio.DiretorioDeDados, "*", SearchOption.AllDirectories))
        {
            var copia = Path.Combine(Diretorio, Path.GetRelativePath(Repositorio.DiretorioDeDados, arquivo));
            Directory.CreateDirectory(Path.GetDirectoryName(copia)!);
            File.Copy(arquivo, copia);
        }
    }

    public string Diretorio { get; }

    public void Dispose() => Directory.Delete(Diretorio, recursive: true);

    // caminho relativo a data/, como "catalogo/medalhas.json".
    public void Alterar(string caminho, Action<JsonNode> alteracao)
    {
        var completo = Path.Combine(Diretorio, caminho);
        var raiz = JsonNode.Parse(File.ReadAllText(completo))!;
        alteracao(raiz);
        File.WriteAllText(completo, raiz.ToJsonString());
    }

    public void AlterarItem(string arquivo, string codigo, Action<JsonObject> alteracao) =>
        Alterar($"catalogo/{arquivo}", raiz =>
            alteracao(raiz["itens"]!.AsArray().Single(item => (string?)item!["codigo"] == codigo)!.AsObject()));

    public void RemoverItem(string arquivo, string codigo) =>
        Alterar($"catalogo/{arquivo}", raiz =>
        {
            var itens = raiz["itens"]!.AsArray();
            itens.Remove(itens.Single(item => (string?)item!["codigo"] == codigo));
        });
}
