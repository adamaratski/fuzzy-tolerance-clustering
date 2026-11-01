function results = dpafc(T, opts)
%VFC.DPAFC  Direct Principal Allotment Fuzzy Clustering — automatic cluster count.
%
%   RESULTS = vfc.dpafc(T)
%   RESULTS = vfc.dpafc(T, OPTS)
%
%   Determines the number of clusters c automatically by running D-AFC
%   across all distinct alpha levels for c = 2, 3, ..., n.  Terminates
%   early when two consecutive values of c produce no valid allotments.
%
%   This is the "relational" variant: it operates directly on the tolerance
%   matrix T without requiring raw data.  For the prototype variant that
%   also computes the transitive closure, see vfc.dpafc_tc.
%
%   Algorithm:
%     for c = 2 to n:
%       for each distinct alpha level (descending):
%         collect all valid c-allotments via vfc.dafc
%       if no valid allotment found for c  AND  for c-1:  STOP
%     return all allotments, sorted by score (descending)
%
%   Input:
%     T     - n-by-n fuzzy tolerance matrix
%
%   OPTS struct fields (all optional):
%     min_power   - minimum cluster cardinality  [default: 1]
%     max_power   - maximum cardinality, 0 = n/c [default: 0]
%     max_results - total results cap            [default: 500]
%     criterion   - 'F1' or 'F2'                [default: 'F1']
%     verbose     - print per-c summary          [default: false]
%
%   Output RESULTS: 1-by-K struct array (same fields as vfc.dafc output),
%   sorted by score descending.  Returns empty struct array if no valid
%   allotment exists.
%
%   Example:
%     T = vfc.build_tolerance_matrix(X);
%     r = vfc.dpafc(T);
%     vfc.print_results(r(1));
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering. Springer, 2013 (StudFuzz 297).
%     Algorithm 3.2 (D-PAFC).
%
%   See also: vfc.dafc, vfc.dpafc_tc, vfc.alpha_levels
%
%   Authors: Dmitry Viattchenin (algorithms), Aliaksandr Damaratski (implementation)

    narginchk(1, 2);
    if nargin < 2, opts = struct(); end
    opts = defaults(opts);

    validateattributes(T, {'numeric'}, {'square','2d','real','>=',0,'<=',1}, 'vfc.dpafc', 'T');

    n      = size(T, 1);
    levels = vfc.alpha_levels(T);

    if isempty(levels)
        results = empty_result_array();
        return;
    end

    % Per-c opts: pass through, but cap max_results per call
    dafc_opts           = opts;
    dafc_opts.verbose   = false;
    dafc_opts.max_results = min(opts.max_results, 200);

    all_buf          = {};
    consecutive_empty = 0;

    for c = 2 : n
        c_found = 0;

        for ai = 1 : numel(levels)
            r = vfc.dafc(T, c, levels(ai), dafc_opts);
            if ~isempty(r)
                all_buf{end+1} = r;  %#ok<AGROW>
                c_found = c_found + numel(r);
            end
        end

        if opts.verbose
            fprintf('D-PAFC: c=%d  → %d allotments found\n', c, c_found);
        end

        if c_found == 0
            consecutive_empty = consecutive_empty + 1;
            if consecutive_empty >= 2
                break;
            end
        else
            consecutive_empty = 0;
        end
    end

    if isempty(all_buf)
        results = empty_result_array();
        return;
    end

    % Concatenate, sort, cap
    results  = [all_buf{:}];
    scores   = [results.score];
    [~, idx] = sort(scores, 'descend');
    results  = results(idx(1 : min(opts.max_results, numel(idx))));
end

% =========================================================================
function opts = defaults(opts)
    if ~isfield(opts, 'min_power'),   opts.min_power   = 1;     end
    if ~isfield(opts, 'max_power'),   opts.max_power   = 0;     end
    if ~isfield(opts, 'max_results'), opts.max_results = 500;   end
    if ~isfield(opts, 'criterion'),   opts.criterion   = 'F1';  end
    if ~isfield(opts, 'verbose'),     opts.verbose     = false; end
end

% =========================================================================
function r = empty_result_array()
    r = struct('centers',{}, 'clusters',{{}},'alpha',{}, 'c',{}, ...
               'score',{}, 'criterion',{}, 'membership',{}, 'labels',{});
end
