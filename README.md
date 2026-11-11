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
tolerance matrix given directly; the attribute-space reduction needs attribute data. The
intuitionistic algorithms (`dpaifc`, `daifc`, `dpaifc-tc`, `daifc-ps`) take intuitionistic fuzzy data instead, see
below. Interval-valued, fuzzy-number, three-way and asymmetric data are turned into a tolerance
matrix first and then run with any algorithm that takes a tolerance matrix ([other kinds of
input](#other-kinds-of-input)).

| Name | What is given | What is found | Book section |
|------|---------------|---------------|--------------|
| `dafc` | number of clusters $c$ | threshold $\alpha$, typical points; the separation is relaxed ($w = 0, 1, \dots$) only if needed | D-AFC(c) |
| `dafc-u` | largest cluster size $u$ (`--max-size`) | $c$, $\alpha$, typical points | D-AFC(u) |
| `dpafc` | nothing; optionally `--intersections w` | least $c$ at the first admissible level (principal allotment) | D-PAFC |
| `dafc-tc` | nothing, or $c$ | level of the closure by the leap heuristic | D-AFC-TC |
| `dafc-tc-alpha` | least threshold $\hat{\alpha}$ (`--alpha`) | leap level not below $\hat{\alpha}$ | D-AFC-TC($\alpha$) |
| `dpafc-tc` | nothing | first splitting level of the closure | D-PAFC-TC |
| `dafc-taga`, `dafc-taga-alpha`, `dpafc-taga` | aggregation operator `--taga` (default: chosen automatically) | as the `-tc` algorithms, on a transitive approximation | D-AFC-TAGA family |
| `dafc-to` | nothing; `--opening complete-linkage \| single-linkage-order` | level of the transitive opening by the leap heuristic | D-AFC-TO |
| `dafc-to-alpha` | least threshold $\hat{\alpha}$ (`--alpha`) | leap level of the opening not below $\hat{\alpha}$ | D-AFC-TO($\alpha$) |
| `dpafc-to` | nothing | first splitting level of the opening | D-PAFC-TO |
| `dafc-ps` | number of clusters $c$ and labelled objects (`--label object:y`, repeated; `@k` puts several objects into class $k$); `--intersections w` | $\alpha$, typical points; the labelled objects are typical points of their clusters | D-AFC-PS(c) |
| `hafc-tc`, `hafc-taga` | nothing | the allotment of every level (hierarchy) | H-AFC-TC, H-AFC-TAGA |
| `dpafc-dr`, `dpafc-tc-dr`, `dpafc-taga-dr` | nothing | attributes are clustered, one per cluster is kept, D-PAFC runs on the reduced data | Dimensionality reduction |
| `dpaifc` | nothing | the first pair of thresholds $(\alpha, \beta)$ with fully separated intuitionistic fuzzy clusters (least number of clusters) | D-PAIFC |
| `daifc` | number of clusters $c$ (`--clusters`) | $(\alpha, \beta)$, typical points; the clusters share objects only if no fully separated allotment exists ($w = 0, 1, \dots$) | D-AIFC(c) |
| `dpaifc-tc` | nothing | D-PAIFC on the transitive closure of the intuitionistic fuzzy tolerance (max-min on the memberships, min-max on the non-memberships); ties by the prototype rule | D-PAIFC-TC |
| `daifc-ps` | number of clusters $c$ and labelled objects (`--label object:y:f`, with membership $y$ and non-membership $f$); `--ps-rule step10 \| full-scan` | $(\alpha, \beta)$, typical points; the labelled objects are typical points of their clusters | D-AIFC-PS(c) |

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
| `reduce` | attribute selection (`--method dpafc \| dpafc-tc \| dpafc-taga`): prints the selected attributes and, with `--output`, writes the reduced data; with `--algorithm <key>`, reduction of large data (see below) |
| `interval-stability` | type of the clustering structure of interval-valued data and the plausible numbers of clusters |
| `interval-structure` | the stable-structure technique for interval-valued data |
| `project-paths` | selection of the paths of a project network with interval-valued durations |
| `preference` | the most representative fuzzy preference relations of a group of experts and the best alternatives |
| `infer` | evaluate an inference system for one input vector (`--system rules.json --input 5.1,3.5,1.4,0.2`); the JSON is written by `cluster --rules` or exported by the workbench |

Options of `cluster`:

| Option | Values | Default |
|--------|--------|---------|
| `--algorithm` | see the algorithm table | — |
| `--input` | data file: comma, semicolon or tab separated, with or without a header. A label column — a text column with repeated values, or a column headed `class`, `label`, `labels`, `type`, `species`, `variety`, `target`, `category` or `group` — is left out of the data and used for the external indices; a column of object names is ignored. A missing value is an error | — |
| `--matrix` | the input file is a fuzzy tolerance matrix (symmetric, values in [0, 1]; a header row and a column of names are allowed) | attribute data |
| `--interval`, `--fuzzy-numbers`, `--three-way`, `--asymmetric` | the input file is of that kind, see [other kinds of input](#other-kinds-of-input); at most one of them, `--matrix` and `--nonmembership` | attribute data |
| `--clusters` | $c$ for `dafc`, `daifc`, `dafc-ps`, `daifc-ps` (required) and `dafc-tc` (optional) | — |
| `--alpha` | a single level for `dafc`; the least threshold for `dafc-tc-alpha`, `dafc-taga-alpha`, `dafc-to-alpha` | — |
| `--opening` | `complete-linkage`, `single-linkage-order`: how the transitive opening is computed (`dafc-to`, `dafc-to-alpha`, `dpafc-to`) | `complete-linkage` |
| `--label` | a labelled object `object:y` (`daifc-ps`: `object:y:f`), objects numbered from 1; repeat the option; `object:y@k` puts several objects into class $k$ (`dafc-ps`, `daifc-ps`) | — |
| `--ps-rule` | `step10`, `full-scan`: when the intersection bound is raised (`daifc-ps`) | `step10` |
| `--max-size` | largest number of objects in a cluster; the parameter $u$ of `dafc-u` | no bound |
| `--min-size` | least number of objects in a cluster | 1 |
| `--intersections` | $w\ge 0$ for `dafc`, `dafc-u`, `dpafc`, `dafc-ps` | `dpafc`: 0; `dafc`, `dafc-u`: automatic |
| `--distance` | `euclidean`, `squared-euclidean`, `manhattan`, `chebyshev`, `minkowski`, `cosine`, `hamming`; with `--three-way` only `hamming`, `euclidean`, `squared-euclidean` | `euclidean`; `--three-way`: `squared-euclidean` |
| `--minkowski-p` | order $p$ | 3 |
| `--normalize` | `minmax`, `max`, `none` | `minmax` |
| `--accuracy` | $\varepsilon$, e.g. `0.00001` (not with `--interval`) | full precision |
| `--taga` | `auto`, `max`, `min`, `mean`, `median`, `umedian`, `dmedian` | `auto` |
| `--criterion` | `f1`, `f2` | `f1` |
| `--nonmembership` | file with the matrix of non-memberships; with `--input` as the matrix of memberships it gives an intuitionistic fuzzy tolerance (`dpaifc`, `daifc`, `dpaifc-tc`) | — |
| `--ifs-measure` | `hamming`, `euclidean`: similarity measure that builds the tolerance from intuitionistic attribute data | `hamming` |
| `--scan-order` | `beta-inner`, `beta-outer`, `order-free`: order in which the pairs $(\alpha, \beta)$ are scanned by the intuitionistic algorithms; `beta-outer` reproduces the published D-PAIFC result on the relation of Hung, Lee and Fuh; `order-free` examines all admissible pairs and returns the least number of clusters (`dpaifc`, `dpaifc-tc`; `daifc` rejects it), see below | `beta-inner` |
| `--interval-measure` | `sqeuclid`, `euclid`, `bb`, `hausdorff`, `juyuan`, `sqeuclid-matching`, `euclid-matching` (`--interval`) | `sqeuclid` |
| `--lambda` | exponent $\lambda\ge 1$ of the Ju–Yuan measure (`--interval`) | 2 |
| `--interval-scaling` | `minmax`, `max`, `none`: scaling of the bounds (`--interval`) | `minmax` |
| `--shape-left`, `--shape-right` | shape integrals $l$, $r$ of the shape functions (`--fuzzy-numbers`); 0.5 is the linear shape (triangular, trapezoidal) | 0.5 |
| `--object-column`, `--situation-column` | names of the columns that hold the object and the situation (`--three-way`) | the first and the second column |
| `--keep-self-similarity` | keep $1-d(x_i, x_i)$ on the diagonal of the tolerance (`--three-way`) | diagonal 1 |
| `--symmetrize` | `quasi-equivalence`, `likeness` (`--asymmetric`) | `quasi-equivalence` |
| `--proximity` | `auto`, `dissimilarity`, `similarity` (`--asymmetric`) | `auto` |
| `--max-nodes` | node budget of the combination search | 500 000 000 |
| `--top` | number of allotments reported | 200 |
| `--format`, `--output` | `table`, `json`, `csv`; output file | `table`, stdout |
| `--rules` | file for the fuzzy inference system generated from the best allotment (one Mamdani rule per cluster, JSON) | — |

An option that the chosen algorithm does not use is an error, not a silent no-op, and so is an
unknown algorithm, distance, normalisation, criterion, operator or format name. Exit codes:
0 — an allotment was found, 2 — invalid input or options, 3 — no allotment exists.
Numbers are read and written with a decimal point on every system locale. Object numbers in
the output of `cluster` are 0-based; the other commands number the objects from 1 and
show their names when the file has them.

To reproduce the setting of the monograph, use `--distance squared-euclidean --normalize max --accuracy 0.00001`.
With `--interval`, `--fuzzy-numbers` and `--asymmetric` the options on attribute distances
(`--distance`, `--normalize`, `--minkowski-p`) are errors, and so are the intuitionistic algorithms.

The intuitionistic algorithms do not use `--distance`, `--normalize`, `--criterion` and
`--accuracy`. Their table has an extra `Beta` column and the criterion $F_{IFS}$; the best line
shows $\beta$ and, for `daifc`, the intersection bound $w$; JSON has `beta` and
`intersectionBound`.

The pairs $(\alpha, \beta)$ are only partially ordered, so the first admissible pair of the
printed algorithm depends on the scan order: `beta-inner` stops at the loosest admissible pair
with the least $\alpha$, `beta-outer` at the one with the largest $\beta$, and neither is
guaranteed to give the least number of clusters. `--scan-order order-free` is the definition
proposed in the accompanying book, not a published one; the printed scan orders remain the
default. It examines all admissible pairs; the loosest of them form the frontier, and the least
number of clusters $c^*$ is attained on the frontier. Among all allotments with $c^*$ fully
separated clusters, at whatever pair they occur, the result is the one with the largest quality
$Q_{IFS} = \sum$ over the clusters of (mean membership $-$ mean non-membership); at one pair
$Q_{IFS}$ differs from $F_{IFS}$ by the constant $c(\alpha - \beta)$, so the ranking within a
pair is the same. The thresholds printed with an allotment are its own: its least membership
degree and its largest non-membership degree. The criterion column is named `Q_IFS`; after the
allotments with $c^*$ clusters the table lists, for every larger number of clusters on the
frontier, the allotments of the frontier pairs with that number. The text output lists the
frontier (`Admissible pairs of thresholds: N. The loosest of them (α, β) → c:`); JSON has an
object `thresholds` with `admissiblePairs` (the count) and `frontier` (a list of `alpha`,
`beta`, `clusterCount`). If the node budget runs out, no allotment is returned, because the
least number of clusters would not be known.

```bash
mfuzzy cluster --algorithm dafc-u --max-size 6 --matrix --input datasets/tamura_16.csv
mfuzzy size --input datasets/sneath_sokal.csv --distance squared-euclidean --normalize max
mfuzzy bounds --objects 150 --clusters 2 --alpha 0.80384 --shape gaussian
mfuzzy cluster --algorithm daifc --clusters 3 --input datasets/hung_ifs_membership.csv \
    --nonmembership datasets/hung_ifs_nonmembership.csv
mfuzzy cluster --algorithm dpaifc-tc --input datasets/cars10_ifs.csv --ifs-measure hamming
mfuzzy cluster --algorithm dpaifc --input datasets/cars10_ifs.csv --scan-order order-free
```

The first of the two new examples gives $(\alpha, \beta) = (0.4, 0.6)$ with $w = 1$; the second
gives four clusters at $(0.4833, 0.1667)$. The last line gives four clusters at
$(0.5333, 0.2333)$, where the default order stops at a pair with six clusters. With
`dpaifc-tc` on the same file, `order-free` gives two clusters (nine objects and one) at
$(0.5167, 0.1833)$; the published four clusters at $(0.4833, 0.1667)$ are listed after them. On
the relation of Hung, Lee and Fuh (`--input datasets/hung_ifs_membership.csv --nonmembership
datasets/hung_ifs_nonmembership.csv`) `dpaifc` with `order-free` gives five clusters at
$(0.8, 0.1)$, $Q_{IFS} = 4.5667$; the frontier has $(0.1, 0.1)$ and $(0.8, 0.2)$, both with five
clusters, the pairs at which the two printed scan orders stop.

#### Other kinds of input

Each of these options replaces the attribute data: the file is read, a fuzzy tolerance matrix is
built from it, and the chosen algorithm runs on that matrix as with `--matrix`.

| Option | File | Tolerance |
|--------|------|-----------|
| `--interval` | one row per object, two numeric columns per attribute, the lower and the upper bound, for example `name,gravity_lo,gravity_hi,iodine_lo,iodine_hi`; the first text column holds the object names | a measure between interval-valued fuzzy sets on the jointly scaled bounds (`--interval-measure`, `--lambda`, `--interval-scaling`) |
| `--fuzzy-numbers` | one row per object, several columns per attribute named `attribute_suffix`: `_m,_a,_b` (triangular, LR-type), `_ml,_mr,_a,_b` (trapezoidal, LR-type interval) or `_m,_sigma` (Gaussian); one kind in a file, detected from the suffixes | $1-D/\max D$ with the averaged squared distance of Yang and Ko (`--shape-left`, `--shape-right`) |
| `--three-way` | long format with a header: one row per object and situation, the object in the first column, the situation in the second (or named with `--object-column`, `--situation-column`), then one numeric column per attribute; every object in every situation | $1-d$ with a generalised distance (`--distance hamming \| euclidean \| squared-euclidean`) of the normalised data (`--normalize`, default `minmax`) |
| `--asymmetric` | a square matrix of non-negative proximities, asymmetric, with an optional header and a column of names | the quasi-equivalence or the likeness of the weak preference relation obtained by normalising by the maximum and, for dissimilarities, complementing (`--symmetrize`, `--proximity`) |

```bash
mfuzzy cluster --algorithm dafc --clusters 2 --interval --input datasets/ichino_oils_interval.csv
mfuzzy cluster --algorithm dafc --clusters 3 --three-way --input datasets/sato_sato_threeway.csv
mfuzzy cluster --algorithm dpafc --asymmetric --input datasets/children_dislike.csv
mfuzzy cluster --algorithm dafc-tc --fuzzy-numbers --input datasets/yang_ko_vectors.csv
```

The first line gives two clusters at $\alpha^* = 0.8185$ with the typical points $x_2$ and $x_8$;
the second three fully separated clusters of 3, 7 and 28 objects at $\alpha^* = 0.9312$; the
third five fully separated clusters at $\alpha = 0.6667$.

#### Sub-commands for interval-valued data, large data and preference relations

All of them take `--format table|json` and `--output <file>`; the exit code is 2 for invalid input
and 3 when no allotment exists. Objects are numbered from 1.

| Command | Options | Output |
|---------|---------|--------|
| `interval-stability` | `--input`, `--bound-normalization separate\|joint`, `--interval-scaling`, `--leap ratio\|difference`, `--snorm max\|probsum\|boundedsum` | the type of the structure (stable, quasi-stable, unstable); for the lower and the upper bounds the threshold, the number of clusters, the classes and the prototypes; the possibility of every number of clusters and the plausible numbers |
| `interval-structure` | the options above and `--interval-measure`, `--lambda`, `--validity qmf\|lmf\|msc`, `--validity-direction max\|min`, `--clusters 2,3,4` | the candidates (number of clusters, threshold, criterion, validity, unique, admissible) and the selected allotment |
| `project-paths` | the options of `interval-structure` and `--select density\|lmf\|qmf` | the indices of fuzziness and the density of every cluster of paths and the selected paths |
| `reduce --algorithm <key>` | the options of `cluster` that the key needs (`--clusters`, `--alpha`, `--opening`, `--intersections`, `--min-size`, `--max-size`, `--taga`, `--criterion`, `--accuracy`, `--max-nodes`, `--top`), `--representatives N`, `--partition labels.csv`, `--no-reduce`, `--kmeans-restarts 10`, `--kmeans-seed 0`, `--kmeans-normalization none\|minmax\|max`, `--format table\|json\|csv` | the number of representatives and the allotment of all objects; external validation when the file has labels |
| `preference` | `--input`, `--weights w1,w2,…`, `--tie-tolerance` | the Kuzmin distances, the tolerance, the allotment of the relations and its typical relations, the index of fuzziness of every relation, the selected relation(s), the non-dominated set, the ranking and the best alternative(s) |

`reduce --algorithm` is the reduction of large data sets: the objects are partitioned into $\lfloor\sqrt{n}\rfloor$ classes by
k-means (or into the classes of `--partition`), every class becomes an interval vector of the
minima and maxima of the attributes, the interval vectors are clustered by the chosen algorithm,
and every object takes the memberships of its class. `--no-reduce` makes every object its own class. The options
`--distance` and `--normalize` do not apply: the tolerance between the interval vectors is built from their bounds.
The file of `preference` has the header `expert,alternative,a1,…,an` and $n$ rows per expert: the
expert, the first argument $a_i$ and the degrees $\mu(a_i, a_1), \dots, \mu(a_i, a_n)$. With `--weights`
(one per relation of the file) the selected relations are aggregated with those weights, renormalised
over the selected ones; without it they have equal weights.

```bash
mfuzzy interval-stability --input datasets/ichino_oils_interval.csv
mfuzzy interval-structure --input datasets/ichino_oils_interval.csv --validity qmf
mfuzzy project-paths --input datasets/project_paths_interval.csv --select density
mfuzzy reduce --input datasets/iris_labeled.csv --algorithm dafc --clusters 3
mfuzzy preference --input datasets/preference_six_relations.csv
```

The oil data are of the unstable type (lower bounds: 2 clusters at 0.7900, upper bounds: 3 clusters
at 0.9496; plausible numbers 2, 3, 4) and the technique selects two clusters ($V_{QMF} = 0.4568$).
The first path is selected under each of the three rules. The six relations fall into
$\{R_1, R_2, R_3\}$ and $\{R_4, R_5, R_6\}$ with the typical relations $R_1$ and $R_4$; $R_1$ is
selected and $a_3$ is the best alternative with degree 0.8. On Iris, `reduce` reports 12 representatives.

The examples for the transitive opening and for partial supervision:

```bash
mfuzzy cluster --algorithm dafc-to --input datasets/sneath_sokal.csv --normalize max --distance squared-euclidean
mfuzzy cluster --algorithm dafc-ps --clusters 2 --input datasets/yang_wu.csv --normalize max \
    --distance squared-euclidean --accuracy 0.00001 --label 1:1.0 --label 9:1.0
mfuzzy cluster --algorithm daifc-ps --clusters 2 --input datasets/cars5_ifs.csv --scan-order beta-outer \
    --label 1:1.0:0.0 --label 5:1.0:0.0
```

They give two classes of 8 at $\alpha^* = 0.6450$ (typical points $x_2$ and $x_{14}$), two clusters sharing one
object at $\alpha^* = 0.9266$, and $(\alpha, \beta) = (0.70, 0.25)$.

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

- the category *Intuitionistic* (D-PAIFC, D-AIFC(c), D-PAIFC-TC) with the parameters $c$, scan order (*β inner*, *β outer*, *order-free*) and measure.

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
always resolves to the latest version; version 1.1.0 is
[10.5281/zenodo.24131773](https://doi.org/10.5281/zenodo.24131773) and version 1.0.0 is
[10.5281/zenodo.23856862](https://doi.org/10.5281/zenodo.23856862).

The code is released under the [MIT License](LICENSE). The data sets keep the
licences of their sources, which [`datasets/README.md`](datasets/README.md) lists.
