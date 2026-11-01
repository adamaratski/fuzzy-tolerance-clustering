%DEMO_SYNTHETIC  Fuzzy clustering of small 2-D benchmark datasets using the VFC toolbox.
%
%   Demonstrates the closure-based algorithms on 2-D data where the correct
%   clustering is visually obvious, making it easy to verify results.
%
%   Datasets used (all in the repository's datasets/ folder):
%     looney.csv        —  15-point example, 2 groups
%     wut_z1.csv        —  3 equal groups of 64 objects
%     graves_dense.csv  —  a dense and a sparse cluster, n=200
%     ruspini.csv       —  4 well-separated groups, n=75
%
%   Usage:
%     run('demo_synthetic.m')

clear; clc;

%% Setup
script_dir   = fileparts(mfilename('fullpath'));
toolbox_dir  = fullfile(script_dir, '..');
addpath(toolbox_dir);
datasets_dir = fullfile(toolbox_dir, '..', '..', 'datasets');  % repository root

%% =========================================================================
%% PART 1: Looney 15-point example
%% =========================================================================
fprintf('=== Part 1: Looney 15-point example ===\n\n');

csv_path = fullfile(datasets_dir, 'looney.csv');
if isfile(csv_path)
    data = readmatrix(csv_path);
    X15  = data(:, 1:end);   % use all columns as features

    T15 = vfc.build_tolerance_matrix(X15);
    fprintf('Tolerance matrix T (15×15):\n');
    disp(round(T15, 3));

    Tc15   = vfc.max_min_closure(T15);
    levels = vfc.alpha_levels(Tc15);
    a_star = vfc.alpha_leap(Tc15);
    fprintf('Alpha levels in T~: %s\n', mat2str(round(levels,4)'));
    fprintf('Leap heuristic alpha* = %.4f\n\n', a_star);

    r15 = vfc.dafc_tc(X15, struct('verbose', true, 'criterion', 'F1'));
    vfc.print_results(r15, 'MaxRows', 5);
else
    fprintf('File not found: %s\n', csv_path);
end

%% =========================================================================
%% PART 2: WUT z1 — three equal groups
%% =========================================================================
fprintf('\n=== Part 2: WUT z1, three equal groups (n=192) ===\n\n');

csv3 = fullfile(datasets_dir, 'wut_z1.csv');
if isfile(csv3)
    raw3 = readmatrix(csv3);
    % Assume last column is label if it has few distinct values
    if numel(unique(raw3(:,end))) <= 10
        X3 = raw3(:,1:end-1);
        y3 = raw3(:,end);
    else
        X3 = raw3;
        y3 = [];
    end

    fprintf('Running D-PAFC-TC (all levels, auto c)...\n');
    opts3           = struct();
    opts3.verbose   = true;
    opts3.criterion = 'F1';
    r3 = vfc.dpafc_tc(X3, opts3);

    if ~isempty(r3)
        vfc.print_results(r3(1:min(5,end)), 'MaxRows', 5, 'Labels', y3);
        plot_clustering(X3, r3(1), 'D-PAFC-TC: WUT z1', y3);
    end
else
    fprintf('File not found, generating synthetic data...\n');
    rng(42);
    X3 = [randn(50,2)*0.6 + [0  0];
          randn(50,2)*0.6 + [5  0];
          randn(50,2)*0.6 + [2.5 4]];
    y3 = [ones(50,1); 2*ones(50,1); 3*ones(50,1)];

    r3 = vfc.dpafc_tc(X3, struct('verbose', true));
    if ~isempty(r3)
        vfc.print_results(r3(1:min(5,end)), 'MaxRows', 5, 'Labels', y3);
        plot_clustering(X3, r3(1), 'D-PAFC-TC: Generated 3-cluster data', y3);
    end
end

%% =========================================================================
%% PART 3: Dense and sparse cluster — compare F1 vs F2 criterion
%% =========================================================================
fprintf('\n=== Part 3: Graves dense/sparse clusters — F1 vs F2 criterion ===\n\n');

csv_ov = fullfile(datasets_dir, 'graves_dense.csv');
if isfile(csv_ov)
    raw_ov = readmatrix(csv_ov);
    if numel(unique(raw_ov(:,end))) <= 10
        X_ov = raw_ov(:,1:end-1);
        y_ov = raw_ov(:,end);
    else
        X_ov = raw_ov;
        y_ov = [];
    end
else
    fprintf('Generating overlapping data...\n');
    rng(7);
    X_ov = [randn(100,2)*1.2 + [0 0];
            randn(100,2)*1.2 + [3 0]];
    y_ov = [ones(100,1); 2*ones(100,1)];
end

T_ov  = vfc.build_tolerance_matrix(X_ov);
Tc_ov = vfc.max_min_closure(T_ov);
a_ov  = vfc.alpha_leap(Tc_ov);

fprintf('alpha* = %.4f\n', a_ov);

opts_f1           = struct('criterion', 'F1', 'max_results', 5);
opts_f2           = struct('criterion', 'F2', 'max_results', 5);

r_f1 = vfc.dafc(Tc_ov, 2, a_ov, opts_f1);
r_f2 = vfc.dafc(Tc_ov, 2, a_ov, opts_f2);

fprintf('\nF1 criterion results:\n');
vfc.print_results(r_f1, 'Labels', y_ov);

fprintf('\nF2 criterion results:\n');
vfc.print_results(r_f2, 'Labels', y_ov);

%% =========================================================================
%% PART 4: Ruspini dataset — classic benchmark
%% =========================================================================
fprintf('\n=== Part 4: Ruspini dataset ===\n\n');

csv_r = fullfile(datasets_dir, 'ruspini.csv');
if isfile(csv_r)
    X_r = readmatrix(csv_r);
    if size(X_r, 2) > 2 && numel(unique(X_r(:,end))) <= 10
        y_r = X_r(:,end);
        X_r = X_r(:,1:end-1);
    else
        y_r = [];
    end

    opts_r = struct('verbose', true, 'criterion', 'F1');
    r_r    = vfc.dpafc_tc(X_r, opts_r);
    vfc.print_results(r_r(1:min(5,end)), 'MaxRows', 5, 'Labels', y_r);

    if ~isempty(r_r)
        plot_clustering(X_r, r_r(1), 'D-PAFC-TC: Ruspini dataset', y_r);
    end
end

% =========================================================================
function plot_clustering(X, result, title_str, y_true)
    n_sub = 1 + (~isempty(y_true));
    figure('Name', title_str, 'NumberTitle', 'off', ...
           'Position', [100 100 n_sub*400 340]);

    subplot(1, n_sub, 1);
    gscatter(X(:,1), X(:,2), result.labels);
    hold on;
    plot(X(result.centers, 1), X(result.centers, 2), 'kp', ...
         'MarkerSize', 12, 'MarkerFaceColor', 'k');
    hold off;
    xlabel('x_1');  ylabel('x_2');
    title(sprintf('%s\nc=%d, \\alpha=%.4f, %s=%.4f', ...
        title_str, result.c, result.alpha, result.criterion, result.score));
    legend(arrayfun(@(k) sprintf('C_%d', k), 1:result.c, 'UniformOutput',false), ...
           'Location', 'best');
    grid on;

    if ~isempty(y_true)
        subplot(1, n_sub, 2);
        gscatter(X(:,1), X(:,2), y_true);
        xlabel('x_1');  ylabel('x_2');
        title('Ground truth');
        grid on;
    end
end
