# Math.Fuzzy — Clustering Datasets

All files are comma-separated CSV with header row. Numeric features only in base versions. Files ending `_labeled.csv` include one label column (`class`, `type`, `variety`, `species` or `group`; the reader also accepts `label`, `labels`, `target` and `category`) for validation. Files of type *relational* contain a fuzzy tolerance matrix ($n \times n$, symmetric, values in $[0,1]$) and are passed to the algorithms with `--matrix`.

## Real-world datasets (UCI Machine Learning Repository)

| File | n | m | True clusters | Description |
|---|---|---|---|---|
| `iris.csv` | 150 | 4 | 3 | Anderson's Iris — sepal/petal length & width. |
| `iris_labeled.csv` | 150 | 5 | 3 | Iris with `class` (setosa / versicolor / virginica). |
| `wine.csv` | 178 | 13 | 3 | Chemical analysis of wines from 3 cultivars (Italy). |
| `wine_labeled.csv` | 178 | 14 | 3 | Wine with `class` (1/2/3). |
| `seeds.csv` | 210 | 7 | 3 | Geometric parameters of wheat kernels (Kama, Rosa, Canadian). |
| `seeds_labeled.csv` | 210 | 8 | 3 | Seeds with `variety`. |
| `glass.csv` | 214 | 9 | 6 | Oxide content of glass samples. Forensic use. |
| `glass_labeled.csv` | 214 | 10 | 6 | Glass with `type`. |

**Source:** [UCI Machine Learning Repository](https://archive.ics.uci.edu/) — CC BY 4.0

## Classic clustering benchmarks (feature vector data)

Used in the original AFC algorithm papers.

| File | n | m | True clusters | Source / description |
|---|---|---|---|---|
| `ruspini.csv` | 75 | 2 | 4 | Ruspini (1970) — 4 well-separated 2D groups, as distributed in the R package `cluster`. |
| `ruspini_labeled.csv` | 75 | 3 | 4 | Ruspini with `group` (1–4: the four groups of 20, 23, 17 and 15 objects in file order). |
| `sneath_sokal.csv` | 16 | 2 | 2 | Sneath & Sokal data used throughout Viattchenin (2013) for worked examples of D-AFC FIS generation. |
| `looney.csv` | 15 | 2 | 2 | Looney dataset (2D, integer coords). Used in D-AFC-TC examples. |
| `yang_wu.csv` | 16 | 2 | 2 | Yang & Wu dataset. Used in D-AFC-TC computational experiments (Viattchenin ИИ-2009). |
| `rehms.csv` | 45 | 2 | 3 | Rehms dataset (2D, real-valued, 3 natural groups). Used for D-AFC-TC outlier detection experiments. |

## Classic relational benchmarks (similarity matrix data)

These files contain an $n \times n$ fuzzy tolerance matrix and are the **direct input** of the algorithms, without any distance computation step: `mfuzzy cluster --matrix --input <file>`, the *Open matrix* command of the workbench, or `FuzzyToleranceMatrix.FromCsv` in the library.

| File | n | Clusters | Source |
|---|---|---|---|
| `windham_relational.csv` | 11 | 2 | Windham's relational similarity data (Viattchenin 2013, Chapter 2). |
| `tamura_5.csv` | 5 | 2 | Tamura's 5-object short example — minimal illustrative case. |
| `tamura_16.csv` | 16 | 3 families | Tamura, Higuchi and Tanaka's (1971) subjective similarity of 16 portraits from three families, as printed in Viattchenin (2013, Table 2.4). Used for D-PAFC and D-AFC(u). |
| `dave_sen_fruit.csv` | 7 | 2 | Dave & Sen fruit dataset (tolerance matrix over 7 fruit objects). |

## Recommended algorithm → dataset pairings

| Algorithm | Recommended datasets |
|---|---|
| D-AFC(c) | `windham_relational`, `tamura_5`, `dave_sen_fruit` |
| D-AFC(u) | `tamura_16`, `sneath_sokal` (build T from features) |
| D-PAFC | `windham_relational`, `tamura_16`, `sneath_sokal` (build T from features) |
| D-AFC-TC, D-AFC-TC(α) | `yang_wu`, `looney`, `iris`, `seeds` |
| D-PAFC-TC (outlier detection) | `rehms`, `iris` |
| D-AFC-TAGA, D-PAFC-TAGA | `wine`, `glass`, `seeds` |
| H-AFC-TC (hierarchical) | `iris`, `ruspini`, `seeds` |
| D-PAFC-DR (dimension reduction) | `wine` (13D), `glass` (9D) |

## Additional labeled benchmarks (added 2026-10-27)

All files below follow the same convention: `<name>.csv` holds numeric features only,
`<name>_labeled.csv` adds one trailing `class` column (or `species`). Secondary ground
truths are provided as separate `*_labeled.csv` files so that every labeled file has exactly
one label column.

### Real-world data

| File | n | m | Classes | Source / notes |
|---|---|---|---|---|
| `penguins.csv` / `penguins_labeled.csv` | 342 | 4 | 3 species (adelie 151, chinstrap 68, gentoo 123) | Palmer Archipelago penguins (Gorman et al. 2014; Horst et al. 2020), CC0. Bill length/depth, flipper length, body mass; 2 rows with missing measurements dropped. |
| `land_mines.csv` / `land_mines_labeled.csv` | 338 | 3 | 5 mine types (null, anti_tank, anti_personnel, booby_trapped_ap, m14_ap) | UCI id 763 (Yilmaz, Kahraman & Söyler 2018), CC BY 4.0. Features already scaled to [0,1]; `S` is a 6-level soil code. |
| `forest_fires.csv` / `forest_fires_labeled.csv` | 243 | 10 | 2 (fire 137, not_fire 106) | Algerian forest fires, UCI id 547 (Abid & Izeboudjen 2020), CC BY 4.0. Weather + FWI indices; day/month/year dropped; one malformed source row skipped. |
| `forest_fires_region_labeled.csv` | 243 | 10 | 2 regions (bejaia, sidi_bel_abbes) | Same rows, alternative ground truth. |
| `ecoli.csv` / `ecoli_labeled.csv` | 336 | 7 | 8 localisation sites (cp 143, im 77, pp 52, imU 35, om 20, omL 5, imL 2, imS 2) | UCI id 39 (Horton & Nakai 1996), CC BY 4.0. Three tiny classes make it a natural outlier / singleton-cluster benchmark. |
| `vertebral.csv` / `vertebral_labeled.csv` | 310 | 6 | 3 (normal 100, disk_hernia 60, spondylolisthesis 150) | UCI id 212 (da Mota, Barreto & Neto 2011), CC BY 4.0. Pelvic/lumbar biomechanical angles. |
| `vertebral_2c_labeled.csv` | 310 | 6 | 2 (normal 100, abnormal 210) | Same features, coarser ground truth. |

### Synthetic 2-D / 3-D shape benchmarks (Clustering Benchmarks suite v1.2.0)

Expert reference partitions (`labels0`) from M. Gagolewski's *Clustering Benchmarks*
(Gagolewski 2022, SoftwareX 20:101270; data: github.com/gagolews/clustering-data-v1, tag v1.2.0,
provided for research purposes — cite the suite and the original authors). Label `0` denotes noise.

| File | n | d | k | Battery / original author | Why it is here |
|---|---|---|---|---|---|
| `wut_x1`, `wut_x2` | 120 | 2 | 3 | WUT (E. Kaczorek) | Small, unequal cluster sizes 50/40/30 |
| `wut_x3` | 185 | 2 | 4 | WUT | Four unequal groups |
| `wut_z1` | 192 | 2 | 3 | WUT | Equal groups 64/64/64 |
| `wut_mk1` | 300 | 2 | 3 | WUT | Three equal Gaussian-like groups |
| `fcps_hepta` | 212 | 3 | 7 | FCPS (Ultsch) | Seven compact 3-D clusters |
| `graves_dense` | 200 | 2 | 2 | Graves & Pedrycz | Dense vs. sparse cluster |
| `graves_line` | 250 | 2 | 2 | Graves & Pedrycz | Elongated structure, 200/50 |
| `graves_zigzag` | 250 | 2 | 3 | Graves & Pedrycz | Non-convex chain |
| `graves_zigzag_outliers` | 280 | 2 | 3 + noise | Graves & Pedrycz | 30 outliers labeled `0` — outlier detection |
| `sipu_flame` | 240 | 2 | 2 | SIPU (Fu & Medico) | Touching non-convex clusters |
| `sipu_pathbased` | 300 | 2 | 3 | SIPU (Chang & Yeung) | Two blobs inside a ring path |
| `sipu_spiral` | 312 | 2 | 3 | SIPU (Chang & Yeung) | Three interleaved spirals — single-linkage-type structure where centroid methods fail |

### Suggested pairings for the new data

| Algorithm | Datasets |
|---|---|
| D-AFC(c) with partial separation ($w>0$) | `penguins` (adelie/chinstrap overlap), `vertebral` |
| D-AFC-TC, H-AFC-TC | `sipu_spiral`, `sipu_pathbased`, `graves_zigzag`, `fcps_hepta`, `penguins` |
| D-PAFC-TC (outliers) | `ecoli` (classes of size 2), `graves_zigzag_outliers`, `forest_fires` |
| D-AFC-TAGA / D-PAFC-TAGA | `land_mines`, `wut_x3`, `graves_dense` |
| Validity measures (choice of $c$) | `vertebral` (3 vs 2), `forest_fires` (class vs region) |
