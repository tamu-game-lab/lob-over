"""WO-04 acceptance checks. Generate both kits before running this plain script."""

import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

from pypdf import PdfReader


PAPER = Path(__file__).resolve().parent
ROOT = PAPER.parent
PDF_NAMES = ("manual", "cards", "ledger", "tokens", "jobsign")
VERDICTS = re.compile(r"when right|when wrong|right choice|well done|correct answer|score", re.I)


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def normalized(text):
    return " ".join(text.split())


def assert_contains(text, value, location):
    assert normalized(str(value)) in normalized(text), f"{location}: missing {value!r}"


def forbidden_strings(scenario):
    forbidden = {"cardWhenProtected", "_validate"}
    for deviation in scenario.get("deviations", []):
        forbidden.update([deviation["id"], deviation["when"]["kind"]])
    for event in scenario.get("events", []):
        forbidden.add(event["id"])
        for ft in (event.get("stationFt"), event.get("trigger", {}).get("ft")):
            if ft is not None:
                forbidden.add(f"{int(ft) // 100}+{int(ft) % 100:02d}")
    return forbidden


def check_manual(source, scenario, pages, manual):
    headings = list(re.finditer(r"^## (.+?) \(([^)]+)\)$", source, re.M))
    sections = {}
    for i, heading in enumerate(headings):
        end = headings[i + 1].start() if i + 1 < len(headings) else len(source)
        for key in heading[2].split(","):
            sections[key.strip()] = (heading[1], source[heading.end():end])
    last_page = 0
    for key in scenario["kit"]["manualPages"]:
        title, body = sections[key]
        matching = [i for i, page in enumerate(pages)
                    if normalized(page).startswith(title + " ")
                    and not normalized(page).startswith(title + " (continued)")]
        assert len(matching) == 1, f"{key}: missing or duplicated section"
        page_index = matching[0]
        assert page_index > last_page, f"{key}: section out of order"
        last_page = page_index
        section_text = pages[page_index]
        if key == "procedures":
            section_text += pages[page_index + 1]
        # Check authored prose/cells independently of the generator's Markdown parser.
        for line in body.splitlines():
            line = line.strip()
            if not line or re.fullmatch(r"[| :\-]+", line) or "{{" in line:
                continue
            line = re.sub(r"^### |^- ", "", line).replace("*", "")
            cells = line.strip("|").split("|") if line.startswith("|") else [line]
            for cell in cells:
                if cell.strip():
                    assert_contains(section_text, cell.strip(), f"manual.pdf/{key}")
                    assert_contains(manual, cell.strip(), f"manual.txt/{key}")
    assert "E2 — Unforeseen" not in manual, "Unselected E2 section was printed"
    assert "{{" not in manual, "Unresolved placeholder syntax"
    assert_contains(pages[0], scenario["title"], "cover")
    for value in ("Site / Plan / Rules", "Rules seat only", "LOB, Over — Field Manual v0"):
        assert_contains(pages[0], value, "cover")
    assert_contains(pages[1], "If days lost ≤ buffer", "front-page math glyph")
    for i, page in enumerate(pages, 1):
        assert page.rstrip().endswith(f"{scenario['id']}\n{i}"), f"manual page {i}: footer"


def check_kit(scenario_path):
    scenario = load(scenario_path)
    out = PAPER / "out" / scenario["id"]
    index = (out / "kit_index.txt").read_text(encoding="utf-8")
    manual = (out / "manual.txt").read_text(encoding="utf-8")
    expected_index = [
        f"permit zone {c['zone']} {c['number']} {c['code']}"
        for c in scenario["permits"]["cards"]
    ] + [
        f"inspection zone {c['zone']} {c['number']}" for c in scenario["inspections"]["cards"]
    ]
    assert index.splitlines() == expected_index, "Card index differs from scenario"

    # Ranges: cover + 13 selected sections, up to two continuation pages; four
    # cards per sheet; two ledger copies; twelve two-inch tokens per sheet; one sign.
    card_count = len(scenario["permits"]["cards"]) + len(scenario["inspections"]["cards"])
    token_count = scenario["job"]["lengthFt"] // scenario["kit"]["openTrenchTokenFt"]
    card_pages, token_pages = (card_count + 3) // 4, (token_count + 11) // 12
    minimum_manual = len(scenario["kit"]["manualPages"]) + 1
    ranges = {
        "manual": (minimum_manual, minimum_manual + 2),
        "cards": (card_pages, card_pages), "ledger": (2, 2),
        "tokens": (token_pages, token_pages), "jobsign": (1, 1),
    }
    texts, counts = {}, {}
    for name in PDF_NAMES:
        reader = PdfReader(out / f"{name}.pdf")
        counts[name] = len(reader.pages)
        low, high = ranges[name]
        assert low <= counts[name] <= high, f"{name}: {counts[name]} pages, expected {low}..{high}"
        texts[name] = [page.extract_text() for page in reader.pages]
        expected_size = (792, 612) if name == "ledger" else (612, 792)
        for page in reader.pages:
            assert (float(page.mediabox.width), float(page.mediabox.height)) == expected_size

    surfaces = {"manual.txt": manual, "kit_index.txt": index}
    surfaces.update({name + ".pdf": "\n".join(pages) for name, pages in texts.items()})
    for name, text in surfaces.items():
        assert not VERDICTS.search(text), f"{name}: verdict language"
        for forbidden in forbidden_strings(scenario):
            assert forbidden.lower() not in text.lower(), f"{name}: leaked {forbidden}"

    source = (PAPER / "manual_v0.md").read_text(encoding="utf-8-sig")
    check_manual(source, scenario, texts["manual"], manual)
    cards = "\n".join(texts["cards"])
    assert cards.count("ISSUED BY PM") == (len(scenario["permits"]["cards"]) if scenario["permits"]["heldBy"] == "pm" else 0)
    for kind in ("permits", "inspections"):
        for card in scenario[kind]["cards"]:
            for field in ("number", "code") if kind == "permits" else ("number",):
                for surface in (cards, manual, "\n".join(texts["manual"])):
                    assert_contains(surface, card[field], f"{kind}/{field}")
    for checklist_item in scenario["inspections"]["checklist"]:
        assert normalized(cards).count(checklist_item) == len(scenario["inspections"]["cards"])
    assert normalized(cards).count("Read the code to Plan. Return this card when the zone is backfilled and signed.") == len(scenario["permits"]["cards"])
    for page in texts["ledger"]:
        for feet in range(0, scenario["job"]["lengthFt"] + 1, 500):
            assert_contains(page, f"{feet // 100}+{feet % 100:02d}", "ledger stations")
        for value in [c["name"] for c in scenario["crews"]] + ["Permit day", "Pipe complete day", "Inspection day", "Backfill day"]:
            assert_contains(page, value, "ledger rows")
    label = f"{scenario['kit']['openTrenchTokenFt']} ft open"
    assert "\n".join(texts["tokens"]).count(label) == token_count
    for value in [scenario["title"], *[scenario["job"]["sign"][k] for k in ("cityCode", "pipe", "contractDays")]]:
        assert_contains(texts["jobsign"][0], value, "job sign")
    expected_holder = (
        "all issued and posted by the PM before Day 1" if scenario["permits"]["heldBy"] == "pm"
        else f"held by Rules; the city allows {scenario['permits']['openZoneLimit']} zone(s) open at a time"
    )
    for value in (expected_holder, "6000 ft in 4 zones of 1500 ft", "$5000", "$10000", "$4000", "$60"):
        assert_contains(manual, value, "resolved placeholders")
    print(f"PASS {scenario['id']}: index {len(expected_index)} cards; pages " +
          ", ".join(f"{key}={counts[key]}" for key in PDF_NAMES) +
          f"; {token_count} tokens; authored text and no-leak checks")
    return scenario, out


def check_private_payload_independence(scenario, baseline):
    # Mutating all prohibited sources must leave every printed byte unchanged.
    changed = json.loads(json.dumps(scenario))
    changed["deviations"] = [{"id": "PRIVATE_SENTINEL", "when": {"kind": "PRIVATE_SENTINEL"}}]
    for event in changed["events"]:
        event["trigger"] = {"kind": "PRIVATE_SENTINEL"}
        event["cardWhenProtected"] = "PRIVATE_SENTINEL"
        for option in event["options"]:
            option["effect"] = {"private": "PRIVATE_SENTINEL"}

    def poison_notes(value):
        if isinstance(value, dict):
            for key in value:
                if "_validate" in key:
                    value[key] = "PRIVATE_SENTINEL"
                else:
                    poison_notes(value[key])
        elif isinstance(value, list):
            for item in value:
                poison_notes(item)

    poison_notes(changed)
    with tempfile.TemporaryDirectory(prefix="kit-test-", dir=PAPER / "out") as temporary:
        folder = Path(temporary)
        source = folder / "scenario.json"
        source.write_text(json.dumps(changed), encoding="utf-8")
        target = folder / "custom-output"
        subprocess.run([sys.executable, str(PAPER / "make_kit.py"), str(source), "--out", str(target)],
                       check=True, capture_output=True, text=True)
        for name in [f"{stem}.pdf" for stem in PDF_NAMES] + ["manual.txt", "kit_index.txt"]:
            assert (baseline / name).read_bytes() == (target / name).read_bytes(), f"Private payload changed {name}"
    print("PASS --out and private-payload isolation: all 7 files byte-identical")


def main():
    for stage in ("stage1_texas", "stage2_texas"):
        scenario, out = check_kit(ROOT / "Data" / "Scenarios" / f"{stage}.json")
    check_private_payload_independence(scenario, out)
    print("PASS all WO-04 kit checks")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError, subprocess.CalledProcessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
