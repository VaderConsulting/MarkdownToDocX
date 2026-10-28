using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Records physical crossing locations without modifying routing or audit checkers.</summary>
internal static class EnterpriseCrossingLocations
{
    /// <summary>Measures independent location categories; overlapping pre-band envelopes intentionally count in more than one category.</summary>
    /// <param name="Contract">Semantic membership and group endpoints.</param>
    /// <param name="Layout">Finished, published routes in Y-up model coordinates.</param>
    /// <returns>Band envelopes, location counts and detailed crossing pairs for reproducible comparison.</returns>
    internal static object Measure(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        Dictionary<string, string> Layers = Contract.Nodes.Where(Node => Node.Metadata.ContainsKey("enterpriseElementType"))
            .ToDictionary(Node => Node.Id, Node => EnterpriseElementCatalog.Elements[
                Enum.Parse<EnterpriseElementType>(((RawMetadata)Node.Metadata["enterpriseElementType"]).RawValue)].Layer.ToString(), StringComparer.Ordinal);
        string[] Order = ["Motivation", "Strategy", "Business", "Application", "Technology", "Implementation"];
        Dictionary<string, (double Bottom, double Top)> Bands = Layout.NodeBounds.Where(Box => Layers.ContainsKey(Box.NodeId)).GroupBy(Box => Layers[Box.NodeId])
            .ToDictionary(Group => Group.Key, Group => (Bottom: Group.Min(Box => Box.CenterY - (Box.Height / 2)),
                Top: Group.Max(Box => Box.CenterY + (Box.Height / 2))));
        Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string Band in Bands.Keys)
        {
            Counts["Within " + Band] = 0;
        }

        var Present = Order.Where(Bands.ContainsKey).ToArray();
        for (int Index = 1; Index < Present.Length; Index++)
        {
            Counts["Between " + Present[Index - 1] + " / " + Present[Index]] = 0;
        }

        Counts["On skipping-band lines"] = 0; Counts["Inside frames"] = 0; Counts["Outside band envelopes"] = 0;
        List<object> Details = [];
        for (int A = 0; A < Layout.EdgeRoutes.Count; A++)
        {
            for (int B = A + 1; B < Layout.EdgeRoutes.Count; B++)
        {
                FlowchartLayoutEdgeRoute First = Layout.EdgeRoutes[A]; FlowchartLayoutEdgeRoute Second = Layout.EdgeRoutes[B];
            foreach ((double X, double Y) Point in Points(First, Second))
            {
                    List<string> Categories = [];
                foreach (KeyValuePair<string, (double Bottom, double Top)> Band in Bands.Where(Band => Point.Y >= Band.Value.Bottom && Point.Y <= Band.Value.Top))
                    {
                        Categories.Add("Within " + Band.Key);
                    }

                    for (int Index = 1; Index < Present.Length; Index++)
                    {
                        if (Point.Y < Bands[Present[Index - 1]].Bottom && Point.Y > Bands[Present[Index]].Top)
                        {
                            Categories.Add("Between " + Present[Index - 1] + " / " + Present[Index]);
                        }
                    }

                    if (Categories.Count == 0)
                    {
                        Categories.Add("Outside band envelopes");
                    }

                    if (SkipsBand(First, Layers, Order) || SkipsBand(Second, Layers, Order))
                    {
                        Categories.Add("On skipping-band lines");
                    }

                    if (Layout.ClusterBounds.Any(Box => Point.X > Box.CenterX - (Box.Width / 2) && Point.X < Box.CenterX + (Box.Width / 2)
                    && Point.Y > Box.CenterY - (Box.Height / 2) && Point.Y < Box.CenterY + (Box.Height / 2)))
                    {
                        Categories.Add("Inside frames");
                    }

                    foreach (string Category in Categories)
                    {
                        Counts[Category]++;
                    }

                    Details.Add(new { First = First.EdgeId, Second = Second.EdgeId, Point.X, Point.Y, Categories });
            }
        }
        }

        return new { Counts, Envelopes = Bands.ToDictionary(Pair => Pair.Key, Pair => new { Pair.Value.Bottom, Pair.Value.Top }), Details };
    }

    /// <summary>Identifies links with conventional leaf endpoints separated by at least one semantic layer.</summary>
    /// <param name="Route">Declared routed link.</param>
    /// <param name="Layers">Leaf semantic layers.</param>
    /// <param name="Order">Vertical convention order, including Motivation at the top.</param>
    /// <returns>Whether this leaf-to-leaf line skips a semantic band.</returns>
    private static bool SkipsBand(FlowchartLayoutEdgeRoute Route, IReadOnlyDictionary<string, string> Layers, string[] Order)
    {
        int Source = Array.IndexOf(Order, Layers.GetValueOrDefault(Route.SourceId));
        int Target = Array.IndexOf(Order, Layers.GetValueOrDefault(Route.TargetId));
        return Source >= 0 && Target >= 0 && Math.Abs(Source - Target) > 1;
    }

    /// <summary>Locates the same distinct segment intersections used for geometric crossing totals, excluding shared graph terminals.</summary>
    /// <param name="First">First complete route.</param>
    /// <param name="Second">Second complete route.</param>
    /// <returns>Crossing coordinates rounded to the existing six-decimal measurement precision.</returns>
    private static IEnumerable<(double X, double Y)> Points(FlowchartLayoutEdgeRoute First, FlowchartLayoutEdgeRoute Second)
    {
        HashSet<(double X, double Y)> Hits = [];
        for (int I = 1; I < First.Points.Count; I++)
        {
            for (int J = 1; J < Second.Points.Count; J++)
        {
                FlowchartLayoutPoint A = First.Points[I - 1]; FlowchartLayoutPoint B = First.Points[I];
                FlowchartLayoutPoint C = Second.Points[J - 1]; FlowchartLayoutPoint D = Second.Points[J];
            double Ax = B.X - A.X, Ay = B.Y - A.Y, Bx = D.X - C.X, By = D.Y - C.Y;
            double Denominator = (Ax * By) - (Ay * Bx);
            if (Math.Abs(Denominator) < 0.000001)
                {
                    continue;
                }

                double Cx = C.X - A.X, Cy = C.Y - A.Y;
            double T = ((Cx * By) - (Cy * Bx)) / Denominator, U = ((Cx * Ay) - (Cy * Ax)) / Denominator;
            if (T < -0.000001 || T > 1.000001 || U < -0.000001 || U > 1.000001)
                {
                    continue;
                }

                (double X, double Y) Point = (X: Math.Round(A.X + (T * Ax), 6), Y: Math.Round(A.Y + (T * Ay), 6));
            bool FirstEnd = new[] { First.Points[0], First.Points[^1] }.Any(End => Math.Abs(End.X - Point.X) < 0.01 && Math.Abs(End.Y - Point.Y) < 0.01);
            bool SecondEnd = new[] { Second.Points[0], Second.Points[^1] }.Any(End => Math.Abs(End.X - Point.X) < 0.01 && Math.Abs(End.Y - Point.Y) < 0.01);
            if (!FirstEnd || !SecondEnd)
                {
                    Hits.Add(Point);
                }
            }
        }

        return Hits;
    }
}
