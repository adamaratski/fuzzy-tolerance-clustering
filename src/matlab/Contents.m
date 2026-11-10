% VFC toolbox: heuristic possibilistic clustering on fuzzy tolerance relations
% Version 1.1
%
% Plain MATLAB / GNU Octave functions for the direct clustering algorithms
% described in
%
%   Viattchenin, D.A. "A Heuristic Approach to Possibilistic Clustering".
%   Studies in Fuzziness and Soft Computing, vol. 297. Springer, 2013.
%
% and in the accompanying book
%
%   Damaratski, A. "Fuzzy Tolerance Clustering: Theory, Algorithms, and
%   Implementations", 2026.
%
% The functions follow the statements of the algorithms and favour clarity
% over speed: they are meant for small data and for checking results by hand.
% The .NET library of the same repository is the complete and fast
% implementation.
%
% Usage
%   addpath(fullfile('<repository root>', 'src', 'matlab'));
%   T = vfc.build_tolerance_matrix(X, 'Distance', 'squared-euclidean', 'Normalization', 'max');
%   r = vfc.dpafc(T);
%   vfc.print_results(r);
%
% Tolerance relations
%   vfc.build_tolerance_matrix   fuzzy tolerance T from attribute data
%   vfc.snap_levels              merge levels that differ by rounding noise
%   vfc.max_min_closure          max-min transitive closure of T
%   vfc.alpha_levels             distinct levels of a relation
%   vfc.alpha_leap               threshold by the leap heuristic
%
% Relational algorithms (input: a fuzzy tolerance matrix)
%   vfc.dafc                     allotments among c clusters at one level
%   vfc.dafc_c                   D-AFC(c): all levels, automatic relaxation of the separation
%   vfc.dpafc                    D-PAFC: principal allotment, the number of clusters is found
%                                (with opts.max_power = u: one pass of D-AFC(u))
%
% Prototype-based and hierarchical algorithms (input: attribute data)
%   vfc.dafc_tc                  D-AFC-TC: closure and leap heuristic
%   vfc.dpafc_tc                 D-PAFC-TC: first splitting level of the closure
%   vfc.hafc_tc                  H-AFC-TC: the allotment of every level of the closure
%   vfc.closure_allotment        best allotment of one level of a closure
%
% Output
%   vfc.print_results            table of allotments, external indices for labelled data
%
% Not in this toolbox (see the .NET library): transitive approximations (TAGA),
% D-AFC(u) (only a single pass for a fixed bound: vfc.dpafc with opts.max_power),
% validity measures, generation of
% fuzzy inference systems.
%
% Tests:    tests/run_tests.m   (published examples; prints "all tests passed")
% Example:  examples/demo.m
%
% Algorithms: D. A. Viattchenin. Implementation: A. Damaratski.
