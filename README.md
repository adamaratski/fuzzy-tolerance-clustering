# fuzzy-tolerance-clustering

[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.23856861.svg)](https://doi.org/10.5281/zenodo.23856861)

Reference implementations of the heuristic possibilistic clustering algorithms of
D. A. Viattchenin (D-AFC family). This repository accompanies the monograph
*Fuzzy Tolerance Clustering: Theory, Algorithms, and
Implementations* (A. Damaratski, 2026). The reproduction tables, the timing tables and the
figures of the book are recomputed from the data sets here by the scripts described below.

The book covers the mathematics, the algorithms and the computational techniques.
This README covers how to install and use each implementation.

A ready-to-run build of the desktop workbench for Windows is attached to each
[release](https://github.com/adamaratski/fuzzy-tolerance-clustering/releases). The [user guide of the workbench](docs/user-guide/user-guide.pdf) (PDF, with
screenshots) explains it screen by screen; its source is [`docs/user-guide`](docs/user-guide).

| Component | Location | Status |
|-----------|----------|--------|
| .NET library (Core, Clustering, Inference) | [`src/dotnet/src`](src/dotnet/src) | complete |
| Command-line tool `mfuzzy` | [`src/dotnet/apps/Math.Fuzzy.Cli`](src/dotnet/apps/Math.Fuzzy.Cli) | complete |
| Desktop workbench (Windows) | [`src/dotnet/apps/Math.Fuzzy.Desktop`](src/dotnet/apps/Math.Fuzzy.Desktop) | complete |
| Benchmarks of the computational chapter | [`src/dotnet/bench`](src/dotnet/bench), results in [`results/performance`](results/performance) | complete |
| Figures of the book | [`src/dotnet/tools/Math.Fuzzy.Figures`](src/dotnet/tools/Math.Fuzzy.Figures) | complete |
| MATLAB / GNU Octave toolbox `+vfc` | [`src/matlab`](src/matlab) | relational, closure-based and hierarchical algorithms, for small data |
| Python package | [`src/python`](src/python) | planned |
| Data sets | [`datasets/`](datasets/) ([description](datasets/README.md)) | — |

## Algorithms

Every algorithm runs on attribute data (objects in rows) or, with `--matrix`, on a fuzzy
tolerance matrix given directly; the attribute-space reduction needs attribute data. The three
intuitionistic algorithms (last rows) take intuitionistic fuzzy data instead, see below.

| Name | What is given | What is found | Book section |
|------|---------------|---------------|--------------|
| `dafc` | number of clusters $c$ | threshold $\alpha$, typical points; the separation is relaxed ($w = 0, 1, \dots$) only if needed | D-AFC(c) |
| `dafc-u` | largest cluster size $u$ (`--max-size`) | $c$, $\alpha$, typical points | D-AFC(u) |
| `dpafc` | nothing; optionally `--intersections w` | least $c$ at the first admissible level (principal allotment) | D-PAFC |
| `dafc-tc` | nothing, or $c$ | level of the closure by the leap heuristic | D-AFC-TC |
| `dafc-tc-alpha` | least threshold $\hat{\alpha}$ (`--alpha`) | leap level not below $\hat{\alpha}$ | D-AFC-TC($\alpha$) |
| `dpafc-tc` | nothing | first splitting level of the closure | D-PAFC-TC |
| `dafc-taga`, `dafc-taga-alpha`, `dpafc-taga` | aggregation operator `--taga` (default: chosen automatically) | as the `-tc` algorithms, on a transitive approximation | D-AFC-TAGA family |
| `hafc-tc`, `hafc-taga` | nothing | the allotment of every level (hierarchy) | H-AFC-TC, H-AFC-TAGA |
| `dpafc-dr`, `dpafc-tc-dr`, `dpafc-taga-dr` | nothing | attributes are clustered, one per cluster is kept, D-PAFC runs on the reduced data | Dimensionality reduction |
| `dpaifc` | nothing | the first pair of thresholds $(\alpha, \beta)$ with fully separated intuitionistic fuzzy clusters (least number of clusters) | D-PAIFC |
| `daifc` | number of clusters $c$ (`--clusters`) | $(\alpha, \beta)$, typical points; the clusters share objects only if no fully separated allotment exists ($w = 0, 1, \dots$) | D-AIFC(c) |
| `dpaifc-tc` | nothing | D-PAIFC on the transitive closure of the intuitionistic fuzzy tolerance (max-min on the memberships, min-max on the non-memberships); ties by the prototype rule | D-PAIFC-TC |

On a transitive closure or approximation the allotments of a level are ranked class by class
(one typical point per equivalence class), so the prototype-based and hierarchical algorithms
take milliseconds. The relational algorithms search combinations of typical points; their
search is bounded by a node budget (`--max-nodes`), and the tool says so when the budget was
spent and the result may be incomplete.

The intuitionistic algorithms work on intuitionistic fuzzy data, in one of two forms:
an intuitionistic fuzzy tolerance as two square CSV matrices (memberships with `--input`,
non-memberships with `--nonmembership`), or intuitionistic attribute data, one CSV whose
numeric columns come in pairs (membership, non-membership) per attribute, for example
`f1_mu,f1_nu,f2_mu,f2_nu,…`; the tolerance is then built with a similarity measure
(`--ifs-measure`).

## .NET

Requirements: .NET 10 SDK. On Windows the desktop application additionally needs
the Windows Desktop runtime.

```bash
cd src/dotnet
dotnet build Math.Fuzzy.slnx -c Release
dotnet test --solution Math.Fuzzy.slnx -c Release
```

Tests run on Microsoft Testing Platform with xUnit v3, which `src/dotnet/global.json` enables.
The same build and tests run on every push ([workflow](.github/workflows/ci.yml)).

### Library

| Assembly | Contents |
|----------|----------|
| `Math.Fuzzy.Core` | tolerance matrices; reading of data and matrix files; distances (Hamming, Euclidean, squared Euclidean, Manhattan, Chebyshev, Minkowski, cosine); normalisations (`max`, `minmax`, `none`); accuracy threshold $\varepsilon$; max-min transitive closure; transitive approximations (TAGA) with six aggregation operators and the automatic choice; α-level sequences and the leap heuristic; criteria $F_1$, $F_2$ |
| `Math.Fuzzy.Clustering` | all algorithms above; validity measures of an allotment ($V_{LMF}$, $V_{QMF}$, $V_{MSC}$); choice of the cluster size $u$; bounds of the number of clusters; external indices (Purity, Rand, ARI, NMI, Fowlkes–Mallows) |
| `Math.Fuzzy.Inference` | Mamdani and Takagi–Sugeno inference, generation of inference systems from allotments |

```csharp
using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;

var data        = RawDataMatrix.FromCsv("datasets/iris.csv");
var diagnostics = new SearchDiagnostics();
var options     = new AlgorithmOptions(Distance: "squared-euclidean",
                                       Normalization: "max",
                                       Accuracy: 1e-5,
                                       ClusterCount: 3,
                                       Diagnostics: diagnostics);
var results = AlgorithmFactory.Run("dafc", data, options);

var best = results[0];
Console.WriteLine($"c = {best.ClusterCount}, alpha* = {best.Alpha:F5}, F1 = {best.Score:F4}");
for (int cluster = 0; cluster < best.ClusterCount; cluster++)
    Console.WriteLine($"cluster {cluster + 1}: typical point x{best.Centers[cluster] + 1}, " +
                      $"{best.MemberSets[cluster].Length} objects");
if (diagnostics.BudgetExhausted)
    Console.WriteLine("the search stopped at its node budget: the result may be incomplete");
```

`results` lists the allotments, best first: larger criterion, then (for the prototype-based
algorithms) typical points nearer to the centroids of their clusters, then a fixed total order.
Each result holds the threshold $\alpha$, the typical points, the carriers, the membership
matrix $U$ and the criterion value. A tolerance matrix is passed with
`AlgorithmFactory.Run("dpafc", FuzzyToleranceMatrix.FromCsv(path), options)`.

`AlgorithmOptions` mirrors the command-line options below.

### Command-line tool `mfuzzy`

```bash
dotnet run --project src/dotnet/apps/Math.Fuzzy.Cli -c Release -- cluster \
    --algorithm dafc --input datasets/iris_labeled.csv --clusters 3 \
    --distance squared-euclidean --normalize max --accuracy 0.00001
```

| Command | Purpose |
|---------|---------|
| `cluster` | run a clustering algorithm |
| `size` | choose the largest cluster size $u$: D-AFC(u) for a range of $u$ with the validity measures of every result and the optimal $u^*$ (`--from`, `--to`, `--measure lmf|qmf|msc`, and the `--matrix`, `--distance`, `--normalize`, `--accuracy` options of `cluster`) |
| `bounds` | the set of potential numbers of clusters from one result (`--objects n --clusters c --alpha a --shape triangular\|gaussian`) |
| `matrix build` | build the tolerance matrix $T$ of a data file and write it as CSV (`--distance`, `--normalize`, `--accuracy`, `--output`) |
| `matrix closure` | read an $n\times n$ tolerance matrix and write its max-min transitive closure $\widetilde{T}$ |
| `reduce` | attribute selection (`--method dpafc \| dpafc-tc \| dpafc-taga`): prints the selected attributes and, with `--output`, writes the reduced data |
| `infer` | evaluate an inference system for one input vector (`--system rules.json --input 5.1,3.5,1.4,0.2`); the JSON is written by `cluster --rules` or exported by the workbench |

Options of `cluster`:

| Option | Values | Default |
|--------|--------|---------|
| `--algorithm` | see the algorithm table | — |
| `--input` | data file: comma, semicolon or tab separated, with or without a header. A label column — a text column with repeated values, or a column headed `class`, `label`, `labels`, `type`, `species`, `variety`, `target`, `category` or `group` — is left out of the data and used for the external indices; a column of object names is ignored. A missing value is an error | — |
| `--matrix` | the input file is a fuzzy tolerance matrix (symmetric, values in [0, 1]; a header row and a column of names are allowed) | attribute data |
| `--clusters` | $c$ for `dafc` and `daifc` (required) and `dafc-tc` (optional) | — |
| `--alpha` | a single level for `dafc`; the least threshold for `dafc-tc-alpha`, `dafc-taga-alpha` | — |
| `--max-size` | largest number of objects in a cluster; the parameter $u$ of `dafc-u` | no bound |
| `--min-size` | least number of objects in a cluster | 1 |
| `--intersections` | $w\ge 0$ for `dafc`, `dafc-u`, `dpafc` | `dpafc`: 0; `dafc`, `dafc-u`: automatic |
| `--distance` | `euclidean`, `squared-euclidean`, `manhattan`, `chebyshev`, `minkowski`, `cosine`, `hamming` | `euclidean` |
| `--minkowski-p` | order $p$ | 3 |
| `--normalize` | `minmax`, `max`, `none` | `minmax` |
| `--accuracy` | $\varepsilon$, e.g. `0.00001` | full precision |
| `--taga` | `auto`, `max`, `min`, `mean`, `median`, `umedian`, `dmedian` | `auto` |
| `--criterion` | `f1`, `f2` | `f1` |
| `--nonmembership` | file with the matrix of non-memberships; with `--input` as the matrix of memberships it gives an intuitionistic fuzzy tolerance (`dpaifc`, `daifc`, `dpaifc-tc`) | — |
| `--ifs-measure` | `hamming`, `euclidean`: similarity measure that builds the tolerance from intuitionistic attribute data | `hamming` |
| `--scan-order` | `beta-inner`, `beta-outer`: order in which the pairs $(\alpha, \beta)$ are scanned by the intuitionistic algorithms; `beta-outer` reproduces the published D-PAIFC result on the relation of Hung, Lee and Fuh | `beta-inner` |
| `--max-nodes` | node budget of the combination search | 500 000 000 |
| `--top` | number of allotments reported | 200 |
| `--format`, `--output` | `table`, `json`, `csv`; output file | `table`, stdout |
| `--rules` | file for the fuzzy inference system generated from the best allotment (one Mamdani rule per cluster, JSON) | — |

An option that the chosen algorithm does not use is an error, not a silent no-op, and so is an
unknown algorithm, distance, normalisation, criterion, operator or format name. Exit codes:
0 — an allotment was found, 2 — invalid input or options, 3 — no allotment exists.
Numbers are read and written with a decimal point on every system locale. Object numbers in
the output are 0-based.

To reproduce the setting of the monograph, use `--distance squared-euclidean --normalize max --accuracy 0.00001`.

The intuitionistic algorithms do not use `--distance`, `--normalize`, `--criterion` and
`--accuracy`. Their table has an extra `Beta` column and the criterion $F_{IFS}$; the best line
shows $\beta$ and, for `daifc`, the intersection bound $w$; JSON has `beta` and
`intersectionBound`.

```bash
mfuzzy cluster --algorithm dafc-u --max-size 6 --matrix --input datasets/tamura_16.csv
mfuzzy size --input datasets/sneath_sokal.csv --distance squared-euclidean --normalize max
mfuzzy bounds --objects 150 --clusters 2 --alpha 0.80384 --shape gaussian
mfuzzy cluster --algorithm daifc --clusters 3 --input datasets/hung_ifs_membership.csv \
    --nonmembership datasets/hung_ifs_nonmembership.csv
mfuzzy cluster --algorithm dpaifc-tc --input datasets/cars10_ifs.csv --ifs-measure hamming
```

The first of the two new examples gives $(\alpha, \beta) = (0.4, 0.6)$ with $w = 1$; the second
gives four clusters at $(0.4833, 0.1667)$.

### Desktop workbench

```bash
dotnet run --project src/dotnet/apps/Math.Fuzzy.Desktop -c Release
```

At start-up the workbench opens the sample data sets from `datasets/`.

What it shows:
- the tolerance matrix and its closure as heat maps;
- every algorithm with its parameters, with a Stop button and a progress bar while a run is going;
- membership heat maps, cluster tables and validity indices;
- the dendrogram of the hierarchical algorithms;
- a 2-D scatter coloured by cluster or by class;
- an inference-system page;
- a CPU, memory and GPU monitor.

- the category *Intuitionistic* (D-PAIFC, D-AIFC(c), D-PAIFC-TC) with the parameters $c$, scan order and measure.

Both attribute data and tolerance matrices can be opened and clustered. Intuitionistic data
are opened with *Open data → Intuitionistic tolerance (μ and ν matrices)…* (two file dialogs:
memberships, then non-memberships) or *Intuitionistic attribute data…*.

### Reproducing the experiments of the book

```bash
python results/reproduce.py
```

The script (Python 3.8 or later, no packages) runs every row of the book's reproduction
tables through `mfuzzy` and writes [`results/reproduction`](results/reproduction):
one JSON per run and `summary.md`. Existing JSON files are reused; `--rerun` recomputes
everything (about three minutes), `--only <text>` runs the matching rows only, and
`--check` recomputes into a temporary folder and compares with the committed `summary.md`.

### Benchmarks

The benchmarks behind the measured speed-ups of the book's computational chapter:

```bash
cd src/dotnet
dotnet build bench/Math.Fuzzy.Benchmarks -c Release
bench/Math.Fuzzy.Benchmarks/bin/Release/net10.0/mfuzzy-bench all --data ../../datasets --out results.jsonl
bench/Math.Fuzzy.Benchmarks/bin/Release/net10.0/mfuzzy-bench all --single-core --data ../../datasets --out results.jsonl
```

Each experiment (`closure`, `search`, `bound`, `hafc`, `stages`) times variants that differ in
one technique. Before timing, it checks that every variant returns the same result.
`--single-core` pins the process to one core. The measurements reported in the book are
in [`results/performance`](results/performance); `summarize.py` there rebuilds the tables.

### Figures

```bash
cd src/dotnet
dotnet run --project tools/Math.Fuzzy.Figures -c Release -- --data ../../datasets \
    --performance ../../results/performance/measurements.jsonl --out figures
```

draws every figure of the book as SVG and PNG with the same library calls that produce the
reported results (Windows only: the PNG export uses WPF).

## MATLAB / GNU Octave

The toolbox is a package of plain functions and needs no other toolboxes. It follows the
statements of the algorithms and favours clarity over speed: use it for small data and for
checking results by hand, and the .NET library for everything else.

```matlab
addpath(fullfile(pwd, 'src', 'matlab'));
X = dlmread('datasets/sneath_sokal.csv', ',', 1, 0);
T = vfc.build_tolerance_matrix(X, 'Distance', 'squared-euclidean', 'Normalization', 'max');
r = vfc.dpafc(T);                      % four clusters at alpha = 0.91319
vfc.print_results(r);
r = vfc.dafc_tc(X, struct('distance', 'squared-euclidean', 'normalization', 'max'));
```

| Function | Description |
|----------|-------------|
| `vfc.build_tolerance_matrix` | $T$ from attribute data (distances, normalisations, accuracy threshold) |
| `vfc.max_min_closure` | max-min transitive closure $\widetilde{T}$ |
| `vfc.alpha_levels`, `vfc.alpha_leap` | levels of a relation, leap heuristic |
| `vfc.dafc` | allotments among $c$ clusters at one level |
| `vfc.dafc_c` | D-AFC(c): all levels, automatic relaxation of the separation |
| `vfc.dpafc` | D-PAFC |
| `vfc.closure_allotment` | best allotment of one level of a closure |
| `vfc.snap_levels` | merge levels that differ by rounding noise |
| `vfc.dafc_tc`, `vfc.dpafc_tc`, `vfc.hafc_tc` | closure-based and hierarchical algorithms |
| `vfc.print_results` | formatted output with Purity, ARI, NMI |

`src/matlab/tests/run_tests.m` checks the toolbox against the published examples; it runs
under GNU Octave on every push. Not available here: the transitive approximations (TAGA),
the validity measures and the generation of inference systems; of D-AFC(u) only a single pass for a fixed
bound on the intersections (`vfc.dpafc` with `opts.max_power`).

## Python

Planned ([`src/python`](src/python)). The package will mirror the .NET library's API.

## Citing and license

If you use this software, cite the book. Also cite the archived release used for
your computations, so the exact version can be traced ([`CITATION.cff`](CITATION.cff)).
Every release is archived on Zenodo: the DOI [10.5281/zenodo.23856861](https://doi.org/10.5281/zenodo.23856861)
always resolves to the latest version; version 1.0.0 is
[10.5281/zenodo.23856862](https://doi.org/10.5281/zenodo.23856862).

The code is released under the [MIT License](LICENSE). The data sets keep the
licences of their sources, which [`datasets/README.md`](datasets/README.md) lists.
