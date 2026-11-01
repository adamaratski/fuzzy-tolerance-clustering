%DEMO_IRIS  Fuzzy clustering of the Iris dataset using the VFC toolbox.
%
%   Demonstrates D-AFC-TC and D-AFC (fixed alpha) on the classic Iris
%   dataset (n=150 objects, d=4 features, 3 classes).
%
%   Requires:
%     - VFC toolbox on the MATLAB path (addpath to the +vfc parent folder)
%     - iris_labeled.csv in the datasets/ directory
%
%   Usage:
%     run('demo_iris.m')
%
%   Expected output (approximate):
%     D-AFC-TC best result:  c=3, alpha≈0.70, Purity≈0.69, ARI≈0.56, NMI≈0.71
%
%   Reference:
%     Fisher, R.A. The use of multiple measurements in taxonomic problems.
%     Ann. Eugenics, 7(2):179-188, 1936.
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering. Springer, 2013 (StudFuzz 297).

clear; clc;

%% 0. Setup path
script_dir  = fileparts(mfilename('fullpath'));
toolbox_dir = fullfile(script_dir, '..');  % parent of examples/
addpath(toolbox_dir);

datasets_dir = fullfile(toolbox_dir, '..', '..', 'datasets');  % repository root

%% 1. Load Iris data
fprintf('Loading Iris dataset...\n');

csv_path = fullfile(datasets_dir, 'iris_labeled.csv');
raw = readtable(csv_path, 'VariableNamingRule', 'preserve');

% Last column is the class label (string or numeric)
X       = table2array(raw(:, 1:end-1));   % 150 × 4 feature matrix
y_str   = table2array(raw(:, end));       % class labels

% Encode string labels as integers
if iscell(y_str) || isstring(y_str)
    [classes, ~, y] = unique(y_str);
else
    y = y_str;
    classes = unique(y);
end

n = size(X, 1);  d = size(X, 2);
fprintf('Dataset: n=%d objects, d=%d features, %d classes (%s)\n', ...
    n, d, numel(classes), strjoin(string(classes), ', '));

%% 2. Build fuzzy tolerance matrix
fprintf('\nBuilding tolerance matrix T (Euclidean, MinMax normalization)...\n');
T = vfc.build_tolerance_matrix(X, 'Distance', 'euclidean', 'Normalization', 'minmax');
fprintf('T: %d × %d,  values in [%.4f, %.4f]\n', ...
    size(T,1), size(T,2), min(T(:)), max(T(T<1)));

%% 3. Transitive closure
fprintf('\nComputing max-min transitive closure T~...\n');
Tc = vfc.max_min_closure(T);
levels = vfc.alpha_levels(Tc);
fprintf('T~: %d distinct alpha levels\n', numel(levels));
fprintf('Alpha levels: %s\n', mat2str(round(levels,4)'));

%% 4. Leap heuristic
alpha_star = vfc.alpha_leap(Tc);
fprintf('\nLeap heuristic → alpha* = %.4f\n', alpha_star);

%% 5. D-AFC at alpha* = 0.7, c = 3  (fast, single-level search)
fprintf('\n=== D-AFC (alpha=0.7, c=3) ===\n');
opts_dafc          = struct();
opts_dafc.criterion = 'F1';
opts_dafc.verbose   = true;

r_dafc = vfc.dafc(Tc, 3, 0.7, opts_dafc);
vfc.print_results(r_dafc, 'MaxRows', 5, 'Labels', y);

%% 6. D-AFC-TC (automatic c, leap alpha)
fprintf('\n=== D-AFC-TC (automatic c, leap alpha*=%.4f) ===\n', alpha_star);
opts_tc             = struct();
opts_tc.criterion   = 'F1';
opts_tc.verbose     = true;

r_tc = vfc.dafc_tc(X, opts_tc);

if ~isempty(r_tc)
    vfc.print_results(r_tc, 'MaxRows', 10, 'Labels', y);

    %% 7. Visualize best result (first 2 features)
    best = r_tc(1);
    figure('Name', 'D-AFC-TC: Iris clustering', 'NumberTitle', 'off');

    subplot(1,2,1);
    gscatter(X(:,1), X(:,2), best.labels, 'rbg', 'o+x', 8);
    xlabel('Sepal length');  ylabel('Sepal width');
    title(sprintf('D-AFC-TC prediction  (c=%d, \\alpha=%.4f)', best.c, best.alpha));
    legend(arrayfun(@(k) sprintf('Cluster %d', k), 1:best.c, 'UniformOutput',false));
    grid on;

    subplot(1,2,2);
    gscatter(X(:,1), X(:,2), y, 'rbg', 'o+x', 8);
    xlabel('Sepal length');  ylabel('Sepal width');
    title('Ground truth classes');
    legend(string(classes));
    grid on;
else
    fprintf('No valid allotments found by D-AFC-TC.\n');
end

%% 8. Compare distances
fprintf('\n=== Distance comparison (alpha=0.7, c=3) ===\n');
distances = {'euclidean', 'manhattan', 'chebyshev', 'cosine'};

fprintf('%-12s  %-8s  %-8s  %-8s  %-8s\n', 'Distance', '#Found', 'Score', 'Purity', 'ARI');
fprintf('%s\n', repmat('-', 1, 52));

for di = 1 : numel(distances)
    Ti   = vfc.build_tolerance_matrix(X, 'Distance', distances{di});
    Tci  = vfc.max_min_closure(Ti);
    ri   = vfc.dafc(Tci, 3, 0.7);
    if ~isempty(ri)
        labs = ri(1).labels;
        pur  = sum(arrayfun(@(cl) max(histc(y(labs==cl), unique(y))), unique(labs))) / n;
        [~,~,pred_id] = unique(labs);
        C    = confusionmat(y, pred_id);
        sr   = sum(C,2);  sc = sum(C,1);
        a    = sum(C(:).^2-C(:))/2;  b = sum(sr.^2-sr)/2;  cc = sum(sc.^2-sc)/2;
        dn   = n*(n-1)/2;  exp_ = b*cc/dn;
        ari  = (a-exp_)/((b+cc)/2-exp_+eps);
        fprintf('%-12s  %-8d  %-8.4f  %-8.4f  %-8.4f\n', ...
            distances{di}, numel(ri), ri(1).score, pur, ari);
    else
        fprintf('%-12s  %-8d  %-8s  %-8s  %-8s\n', distances{di}, 0, '-', '-', '-');
    end
end
