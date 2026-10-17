using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Supplies the connection body and occupied outline of the decorated enterprise shapes.</summary>
internal sealed class EnterpriseDecoratedNodeGeometry(EnterpriseBoxKind Kind) : INodeGeometryProvider
{
    /// <summary>The inset drawn by the contract renderer, in pixels.</summary>
    internal const double CONTRACT_INSET = 6;
    /// <summary>The distance from the visual box bottom to the representation wave endpoints.</summary>
    internal const double WAVE_INSET = 8;
    /// <summary>The quadratic control-point displacement from the wave endpoint line.</summary>
    internal const double WAVE_CONTROL_OFFSET = 10;

    /// <inheritdoc />
    public NodeGeometry Create(double Width, double Height)
    {
        NodeGeometry Default = NodeGeometry.Rectangle(Width, Height);
        if (Kind is not (EnterpriseBoxKind.Contract or EnterpriseBoxKind.Representation))
            throw new ArgumentOutOfRangeException(nameof(Kind));
        if (Kind == EnterpriseBoxKind.Contract && (Width <= 2 * CONTRACT_INSET || Height <= CONTRACT_INSET)
            || Kind == EnterpriseBoxKind.Representation && Height <= WAVE_INSET + WAVE_CONTROL_OFFSET / 2)
            throw new ArgumentException("The measured visual box cannot contain the renderer's decorated body.");
        var Shape = new Boundary(Kind, Width, Height);
        return new(Default.VisualBounds, Shape, Shape);
    }

    /// <summary>Owns the renderer's exact local silhouette and eligible cardinal connection segments.</summary>
    private sealed class Boundary(EnterpriseBoxKind Kind, double Width, double Height)
        : INodeConnectionBoundary, INodeObstacleGeometry
    {
        /// <summary>The horizontal endpoint line of the representation wave.</summary>
        private double Midline => Height / 2 - WAVE_INSET;

        /// <inheritdoc />
        public bool TryPort(string Side, double Fraction, out NodeGeometryPort Port)
        {
            Port = default;
            if (!double.IsFinite(Fraction) || Fraction <= 0 || Fraction >= 1) return false;
            double Left = -Width / 2, Right = Width / 2, Top = -Height / 2;
            double Bottom = Kind == EnterpriseBoxKind.Representation ? Midline : Height / 2;
            if (Kind == EnterpriseBoxKind.Contract)
            {
                Left += CONTRACT_INSET;
                Right -= CONTRACT_INSET;
                Top += CONTRACT_INSET;
                // A vertical north approach to the body would traverse the decorative top strip.
                if (Side == "top") return false;
            }
            Port = Side switch
            {
                "left" => new(Left, Top + Fraction * (Bottom - Top), -1, 0),
                "right" => new(Right, Top + Fraction * (Bottom - Top), 1, 0),
                "top" => new(Left + Fraction * (Right - Left), Top, 0, -1),
                "bottom" => new(Left + Fraction * (Right - Left), Bottom, 0, 1),
                _ => default
            };
            if (Side is not ("left" or "right" or "top" or "bottom")) return false;
            // The right half of the preserved wave protrudes below its endpoint line.
            // Its midline ports cannot receive a vertical approach without crossing the stroke.
            return !(Kind == EnterpriseBoxKind.Representation && Side == "bottom" && Fraction > 0.5);
        }

        /// <inheritdoc />
        public bool Contains(double X, double Y, double Tolerance)
        {
            double Left = -Width / 2, Right = Width / 2, Top = -Height / 2;
            double Bottom = Kind == EnterpriseBoxKind.Representation ? Midline : Height / 2;
            if (Kind == EnterpriseBoxKind.Contract)
            {
                Left += CONTRACT_INSET; Right -= CONTRACT_INSET; Top += CONTRACT_INSET;
            }
            bool InX = X >= Left - Tolerance && X <= Right + Tolerance;
            bool InY = Y >= Top - Tolerance && Y <= Bottom + Tolerance;
            return InY && (Math.Abs(X - Left) <= Tolerance || Math.Abs(X - Right) <= Tolerance)
                || InX && Math.Abs(Y - Bottom) <= Tolerance
                    && (Kind != EnterpriseBoxKind.Representation || X <= Tolerance)
                || Kind != EnterpriseBoxKind.Contract && InX && Math.Abs(Y - Top) <= Tolerance;
        }

        /// <inheritdoc />
        public bool IntersectsSegmentInterior(double StartX, double StartY, double EndX, double EndY)
        {
            if (StartX == EndX && StartY == EndY) return false;
            // Split at every silhouette intersection. Occupancy is constant on each open interval;
            // testing its midpoint is exact, rather than sampling a curve at a chosen resolution.
            double Dx = EndX - StartX, Dy = EndY - StartY;
            var Cuts = new List<double> { 0, 1 };
            void Linear(double A, double B) { if (A != 0) Add(-B / A); }
            void Add(double T) { if (T > 0 && T < 1 && double.IsFinite(T)) Cuts.Add(T); }
            Linear(Dx, StartX + Width / 2); Linear(Dx, StartX - Width / 2);
            Linear(Dy, StartY + Height / 2); Linear(Dy, StartY - Height / 2);
            if (Kind == EnterpriseBoxKind.Contract)
            {
                Linear(Dy, StartY + Height / 2 - CONTRACT_INSET);
                Linear(Dx, StartX + Width / 2 - CONTRACT_INSET);
                Linear(Dx, StartX - Width / 2 + CONTRACT_INSET);
                Linear(Dx - Dy, StartX + Width / 2 - StartY - Height / 2);
                Linear(Dx + Dy, StartX - Width / 2 + StartY + Height / 2);
            }
            else
            {
                Linear(Dx, StartX);
                double U = (StartX + Width / 2) / Width, Du = Dx / Width;
                // Q control points are halfway across each half-wave. Thus X is linear in t.
                // These coefficients are the two exact quadratic Bezier ordinates in u=x/width.
                Roots(8 * WAVE_CONTROL_OFFSET, -4 * WAVE_CONTROL_OFFSET, 0);
                Roots(-8 * WAVE_CONTROL_OFFSET, 12 * WAVE_CONTROL_OFFSET, -4 * WAVE_CONTROL_OFFSET);
                void Roots(double A, double B, double C)
                {
                    double Qa = A * Du * Du;
                    double Qb = 2 * A * U * Du + B * Du - Dy;
                    double Qc = A * U * U + B * U + C + Midline - StartY;
                    if (Qa == 0) { Linear(Qb, Qc); return; }
                    double Discriminant = Qb * Qb - 4 * Qa * Qc;
                    if (Discriminant < 0) return;
                    double Root = Math.Sqrt(Discriminant);
                    Add((-Qb - Root) / (2 * Qa)); Add((-Qb + Root) / (2 * Qa));
                }
            }
            Cuts.Sort();
            for (int Index = 1; Index < Cuts.Count; Index++)
            {
                if (Cuts[Index] == Cuts[Index - 1]) continue;
                double T = (Cuts[Index - 1] + Cuts[Index]) / 2;
                if (Inside(StartX + T * Dx, StartY + T * Dy)) return true;
            }
            return false;
        }

        /// <summary>Tests strict occupied interior against the renderer's exact filled silhouette.</summary>
        /// <param name="X">Local horizontal position.</param>
        /// <param name="Y">Local page-down vertical position.</param>
        /// <returns>Whether the point lies inside the painted body or decoration.</returns>
        private bool Inside(double X, double Y)
        {
            double V = Y + Height / 2;
            if (Kind == EnterpriseBoxKind.Contract)
            {
                double Inset = Math.Min(CONTRACT_INSET, V);
                return V > 0 && V < Height && X > -Width / 2 + Inset && X < Width / 2 - Inset;
            }
            double U = (X + Width / 2) / Width;
            double Wave = Midline + (U <= 0.5
                ? WAVE_CONTROL_OFFSET * (8 * U * U - 4 * U)
                : WAVE_CONTROL_OFFSET * (-8 * U * U + 12 * U - 4));
            return U > 0 && U < 1 && V > 0 && Y < Wave;
        }
    }
}
