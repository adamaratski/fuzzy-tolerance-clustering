%RUN_TESTS  Checks the toolbox against the published examples.
%
%   Run from the repository root or from this folder (MATLAB or GNU Octave):
%       run('src/matlab/tests/run_tests.m')
%   The script stops with an error at the first failed check and prints
%   "all tests passed" otherwise.

here = fileparts(mfilename('fullpath'));
addpath(fullfile(here, '..'));

tol = 1e-9;
check = @(condition, message) assert(condition, ['FAILED: ' message]);

%% 1. Five-object example of the book (Tamura, Higuchi, Tanaka)
T5 = [1 .8 0 .1 .2; .8 1 .4 0 .9; 0 .4 1 0 0; .1 0 0 1 .5; .2 .9 0 .5 1];

[r, w] = vfc.dafc_c(T5, 3);
check(w == 0, 'five objects: D-AFC(3) needs no intersections');
check(numel(r) == 2, 'five objects: two allotments among three clusters');
check(abs(r(1).alpha - 0.5) < tol && isequal(r(1).centers, [1 3 4]), 'five objects: best allotment');
check(abs(r(1).score - 1.15) < tol, 'five objects: F1 = 1.15');
check(abs(r(2).alpha - 0.8) < tol && isequal(r(2).centers, [2 3 4]) && abs(r(2).score - 0.5) < tol, ...
      'five objects: second allotment');

Tc5 = vfc.max_min_closure(T5);
expected = [1 .8 .4 .5 .8; .8 1 .4 .5 .9; .4 .4 1 .4 .4; .5 .5 .4 1 .5; .8 .9 .4 .5 1];
check(max(abs(Tc5(:) - expected(:))) < tol, 'five objects: transitive closure');
check(abs(vfc.alpha_leap(Tc5) - 0.8) < tol, 'five objects: leap heuristic selects 0.8');

%% 2. Separation: three objects pairwise tolerant at 0.9 need one shared object
T3 = [1 .9 .3; .9 1 .9; .3 .9 1];
check(isempty(vfc.dafc(T3, 2, 0.9)), 'three objects: no fully separated allotment');
[r, w] = vfc.dafc_c(T3, 2);
check(~isempty(r) && w == 1 && r(1).intersections == 1, 'three objects: one object in the intersection');

%% 3. Sneath-Sokal data, the setting of the monograph
X = [0 4; 0 3; 1 5; 2 4; 3 3; 2 2; 2 1; 1 0; 5 5; 6 5; 7 6; 5 3; 7 3; 6 2; 6 1; 8 1];
T = vfc.build_tolerance_matrix(X, 'Distance', 'squared-euclidean', 'Normalization', 'max');
check(abs(T(1, 2) - (1 - (1/6)^2 / 2)) < tol, 'Sneath-Sokal: tolerance of objects 1 and 2');

r = vfc.dpafc(T);
check(~isempty(r) && r(1).c == 4, 'Sneath-Sokal: D-PAFC finds four clusters');
check(abs(r(1).alpha - 263/288) < tol, 'Sneath-Sokal: D-PAFC threshold 0.91319');
check(isequal(r(1).centers, [3 8 11 15]), 'Sneath-Sokal: D-PAFC typical points');
check(isequal(cellfun(@numel, r(1).clusters), [5 3 3 5]), 'Sneath-Sokal: D-PAFC cluster sizes');
check(abs(r(1).score - 0.223495) < 1e-6, 'Sneath-Sokal: D-PAFC F1');

options = struct('distance', 'squared-euclidean', 'normalization', 'max');
r = vfc.dafc_tc(X, options);
check(abs(r.alpha - 0.96875) < tol && isequal(sort(cellfun(@numel, r.clusters)), [3 13]), ...
      'Sneath-Sokal: D-AFC-TC gives 13 and 3 objects at 0.96875');
check(isequal(r.centers, [6 10]), 'Sneath-Sokal: D-AFC-TC typical points');

r = vfc.dpafc_tc(X, options);
check(abs(r.alpha - 0.96875) < tol && r.c == 2, 'Sneath-Sokal: D-PAFC-TC');

levels = vfc.hafc_tc(X, options);
counts = arrayfun(@(level) level.c, levels);
check(numel(levels) >= 3 && all(diff(counts) >= 0), 'Sneath-Sokal: the hierarchy is refined level by level');

%% 4. Accuracy threshold and merging of levels
Te = vfc.build_tolerance_matrix(X, 'Distance', 'squared-euclidean', 'Normalization', 'max', 'Accuracy', 0.01);
values = Te(triu(true(16), 1));
check(max(abs(values * 100 - round(values * 100))) < 1e-9, 'accuracy: values are multiples of 0.01');
levels = vfc.alpha_levels(T);
check(min(abs(diff(levels))) > 1e-9, 'levels that differ by rounding noise are merged');

%% 5. Tamura's portraits (relational data): D-PAFC
triangle = { 1, [0 1], [0 0 1], [0 0 .4 1], [0 .8 0 0 1], [.5 0 .2 .2 0 1], [0 .8 0 0 .4 0 1], ...
          [.4 .2 .2 .5 0 .8 0 1], [0 .4 0 .8 .4 .2 .4 0 1], [0 0 .2 .2 0 0 .2 0 .2 1], ...
          [0 .5 .2 .2 0 0 .8 0 .4 .2 1], [0 0 .2 .8 0 0 0 0 .4 .8 0 1], [.8 0 .2 .4 0 .4 0 .4 0 0 0 0 1], ...
          [0 .8 0 .2 .4 0 .8 0 .2 .2 .6 0 0 1], [0 0 .4 .8 0 .2 0 0 .2 0 0 .2 .2 0 1], ...
          [.6 0 0 .2 .2 .8 0 .4 0 0 0 0 .4 .2 0 1] };
Tp = eye(16);
for i = 1 : 16
    Tp(i, 1:i) = triangle{i};
    Tp(1:i, i) = triangle{i}';
end
r = vfc.dpafc(Tp);
check(r(1).c == 4 && abs(r(1).alpha - 0.4) < tol && isequal(r(1).centers, [2 6 10 15]), 'Tamura: D-PAFC');
check(abs(r(1).score - 1.45) < tol, 'Tamura: F1 = 1.45');

r = vfc.dpafc(Tp, struct('max_power', 5, 'max_intersections', 1));
check(~isempty(r) && isequal(r(1).centers, [6 12 14 15]) && abs(r(1).score - 1.303333333) < 1e-6, ...
      'Tamura: clusters of at most five objects with one shared object');

fprintf('all tests passed\n');
