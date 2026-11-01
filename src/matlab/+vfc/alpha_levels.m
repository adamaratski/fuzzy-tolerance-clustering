function levels = alpha_levels(T)
%VFC.ALPHA_LEVELS  Distinct alpha-cut threshold levels of a fuzzy matrix.
%
%   LEVELS = vfc.alpha_levels(T)
%
%   Returns all distinct off-diagonal values of T that lie strictly in
%   the open interval (0, 1), sorted in descending order.  These are the
%   meaningful alpha-cut levels for D-AFC / D-PAFC analysis.
%
%   In a transitive closure T~, distinct levels correspond to the
%   hierarchical structure of the fuzzy equivalence classes.
%
%   Input:
%     T       - n-by-n fuzzy tolerance (or equivalence) matrix
%
%   Output:
%     LEVELS  - k-by-1 double vector of distinct values in (0,1), descending
%               Returns empty [] if no such values exist.
%
%   Example:
%     T = [1.0 0.8 0.3; 0.8 1.0 0.5; 0.3 0.5 1.0];
%     vfc.alpha_levels(T)
%       ans = [0.8; 0.5; 0.3]
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering. Springer, 2013 (StudFuzz 297).
%     Ch. 3, alpha-level sets of fuzzy tolerance relations.
%
%   See also: vfc.alpha_leap, vfc.dafc, vfc.dpafc
%
%   Authors: Dmitry Viattchenin (algorithms), Aliaksandr Damaratski (implementation)

    narginchk(1, 1);

    n = size(T, 1);

    % Collect upper-triangle values (exclude diagonal, exploit symmetry)
    vals = [];
    for i = 1 : n-1
        for j = i+1 : n
            v = T(i,j);
            if v > 0.0 && v < 1.0
                vals(end+1) = v; %#ok<AGROW>
            end
        end
    end

    if isempty(vals)
        levels = [];
        return;
    end

    levels = unique(vals, 'sorted');  % ascending
    levels = flipud(levels(:));       % descending column vector
end
