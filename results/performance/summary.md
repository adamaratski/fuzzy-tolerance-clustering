# Measurements of the computational chapter

Median wall-clock time; `cores` is the number of logical cores the process could use.

## Transitive closure

| Variant | cores | n = 150 | n = 500 | n = 1000 | n = 2000 |
|---|---|---|---|---|---|
| Floyd–Warshall, scalar, 1 thread | 24 | 2.11 ms | 75 ms | 591 ms | 5.24 s |
| Floyd–Warshall, SIMD 128-bit, 1 thread | 24 | 1.43 ms | 53 ms | 417 ms | 4.27 s |
| Floyd–Warshall, SIMD 256-bit, 1 thread | 24 | 0.84 ms | 29 ms | 230 ms | 3.00 s |
| Floyd–Warshall, SIMD 512-bit, 1 thread | 24 | 0.52 ms | 16 ms | 140 ms | 2.39 s |
| Floyd–Warshall, scalar, parallel | 24 | 1.75 ms | 17 ms | 89 ms | 585 ms |
| Floyd–Warshall, SIMD 256-bit, parallel (former library) | 24 | 1.50 ms | 17 ms | 151 ms | 935 ms |
| Spanning tree, 1 thread | 24 | 0.18 ms | 2.76 ms | 13 ms | 48 ms |
| Spanning tree, parallel (library) | 24 | 0.11 ms | 0.99 ms | 3.99 ms | 14 ms |
| Floyd–Warshall, scalar, parallel | 1 | 3.94 ms | 85 ms | 627 ms | 5.49 s |
| Floyd–Warshall, SIMD 256-bit, parallel (former library) | 1 | 2.33 ms | 35 ms | 237 ms | 3.34 s |
| Spanning tree, parallel (library) | 1 | 0.22 ms | 2.99 ms | 14 ms | 54 ms |

## D-AFC(3) search on Iris

| Variant | data | n | cores | time | note |
|---|---|---|---|---|---|
| Bit-set engine + pre-filter + exact-cover bound (library) | iris | 150 | 24 | 42 ms | 3447 of 7887 levels searched, 2,910,078 nodes |
| Bit-set engine + pre-filter | iris | 150 | 24 | 730 ms | 3916 of 7887 levels searched, 22,559,412 nodes |
| Bit-set engine + early stopping (heuristic) | iris | 150 | 24 | 48 ms | best F1 = 0.323028 at α = 0.76318 vs exhaustive F1 = 0.367206 at α = 0.73864 |
| Pre-filter + plain enumeration | iris | 150 | 1 | 478 s | 3916 of 7887 levels searched, 2,158,890,800 combinations |
| Plain enumeration of all combinations | iris | 150 | 1 | 897 s (extrapolated) | 113.8 ms per level on 24 evenly spaced levels × 7887 levels |
| Bit-set engine + pre-filter + exact-cover bound (library) | iris | 150 | 1 | 402 ms | 3447 of 7887 levels searched, 2,910,078 nodes |
| Bit-set engine + pre-filter | iris | 150 | 1 | 7.54 s | 3916 of 7887 levels searched, 22,559,412 nodes |

## Exact-cover bound, D-AFC(3)

| Variant | data | n | cores | time | note |
|---|---|---|---|---|---|
| With the exact-cover bound (library) | seeds | 210 | 24 | 20 s | w = 21, 1,113,113,981 nodes |
| Without the exact-cover bound | seeds | 210 | 24 | 123 s | w = 21, 2,879,170,425 nodes |
| With the exact-cover bound (library) | wine | 178 | 24 | 24 s | w = 42, 1,498,401,149 nodes |
| Without the exact-cover bound | wine | 178 | 24 | 38 s | w = 42, 2,705,395,420 nodes |
| With the exact-cover bound (library) | penguins | 342 | 24 | 159 s | w = 17, 6,050,208,802 nodes |
| Without the exact-cover bound | penguins | 342 | 24 | 802 s | w = 17, 16,630,892,118 nodes |

## H-AFC-TC on Iris

| Variant | data | n | cores | time | note |
|---|---|---|---|---|---|
| Best typical point per class (library) | iris | 150 | 1 | 10 ms | 75 levels |
| Best typical point per class (library) | iris | 150 | 24 | 9.82 ms | 75 levels |
| Combination search per level (budget 5·10⁸) | iris | 150 | 24 | 53 s | 76 levels |

## Pipeline stages

| Variant | data | n | cores | time | note |
|---|---|---|---|---|---|
| Tolerance matrix T | iris | 150 | 1 | 0.76 ms |  |
| Transitive closure (library) | iris | 150 | 1 | 0.21 ms |  |
| Distinct α-levels of T | iris | 150 | 1 | 0.45 ms | 7887 levels |
| Tolerance matrix T | blobs2000 | 2000 | 1 | 170 ms |  |
| Transitive closure (library) | blobs2000 | 2000 | 1 | 60 ms |  |
| Distinct α-levels of T | blobs2000 | 2000 | 1 | 126 ms | 1998990 levels |
| Tolerance matrix T | iris | 150 | 24 | 0.71 ms |  |
| Transitive closure (library) | iris | 150 | 24 | 0.12 ms |  |
| Distinct α-levels of T | iris | 150 | 24 | 0.45 ms | 7887 levels |
| Tolerance matrix T | blobs2000 | 2000 | 24 | 152 ms |  |
| Transitive closure (library) | blobs2000 | 2000 | 24 | 14 ms |  |
| Distinct α-levels of T | blobs2000 | 2000 | 24 | 120 ms | 1998990 levels |
