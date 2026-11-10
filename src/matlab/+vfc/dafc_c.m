function [results, w] = dafc_c(T, c, opts)
%VFC.DAFC_C  D-AFC(c): the direct algorithm for detecting the allotment among c fuzzy clusters.
%
%   RESULTS = vfc.dafc_c(T, C)
%   [RESULTS, W] = vfc.dafc_c(T, C, OPTS)
%
%   The complete algorithm of the monograph: the allotments among exactly C
%   fuzzy clusters are constructed at every level of the fuzzy tolerance T,
%   first for fully separated clusters (w = 0). Only if no allotment exists at
%   any level, the number w of objects admitted in intersection areas is
%   increased by one and the scan is repeated. The class of solutions B(c)
%   is returned ordered by the criterion; RESULTS(1) is the result of
%   classification and RESULTS(1).alpha its threshold.
%
%   OPTS fields (all optional): min_power, max_power, max_results, criterion
%   (see vfc.dafc), and
%     max_intersections - a fixed bound w instead of the automatic rule
%     verbose           - print the progress of the scan  [default: false]
%
%   W is the bound on the intersections that was needed.
%
%   The search is a plain depth-first enumeration and is meant for small data
%   (tens of objects, a few hundred levels). For larger data use the .NET
%   implementation of the repository.
%
%   Example (five-object example of the book):
%     T = [1 .8 0 .1 .2; .8 1 .4 0 .9; 0 .4 1 0 0; .1 0 0 1 .5; .2 .9 0 .5 1];
%     r = vfc.dafc_c(T, 3);      % alpha = 0.5, typical points 1, 3, 4, F1 = 1.15
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering.
%     Springer, 2013 (StudFuzz 297), sec. 2.2.1.
%
%   See also: vfc.dafc, vfc.dpafc, vfc.dafc_tc

    narginchk(2, 3);
    if nargin < 3, opts = struct(); end
    if ~isfield(opts, 'max_results'), opts.max_results = 200; end
    if ~isfield(opts, 'verbose'),     opts.verbose = false;   end

    n = size(T, 1);
    levels = vfc.alpha_levels(T);          % descending
    results = vfc.empty_results();
    w = [];
    if isempty(levels), return; end

    if isfield(opts, 'max_intersections')
        bounds = opts.max_intersections;
    else
        bounds = 0 : n;
    end

    level_opts = opts;
    for w = bounds
        level_opts.max_intersections = w;
        found = vfc.empty_results();
        for index = 1 : numel(levels)
            r = vfc.dafc(T, c, levels(index), level_opts);
            if ~isempty(r)
                found = [found, r]; %#ok<AGROW>
            end
        end
        if opts.verbose
            fprintf('D-AFC(%d): w = %d, %d allotment(s)\n', c, w, numel(found));
        end
        if ~isempty(found)
            results = vfc.rank_results(found, opts.max_results);
            return;
        end
    end
    w = [];
end
