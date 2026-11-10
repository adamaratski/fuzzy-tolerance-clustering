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
  `--scan-order beta-inner|beta-outer` (`beta-outer` reproduces the published D-PAIFC result);
  an extra `Beta` column in the table, `β` and `w` in the best line, `beta` and
  `intersectionBound` in JSON. `--distance`, `--normalize`, `--criterion` and `--accuracy`
  are rejected for these algorithms.

Desktop workbench
- *Open data → Intuitionistic tolerance (μ and ν matrices)…* and *Intuitionistic attribute
  data…*; the category *Intuitionistic* with D-PAIFC, D-AIFC(c) and D-PAIFC-TC; parameters:
  number of clusters (D-AIFC(c)), scan order, measure (attribute data). The *Tolerance* tab
  shows the membership matrix, the result line the pair (α, β). Running an intuitionistic
  algorithm on ordinary data, or an ordinary algorithm on intuitionistic data, gives a
  message that says what to open or choose.

Data files
- `hung_ifs_membership.csv`, `hung_ifs_nonmembership.csv`, `cars5_ifs.csv`, `cars10_ifs.csv`.

Tests
- 20 new tests for the intuitionistic algorithms; five runs on intuitionistic data in
  `results/reproduce.py` (51 runs in all).

Other
- MATLAB / GNU Octave toolbox rewritten to follow the algorithms (D-AFC(c), D-PAFC, D-AFC-TC,
  D-PAFC-TC, H-AFC-TC), with a test script.
- Figures of the book (`tools/Math.Fuzzy.Figures`), continuous integration, `tamura_16.csv`.

## 1.0.0

First public release: D-AFC(c), D-PAFC, D-AFC-TC, D-AFC-TC(α), D-PAFC-TC, H-AFC-TC,
attribute-space reduction, inference systems, command-line tool, desktop workbench.
