function results = rank_results(results, max_results)
%VFC.RANK_RESULTS  Order allotments: larger criterion, then higher threshold,
%   then fewer clusters, then lexicographically smaller typical points.
    if isempty(results), return; end
    K = numel(results);
    width = max(arrayfun(@(r) numel(r.centers), results));
    key = zeros(K, 3 + width);
    for k = 1 : K
        key(k, 1) = -results(k).score;
        key(k, 2) = -results(k).alpha;
        key(k, 3) = results(k).c;
        key(k, 4 : 3 + numel(results(k).centers)) = results(k).centers;
    end
    [~, order] = sortrows(key);
    results = results(order);
    if nargin >= 2 && K > max_results
        results = results(1 : max_results);
    end
end
