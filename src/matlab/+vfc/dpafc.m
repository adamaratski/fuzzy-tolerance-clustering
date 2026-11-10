function results = dpafc(T, opts)
%VFC.DPAFC  D-PAFC: the direct algorithm for detecting the principal allotment.
%
%   RESULTS = vfc.dpafc(T)
%   RESULTS = vfc.dpafc(T, OPTS)
%
%   The number of clusters is not given. The levels of the fuzzy tolerance T
%   are scanned upwards; levels at which some alpha-neighbourhood is still the
%   whole set are skipped. At the first remaining level that admits an
%   allotment, the least number of clusters c is taken, and the allotments
%   among c clusters of that level are returned ordered by the criterion.
%   With fully separated clusters (the default) RESULTS(1) is the principal
%   allotment.
%
%   OPTS fields (all optional):
%     max_intersections - objects admitted in intersection areas  [default: 0]
%     min_power, max_power, max_results, criterion - see vfc.dafc
%
%   Example (Sneath-Sokal data, the setting of the monograph):
%     T = vfc.build_tolerance_matrix(X, 'Distance', 'squared-euclidean', 'Normalization', 'max');
%     r = vfc.dpafc(T);          % four clusters at alpha = 0.91319
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering.
%     Springer, 2013 (StudFuzz 297), sec. 2.2.3.
%
%   See also: vfc.dafc, vfc.dafc_c, vfc.dpafc_tc

    narginchk(1, 2);
    if nargin < 2, opts = struct(); end
    if ~isfield(opts, 'max_intersections'), opts.max_intersections = 0; end
    if ~isfield(opts, 'min_power'), opts.min_power = 1; end
    if ~isfield(opts, 'max_power'), opts.max_power = 0; end

    validateattributes(T, {'numeric'}, {'square','2d','real','>=',0,'<=',1}, 'vfc.dpafc', 'T');

    n = size(T, 1);
    results = vfc.empty_results();
    levels = flipud(vfc.alpha_levels(T));      % ascending
    max_pow = opts.max_power;
    if max_pow == 0, max_pow = n; end

    for index = 1 : numel(levels)
        alpha = levels(index);
        powers = sum(T >= alpha, 2);
        if any(powers == n), continue; end     % the whole set is still one cluster
        candidates = sum(powers >= opts.min_power & powers <= max_pow);
        for c = 2 : candidates
            r = vfc.dafc(T, c, alpha, opts);
            if ~isempty(r)
                results = r;
                return;
            end
        end
    end
end
