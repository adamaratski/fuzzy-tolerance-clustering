# Changes

## 1.1.0

Algorithms
- **D-AFC(u)** (`dafc-u`): the largest cluster size is given instead of the number of clusters.
- **TAGA family rewritten** after the published procedure: transitive approximation with six
  aggregation operators, automatic choice of the operator, D-AFC-TAGA(α) (`dafc-taga-alpha`),
  hierarchical variant (`hafc-taga`). The published six-operator table for the Sneath–Sokal data
  is a test. Equal similarities are recognised as equal by default (tie tolerance 10⁻¹²).
- Validity measures of an allotment (linear and quadratic fuzziness, separation and
  compactness), the technique for the optimal cluster size (`mfuzzy size`) and the bounds of
  the number of clusters (`mfuzzy bounds`).
- **Tolerance matrices as input** (`--matrix`, *Open matrix* in the workbench) for every
  algorithm except the attribute-space reduction.

- **Intuitionistic fuzzy data** (D. A. Viattchenin's heuristic approach): D-PAIFC (`dpaifc`),
  D-AIFC(c) (`daifc`) and D-PAIFC-TC (`dpaifc-tc`), in the library, the command-line tool and
  the workbench. The input is an intuitionistic fuzzy tolerance (two matrices: memberships and
  non-memberships) or intuitionistic attribute data (pairs of columns, membership and
  non-membership), from which the tolerance is built with the Hamming or the Euclidean
  similarity measure. D-PAIFC returns the first pair of thresholds (α, β) with fully separated
  clusters; D-AIFC(c) raises the intersection bound w only if no fully separated allotment
  with c clusters exists; D-PAIFC-TC applies D-PAIFC to the transitive closure (max-min on the
  memberships, min-max on the non-memberships). The criterion is F_IFS. The published
  examples (Hung–Lee–Fuh relation, ten cars) are tests.
- **Order-free scan** for D-PAIFC and D-PAIFC-TC, in the library, the command-line tool and the
  workbench: the definition proposed in the accompanying book, not a published one; the printed
  scan orders remain the default. The pairs (α, β) are only partially ordered, so the first
  admissible pair depends on the scan order and neither printed order is guaranteed to give the
  least number of clusters. The order-free scan examines all admissible pairs; the loosest of
  them form the frontier, the least number of clusters c* is attained on the frontier, and among
  all allotments with c* fully separated clusters the one with the largest quality
  Q = Σ (mean membership − mean non-membership) over the clusters is the result. The thresholds
  of an allotment are its own. If the node budget runs out, no allotment is returned.

- **Transitive opening** (`dafc-to`, `dafc-to-alpha`, `dpafc-to`; option `--opening
  complete-linkage|single-linkage-order`): the closure-based algorithms with a transitive
  opening of the tolerance in place of the closure.
- **Partial supervision**: D-AFC-PS(c) (`dafc-ps`) and D-AIFC-PS(c) (`daifc-ps`); the labelled
  objects are given with `--label object:y[:f][@class]` (objects numbered from 1), the rule for
  raising the intersection bound of `daifc-ps` with `--ps-rule step10|full-scan`.
- **Interval-valued data**: tolerances from seven measures between interval-valued fuzzy sets,
  the type of the clustering structure (stable, quasi-stable, unstable) with the plausible
  numbers of clusters, the stable-structure technique and the selection of the paths of a
  project network.
- **Fuzzy-number, three-way and asymmetric data**: reading and the construction of the fuzzy
  tolerance (triangular, trapezoidal and Gaussian fuzzy numbers; generalised distances of
  three-way data; quasi-equivalence or likeness of an asymmetric proximity matrix).
- **Large data sets**: reduction to interval-valued representatives by k-means or a given
  partition, clustering of the representatives by any relational algorithm, memberships carried
  over to the objects.
- **Fuzzy preference relations**: Kuzmin distances, clustering of the relations, selection of
  the most representative relation by the index of fuzziness and Orlovsky's non-domination method.

Search
- Closure-based algorithms (D-AFC-TC, D-AFC-TC(α), D-PAFC-TC) rank the allotments of a level
  class by class instead of enumerating combinations; ties between typical points are resolved
  by the distance to the centroid of the class, the same rule in all of them and in H-AFC-TC.
- Exact-cover bound in the D-AFC search (the cluster powers of a fully separated allotment
  sum to n); results are unchanged, D-AFC(3) on the Iris data is about 17 times faster.
- The node budget is reported: `SearchDiagnostics`, a warning of the tool and of the
  workbench, option `--max-nodes`. D-PAFC no longer returns an allotment when the budget ran
  out before a level was decided.
- Levels of the tolerance matrix that differ by rounding noise (≤ 10⁻¹²) are merged: the Iris
  data have 7 887 levels with the normalised Euclidean distance, not 8 653.

Command-line tool
- Options that an algorithm does not use are rejected; new options `--matrix`, `--max-size`,
  `--min-size`, `--max-nodes`, `--top`; exit codes 0, 2, 3; H-AFC-TC prints the hierarchy.
- Data files: comma, semicolon or tab separated, quoted fields, decimal comma, object-name
  columns; missing values are reported with their position.
- External indices assign an object that lies in several clusters to the cluster of its
  largest membership.
- JSON output is always an object with `results` and `search`.
- Intuitionistic algorithms: options `--nonmembership`, `--ifs-measure hamming|euclidean`,
  `--scan-order beta-inner|beta-outer|order-free` (`beta-outer` reproduces the published D-PAIFC
  result; `order-free` is available for `dpaifc` and `dpaifc-tc`, `daifc` rejects it; the
  criterion column is named `Q_IFS`, the text output lists the frontier, JSON has an object
  `thresholds` with `admissiblePairs` and `frontier`); an extra `Beta` column in the table, `β` and `w` in the best line, `beta` and
  `intersectionBound` in JSON. `--distance`, `--normalize`, `--criterion` and `--accuracy`
  are rejected for these algorithms.
- Other kinds of input for `cluster`: `--interval` (with `--interval-measure`, `--lambda`,
  `--interval-scaling`), `--fuzzy-numbers` (`--shape-left`, `--shape-right`), `--three-way`
  (`--object-column`, `--situation-column`, `--keep-self-similarity`; `--distance` and `--normalize`
  apply) and `--asymmetric` (`--symmetrize`, `--proximity`); at most one of them, `--matrix` and
  `--nonmembership`. `--distance`, `--normalize` and `--minkowski-p` are rejected for the others.
- New commands `interval-stability`, `interval-structure`, `project-paths` and `preference`;
  `reduce --algorithm <key>` for large data (the former attribute selection stays as
  `reduce --method`). All print a table or JSON (`--format`) and write to `--output`.
- Options `--opening`, `--label` and `--ps-rule` of `cluster`.

Desktop workbench
- *Open data → Intuitionistic tolerance (μ and ν matrices)…* and *Intuitionistic attribute
  data…*; the category *Intuitionistic* with D-PAIFC, D-AIFC(c) and D-PAIFC-TC; parameters:
  number of clusters (D-AIFC(c)), scan order (the third item, *order-free*, gives a parameter
  message with D-AIFC(c)), measure (attribute data). The *Tolerance* tab
  shows the membership matrix, the result line the pair (α, β). Running an intuitionistic
  algorithm on ordinary data, or an ordinary algorithm on intuitionistic data, gives a
  message that says what to open or choose.
- D-AFC-TO, D-AFC-TO(α) and D-PAFC-TO in the category *Prototype-based* (parameter *Opening*),
  D-AFC-PS(c) in *Relational* and D-AIFC-PS(c) in *Intuitionistic* (field *Labelled objects*,
  `object:y` or `object:y:f`).
- *Open data → Interval-valued data…*, *Fuzzy-number data…*, *Three-way data…* and *Asymmetric
  proximity matrix…*: the tolerance is built on loading with the default settings of the
  command-line tool, and every algorithm that accepts a tolerance matrix runs on it.

Data files
- `hung_ifs_membership.csv`, `hung_ifs_nonmembership.csv`, `cars5_ifs.csv`, `cars10_ifs.csv`.
- `ichino_oils_interval.csv`, `sato_jain_interval.csv`, `project_paths_interval.csv`,
  `sato_sato_threeway.csv`, `children_dislike.csv`, `yang_ko_vectors.csv`, `yang_ko_30.csv`,
  `preference_six_relations.csv`.

Tests
- 27 new tests for the intuitionistic algorithms; nine runs on intuitionistic data in
  `results/reproduce.py` (55 runs in all).
- Six runs of the transitive opening, partial supervision and the interval, three-way and
  asymmetric inputs in `results/reproduce.py` (61 runs in all). The test suite has 478 tests.

Other
- MATLAB / GNU Octave toolbox rewritten to follow the algorithms (D-AFC(c), D-PAFC, D-AFC-TC,
  D-PAFC-TC, H-AFC-TC), with a test script.
- Figures of the book (`tools/Math.Fuzzy.Figures`), continuous integration, `tamura_16.csv`.

## 1.0.0

First public release: D-AFC(c), D-PAFC, D-AFC-TC, D-AFC-TC(α), D-PAFC-TC, H-AFC-TC,
attribute-space reduction, inference systems, command-line tool, desktop workbench.
