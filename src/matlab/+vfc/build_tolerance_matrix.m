function [T, Xn] = build_tolerance_matrix(X, varargin)
%VFC.BUILD_TOLERANCE_MATRIX  Build a fuzzy tolerance matrix from attribute data.
%
%   T = vfc.build_tolerance_matrix(X)
%   T = vfc.build_tolerance_matrix(X, 'Distance', DIST, 'Normalization', NORM, 'Accuracy', EPS)
%   [T, XN] = vfc.build_tolerance_matrix(...)
%
%   Constructs the fuzzy tolerance T on the objects {1,...,n} of an n-by-m
%   data matrix X:
%     1. every column of X is normalised;
%     2. the distance d(x_i, x_j) between the normalised objects is computed;
%     3. T(i,j) = 1 - d(x_i, x_j), clamped to [0,1];
%     4. with an accuracy threshold EPS the values of T are rounded to
%        multiples of EPS; without it, values that differ by rounding noise
%        only (at most 1e-12) are merged into one level (vfc.snap_levels).
%
%   Name-value parameters:
%     'Distance'      - 'euclidean' (default) : sqrt( sum((a-b).^2) / m )
%                       'squared-euclidean'   : sum((a-b).^2) / m
%                       'hamming'             : sum(|a-b|) / m  (normalised Hamming distance)
%                       'manhattan'           : the same as 'hamming'
%                       'chebyshev'           : max(|a-b|)
%                       'minkowski'           : (sum(|a-b|.^3) / m)^(1/3)
%                       'cosine'              : 1 - a.b / (|a| |b|)
%     'Normalization' - 'minmax' (default) : (x - min) / (max - min)
%                       'max'              : x / max(|x|)
%                       'none'
%     'Accuracy'      - accuracy threshold EPS in (0,1), [] (default) = full precision
%
%   Output:
%     T   - n-by-n matrix, symmetric, T(i,i) = 1, values in [0,1]
%     XN  - the normalised data the matrix was built from
%
%   The setting of the reference experiments of the monograph is
%     'Distance', 'squared-euclidean', 'Normalization', 'max'.
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering.
%     Springer, 2013 (StudFuzz 297), sec. 2.2.4 and 2.2.5.
%
%   See also: vfc.snap_levels, vfc.max_min_closure, vfc.dafc_c, vfc.dpafc

    p = inputParser;
    addRequired(p,  'X',              @(x) isnumeric(x) && ismatrix(x) && ~any(isnan(x(:))));
    addParameter(p, 'Distance',      'euclidean', @(s) ischar(s) || isstring(s));
    addParameter(p, 'Normalization', 'minmax',    @(s) ischar(s) || isstring(s));
    addParameter(p, 'Accuracy',      [],          @(e) isempty(e) || (isscalar(e) && e > 0 && e < 1));
    parse(p, X, varargin{:});

    dist_name = lower(char(p.Results.Distance));
    norm_name = lower(char(p.Results.Normalization));
    accuracy  = p.Results.Accuracy;

    Xn = normalize_columns(X, norm_name);

    [n, m] = size(Xn);
    T = eye(n);
    dist_fn = select_distance(dist_name, m);

    for i = 1 : n-1
        for j = i+1 : n
            dij = dist_fn(Xn(i,:), Xn(j,:));
            tij = max(0.0, min(1.0, 1.0 - dij));
            T(i,j) = tij;
            T(j,i) = tij;
        end
    end

    if isempty(accuracy)
        T = vfc.snap_levels(T);
    else
        T = min(1.0, max(0.0, round(T / accuracy) * accuracy));
        T(1 : n+1 : end) = 1.0;
    end
end

% =========================================================================
function Z = normalize_columns(X, method)
    switch method
        case 'minmax'
            lo  = min(X, [], 1);
            hi  = max(X, [], 1);
            span = hi - lo;
            span(span == 0) = 1;        % constant column
            Z = (X - lo) ./ span;
        case 'max'
            mx = max(abs(X), [], 1);
            mx(mx == 0) = 1;
            Z = X ./ mx;
        case 'none'
            Z = X;
        otherwise
            error('vfc:build_tolerance_matrix:unknownNorm', ...
                'Unknown normalization ''%s''. Supported: ''minmax'', ''max'', ''none''.', method);
    end
end

% =========================================================================
function fn = select_distance(name, m)
    switch name
        case 'euclidean'
            fn = @(a,b) sqrt(sum((a - b).^2) / m);
        case {'squared-euclidean', 'squared'}
            fn = @(a,b) sum((a - b).^2) / m;
        case {'hamming', 'manhattan'}
            fn = @(a,b) sum(abs(a - b)) / m;
        case 'chebyshev'
            fn = @(a,b) max(abs(a - b));
        case 'cosine'
            fn = @(a,b) max(0, 1 - dot(a,b) / (norm(a) * norm(b) + 1e-12));
        case 'minkowski'
            fn = @(a,b) (sum(abs(a - b).^3) / m)^(1/3);
        otherwise
            error('vfc:build_tolerance_matrix:unknownDist', ...
                ['Unknown distance ''%s''. Supported: ''euclidean'', ''squared-euclidean'', ' ...
                 '''hamming'', ''manhattan'', ''chebyshev'', ''minkowski'', ''cosine''.'], name);
    end
end
