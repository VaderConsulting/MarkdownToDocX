using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MermaidRecognition.Layout;

internal static partial class FlowchartLayoutScore
{
    /// <summary>Retains the complete-layout length term alongside the original costs so length is compared only at tied bend and crossing costs.</summary>
    /// <param name="Primary">All established score terms and any separate repair charges.</param>
    /// <param name="WeightedLength">Total final route length in grid units multiplied by the configured length weight.</param>
    /// <param name="Bends">Completed bend count identifying the bend-cost tier.</param>
    /// <param name="CrossingCost">Completed ordinary and shared-endpoint crossing cost after the forced minimum.</param>
    /// <param name="Threshold">An existing comparison epsilon, applied to primary costs rather than discarding an exact length improvement.</param>
    internal readonly record struct ComparisonCost(double Primary, double WeightedLength, int Bends,
        double CrossingCost, double Threshold = 0) : IComparable<ComparisonCost>, IFormattable
    {
        /// <summary>Creates the unbounded sentinel used before the first legal candidate is found.</summary>
        internal static ComparisonCost PositiveInfinity => new(double.PositiveInfinity, 0, 0, 0);

        /// <summary>Calculates the approved bounded term once for the complete measured route inventory.</summary>
        internal double LengthTerm => BoundedLength(WeightedLength);

        /// <summary>Orders costs without letting length influence different bend or crossing tiers.</summary>
        /// <param name="Other">The complete incumbent measurement.</param>
        /// <returns>A negative value for a cheaper legal candidate, zero for a tie, or a positive value otherwise.</returns>
        public int CompareTo(ComparisonCost Other)
        {
            bool Tied = Bends == Other.Bends && CrossingCost == Other.CrossingCost;
            // Compare raw weighted length when the primary costs tie. Comparing
            // two saturated bounded doubles, or applying the old score epsilon,
            // could otherwise erase a real improvement at large route lengths.
            if (Tied && Primary == Other.Primary && WeightedLength != Other.WeightedLength)
            {
                return WeightedLength.CompareTo(Other.WeightedLength);
            }

            double Delta = Primary - Other.Primary + Threshold - Other.Threshold;
            if (Tied)
            {
                Delta += LengthTerm - Other.LengthTerm;
            }

            return double.IsNaN(Delta) ? Primary.CompareTo(Other.Primary) : Delta.CompareTo(0);
        }

        /// <summary>Adds an unchanged repair charge without normalising that separate rule 5.10 cost.</summary>
        /// <param name="Value">The complete measured layout cost.</param>
        /// <param name="Charge">Unchanged separate repair charge.</param>
        /// <returns>The resulting cost, comparison or published numeric total.</returns>
        public static ComparisonCost operator +(ComparisonCost Value, double Charge) => Value with { Primary = Value.Primary + Charge };
        /// <summary>Retains an existing primary-score threshold while preserving exact length tie-breaking.</summary>
        /// <param name="Value">The complete measured layout cost.</param>
        /// <param name="Epsilon">Existing primary-score comparison threshold.</param>
        /// <returns>The resulting cost, comparison or published numeric total.</returns>
        public static ComparisonCost operator -(ComparisonCost Value, double Epsilon) => Value with { Threshold = Value.Threshold - Epsilon };
        /// <summary>Measures a comparison difference for existing tie diagnostics.</summary>
        /// <param name="Left">Candidate layout cost.</param>
        /// <param name="Right">Reference layout cost.</param>
        /// <returns>The resulting cost, comparison or published numeric total.</returns>
        public static double operator -(ComparisonCost Left, ComparisonCost Right) =>
            Left.Primary - Right.Primary + Left.Threshold - Right.Threshold
            + (Left.Bends == Right.Bends && Left.CrossingCost == Right.CrossingCost ? Left.LengthTerm - Right.LengthTerm : 0);
        /// <summary>Compares complete candidates under the approved length tie-break policy.</summary>
        /// <param name="Left">Candidate layout cost.</param>
        /// <param name="Right">Reference layout cost.</param>
        /// <returns>The resulting cost, comparison or published numeric total.</returns>
        public static bool operator <(ComparisonCost Left, ComparisonCost Right) => Left.CompareTo(Right) < 0;
        /// <summary>Compares complete candidates under the approved length tie-break policy.</summary>
        /// <param name="Left">Candidate layout cost.</param>
        /// <param name="Right">Reference layout cost.</param>
        /// <returns>The resulting cost, comparison or published numeric total.</returns>
        public static bool operator >(ComparisonCost Left, ComparisonCost Right) => Left.CompareTo(Right) > 0;
        /// <summary>Compares complete candidates under the approved length tie-break policy.</summary>
        /// <param name="Left">Candidate layout cost.</param>
        /// <param name="Right">Reference layout cost.</param>
        /// <returns>The resulting cost, comparison or published numeric total.</returns>
        public static bool operator <=(ComparisonCost Left, ComparisonCost Right) => Left.CompareTo(Right) <= 0;
        /// <summary>Compares complete candidates under the approved length tie-break policy.</summary>
        /// <param name="Left">Candidate layout cost.</param>
        /// <param name="Right">Reference layout cost.</param>
        /// <returns>The resulting cost, comparison or published numeric total.</returns>
        public static bool operator >=(ComparisonCost Left, ComparisonCost Right) => Left.CompareTo(Right) >= 0;
        /// <summary>Publishes the numeric total for existing observers; candidate comparisons retain their cost tiers.</summary>
        /// <param name="Value">The complete measured layout cost.</param>
        /// <returns>The resulting cost, comparison or published numeric total.</returns>
        public static implicit operator double(ComparisonCost Value) => Value.Primary + Value.LengthTerm + Value.Threshold;
        /// <summary>Formats the complete numeric score for existing diagnostics.</summary>
        /// <param name="Format">Numeric format used by the existing diagnostic.</param>
        /// <param name="Provider">Culture used to format the numeric total.</param>
        /// <returns>The formatted numeric score.</returns>
        public string ToString(string? Format, IFormatProvider? Provider) => ((double)this).ToString(Format, Provider);
        /// <summary>Formats the complete numeric score without depending on the machine locale.</summary>
        /// <returns>The invariant formatted numeric score.</returns>
        public override string ToString() => ToString(null, CultureInfo.InvariantCulture);
    }

    /// <summary>Calculates wL/(1+wL/B) without overflowing the intermediate denominator.</summary>
    /// <param name="WeightedLength">Nonnegative weighted length of the whole layout, in routing grid units.</param>
    /// <returns>A nonnegative term strictly below the smaller bend or crossing weight.</returns>
    internal static double BoundedLength(double WeightedLength)
    {
        if (WeightedLength <= 0)
        {
            return 0;
        }

        double Bound = Math.Min(FlowchartLayoutRules.BendScore, FlowchartLayoutRules.CrossingScore);
        return Math.Min(Bound / (1 + (Bound / WeightedLength)), Math.BitDecrement(Bound));
    }

    /// <summary>Measures all drawn straight runs, including exact terminals and slanted diamond normals.</summary>
    /// <param name="Points">One complete final polyline.</param>
    /// <returns>Euclidean route length in pixels.</returns>
    private static double DrawnLength(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        double Total = 0;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            double X = Points[Index].X - Points[Index - 1].X, Y = Points[Index].Y - Points[Index - 1].Y;
            Total += Math.Sqrt((X * X) + (Y * Y));
        }
        return Total;
    }
}
