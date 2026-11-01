# Measurements of the computational chapter

Median wall-clock time; `cores` is the number of logical cores the process could use.

## Transitive closure

| Variant | cores | n = 150 | n = 500 | n = 1000 | n = 2000 |
|---|---|---|---|---|---|
| Floyd–Warshall, scalar, 1 thread | 24 | 2.19 ms | 74 ms | 582 ms | 5.19 s |
| Floyd–Warshall, SIMD 128-bit, 1 thread | 24 | 1.42 ms | 52 ms | 410 ms | 4.13 s |
| Floyd–Warshall, SIMD 256-bit, 1 thread | 24 | 0.82 ms | 29 ms | 226 ms | 3.00 s |
| Floyd–Warshall, SIMD 512-bit, 1 thread | 24 | 0.54 ms | 18 ms | 131 ms | 2.46 s |
| Floyd–Warshall, scalar, parallel | 24 | 1.72 ms | 17 ms | 101 ms | 554 ms |
| Floyd–Warshall, SIMD 256-bit, parallel (former library) | 24 | 1.38 ms | 17 ms | 160 ms | 894 ms |
| Spanning tree, 1 thread | 24 | 0.19 ms | 2.75 ms | 13 ms | 48 ms |
| Spanning tree, parallel (library) | 24 | 0.11 ms | 0.89 ms | 3.78 ms | 13 ms |
| Floyd–Warshall, scalar, parallel | 1 | 3.56 ms | 82 ms | 633 ms | 5.21 s |
| Floyd–Warshall, SIMD 256-bit, parallel (former library) | 1 | 2.24 ms | 34 ms | 237 ms | 2.69 s |
| Spanning tree, parallel (library) | 1 | 0.19 ms | 2.83 ms | 14 ms | 51 ms |

## D-AFC(3) search on Iris

| Variant | data | n | cores | time | note |
|---|---|---|---|---|---|
| Bit-set engine + pre-filter (library) | iris | 150 | 24 | 718 ms | 8653 levels |
| Pre-filter + plain enumeration | iris | 150 | 1 | 495 s | 4300 of 8653 levels searched, 2,370,590,000 combinations |
| Plain enumeration of all combinations | iris | 150 | 1 | 1,011 s (extrapolated) | 116.9 ms per level on 24 evenly spaced levels × 8653 levels |
| Bit-set engine + pre-filter (library) | iris | 150 | 1 | 7.77 s | 8653 levels |

## H-AFC-TC on Iris

| Variant | data | n | cores | time | note |
|---|---|---|---|---|---|
| Best typical point per class (library) | iris | 150 | 24 | 12 ms | 93 levels |
| Combination search per level (budget 5·10⁸) | iris | 150 | 24 | 59 s | 94 levels |
| Best typical point per class (library) | iris | 150 | 1 | 10 ms | 93 levels |

## Pipeline stages

| Variant | data | n | cores | time | note |
|---|---|---|---|---|---|
| Tolerance matrix T | iris | 150 | 24 | 0.06 ms |  |
| Transitive closure (library) | iris | 150 | 24 | 0.12 ms |  |
| Distinct α-levels of T | iris | 150 | 24 | 1.01 ms | 8653 levels |
| Tolerance matrix T | blobs2000 | 2000 | 24 | 4.88 ms |  |
| Transitive closure (library) | blobs2000 | 2000 | 24 | 16 ms |  |
| Distinct α-levels of T | blobs2000 | 2000 | 24 | 1.25 s | 1999000 levels |
| Tolerance matrix T | iris | 150 | 1 | 0.10 ms |  |
| Transitive closure (library) | iris | 150 | 1 | 0.21 ms |  |
| Distinct α-levels of T | iris | 150 | 1 | 1.01 ms | 8653 levels |
| Tolerance matrix T | blobs2000 | 2000 | 1 | 15 ms |  |
| Transitive closure (library) | blobs2000 | 2000 | 1 | 54 ms |  |
| Distinct α-levels of T | blobs2000 | 2000 | 1 | 1.31 s | 1999000 levels |
