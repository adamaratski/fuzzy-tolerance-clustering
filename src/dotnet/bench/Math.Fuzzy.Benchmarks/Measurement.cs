using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MathFuzzy.Benchmarks;

/// <summary>One timed configuration of one experiment.</summary>
public sealed record Measurement(
    string Experiment,
    string Variant,
    string Dataset,
    int ObjectCount,
    int Cores,
    int Repeats,
    double MedianMilliseconds,
    double MinMilliseconds,
    double MaxMilliseconds,
    bool Extrapolated = false,
    string? Note = null);

/// <summary>Wall-clock timing: one warm-up run, then the median of several runs.</summary>
public static class WallClock
{
    public static (double Median, double Min, double Max) Measure(Action action, int repeats)
    {
        action(); // warm-up: tiered JIT, caches, thread-pool growth
        var samples = new double[repeats];
        for (int repeat = 0; repeat < repeats; repeat++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            samples[repeat] = stopwatch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        return (samples[repeats / 2], samples[0], samples[^1]);
    }

    /// <summary>A single run without warm-up, for configurations that take minutes.</summary>
    public static double MeasureOnce(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        return stopwatch.Elapsed.TotalMilliseconds;
    }
}

/// <summary>Appends measurements to a JSON-lines file and echoes them to the console.</summary>
public sealed class MeasurementLog(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Add(Measurement measurement)
    {
        File.AppendAllText(path, JsonSerializer.Serialize(measurement, JsonOptions) + Environment.NewLine);
        Console.WriteLine(
            $"{measurement.Experiment,-10} {measurement.Variant,-38} {measurement.Dataset,-10} n={measurement.ObjectCount,-5} " +
            $"cores={measurement.Cores,-3} median={measurement.MedianMilliseconds,12:F2} ms" +
            (measurement.Extrapolated ? "  (extrapolated)" : "") +
            (measurement.Note is null ? "" : $"  [{measurement.Note}]"));
    }
}
