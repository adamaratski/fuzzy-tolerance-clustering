# fuzzy-tolerance-clustering

[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.23856861.svg)](https://doi.org/10.5281/zenodo.23856861)

Reference implementations of the heuristic possibilistic clustering algorithms of
D. A. Viattchenin (D-AFC family). This repository accompanies the monograph
*Fuzzy Tolerance Clustering: Theory, Algorithms, and
Implementations* (A. Damaratski, 2026). Every result in the book can be recomputed
from the data sets here.

The book covers the mathematics, the algorithms and the computational techniques.
This README covers how to install and use each implementation.

A ready-to-run build of the desktop workbench for Windows is attached to each
[release](https://github.com/adamaratski/fuzzy-tolerance-clustering/releases).

| Component | Location | Status |
|-----------|----------|--------|
| .NET library (Core, Clustering, Inference) | [`src/dotnet/src`](src/dotnet/src) | complete |
| Command-line tool `mfuzzy` | [`src/dotnet/apps/Math.Fuzzy.Cli`](src/dotnet/apps/Math.Fuzzy.Cli) | complete |
| Desktop workbench (Windows) | [`src/dotnet/apps/Math.Fuzzy.Desktop`](src/dotnet/apps/Math.Fuzzy.Desktop) | complete |
| Benchmarks of the computational chapter | [`src/dotnet/bench`](src/dotnet/bench), results in [`results/performance`](results/performance) | complete |
| MATLAB toolbox `+vfc` | [`src/matlab`](src/matlab) | relational and closure-based algorithms |
| Python package | [`src/python`](src/python) | planned |
| Benchmark data sets | [`datasets/`](datasets/) ([description](datasets/README.md)) | — |

## Algorithms

| Name | Input | Number of clusters | Book section |
|------|-------|--------------------|--------------|
| `dafc` | tolerance $T$ | given $c$ | D-AFC(c) |
| `dpafc` | tolerance $T$ | least $c$ (principal allotment); `--intersections w` admits $w$ shared objects | D-PAFC |
| `dafc-tc` | data | from the leap heuristic on $\widetilde{T}$, or given $c$ | D-AFC-TC |
| `dafc-tc-alpha` | data | from a user threshold $\hat{\alpha}$ | D-AFC-TC($\alpha$) |
| `dpafc-tc` | data | first splitting level of $\widetilde{T}$ | D-PAFC-TC |
| `dafc-taga`, `dpafc-taga` | data | via a transitive approximation | D-AFC-TAGA, D-PAFC-TAGA |
| `hafc-tc` | data | every level of $\widetilde{T}$ (hierarchy) | H-AFC-TC |
| `dpafc-dr`, `dpafc-tc-dr`, `dpafc-taga-dr` | data | attribute-space reduction | Dimensionality reduction |

## .NET

Requirements: .NET 10 SDK. On Windows the desktop application additionally needs
the Windows Desktop runtime.

```bash
cd src/dotnet
dotnet build Math.Fuzzy.slnx -c Release
dotnet test --solution Math.Fuzzy.slnx -c Release
```

Tests run on Microsoft Testing Platform with xUnit v3, which `src/dotnet/global.json` enables.

### Library

| Assembly | Contents |
|----------|----------|
| `Math.Fuzzy.Core` | tolerance matrices; distances (Hamming, Euclidean, squared Euclidean, Manhattan, Chebyshev, Minkowski, cosine); normalisations (`max`, `minmax`, `none`); accuracy threshold $\varepsilon$; max-min transitive closure; α-level sequences and leap heuristic; criteria $F_1$, $F_2$; TAGA aggregation operators |
| `Math.Fuzzy.Clustering` | all algorithms above, external validity indices (Purity, ARI, NMI) |
| `Math.Fuzzy.Inference` | Mamdani and Takagi–Sugeno inference, generation of inference systems from allotments |

```csharp
using MathFuzzy.Clustering;
using MathFuzzy.Core.Matrix;

var data    = RawDataMatrix.FromCsv("datasets/iris.csv");
var options = new AlgorithmOptions(Distance: "squared-euclidean",
                                   Normalization: "max",
                                   Accuracy: 1e-5,
                                   ClusterCount: 3);
var results = AlgorithmFactory.Run("dafc", data, options);

var best = results[0];
Console.WriteLine($"c = {best.ClusterCount}, alpha* = {best.Alpha:F5}, F1 = {best.Score:F4}");
for (int cluster = 0; cluster < best.ClusterCount; cluster++)
    Console.WriteLine($"cluster {cluster + 1}: typical point x{best.Centers[cluster] + 1}, " +
                      $"{best.MemberSets[cluster].Length} objects");
```

`results` lists the valid allotments, ranked by the criterion, with ties broken by a
total order. Each result holds:
- the threshold $\alpha$;
- the typical points;
- the carriers;
- the membership matrix $U$;
- the criterion value.

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
| `matrix build`, `matrix closure` | build $T$ from data; compute $\widetilde{T}$ |
| `reduce` | attribute selection (`--method dpafc \| dpafc-tc \| dpafc-taga`) |
| `infer` | evaluate a Mamdani or Sugeno system (`--system rules.json --input 5.1,3.5,1.4,0.2`) |

Options of `cluster`:

| Option | Values | Default |
|--------|--------|---------|
| `--algorithm` | see the algorithm table | — |
| `--input` | CSV with data (a `class` column is detected and used for validity indices) or an $n\times n$ tolerance matrix | — |
| `--clusters` | $c$ for D-AFC(c) and fixed-$c$ D-AFC-TC | auto |
| `--alpha` | $\hat{\alpha}\in(0,1)$ for `dafc-tc-alpha`; a single level for `dafc` | — |
| `--intersections` | $w\ge 0$ for `dpafc` | 0 |
| `--distance` | `euclidean`, `squared-euclidean`, `manhattan`, `chebyshev`, `minkowski`, `cosine`, `hamming` | `euclidean` |
| `--minkowski-p` | order $p$ | 3 |
| `--normalize` | `minmax`, `max`, `none` | `minmax` |
| `--accuracy` | $\varepsilon$, e.g. `0.00001` | full precision |
| `--taga` | `max`, `min`, `mean`, `median`, `umedian`, `dmedian` | `mean` |
| `--criterion` | `f1`, `f2` | `f1` |
| `--format`, `--output` | `table`, `json`, `csv`; output file | `table`, stdout |

To reproduce the setting of the monograph, use `--distance squared-euclidean --normalize max --accuracy 0.00001`.

### Desktop workbench

```bash
dotnet run --project src/dotnet/apps/Math.Fuzzy.Desktop -c Release
```

At start-up the workbench opens the sample data sets from `datasets/`.

What it shows:
- the tolerance matrix and its closure as heat maps;
- every algorithm with its parameters, with a Stop button and a progress bar while a run is going;
- membership heat maps, cluster tables and validity indices;
- the dendrogram of H-AFC-TC;
- a 2-D scatter coloured by cluster or by class;
- an inference-system page;
- a CPU, memory and GPU monitor.

### Benchmarks

The benchmarks behind the measured speed-ups of the book's computational chapter:

```bash
cd src/dotnet
dotnet build bench/Math.Fuzzy.Benchmarks -c Release
bench/Math.Fuzzy.Benchmarks/bin/Release/net10.0/mfuzzy-bench all --data ../../datasets --out results.jsonl
bench/Math.Fuzzy.Benchmarks/bin/Release/net10.0/mfuzzy-bench all --single-core --data ../../datasets --out results.jsonl
```

Each experiment (`closure`, `search`, `hafc`, `stages`) times variants that differ in
one technique. Before timing, it checks that every variant returns the same result.
`--single-core` pins the process to one core. The measurements reported in the book are
in [`results/performance`](results/performance); `summarize.py` there rebuilds the tables.

## MATLAB

The toolbox is a package of plain MATLAB functions and needs no other toolboxes.

```matlab
addpath(fullfile(pwd, 'src', 'matlab'));
X  = readmatrix('datasets/iris.csv');
T  = vfc.build_tolerance_matrix(X, 'Distance', 'euclidean');
Tc = vfc.max_min_closure(T);
r  = vfc.dpafc_tc(Tc);
vfc.print_results(r);
```

| Function | Description |
|----------|-------------|
| `vfc.build_tolerance_matrix` | $T$ from attribute data |
| `vfc.max_min_closure` | max-min transitive closure $\widetilde{T}$ |
| `vfc.alpha_levels` | distinct α-levels |
| `vfc.alpha_leap` | leap heuristic |
| `vfc.dafc` | D-AFC(c) at a given level |
| `vfc.dpafc` | search over $c$ and levels |
| `vfc.dafc_tc`, `vfc.dpafc_tc` | closure-based algorithms |
| `vfc.print_results` | formatted output with Purity, ARI, NMI |

Not available in MATLAB: the transitive approximations (TAGA), H-AFC-TC and the
generation of inference systems. Use the .NET library for these.

## Python

Planned ([`src/python`](src/python)). The package will mirror the .NET library's API.

## Citing and license

If you use this software, cite the book. Also cite the archived release used for
your computations, so the exact version can be traced ([`CITATION.cff`](CITATION.cff)).
Every release is archived on Zenodo: the DOI [10.5281/zenodo.23856861](https://doi.org/10.5281/zenodo.23856861)
always resolves to the latest version, and version 1.0.0, with which the results of
the book were computed, is [10.5281/zenodo.23856862](https://doi.org/10.5281/zenodo.23856862).

The code is released under the [MIT License](LICENSE). The data sets keep the
licences of their sources, which [`datasets/README.md`](datasets/README.md) lists.
