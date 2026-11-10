function results = dpafc_tc(X, opts)
%VFC.DPAFC_TC  D-PAFC-TC: the principal allotment with the transitive closure.
%
%   RESULTS = vfc.dpafc_tc(X)
%   RESULTS = vfc.dpafc_tc(X, OPTS)
%
%   As vfc.dafc_tc, but the leap heuristic is not used: the levels of the
%   closure are scanned upwards and the allotment of the first level that
%   splits the set into at least two classes is returned - the allotment among
%   the least number of fully separated clusters.
%
%   OPTS and the output are those of vfc.dafc_tc.
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering.
%     Springer, 2013 (StudFuzz 297), sec. 2.3.2.
%
%   See also: vfc.dafc_tc, vfc.hafc_tc, vfc.dpafc

    narginchk(1, 2);
    if nargin < 2, opts = struct(); end
    [T, Tc, Xn] = vfc.closure_of_data(X, opts);

    results = vfc.empty_results();
    levels = flipud(vfc.alpha_levels(Tc));     % ascending
    for index = 1 : numel(levels)
        best = vfc.closure_allotment(Tc, levels(index), Xn, opts);
        if ~isempty(best)
            best.T  = T;
            best.Tc = Tc;
            results = best;
            return;
        end
    end
end
