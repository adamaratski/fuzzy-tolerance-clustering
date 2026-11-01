function results = dafc_tc(X, opts)
%VFC.DAFC_TC  D-AFC with Transitive Closure — prototype variant.
%
%   RESULTS = vfc.dafc_tc(X)
%   RESULTS = vfc.dafc_tc(X, OPTS)
%
%   Prototype pipeline:
%     1. Normalize X and build fuzzy tolerance matrix T.
%     2. Compute max-min transitive closure T~ of T.
%     3. Select alpha* via the leap heuristic (maximal leap, upper endpoint).
%     4. Run D-AFC at alpha* for c = 2, 3, ..., n.
%     5. Return all valid allotments found, sorted by score.
%
%   The transitive closure T~ has fewer distinct alpha levels than T,
%   making the search tractable for medium-size datasets (n up to ~200).
%   The leap heuristic selects the most representative partition level.
%
%   Input:
%     X     - n-by-d raw data matrix  (n objects, d numeric features)
%
%   OPTS struct fields (all optional):
%     distance      - see vfc.build_tolerance_matrix  [default: 'euclidean']
%     normalization - see vfc.build_tolerance_matrix  [default: 'minmax']
%     min_power     - minimum cluster cardinality     [default: 1]
%     max_power     - maximum cardinality, 0 = n/c    [default: 0]
%     max_results   - total results cap               [default: 200]
%     criterion     - 'F1' or 'F2'                   [default: 'F1']
%     verbose       - print pipeline steps            [default: false]
%
%   Output RESULTS: 1-by-K struct array (same fields as vfc.dafc).
%     Additional fields added to each result:
%       .T   - n-by-n fuzzy tolerance matrix
%       .Tc  - n-by-n max-min transitive closure T~
%
%   Example:
%     X = readmatrix('iris.csv');  X = X(:,1:4);
%     r = vfc.dafc_tc(X, struct('criterion','F1','verbose',true));
%     vfc.print_results(r(1));
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering. Springer, 2013 (StudFuzz 297).
%     Algorithm 4.1 (D-AFC-TC).
%
%   See also: vfc.dpafc_tc, vfc.dafc, vfc.build_tolerance_matrix, vfc.max_min_closure
%
%   Authors: Dmitry Viattchenin (algorithms), Aliaksandr Damaratski (implementation)

    narginchk(1, 2);
    if nargin < 2, opts = struct(); end
    opts = defaults(opts);

    validateattributes(X, {'numeric'}, {'2d','real','nonempty'}, 'vfc.dafc_tc', 'X');

    n = size(X, 1);

    % ── Step 1: tolerance matrix ────────────────────────────────────────────
    if opts.verbose, fprintf('D-AFC-TC: building tolerance matrix (n=%d)...\n', n); end
    T = vfc.build_tolerance_matrix(X, ...
        'Distance',      opts.distance, ...
        'Normalization', opts.normalization);

    % ── Step 2: transitive closure ──────────────────────────────────────────
    if opts.verbose, fprintf('D-AFC-TC: computing max-min closure...\n'); end
    Tc = vfc.max_min_closure(T);

    % ── Step 3: leap heuristic ──────────────────────────────────────────────
    alpha_star = vfc.alpha_leap(Tc);

    if isempty(alpha_star)
        warning('vfc:dafc_tc:noLevels', 'No distinct alpha levels found in T~.');
        results = empty_result_array();
        return;
    end

    if opts.verbose
        fprintf('D-AFC-TC: alpha* = %.4f (leap heuristic)\n', alpha_star);
    end

    % ── Step 4: D-AFC at alpha* for c = 2 ... n ────────────────────────────
    dafc_opts           = opts;
    dafc_opts.verbose   = false;

    all_buf = {};
    for c = 2 : n
        r = vfc.dafc(Tc, c, alpha_star, dafc_opts);
        if ~isempty(r)
            all_buf{end+1} = r;  %#ok<AGROW>
        end
    end

    if isempty(all_buf)
        results = empty_result_array();
        return;
    end

    results  = [all_buf{:}];
    scores   = [results.score];
    [~, idx] = sort(scores, 'descend');
    results  = results(idx(1 : min(opts.max_results, numel(idx))));

    % Attach matrices to each result for inspection
    for ri = 1 : numel(results)
        results(ri).T  = T;
        results(ri).Tc = Tc;
    end

    if opts.verbose
        fprintf('D-AFC-TC: %d allotment(s) found.\n', numel(results));
    end
end

% =========================================================================
function opts = defaults(opts)
    if ~isfield(opts, 'distance'),      opts.distance      = 'euclidean'; end
    if ~isfield(opts, 'normalization'), opts.normalization = 'minmax';    end
    if ~isfield(opts, 'min_power'),     opts.min_power     = 1;           end
    if ~isfield(opts, 'max_power'),     opts.max_power     = 0;           end
    if ~isfield(opts, 'max_results'),   opts.max_results   = 200;         end
    if ~isfield(opts, 'criterion'),     opts.criterion     = 'F1';        end
    if ~isfield(opts, 'verbose'),       opts.verbose       = false;       end
end

% =========================================================================
function r = empty_result_array()
    r = struct('centers',{}, 'clusters',{{}},'alpha',{}, 'c',{}, ...
               'score',{}, 'criterion',{}, 'membership',{}, 'labels',{}, ...
               'T',{}, 'Tc',{});
end
