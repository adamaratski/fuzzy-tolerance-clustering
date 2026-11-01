using MathFuzzy.Core.Matrix;
using MathFuzzy.Inference.Systems;

namespace MathFuzzy.Inference.Generation;

/// <summary>
/// Fits Takagi–Sugeno consequents by local weighted least squares: rule k uses the rule's own
/// firing strengths wₖ(xᵢ) on the sample as weights (Yen, Wang, Gillespie 1998), so every rule
/// stays an interpretable local model. Call again after antecedent terms are edited.
/// </summary>
public static class ConsequentFitter
{
    /// <summary>Ridge term relative to the mean diagonal of XᵀWX; keeps singular local fits solvable.</summary>
    private const double Ridge = 1e-9;

    public static void FitSugeno(FuzzyInferenceSystem fis, RawDataMatrix inputs, IReadOnlyList<double> targets)
    {
        if (fis.Kind != FisKind.Sugeno) throw new InvalidOperationException("Only Sugeno consequents can be fitted.");
        if (inputs.Cols != fis.Inputs.Count) throw new ArgumentException("Input columns do not match the system inputs.", nameof(inputs));
        if (targets.Count != inputs.Rows) throw new ArgumentException("One target per row expected.", nameof(targets));

        int coefficientCount = fis.SugenoOrder == 0 ? 1 : inputs.Cols + 1;
        double targetMean = inputs.Rows == 0 ? 0 : targets.Average();

        foreach (var rule in fis.Rules)
        {
            // weights are taken from the rule as if enabled with unit weight, so toggling a rule does not change its fit
            var unweightedRule = new FuzzyRule { Antecedent = rule.Antecedent, Negated = rule.Negated, Connective = rule.Connective };
            var normalMatrix = new double[coefficientCount, coefficientCount];   // XᵀWX
            var rightHandSide = new double[coefficientCount];                    // XᵀWy
            var regressors = new double[coefficientCount];                       // (1, x₁, …, xₘ)
            double firingTotal = 0;

            for (int row = 0; row < inputs.Rows; row++)
            {
                var inputValues = inputs.GetRow(row);
                double firing = fis.FiringStrength(unweightedRule, inputValues);
                if (firing <= 0) continue;
                firingTotal += firing;
                regressors[0] = 1;
                for (int coefficient = 1; coefficient < coefficientCount; coefficient++) regressors[coefficient] = inputValues[coefficient - 1];
                for (int rowIndex = 0; rowIndex < coefficientCount; rowIndex++)
                {
                    rightHandSide[rowIndex] += firing * regressors[rowIndex] * targets[row];
                    for (int columnIndex = 0; columnIndex < coefficientCount; columnIndex++)
                        normalMatrix[rowIndex, columnIndex] += firing * regressors[rowIndex] * regressors[columnIndex];
                }
            }

            if (firingTotal <= 0)
            {
                // the rule never fires on the sample: fall back to a constant at the global mean
                rule.Coefficients = new double[coefficientCount];
                rule.Coefficients[0] = targetMean;
                continue;
            }

            double trace = 0;
            for (int diagonal = 0; diagonal < coefficientCount; diagonal++) trace += normalMatrix[diagonal, diagonal];
            double ridge = Ridge * trace / coefficientCount;
            for (int diagonal = 1; diagonal < coefficientCount; diagonal++) normalMatrix[diagonal, diagonal] += ridge;   // the intercept is not penalised
            rule.Coefficients = Solve(normalMatrix, rightHandSide);
        }
    }

    /// <summary>Gaussian elimination with partial pivoting.</summary>
    internal static double[] Solve(double[,] matrix, double[] rightHandSide)
    {
        int size = rightHandSide.Length;
        var reduced = (double[,])matrix.Clone();
        var values = (double[])rightHandSide.Clone();
        for (int pivotColumn = 0; pivotColumn < size; pivotColumn++)
        {
            int pivotRow = pivotColumn;
            for (int row = pivotColumn + 1; row < size; row++)
                if (System.Math.Abs(reduced[row, pivotColumn]) > System.Math.Abs(reduced[pivotRow, pivotColumn])) pivotRow = row;
            if (System.Math.Abs(reduced[pivotRow, pivotColumn]) < 1e-300) continue;   // singular column: its coefficient stays 0
            if (pivotRow != pivotColumn)
            {
                for (int column = 0; column < size; column++)
                    (reduced[pivotColumn, column], reduced[pivotRow, column]) = (reduced[pivotRow, column], reduced[pivotColumn, column]);
                (values[pivotColumn], values[pivotRow]) = (values[pivotRow], values[pivotColumn]);
            }
            for (int row = pivotColumn + 1; row < size; row++)
            {
                double factor = reduced[row, pivotColumn] / reduced[pivotColumn, pivotColumn];
                if (factor == 0) continue;
                for (int column = pivotColumn; column < size; column++) reduced[row, column] -= factor * reduced[pivotColumn, column];
                values[row] -= factor * values[pivotColumn];
            }
        }
        var solution = new double[size];
        for (int row = size - 1; row >= 0; row--)
        {
            if (System.Math.Abs(reduced[row, row]) < 1e-300) { solution[row] = 0; continue; }
            double remainder = values[row];
            for (int column = row + 1; column < size; column++) remainder -= reduced[row, column] * solution[column];
            solution[row] = remainder / reduced[row, row];
        }
        return solution;
    }
}
