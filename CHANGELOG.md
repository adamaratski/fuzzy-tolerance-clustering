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

Other
- MATLAB / GNU Octave toolbox rewritten to follow the algorithms (D-AFC(c), D-PAFC, D-AFC-TC,
  D-PAFC-TC, H-AFC-TC), with a test script.
- Figures of the book (`tools/Math.Fuzzy.Figures`), continuous integration, `tamura_16.csv`.

## 1.0.0

First public release: D-AFC(c), D-PAFC, D-AFC-TC, D-AFC-TC(α), D-PAFC-TC, H-AFC-TC,
attribute-space reduction, inference systems, command-line tool, desktop workbench.
