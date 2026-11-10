function T = snap_levels(T, tol)
%VFC.SNAP_LEVELS  Merge values of a tolerance matrix that differ by rounding noise.
%
%   T = vfc.snap_levels(T)
%   T = vfc.snap_levels(T, TOL)
%
%   The sorted off-diagonal values are swept; a gap larger than TOL
%   (default 1e-12) starts a new level, and every value of a level is
%   replaced by its smallest value. Distances accumulated in a different
%   order differ in the last bits, which would otherwise split one
%   alpha-level of the data into several.
%
%   See also: vfc.build_tolerance_matrix, vfc.alpha_levels

    if nargin < 2, tol = 1e-12; end
    n = size(T, 1);
    if n < 2, return; end

    mask = triu(true(n), 1);
    vals = unique(T(mask));                 % ascending column vector
    if numel(vals) >= 2
        starts = [true; diff(vals(:)) > tol];   % a gap larger than tol starts a new level
        group  = cumsum(starts);
        reps   = vals(starts);                  % the smallest value of every level
        [~, loc] = ismember(T(mask), vals);
        U = zeros(n);
        U(mask) = reps(group(loc));
        T = U + U';
    else
        T = triu(T, 1) + triu(T, 1)';
    end
    T(1 : n+1 : end) = 1.0;
end
