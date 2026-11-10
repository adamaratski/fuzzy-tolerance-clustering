"""Re-runs every experiment reported in the book's reproduction tables with the
command-line tool and writes results/reproduction/<run>.json plus summary.md.

Usage (from the repository root, after `dotnet build src/dotnet/Math.Fuzzy.slnx -c Release`):
    python results/reproduce.py [--only <substring>] [--rerun] [--check]
Existing JSON outputs are reused unless --rerun is given. With --only the selected rows are
printed and summary.md is left as it is. With --check the selected runs are recomputed into a
temporary folder and compared with the rows of the committed summary.md (times excluded);
the exit code is 1 when a row differs. Python 3.8 or later.

Each JSON keeps the ten top-ranked allotments of its run (every level for H-AFC-TC) in
compact form; `resultCount` records how many the tool returned.
"""
import json, subprocess, sys, tempfile, time
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"): sys.stdout.reconfigure(errors="replace")   # consoles without Unicode
root = Path(__file__).resolve().parents[1]
cli = root / "src/dotnet/apps/Math.Fuzzy.Cli/bin/Release/net10.0/mfuzzy.exe"
archive_dir = root / "results/reproduction"
check = "--check" in sys.argv
out_dir = Path(tempfile.mkdtemp(prefix="mfuzzy-check-")) if check else archive_dir
out_dir.mkdir(exist_ok=True)
only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else None
KEPT_RESULTS = 10

# (run name, dataset file, algorithm, extra CLI options) — the settings of
# tbl-reproduction (unitarisation, full precision) and tbl-reproduction-iris /
# tbl-reproduction-leap (the monograph's setting: squared Euclidean, max, ε).
MINMAX = ["--normalize", "minmax"]
MONO = ["--normalize", "max", "--distance", "squared-euclidean"]
NONMEMBERSHIP = ["--nonmembership", str(root / "datasets" / "hung_ifs_nonmembership.csv")]
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
    # tbl-dafc-u: the maximal number of objects in a cluster is given (Tamura's portraits are a relation)
    ("tamura_dafc-u3",                "tamura_16.csv",    "dafc-u",   ["--matrix", "--max-size", "3"]),
    ("tamura_dafc-u4",                "tamura_16.csv",    "dafc-u",   ["--matrix", "--max-size", "4"]),
    ("tamura_dafc-u5",                "tamura_16.csv",    "dafc-u",   ["--matrix", "--max-size", "5"]),
    ("tamura_dafc-u6",                "tamura_16.csv",    "dafc-u",   ["--matrix", "--max-size", "6"]),
    ("tamura_dpafc",                  "tamura_16.csv",    "dpafc",    ["--matrix"]),
    ("sneath_dafc-u4_sqeuclid_max",   "sneath_sokal.csv", "dafc-u",   ["--max-size", "4"] + MONO),
    ("sneath_dafc-u5_sqeuclid_max",   "sneath_sokal.csv", "dafc-u",   ["--max-size", "5"] + MONO),
    ("iris_dafc-u51_sqeuclid_max",    "iris_labeled.csv", "dafc-u",   ["--max-size", "51"] + MONO),
    ("iris_dafc-u52_sqeuclid_max",    "iris_labeled.csv", "dafc-u",   ["--max-size", "52"] + MONO),
    # tbl-taga-sneath-ties: transitive approximations, equal similarities recognised as equal
    ("sneath_dafc-taga-max_sqeuclid_max", "sneath_sokal.csv", "dafc-taga", ["--taga", "max"] + MONO),
    ("sneath_dafc-taga-min_sqeuclid_max", "sneath_sokal.csv", "dafc-taga", ["--taga", "min"] + MONO),
    ("sneath_dafc-taga-mean_sqeuclid_max", "sneath_sokal.csv", "dafc-taga", ["--taga", "mean"] + MONO),
    ("sneath_dafc-taga-median_sqeuclid_max", "sneath_sokal.csv", "dafc-taga", ["--taga", "median"] + MONO),
    ("sneath_dafc-taga-umedian_sqeuclid_max", "sneath_sokal.csv", "dafc-taga", ["--taga", "umedian"] + MONO),
    ("sneath_dafc-taga-dmedian_sqeuclid_max", "sneath_sokal.csv", "dafc-taga", ["--taga", "dmedian"] + MONO),
    ("sneath_dafc-taga-auto_sqeuclid_max", "sneath_sokal.csv", "dafc-taga", ["--taga", "auto"] + MONO),
    ("iris_dafc-taga-auto_euclidean_max", "iris_labeled.csv", "dafc-taga", ["--taga", "auto", "--normalize", "max", "--distance", "euclidean"]),
    ("iris_hafc-taga-mean_euclidean", "iris_labeled.csv", "hafc-taga", ["--taga", "mean", "--distance", "euclidean"] + MINMAX),
    # intuitionistic fuzzy data: a relation given by two matrices, and attribute data
    ("ifs_hung_dpaifc_beta-outer",    "hung_ifs_membership.csv", "dpaifc", NONMEMBERSHIP + ["--scan-order", "beta-outer"]),
    ("ifs_hung_dpaifc_beta-inner",    "hung_ifs_membership.csv", "dpaifc", NONMEMBERSHIP),
    ("ifs_hung_daifc3",               "hung_ifs_membership.csv", "daifc",  NONMEMBERSHIP + ["--clusters", "3"]),
    ("ifs_cars5_dpaifc-tc",           "cars5_ifs.csv",    "dpaifc-tc", []),
    ("ifs_cars10_dpaifc-tc",          "cars10_ifs.csv",   "dpaifc-tc", []),
    # the order-free definition: all pairs of thresholds, least number of clusters, quality Q in the column F
    ("ifs_hung_dpaifc_order-free",    "hung_ifs_membership.csv", "dpaifc", NONMEMBERSHIP + ["--scan-order", "order-free"]),
    ("ifs_cars5_dpaifc_order-free",   "cars5_ifs.csv",    "dpaifc",    ["--scan-order", "order-free"]),
    ("ifs_cars10_dpaifc_order-free",  "cars10_ifs.csv",   "dpaifc",    ["--scan-order", "order-free"]),
    ("ifs_cars10_dpaifc-tc_order-free", "cars10_ifs.csv", "dpaifc-tc", ["--scan-order", "order-free"]),
    # transitive opening (TO) and partial supervision (PS); objects of --label are numbered from 1
    ("sneath_dafc-to_sqeuclid_max",   "sneath_sokal.csv", "dafc-to", MONO),
    ("yangwu_dafc-ps2_sqeuclid_max_1e-5", "yang_wu.csv",  "dafc-ps", ["--clusters", "2", "--accuracy", "0.00001", "--label", "1:1.0", "--label", "9:1.0"] + MONO),
    ("ifs_cars5_daifc-ps2",           "cars5_ifs.csv",    "daifc-ps", ["--clusters", "2", "--scan-order", "beta-outer", "--label", "1:1.0:0.0", "--label", "5:1.0:0.0"]),
    # other kinds of input: the flag that names the kind (--interval, --three-way, --asymmetric, …) is part of the options
    ("oils_dafc2_interval",           "ichino_oils_interval.csv", "dafc", ["--clusters", "2", "--interval"]),
    ("sato_dafc3_threeway",           "sato_sato_threeway.csv",   "dafc", ["--clusters", "3", "--three-way"]),
    ("children_dpafc_asymmetric",     "children_dislike.csv",     "dpafc", ["--asymmetric"]),
]

lines = ["# Reproduction runs", "",
         "Each row is the top-ranked allotment of one command-line run; `overlap` is the number of objects in more than one cluster (the w of the book); `misclassified` is n · (1 − purity) and is "
         "reported only for labelled data. Centres are 1-based object indices. `time` is the wall-clock time of the process."
         " For the runs on intuitionistic fuzzy data (`ifs_…`) the column α* holds the pair of thresholds (α, β)."
         " In the `order-free` runs these are the own thresholds of the allotment and the column F holds the quality Q.", "",
         "| run | n | c | α* | F | centres (1-based) | sizes | overlap | purity | ARI | NMI | misclassified | time s |",
         "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
header_lines = len(lines)
for name, dataset, algorithm, options in runs:
    if only and only not in name: continue
    target = out_dir / f"{name}.json"
    command = [str(cli), "cluster", "--algorithm", algorithm, "--input", str(root / "datasets" / dataset),
               "--format", "json", "--output", str(target)] + options
    if not name.startswith("ifs_"): command += ["--criterion", "f1"]   # the intuitionistic criterion is fixed
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
    # the tool writes {"results", "search", "validation"}; version 1.0.0 wrote a bare list for unlabelled data
    if not isinstance(data, dict): data = {"results": data}
    if data.get("search", {}).get("budgetExhausted"):
        print(name, "WARNING: the search stopped at its node budget")
    results = data["results"]
    if "resultCount" not in data:
        # archive form: the ten best allotments (the whole hierarchy for H-AFC-TC), compact JSON
        data["resultCount"] = len(results)
        if not algorithm.startswith("hafc-"): data["results"] = results = results[:KEPT_RESULTS]
        target.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    if algorithm.startswith("hafc-"):
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
    validation = best.get("validation") or data.get("validation")   # per level for a hierarchy
    purity = f"{validation['purity']:.3f}" if validation else "—"
    ari = f"{validation['adjustedRandIndex']:.3f}" if validation else "—"
    nmi = f"{validation['nmi']:.3f}" if validation else "—"
    misclassified = f"{round(objects * (1 - validation['purity']))}" if validation else "—"
    elapsed_text = "—" if elapsed != elapsed else f"{elapsed:.1f}"
    # intuitionistic runs: the pair of thresholds (α, β)
    alpha_text = f"{best['alpha']:.5f}" + (f", β {best['beta']:.5f}" if best.get("beta") is not None else "")
    lines.append(f"| {name} | {objects} | {best['clusterCount']} | {alpha_text} | {best['score']:.5f} | "
                 f"{[c + 1 for c in best['centers']]} | {sizes} | {overlap} | {purity} | {ari} | {nmi} | {misclassified} | {elapsed_text} |")
    print(lines[-1])

def without_time(row):
    return "|".join(row.split("|")[:-2])


if check:
    committed = {row.split("|")[1].strip(): without_time(row)
                 for row in (archive_dir / "summary.md").read_text(encoding="utf-8").splitlines() if row.startswith("| ")}
    differing = [row.split("|")[1].strip() for row in lines[header_lines:]
                 if committed.get(row.split("|")[1].strip()) != without_time(row)]
    print(f"checked {len(lines) - header_lines} run(s): " + ("all rows match summary.md" if not differing
          else "DIFFERENT from summary.md: " + ", ".join(differing)))
    sys.exit(1 if differing else 0)
if only is None:
    (out_dir / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
