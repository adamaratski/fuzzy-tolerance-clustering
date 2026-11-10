function results = dafc_tc(X, opts)
%VFC.DAFC_TC  D-AFC-TC: the direct algorithm with the transitive closure.
%
%   RESULTS = vfc.dafc_tc(X)
%   RESULTS = vfc.dafc_tc(X, OPTS)
%
%   Prototype-based pipeline:
%     1. normalise X and build the fuzzy tolerance T;
%     2. compute the max-min transitive closure of T;
%     3. select the threshold alpha* with the leap heuristic (the largest leap
%        between consecutive levels of the closure);
%     4. return the best allotment of that level: its clusters are the classes
%        of the level, and each typical point maximises the mean membership of
%        its class (ties: nearest to the centroid of the class).
%
%   The number of clusters follows from the data; it is not given.
%
%   OPTS fields (all optional):
%     distance, normalization, accuracy - see vfc.build_tolerance_matrix
%     min_power, max_power, criterion   - see vfc.closure_allotment
%
%   Output: a result struct as returned by vfc.dafc, with the additional
%   fields .T and .Tc (empty struct array when no allotment exists).
%
%   Example (Sneath-Sokal data, the setting of the monograph):
%     r = vfc.dafc_tc(X, struct('distance', 'squared-euclidean', 'normalization', 'max'));
%     % two clusters of 13 and 3 objects at alpha = 0.96875
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering.
%     Springer, 2013 (StudFuzz 297), sec. 2.3.1.
%
%   See also: vfc.dpafc_tc, vfc.hafc_tc, vfc.alpha_leap, vfc.max_min_closure

    narginchk(1, 2);
    if nargin < 2, opts = struct(); end
    [T, Tc, Xn] = vfc.closure_of_data(X, opts);

    results = vfc.empty_results();
    alpha_star = vfc.alpha_leap(Tc);
    if isempty(alpha_star), return; end

    best = vfc.closure_allotment(Tc, alpha_star, Xn, opts);
    if isempty(best), return; end
    best.T  = T;
    best.Tc = Tc;
    results = best;
end
