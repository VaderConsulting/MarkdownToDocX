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
using MermaidSharp;
using SkiaSharp;
using Svg.Skia;

namespace SvgHarness;

internal static class Program
{
    private static readonly HttpClient _HttpClient = new HttpClient();
    private static readonly object _ConsoleLock = new object();

    private sealed class MermaidBlock
    {
        public required int Index { get; init; }
        public required string Source { get; init; }
        public required string SourceLabel { get; init; }
        public required string DetectedDiagramType { get; init; }
    }

    private sealed class CompareResult
    {
        public required MermaidBlock Block { get; init; }
        public required bool NaiadSucceeded { get; init; }
        public required bool ReferenceSucceeded { get; init; }
        public required bool MatchAfterNormalisation { get; init; }
        public required string NaiadSvgPath { get; init; }
        public required string ReferenceSvgPath { get; init; }
        public required string Notes { get; init; }
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
            Console.Error.WriteLine("  dotnet run --project .\\src\\SvgHarness\\SvgHarness.csproj -- <markdownPath> <outputDirectory> [--no-reference]");
            Console.Error.WriteLine("  dotnet run --project .\\src\\SvgHarness\\SvgHarness.csproj -- --rasterise-svg <inputSvgPath> <outputPngPath>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Notes:");
            Console.Error.WriteLine("- Reference rendering uses Mermaid Ink (no Node required).");
            Console.Error.WriteLine("- If Node is installed, you can still opt into mermaid-cli with --reference=npx.");
            return 2;
        }

        string markdownPath = Path.GetFullPath(Args[0]);
        string outputDirectory = Path.GetFullPath(Args[1]);
        bool noReference = HasFlag(Args, "--no-reference");
        bool emitPng = HasFlag(Args, "--emit-png");
        string referenceMode = GetOptionValue(Args, "--reference") ?? "mermaid-ink";
        int maxBlocks = TryGetIntOptionValue(Args, "--max-blocks") ?? int.MaxValue;
        int parallel = TryGetIntOptionValue(Args, "--parallel") ?? 4;
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
        results.AddRange(RunCases(blocks, outputDirectory, referenceMode, parallel, emitPng));

        WriteSummary(results, string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase));
        WriteScopedTypeSummary(results);

        bool anyRenderFailed = results.Exists(r => !r.NaiadSucceeded || (!noReference && !r.ReferenceSucceeded));
        if (anyRenderFailed)
        {
            return 3;
        }

        bool anyMismatch = results.Exists(r => !r.MatchAfterNormalisation);
        return anyMismatch ? 1 : 0;
    }

    private static List<CompareResult> RunCases(List<MermaidBlock> blocks, string outputDirectory, string referenceMode, int parallel, bool emitPng)
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
            CompareResult result = RunCase(block, outputDirectory, referenceMode, emitPng);
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

    private static CompareResult RunCase(MermaidBlock block, string outputDirectory, string referenceMode, bool emitPng)
    {
        string prefix = $"{block.Index:D3}";
        string naiadSvgPath = Path.Combine(outputDirectory, prefix + ".naiad.svg");
        string referenceSvgPath = Path.Combine(outputDirectory, prefix + ".reference.svg");
        string naiadPngPath = Path.Combine(outputDirectory, prefix + ".naiad.png");
        string referencePngPath = Path.Combine(outputDirectory, prefix + ".reference.png");
        string inputPath = Path.Combine(outputDirectory, prefix + ".mmd");

        File.WriteAllText(inputPath, block.Source, Encoding.UTF8);

        bool naiadSucceeded = TryWriteNaiadSvg(block.Source, naiadSvgPath, out string? naiadError);
        string? referenceError = null;
        bool referenceSucceeded = string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase)
            ? false
            : TryWriteReferenceSvg(block.Source, inputPath, referenceSvgPath, referenceMode, out referenceError);

        if (emitPng && naiadSucceeded)
        {
            string? naiadPngError = null;
            TryRenderSvgToPng(naiadSvgPath, naiadPngPath, out naiadPngError);
        }

        string notes = string.Empty;
        if (!naiadSucceeded)
        {
            notes = "Naiad render failed: " + naiadError;
        }
        else if (!string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase) && !referenceSucceeded)
        {
            notes = "Reference render failed: " + referenceError;
        }

        bool match = false;
        if (naiadSucceeded && (string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase) || referenceSucceeded))
        {
            if (string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase))
            {
                match = true;
            }
            else
            {
                // Prefer semantic comparison: render to PNG and pixel-diff.
                string? naiadPngError = null;
                string? refPngError = null;
                if (TryRenderSvgToPng(naiadSvgPath, naiadPngPath, out naiadPngError)
                    && TryGetReferencePng(referenceMode, block.Source, referenceSvgPath, referencePngPath, out refPngError))
                {
                    match = ComputePngVisualMatch(naiadPngPath, referencePngPath, out double diffRatio);
                    if (!match)
                    {
                        notes = $"Visual diff ratio={diffRatio:0.0000}";
                    }
                }
                else
                {
                    notes = "PNG compare failed: " + (naiadPngError ?? refPngError ?? "unknown");
                    match = false;
                }
            }
        }

        return new CompareResult
        {
            Block = block,
            NaiadSucceeded = naiadSucceeded,
            ReferenceSucceeded = referenceSucceeded || string.Equals(referenceMode, "none", StringComparison.OrdinalIgnoreCase),
            MatchAfterNormalisation = match,
            NaiadSvgPath = naiadSvgPath,
            ReferenceSvgPath = referenceSvgPath,
            Notes = notes
        };
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

        const int MAX_ATTEMPTS = 4;
        int attempt = 0;
        while (attempt < MAX_ATTEMPTS)
        {
            attempt++;
            try
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
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

        const int MAX_ATTEMPTS = 4;
        int attempt = 0;
        while (attempt < MAX_ATTEMPTS)
        {
            attempt++;
            try
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
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
        }

        error = "Mermaid Ink PNG request failed after retries.";
        return false;
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

            SKRect bounds = picture.CullRect;
            int width = (int)Math.Ceiling(Math.Max(1.0, bounds.Width));
            int height = (int)Math.Ceiling(Math.Max(1.0, bounds.Height));

            using (SKBitmap bitmap = new SKBitmap(width, height))
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
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

    private static bool ComputePngVisualMatch(string naiadPngPath, string referencePngPath, out double diffRatio)
    {
        // Simple per-pixel RGB diff. Resizes to a common bounding box by padding with white.
        // Threshold is intentionally forgiving to allow minor anti-aliasing diffs.
        const double CHANNEL_DIFF_THRESHOLD = 0.18; // per-channel difference (0..1)
        const double PIXEL_MISMATCH_RATIO_THRESHOLD = 0.0025; // 0.25% mismatched pixels

        using (SKBitmap a = SKBitmap.Decode(naiadPngPath))
        using (SKBitmap b = SKBitmap.Decode(referencePngPath))
        {
            int width = Math.Max(a.Width, b.Width);
            int height = Math.Max(a.Height, b.Height);
            if (width <= 0 || height <= 0)
            {
                diffRatio = 1.0;
                return false;
            }

            long mismatched = 0;
            long total = (long)width * height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    SKColor ca = (x < a.Width && y < a.Height) ? a.GetPixel(x, y) : SKColors.White;
                    SKColor cb = (x < b.Width && y < b.Height) ? b.GetPixel(x, y) : SKColors.White;

                    // Ignore nearly-white pixels in both images to reduce sensitivity to different page sizes/margins.
                    if (IsNearWhite(ca) && IsNearWhite(cb))
                    {
                        continue;
                    }

                    double dr = Math.Abs(ca.Red - cb.Red) / 255.0;
                    double dg = Math.Abs(ca.Green - cb.Green) / 255.0;
                    double db = Math.Abs(ca.Blue - cb.Blue) / 255.0;

                    if (dr > CHANNEL_DIFF_THRESHOLD || dg > CHANNEL_DIFF_THRESHOLD || db > CHANNEL_DIFF_THRESHOLD)
                    {
                        mismatched++;
                    }
                }
            }

            diffRatio = total == 0 ? 1.0 : (double)mismatched / total;
            return diffRatio <= PIXEL_MISMATCH_RATIO_THRESHOLD;
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
        List<MermaidBlock> blocks = new List<MermaidBlock>();

        foreach (MermaidBlock block in ExtractFencedBlocks(markdown))
        {
            blocks.Add(block);
        }

        foreach (MermaidBlock block in ExtractColonBlocks(markdown, startToken: ":::mermaid", endToken: ":::"))
        {
            blocks.Add(block);
        }

        blocks.Sort((a, b) => a.Index.CompareTo(b.Index));
        return blocks;
    }

    private static IEnumerable<MermaidBlock> ExtractFencedBlocks(string markdown)
    {
        Regex regex = new Regex(@"(?s)```mermaid\s*\r?\n(?<Body>.*?)(\r?\n```)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        MatchCollection matches = regex.Matches(markdown);
        for (int i = 0; i < matches.Count; i++)
        {
            string body = matches[i].Groups["Body"].Value.Trim();
            string detectedType = DetectDiagramTypeSafe(body);
            yield return new MermaidBlock
            {
                Index = i,
                Source = body,
                SourceLabel = "```mermaid",
                DetectedDiagramType = detectedType
            };
        }
    }

    private static IEnumerable<MermaidBlock> ExtractColonBlocks(string markdown, string startToken, string endToken)
    {
        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        bool inBlock = false;
        StringBuilder current = new StringBuilder();
        int index = 100000; // offset to avoid collisions with fenced blocks; final sort uses Index

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
                    yield return new MermaidBlock
                    {
                        Index = index,
                        Source = body,
                        SourceLabel = startToken,
                        DetectedDiagramType = detectedType
                    };
                    index++;
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

        for (int i = 0; i < results.Count; i++)
        {
            CompareResult result = results[i];
            if (!result.NaiadSucceeded || (!noReference && !result.ReferenceSucceeded))
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
        Console.WriteLine();

        for (int i = 0; i < results.Count; i++)
        {
            CompareResult result = results[i];
            string status;
            if (!result.NaiadSucceeded)
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

            Console.WriteLine($"{result.Block.Index:D3} {status} {result.Block.SourceLabel} {result.Notes}");
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

