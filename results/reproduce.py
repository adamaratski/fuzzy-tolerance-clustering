"""Re-runs every experiment reported in the book's reproduction tables with the
command-line tool and writes results/reproduction/<run>.json plus summary.md.

Usage (from the repository root, after `dotnet build src/dotnet/Math.Fuzzy.slnx -c Release`):
    python results/reproduce.py [--only <substring>] [--rerun]
Existing JSON outputs are reused unless --rerun is given. With --only the selected rows are
printed and summary.md is left as it is. Python 3.8 or later.

Each JSON keeps the ten top-ranked allotments of its run (every level for H-AFC-TC) in
compact form; `resultCount` records how many the tool returned.
"""
import json, subprocess, sys, time
from pathlib import Path

root = Path(__file__).resolve().parents[1]
cli = root / "src/dotnet/apps/Math.Fuzzy.Cli/bin/Release/net10.0/mfuzzy.exe"
out_dir = root / "results/reproduction"
out_dir.mkdir(exist_ok=True)
only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else None
KEPT_RESULTS = 10

# (run name, dataset file, algorithm, extra CLI options) — the settings of
# tbl-reproduction (unitarisation, full precision) and tbl-reproduction-iris /
# tbl-reproduction-leap (the monograph's setting: squared Euclidean, max, ε).
MINMAX = ["--normalize", "minmax"]
MONO = ["--normalize", "max", "--distance", "squared-euclidean"]
runs = [
    # tbl-reproduction
    ("iris_dafc3_euclidean",        "iris_labeled.csv",      "dafc",     ["--clusters", "3", "--distance", "euclidean"] + MINMAX),
    ("iris_dafc3_hamming",          "iris_labeled.csv",      "dafc",     ["--clusters", "3", "--distance", "hamming"] + MINMAX),
    ("iris_dafc3_chebyshev",        "iris_labeled.csv",      "dafc",     ["--clusters", "3", "--distance", "chebyshev"] + MINMAX),
    ("iris_dafc-tc_euclidean",      "iris_labeled.csv",      "dafc-tc",  ["--distance", "euclidean"] + MINMAX),
    ("iris_dpafc-tc_euclidean",     "iris_labeled.csv",      "dpafc-tc", ["--distance", "euclidean"] + MINMAX),
    ("iris_hafc-tc_euclidean",      "iris_labeled.csv",      "hafc-tc",  ["--distance", "euclidean"] + MINMAX),
    ("seeds_dafc3_euclidean",       "seeds_labeled.csv",     "dafc",     ["--clusters", "3", "--distance", "euclidean"] + MINMAX),
    ("wine_dafc3_euclidean",        "wine_labeled.csv",      "dafc",     ["--clusters", "3", "--distance", "euclidean"] + MINMAX),
    ("penguins_dafc3_euclidean",    "penguins_labeled.csv",  "dafc",     ["--clusters", "3", "--distance", "euclidean"] + MINMAX),
    ("penguins_dafc-tc_euclidean",  "penguins_labeled.csv",  "dafc-tc",  ["--distance", "euclidean"] + MINMAX),
    ("spiral_dafc-tc_euclidean",    "sipu_spiral_labeled.csv",   "dafc-tc", ["--distance", "euclidean"] + MINMAX),
    ("pathbased_dafc-tc_euclidean", "sipu_pathbased_labeled.csv","dafc-tc", ["--distance", "euclidean"] + MINMAX),
    ("zigzag_dafc-tc_euclidean",    "graves_zigzag_labeled.csv", "dafc-tc", ["--distance", "euclidean"] + MINMAX),
    ("hepta_dafc-tc_euclidean",     "fcps_hepta_labeled.csv",    "dafc-tc", ["--distance", "euclidean"] + MINMAX),
    ("zigzag-outliers_dpafc-tc_euclidean", "graves_zigzag_outliers_labeled.csv", "dpafc-tc", ["--distance", "euclidean"] + MINMAX),
    ("ecoli_dpafc-tc_euclidean",    "ecoli_labeled.csv",     "dpafc-tc", ["--distance", "euclidean"] + MINMAX),
    # tbl-reproduction-iris
    ("iris_dafc3_hamming_max_1e-5",   "iris_labeled.csv", "dafc", ["--clusters", "3", "--normalize", "max", "--distance", "hamming", "--accuracy", "0.00001"]),
    ("iris_dafc3_euclidean_max_1e-5", "iris_labeled.csv", "dafc", ["--clusters", "3", "--normalize", "max", "--distance", "euclidean", "--accuracy", "0.00001"]),
    ("iris_dafc3_sqeuclid_max_1e-5",  "iris_labeled.csv", "dafc", ["--clusters", "3", "--accuracy", "0.00001"] + MONO),
    ("iris_dafc3_sqeuclid_max_1e-4",  "iris_labeled.csv", "dafc", ["--clusters", "3", "--accuracy", "0.0001"] + MONO),
    ("iris_dafc3_sqeuclid_max_1e-3",  "iris_labeled.csv", "dafc", ["--clusters", "3", "--accuracy", "0.001"] + MONO),
    ("iris_dafc3_sqeuclid_max_1e-2",  "iris_labeled.csv", "dafc", ["--clusters", "3", "--accuracy", "0.01"] + MONO),
    ("iris_dpafc-tc_sqeuclid_max_1e-4", "iris_labeled.csv", "dpafc-tc", ["--accuracy", "0.0001"] + MONO),
    ("iris_dafc-tc_sqeuclid_max_1e-4",  "iris_labeled.csv", "dafc-tc",  ["--accuracy", "0.0001"] + MONO),
    # tbl-reproduction-leap and D-PAFC on Sneath–Sokal
    ("yangwu_dafc-tc_sqeuclid_max",   "yang_wu.csv",      "dafc-tc",  MONO),
    ("yangwu_dpafc-tc_sqeuclid_max",  "yang_wu.csv",      "dpafc-tc", MONO),
    ("sneath_dafc-tc_sqeuclid_max",   "sneath_sokal.csv", "dafc-tc",  MONO),
    ("sneath_dpafc_sqeuclid_max_1e-5","sneath_sokal.csv", "dpafc",    ["--accuracy", "0.00001"] + MONO),
]

lines = ["# Reproduction runs", "",
         "Each row is the top-ranked allotment of one command-line run; `overlap` is the number of objects in more than one cluster (the w of the book); `misclassified` is n · (1 − purity) and is "
         "reported only for labelled data. Centres are 1-based object indices. `time` is the wall-clock time of the process.", "",
         "| run | n | c | α* | F | centres (1-based) | sizes | overlap | purity | ARI | NMI | misclassified | time s |",
         "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
for name, dataset, algorithm, options in runs:
    if only and only not in name: continue
    target = out_dir / f"{name}.json"
    command = [str(cli), "cluster", "--algorithm", algorithm, "--input", str(root / "datasets" / dataset),
               "--criterion", "f1", "--format", "json", "--output", str(target)] + options
    if target.exists() and "--rerun" not in sys.argv:
        elapsed = float("nan")   # kept from an earlier run; pass --rerun to recompute everything
    else:
        started = time.perf_counter()
        completed = subprocess.run(command, capture_output=True, text=True)
        elapsed = time.perf_counter() - started
        if completed.returncode != 0 or not target.exists():
            lines.append(f"| {name} | — | — | — | — | — | — | — | — | — | — | FAILED | {elapsed:.1f} |")
            print(name, "FAILED", completed.stderr.strip()[:200]); continue
    data = json.loads(target.read_text(encoding="utf-8-sig"))
    # the tool writes {"results", "validation"} for labelled data and a bare list otherwise
    if not isinstance(data, dict): data = {"results": data}
    results = data["results"]
    if "resultCount" not in data:
        # archive form: the ten best allotments (the whole hierarchy for H-AFC-TC), compact JSON
        data["resultCount"] = len(results)
        if algorithm != "hafc-tc": data["results"] = results = results[:KEPT_RESULTS]
        target.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    if algorithm == "hafc-tc":
        # the hierarchy: report the level with three clusters as the book does
        results = [r for r in results if r["clusterCount"] == 3] or results
    best = results[0]
    sizes = [len(c) for c in best["clusters"]]
    objects = len({o for c in best["clusters"] for o in c})
    # w of the book: the number of objects that lie in more than one cluster
    covered = {}
    for cluster in best["clusters"]:
        for o in cluster: covered[o] = covered.get(o, 0) + 1
    overlap = sum(1 for count in covered.values() if count > 1)
    validation = data.get("validation")
    purity = f"{validation['purity']:.3f}" if validation else "—"
    ari = f"{validation['adjustedRandIndex']:.3f}" if validation else "—"
    nmi = f"{validation['nmi']:.3f}" if validation else "—"
    misclassified = f"{round(objects * (1 - validation['purity']))}" if validation else "—"
    elapsed_text = "—" if elapsed != elapsed else f"{elapsed:.1f}"
    lines.append(f"| {name} | {objects} | {best['clusterCount']} | {best['alpha']:.5f} | {best['score']:.5f} | "
                 f"{[c + 1 for c in best['centers']]} | {sizes} | {overlap} | {purity} | {ari} | {nmi} | {misclassified} | {elapsed_text} |")
    print(lines[-1])

if only is None:
    (out_dir / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
