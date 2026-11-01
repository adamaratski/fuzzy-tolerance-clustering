function print_results(results, varargin)
%VFC.PRINT_RESULTS  Display a summary of D-AFC / D-PAFC clustering results.
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
%     r = vfc.dafc(T, 3, 0.7);
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
function p = purity(pred, true_labels)
    classes = unique(true_labels);
    clusters = unique(pred);
    total = 0;
    for cl = clusters(:)'
        mask = pred == cl;
        counts = histc(true_labels(mask), classes);  %#ok<HISTC>
        total = total + max(counts);
    end
    p = total / numel(pred);
end

% =========================================================================
function ari = adjusted_rand(pred, true_labels)
    % Hubert & Arabie (1985) formula
    n   = numel(pred);
    C   = confusionmat(true_labels, pred);
    sum_row = sum(C, 2);  sum_col = sum(C, 1);
    a  = sum(C(:).^2 - C(:)) / 2;           % sum C(i,j) choose 2
    b  = sum(sum_row.^2 - sum_row) / 2;     % sum a_i choose 2
    c  = sum(sum_col.^2 - sum_col) / 2;     % sum b_j choose 2
    d  = n*(n-1)/2;
    expected = b*c/d;
    ari = (a - expected) / ((b + c)/2 - expected + eps);
end

% =========================================================================
function val = nmi(pred, true_labels)
    n = numel(pred);
    pred_u  = unique(pred);
    true_u  = unique(true_labels);
    nc = numel(pred_u);
    nt = numel(true_u);

    H_pred = 0;
    for k = 1:nc
        p = sum(pred == pred_u(k)) / n;
        if p > 0, H_pred = H_pred - p*log2(p); end
    end
    H_true = 0;
    for k = 1:nt
        p = sum(true_labels == true_u(k)) / n;
        if p > 0, H_true = H_true - p*log2(p); end
    end
    MI = 0;
    for ki = 1:nc
        for kj = 1:nt
            pij = sum(pred == pred_u(ki) & true_labels == true_u(kj)) / n;
            pi  = sum(pred == pred_u(ki)) / n;
            pj  = sum(true_labels == true_u(kj)) / n;
            if pij > 0
                MI = MI + pij * log2(pij / (pi * pj + eps));
            end
        end
    end
    val = 2*MI / (H_pred + H_true + eps);
end
