function results = dpafc_tc(X, opts)
%VFC.DPAFC_TC  D-PAFC with Transitive Closure — prototype variant, automatic c.
%
%   RESULTS = vfc.dpafc_tc(X)
%   RESULTS = vfc.dpafc_tc(X, OPTS)
%
%   Prototype pipeline:
%     1. Normalize X and build fuzzy tolerance matrix T.
%     2. Compute max-min transitive closure T~ of T.
%     3. Run D-PAFC on T~ (all distinct alpha levels, automatic c).
%
%   Unlike vfc.dafc_tc, no leap heuristic is applied: all alpha levels of
%   T~ are examined, so the result covers the complete allotment hierarchy.
%   This is the most exhaustive of the TC-based algorithms and may be slow
%   for n > 100 with many cluster counts.
%
%   Input:
%     X     - n-by-d raw data matrix  (n objects, d numeric features)
%
%   OPTS struct fields (all optional):
%     distance      - see vfc.build_tolerance_matrix  [default: 'euclidean']
%     normalization - see vfc.build_tolerance_matrix  [default: 'minmax']
%     min_power     - minimum cluster cardinality     [default: 1]
%     max_power     - maximum cardinality, 0 = n/c    [default: 0]
%     max_results   - total results cap               [default: 500]
%     criterion     - 'F1' or 'F2'                   [default: 'F1']
%     verbose       - print pipeline steps            [default: false]
%
%   Output RESULTS: 1-by-K struct array (same fields as vfc.dafc).
%     Additional fields:
%       .T   - n-by-n fuzzy tolerance matrix
%       .Tc  - n-by-n max-min transitive closure T~
%
%   Example:
%     X = readmatrix('iris.csv');  X = X(:,1:4);
%     r = vfc.dpafc_tc(X, struct('verbose', true));
%     vfc.print_results(r(1:5));
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering. Springer, 2013 (StudFuzz 297).
%     Algorithm 4.2 (D-PAFC-TC).
%
%   See also: vfc.dafc_tc, vfc.dpafc, vfc.build_tolerance_matrix, vfc.max_min_closure
%
%   Authors: Dmitry Viattchenin (algorithms), Aliaksandr Damaratski (implementation)

    narginchk(1, 2);
    if nargin < 2, opts = struct(); end
    opts = defaults(opts);

    validateattributes(X, {'numeric'}, {'2d','real','nonempty'}, 'vfc.dpafc_tc', 'X');

    n = size(X, 1);

    % ── Step 1: tolerance matrix ────────────────────────────────────────────
    if opts.verbose, fprintf('D-PAFC-TC: building tolerance matrix (n=%d)...\n', n); end
    T = vfc.build_tolerance_matrix(X, ...
        'Distance',      opts.distance, ...
        'Normalization', opts.normalization);

    % ── Step 2: transitive closure ──────────────────────────────────────────
    if opts.verbose, fprintf('D-PAFC-TC: computing max-min closure...\n'); end
    Tc = vfc.max_min_closure(T);

    levels = vfc.alpha_levels(Tc);
    if opts.verbose
        fprintf('D-PAFC-TC: %d distinct alpha levels in T~\n', numel(levels));
    end

    % ── Step 3: D-PAFC on T~ ───────────────────────────────────────────────
    dpafc_opts = opts;
    dpafc_opts.verbose = opts.verbose;

    results = vfc.dpafc(Tc, dpafc_opts);

    if isempty(results)
        return;
    end

    % Attach matrices for inspection
    for ri = 1 : numel(results)
        results(ri).T  = T;
        results(ri).Tc = Tc;
    end

    if opts.verbose
        fprintf('D-PAFC-TC: %d allotment(s) found.\n', numel(results));
    end
end

% =========================================================================
function opts = defaults(opts)
    if ~isfield(opts, 'distance'),      opts.distance      = 'euclidean'; end
    if ~isfield(opts, 'normalization'), opts.normalization = 'minmax';    end
    if ~isfield(opts, 'min_power'),     opts.min_power     = 1;           end
    if ~isfield(opts, 'max_power'),     opts.max_power     = 0;           end
    if ~isfield(opts, 'max_results'),   opts.max_results   = 500;         end
    if ~isfield(opts, 'criterion'),     opts.criterion     = 'F1';        end
    if ~isfield(opts, 'verbose'),       opts.verbose       = false;       end
end
