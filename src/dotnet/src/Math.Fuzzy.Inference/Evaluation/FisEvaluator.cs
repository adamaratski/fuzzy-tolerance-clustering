using MathFuzzy.Core.Matrix;
using MathFuzzy.Inference.Systems;

namespace MathFuzzy.Inference.Evaluation;

/// <summary>
/// Classification quality on a labelled sample. <see cref="Confusion"/>[true, predicted] has
/// two extra columns after the classes: ambiguous (tied best score, "double-classified")
/// and rejected (no evidence for any class).
/// </summary>
public sealed record ClassificationReport(
    int Correct,
    int Misclassified,
    int Ambiguous,
    int Rejected,
    int[,] Confusion,
    IReadOnlyList<string> Classes)
{
    public int Total => Correct + Misclassified + Ambiguous + Rejected;
    /// <summary>Share of objects assigned to their true class (ambiguous and rejected count as errors).</summary>
    public double Accuracy => Total == 0 ? 0 : (double)Correct / Total;
}

/// <summary>Regression quality; RMSE and MAE are over the objects on which some rule fired.</summary>
public sealed record RegressionReport(double Rmse, double Mae, int Evaluated, int NoFire);

public static class FisEvaluator
{
    /// <summary>
    /// Evaluates a classifying system on rows of <paramref name="inputs"/> against the true class
    /// name of every row. Classes are matched by name, so several outputs may stand for one
    /// class (two clusters with the same majority class). The confusion matrix is over the
    /// distinct names: system classes first, then names that occur only in the truth.
    /// </summary>
    public static ClassificationReport Classify(FuzzyInferenceSystem fis, RawDataMatrix inputs, IReadOnlyList<string> truth)
    {
        if (!fis.IsClassifying) throw new InvalidOperationException("The system does not produce class decisions.");
        if (truth.Count != inputs.Rows) throw new ArgumentException("One label per row expected.", nameof(truth));

        var classNames = fis.Classes.Concat(truth).Distinct().ToList();
        var indexOfName = classNames.Select((name, index) => (name, index)).ToDictionary(pair => pair.name, pair => pair.index);
        int classCount = classNames.Count;
        int ambiguousColumn = classCount, rejectedColumn = classCount + 1;
        var confusion = new int[classCount, classCount + 2];
        int correct = 0, misclassified = 0, ambiguous = 0, rejected = 0;

        for (int row = 0; row < inputs.Rows; row++)
        {
            var result = fis.Evaluate(inputs.GetRow(row));
            int trueClass = indexOfName[truth[row]];
            if (result.Ambiguous)               { ambiguous++; confusion[trueClass, ambiguousColumn]++; }
            else if (result.PredictedClass < 0) { rejected++;  confusion[trueClass, rejectedColumn]++;  }
            else
            {
                int predictedClass = indexOfName[fis.Classes[result.PredictedClass]];
                confusion[trueClass, predictedClass]++;
                if (predictedClass == trueClass) correct++; else misclassified++;
            }
        }
        return new ClassificationReport(correct, misclassified, ambiguous, rejected, confusion, classNames);
    }

    /// <summary>Evaluates the first output of a Mamdani / Sugeno system against target values.</summary>
    public static RegressionReport Regress(FuzzyInferenceSystem fis, RawDataMatrix inputs, IReadOnlyList<double> targets)
    {
        if (fis.Kind == FisKind.Classifier) throw new InvalidOperationException("Use Classify for classifiers.");
        if (targets.Count != inputs.Rows) throw new ArgumentException("One target per row expected.", nameof(targets));

        double squaredErrorSum = 0, absoluteErrorSum = 0;
        int evaluated = 0, noFire = 0;
        for (int row = 0; row < inputs.Rows; row++)
        {
            double prediction = fis.Evaluate(inputs.GetRow(row)).Output;
            if (double.IsNaN(prediction)) { noFire++; continue; }
            double error = prediction - targets[row];
            squaredErrorSum  += error * error;
            absoluteErrorSum += System.Math.Abs(error);
            evaluated++;
        }
        return evaluated == 0
            ? new RegressionReport(double.NaN, double.NaN, 0, noFire)
            : new RegressionReport(System.Math.Sqrt(squaredErrorSum / evaluated), absoluteErrorSum / evaluated, evaluated, noFire);
    }
}
