using System.Text.Json;
using System.Text.Json.Serialization;
using AbilityKit.Core.Recording.FrameRecord;

namespace AbilityKit.Record.Tools;

internal static class Program
{
    private const int ExitMatched = 0;
    private const int ExitDiverged = 1;
    private const int ExitError = 2;

    public static int Main(string[] args)
    {
        try
        {
            if (!TryParse(args, out var options, out var error))
            {
                Console.Error.WriteLine(error);
                WriteUsage();
                return ExitError;
            }

            var codec = new FrameRecordOptimizedBinaryCodec();
            var left = codec.Load(options.LeftPath);
            var right = codec.Load(options.RightPath);
            var report = new FrameRecordDiffAnalyzer().Compare(
                left,
                right,
                new FrameRecordDiffOptions { ContextFrames = options.ContextFrames });
            var json = JsonSerializer.Serialize(report, CreateJsonOptions(options.Indented));

            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                Console.Out.WriteLine(json);
            }
            else
            {
                var fullOutputPath = Path.GetFullPath(options.OutputPath);
                var directory = Path.GetDirectoryName(fullOutputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(fullOutputPath, json + Environment.NewLine);
            }

            return report.Matched ? ExitMatched : ExitDiverged;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return ExitError;
        }
    }

    private static JsonSerializerOptions CreateJsonOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static bool TryParse(
        string[] args,
        out DiffCommandOptions options,
        out string error)
    {
        options = new DiffCommandOptions();
        error = string.Empty;
        if (args.Length < 3 || !string.Equals(args[0], "diff", StringComparison.OrdinalIgnoreCase))
        {
            error = "Expected the diff command and two record paths.";
            return false;
        }

        options.LeftPath = args[1];
        options.RightPath = args[2];
        for (var i = 3; i < args.Length; i++)
        {
            var argument = args[i];
            if (string.Equals(argument, "--indented", StringComparison.OrdinalIgnoreCase))
            {
                options.Indented = true;
                continue;
            }

            if (string.Equals(argument, "--context", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length || !int.TryParse(args[i], out var contextFrames) || contextFrames < 0)
                {
                    error = "--context requires a non-negative integer.";
                    return false;
                }

                options.ContextFrames = contextFrames;
                continue;
            }

            if (string.Equals(argument, "--output", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]))
                {
                    error = "--output requires a file path.";
                    return false;
                }

                options.OutputPath = args[i];
                continue;
            }

            error = $"Unknown argument: {argument}";
            return false;
        }

        if (!File.Exists(options.LeftPath))
        {
            error = $"Left record does not exist: {options.LeftPath}";
            return false;
        }

        if (!File.Exists(options.RightPath))
        {
            error = $"Right record does not exist: {options.RightPath}";
            return false;
        }

        return true;
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine(
            "Usage: AbilityKit.Record.Tools diff <left.record.bin> <right.record.bin> [--context N] [--output report.json] [--indented]");
    }

    private sealed class DiffCommandOptions
    {
        public string LeftPath { get; set; } = string.Empty;
        public string RightPath { get; set; } = string.Empty;
        public int ContextFrames { get; set; } = 2;
        public string OutputPath { get; set; } = string.Empty;
        public bool Indented { get; set; }
    }
}
