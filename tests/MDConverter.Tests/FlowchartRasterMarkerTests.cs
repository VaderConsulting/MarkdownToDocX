using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using MDConverter.Pipeline;
using MermaidSharp;

namespace MDConverter.Tests;

[TestClass]
public sealed class FlowchartRasterMarkerTests
{
    [TestMethod]
    public void ExpandSvgFlowchartMarkers_ExtendsConnectorToTargetBoundary()
    {
        string Svg = Mermaid.Render(
            """
            flowchart LR
            A>Asymmetric flag] --> B[Target]
            """,
            RenderOptions.Default);

        MethodInfo? Method = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(Method);

        string Expanded = (string)Method.Invoke(null, new object[] { Svg })!;
        StringAssert.Contains(Expanded, "L204.88,28");
        StringAssert.Contains(Expanded, "M204.88,28");
        Assert.IsFalse(Expanded.Contains("marker-end", StringComparison.Ordinal));

        int TargetShapeIndex = Expanded.IndexOf("M204.88,0 H304.08 V56", StringComparison.Ordinal);
        int ExtendedConnectorIndex = Expanded.IndexOf("M154.88,28L154.88,28", StringComparison.Ordinal);
        Assert.IsTrue(TargetShapeIndex >= 0);
        Assert.IsTrue(ExtendedConnectorIndex > TargetShapeIndex);
    }

    [TestMethod]
    public void ExpandSvgFlowchartMarkers_F53FullDiagram_StadiumMergeArrowsTouchEnd()
    {
        string Svg = Mermaid.Render(
            """
            flowchart TD
            Start([Start]) --> Process[Process]
            Process --> Decision{Valid?}
            Decision -->|Yes| Success[Success]
            Decision -->|No| Error[Error]
            Success --> End([End])
            Error --> End

            style Start fill:#e1f5e1
            style End fill:#e1f5e1
            style Error fill:#f8d7da
            style Success fill:#d1ecf1
            """,
            RenderOptions.Default);

        MethodInfo? Method = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(Method);

        Match EndStadiumMatch = Regex.Match(
            Svg,
            @"path d=""(M([\d\.-]+),([\d\.-]+)\s+H([\d\.-]+)\s+A([\d\.-]+),([\d\.-]+)[^""]+)""[^>]*class=""node""[^>]*>\s*<text[^>]*>\s*<tspan[^>]*>End</tspan>",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.IsTrue(EndStadiumMatch.Success, "Expected End stadium in F53 SVG.");

        double LeftCapCentreX = double.Parse(EndStadiumMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        double TopY = double.Parse(EndStadiumMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        double RightCapCentreX = double.Parse(EndStadiumMatch.Groups[4].Value, CultureInfo.InvariantCulture);
        double Radius = double.Parse(EndStadiumMatch.Groups[6].Value, CultureInfo.InvariantCulture);
        double CapCentreY = TopY + Radius;

        string Expanded = (string)Method.Invoke(null, new object[] { Svg })!;

        MatchCollection ArrowMatches = Regex.Matches(
            Expanded,
            @"M([-\d.]+),([-\d.]+)L([-\d.]+),([-\d.]+)L([-\d.]+),([-\d.]+)Z",
            RegexOptions.CultureInvariant);
        int EndMergeArrowCount = 0;
        for (int Index = 0; Index < ArrowMatches.Count; Index++)
        {
            Match ArrowMatch = ArrowMatches[Index];
            double TipX = double.Parse(ArrowMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            double TipY = double.Parse(ArrowMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            if (TipY < TopY - 4.0 || TipY > CapCentreY + 4.0)
            {
                continue;
            }

            double LeftDistance = Math.Sqrt(((TipX - LeftCapCentreX) * (TipX - LeftCapCentreX)) + ((TipY - CapCentreY) * (TipY - CapCentreY)));
            double RightDistance = Math.Sqrt(((TipX - RightCapCentreX) * (TipX - RightCapCentreX)) + ((TipY - CapCentreY) * (TipY - CapCentreY)));
            double OutlineDistance = Math.Min(Math.Abs(LeftDistance - Radius), Math.Abs(RightDistance - Radius));
            Assert.IsTrue(
                OutlineDistance <= 1.5,
                "F53 merge arrow at "
                + TipX.ToString(CultureInfo.InvariantCulture)
                + ","
                + TipY.ToString(CultureInfo.InvariantCulture)
                + " outlineDistance="
                + OutlineDistance.ToString(CultureInfo.InvariantCulture));
            EndMergeArrowCount++;
        }

        Assert.IsTrue(EndMergeArrowCount >= 2, "Expected Success→End and Error→End arrowheads near End top (found " + EndMergeArrowCount + ").");
    }

    [TestMethod]
    public void ExpandSvgFlowchartMarkers_StadiumMerge_ArrowTipsTouchStadiumOutline()
    {
        string Svg = Mermaid.Render(
            """
            flowchart TD
            Success[Success] --> End([End])
            Error[Error] --> End
            """,
            RenderOptions.Default);

        MethodInfo? Method = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(Method);

        string Expanded = (string)Method.Invoke(null, new object[] { Svg })!;
        Match StadiumMatch = Regex.Match(
            Expanded,
            @"path d=""(M([\d\.-]+),([\d\.-]+)\s+H([\d\.-]+)\s+A([\d\.-]+),([\d\.-]+)[^""]+)""[^>]*class=""node""[^>]*>\s*<text[^>]*>\s*<tspan[^>]*>End</tspan>",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.IsTrue(StadiumMatch.Success, "Expected End stadium path.");

        double LeftCapCentreX = double.Parse(StadiumMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        double TopY = double.Parse(StadiumMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        double RightCapCentreX = double.Parse(StadiumMatch.Groups[4].Value, CultureInfo.InvariantCulture);
        double Radius = double.Parse(StadiumMatch.Groups[6].Value, CultureInfo.InvariantCulture);
        double CapCentreY = TopY + Radius;

        MatchCollection ArrowMatches = Regex.Matches(
            Expanded,
            @"M([-\d.]+),([-\d.]+)L([-\d.]+),([-\d.]+)L([-\d.]+),([-\d.]+)Z",
            RegexOptions.CultureInvariant);
        Assert.IsTrue(ArrowMatches.Count >= 2, "Expected inlined arrowheads for merge edges.");

        for (int Index = 0; Index < ArrowMatches.Count; Index++)
        {
            Match ArrowMatch = ArrowMatches[Index];
            double TipX = double.Parse(ArrowMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            double TipY = double.Parse(ArrowMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            double LeftDistance = Math.Sqrt(((TipX - LeftCapCentreX) * (TipX - LeftCapCentreX)) + ((TipY - CapCentreY) * (TipY - CapCentreY)));
            double RightDistance = Math.Sqrt(((TipX - RightCapCentreX) * (TipX - RightCapCentreX)) + ((TipY - CapCentreY) * (TipY - CapCentreY)));
            double OutlineDistance = Math.Min(Math.Abs(LeftDistance - Radius), Math.Abs(RightDistance - Radius));
            Assert.IsTrue(OutlineDistance <= 1.5, "Arrow tip should sit on the End stadium outline.");
        }
    }

    [TestMethod]
    public void ExpandSvgFlowchartMarkers_BranchingEdges_ArrowheadAlignedWithLastSegment()
    {
        string Svg = Mermaid.Render(
            """
            flowchart LR
            A[Request] -->|approve| B[Approved]
            A -->|reject| C[Rejected]
            """,
            RenderOptions.Default);

        MethodInfo? Method = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(Method);

        string Expanded = (string)Method.Invoke(null, new object[] { Svg })!;

        MatchCollection ArrowMatches = Regex.Matches(
            Expanded,
            @"M([-\d.]+),([-\d.]+)L([-\d.]+),([-\d.]+)L([-\d.]+),([-\d.]+)Z",
            RegexOptions.CultureInvariant);
        Assert.IsTrue(ArrowMatches.Count >= 2, "Expected inlined arrow paths for approve and reject edges.");

        for (int Index = 0; Index < ArrowMatches.Count; Index++)
        {
            Match ArrowMatch = ArrowMatches[Index];
            double TipX = double.Parse(ArrowMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            double TipY = double.Parse(ArrowMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            double LeftX = double.Parse(ArrowMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            double LeftY = double.Parse(ArrowMatch.Groups[4].Value, CultureInfo.InvariantCulture);
            double RightY = double.Parse(ArrowMatch.Groups[6].Value, CultureInfo.InvariantCulture);
            double BaseMidY = (LeftY + RightY) / 2.0;

            Assert.AreEqual(TipY, BaseMidY, 0.5, "Arrow tip should be centred on the base for a horizontal final segment.");
            Assert.IsTrue(Math.Abs(LeftY - RightY) > 4.0, "Arrow base should span perpendicular to the connector.");
            Assert.IsTrue(TipX > LeftX, "Arrow should point toward the target along +X.");
        }
    }

    [TestMethod]
    public void ExpandSvgFlowchartMarkers_StadiumMerge_DoesNotExtendThroughTarget()
    {
        string Svg = Mermaid.Render(
            """
            flowchart TD
            Success[Success] --> End([End])
            Error[Error] --> End
            """,
            RenderOptions.Default);

        MethodInfo? Method = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(Method);

        string Expanded = (string)Method.Invoke(null, new object[] { Svg })!;
        MatchCollection LinkPaths = Regex.Matches(
            Expanded,
            @"<path\b[^>]*\bd=""([^""]+)""[^>]*\bclass=""flowchart-link""",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        Assert.IsTrue(LinkPaths.Count >= 2, "Expected Success→End and Error→End link paths.");

        for (int Index = 0; Index < LinkPaths.Count; Index++)
        {
            string PathData = LinkPaths[Index].Groups[1].Value;
            Match EndMatch = Regex.Match(
                PathData,
                @"L\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*$",
                RegexOptions.CultureInvariant);
            Assert.IsTrue(EndMatch.Success, "Expected explicit path endpoint for merge link.");
            double EndY = double.Parse(EndMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            Assert.IsTrue(EndY < 130.0, "Stadium merge path should end on the upper outline, not through the pill bottom.");
        }
    }

    [TestMethod]
    public void ExpandSvgFlowchartMarkers_StadiumMerge_ArrowheadsUseIncomingPathDirection()
    {
        string Svg = Mermaid.Render(
            """
            flowchart TD
            Success[Success] --> End([End])
            Error[Error] --> End
            """,
            RenderOptions.Default);

        MethodInfo? Method = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(Method);

        string Expanded = (string)Method.Invoke(null, new object[] { Svg })!;
        Assert.IsFalse(Expanded.Contains("marker-end", StringComparison.Ordinal), "Raster expansion should inline arrowheads.");

        MatchCollection ArrowMatches = Regex.Matches(
            Expanded,
            @"M([-\d.]+),([-\d.]+)L([-\d.]+),([-\d.]+)L([-\d.]+),([-\d.]+)Z",
            RegexOptions.CultureInvariant);
        Assert.IsTrue(ArrowMatches.Count >= 2, "Expected inlined arrowheads for Success→End and Error→End.");

        List<double> TipXValues = new List<double>();
        for (int Index = 0; Index < ArrowMatches.Count; Index++)
        {
            Match ArrowMatch = ArrowMatches[Index];
            double TipX = double.Parse(ArrowMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            double TipY = double.Parse(ArrowMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            double LeftX = double.Parse(ArrowMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            double LeftY = double.Parse(ArrowMatch.Groups[4].Value, CultureInfo.InvariantCulture);
            double RightX = double.Parse(ArrowMatch.Groups[5].Value, CultureInfo.InvariantCulture);
            double RightY = double.Parse(ArrowMatch.Groups[6].Value, CultureInfo.InvariantCulture);
            double BaseMidX = (LeftX + RightX) / 2.0;
            double BaseMidY = (LeftY + RightY) / 2.0;
            double DirX = TipX - BaseMidX;
            double DirY = TipY - BaseMidY;
            double Magnitude = Math.Sqrt((DirX * DirX) + (DirY * DirY));
            Assert.IsTrue(Magnitude > 4.0, "Arrowhead base should span perpendicular to the connector.");
            TipXValues.Add(TipX);
        }

        TipXValues.Sort();
        Assert.IsTrue(TipXValues[^1] - TipXValues[0] > 20.0, "Merge arrowheads should land on separated End shoulders.");
    }

    [TestMethod]
    public void ExpandSvgFlowchartMarkers_F35MultiIncoming_ArrowTipsTouchDiamondOutline()
    {
        string Svg = Mermaid.Render(
            """
            flowchart TB
            A[Path A] --> D{Merge?}
            B[Path B] --> D
            C[Path C] --> D
            """,
            RenderOptions.Default);

        MethodInfo? Method = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(Method);

        Match DiamondMatch = Regex.Match(
            Svg,
            @"path d=""(M([\d\.-]+),([\d\.-]+)\s+L([\d\.-]+),([\d\.-]+)\s+L([\d\.-]+),([\d\.-]+)\s+L([\d\.-]+),([\d\.-]+)\s+Z)""[^>]*class=""node""[^>]*>\s*<text[^>]*>\s*<tspan[^>]*>Merge\?</tspan>",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.IsTrue(DiamondMatch.Success, "Expected Merge? diamond in F35 SVG.");

        double TopX = double.Parse(DiamondMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        double TopY = double.Parse(DiamondMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        double RightX = double.Parse(DiamondMatch.Groups[4].Value, CultureInfo.InvariantCulture);
        double RightY = double.Parse(DiamondMatch.Groups[5].Value, CultureInfo.InvariantCulture);
        double BottomX = double.Parse(DiamondMatch.Groups[6].Value, CultureInfo.InvariantCulture);
        double BottomY = double.Parse(DiamondMatch.Groups[7].Value, CultureInfo.InvariantCulture);
        double LeftX = double.Parse(DiamondMatch.Groups[8].Value, CultureInfo.InvariantCulture);
        double LeftY = double.Parse(DiamondMatch.Groups[9].Value, CultureInfo.InvariantCulture);

        string Expanded = (string)Method.Invoke(null, new object[] { Svg })!;

        MatchCollection ArrowMatches = Regex.Matches(
            Expanded,
            @"M([-\d.]+),([-\d.]+)L([-\d.]+),([-\d.]+)L([-\d.]+),([-\d.]+)Z",
            RegexOptions.CultureInvariant);

        int IncomingArrowCount = 0;
        for (int Index = 0; Index < ArrowMatches.Count; Index++)
        {
            Match ArrowMatch = ArrowMatches[Index];
            double TipX = double.Parse(ArrowMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            double TipY = double.Parse(ArrowMatch.Groups[2].Value, CultureInfo.InvariantCulture);

            double OutlineDistance = DistancePointToDiamondOutline(
                TipX,
                TipY,
                TopX,
                TopY,
                RightX,
                RightY,
                BottomX,
                BottomY,
                LeftX,
                LeftY);
            Assert.IsTrue(
                OutlineDistance <= 1.5,
                "F35 incoming arrow at "
                + TipX.ToString(CultureInfo.InvariantCulture)
                + ","
                + TipY.ToString(CultureInfo.InvariantCulture)
                + " outlineDistance="
                + OutlineDistance.ToString(CultureInfo.InvariantCulture));
            IncomingArrowCount++;
        }

        Assert.AreEqual(3, IncomingArrowCount, "Expected three inlined arrowheads on the Merge? diamond.");
    }

    [TestMethod]
    public void ExpandSvgFlowchartMarkers_F40CrossSubgraph_CrossBoundaryPathsUseCurves()
    {
        string Svg = Mermaid.Render(
            """
            flowchart LR
            subgraph S0["Stage A"]
              direction TB
              a0[A0] --> a1[A1]
            end
            subgraph S1["Stage B"]
              direction TB
              b0[B0] --> b1[B1]
            end
            IN[In] --> a0
            a1 --> b0
            b1 --> OUT[Out]
            """,
            RenderOptions.Default);

        MethodInfo? Method = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(Method);

        string Expanded = (string)Method.Invoke(null, new object[] { Svg })!;

        MatchCollection PathMatches = Regex.Matches(
            Expanded,
            @"<path d=""(M[^""]+)""[^>]*class=""flowchart-link""",
            RegexOptions.CultureInvariant);
        Assert.IsTrue(PathMatches.Count >= 5, "Expected internal and cross-subgraph connector paths.");

        int CurvedCrossBoundaryCount = 0;
        for (int Index = 0; Index < PathMatches.Count; Index++)
        {
            string PathData = PathMatches[Index].Groups[1].Value;
            bool IsSquareOnly = !PathData.Contains('C', StringComparison.Ordinal)
                && Regex.IsMatch(PathData, @"L[-\d.]+,[-\d.]+L[-\d.]+,[-\d.]+L", RegexOptions.CultureInvariant);
            if (!IsSquareOnly && PathData.Contains('C', StringComparison.Ordinal))
            {
                CurvedCrossBoundaryCount++;
            }
        }

        Assert.IsTrue(
            CurvedCrossBoundaryCount >= 3,
            "Expected IN→a0, a1→b0, and b1→OUT cross-subgraph links to use curved paths (found "
            + CurvedCrossBoundaryCount.ToString(CultureInfo.InvariantCulture)
            + " curved).");
    }

    private static double DistancePointToDiamondOutline(
        double PointX,
        double PointY,
        double TopX,
        double TopY,
        double RightX,
        double RightY,
        double BottomX,
        double BottomY,
        double LeftX,
        double LeftY)
    {
        double BestDistance = double.PositiveInfinity;
        BestDistance = Math.Min(BestDistance, DistancePointToSegment(PointX, PointY, TopX, TopY, RightX, RightY));
        BestDistance = Math.Min(BestDistance, DistancePointToSegment(PointX, PointY, RightX, RightY, BottomX, BottomY));
        BestDistance = Math.Min(BestDistance, DistancePointToSegment(PointX, PointY, BottomX, BottomY, LeftX, LeftY));
        BestDistance = Math.Min(BestDistance, DistancePointToSegment(PointX, PointY, LeftX, LeftY, TopX, TopY));
        return BestDistance;
    }

    private static double DistancePointToSegment(
        double PointX,
        double PointY,
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        double SegmentDx = EndX - StartX;
        double SegmentDy = EndY - StartY;
        double LengthSquared = (SegmentDx * SegmentDx) + (SegmentDy * SegmentDy);
        if (LengthSquared < 1e-9)
        {
            double DeltaX = PointX - StartX;
            double DeltaY = PointY - StartY;
            return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        }

        double Parameter = (((PointX - StartX) * SegmentDx) + ((PointY - StartY) * SegmentDy)) / LengthSquared;
        if (Parameter < 0.0)
        {
            Parameter = 0.0;
        }
        else if (Parameter > 1.0)
        {
            Parameter = 1.0;
        }

        double ClosestX = StartX + (Parameter * SegmentDx);
        double ClosestY = StartY + (Parameter * SegmentDy);
        double ClosestDeltaX = PointX - ClosestX;
        double ClosestDeltaY = PointY - ClosestY;
        return Math.Sqrt((ClosestDeltaX * ClosestDeltaX) + (ClosestDeltaY * ClosestDeltaY));
    }
}
