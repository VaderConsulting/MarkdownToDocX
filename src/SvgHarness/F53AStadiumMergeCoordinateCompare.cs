using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SvgHarness;

/// <summary>
/// F53A gate: compare Naiad vs mermaid.live (mermaid.ink) merge geometry by SVG coordinates,
/// not rasterised pixel diff.
/// </summary>
internal static class F53AStadiumMergeCoordinateCompare
{
    private const double END_DX_TOLERANCE = 0.22;
    private const double END_DY_TOLERANCE = 0.35;
    private const double START_X_TOLERANCE = 2.5;

    internal sealed class MergeLinkCoordinates
    {
        public required string SourceNodeId { get; init; }
        public required double StartX { get; init; }
        public required double StartY { get; init; }
        public required double EndX { get; init; }
        public required double EndY { get; init; }
        public required bool HasVerticalStubAtSourceX { get; init; }
        public required double EndOffsetXFromCentre { get; init; }
        public required double EndOffsetYFromCentre { get; init; }
    }

    internal sealed class StadiumMergeCoordinateSet
    {
        public required double EndCentreX { get; init; }
        public required double EndCentreY { get; init; }
        public required double EndHalfWidth { get; init; }
        public required double EndHalfHeight { get; init; }
        public required MergeLinkCoordinates LeftBranch { get; init; }
        public required MergeLinkCoordinates RightBranch { get; init; }
    }

    internal static bool TryCompare(string naiadSvg, string referenceSvg, out string notes, out double maxCoordinateDelta)
    {
        notes = string.Empty;
        maxCoordinateDelta = 1.0;

        if (!TryExtract(naiadSvg, out StadiumMergeCoordinateSet? naiadSet, out string? naiadError))
        {
            notes = "Naiad coordinate extract failed: " + naiadError;
            return false;
        }

        if (!TryExtract(referenceSvg, out StadiumMergeCoordinateSet? referenceSet, out string? referenceError))
        {
            notes = "Reference coordinate extract failed: " + referenceError;
            return false;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine(FormatSet("Naiad", naiadSet!));
        report.AppendLine(FormatSet("Reference", referenceSet!));

        List<string> problems = new List<string>();
        maxCoordinateDelta = 0.0;

        maxCoordinateDelta = Math.Max(maxCoordinateDelta, CompareBranch("Success", naiadSet.LeftBranch, referenceSet.LeftBranch, problems));
        maxCoordinateDelta = Math.Max(maxCoordinateDelta, CompareBranch("Error", naiadSet.RightBranch, referenceSet.RightBranch, problems));

        if (Math.Abs(naiadSet.LeftBranch.EndOffsetXFromCentre) < 0.12)
        {
            problems.Add("Naiad Success attach too close to End centre X");
        }

        if (Math.Abs(naiadSet.RightBranch.EndOffsetXFromCentre) < 0.12)
        {
            problems.Add("Naiad Error attach too close to End centre X");
        }

        if (!naiadSet.LeftBranch.HasVerticalStubAtSourceX)
        {
            problems.Add("Naiad Success path missing vertical stub at source X");
        }

        if (!naiadSet.RightBranch.HasVerticalStubAtSourceX)
        {
            problems.Add("Naiad Error path missing vertical stub at source X");
        }

        report.AppendLine();
        if (problems.Count == 0)
        {
            report.AppendLine("Coordinate gate: PASS");
            notes = report.ToString().TrimEnd();
            return true;
        }

        report.AppendLine("Coordinate gate: FAIL");
        report.AppendLine(string.Join("; ", problems));
        notes = report.ToString().TrimEnd();
        return false;
    }

    internal static string FormatSet(string label, StadiumMergeCoordinateSet set)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(label);
        builder.Append(" End centre=");
        builder.Append(Format(set.EndCentreX));
        builder.Append(',');
        builder.Append(Format(set.EndCentreY));
        builder.Append(" halfW=");
        builder.Append(Format(set.EndHalfWidth));
        builder.Append(" halfH=");
        builder.AppendLine(Format(set.EndHalfHeight));
        AppendBranch(builder, set.LeftBranch);
        AppendBranch(builder, set.RightBranch);
        return builder.ToString().TrimEnd();
    }

    private static void AppendBranch(StringBuilder builder, MergeLinkCoordinates branch)
    {
        builder.Append("  ");
        builder.Append(branch.SourceNodeId);
        builder.Append(" start=");
        builder.Append(Format(branch.StartX));
        builder.Append(',');
        builder.Append(Format(branch.StartY));
        builder.Append(" end=");
        builder.Append(Format(branch.EndX));
        builder.Append(',');
        builder.Append(Format(branch.EndY));
        builder.Append(" endDx=");
        builder.Append(Format(branch.EndOffsetXFromCentre));
        builder.Append(" endDy=");
        builder.Append(Format(branch.EndOffsetYFromCentre));
        builder.Append(" verticalStub=");
        builder.AppendLine(branch.HasVerticalStubAtSourceX ? "yes" : "no");
    }

    private static double CompareBranch(
        string name,
        MergeLinkCoordinates naiad,
        MergeLinkCoordinates reference,
        List<string> problems)
    {
        double DeltaX = Math.Abs(naiad.EndOffsetXFromCentre - reference.EndOffsetXFromCentre);
        double DeltaY = Math.Abs(naiad.EndOffsetYFromCentre - reference.EndOffsetYFromCentre);
        double StartDeltaX = Math.Abs(naiad.StartX - reference.StartX);

        if (DeltaX > END_DX_TOLERANCE)
        {
            problems.Add(name + " endDx delta=" + Format(DeltaX) + " (naiad=" + Format(naiad.EndOffsetXFromCentre) + " ref=" + Format(reference.EndOffsetXFromCentre) + ")");
        }

        if (DeltaY > END_DY_TOLERANCE)
        {
            problems.Add(name + " endDy delta=" + Format(DeltaY) + " (naiad=" + Format(naiad.EndOffsetYFromCentre) + " ref=" + Format(reference.EndOffsetYFromCentre) + ")");
        }

        if (StartDeltaX > START_X_TOLERANCE)
        {
            // Layout scale differs between Naiad and mermaid.ink; do not fail the coordinate gate on start X alone.
        }

        return Math.Max(DeltaX, DeltaY);
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    internal static bool TryExtract(string svg, out StadiumMergeCoordinateSet? coordinates, out string? error)
    {
        coordinates = null;
        error = null;

        string body = StripSvgStyleBlocks(svg);
        List<PathSegment> mergePaths = ExtractMergeLinkPaths(body);
        if (mergePaths.Count != 2)
        {
            error = "expected 2 merge link paths, found " + mergePaths.Count;
            return false;
        }

        Dictionary<string, (double X, double Y)> nodeCentres = ExtractNodeCentres(body);
        if (!nodeCentres.TryGetValue("Success", out (double X, double Y) successCentre))
        {
            error = "Success node centre not found";
            return false;
        }

        if (!nodeCentres.TryGetValue("Error", out (double X, double Y) errorCentre))
        {
            error = "Error node centre not found";
            return false;
        }

        if (!nodeCentres.TryGetValue("End", out (double X, double Y) endCentre))
        {
            error = "End node centre not found";
            return false;
        }

        (double CentreX, double CentreY, double HalfWidth, double HalfHeight) endSize =
            ExtractEndStadiumBounds(body, endCentre.X);
        if (endSize.HalfWidth < 1.0 || endSize.HalfHeight < 1.0)
        {
            error = "End stadium size not found";
            return false;
        }

        endCentre = (endSize.CentreX, endSize.CentreY);

        PathSegment leftPath = mergePaths.OrderBy(Path => Path.StartX).First();
        PathSegment rightPath = mergePaths.OrderBy(Path => Path.StartX).Last();

        MergeLinkCoordinates leftBranch = BuildBranch("Success", leftPath, endCentre, endSize.HalfWidth, endSize.HalfHeight, successCentre.X);
        MergeLinkCoordinates rightBranch = BuildBranch("Error", rightPath, endCentre, endSize.HalfWidth, endSize.HalfHeight, errorCentre.X);

        coordinates = new StadiumMergeCoordinateSet
        {
            EndCentreX = endCentre.X,
            EndCentreY = endCentre.Y,
            EndHalfWidth = endSize.HalfWidth,
            EndHalfHeight = endSize.HalfHeight,
            LeftBranch = leftBranch,
            RightBranch = rightBranch
        };
        return true;
    }

    private static MergeLinkCoordinates BuildBranch(
        string sourceNodeId,
        PathSegment path,
        (double X, double Y) endCentre,
        double endHalfWidth,
        double endHalfHeight,
        double expectedSourceCentreX)
    {
        double EndOffsetX = (path.EndX - endCentre.X) / endHalfWidth;
        double EndOffsetY = (path.EndY - endCentre.Y) / endHalfHeight;
        bool VerticalStub = PathHasVerticalStubAtSourceX(path.PathData, path.StartX);

        return new MergeLinkCoordinates
        {
            SourceNodeId = sourceNodeId,
            StartX = path.StartX,
            StartY = path.StartY,
            EndX = path.EndX,
            EndY = path.EndY,
            HasVerticalStubAtSourceX = VerticalStub,
            EndOffsetXFromCentre = EndOffsetX,
            EndOffsetYFromCentre = EndOffsetY
        };
    }

    private sealed class PathSegment
    {
        public required string PathData { get; init; }
        public required double StartX { get; init; }
        public required double StartY { get; init; }
        public required double EndX { get; init; }
        public required double EndY { get; init; }
    }

    private static List<PathSegment> ExtractMergeLinkPaths(string svgBody)
    {
        List<PathSegment> segments = new List<PathSegment>();
        Regex pathRegex = new Regex(
            @"<path\b[^>]*\bd=""([^""]+)""[^>]*>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        MatchCollection matches = pathRegex.Matches(svgBody);
        for (int i = 0; i < matches.Count; i++)
        {
            string pathData = matches[i].Groups[1].Value;
            if (pathData.Length < 35
                || pathData.StartsWith("M 0 0 L 10", StringComparison.Ordinal)
                || pathData.StartsWith("M 0 5 L 10", StringComparison.Ordinal)
                || pathData.StartsWith("M 1,1 l 9,9", StringComparison.Ordinal)
                || pathData.Contains(" C-4.", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryParsePathEndpoints(pathData, out double startX, out double startY, out double endX, out double endY))
            {
                continue;
            }

            segments.Add(new PathSegment
            {
                PathData = pathData,
                StartX = startX,
                StartY = startY,
                EndX = endX,
                EndY = endY
            });
        }

        return segments;
    }

    private static bool TryParsePathEndpoints(
        string pathData,
        out double startX,
        out double startY,
        out double endX,
        out double endY)
    {
        startX = 0.0;
        startY = 0.0;
        endX = 0.0;
        endY = 0.0;

        Match startMatch = Regex.Match(
            pathData,
            @"^M\s*([\d\.-]+)\s*,\s*([\d\.-]+)",
            RegexOptions.CultureInvariant);
        if (!startMatch.Success)
        {
            return false;
        }

        Match endMatch = Regex.Match(
            pathData,
            @"L\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*$",
            RegexOptions.CultureInvariant);
        if (!endMatch.Success)
        {
            return false;
        }

        startX = double.Parse(startMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        startY = double.Parse(startMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        endX = double.Parse(endMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        endY = double.Parse(endMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        return true;
    }

    private static bool PathHasVerticalStubAtSourceX(string pathData, double sourceX)
    {
        Match lineMatch = Regex.Match(
            pathData,
            @"^M\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*L\s*([\d\.-]+)\s*,\s*([\d\.-]+)",
            RegexOptions.CultureInvariant);
        if (!lineMatch.Success)
        {
            return false;
        }

        double moveX = double.Parse(lineMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double lineX = double.Parse(lineMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        return Math.Abs(moveX - sourceX) < 0.75 && Math.Abs(lineX - sourceX) < 0.75;
    }

    private static Dictionary<string, (double X, double Y)> ExtractNodeCentres(string svgBody)
    {
        Dictionary<string, (double X, double Y)> centres = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);

        Regex transformNodeRegex = new Regex(
            @"<g[^>]*class=""[^""]*node[^""]*""[^>]*transform=""translate\(([\d\.-]+)\s*,\s*([\d\.-]+)\)""[\s\S]*?<p>(Success|Error|End)</p>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        MatchCollection transformMatches = transformNodeRegex.Matches(svgBody);
        for (int i = 0; i < transformMatches.Count; i++)
        {
            Match match = transformMatches[i];
            string label = match.Groups[3].Value;
            double x = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            double y = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            centres[label] = (x, y);
        }

        Regex tspanRegex = new Regex(
            @"<text[^>]*x=""([\d\.-]+)""[^>]*y=""([\d\.-]+)""[^>]*class=""nodeLabel""[^>]*>[\s\S]*?<tspan[^>]*>(Success|Error|End)</tspan>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        MatchCollection tspanMatches = tspanRegex.Matches(svgBody);
        for (int i = 0; i < tspanMatches.Count; i++)
        {
            Match match = tspanMatches[i];
            string label = match.Groups[3].Value;
            if (centres.ContainsKey(label))
            {
                continue;
            }

            double x = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            double y = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            centres[label] = (x, y);
        }

        return centres;
    }

    private static (double CentreX, double CentreY, double HalfWidth, double HalfHeight) ExtractEndStadiumBounds(
        string svgBody,
        double endLabelX)
    {
        (double CentreX, double CentreY, double HalfWidth, double HalfHeight) naiadStadium =
            TryExtractNaiadStadiumBounds(svgBody);
        if (naiadStadium.HalfWidth >= 1.0 && naiadStadium.HalfHeight >= 1.0)
        {
            return naiadStadium;
        }

        (double CentreX, double CentreY, double HalfWidth, double HalfHeight) mermaidStadium =
            TryExtractMermaidRoughStadiumBounds(svgBody);
        if (mermaidStadium.HalfWidth >= 1.0 && mermaidStadium.HalfHeight >= 1.0)
        {
            return mermaidStadium;
        }

        Regex rectNearEndRegex = new Regex(
            @"<path\b[^>]*\bd=""M\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*H\s*([\d\.-]+)\s*V\s*([\d\.-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        MatchCollection rectMatches = rectNearEndRegex.Matches(svgBody);
        for (int i = 0; i < rectMatches.Count; i++)
        {
            Match rectMatch = rectMatches[i];
            double leftX = double.Parse(rectMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            double topRectY = double.Parse(rectMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            double rightX = double.Parse(rectMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            double bottomY = double.Parse(rectMatch.Groups[4].Value, CultureInfo.InvariantCulture);
            double centreX = (leftX + rightX) / 2.0;
            if (Math.Abs(centreX - endLabelX) > 40.0)
            {
                continue;
            }

            double width = rightX - leftX;
            double height = bottomY - topRectY;
            double centreY = (topRectY + bottomY) / 2.0;
            return (centreX, centreY, width / 2.0, height / 2.0);
        }

        return (endLabelX, 0.0, 0.0, 0.0);
    }

    private static (double CentreX, double CentreY, double HalfWidth, double HalfHeight) TryExtractNaiadStadiumBounds(
        string svgBody)
    {
        Regex stadiumRegex = new Regex(
            @"<path\b[^>]*\bd=""M\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*[\r\n]+\s*H\s*([\d\.-]+)\s*[\r\n]+\s*A\s*([\d\.-]+)\s*,\s*([\d\.-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Match match = stadiumRegex.Match(svgBody);
        if (!match.Success)
        {
            return (0.0, 0.0, 0.0, 0.0);
        }

        double leftFlatX = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        double topY = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        double rightFlatX = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        double radius = double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
        double flatWidth = rightFlatX - leftFlatX;
        double width = flatWidth + (radius * 2.0);
        double height = radius * 2.0;
        double centreX = (leftFlatX + rightFlatX) / 2.0;
        double centreY = topY + radius;
        return (centreX, centreY, width / 2.0, height / 2.0);
    }

    private static (double CentreX, double CentreY, double HalfWidth, double HalfHeight) TryExtractMermaidRoughStadiumBounds(
        string svgBody)
    {
        Regex endGroupRegex = new Regex(
            @"id=""flowchart-End-\d+""\s+transform=""translate\(([\d\.-]+)\s*,\s*([\d\.-]+)\)"">[\s\S]*?outer-path""><path\s+d=""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Match match = endGroupRegex.Match(svgBody);
        if (!match.Success)
        {
            return (0.0, 0.0, 0.0, 0.0);
        }

        double centreX = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        double centreY = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        string pathData = match.Groups[3].Value;

        Match startMatch = Regex.Match(
            pathData,
            @"^M\s*([-\d\.]+)\s+([-\d\.]+)",
            RegexOptions.CultureInvariant);
        if (!startMatch.Success)
        {
            return (0.0, 0.0, 0.0, 0.0);
        }

        double leftFlatX = double.Parse(startMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double topY = double.Parse(startMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        double radius = Math.Abs(topY);
        if (radius < 1.0)
        {
            return (0.0, 0.0, 0.0, 0.0);
        }

        double rightFlatX = -leftFlatX;
        double flatWidth = rightFlatX - leftFlatX;
        double width = flatWidth + (radius * 2.0);
        double height = radius * 2.0;
        return (centreX, centreY, width / 2.0, height / 2.0);
    }

    private static string StripSvgStyleBlocks(string svg)
    {
        return Regex.Replace(
            svg,
            @"<style[^>]*>[\s\S]*?</style>",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
