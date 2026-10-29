// Proof of concept for task 081: how fast and how big is one ONNX embedding model on this CPU.
// Usage: dotnet run -- <model.onnx> <text|image> [runs=100] [seqLen=32]
// The inputs are synthetic (random token ids or pixels), so this measures speed and size only, not quality.
using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: SpeedProbe <model.onnx> <text|image> [runs=100] [seqLen=32]");
    return 2;
}

if (args[0] == "scan")
{
    // Brute-force search over stored vectors: args are "scan <count> <dimensions>".
    return Scan(int.Parse(args[1]), int.Parse(args[2]));
}

var modelPath = args[0];
var kind = args[1];
var runs = args.Length > 2 ? int.Parse(args[2]) : 100;
var seqLen = args.Length > 3 ? int.Parse(args[3]) : 32;
var random = new Random(42);
var fileMegabytes = new FileInfo(modelPath).Length / 1024.0 / 1024.0;

using var options = new SessionOptions();
options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
using var session = new InferenceSession(modelPath, options);

Console.WriteLine($"model: {Path.GetFileName(modelPath)} ({fileMegabytes:F1} MB), kind: {kind}");
foreach (var input in session.InputMetadata)
{
    Console.WriteLine($"  input  {input.Key}: {string.Join("x", input.Value.Dimensions)} {input.Value.ElementDataType}");
}

var feeds = kind == "text" ? TextFeeds(session, seqLen, random) : ImageFeeds(session, random);

// Warm-up, then timed runs. Each run is one item (batch size 1), like a single query or a single picture.
for (var i = 0; i < 3; i++)
{
    using var warm = session.Run(feeds);
}

var times = new List<double>(runs);
long outputLength = 0;
for (var i = 0; i < runs; i++)
{
    var clock = Stopwatch.StartNew();
    using var results = session.Run(feeds);
    clock.Stop();
    times.Add(clock.Elapsed.TotalMilliseconds);
    if (i == 0)
    {
        outputLength = results.First().AsTensor<float>().Length;
    }
}

times.Sort();
var mean = times.Average();
var median = times[times.Count / 2];
Console.WriteLine($"  runs: {runs}, mean {mean:F1} ms, median {median:F1} ms, p90 {times[(int)(times.Count * 0.9)]:F1} ms");
Console.WriteLine($"  output vector length (first output): {outputLength}");
Console.WriteLine($"  threads: {Environment.ProcessorCount} logical CPUs");
return 0;

static List<NamedOnnxValue> TextFeeds(InferenceSession session, int seqLen, Random random)
{
    var feeds = new List<NamedOnnxValue>();
    foreach (var input in session.InputMetadata)
    {
        var shape = new[] { 1, seqLen };
        var values = new long[seqLen];
        for (var i = 0; i < seqLen; i++)
        {
            values[i] = input.Key.Contains("mask", StringComparison.OrdinalIgnoreCase)
                ? 1
                : input.Key.Contains("type", StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : random.Next(5, 5000);
        }

        feeds.Add(NamedOnnxValue.CreateFromTensor(input.Key, new DenseTensor<long>(values, shape)));
    }

    return feeds;
}

static List<NamedOnnxValue> ImageFeeds(InferenceSession session, Random random)
{
    var feeds = new List<NamedOnnxValue>();
    foreach (var input in session.InputMetadata)
    {
        // The model has dynamic dimensions; CLIP takes one RGB picture of 224x224 pixels.
        var dims = new[] { 1, 3, 224, 224 };
        var values = new float[dims.Aggregate(1, (a, b) => a * b)];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (float)(random.NextDouble() * 2 - 1);
        }

        feeds.Add(NamedOnnxValue.CreateFromTensor(input.Key, new DenseTensor<float>(values, dims)));
    }

    return feeds;
}

static int Scan(int count, int dimensions)
{
    var random = new Random(7);
    var vectors = new float[count * dimensions];
    for (var i = 0; i < vectors.Length; i++)
    {
        vectors[i] = (float)(random.NextDouble() * 2 - 1);
    }

    var scores = new float[count];
    var times = new List<double>();
    for (var query = 0; query < 30; query++)
    {
        var q = new float[dimensions];
        for (var i = 0; i < dimensions; i++)
        {
            q[i] = (float)(random.NextDouble() * 2 - 1);
        }

        var clock = Stopwatch.StartNew();
        for (var row = 0; row < count; row++)
        {
            scores[row] = System.Numerics.Tensors.TensorPrimitives.Dot(
                vectors.AsSpan(row * dimensions, dimensions), q);
        }

        var best = scores.Select((score, index) => (score, index)).OrderByDescending(p => p.score).First();
        clock.Stop();
        times.Add(clock.Elapsed.TotalMilliseconds);
        if (best.index < 0)
        {
            return 1;
        }
    }

    times.Sort();
    Console.WriteLine($"scan: {count} vectors x {dimensions} floats ({count * dimensions * 4.0 / 1024 / 1024:F1} MB in memory)");
    Console.WriteLine($"  30 queries, mean {times.Average():F1} ms, median {times[times.Count / 2]:F1} ms (dot products + top-1)");
    return 0;
}
