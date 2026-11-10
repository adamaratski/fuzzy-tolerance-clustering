%DEMO  The toolbox on the Sneath-Sokal data and on Anderson's Iris data.
%
%   Run from anywhere:  run('src/matlab/examples/demo.m')
%   Works in MATLAB and in GNU Octave; needs no toolboxes.

here = fileparts(mfilename('fullpath'));
addpath(fullfile(here, '..'));
datasets = fullfile(here, '..', '..', '..', 'datasets');

%% Sneath-Sokal data: 16 objects, the setting of the monograph
X = dlmread(fullfile(datasets, 'sneath_sokal.csv'), ',', 1, 0);
T = vfc.build_tolerance_matrix(X, 'Distance', 'squared-euclidean', 'Normalization', 'max');

fprintf('\nD-PAFC (principal allotment) on the Sneath-Sokal data\n');
vfc.print_results(vfc.dpafc(T), 'MaxRows', 3);

fprintf('D-AFC(3): three clusters, all levels\n');
vfc.print_results(vfc.dafc_c(T, 3), 'MaxRows', 3);

options = struct('distance', 'squared-euclidean', 'normalization', 'max');
fprintf('D-AFC-TC: transitive closure and leap heuristic\n');
vfc.print_results(vfc.dafc_tc(X, options));

fprintf('H-AFC-TC: the hierarchy\n');
levels = vfc.hafc_tc(X, options);
for k = 1 : numel(levels)
    fprintf('  alpha = %.5f   %2d clusters   sizes %s\n', levels(k).alpha, levels(k).c, ...
            mat2str(cellfun(@numel, levels(k).clusters)));
end

%% Iris data: 150 objects, closure-based algorithms (the relational search on
%% this many objects is slow in an interpreted language - use the .NET tool)
iris = dlmread(fullfile(datasets, 'iris.csv'), ',', 1, 0);
fprintf('\nD-AFC-TC on the Iris data (accuracy 0.0001)\n');
options.accuracy = 0.0001;
vfc.print_results(vfc.dafc_tc(iris, options));
