function print_results(results, varargin)
%VFC.PRINT_RESULTS  Display a summary of clustering results.
%
%   vfc.print_results(RESULTS)
%   vfc.print_results(RESULTS, 'MaxRows', N)
%   vfc.print_results(RESULTS, 'Labels', Y)
%
%   Prints a formatted table of allotments and, if ground-truth labels are
%   provided, computes external validation indices.
%
%   Input:
%     RESULTS   - struct array returned by vfc.dafc, vfc.dpafc, etc.
%
%   Name-Value Parameters:
%     'MaxRows'  - maximum number of rows to print     [default: 10]
%     'Labels'   - n-by-1 integer vector of true class labels
%                  (enables Purity, Adjusted Rand Index, NMI output)
%
%   Output: printed to stdout.
%
%   Example:
%     r = vfc.dpafc(T);
%     vfc.print_results(r, 'Labels', y);
%
%   See also: vfc.dafc, vfc.dpafc, vfc.dafc_tc
%
%   Authors: Dmitry Viattchenin (algorithms), Aliaksandr Damaratski (implementation)

    narginchk(1, Inf);

    p = inputParser;
    addRequired(p,  'results');
    addParameter(p, 'MaxRows', 10,  @(x) isnumeric(x) && isscalar(x));
    addParameter(p, 'Labels',  [],  @(x) isnumeric(x) || isempty(x));
    parse(p, results, varargin{:});

    max_rows = p.Results.MaxRows;
    y_true   = p.Results.Labels;

    if isempty(results)
        fprintf('No valid allotments found.\n');
        return;
    end

    n_show = min(max_rows, numel(results));

    fprintf('\n');
    fprintf('%-4s  %-4s  %-8s  %-8s  %-20s  %s\n', ...
        '#', 'c', 'alpha', results(1).criterion, 'Sizes', 'Centers');
    fprintf('%s\n', repmat('-', 1, 72));

    for ri = 1 : n_show
        r     = results(ri);
        sizes = cellfun(@numel, r.clusters);
        sz_str = strjoin(arrayfun(@num2str, sizes, 'UniformOutput', false), ' | ');
        ctr_str = strjoin(arrayfun(@num2str, r.centers, 'UniformOutput', false), ', ');

        fprintf('%-4d  %-4d  %-8.4f  %-8.4f  %-20s  {%s}\n', ...
            ri, r.c, r.alpha, r.score, sz_str, ctr_str);
    end

    if numel(results) > n_show
        fprintf('  ... (%d more results not shown)\n', numel(results) - n_show);
    end

    % ── External validation (if ground-truth provided) ──────────────────────
    if ~isempty(y_true) && ~isempty(results)
        r_best = results(1);
        if ~isempty(r_best.labels)
            fprintf('\n--- External Validation (best result) ---\n');
            fprintf('  Purity : %.4f\n', purity(r_best.labels, y_true));
            fprintf('  ARI    : %.4f\n', adjusted_rand(r_best.labels, y_true));
            fprintf('  NMI    : %.4f\n', nmi(r_best.labels, y_true));
        end
    end
    fprintf('\n');
end

% =========================================================================
function C = contingency(pred, true_labels)
    % C(i,j) = number of objects of class i assigned to cluster j
    [~, ~, class_index]   = unique(true_labels(:));
    [~, ~, cluster_index] = unique(pred(:));
    C = accumarray([class_index, cluster_index], 1);
end

function p = purity(pred, true_labels)
    C = contingency(pred, true_labels);
    p = sum(max(C, [], 1)) / numel(pred);
end

function ari = adjusted_rand(pred, true_labels)
    % Hubert & Arabie (1985)
    n   = numel(pred);
    C   = contingency(pred, true_labels);
    sum_row = sum(C, 2);  sum_col = sum(C, 1);
    a  = sum(C(:).^2 - C(:)) / 2;
    b  = sum(sum_row.^2 - sum_row) / 2;
    c  = sum(sum_col.^2 - sum_col) / 2;
    d  = n*(n-1)/2;
    expected = b*c/d;
    ari = (a - expected) / ((b + c)/2 - expected + eps);
end

function val = nmi(pred, true_labels)
    n = numel(pred);
    P = contingency(pred, true_labels) / n;
    row = sum(P, 2);  col = sum(P, 1);
    entropy = @(q) -sum(q(q > 0) .* log2(q(q > 0)));
    outer = row * col;
    mask = P > 0;
    MI = sum(P(mask) .* log2(P(mask) ./ outer(mask)));
    val = 2*MI / (entropy(row) + entropy(col) + eps);
end
