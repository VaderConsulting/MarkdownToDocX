using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Globalization;
using MermaidRecognition;
using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;
using MermaidSharp;
using SkiaSharp;
using Svg.Skia;

namespace SvgHarness;

internal static class Program
{
    private static readonly HttpClient _HttpClient = new HttpClient();
    private static readonly object _ConsoleLock = new object();
    private static readonly SemaphoreSlim _MermaidInkThrottle = new SemaphoreSlim(1, 1);
    private static readonly char[] _ViewBoxSplitChars = new[] { ' ', ',', '\t', '\r', '\n' };

    private sealed class MermaidBlock
    {
        public required int Index { get; init; }
        public required string ScenarioId { get; init; }
        public required string ScenarioTitle { get; init; }
        public required string Source { get; init; }
        public required string SourceLabel { get; init; }
        public required string DetectedDiagramType { get; init; }
    }

    private sealed class CompareResult
    {
        public required MermaidBlock Block { get; init; }
        public required bool Skipped { get; init; }
        public required bool NaiadSucceeded { get; init; }
        public required bool ReferenceSucceeded { get; init; }
        public required bool StructuralMatch { get; init; }
        public required bool VisualMatch { get; init; }
        public required bool MatchAfterNormalisation { get; init; }
        public required double VisualDiffRatio { get; init; }
        public required string NaiadSvgPath { get; init; }
        public required string ReferenceSvgPath { get; init; }
        public required string NativeSvgPath { get; init; }
        public required string Notes { get; init; }
    }

    private sealed class FlowchartSvgMetrics
    {
        public HashSet<string> NodeLabels { get; init; } = new HashSet<string>(StringComparer.Ordinal);
        public int EdgePathCount { get; init; }
        public int ClusterRectCount { get; init; }
    }

    private static int Main(string[] Args)
    {
        if (Args.Length >= 3 && string.Equals(Args[0], "--rasterise-svg", StringComparison.OrdinalIgnoreCase))
        {
            string inputSvgPath = Path.GetFullPath(Args[1]);
            string outputPngPath = Path.GetFullPath(Args[2]);

            if (!File.Exists(inputSvgPath))
            {
                Console.Error.WriteLine("SVG file not found: " + inputSvgPath);
                return 2;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPngPath) ?? ".");

            string? error = null;
            if (!TryRenderSvgToPng(inputSvgPath, outputPngPath, out error))
            {
                Console.Error.WriteLine("Rasterise failed: " + (error ?? "unknown"));
                return 3;
            }

            Console.WriteLine("Wrote: " + outputPngPath);
            return 0;
        }

        if (Args.Length < 2)
        {
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  dotnet run --project .\\src\\SvgHarness\\SvgHarness.csproj -- <markdownPath> <outputDirectory> [options]");
            Console.Error.WriteLine("  dotnet run --project .\\src\\SvgHarness\\SvgHarness.csproj -- --rasterise-svg <inputSvgPath> <outputPngPath>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --no-reference          Naiad render only (no mermaid.ink gate)");
            Console.Error.WriteLine("  --reference=mermaid-ink Reference via mermaid.ink (default)");
            Console.Error.WriteLine("  --reference=npx         Reference via @mermaid-js/mermaid-cli");
            Console.Error.WriteLine("  --report=<path>         Write F01-style markdown audit (default: <out>/report.md)");
            Console.Error.WriteLine("  --parallel=<n>          Parallel cases (default: 2 for mermaid-ink, 4 otherwise)");
            Console.Error.WriteLine("  --max-blocks=<n>        Limit blocks processed");
            Console.Error.WriteLine("  --emit-png              Keep PNG artefacts");
            Console.Error.WriteLine("  --emit-native           Write *.native.svg via Sugiyama layout (flowchart pilot)");
            return 2;
        }

        string markdownPath = Path.GetFullPath(Args[0]);
        string outputDirectory = Path.GetFullPath(Args[1]);
        bool noReference = HasFlag(Args, "--no-reference");
        bool emitPng = HasFlag(Args, "--emit-png");
        bool emitNative = HasFlag(Args, "--emit-native");
        string referenceMode = GetOptionValue(Args, "--reference") ?? "mermaid-ink";
        int maxBlocks = TryGetIntOptionValue(Args, "--max-blocks") ?? int.MaxValue;
        int defaultParallel = string.Equals(referenceMode, "mermaid-ink", StringComparison.OrdinalIgnoreCase) ? 2 : 4;
        int parallel = TryGetIntOptionValue(Args, "--parallel") ?? defaultParallel;
        string? reportPath = GetOptionValue(Args, "--report");
        if (noReference)
        {
            referenceMode = "none";
        }
        else if (string.Equals(referenceMode, "npx", StringComparison.OrdinalIgnoreCase) && !IsCommandAvailable("npx"))
        {
            referenceMode = "mermaid-ink";
            Console.WriteLine("Reference mode fallback: '--reference=npx' requested but 'npx' was not found on PATH.");
            Console.WriteLine("Falling back to Mermaid Ink reference rendering.");
            Console.WriteLine();
        }

        if (!File.Exists(markdownPath))
        {
            Console.Error.WriteLine("Markdown file not found: " + markdownPath);
            return 2;
        }

        Directory.CreateDirectory(outputDirectory);

        string markdown = File.ReadAllText(markdownPath);
        List<MermaidBlock> blocks = ExtractMermaidBlocks(markdown);
        if (blocks.Count == 0)
        {
            Console.Error.WriteLine("No Mermaid blocks found in: " + markdownPath);
            return 2;
        }

        if (blocks.Count > maxBlocks)
        {
            blocks = blocks.GetRange(0, maxBlocks);
        }

        List<CompareResult> results = new List<CompareResult>();
        results.AddRange(RunCases(blocks, outputDirectory, referenceMode, parallel, emitPng, emitNative));

        bool noReferenceMode = string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase);
        WriteSummary(results, noReferenceMode);
        WriteScopedTypeSummary(results);

        string auditPath = string.IsNullOrWhiteSpace(reportPath)
            ? Path.Combine(outputDirectory, "report.md")
            : Path.GetFullPath(reportPath);
        WriteAuditReport(results, auditPath, markdownPath, noReferenceMode);
        Console.WriteLine();
        Console.WriteLine("Audit report: " + auditPath);

        bool anyRenderFailed = results.Exists(
            Result => !Result.Skipped && (!Result.NaiadSucceeded || (!noReference && !Result.ReferenceSucceeded)));
        if (anyRenderFailed)
        {
            return 3;
        }

        bool anyMismatch = results.Exists(Result => !Result.Skipped && !Result.MatchAfterNormalisation);
        return anyMismatch ? 1 : 0;
    }

    private static List<CompareResult> RunCases(List<MermaidBlock> blocks, string outputDirectory, string referenceMode, int parallel, bool emitPng, bool emitNative)
    {
        int degree = Math.Max(1, Math.Min(16, parallel));
        CompareResult?[] resultsByIndex = new CompareResult?[blocks.Count];
        int completed = 0;

        ParallelOptions options = new ParallelOptions
        {
            MaxDegreeOfParallelism = degree
        };

        Parallel.For(0, blocks.Count, options, i =>
        {
            MermaidBlock block = blocks[i];
            CompareResult result = RunCase(block, outputDirectory, referenceMode, emitPng, emitNative);
            resultsByIndex[i] = result;

            int done = Interlocked.Increment(ref completed);
            if (done == 1 || done % 10 == 0 || done == blocks.Count)
            {
                lock (_ConsoleLock)
                {
                    Console.WriteLine($"Progress: {done}/{blocks.Count}");
                }
            }
        });

        List<CompareResult> results = new List<CompareResult>(blocks.Count);
        for (int i = 0; i < resultsByIndex.Length; i++)
        {
            if (resultsByIndex[i] is not null)
            {
                results.Add(resultsByIndex[i]!);
            }
        }

        return results;
    }

    private static CompareResult RunCase(MermaidBlock block, string outputDirectory, string referenceMode, bool emitPng, bool emitNative)
    {
        string prefix = $"{block.Index:D3}";

        MermaidRemarkAnalyzer.MermaidRemarkAnalysis RemarkAnalysis = MermaidRemarkAnalyzer.Analyze(block.Source);
        if (RemarkAnalysis.Status == MermaidDiagramRemarkStatus.CatalogueRemarked)
        {
            return new CompareResult
            {
                Block = block,
                Skipped = true,
                NaiadSucceeded = false,
                ReferenceSucceeded = false,
                StructuralMatch = true,
                VisualMatch = true,
                MatchAfterNormalisation = true,
                VisualDiffRatio = 0.0,
                NaiadSvgPath = string.Empty,
                ReferenceSvgPath = string.Empty,
                NativeSvgPath = string.Empty,
                Notes = "SKIP (catalogue-remarked): every line is a %% remark per §9.1"
            };
        }

        string naiadSvgPath = Path.Combine(outputDirectory, prefix + ".naiad.svg");
        string referenceSvgPath = Path.Combine(outputDirectory, prefix + ".reference.svg");
        string nativeSvgPath = Path.Combine(outputDirectory, prefix + ".native.svg");
        string naiadPngPath = Path.Combine(outputDirectory, prefix + ".naiad.png");
        string referencePngPath = Path.Combine(outputDirectory, prefix + ".reference.png");
        string inputPath = Path.Combine(outputDirectory, prefix + ".mmd");

        File.WriteAllText(inputPath, block.Source, Encoding.UTF8);

        bool naiadSucceeded = TryWriteNaiadSvg(block.Source, naiadSvgPath, out string? naiadError);
        string? referenceError = null;
        bool referenceSucceeded = string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase)
            ? false
            : TryWriteReferenceSvg(block.Source, inputPath, referenceSvgPath, referenceMode, out referenceError);

        string notes = string.Empty;
        if (emitNative)
        {
            bool nativeSucceeded = TryWriteNativeSvg(block.Source, nativeSvgPath, out string? nativeError);
            if (!nativeSucceeded && !string.IsNullOrEmpty(nativeError))
            {
                notes = "Native render: " + nativeError;
            }
        }
        else
        {
            nativeSvgPath = string.Empty;
        }

        if (!naiadSucceeded)
        {
            notes = string.IsNullOrEmpty(notes)
                ? "Naiad render failed: " + naiadError
                : notes + "; Naiad render failed: " + naiadError;
        }
        else if (!string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase) && !referenceSucceeded)
        {
            string referenceNote = "Reference render failed: " + referenceError;
            notes = string.IsNullOrEmpty(notes) ? referenceNote : notes + "; " + referenceNote;
        }

        if (emitPng && naiadSucceeded)
        {
            string? naiadPngError = null;
            TryRenderSvgToPng(naiadSvgPath, naiadPngPath, out naiadPngError);
        }

        bool structuralMatch = false;
        bool visualMatch = false;
        double visualDiffRatio = 1.0;
        bool match = false;
        if (naiadSucceeded && (string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase) || referenceSucceeded))
        {
            if (string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase))
            {
                structuralMatch = true;
                visualMatch = true;
                match = true;
            }
            else
            {
                string naiadSvg = File.ReadAllText(naiadSvgPath, Encoding.UTF8);
                string referenceSvg = File.ReadAllText(referenceSvgPath, Encoding.UTF8);
                structuralMatch = CompareFlowchartSvgStructure(naiadSvg, referenceSvg, out string structuralNotes);
                if (!structuralMatch)
                {
                    notes = structuralNotes;
                }

                bool useCoordinateGate = string.Equals(block.ScenarioId, "F53A", StringComparison.Ordinal);
                if (useCoordinateGate)
                {
                    bool coordinateMatch = F53AStadiumMergeCoordinateCompare.TryCompare(
                        naiadSvg,
                        referenceSvg,
                        out string coordinateNotes,
                        out double maxCoordinateDelta);
                    visualMatch = coordinateMatch;
                    visualDiffRatio = maxCoordinateDelta;
                    string coordinatePath = Path.Combine(outputDirectory, prefix + ".coordinates.txt");
                    File.WriteAllText(coordinatePath, coordinateNotes, Encoding.UTF8);
                    notes = string.IsNullOrEmpty(notes)
                        ? coordinateNotes.Replace("\r\n", "; ", StringComparison.Ordinal)
                        : notes + "; " + coordinateNotes.Replace("\r\n", "; ", StringComparison.Ordinal);
                }
                else
                {
                    string? naiadPngError = null;
                    string? refPngError = null;
                    if (TryRenderSvgToPng(naiadSvgPath, naiadPngPath, out naiadPngError)
                        && TryGetReferencePng(referenceMode, block.Source, referenceSvgPath, referencePngPath, out refPngError))
                    {
                        visualMatch = ComputePngVisualMatch(naiadPngPath, referencePngPath, out visualDiffRatio);
                        if (!visualMatch)
                        {
                            string visualNote = $"Visual diff ratio={visualDiffRatio:0.0000}";
                            notes = string.IsNullOrEmpty(notes) ? visualNote : notes + "; " + visualNote;
                        }
                    }
                    else
                    {
                        string pngNote = "PNG compare failed: " + (naiadPngError ?? refPngError ?? "unknown");
                        notes = string.IsNullOrEmpty(notes) ? pngNote : notes + "; " + pngNote;
                        visualMatch = false;
                    }
                }

                match = structuralMatch && visualMatch;
            }
        }

        return new CompareResult
        {
            Block = block,
            Skipped = false,
            NaiadSucceeded = naiadSucceeded,
            ReferenceSucceeded = referenceSucceeded || string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase),
            StructuralMatch = structuralMatch,
            VisualMatch = visualMatch,
            MatchAfterNormalisation = match,
            VisualDiffRatio = visualDiffRatio,
            NaiadSvgPath = naiadSvgPath,
            ReferenceSvgPath = referenceSvgPath,
            NativeSvgPath = nativeSvgPath,
            Notes = notes
        };
    }

    private static bool TryWriteNativeSvg(string input, string outputSvgPath, out string? error)
    {
        try
        {
            if (!NativeFlowchartRenderBridge.TryRenderFlowchartSvg(input, out string Svg))
            {
                error = "Native Sugiyama render failed or source is not a flowchart.";
                return false;
            }

            if (string.IsNullOrEmpty(Svg))
            {
                error = "Sugiyama layout produced empty SVG.";
                return false;
            }

            File.WriteAllText(outputSvgPath, Svg, Encoding.UTF8);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }

    private static bool TryWriteNaiadSvg(string input, string outputSvgPath, out string? error)
    {
        try
        {
            MemoryStream stream = Mermaid.RenderToMemoryStream(input, outputSvgPath, RenderOptions.Default);
            stream.Dispose();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }

    private static void WriteScopedTypeSummary(List<CompareResult> results)
    {
        // User-scoped types for current work session.
        string[] scopedTypes = new[]
        {
            "Flowchart",
            "Sequence",
            "Class",
            "State",
            "EntityRelationship",
            "Gantt",
            "Pie"
        };

        Dictionary<string, TypeBucket> buckets = new Dictionary<string, TypeBucket>(StringComparer.OrdinalIgnoreCase);
        foreach (string t in scopedTypes)
        {
            buckets[t] = new TypeBucket();
        }

        foreach (CompareResult result in results)
        {
            string detected = result.Block.DetectedDiagramType;
            if (!buckets.ContainsKey(detected))
            {
                continue;
            }

            TypeBucket bucket = buckets[detected];
            bucket.Total++;

            if (!result.NaiadSucceeded)
            {
                bucket.NaiadFailed++;
                continue;
            }

            if (!result.ReferenceSucceeded)
            {
                bucket.ReferenceFailed++;
                continue;
            }

            if (result.MatchAfterNormalisation)
            {
                bucket.Match++;
            }
            else
            {
                bucket.Diff++;
            }
        }

        Console.WriteLine();
        Console.WriteLine("Scoped diagram-type summary (in-scope types only)");
        foreach (string t in scopedTypes)
        {
            TypeBucket b = buckets[t];
            Console.WriteLine($"- {t}: total={b.Total} match={b.Match} diff={b.Diff} naiadFail={b.NaiadFailed} refFail={b.ReferenceFailed}");
        }
    }

    private sealed class TypeBucket
    {
        public int Total;
        public int Match;
        public int Diff;
        public int NaiadFailed;
        public int ReferenceFailed;
    }

    private static bool TryWriteReferenceSvg(string mermaidSource, string inputPath, string outputSvgPath, string referenceMode, out string? error)
    {
        try
        {
            if (string.Equals(referenceMode, "npx", StringComparison.OrdinalIgnoreCase))
            {
                return TryWriteReferenceSvgViaNpx(inputPath, outputSvgPath, out error);
            }

            if (string.Equals(referenceMode, "mermaid-ink", StringComparison.OrdinalIgnoreCase))
            {
                return TryWriteReferenceSvgViaMermaidInk(mermaidSource, outputSvgPath, out error);
            }

            error = "Unknown reference mode: " + referenceMode;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }

    private static bool TryWriteReferenceSvgViaNpx(string inputPath, string outputSvgPath, out string? error)
    {
        // Uses mermaid-cli (mmdc) via npx so we do not need a repo-local Node install.
        if (!IsCommandAvailable("npx"))
        {
            error = "npx not found on PATH.";
            return false;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "npx",
            Arguments = $"-y @mermaid-js/mermaid-cli mmdc -i \"{inputPath}\" -o \"{outputSvgPath}\" --quiet",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo)!)
        {
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                error = $"ExitCode={process.ExitCode} StdOut={stdout} StdErr={stderr}";
                return false;
            }
        }

        error = null;
        return File.Exists(outputSvgPath);
    }

    private static bool TryWriteReferenceSvgViaMermaidInk(string mermaidSource, string outputSvgPath, out string? error)
    {
        // Mermaid Ink API uses a deflated + base64url payload in the path.
        // Ref: https://mermaid.ink/ (behaviour matches Mermaid’s default rendering for practical parity checks).
        string payload = MermaidInkEncode(mermaidSource);
        string url = "https://mermaid.ink/svg/" + payload;

        const int MAX_ATTEMPTS = 8;
        int attempt = 0;
        while (attempt < MAX_ATTEMPTS)
        {
            attempt++;
            bool acquired = false;
            try
            {
                _MermaidInkThrottle.Wait();
                acquired = true;
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    HttpResponseMessage response = _HttpClient.Send(request, cts.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        if (IsRetryableStatusCode(response.StatusCode) && attempt < MAX_ATTEMPTS)
                        {
                            BackoffDelay(attempt);
                            continue;
                        }

                        error = "HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase;
                        return false;
                    }

                    string svg = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
                    if (string.IsNullOrWhiteSpace(svg) || !svg.Contains("<svg", StringComparison.OrdinalIgnoreCase))
                    {
                        error = "Mermaid Ink returned empty or non-SVG content.";
                        return false;
                    }

                    File.WriteAllText(outputSvgPath, svg, Encoding.UTF8);
                    error = null;
                    return File.Exists(outputSvgPath);
                }
            }
            catch (TaskCanceledException ex)
            {
                if (attempt < MAX_ATTEMPTS)
                {
                    BackoffDelay(attempt);
                    continue;
                }

                error = ex.ToString();
                return false;
            }
            catch (HttpRequestException ex)
            {
                if (attempt < MAX_ATTEMPTS)
                {
                    BackoffDelay(attempt);
                    continue;
                }

                error = ex.ToString();
                return false;
            }
            finally
            {
                if (acquired)
                {
                    _MermaidInkThrottle.Release();
                    Thread.Sleep(200);
                }
            }
        }

        error = "Mermaid Ink request failed after retries.";
        return false;
    }

    private static bool TryGetReferencePng(string referenceMode, string mermaidSource, string referenceSvgPath, string referencePngPath, out string? error)
    {
        // For Ink, fetch PNG directly so we do not depend on foreignObject SVG support in local renderers.
        if (string.Equals(referenceMode, "mermaid-ink", StringComparison.OrdinalIgnoreCase))
        {
            return TryWriteReferencePngViaMermaidInk(mermaidSource, referencePngPath, out error);
        }

        // For npx mode, we only have SVG; best-effort render to PNG.
        if (File.Exists(referenceSvgPath))
        {
            return TryRenderSvgToPng(referenceSvgPath, referencePngPath, out error);
        }

        error = "Reference SVG was not available.";
        return false;
    }

    private static bool TryWriteReferencePngViaMermaidInk(string mermaidSource, string outputPngPath, out string? error)
    {
        string payload = MermaidInkEncode(mermaidSource);
        string url = "https://mermaid.ink/img/" + payload + "?type=png";

        const int MAX_ATTEMPTS = 8;
        int attempt = 0;
        while (attempt < MAX_ATTEMPTS)
        {
            attempt++;
            bool acquired = false;
            try
            {
                _MermaidInkThrottle.Wait();
                acquired = true;
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    HttpResponseMessage response = _HttpClient.Send(request, cts.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        if (IsRetryableStatusCode(response.StatusCode) && attempt < MAX_ATTEMPTS)
                        {
                            BackoffDelay(attempt);
                            continue;
                        }

                        error = "HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase;
                        return false;
                    }

                    byte[] bytes = response.Content.ReadAsByteArrayAsync(cts.Token).GetAwaiter().GetResult();
                    if (bytes.Length < 16)
                    {
                        error = "Mermaid Ink returned empty PNG.";
                        return false;
                    }

                    File.WriteAllBytes(outputPngPath, bytes);
                    error = null;
                    return File.Exists(outputPngPath);
                }
            }
            catch (TaskCanceledException ex)
            {
                if (attempt < MAX_ATTEMPTS)
                {
                    BackoffDelay(attempt);
                    continue;
                }

                error = ex.ToString();
                return false;
            }
            catch (HttpRequestException ex)
            {
                if (attempt < MAX_ATTEMPTS)
                {
                    BackoffDelay(attempt);
                    continue;
                }

                error = ex.ToString();
                return false;
            }
            finally
            {
                if (acquired)
                {
                    _MermaidInkThrottle.Release();
                    Thread.Sleep(200);
                }
            }
        }

        error = "Mermaid Ink PNG request failed after retries.";
        return false;
    }

    private static bool TryParseSvgViewBox(string svgXml, out SKRect viewBox)
    {
        viewBox = SKRect.Empty;
        Match match = Regex.Match(svgXml, @"viewBox\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        if (!match.Success)
        {
            return false;
        }

        string[] parts = match.Groups[1].Value.Split(_ViewBoxSplitChars, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double w)
            || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double h))
        {
            return false;
        }

        viewBox = new SKRect((float)x, (float)y, (float)(x + w), (float)(y + h));
        return w > 0 && h > 0;
    }

    private static bool TryRenderSvgToPng(string inputSvgPath, string outputPngPath, out string? error)
    {
        try
        {
            string svgXml = File.ReadAllText(inputSvgPath, Encoding.UTF8);
            SKSvg svg = new SKSvg();
            svg.FromSvg(svgXml);

            SKPicture? picture = svg.Picture;
            if (picture is null)
            {
                error = "SVG parsed but produced no picture.";
                return false;
            }

            SKRect renderBounds = picture.CullRect;
            if (TryParseSvgViewBox(svgXml, out SKRect viewBox))
            {
                renderBounds = viewBox;
            }

            int width = (int)Math.Ceiling(Math.Max(1.0, renderBounds.Width));
            int height = (int)Math.Ceiling(Math.Max(1.0, renderBounds.Height));

            using (SKBitmap bitmap = new SKBitmap(width, height))
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                canvas.Translate(-renderBounds.Left, -renderBounds.Top);
                canvas.DrawPicture(picture);
                canvas.Flush();

                using (SKImage image = SKImage.FromBitmap(bitmap))
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    using (FileStream fileStream = new FileStream(outputPngPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        data.SaveTo(fileStream);
                        fileStream.Flush();
                    }
                }
            }

            error = null;
            return File.Exists(outputPngPath);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }

    private static SKBitmap CropToContentBounds(SKBitmap source, int marginPx)
    {
        int minX = source.Width;
        int minY = source.Height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                if (!IsNearWhite(source.GetPixel(x, y)))
                {
                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }
        }

        if (maxX < 0 || maxY < 0)
        {
            return source.Copy();
        }

        minX = Math.Max(0, minX - marginPx);
        minY = Math.Max(0, minY - marginPx);
        maxX = Math.Min(source.Width - 1, maxX + marginPx);
        maxY = Math.Min(source.Height - 1, maxY + marginPx);

        int cropWidth = Math.Max(1, maxX - minX + 1);
        int cropHeight = Math.Max(1, maxY - minY + 1);
        SKBitmap cropped = new SKBitmap(cropWidth, cropHeight);
        using (SKCanvas canvas = new SKCanvas(cropped))
        {
            canvas.Clear(SKColors.White);
            SKRect sourceRect = new SKRect(minX, minY, maxX + 1, maxY + 1);
            SKRect destRect = new SKRect(0, 0, cropWidth, cropHeight);
            canvas.DrawBitmap(source, sourceRect, destRect);
        }

        return cropped;
    }

    private static SKBitmap ScaleBitmapToSize(SKBitmap source, int width, int height)
    {
        SKBitmap scaled = new SKBitmap(width, height);
        using (SKCanvas canvas = new SKCanvas(scaled))
        {
            canvas.Clear(SKColors.White);
            SKRect dest = new SKRect(0, 0, width, height);
            SKRect sourceRect = new SKRect(0, 0, source.Width, source.Height);
            canvas.DrawBitmap(source, sourceRect, dest);
        }

        return scaled;
    }

    private static bool ComputePngVisualMatch(string naiadPngPath, string referencePngPath, out double diffRatio)
    {
        // Crop to diagram ink, scale both to the same canvas, then compare non-white pixels only.
        const double CHANNEL_DIFF_THRESHOLD = 0.18;
        const double PIXEL_MISMATCH_RATIO_THRESHOLD = 0.0025;
        const int CONTENT_MARGIN_PX = 4;

        using (SKBitmap aRaw = SKBitmap.Decode(naiadPngPath))
        using (SKBitmap bRaw = SKBitmap.Decode(referencePngPath))
        using (SKBitmap aCrop = CropToContentBounds(aRaw, CONTENT_MARGIN_PX))
        using (SKBitmap bCrop = CropToContentBounds(bRaw, CONTENT_MARGIN_PX))
        {
            int width = Math.Max(aCrop.Width, bCrop.Width);
            int height = Math.Max(aCrop.Height, bCrop.Height);
            if (width <= 0 || height <= 0)
            {
                diffRatio = 1.0;
                return false;
            }

            using (SKBitmap a = ScaleBitmapToSize(aCrop, width, height))
            using (SKBitmap b = ScaleBitmapToSize(bCrop, width, height))
            {
                long mismatched = 0;
                long compared = 0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        SKColor ca = a.GetPixel(x, y);
                        SKColor cb = b.GetPixel(x, y);
                        if (IsNearWhite(ca) && IsNearWhite(cb))
                        {
                            continue;
                        }

                        compared++;
                        double dr = Math.Abs(ca.Red - cb.Red) / 255.0;
                        double dg = Math.Abs(ca.Green - cb.Green) / 255.0;
                        double db = Math.Abs(ca.Blue - cb.Blue) / 255.0;

                        if (dr > CHANNEL_DIFF_THRESHOLD || dg > CHANNEL_DIFF_THRESHOLD || db > CHANNEL_DIFF_THRESHOLD)
                        {
                            mismatched++;
                        }
                    }
                }

                if (compared == 0)
                {
                    diffRatio = 1.0;
                    return false;
                }

                diffRatio = (double)mismatched / compared;
                return diffRatio <= PIXEL_MISMATCH_RATIO_THRESHOLD;
            }
        }
    }

    private static bool IsNearWhite(SKColor color)
    {
        return color.Red >= 250 && color.Green >= 250 && color.Blue >= 250;
    }

    private static bool IsRetryableStatusCode(System.Net.HttpStatusCode statusCode)
    {
        int code = (int)statusCode;
        return code == 429 || code == 500 || code == 502 || code == 503 || code == 504;
    }

    private static void BackoffDelay(int attempt)
    {
        // Exponential backoff with a small cap; keep overall harness runtime reasonable.
        int delayMs = attempt switch
        {
            1 => 250,
            2 => 750,
            3 => 1500,
            _ => 2000
        };
        Thread.Sleep(delayMs);
    }

    private static string MermaidInkEncode(string mermaidSource)
    {
        // Mermaid Ink expects:
        // - JSON payload: { code: "...", mermaid: { theme: "default" } }
        // - zlib/deflate compressed bytes
        // - base64url (no padding)
        // - prefixed with "pako:"
        // Example URL:
        //   https://mermaid.ink/svg/pako:<base64url(zlib(json))>
        string json = JsonSerializer.Serialize(new
        {
            code = mermaidSource,
            mermaid = new
            {
                theme = "default"
            }
        });

        byte[] inputBytes = Encoding.UTF8.GetBytes(json);
        byte[] deflated = ZlibDeflate(inputBytes);
        return "pako:" + Base64UrlEncode(deflated);
    }

    private static byte[] ZlibDeflate(byte[] inputBytes)
    {
        using (MemoryStream output = new MemoryStream())
        {
            // ZLibStream emits zlib header+deflate+adler32, which matches Mermaid Ink expectations.
            using (ZLibStream zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zlib.Write(inputBytes, 0, inputBytes.Length);
            }
            return output.ToArray();
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        string b64 = Convert.ToBase64String(bytes);
        string url = b64.Replace('+', '-').Replace('/', '_');
        url = url.TrimEnd('=');
        return url;
    }

    private static List<MermaidBlock> ExtractMermaidBlocks(string markdown)
    {
        // Supports:
        // - fenced code blocks: ```mermaid ... ```
        // - ":::mermaid" blocks: :::mermaid ... :::
        List<(int Position, string ScenarioId, string ScenarioTitle)> headers = ParseScenarioHeaders(markdown);
        List<MermaidBlock> blocks = new List<MermaidBlock>();

        foreach (MermaidBlock block in ExtractFencedBlocks(markdown, headers))
        {
            blocks.Add(block);
        }

        foreach (MermaidBlock block in ExtractColonBlocks(markdown, startToken: ":::mermaid", endToken: ":::", headers))
        {
            blocks.Add(block);
        }

        blocks.Sort((a, b) => a.Index.CompareTo(b.Index));
        return blocks;
    }

    private static List<(int Position, string ScenarioId, string ScenarioTitle)> ParseScenarioHeaders(string markdown)
    {
        List<(int Position, string ScenarioId, string ScenarioTitle)> headers =
            new List<(int, string, string)>();
        Regex headerRegex = new Regex(
            @"^###\s+(?<Id>(?:ST|F|S|C|E)\d{2}[A-Z]?)\s+(?<Title>.+)$",
            RegexOptions.Compiled | RegexOptions.Multiline);
        MatchCollection matches = headerRegex.Matches(markdown);
        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            headers.Add((match.Index, match.Groups["Id"].Value, match.Groups["Title"].Value.Trim()));
        }

        return headers;
    }

    private static (string ScenarioId, string ScenarioTitle) ResolveScenarioForBlockIndex(
        int blockIndex,
        List<(int Position, string ScenarioId, string ScenarioTitle)> headers)
    {
        string scenarioId = $"BLOCK-{blockIndex + 1:D2}";
        string scenarioTitle = string.Empty;
        if (headers.Count == 0)
        {
            return (scenarioId, scenarioTitle);
        }

        if (blockIndex < headers.Count)
        {
            return (headers[blockIndex].ScenarioId, headers[blockIndex].ScenarioTitle);
        }

        return (headers[headers.Count - 1].ScenarioId, headers[headers.Count - 1].ScenarioTitle);
    }

    private static IEnumerable<MermaidBlock> ExtractFencedBlocks(
        string markdown,
        List<(int Position, string ScenarioId, string ScenarioTitle)> headers)
    {
        Regex regex = new Regex(@"(?s)```mermaid\s*\r?\n(?<Body>.*?)(\r?\n```)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        MatchCollection matches = regex.Matches(markdown);
        for (int i = 0; i < matches.Count; i++)
        {
            string body = matches[i].Groups["Body"].Value.Trim();
            string detectedType = DetectDiagramTypeSafe(body);
            (string scenarioId, string scenarioTitle) = ResolveScenarioForBlockIndex(i, headers);
            yield return new MermaidBlock
            {
                Index = i,
                ScenarioId = scenarioId,
                ScenarioTitle = scenarioTitle,
                Source = body,
                SourceLabel = scenarioId + " " + scenarioTitle,
                DetectedDiagramType = detectedType
            };
        }
    }

    private static IEnumerable<MermaidBlock> ExtractColonBlocks(
        string markdown,
        string startToken,
        string endToken,
        List<(int Position, string ScenarioId, string ScenarioTitle)> headers)
    {
        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        bool inBlock = false;
        StringBuilder current = new StringBuilder();
        int index = 100000; // offset to avoid collisions with fenced blocks; final sort uses Index
        int colonBlockOrdinal = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (!inBlock)
            {
                if (string.Equals(line.Trim(), startToken, StringComparison.OrdinalIgnoreCase))
                {
                    inBlock = true;
                    current.Clear();
                }
            }
            else
            {
                if (string.Equals(line.Trim(), endToken, StringComparison.OrdinalIgnoreCase))
                {
                    string body = current.ToString().Trim();
                    string detectedType = DetectDiagramTypeSafe(body);
                    (string scenarioId, string scenarioTitle) = ResolveScenarioForBlockIndex(colonBlockOrdinal, headers);
                    yield return new MermaidBlock
                    {
                        Index = index,
                        ScenarioId = scenarioId,
                        ScenarioTitle = scenarioTitle,
                        Source = body,
                        SourceLabel = scenarioId + " " + scenarioTitle,
                        DetectedDiagramType = detectedType
                    };
                    index++;
                    colonBlockOrdinal++;
                    inBlock = false;
                    current.Clear();
                }
                else
                {
                    current.AppendLine(line);
                }
            }
        }
    }

    private static void WriteSummary(List<CompareResult> results, bool noReference)
    {
        int matchCount = 0;
        int mismatchCount = 0;
        int failCount = 0;
        int skipCount = 0;

        for (int i = 0; i < results.Count; i++)
        {
            CompareResult result = results[i];
            if (result.Skipped)
            {
                skipCount++;
            }
            else if (!result.NaiadSucceeded || (!noReference && !result.ReferenceSucceeded))
            {
                failCount++;
            }
            else if (result.MatchAfterNormalisation)
            {
                matchCount++;
            }
            else
            {
                mismatchCount++;
            }
        }

        Console.WriteLine("SVG harness results");
        Console.WriteLine("- Total: " + results.Count);
        Console.WriteLine("- Match: " + matchCount);
        Console.WriteLine("- Mismatch: " + mismatchCount);
        Console.WriteLine("- Failed: " + failCount);
        Console.WriteLine("- Skipped (catalogue-remarked): " + skipCount);
        Console.WriteLine();

        for (int i = 0; i < results.Count; i++)
        {
            CompareResult result = results[i];
            string status;
            if (result.Skipped)
            {
                status = "SKIP (remarked)";
            }
            else if (!result.NaiadSucceeded)
            {
                status = "FAIL (naiad)";
            }
            else if (!noReference && !result.ReferenceSucceeded)
            {
                status = "FAIL (reference)";
            }
            else
            {
                status = result.MatchAfterNormalisation ? "OK" : "DIFF";
            }

            Console.WriteLine($"{result.Block.Index:D3} {status} {result.Block.ScenarioId} {result.Block.ScenarioTitle} {result.Notes}");
            if (!result.MatchAfterNormalisation && result.NaiadSucceeded && (noReference || result.ReferenceSucceeded))
            {
                Console.WriteLine("  Naiad: " + result.NaiadSvgPath);
                if (!noReference)
                {
                    Console.WriteLine("  Ref:   " + result.ReferenceSvgPath);
                }
            }
        }
    }

    /// <summary>Word-review fix order (F58 last within this queue).</summary>
    private static readonly string[] _WordReviewPriorityScenarioIds =
    {
        "F24", "F29", "F30", "F31", "F32", "F34", "F35", "F37", "F38", "F40",
        "F43", "F48", "F53", "F59", "F60", "F58"
    };

    private static readonly Dictionary<string, string> _WordReviewPriorityNotes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["F24"] = "Edge labels (short).",
        ["F29"] = "Binary decision (Yes / No).",
        ["F30"] = "Multi-way decision.",
        ["F31"] = "Fan-out (one to many).",
        ["F32"] = "Fan-in (many to one).",
        ["F34"] = "Retry loop.",
        ["F35"] = "Diamond with multiple incoming edges.",
        ["F37"] = "Subgraph with explicit `direction TB`.",
        ["F38"] = "Subgraph with `direction LR` inside TB diagram.",
        ["F40"] = "Two sibling subgraphs — allow interior nodes to flow left–right (LR).",
        ["F43"] = "Nested subgraphs; cross-boundary edges.",
        ["F48"] = "Incorrect label text (entity codes / display).",
        ["F53"] = "`style` directive — same issue class as F29 (decisions / labels).",
        ["F53A"] = "Isolated Success/Error → End merge; gate on SVG coordinates vs mermaid.live (not PNG).",
        ["F59"] = "Subgraph + decision + retry loop.",
        ["F60"] = "LR diagram with nested TB and cross-links (integration).",
        ["F58"] = "Full pipeline — defer until decision/subgraph fixes above land."
    };

    private static void WriteAuditReport(List<CompareResult> results, string reportPath, string markdownPath, bool noReference)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("# Flowchart mermaid.ink audit");
        report.AppendLine();
        report.AppendLine("Generated by SvgHarness. **Gate:** every scenario must pass structural + visual comparison against [mermaid.ink](https://mermaid.ink) (same engine as Mermaid Live).");
        report.AppendLine();
        report.AppendLine("- Source: `" + markdownPath + "`");
        report.AppendLine("- Scenarios: **" + results.Count + "** (one isolated diagram per capability; no single diagram can cover all axes)");
        report.AppendLine();

        int pass = 0;
        int fail = 0;
        int diff = 0;
        int skip = 0;
        for (int i = 0; i < results.Count; i++)
        {
            CompareResult result = results[i];
            if (result.Skipped)
            {
                skip++;
            }
            else if (!result.NaiadSucceeded || (!noReference && !result.ReferenceSucceeded))
            {
                fail++;
            }
            else if (result.MatchAfterNormalisation)
            {
                pass++;
            }
            else
            {
                diff++;
            }
        }

        report.AppendLine("## Summary");
        report.AppendLine();
        report.AppendLine("| Result | Count |");
        report.AppendLine("| --- | ---: |");
        report.AppendLine("| PASS (structural + visual) | " + pass + " |");
        report.AppendLine("| DIFF | " + diff + " |");
        report.AppendLine("| FAIL (render) | " + fail + " |");
        report.AppendLine("| SKIP (catalogue-remarked `%%`) | " + skip + " |");
        report.AppendLine();

        Dictionary<string, CompareResult> resultsByScenarioId = new Dictionary<string, CompareResult>(StringComparer.Ordinal);
        for (int i = 0; i < results.Count; i++)
        {
            resultsByScenarioId[results[i].Block.ScenarioId] = results[i];
        }

        report.AppendLine("## Priority fix queue (Word review)");
        report.AppendLine();
        report.AppendLine("Fix in this order first (from `flow.docx` review). Remaining scenarios follow below, sorted by visual diff ratio.");
        report.AppendLine();
        report.AppendLine("| Order | ID | Fix focus | Structural | Visual | Diff ratio | Harness notes |");
        report.AppendLine("| ---: | --- | --- | --- | --- | ---: | --- |");

        HashSet<string> priorityIds = new HashSet<string>(_WordReviewPriorityScenarioIds, StringComparer.Ordinal);
        for (int order = 0; order < _WordReviewPriorityScenarioIds.Length; order++)
        {
            string scenarioId = _WordReviewPriorityScenarioIds[order];
            if (!resultsByScenarioId.TryGetValue(scenarioId, out CompareResult? result) || result is null)
            {
                continue;
            }

            string fixFocus = _WordReviewPriorityNotes.TryGetValue(scenarioId, out string? focus)
                ? focus
                : string.Empty;
            AppendPriorityQueueRow(report, order + 1, result, fixFocus, noReference);
        }

        report.AppendLine();
        report.AppendLine("## Per scenario");
        report.AppendLine();
        report.AppendLine("### Priority queue (same order as above)");
        report.AppendLine();
        report.AppendLine("| ID | Index | Structural | Visual | Diff ratio | Status | Notes |");
        report.AppendLine("| --- | ---: | --- | --- | ---: | --- | --- |");

        List<CompareResult> priorityResults = new List<CompareResult>();
        for (int i = 0; i < _WordReviewPriorityScenarioIds.Length; i++)
        {
            string scenarioId = _WordReviewPriorityScenarioIds[i];
            if (resultsByScenarioId.TryGetValue(scenarioId, out CompareResult? result) && result is not null)
            {
                priorityResults.Add(result);
            }
        }

        AppendAuditResultRows(report, priorityResults, noReference);

        report.AppendLine();
        report.AppendLine("### All other scenarios (visual diff ratio, largest first)");
        report.AppendLine();
        report.AppendLine("| ID | Index | Structural | Visual | Diff ratio | Status | Notes |");
        report.AppendLine("| --- | ---: | --- | --- | ---: | --- | --- |");

        List<CompareResult> remainingResults = new List<CompareResult>();
        for (int i = 0; i < results.Count; i++)
        {
            CompareResult result = results[i];
            if (!priorityIds.Contains(result.Block.ScenarioId))
            {
                remainingResults.Add(result);
            }
        }

        if (!noReference)
        {
            remainingResults.Sort((a, b) => b.VisualDiffRatio.CompareTo(a.VisualDiffRatio));
        }

        AppendAuditResultRows(report, remainingResults, noReference);

        report.AppendLine();
        report.AppendLine("## Coverage rule");
        report.AppendLine();
        report.AppendLine("Do not treat F58/F60 or a shape row as sufficient. **All 60 rows above must PASS** before flowchart work is merge-ready.");
        report.AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? ".");
        File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
    }

    private static void AppendPriorityQueueRow(
        StringBuilder report,
        int order,
        CompareResult result,
        string fixFocus,
        bool noReference)
    {
        string structural = noReference ? "n/a" : (result.StructuralMatch ? "yes" : "no");
        string visual = noReference ? "n/a" : (result.VisualMatch ? "yes" : "no");
        string diffRatio = noReference
            ? ""
            : result.VisualDiffRatio.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
        string harnessNotes = result.Notes.Replace("|", "\\|", StringComparison.Ordinal);
        string fixFocusEscaped = fixFocus.Replace("|", "\\|", StringComparison.Ordinal);
        report.AppendLine("| "
            + order + " | "
            + result.Block.ScenarioId + " | "
            + fixFocusEscaped + " | "
            + structural + " | "
            + visual + " | "
            + diffRatio + " | "
            + harnessNotes + " |");
    }

    private static void AppendAuditResultRows(StringBuilder report, List<CompareResult> orderedResults, bool noReference)
    {
        for (int i = 0; i < orderedResults.Count; i++)
        {
            CompareResult result = orderedResults[i];
            string status;
            if (!result.NaiadSucceeded)
            {
                status = "FAIL naiad";
            }
            else if (!noReference && !result.ReferenceSucceeded)
            {
                status = "FAIL reference";
            }
            else if (result.MatchAfterNormalisation)
            {
                status = "PASS";
            }
            else
            {
                status = "DIFF";
            }

            string structural = noReference ? "n/a" : (result.StructuralMatch ? "yes" : "no");
            string visual = noReference ? "n/a" : (result.VisualMatch ? "yes" : "no");
            string diffRatio = noReference
                ? ""
                : result.VisualDiffRatio.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
            string notes = result.Notes.Replace("|", "\\|", StringComparison.Ordinal);
            report.AppendLine("| "
                + result.Block.ScenarioId + " | "
                + result.Block.Index + " | "
                + structural + " | "
                + visual + " | "
                + diffRatio + " | "
                + status + " | "
                + notes + " |");
        }
    }

    private static string StripSvgStyleBlocks(string svg)
    {
        return Regex.Replace(
            svg,
            @"<style[^>]*>[\s\S]*?</style>",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static FlowchartSvgMetrics ExtractFlowchartSvgMetrics(string svg)
    {
        HashSet<string> labels = new HashSet<string>(StringComparer.Ordinal);
        string svgBody = StripSvgStyleBlocks(svg);

        void AddLabel(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            string label = System.Net.WebUtility.HtmlDecode(raw.Trim());
            if (!string.IsNullOrEmpty(label))
            {
                labels.Add(label);
            }
        }

        Regex nodeLabelBlockRegex = new Regex(
            @"class=""nodeLabel""[^>]*>([\s\S]*?)</text>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        MatchCollection nodeLabelBlocks = nodeLabelBlockRegex.Matches(svgBody);
        for (int i = 0; i < nodeLabelBlocks.Count; i++)
        {
            string block = nodeLabelBlocks[i].Groups[1].Value;
            string joined = Regex.Replace(
                block,
                @"<tspan[^>]*>([^<]*)</tspan>",
                "$1 ",
                RegexOptions.Compiled | RegexOptions.IgnoreCase).Trim();
            AddLabel(joined);
        }

        // mermaid.ink uses HTML labels inside foreignObject (no tspan).
        Regex foreignObjectParagraphRegex = new Regex(
            @"<foreignObject[^>]*>[\s\S]*?<p[^>]*>([\s\S]*?)</p>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        MatchCollection foreignObjectMatches = foreignObjectParagraphRegex.Matches(svgBody);
        for (int i = 0; i < foreignObjectMatches.Count; i++)
        {
            string paragraph = Regex.Replace(
                foreignObjectMatches[i].Groups[1].Value,
                @"<br\s*/?>",
                " ",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            paragraph = Regex.Replace(paragraph, @"<[^>]+>", string.Empty);
            AddLabel(paragraph);
        }

        Regex edgeLabelBlockRegex = new Regex(
            @"<text\b[^>]*\bclass=""edgeLabel""[^>]*>([\s\S]*?)</text>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        MatchCollection edgeLabelBlocks = edgeLabelBlockRegex.Matches(svgBody);
        for (int i = 0; i < edgeLabelBlocks.Count; i++)
        {
            string block = edgeLabelBlocks[i].Groups[1].Value;
            string joined = Regex.Replace(
                block,
                @"<tspan[^>]*>([^<]*)</tspan>",
                "$1 ",
                RegexOptions.Compiled | RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(joined))
            {
                joined = Regex.Replace(block, "<[^>]+>", string.Empty, RegexOptions.Compiled).Trim();
            }

            AddLabel(joined);
        }

        Regex edgeLabelForeignObjectRegex = new Regex(
            @"class=""edgeLabel""[\s\S]*?<p[^>]*>([^<]+)</p>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        MatchCollection edgeLabelForeignObjectMatches = edgeLabelForeignObjectRegex.Matches(svgBody);
        for (int i = 0; i < edgeLabelForeignObjectMatches.Count; i++)
        {
            AddLabel(edgeLabelForeignObjectMatches[i].Groups[1].Value);
        }

        Regex edgeLabelSpanRegex = new Regex(
            @"<span[^>]*class=""[^""]*edgeLabel[^""]*""[^>]*>([^<]*)</span>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        MatchCollection edgeLabelSpanMatches = edgeLabelSpanRegex.Matches(svgBody);
        for (int i = 0; i < edgeLabelSpanMatches.Count; i++)
        {
            AddLabel(edgeLabelSpanMatches[i].Groups[1].Value);
        }

        int edgeCount = Regex.Matches(svgBody, @"marker-end=""url\(#", RegexOptions.Compiled | RegexOptions.IgnoreCase).Count;
        if (edgeCount == 0)
        {
            edgeCount = Regex.Matches(svgBody, @"class=""[^""]*flowchart-link""", RegexOptions.Compiled).Count;
        }

        int clusterCount = Regex.Matches(
            svgBody,
            @"fill=""#ffffde""[^>]*stroke=""#aaaa33""|stroke=""#aaaa33""[^>]*fill=""#ffffde""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase).Count;

        return new FlowchartSvgMetrics
        {
            NodeLabels = labels,
            EdgePathCount = edgeCount,
            ClusterRectCount = clusterCount
        };
    }

    private static bool CompareFlowchartSvgStructure(string naiadSvg, string referenceSvg, out string notes)
    {
        notes = string.Empty;
        FlowchartSvgMetrics naiad = ExtractFlowchartSvgMetrics(naiadSvg);
        FlowchartSvgMetrics reference = ExtractFlowchartSvgMetrics(referenceSvg);

        List<string> problems = new List<string>();
        if (naiad.EdgePathCount != reference.EdgePathCount)
        {
            problems.Add($"edge count naiad={naiad.EdgePathCount} ref={reference.EdgePathCount}");
        }

        if (naiad.ClusterRectCount != reference.ClusterRectCount)
        {
            problems.Add($"cluster count naiad={naiad.ClusterRectCount} ref={reference.ClusterRectCount}");
        }

        foreach (string label in reference.NodeLabels)
        {
            if (!naiad.NodeLabels.Contains(label))
            {
                problems.Add("missing label: " + label);
            }
        }

        foreach (string label in naiad.NodeLabels)
        {
            if (!reference.NodeLabels.Contains(label))
            {
                problems.Add("extra label: " + label);
            }
        }

        if (problems.Count == 0)
        {
            return true;
        }

        notes = string.Join("; ", problems);
        return false;
    }

    private static string DetectDiagramTypeSafe(string mermaidSource)
    {
        try
        {
            DiagramType diagramType = Mermaid.DetectDiagramType(mermaidSource);
            return diagramType.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string NormaliseSvgForComparison(string svg)
    {
        // Normalise common non-semantic diffs to make the harness useful:
        // - whitespace
        // - ids and url(#...) references (Mermaid and Naiad generate different ids)
        string normalised = svg;
        normalised = Regex.Replace(normalised, @"\s+", " ", RegexOptions.Compiled);
        normalised = Regex.Replace(normalised, @"url\(#[-A-Za-z0-9_:.]+\)", "url(#ID)", RegexOptions.Compiled);
        normalised = Regex.Replace(normalised, @"\sid=""[^""]+""", " id=\"ID\"", RegexOptions.Compiled);
        normalised = Regex.Replace(normalised, @"\s(xlink:href|href)=""#[^""]+""", " $1=\"#ID\"", RegexOptions.Compiled);
        return normalised.Trim();
    }

    private static string Sha256Hex(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        byte[] hash = SHA256.HashData(bytes);
        StringBuilder sb = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
        {
            sb.Append(hash[i].ToString("x2"));
        }
        return sb.ToString();
    }

    private static bool HasFlag(string[] args, string flag)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string? GetOptionValue(string[] args, string optionName)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring(optionName.Length + 1);
            }

            if (string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    return args[i + 1];
                }
                return string.Empty;
            }
        }
        return null;
    }

    private static int? TryGetIntOptionValue(string[] args, string optionName)
    {
        string? value = GetOptionValue(args, optionName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out int parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool IsCommandAvailable(string exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName))
        {
            return false;
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string[] parts = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string dir = parts[i];
            try
            {
                string candidate = Path.Combine(dir, exeName);
                if (File.Exists(candidate))
                {
                    return true;
                }

                if (OperatingSystem.IsWindows())
                {
                    string candidateExe = candidate + ".exe";
                    string candidateCmd = candidate + ".cmd";
                    string candidateBat = candidate + ".bat";
                    if (File.Exists(candidateExe) || File.Exists(candidateCmd) || File.Exists(candidateBat))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return false;
    }
}

