#!/usr/bin/env bash
# Regenera apps/mobile/assets/fonts a partir das fontes variáveis, em versões travadas.
# Precisa só de Docker: quem usa o app não roda isto, os arquivos ficam no repositório.
set -euo pipefail

raiz="$(cd "$(dirname "$0")/../.." && pwd)"
saida="$raiz/apps/mobile/assets/fonts"
mkdir -p "$saida"

# MSYS_NO_PATHCONV: no Git Bash do Windows, impede a reescrita dos caminhos do contêiner.
MSYS_NO_PATHCONV=1 docker run --rm \
  -v "$raiz/design/fontes:/scripts:ro" \
  -v "$saida:/saida" \
  python:3.13-slim@sha256:09a17cad02668ae61ece136b4fd36ac3849e14f670ebedb637ea3e15580525bc \
  sh -c 'pip install --quiet --root-user-action=ignore --disable-pip-version-check fonttools==4.68.0 && cd /tmp && python -I /scripts/instanciar.py /saida'
