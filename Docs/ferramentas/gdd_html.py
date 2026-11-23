"""Gera Docs/GDD.html a partir de Docs/GDD.md com o template html-default
do Immersive Games Documentation Framework (repositório com.immersivegames.docs).

Uso (da raiz do repositório):
    python Docs/ferramentas/gdd_html.py
    python Docs/ferramentas/gdd_html.py --template ../com.immersivegames.docs/templates/html-default

O HTML sai num arquivo único (CSS, JS e logos embutidos), que abre direto no
navegador, sem servidor. Só usa a biblioteca padrão do Python 3.9+.
O GDD.md é a cópia exportada do Claude Doc; não editar o HTML à mão.
"""
from __future__ import annotations

import argparse
import base64
import html
import re
import sys
import unicodedata
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SOURCE = REPO / "Docs" / "GDD.md"
OUTPUT = REPO / "Docs" / "GDD.html"
DEFAULT_TEMPLATE = REPO.parent / "com.immersivegames.docs" / "templates" / "html-default"

CSS_FILES = ["variables.css", "typography.css", "layout.css", "components.css"]
JS_FILES = ["navigation.js", "search.js", "glossary.js", "ui.js"]

# Textos do template (em inglês) que aparecem para quem lê.
JS_PT = {
    '"Search all documents..."': '"Buscar em todos os documentos..."',
    '"Search active document..."': '"Buscar no GDD..."',
    '"Type in the header search field to search all documents."': '"Digite no campo de busca para pesquisar em todos os documentos."',
    '"Type in the header search field to search the active document."': '"Digite no campo de busca para pesquisar no GDD."',
    '"Active" : "Global"': '"Atual" : "Global"',
    "`Document: ${result.documentTitle}`": "`Documento: ${result.documentTitle}`",
    "`Section: ${result.sectionTitle}`": "`Seção: ${result.sectionTitle}`",
    '"Search is ready."': '"Busca pronta."',
    '"No results found."': '"Nenhum resultado encontrado."',
    '"Search reset for active document."': '"Busca limpa."',
    '"Copy code to clipboard"': '"Copiar código"',
    '"Copied" : "Copy failed"': '"Copiado" : "Não copiou"',
    '"Copy failed"': '"Não copiou"',
    '"Copy"': '"Copiar"',
}

EXTRA_CSS = """
/* Pro Gol: acréscimos ao template padrão (iguais aos do GDD do PEGA). */
.docs-gdd-hero {
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--docs-color-accent) 18%, transparent), transparent 45%),
    linear-gradient(135deg, var(--docs-color-surface-raised), var(--docs-color-surface));
}
.docs-gdd-meta { display: flex; flex-wrap: wrap; gap: var(--docs-spacing-sm); margin-top: var(--docs-spacing-md); }
.docs-table-wrap { overflow-x: auto; }
/* O template só dá scroll-margin aos títulos; o link do menu aponta para a seção. */
.docs-section[id] { scroll-margin-top: calc(var(--docs-layout-topbar-height) + var(--docs-spacing-lg)); }
"""

# O realce do menu do template usa IntersectionObserver com limiar de 10% da
# seção, que seções longas nunca atingem; aqui marca a última seção que já
# passou do topo da tela.
EXTRA_JS = """
(function () {
  const sections = Array.from(document.querySelectorAll("[data-document] .docs-section[id]"));
  function update() {
    const limit = window.innerHeight * 0.3;
    let current = sections[0];
    for (const section of sections) {
      if (section.getBoundingClientRect().top <= limit) current = section; else break;
    }
    if (!current) return;
    document.querySelectorAll("[data-nav-target]").forEach((link) => {
      link.classList.toggle("is-active", link.dataset.navTarget === current.id);
    });
  }
  window.addEventListener("scroll", () => requestAnimationFrame(update), { passive: true });
  update();
})();
"""


# ---------------------------------------------------------------- Markdown

def slugify(value: str, used: set[str]) -> str:
    value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    value = re.sub(r"[^\w\s-]", "", value)
    value = re.sub(r"\s+", "-", value.strip().lower()) or "secao"
    base, n = value, 2
    while value in used:
        value, n = f"{base}-{n}", n + 1
    used.add(value)
    return value


def inline(text: str) -> str:
    """Negrito, itálico, código, links e URLs soltas; o resto vira texto escapado."""
    # Colchetes escapados pelo exportador viram link de verdade: \[nome\](url).
    text = text.replace("\\[", "[").replace("\\]", "]")
    keep: list[str] = []

    def stash(fragment: str) -> str:
        keep.append(fragment)
        return f"\x00{len(keep) - 1}\x00"

    text = re.sub(r"\\(.)", lambda m: stash(html.escape(m.group(1))), text)
    text = re.sub(r"`([^`]+)`", lambda m: stash(f"<code>{html.escape(m.group(1))}</code>"), text)
    text = re.sub(
        r"\[([^\]]+)\]\((https?://[^)\s]+)\)",
        lambda m: stash(f'<a href="{html.escape(m.group(2))}">{html.escape(m.group(1))}</a>'),
        text,
    )
    text = re.sub(
        r"https?://[^\s)<]+[^\s)<.,;:]",
        lambda m: stash(f'<a href="{html.escape(m.group(0))}">{html.escape(m.group(0))}</a>'),
        text,
    )
    text = html.escape(text, quote=False)
    text = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", text)
    text = re.sub(r"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", r"<em>\1</em>", text)
    return re.sub(r"\x00(\d+)\x00", lambda m: keep[int(m.group(1))], text)


def split_row(line: str) -> list[str]:
    cells = re.split(r"(?<!\\)\|", line.strip().strip("|"))
    return [c.strip() for c in cells]


def render_blocks(lines: list[str]) -> str:
    """Converte um trecho de Markdown (sem títulos) em HTML."""
    out: list[str] = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1
            continue
        if line.lstrip().startswith("|"):
            rows = []
            while i < len(lines) and lines[i].lstrip().startswith("|"):
                rows.append(lines[i])
                i += 1
            header = split_row(rows[0])
            body = [split_row(r) for r in rows[2:]]
            out.append('<div class="docs-table-wrap"><table class="docs-table"><thead><tr>')
            out.extend(f"<th>{inline(c)}</th>" for c in header)
            out.append("</tr></thead><tbody>")
            for row in body:
                out.append("<tr>" + "".join(f"<td>{inline(c)}</td>" for c in row) + "</tr>")
            out.append("</tbody></table></div>")
            continue
        m = re.match(r"^(\s*)([-*]|\d+\.)\s+", line)
        if m:
            ordered = m.group(2)[0].isdigit()
            items: list[list[str]] = []
            while i < len(lines):
                cur = lines[i]
                mm = re.match(r"^(\s*)([-*]|\d+\.)\s+(.*)$", cur)
                if mm and len(mm.group(1)) == len(m.group(1)) and mm.group(2)[0].isdigit() == ordered:
                    items.append([mm.group(3)])
                elif cur.strip() and cur.startswith(" ") and items:
                    items[-1].append(cur[len(m.group(1)) + 2:] if len(cur) > 2 else cur.strip())
                elif not cur.strip() and i + 1 < len(lines) and re.match(
                    r"^(\s*)([-*]|\d+\.)\s+|^\s{2,}\S", lines[i + 1]
                ):
                    pass
                else:
                    break
                i += 1
            tag = "ol" if ordered else "ul"
            start = re.match(r"\d+", m.group(2))
            attr = f' start="{start.group(0)}"' if ordered and start and start.group(0) != "1" else ""
            out.append(f"<{tag}{attr}>")
            for item in items:
                first, rest = item[0], item[1:]
                inner = inline(first)
                if rest:
                    inner += render_blocks(rest)
                out.append(f"<li>{inner}</li>")
            out.append(f"</{tag}>")
            continue
        if line.startswith(">"):
            quote = []
            while i < len(lines) and lines[i].startswith(">"):
                quote.append(lines[i][1:].lstrip())
                i += 1
            out.append(f"<blockquote>{render_blocks(quote)}</blockquote>")
            continue
        para = []
        while i < len(lines) and lines[i].strip() and not re.match(r"^(\s*)([-*]|\d+\.)\s+|^\||^>", lines[i]):
            para.append(lines[i].strip())
            i += 1
        out.append(f"<p>{inline(' '.join(para))}</p>")
    return "\n".join(out)


def parse(md: str):
    """Separa título, nota de exportação, byline e seções (## e ###)."""
    lines = md.splitlines()
    title = "GDD"
    intro: list[str] = []
    sections: list[tuple[int, str, list[str]]] = []
    current = intro
    for line in lines:
        h = re.match(r"^(#{1,4})\s+(.*)$", line)
        if h:
            level = len(h.group(1))
            if level == 1:
                title = h.group(2).strip()
                continue
            sections.append((level, h.group(2).strip(), []))
            current = sections[-1][2]
            continue
        current.append(line)
    return title, intro, sections


# ---------------------------------------------------------------- Página

def data_uri(path: Path) -> str:
    mime = "image/svg+xml" if path.suffix == ".svg" else "image/png"
    return f"data:{mime};base64," + base64.b64encode(path.read_bytes()).decode("ascii")


def build(template: Path) -> str:
    md = SOURCE.read_text(encoding="utf-8")
    title, intro, sections = parse(md)

    note = " ".join(l[1:].strip() for l in intro if l.startswith(">"))
    byline = " ".join(l.strip() for l in intro if l.strip() and not l.startswith(">"))
    rev = re.search(r"rev (\d+)", note)
    exported = re.search(r"em (\d{2}/\d{2}/\d{4})", note)

    used: set[str] = set()
    body: list[str] = []
    for level, heading, content in sections:
        sid = slugify(heading, used)
        tag = "h3" if level == 2 else "h4" if level == 3 else "h5"
        body.append(
            f'<section class="docs-section" id="{sid}" data-title="{html.escape(heading)}">'
            f"<{tag}>{inline(heading)}</{tag}>\n{render_blocks(content)}\n</section>"
        )

    css = "\n".join((template / "css" / f).read_text(encoding="utf-8") for f in CSS_FILES) + EXTRA_CSS
    js_parts = []
    for f in JS_FILES:
        code = (template / "js" / f).read_text(encoding="utf-8")
        if f in ("search.js", "ui.js"):
            for en, pt in JS_PT.items():
                code = code.replace(en, pt)
        js_parts.append(code)
    js_parts.append(EXTRA_JS)
    js = "\n".join(js_parts).replace("</script", "<\\/script")

    images = template / "assets" / "images"
    logo_white = data_uri(images / "immersive-games-logo-full-white.png")
    logo_black = data_uri(images / "immersive-games-logo-full-black.png")

    badges = ["Português", "Fonte: Claude Doc"]
    if rev:
        badges.append(f"Revisão {rev.group(1)}")
    if exported:
        badges.append(f"Exportado em {exported.group(1)}")
    badges_html = "".join(f'<span class="docs-badge">{html.escape(b)}</span>' for b in badges)

    return f"""<!doctype html>
<html lang="pt-BR" data-theme="dark">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{html.escape(title)}</title>
  <style>
{css}
  </style>
</head>
<body>
  <a class="docs-skip-link" href="#docs-main">Pular para o conteúdo</a>

  <div class="docs-topbar">
    <header class="docs-header">
      <div class="docs-header__brand">
        <span class="docs-header__logo-set" aria-hidden="true">
          <img class="docs-header__logo docs-header__logo--dark" src="{logo_white}" alt="" width="180" height="65">
          <img class="docs-header__logo docs-header__logo--light" src="{logo_black}" alt="" width="180" height="65">
        </span>
        <div>
          <h1>Pro Gol!</h1>
        </div>
      </div>

      <div class="docs-header__tools">
        <label class="docs-search-label" for="docs-search-input">Buscar no GDD</label>
        <input id="docs-search-input" class="docs-search-input" type="search" placeholder="Buscar no GDD..." autocomplete="off">
        <button class="docs-button docs-button--ghost" type="button" data-action="toggle-theme" aria-label="Alternar tema claro ou escuro">Tema</button>
        <button class="docs-button docs-button--ghost docs-mobile-only" type="button" data-action="toggle-sidebar" aria-expanded="false" aria-controls="docs-sidebar">Menu</button>
      </div>
    </header>

    <nav class="docs-tabs" role="tablist" aria-label="Documentos">
      <button id="docs-tab-gdd" class="docs-tab is-active" type="button" role="tab" data-document-target="gdd" aria-selected="true" aria-controls="docs-panel-gdd">Game Design Document</button>
    </nav>
  </div>

  <div class="docs-search-status" id="docs-search-status" role="status" aria-live="polite">Busca pronta.</div>

  <div class="docs-shell">
    <aside class="docs-sidebar" id="docs-sidebar" aria-label="Navegação do documento">
      <div class="docs-sidebar__header">
        <span>Navegação</span>
      </div>
      <nav class="docs-sidebar__nav" data-sidebar-nav></nav>
      <div class="docs-sidebar__footer">
        <button class="docs-back-to-top" type="button" data-action="back-to-top">&uarr; Voltar ao topo</button>
      </div>
    </aside>

    <main class="docs-main" id="docs-main" tabindex="-1">
      <section class="docs-search-panel docs-content" data-search-panel hidden aria-labelledby="docs-search-panel-title">
        <div class="docs-search-panel__header">
          <p class="docs-kicker">Busca</p>
          <h2 id="docs-search-panel-title">Resultados da busca</h2>
          <p data-search-summary>Digite no campo de busca para pesquisar no GDD.</p>
        </div>
        <div class="docs-search-results" data-search-results></div>
        <div class="docs-empty-state" data-search-empty hidden>
          <h2>Nenhum resultado encontrado.</h2>
          <p>Tente outro termo ou limpe a busca.</p>
        </div>
      </section>

      <section id="docs-panel-gdd" class="docs-document is-active" role="tabpanel" aria-labelledby="docs-tab-gdd" data-document="gdd" data-document-title="Game Design Document">
        <article class="docs-content">
          <header id="gdd-titulo" class="docs-page-header docs-gdd-hero">
            <p class="docs-kicker">Game Design Document</p>
            <h2>{inline(title)}</h2>
            <p>{inline(byline)}</p>
            <div class="docs-gdd-meta" aria-label="Dados do documento">{badges_html}</div>
          </header>
{chr(10).join(body)}
        </article>
      </section>
    </main>
  </div>

  <footer class="docs-footer">
    <p>{inline(note) if note else "GDD do Pro Gol!"} Gerado com <code>com.immersivegames.docs</code> (template html-default).</p>
    <nav aria-label="Links do rodapé">
      <a href="GDD.md">Markdown</a>
      <a href="#gdd-titulo">Voltar ao topo</a>
    </nav>
  </footer>

  <script>
{js}
  </script>
</body>
</html>
"""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--template", type=Path, default=DEFAULT_TEMPLATE,
                        help="pasta templates/html-default do com.immersivegames.docs")
    args = parser.parse_args()
    if not (args.template / "css" / "variables.css").exists():
        print(f"Template não encontrado em {args.template}. Clone o com.immersivegames.docs ao lado "
              "deste repositório ou passe --template.", file=sys.stderr)
        return 1
    OUTPUT.write_text(build(args.template), encoding="utf-8", newline="\n")
    print(f"Gerado {OUTPUT.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
