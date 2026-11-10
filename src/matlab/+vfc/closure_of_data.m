function [T, Tc, Xn] = closure_of_data(X, opts)
%VFC.CLOSURE_OF_DATA  Tolerance matrix, its transitive closure and the normalised data.
%
%   [T, TC, XN] = vfc.closure_of_data(X, OPTS) with the fields distance,
%   normalization and accuracy of OPTS (see vfc.build_tolerance_matrix).
    if nargin < 2, opts = struct(); end
    if ~isfield(opts, 'distance'),      opts.distance      = 'euclidean'; end
    if ~isfield(opts, 'normalization'), opts.normalization = 'minmax';    end
    if ~isfield(opts, 'accuracy'),      opts.accuracy      = [];          end

    validateattributes(X, {'numeric'}, {'2d','real','nonempty'}, 'vfc.closure_of_data', 'X');
    [T, Xn] = vfc.build_tolerance_matrix(X, 'Distance', opts.distance, ...
        'Normalization', opts.normalization, 'Accuracy', opts.accuracy);
    Tc = vfc.max_min_closure(T);
end
