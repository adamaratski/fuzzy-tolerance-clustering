function T = build_tolerance_matrix(X, varargin)
%VFC.BUILD_TOLERANCE_MATRIX  Build a fuzzy tolerance matrix from raw data.
%
%   T = vfc.build_tolerance_matrix(X)
%   T = vfc.build_tolerance_matrix(X, 'Distance', DIST, 'Normalization', NORM)
%
%   Constructs the fuzzy tolerance relation T on {1,...,n} from an n-by-d
%   data matrix X by:
%     1. Normalizing each column of X.
%     2. Computing the pairwise distance d(x_i, x_j).
%     3. Setting T(i,j) = 1 - d(x_i, x_j),  clamped to [0,1].
%
%   The result is reflexive (T(i,i)=1), symmetric, and valued in [0,1].
%
%   Input:
%     X  - n-by-d data matrix  (n objects, d features, numeric, no NaN)
%
%   Name-Value Parameters:
%     'Distance'      - Distance metric (default: 'euclidean')
%                       'euclidean'  : sqrt( sum((a-b)^2) / d )
%                       'manhattan'  : sum(|a-b|) / d
%                       'chebyshev'  : max(|a-b|)
%                       'hamming'    : fraction of differing positions
%                       'cosine'     : 1 - dot(a,b)/(||a||*||b||)
%     'Normalization' - Column normalization (default: 'minmax')
%                       'minmax' : (x - min) / (max - min)
%                       'max'    : x / max(|x|)
%                       'none'   : no normalization
%
%   Output:
%     T  - n-by-n double matrix,  T(i,j) in [0,1],  T(i,i) = 1
%
%   Example:
%     X = load('iris.txt');  X = X(:,1:4);
%     T = vfc.build_tolerance_matrix(X, 'Distance', 'euclidean');
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering. Springer, 2013 (StudFuzz 297).
%     Ch. 2, Definition 2.1 (fuzzy tolerance relation).
%
%   See also: vfc.max_min_closure, vfc.dafc, vfc.dpafc
%
%   Authors: Dmitry Viattchenin (algorithms), Aliaksandr Damaratski (implementation)

    narginchk(1, Inf);

    p = inputParser;
    addRequired(p,  'X',              @(x) isnumeric(x) && ismatrix(x) && ~any(isnan(x(:))));
    addParameter(p, 'Distance',      'euclidean', @(s) ischar(s) || isstring(s));
    addParameter(p, 'Normalization', 'minmax',    @(s) ischar(s) || isstring(s));
    parse(p, X, varargin{:});

    dist_name = lower(char(p.Results.Distance));
    norm_name = lower(char(p.Results.Normalization));

    % --- Normalize ---
    X_norm = normalize_columns(X, norm_name);

    % --- Build tolerance matrix ---
    [n, d] = size(X_norm);
    T = eye(n);  % reflexivity: T(i,i) = 1

    dist_fn = select_distance(dist_name, d);

    for i = 1 : n-1
        for j = i+1 : n
            dij = dist_fn(X_norm(i,:), X_norm(j,:));
            tij = max(0.0, min(1.0, 1.0 - dij));
            T(i,j) = tij;
            T(j,i) = tij;   % symmetry
        end
    end
end

% =========================================================================
function Z = normalize_columns(X, method)
    switch method
        case 'minmax'
            lo  = min(X, [], 1);
            hi  = max(X, [], 1);
            rng = hi - lo;
            rng(rng == 0) = 1;          % constant column → keep as-is
            Z = (X - lo) ./ rng;
        case 'max'
            mx = max(abs(X), [], 1);
            mx(mx == 0) = 1;
            Z = X ./ mx;
        case 'none'
            Z = X;
        otherwise
            error('vfc:build_tolerance_matrix:unknownNorm', ...
                ['Unknown normalization ''%s''. ' ...
                 'Supported: ''minmax'', ''max'', ''none''.'], method);
    end
end

% =========================================================================
function fn = select_distance(name, d)
    switch name
        case 'euclidean'
            % Normalized so that the maximum possible distance is 1
            % (all features span [0,1] after minmax normalization).
            fn = @(a,b) sqrt(sum((a - b).^2) / d);

        case 'manhattan'
            fn = @(a,b) sum(abs(a - b)) / d;

        case 'chebyshev'
            fn = @(a,b) max(abs(a - b));

        case 'hamming'
            fn = @(a,b) sum(abs(a - b) > 1e-10) / d;

        case 'cosine'
            fn = @(a,b) max(0, 1 - dot(a,b) / (norm(a) * norm(b) + 1e-12));

        case 'minkowski'
            p = 3;          % default order; clone the function for other p
            fn = @(a,b) (sum(abs(a - b).^p) / d)^(1/p);

        otherwise
            error('vfc:build_tolerance_matrix:unknownDist', ...
                ['Unknown distance ''%s''. ' ...
                 'Supported: ''euclidean'', ''manhattan'', ''chebyshev'', ' ...
                 '''hamming'', ''cosine''.'], name);
    end
end
