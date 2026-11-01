function alpha_star = alpha_leap(T)
%VFC.ALPHA_LEAP  Select the tolerance threshold alpha* via the leap heuristic.
%
%   ALPHA_STAR = vfc.alpha_leap(T)
%
%   For the ascending sequence 0 < a_0 < a_1 < ... < a_Z = 1 of levels of T
%   (its distinct off-diagonal values in (0,1) followed by the projection
%   a_Z = 1, formulae (1.56)-(1.58) of Viattchenin 2013) the leaps
%       g_l = a_l - a_(l-1),   l = 1, ..., Z-1,
%   are compared -- the leap up to a_Z = 1 is NOT compared -- and alpha* is
%   the UPPER endpoint a_(l*) of the MAXIMAL leap; on ties the smallest index
%   wins.
%
%   Note on the sign: the printed statement of the heuristic (Viattchenin
%   2013, p. 116) says "minimal leap", but the published results it
%   accompanies (Yang-Wu: c = 4 at alpha* = 0.9994; Sneath-Sokal: c = 2 at
%   alpha* = 0.96875; Iris: c = 2 at alpha* = 0.9929) are reproduced only by
%   the maximal leap, which is also what the author's software computed.
%   This function therefore uses the maximal leap, in agreement with the
%   .NET implementation (LeapHeuristicAlphaSelector).
%
%   Input:
%     T          - n-by-n fuzzy tolerance (or equivalence) matrix
%
%   Output:
%     ALPHA_STAR - selected threshold scalar; [] if T has no level in (0,1);
%                  the single level if there is only one.
%
%   Example:
%     T  = vfc.build_tolerance_matrix(X);
%     Tc = vfc.max_min_closure(T);
%     a  = vfc.alpha_leap(Tc)
%
%     % Yang-Wu closure levels 0.96875 < 0.98 < 0.99944 < 0.99969:
%     % leaps 0.01125, 0.01944, 0.00024 -> maximal 0.01944 -> alpha* = 0.99944
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering:
%     Algorithms and Applications. Studies in Fuzziness and Soft Computing
%     297, Springer, 2013. Sec. 2.3.2 (leap heuristic), sec. 1.1
%     (formulae (1.56)-(1.58)).
%
%   See also: vfc.alpha_levels, vfc.dafc_tc
%
%   Authors: Dmitry Viattchenin (algorithms), Aliaksandr Damaratski (implementation)

    narginchk(1, 1);

    levels = vfc.alpha_levels(T);   % descending, values in (0,1)

    if isempty(levels)
        alpha_star = [];
        return;
    end
    if numel(levels) == 1
        alpha_star = levels(1);
        return;
    end

    asc = flipud(levels(:));        % ascending: asc(1) = a_0, ..., asc(end) = a_(Z-1)

    % Leaps between consecutive off-diagonal levels; the leap from asc(end)
    % up to the projection a_Z = 1 is not part of the comparison.
    leaps = diff(asc);              % leaps(l) = asc(l+1) - asc(l)

    % Maximal leap; leaps equal within a small tolerance count as ties and
    % the smallest index wins (first occurrence).
    tie_tol  = 1e-12;
    max_leap = -Inf;
    best     = 1;
    for l = 1 : numel(leaps)
        if leaps(l) > max_leap + tie_tol
            max_leap = leaps(l);
            best     = l;
        end
    end

    alpha_star = asc(best + 1);     % upper endpoint of the maximal leap
end
