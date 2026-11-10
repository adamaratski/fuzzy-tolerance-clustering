function levels = hafc_tc(X, opts)
%VFC.HAFC_TC  H-AFC-TC: the hierarchical algorithm with the transitive closure.
%
%   LEVELS = vfc.hafc_tc(X)
%   LEVELS = vfc.hafc_tc(X, OPTS)
%
%   Returns the best allotment of every level of the transitive closure,
%   from the coarsest level to the finest: LEVELS(k) is a result struct as
%   returned by vfc.dafc_tc. Each allotment refines the previous one, so the
%   sequence is a hierarchy of allotments.
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering.
%     Springer, 2013 (StudFuzz 297), sec. 2.3.2.
%
%   See also: vfc.dafc_tc, vfc.dpafc_tc

    narginchk(1, 2);
    if nargin < 2, opts = struct(); end
    [~, Tc, Xn] = vfc.closure_of_data(X, opts);

    levels = vfc.empty_results();
    alphas = flipud(vfc.alpha_levels(Tc));     % ascending
    for index = 1 : numel(alphas)
        best = vfc.closure_allotment(Tc, alphas(index), Xn, opts);
        if ~isempty(best)
            levels(end+1) = best; %#ok<AGROW>
        end
    end
end
