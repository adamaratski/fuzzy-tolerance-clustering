% Viattchenin Fuzzy Clustering Toolbox (VFC)
% Version 1.0  —  June 2026
%
% Implementation of direct fuzzy clustering algorithms based on fuzzy
% tolerance relations, as described in:
%
%   Viattchenin, D.A. "A Heuristic Approach to Possibilistic Clustering".
%   Studies in Fuzziness and Soft Computing, vol. 297. Springer, 2013.
%
% and in the accompanying book
%
%   Damaratski, A. "Fuzzy Tolerance Clustering: Theory, Algorithms, and
%   Implementations", 2026.
%
% =========================================================================
% USAGE
%   Add the folder containing +vfc/ to the MATLAB path:
%
%     addpath(fullfile('<repository root>', 'src', 'matlab'));
%
%   Then call functions as:
%
%     T = vfc.build_tolerance_matrix(X);
%     r = vfc.dafc(T, 3, 0.7);
%
% =========================================================================
% ALGORITHMS
%
%   vfc.build_tolerance_matrix   Build fuzzy tolerance matrix T from raw data
%   vfc.max_min_closure          Max-min transitive closure T~ of T
%   vfc.alpha_levels             Extract all distinct alpha-cut levels
%   vfc.alpha_leap               Select alpha* via leap heuristic (maximal leap)
%
%   vfc.dafc          D-AFC    : Direct AFC, fixed c and alpha
%   vfc.dpafc         D-PAFC   : Direct PAFC, automatic c
%   vfc.dafc_tc       D-AFC-TC : D-AFC with transitive closure (prototype)
%   vfc.dpafc_tc      D-PAFC-TC: D-PAFC on transitive closure (prototype)
%
%   vfc.print_results            Display clustering results
%
% =========================================================================
% ALGORITHM OVERVIEW
%
%  D-AFC (Relational, requires T)
%  ─────────────────────────────
%  Given T (n×n fuzzy tolerance matrix), cluster count c, and threshold α:
%    1. For each combination of c objects as candidate centers:
%       a. Compute alpha-neighborhood:  C_l = { j : T(c_l, j) >= alpha }
%       b. Validate cluster power:      |C_l| >= min_power
%       c. Validate coverage:           C_1 ∪ ... ∪ C_c = {1,...,n}
%    2. Score valid allotments with F1 or F2 criterion.
%    3. Return top-k results sorted by score.
%
%  D-PAFC (Relational, requires T)
%  ───────────────────────────────
%  Iterates c = 2, 3, ... and all distinct alpha levels.
%  Stops when two consecutive values of c yield no valid allotments.
%
%  D-AFC-TC / D-PAFC-TC (Prototype, requires raw data X)
%  ──────────────────────────────────────────────────────
%  1. Normalize X and compute pairwise distances.
%  2. Build T(i,j) = 1 - dist(x_i, x_j).
%  3. Compute max-min transitive closure T~.
%  4. D-AFC-TC : select alpha* via leap heuristic, run D-AFC at alpha*.
%     D-PAFC-TC: run D-PAFC on T~ (all alpha levels).
%
% =========================================================================
% QUALITY CRITERIA
%
%  F1 = sum_l [ mean_{j in C_l} T(c_l, j) ] - alpha * c   (maximize)
%  F2 = mean_l [ mean_{j in C_l} T(c_l, j) ]              (maximize)
%
% =========================================================================
% EXAMPLES
%   See examples/ directory:
%     demo_iris.m        Iris dataset (n=150, d=4, 3 classes)
%     demo_synthetic.m   Synthetic 2-D datasets
%
% =========================================================================
% AUTHORS
%   Dmitry Viattchenin    — original algorithms and theoretical framework
%   Aliaksandr Damaratski — MATLAB implementation, 2026
