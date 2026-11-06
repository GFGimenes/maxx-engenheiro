"""Gera as fontes estáticas do app a partir das variáveis do Google Fonts.

React Native não expõe eixo de fonte variável, então cada combinação de largura, peso e
itálico vira um arquivo (docs/decisoes/README.md, Tipografia). Roda dentro do contêiner de
gerar.sh; a saída é determinística e vai para o repositório, sem Python na CI.
"""

import hashlib
import sys
import urllib.request
from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

GOOGLE_FONTS = "https://raw.githubusercontent.com/google/fonts"
ARCHIVO = f"{GOOGLE_FONTS}/95f4904fc8bcf26d3420fe315560c96417c6dec7/ofl/archivo"
INSTRUMENT = f"{GOOGLE_FONTS}/0b58fb370093f9a9f4ff785d94405710b79de67c/ofl/instrumentsans"

ORIGENS = {
    "archivo": (
        f"{ARCHIVO}/Archivo%5Bwdth,wght%5D.ttf",
        "0e094a7d3c7c4c25cf1310c4b30014f1dae9332220b1c2c88f4fa996f0b05053",
    ),
    "archivo-italico": (
        f"{ARCHIVO}/Archivo-Italic%5Bwdth,wght%5D.ttf",
        "305d13b4eb80e62f7d517f78d7c045250d977552a420f2dbed906f314e761305",
    ),
    "instrument": (
        f"{INSTRUMENT}/InstrumentSans%5Bwdth,wght%5D.ttf",
        "b24f1812584816958afcf22e22d08e44318c5e51651e25d2438efdde389b33b1",
    ),
}

LICENCAS = {
    "OFL-Archivo.txt": (
        f"{ARCHIVO}/OFL.txt",
        "108b4e57c9c796d3d38d0428ca7ee39de47ad93187302718d9b2d8864b9b716b",
    ),
    "OFL-InstrumentSans.txt": (
        f"{INSTRUMENT}/OFL.txt",
        "9e27a72ed30eb49a08678f6a5d6ed98ec7ba5368f541637ee0683ec9134ef966",
    ),
}

# (arquivo sem extensão, origem, largura, peso, itálico). Só os pesos que a escala e os
# componentes usam: cada arquivo pesa no primeiro acesso da web.
INSTANCIAS = [
    ("Archivo112-700", "archivo", 112, 700, False),
    ("Archivo112-800", "archivo", 112, 800, False),
    ("Archivo112-800Italic", "archivo-italico", 112, 800, True),
    ("InstrumentSans-400", "instrument", 100, 400, False),
    ("InstrumentSans-500", "instrument", 100, 500, False),
    ("InstrumentSans-600", "instrument", 100, 600, False),
    ("InstrumentSans-700", "instrument", 100, 700, False),
]


def baixar(url: str, sha256: str, destino: Path) -> None:
    with urllib.request.urlopen(url) as resposta:
        conteudo = resposta.read()
    obtido = hashlib.sha256(conteudo).hexdigest()
    if obtido != sha256:
        raise SystemExit(f"Hash inesperado em {url}: {obtido}")
    destino.write_bytes(conteudo)


def renomear(fonte: TTFont, nome: str, italico: bool) -> None:
    # Cada arquivo precisa de nome PostScript próprio: o iOS registra uma fonte só por nome.
    tabela = fonte["name"]
    for id_nome in (16, 17, 21, 22, 25):
        tabela.removeNames(nameID=id_nome)
    familia = nome.removesuffix("Italic").replace("-", " ")
    estilo = "Italic" if italico else "Regular"
    for id_nome, texto in {
        1: familia,
        2: estilo,
        3: nome,
        4: f"{familia} {estilo}" if italico else familia,
        6: nome,
    }.items():
        tabela.setName(texto, id_nome, 3, 1, 0x409)
        tabela.setName(texto, id_nome, 1, 0, 0)


def instanciar(origem: Path, destino: Path, nome: str, largura: int, peso: int, italico: bool) -> None:
    # Sem recalcular a data: a mesma entrada gera exatamente os mesmos bytes.
    fonte = TTFont(origem, recalcTimestamp=False)
    estatica = instancer.instantiateVariableFont(fonte, {"wdth": largura, "wght": peso})
    if "STAT" in estatica:
        del estatica["STAT"]
    renomear(estatica, nome, italico)
    estatica.save(destino)


def main() -> None:
    saida = Path(sys.argv[1])
    origens = Path("/tmp/origens")
    origens.mkdir(exist_ok=True)

    for chave, (url, sha256) in ORIGENS.items():
        baixar(url, sha256, origens / f"{chave}.ttf")
    for arquivo, (url, sha256) in LICENCAS.items():
        baixar(url, sha256, saida / arquivo)

    for nome, chave, largura, peso, italico in INSTANCIAS:
        destino = saida / f"{nome}.ttf"
        instanciar(origens / f"{chave}.ttf", destino, nome, largura, peso, italico)
        print(f"{destino.name}: {destino.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()
