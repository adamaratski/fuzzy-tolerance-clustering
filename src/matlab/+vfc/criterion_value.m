function s = criterion_value(T, centers, clusters, alpha, criterion)
%VFC.CRITERION_VALUE  Criterion F1 or F2 of an allotment.
%
%   F1 = sum_l mean_{j in C_l} T(tau_l, j) - alpha * c
%   F2 = mean_l mean_{j in C_l} T(tau_l, j)
    c = numel(centers);
    total = 0.0;
    for l = 1 : c
        total = total + mean(T(centers(l), clusters{l}));
    end
    switch upper(criterion)
        case 'F1'
            s = total - alpha * c;
        case 'F2'
            s = total / c;
        otherwise
            error('vfc:criterion:unknown', 'Unknown criterion ''%s''. Use ''F1'' or ''F2''.', criterion);
    end
end
