function r = empty_results()
%VFC.EMPTY_RESULTS  Empty struct array with the fields of a clustering result.
    r = struct('centers', {}, 'clusters', {}, 'alpha', {}, 'c', {}, 'score', {}, ...
               'criterion', {}, 'membership', {}, 'labels', {}, 'intersections', {});
end
