"""Typeset the authored field manual and scenario cards: python Paper/make_kit.py SCENARIO."""

import argparse
import json
import re
from pathlib import Path
from xml.sax.saxutils import escape

import reportlab
from reportlab.lib import colors
from reportlab.lib.pagesizes import landscape, letter
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import inch
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph, Table, TableStyle


PAPER = Path(__file__).resolve().parent
ROOT = PAPER.parent
MARGIN = 42
WIDTH, HEIGHT = letter
TEXT_WIDTH = WIDTH - 2 * MARGIN
STATION_LABEL_FT = 500  # WO-04: ledger labels every 500 ft.
TOKEN_DIAMETER = 2 * inch  # WO-04: two-inch circles, printed at actual size.
CARDS_PER_PAGE = 4  # WO-04: four permit/inspection cards per sheet.
TOKENS_PER_PAGE = 12  # Letter layout: three columns of four two-inch circles.
SEATS = "Site / Plan / Rules"  # WO-04: the three seats on the cover.
RETURN_INSTRUCTION = (
    "Read the code to Plan. Return this card when the zone is backfilled and signed."
)
SECTION = re.compile(r"^## (.+?) \(([^)]+)\)\s*$", re.MULTILINE)
PLACEHOLDER = re.compile(r"\{\{([^{}]+)\}\}")


def register_fonts():
    # ReportLab ships these fonts; embedding keeps the manual's math glyphs printable.
    font_dir = Path(reportlab.__file__).parent / "fonts"
    for name, filename in (
        ("KitSans", "Vera.ttf"), ("KitSans-Bold", "VeraBd.ttf"),
        ("KitSans-Italic", "VeraIt.ttf"), ("KitSans-BoldItalic", "VeraBI.ttf"),
    ):
        pdfmetrics.registerFont(TTFont(name, str(font_dir / filename)))
    pdfmetrics.registerFontFamily(
        "KitSans", normal="KitSans", bold="KitSans-Bold",
        italic="KitSans-Italic", boldItalic="KitSans-BoldItalic",
    )


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def markdown_table(headers, rows):
    return "\n".join(
        "| " + " | ".join(map(str, row)) + " |"
        for row in [headers, ["---"] * len(headers), *rows]
    )


def fill_placeholders(text, scenario, catalog, missing):
    zones = scenario["job"]["zones"]
    permits = scenario["permits"]
    inspections = scenario["inspections"]
    derived = {
        "job.zoneCount": len(zones),
        "job.zoneLengthFt": zones[0]["toFt"] - zones[0]["fromFt"],
        "permits.heldByText": (
            "all issued and posted by the PM before Day 1"
            if permits["heldBy"] == "pm"
            else f"held by Rules; the city allows {permits['openZoneLimit']} zone(s) open at a time"
        ),
        "permits.cardsTable": markdown_table(
            ["Zone", "Permit number", "Code"],
            [[c["zone"], c["number"], c["code"]] for c in permits["cards"]],
        ),
        "inspections.cardsTable": markdown_table(
            ["Zone", "Inspection card number"],
            [[c["zone"], c["number"]] for c in inspections["cards"]],
        ),
        "inspections.checklistBullets": "\n".join(
            f"- [ ] {item}" for item in inspections["checklist"]
        ),
    }

    def replace(match):
        key = match[1].strip()
        if key in derived:
            return str(derived[key])
        parts = key.split(".")
        # Never expose event/deviation payloads or author-only metadata via a template.
        forbidden = {"events", "deviations", "effect", "trigger", "cardWhenProtected"}
        if not any(p.startswith("_") or p in forbidden for p in parts):
            source = {**catalog, **scenario, "city": catalog["cities"][scenario["city"]]}
            try:
                for part in parts:
                    source = source[part]
                if isinstance(source, (str, int, float, bool)):
                    return str(source)
            except (KeyError, TypeError):
                pass
        missing.add(key)
        return "[to validate]"

    # WO-05a: format authored and substituted dollar amounts, including validation placeholders.
    return re.sub(
        r"\$([0-9]+(?:,[0-9]+)*)(\.[0-9]+)?",
        lambda match: f"${int(match[1].replace(',', '')):,}{match[2] or ''}",
        PLACEHOLDER.sub(replace, text),
    )


def manual_sections(source, scenario, catalog):
    headings = list(SECTION.finditer(source))
    sections = {}
    for index, heading in enumerate(headings):
        end = headings[index + 1].start() if index + 1 < len(headings) else len(source)
        body = source[heading.end():end].strip()
        for section_id in heading[2].split(","):
            sections[section_id.strip()] = (heading[1], body)
    missing = set()
    selected = []
    for section_id in scenario["kit"]["manualPages"]:
        if section_id not in sections:
            raise ValueError(f"manual_v0.md has no section {section_id!r}")
        title, body = sections[section_id]
        selected.append((section_id, title, fill_placeholders(body, scenario, catalog, missing)))
    # The typesetting note is author instruction, not player-facing manual text.
    preface = source[:headings[0].start()].split("Typesetting notes", 1)[0].strip()
    return preface, selected, missing


def inline(text):
    text = escape(text)
    text = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", text)
    return re.sub(r"\*([^*]+)\*", r"<i>\1</i>", text)


def plain(text):
    result = []
    for line in text.splitlines():
        if line.strip() == "---" or re.fullmatch(r"[| :\-]+", line.strip()):
            continue
        line = re.sub(r"^#{1,6} ", "", line)
        line = line.replace("**", "").replace("*", "")
        if line.startswith("|"):
            line = " | ".join(cell.strip() for cell in line.strip("|").split("|"))
        result.append(line)
    return "\n".join(result).strip()


def paragraph(text, size=10.5, bold=False, **kwargs):
    style = ParagraphStyle(
        "kit", fontName="KitSans-Bold" if bold else "KitSans",
        fontSize=size, leading=size * 1.24, spaceAfter=6, **kwargs,
    )
    return Paragraph(inline(text), style)


def table_widths(headers, width):
    count = len(headers)
    if count == 2:
        fractions = [0.24, 0.76]
    elif headers[0] == "Option":
        fractions = [0.23, 0.45, 0.32]
    elif headers[0] == "Site says":
        fractions = [0.35, 0.18, 0.32, 0.15]
    elif headers[0] == "Hazard":
        fractions = [0.22, 0.12, 0.66]
    elif headers[0] == "Kind":
        fractions = [0.23, 0.12, 0.12, 0.23, 0.30]
    elif headers[0] == "Machine":
        fractions = [0.32, 0.14, 0.16, 0.18, 0.20]
    else:
        fractions = [1 / count] * count
    return [fraction * width for fraction in fractions]


def flowables(markdown, width, size=10.5):
    lines = markdown.splitlines()
    blocks = []
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        i += 1
        if not line or line == "---":
            continue
        if line.startswith("|"):
            rows = [line]
            while i < len(lines) and lines[i].strip().startswith("|"):
                rows.append(lines[i].strip())
                i += 1
            cells = [[cell.strip() for cell in row.strip("|").split("|")] for row in rows]
            cells = [row for row in cells if not all(re.fullmatch(r":?-+:?", c) for c in row)]
            has_header = any(cells[0])
            if not has_header:
                cells.pop(0)
            widths = table_widths(cells[0] if has_header else ["", ""], width)
            data = [[paragraph(c, size - 0.3, bold=has_header and r == 0)
                     for c in row] for r, row in enumerate(cells)]
            table = Table(data, colWidths=widths, hAlign="LEFT")
            styles = [
                ("GRID", (0, 0), (-1, -1), 0.5, colors.black),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
                ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
            ]
            if has_header:
                styles.append(("BACKGROUND", (0, 0), (-1, 0), colors.Color(0.94, 0.94, 0.94)))
            table.setStyle(TableStyle(styles))
            table.spaceAfter = 10
            blocks.append(table)
        elif match := re.match(r"^#{1,3} (.+)", line):
            blocks.append(paragraph(match[1], size + 2, bold=True))
        elif match := re.match(r"(\d+)\. (Site|Plan|Rules): (.*)", line):
            # Put the named seat in a separate margin without changing the procedure.
            table = Table([[
                paragraph(match[1] + ".", size), paragraph(match[2] + ":", size, bold=True),
                paragraph(match[3], size),
            ]], colWidths=[22, 46, width - 68])
            table.setStyle(TableStyle([
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 0),
                ("RIGHTPADDING", (0, 0), (-1, -1), 3),
                ("TOPPADDING", (0, 0), (-1, -1), 0),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
            ]))
            blocks.append(table)
        elif line.startswith("- "):
            blocks.append(paragraph(line[2:], size, leftIndent=12, bulletIndent=0, bulletText="\u2022"))
        else:
            while (i < len(lines) and lines[i].strip()
                   and not re.match(r"^(?:\||#|- |\d+\.)", lines[i].strip())):
                line += " " + lines[i].strip()
                i += 1
            blocks.append(paragraph(line, size))
    return blocks


def block_height(block, width):
    return block.wrap(width, HEIGHT)[1] + block.getSpaceAfter()


def draw_blocks(pdf, blocks, x, top, width):
    for block in blocks:
        _, height = block.wrap(width, HEIGHT)
        block.drawOn(pdf, x, top - height)
        top -= height + block.getSpaceAfter()
    return top


def new_pdf(path, page_size=letter):
    # Invariant metadata makes reruns reproducible for the same authored inputs.
    pdf = canvas.Canvas(str(path), pagesize=page_size, invariant=1, pageCompression=1)
    pdf.setTitle(path.stem)
    pdf.setAuthor("LOB, Over")
    return pdf


def footer(pdf, stage_id, page, page_size=letter):
    width, _ = page_size
    pdf.setLineWidth(0.6)
    pdf.line(MARGIN, 34, width - MARGIN, 34)
    pdf.setFont("KitSans", 8)
    pdf.drawString(MARGIN, 21, stage_id)
    pdf.drawRightString(width - MARGIN, 21, str(page))


def section_page(pdf, stage_id, page, title, markdown):
    heading = paragraph(title, 18, bold=True)
    heading_height = heading.wrap(TEXT_WIDTH, HEIGHT)[1]
    top = HEIGHT - MARGIN
    heading.drawOn(pdf, MARGIN, top - heading_height)
    content_top = top - heading_height - 19
    pdf.setLineWidth(1.2)
    pdf.line(MARGIN, content_top + 9, WIDTH - MARGIN, content_top + 9)
    available = content_top - 48
    for size in [10.5, 10, 9.5, 9, 8.5]:
        blocks = flowables(markdown, TEXT_WIDTH, size)
        if sum(block_height(b, TEXT_WIDTH) for b in blocks) <= available:
            draw_blocks(pdf, blocks, MARGIN, content_top, TEXT_WIDTH)
            footer(pdf, stage_id, page)
            pdf.showPage()
            return
    raise ValueError(f"Manual section does not fit one readable page: {title}")


def make_manual(out, scenario, catalog, source):
    preface, sections, missing = manual_sections(source, scenario, catalog)
    pdf = new_pdf(out / "manual.pdf")
    draw_blocks(pdf, [paragraph(scenario["title"], 30, bold=True)], MARGIN, 661, TEXT_WIDTH)
    draw_blocks(pdf, flowables(preface, TEXT_WIDTH, 12), MARGIN, 570, TEXT_WIDTH)
    pdf.setLineWidth(2)
    pdf.line(MARGIN, 340, WIDTH - MARGIN, 340)
    pdf.setFont("KitSans-Bold", 20)
    pdf.drawString(MARGIN, 301, SEATS)
    pdf.setFont("KitSans-Bold", 15)
    pdf.drawString(MARGIN, 265, "Rules seat only")
    footer(pdf, scenario["id"], 1)
    pdf.showPage()
    plain_parts = [scenario["title"], plain(preface), SEATS, "Rules seat only"]
    page = 2
    for section_id, title, markdown in sections:
        plain_parts.extend([title, plain(markdown)])
        if section_id == "procedures":
            # Keep each procedure together; six procedures occupy two legible pages.
            split = markdown.index("**P-4 ")
            pieces = [markdown[:split], markdown[split:]]
        else:
            pieces = [markdown]
        for index, piece in enumerate(pieces):
            heading = title if index == 0 else title + " (continued)"
            section_page(pdf, scenario["id"], page, heading, piece)
            page += 1
    pdf.save()
    (out / "manual.txt").write_text("\n\n".join(plain_parts) + "\n", encoding="utf-8")
    return missing


def html_blocks(markdown):
    """Render the manual's small authored Markdown vocabulary without dependencies."""
    lines = markdown.splitlines()
    blocks = []
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        i += 1
        if not line or line == "---":
            continue
        if line.startswith("|"):
            rows = [line]
            while i < len(lines) and lines[i].strip().startswith("|"):
                rows.append(lines[i].strip())
                i += 1
            cells = [[cell.strip() for cell in row.strip("|").split("|")] for row in rows]
            cells = [row for row in cells if not all(re.fullmatch(r":?-+:?", c) for c in row)]
            blocks.append('<div class="table-scroll"><table>')
            for index, row in enumerate(cells):
                tag = "th" if index == 0 else "td"
                blocks.append("<tr>" + "".join(f"<{tag}>{inline(cell)}</{tag}>" for cell in row) + "</tr>")
            blocks.append("</table></div>")
        elif match := re.match(r"^#{1,3} (.+)", line):
            blocks.append(f"<h3>{inline(match[1])}</h3>")
        elif line.startswith("- ") or re.match(r"^\d+\. ", line):
            ordered = not line.startswith("- ")
            pattern = r"^\d+\. " if ordered else r"^- "
            items = [re.sub(pattern, "", line)]
            while i < len(lines) and re.match(pattern, lines[i].strip()):
                items.append(re.sub(pattern, "", lines[i].strip()))
                i += 1
            tag = "ol" if ordered else "ul"
            start = f' start="{line.split(".")[0]}"' if ordered else ""
            blocks.append(f"<{tag}{start}>" + "".join(f"<li>{inline(item)}</li>" for item in items) + f"</{tag}>")
        else:
            while i < len(lines) and lines[i].strip() and not re.match(r"^(?:\||#|- |\d+\.)", lines[i].strip()):
                line += " " + lines[i].strip()
                i += 1
            blocks.append(f"<p>{inline(line)}</p>")
    return "\n".join(blocks)


def make_manual_html(out, scenario, catalog, source):
    preface, sections, missing = manual_sections(source, scenario, catalog)
    content = [f"<h1>{inline(scenario['title'])} — Field manual</h1>", html_blocks(preface)]
    links = "".join(f'<a href="#{section_id}">{inline(title)}</a>' for section_id, title, _ in sections)
    content.append(f'<nav aria-label="Manual sections">{links}</nav>')
    for section_id, title, markdown in sections:
        content.append(f'<section id="{section_id}"><h2>{inline(title)}</h2>{html_blocks(markdown)}</section>')
    style = """body{font:16px/1.55 system-ui,sans-serif;color:#283942;background:#fffdf8;margin:24px auto;padding:0 20px;max-width:1000px}
nav{display:flex;flex-wrap:wrap;gap:12px}a{color:inherit}section{padding:20px 0;border-top:1px solid #bac6ca;scroll-margin-top:15px}
table{border-collapse:collapse;width:100%}th,td{text-align:left;vertical-align:top;border:1px solid #bac6ca;padding:8px}.table-scroll{overflow:auto}
li{margin:8px 0}section:target{border-top:4px solid #324f60}@media print{nav{display:none}section{break-before:page}}"""
    html = '<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
    html += f"<title>{inline(scenario['title'])} — Field manual</title><style>{style}</style></head><body><main>"
    html += "\n".join(content) + "</main></body></html>\n"
    (out / "manual.html").write_text(html, encoding="utf-8")
    return missing


def cut_rectangle(pdf, x, y, width, height):
    pdf.saveState()
    pdf.setDash(3, 3)
    pdf.setLineWidth(0.5)
    pdf.rect(x, y, width, height)
    pdf.restoreState()


def draw_text(pdf, text, x, top, width, size=11, bold=False):
    block = paragraph(text, size, bold=bold)
    _, height = block.wrap(width, HEIGHT)
    block.drawOn(pdf, x, top - height)
    return top - height


def make_cards(out, scenario):
    pdf = new_pdf(out / "cards.pdf")
    cards = [(kind, card) for kind in ("permits", "inspections") for card in scenario[kind]["cards"]]
    card_width, card_height = 252, 324
    for i, (kind, card) in enumerate(cards):
        slot = i % CARDS_PER_PAGE
        x = MARGIN + (slot % 2) * 276
        y = 408 if slot < 2 else 66
        cut_rectangle(pdf, x, y, card_width, card_height)
        left, top, inside = x + 16, y + card_height - 18, card_width - 32
        if kind == "permits":
            label = "ISSUED BY PM" if scenario["permits"]["heldBy"] == "pm" else "PERMIT"
        else:
            label = "INSPECTION"
        draw_text(pdf, label, left, top, inside, 12, True)
        draw_text(pdf, f"Zone {card['zone']}", left, top - 35, inside, 24, True)
        if kind == "permits":
            draw_text(pdf, card["number"], left, top - 82, inside, 18)
            draw_text(pdf, card["code"], left, top - 121, inside, 34, True)
            draw_text(pdf, RETURN_INSTRUCTION, left, top - 190, inside, 11)
        else:
            draw_text(pdf, card["number"], left, top - 77, inside, 26, True)
            checklist_top = top - 124
            for item in scenario["inspections"]["checklist"]:
                pdf.rect(left, checklist_top - 9, 9, 9)
                bottom = draw_text(pdf, item, left + 17, checklist_top + 1, inside - 17, 10)
                checklist_top = bottom - 14
            pdf.line(left, y + 40, x + card_width - 16, y + 40)
            draw_text(pdf, "Rules signature", left, y + 34, inside, 8)
        if slot == CARDS_PER_PAGE - 1 or i == len(cards) - 1:
            footer(pdf, scenario["id"], i // CARDS_PER_PAGE + 1)
            pdf.showPage()
    pdf.save()


def station(feet):
    feet = int(feet)
    return f"{feet // 100}+{feet % 100:02d}"


def make_ledger(out, scenario):
    size = landscape(letter)
    pdf = new_pdf(out / "ledger.pdf", size)
    left, right, top = 139, 750, 472
    length = scenario["job"]["lengthFt"]

    def position(feet):
        return left + (right - left) * feet / length

    for copy in (1, 2):  # WO-04: two copies of the ledger.
        draw_text(pdf, scenario["title"] + " — Zone ledger", MARGIN, 570, 708, 19, True)
        for zone in scenario["job"]["zones"]:
            center = position((zone["fromFt"] + zone["toFt"]) / 2)
            pdf.setFont("KitSans-Bold", 10)
            pdf.drawCentredString(center, 516, f"Zone {zone['id']}")
        pdf.setFont("KitSans", 8)
        for feet in range(0, length + 1, STATION_LABEL_FT):
            x = position(feet)
            pdf.drawCentredString(x, 492, station(feet))
            pdf.setLineWidth(0.35)
            pdf.line(x, top, x, top - 186)
        for row, crew in enumerate(scenario["crews"]):
            draw_text(pdf, crew["name"], MARGIN, top - row * 62 - 22, left - MARGIN - 8, 10, True)
        for row in range(4):
            pdf.line(left, top - row * 62, right, top - row * 62)
        box_top, box_bottom = 246, 102
        labels = ["Permit day", "Pipe complete day", "Inspection day", "Backfill day"]
        for row, label in enumerate(labels):
            draw_text(pdf, label, MARGIN, box_top - row * 36 - 11, left - MARGIN - 8, 9)
        for row in range(5):
            pdf.setLineWidth(0.5)
            pdf.line(left, box_top - row * 36, right, box_top - row * 36)
        boundaries = sorted({ft for z in scenario["job"]["zones"] for ft in (z["fromFt"], z["toFt"])})
        for feet in boundaries:
            pdf.setLineWidth(1.6)
            x = position(feet)
            pdf.line(x, top, x, top - 186)
            pdf.line(x, box_top, x, box_bottom)
        footer(pdf, scenario["id"], copy, size)
        pdf.showPage()
    pdf.save()


def make_tokens(out, scenario):
    token_ft = scenario["kit"]["openTrenchTokenFt"]
    count, remainder = divmod(scenario["job"]["lengthFt"], token_ft)
    if remainder:
        raise ValueError("Job length must be a whole number of open-trench tokens")
    pdf = new_pdf(out / "tokens.pdf")
    for i in range(count):
        slot = i % TOKENS_PER_PAGE
        x = 138 + (slot % 3) * 168
        y = 648 - (slot // 3) * 168
        pdf.saveState()
        pdf.setDash(3, 3)
        pdf.setLineWidth(0.7)
        pdf.circle(x, y, TOKEN_DIAMETER / 2)
        pdf.restoreState()
        pdf.setFont("KitSans-Bold", 16)
        pdf.drawCentredString(x, y - 5, f"{token_ft} ft open")
        if slot == TOKENS_PER_PAGE - 1 or i == count - 1:
            footer(pdf, scenario["id"], i // TOKENS_PER_PAGE + 1)
            pdf.showPage()
    pdf.save()


def make_jobsign(out, scenario):
    pdf = new_pdf(out / "jobsign.pdf")
    sign = scenario["job"]["sign"]
    cut_rectangle(pdf, 60, 276, 492, 300)
    draw_text(pdf, scenario["title"], 84, 550, 444, 24, True)
    draw_text(pdf, sign["cityCode"], 84, 495, 444, 48, True)
    draw_text(pdf, sign["pipe"], 84, 415, 444, 21)
    draw_text(pdf, f"{sign['contractDays']} contract days", 84, 345, 444, 25, True)
    footer(pdf, scenario["id"], 1)
    pdf.showPage()
    pdf.save()


def make_index(out, scenario):
    lines = [f"permit zone {c['zone']} {c['number']} {c['code']}" for c in scenario["permits"]["cards"]]
    lines.extend(f"inspection zone {c['zone']} {c['number']}" for c in scenario["inspections"]["cards"])
    (out / "kit_index.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")


def make_kit(scenario_path, out=None, html=False):
    scenario = read_json(scenario_path)
    catalog = read_json(ROOT / "Data" / "catalog.json")
    source = (PAPER / "manual_v0.md").read_text(encoding="utf-8-sig")
    out = Path(out) if out else PAPER / "out" / scenario["id"]
    out.mkdir(parents=True, exist_ok=True)
    if html:
        missing = make_manual_html(out, scenario, catalog, source)
        print(f"{scenario['id']}: wrote manual.html to {out}")
        print("Unresolved placeholders: " + (", ".join(sorted(missing)) if missing else "none"))
        return out
    register_fonts()
    missing = make_manual(out, scenario, catalog, source)
    make_cards(out, scenario)
    make_ledger(out, scenario)
    make_tokens(out, scenario)
    make_jobsign(out, scenario)
    make_index(out, scenario)
    print(f"{scenario['id']}: wrote 5 PDFs, manual.txt and kit_index.txt to {out}")
    print("Unresolved placeholders: " + (", ".join(sorted(missing)) if missing else "none"))
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scenario", type=Path)
    parser.add_argument("--out", type=Path, help="Output directory (default: Paper/out/<scenario id>)")
    parser.add_argument("--html", action="store_true", help="Write only manual.html, with section anchors")
    args = parser.parse_args()
    make_kit(args.scenario, args.out, html=args.html)


if __name__ == "__main__":
    main()
