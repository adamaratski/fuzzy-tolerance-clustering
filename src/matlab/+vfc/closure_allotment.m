function result = closure_allotment(Tc, alpha, Xn, opts)
%VFC.CLOSURE_ALLOTMENT  Best allotment of one level of a transitive closure.
%
%   RESULT = vfc.closure_allotment(TC, ALPHA)
%   RESULT = vfc.closure_allotment(TC, ALPHA, XN, OPTS)
%
%   On a max-min transitive closure TC the ALPHA-neighbourhoods are the
%   equivalence classes of the level, so every allotment of the level has the
%   same clusters - the classes - and differs only in the typical point chosen
%   inside each class. The criterion F1 or F2 is therefore maximised by taking
%   in every class the object with the largest mean membership. Objects whose
%   mean memberships tie (within 1e-12) are ordered by their distance to the
%   centroid of the class in the normalised data XN, then by index; without XN
%   the smallest index wins.
%
%   Returns an empty result when the level has fewer than two classes or a
%   class violates opts.min_power / opts.max_power.
%
%   See also: vfc.dafc_tc, vfc.dpafc_tc, vfc.hafc_tc

    if nargin < 3, Xn = []; end
    if nargin < 4, opts = struct(); end
    if ~isfield(opts, 'min_power'), opts.min_power = 1;    end
    if ~isfield(opts, 'max_power'), opts.max_power = 0;    end
    if ~isfield(opts, 'criterion'), opts.criterion = 'F1'; end

    n = size(Tc, 1);
    tie_tolerance = 1e-12;
    result = vfc.empty_results();

    % equivalence classes of the level
    assigned = false(1, n);
    classes = {};
    for i = 1 : n
        if assigned(i), continue; end
        members = find(Tc(i, :) >= alpha);
        assigned(members) = true;
        classes{end+1} = members; %#ok<AGROW>
    end
    c = numel(classes);
    if c < 2, return; end

    sizes = cellfun(@numel, classes);
    max_pow = opts.max_power;
    if max_pow == 0, max_pow = n; end
    if any(sizes < opts.min_power) || any(sizes > max_pow), return; end

    centers = zeros(1, c);
    for l = 1 : c
        members = classes{l};
        contribution = mean(Tc(members, members), 2)';       % mean membership for every candidate
        tied = members(contribution >= max(contribution) - tie_tolerance);
        if numel(tied) > 1 && ~isempty(Xn)
            centroid = mean(Xn(members, :), 1);
            distance = sqrt(sum((Xn(tied, :) - centroid).^2, 2))';
            tied = tied(distance <= min(distance) + tie_tolerance);
        end
        centers(l) = min(tied);
    end

    [centers, order] = sort(centers);
    classes = classes(order);

    result = struct();
    result.centers   = centers;
    result.clusters  = classes;
    result.alpha     = alpha;
    result.c         = c;
    result.score     = vfc.criterion_value(Tc, centers, classes, alpha, opts.criterion);
    result.criterion = upper(opts.criterion);
    U = zeros(n, c);
    for l = 1 : c
        U(classes{l}, l) = Tc(centers(l), classes{l})';
    end
    result.membership = U;
    [~, result.labels] = max(U, [], 2);
    result.intersections = 0;
end
