function results = dafc(T, c, alpha, opts)
%VFC.DAFC  Direct Adaptive Fuzzy Clustering — fixed cluster count and threshold.
%
%   RESULTS = vfc.dafc(T, C, ALPHA)
%   RESULTS = vfc.dafc(T, C, ALPHA, OPTS)
%
%   Searches for all valid c-allotments of the object set {1,...,n} with
%   respect to the fuzzy tolerance matrix T at threshold level ALPHA.
%
%   A c-ALLOTMENT {(c_1,C_1), ..., (c_c,C_c)} is VALID when:
%     (1) Membership : C_l = { j : T(c_l, j) >= alpha }
%     (2) Power      : opts.min_power <= |C_l| <= opts.max_power
%     (3) Coverage   : C_1 union ... union C_c = {1, ..., n}
%
%   Centers are enumerated exhaustively: all C(n,c) combinations are tested.
%   For large n/c, consider vfc.dafc_tc which applies transitive closure
%   to reduce the number of distinct alpha levels.
%
%   Input:
%     T      - n-by-n fuzzy tolerance matrix (T(i,j) in [0,1], T(i,i)=1)
%     C      - number of clusters (positive integer, 2 <= C <= n)
%     ALPHA  - alpha-cut threshold (scalar in (0, 1])
%
%   OPTS struct fields (all optional):
%     min_power   - minimum cluster cardinality  [default: 1]
%     max_power   - maximum cluster cardinality, 0 = n/c  [default: 0]
%     max_results - maximum results returned      [default: 200]
%     criterion   - quality criterion: 'F1' or 'F2'  [default: 'F1']
%     verbose     - print progress               [default: false]
%
%   Output RESULTS: 1-by-K struct array (K <= max_results), descending score:
%     .centers    - 1-by-c  integer vector of center indices (1-based)
%     .clusters   - 1-by-c  cell array {C_1, ..., C_c} of member index vectors
%     .alpha      - scalar  alpha level
%     .c          - scalar  number of clusters
%     .score      - scalar  F1 or F2 value
%     .criterion  - string  criterion name
%     .membership - n-by-c  fuzzy membership matrix  U(j,l) = T(c_l,j) if j in C_l
%     .labels     - n-by-1  hard assignment  (argmax over clusters)
%
%   F1 criterion:  sum_l [ mean_{j in C_l} T(c_l,j) ] - alpha * c
%   F2 criterion:  mean_l[ mean_{j in C_l} T(c_l,j) ]
%
%   Example:
%     X = [0 0; 1 0; 5 0; 6 0; 10 0; 11 0];
%     T = vfc.build_tolerance_matrix(X);
%     r = vfc.dafc(T, 3, 0.7);
%     vfc.print_results(r);
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering. Springer, 2013 (StudFuzz 297).
%     Algorithm 3.1 (D-AFC).
%
%   See also: vfc.dpafc, vfc.dafc_tc, vfc.alpha_levels, vfc.build_tolerance_matrix
%
%   Authors: Dmitry Viattchenin (algorithms), Aliaksandr Damaratski (implementation)

    narginchk(3, 4);
    if nargin < 4, opts = struct(); end
    opts = defaults(opts);

    validateattributes(T,     {'numeric'}, {'square','2d','real','>=',0,'<=',1}, 'vfc.dafc','T',1);
    validateattributes(c,     {'numeric'}, {'scalar','integer','positive'},       'vfc.dafc','c',2);
    validateattributes(alpha, {'numeric'}, {'scalar','real','>',0,'<=',1},        'vfc.dafc','alpha',3);

    n = size(T, 1);

    if c > n
        results = empty_result_array();
        return;
    end

    % ── Pre-compute alpha-neighbourhoods for every object ──────────────────
    max_pow = opts.max_power;
    if max_pow == 0, max_pow = n; end

    neighborhoods = cell(n, 1);
    powers        = zeros(n, 1);
    for i = 1 : n
        nb = find(T(i,:) >= alpha);
        powers(i) = numel(nb);
        neighborhoods{i} = nb;
    end

    % Only objects whose neighbourhood satisfies power constraints can be centers
    valid_ctr = find(powers >= opts.min_power & powers <= max_pow);

    if numel(valid_ctr) < c
        results = empty_result_array();
        return;
    end

    % ── Enumerate all C(|valid_ctr|, c) combinations ───────────────────────
    combos = nchoosek(valid_ctr(:)', c);   % K-by-c
    K = size(combos, 1);

    if opts.verbose
        fprintf('D-AFC: n=%d, c=%d, alpha=%.4f, valid centers=%d, combinations=%d\n', ...
            n, c, alpha, numel(valid_ctr), K);
    end

    if K > 2e6
        warning('vfc:dafc:largeSearch', ...
            ['C(%d,%d) = %d combinations — this may be slow.\n' ...
             'Consider using vfc.dafc_tc (applies transitive closure first).'], ...
            numel(valid_ctr), c, K);
    end

    % ── Test each combination ───────────────────────────────────────────────
    buf      = cell(K, 1);
    n_found  = 0;

    for ki = 1 : K
        ctrs = combos(ki, :);

        % Coverage check: union of neighbourhoods must span all n objects
        covered = false(1, n);
        for l = 1 : c
            covered(neighborhoods{ctrs(l)}) = true;
        end
        if ~all(covered), continue; end

        % Score
        score = criterion_score(T, ctrs, neighborhoods(ctrs), alpha, opts.criterion);

        entry.centers   = ctrs;
        entry.clusters  = neighborhoods(ctrs);
        entry.alpha     = alpha;
        entry.c         = c;
        entry.score     = score;
        entry.criterion = opts.criterion;
        entry.membership = [];  % filled below for top-k only
        entry.labels     = [];

        n_found         = n_found + 1;
        buf{n_found}    = entry;
    end

    if n_found == 0
        results = empty_result_array();
        return;
    end

    buf = buf(1 : n_found);

    % ── Keep top-k by score ─────────────────────────────────────────────────
    scores   = cellfun(@(e) e.score, buf);
    [~, idx] = sort(scores, 'descend');
    keep     = idx(1 : min(opts.max_results, n_found));
    buf      = buf(keep);

    % ── Build membership matrix and hard labels ─────────────────────────────
    for ri = 1 : numel(buf)
        buf{ri}.membership = build_membership(T, buf{ri}.centers, buf{ri}.clusters, n);
        [~, buf{ri}.labels] = max(buf{ri}.membership, [], 2);
    end

    results = [buf{:}];
end

% =========================================================================
function s = criterion_score(T, centers, clusters, alpha, criterion)
    c = numel(centers);
    total = 0.0;
    for l = 1 : c
        ms = clusters{l};
        if ~isempty(ms)
            total = total + mean(T(centers(l), ms));
        end
    end
    switch upper(criterion)
        case 'F1',  s = total - alpha * c;
        case 'F2',  s = total / c;
        otherwise
            error('vfc:dafc:unknownCriterion', ...
                'Unknown criterion ''%s''. Use ''F1'' or ''F2''.', criterion);
    end
end

% =========================================================================
function U = build_membership(T, centers, clusters, n)
    c = numel(centers);
    U = zeros(n, c);
    for l = 1 : c
        ms = clusters{l};
        if ~isempty(ms)
            U(ms, l) = T(centers(l), ms)';
        end
    end
end

% =========================================================================
function opts = defaults(opts)
    if ~isfield(opts, 'min_power'),   opts.min_power   = 1;     end
    if ~isfield(opts, 'max_power'),   opts.max_power   = 0;     end
    if ~isfield(opts, 'max_results'), opts.max_results = 200;   end
    if ~isfield(opts, 'criterion'),   opts.criterion   = 'F1';  end
    if ~isfield(opts, 'verbose'),     opts.verbose     = false; end
end

% =========================================================================
function r = empty_result_array()
    r = struct('centers',{}, 'clusters',{{}},'alpha',{}, 'c',{}, ...
               'score',{}, 'criterion',{}, 'membership',{}, 'labels',{});
end
