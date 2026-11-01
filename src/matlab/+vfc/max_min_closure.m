function Tc = max_min_closure(T)
%VFC.MAX_MIN_CLOSURE  Max-min transitive closure of a fuzzy tolerance matrix.
%
%   Tc = vfc.max_min_closure(T)
%
%   Computes the smallest fuzzy equivalence relation T~ that contains T,
%   i.e. the max-min transitive closure defined by:
%
%     T~(i,j) = max_k  min( T(i,k), T(k,j) )
%
%   The closure is obtained by repeating the max-min composition
%   until the matrix stabilises (equivalent to Floyd-Warshall on the
%   (max, min) semiring in O(n^3)).
%
%   Properties of T~:
%     - Reflexive  : T~(i,i) = 1
%     - Symmetric  : T~(i,j) = T~(j,i)
%     - Max-min transitive : T~(i,j) >= min(T~(i,k), T~(k,j))  for all k
%
%   Input:
%     T   - n-by-n fuzzy tolerance matrix (values in [0,1], diagonal = 1)
%
%   Output:
%     Tc  - n-by-n max-min transitive closure T~
%
%   Example:
%     X  = [1 0; 2 0; 5 0; 10 0; 11 0];
%     T  = vfc.build_tolerance_matrix(X);
%     Tc = vfc.max_min_closure(T);
%
%   Reference:
%     Viattchenin, D.A. A Heuristic Approach to Possibilistic Clustering. Springer, 2013 (StudFuzz 297).
%     Ch. 2, Theorem 2.1 (max-min transitive closure).
%     Zadeh, L.A. Similarity relations and fuzzy orderings. Inform. Sci., 1971.
%
%   See also: vfc.build_tolerance_matrix, vfc.alpha_levels, vfc.dpafc_tc
%
%   Authors: Dmitry Viattchenin (algorithms), Aliaksandr Damaratski (implementation)

    narginchk(1, 1);
    validateattributes(T, {'numeric'}, ...
        {'square', '2d', 'real', '>=', 0, '<=', 1}, 'vfc.max_min_closure', 'T');

    n   = size(T, 1);
    Tc  = T;

    % Floyd-Warshall on the (max, min) semiring.
    % For each pivot k:  Tc(i,j) = max( Tc(i,j),  min(Tc(i,k), Tc(k,j)) )
    for k = 1 : n
        col_k = Tc(:, k);   % n-by-1,  Tc(i,k)  for all i
        row_k = Tc(k, :);   % 1-by-n,  Tc(k,j)  for all j

        % Outer min:  min_matrix(i,j) = min( Tc(i,k), Tc(k,j) )
        %   = min( col_k * ones(1,n),  ones(n,1) * row_k )
        min_matrix = min( repmat(col_k, 1, n), repmat(row_k, n, 1) );

        Tc = max(Tc, min_matrix);
    end

    % Enforce exact reflexivity and symmetry (numerical safety).
    Tc = max(Tc, Tc');
    Tc(1 : n+1 : end) = 1.0;
end
