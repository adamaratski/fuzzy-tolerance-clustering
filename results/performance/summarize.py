"""Builds the tables of the computational chapter from measurements.jsonl.

Usage: python summarize.py  (writes summary.md next to this file)
"""
import json
import sys
from collections import defaultdict
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(errors="replace")   # consoles without Unicode
here = Path(__file__).parent
records = [json.loads(line) for line in (here / "measurements.jsonl").read_text(encoding="utf-8").splitlines() if line]


def seconds_or_ms(milliseconds):
    if milliseconds >= 10_000:
        return f"{milliseconds / 1000:,.0f} s"
    if milliseconds >= 1000:
        return f"{milliseconds / 1000:.2f} s"
    if milliseconds >= 10:
        return f"{milliseconds:.0f} ms"
    return f"{milliseconds:.2f} ms"


lines = ["# Measurements of the computational chapter", ""]
lines.append("Median wall-clock time; `cores` is the number of logical cores the process could use.")
lines.append("")

# closure: variants × sizes
closure = [record for record in records if record["experiment"] == "closure"]
sizes = sorted({record["objectCount"] for record in closure})
variants = list(dict.fromkeys((record["variant"], record["cores"]) for record in closure))
lines.append("## Transitive closure")
lines.append("")
lines.append("| Variant | cores | " + " | ".join(f"n = {size}" for size in sizes) + " |")
lines.append("|---|---|" + "---|" * len(sizes))
by_key = {(record["variant"], record["cores"], record["objectCount"]): record for record in closure}
for variant, cores in variants:
    cells = [seconds_or_ms(by_key[(variant, cores, size)]["medianMilliseconds"]) if (variant, cores, size) in by_key else "—"
             for size in sizes]
    lines.append(f"| {variant} | {cores} | " + " | ".join(cells) + " |")
lines.append("")

for experiment, title in [("search", "D-AFC(3) search on Iris"), ("bound", "Exact-cover bound, D-AFC(3)"),
                          ("hafc", "H-AFC-TC on Iris"), ("stages", "Pipeline stages")]:
    lines.append(f"## {title}")
    lines.append("")
    lines.append("| Variant | data | n | cores | time | note |")
    lines.append("|---|---|---|---|---|---|")
    for record in (record for record in records if record["experiment"] == experiment):
        time = seconds_or_ms(record["medianMilliseconds"]) + (" (extrapolated)" if record.get("extrapolated") else "")
        lines.append(f"| {record['variant']} | {record['dataset']} | {record['objectCount']} | {record['cores']} | {time} | {record.get('note', '')} |")
    lines.append("")

(here / "summary.md").write_text("\n".join(lines), encoding="utf-8")
print("\n".join(lines))
