using System;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves interior bulge guide coordinates that curve toward the target without cross-axis backtracking.
/// </summary>
public static class FlowchartCurveBulgeHelper
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    /// <summary>
    /// Resolves the bulge X for vertical-flow curved routes (TB/BT).
    /// </summary>
    public static double ResolveVerticalFlowBulgeX(
        double SourceStubX,
        double TargetStubX,
        double RouteHubCenterX,
        double BulgeRatio)
    {
        double MidX = (SourceStubX + TargetStubX) / 2.0;
        double Spread = Math.Max(
            Math.Abs(SourceStubX - RouteHubCenterX),
            Math.Abs(TargetStubX - RouteHubCenterX)) * BulgeRatio;

        double SourceOffsetX = SourceStubX - RouteHubCenterX;
        double TargetOffsetX = TargetStubX - RouteHubCenterX;

        if (Math.Abs(SourceOffsetX) > COLLINEAR_EPSILON_PX
            && Math.Abs(TargetOffsetX) > COLLINEAR_EPSILON_PX
            && Math.Sign(SourceOffsetX) == Math.Sign(TargetOffsetX)
            && Math.Abs(TargetOffsetX) >= Math.Abs(SourceOffsetX) - COLLINEAR_EPSILON_PX)
        {
            return MidX;
        }

        if (SourceStubX < RouteHubCenterX - COLLINEAR_EPSILON_PX
            || TargetStubX < RouteHubCenterX - COLLINEAR_EPSILON_PX)
        {
            double CandidateBulgeX = MidX - Spread;
            double LeftmostStubX = Math.Min(SourceStubX, TargetStubX);
            if (CandidateBulgeX < LeftmostStubX - COLLINEAR_EPSILON_PX)
            {
                return MidX;
            }

            return CandidateBulgeX;
        }

        if (SourceStubX > RouteHubCenterX + COLLINEAR_EPSILON_PX
            || TargetStubX > RouteHubCenterX + COLLINEAR_EPSILON_PX)
        {
            double CandidateBulgeX = MidX + Spread;
            double RightmostStubX = Math.Max(SourceStubX, TargetStubX);
            if (CandidateBulgeX > RightmostStubX + COLLINEAR_EPSILON_PX)
            {
                return MidX;
            }

            return CandidateBulgeX;
        }

        return MidX;
    }

    /// <summary>
    /// Resolves the bulge Y for horizontal-flow curved routes (LR/RL).
    /// </summary>
    public static double ResolveHorizontalFlowBulgeY(
        double SourceStubY,
        double TargetStubY,
        double RouteHubCenterY,
        double BulgeRatio)
    {
        double MidY = (SourceStubY + TargetStubY) / 2.0;
        double Spread = Math.Max(
            Math.Abs(SourceStubY - RouteHubCenterY),
            Math.Abs(TargetStubY - RouteHubCenterY)) * BulgeRatio;

        double SourceOffsetY = SourceStubY - RouteHubCenterY;
        double TargetOffsetY = TargetStubY - RouteHubCenterY;

        if (Math.Abs(SourceOffsetY) > COLLINEAR_EPSILON_PX
            && Math.Abs(TargetOffsetY) > COLLINEAR_EPSILON_PX
            && Math.Sign(SourceOffsetY) == Math.Sign(TargetOffsetY)
            && Math.Abs(TargetOffsetY) >= Math.Abs(SourceOffsetY) - COLLINEAR_EPSILON_PX)
        {
            return MidY;
        }

        if (SourceStubY < RouteHubCenterY - COLLINEAR_EPSILON_PX
            || TargetStubY < RouteHubCenterY - COLLINEAR_EPSILON_PX)
        {
            double CandidateBulgeY = MidY - Spread;
            double TopmostStubY = Math.Min(SourceStubY, TargetStubY);
            if (CandidateBulgeY < TopmostStubY - COLLINEAR_EPSILON_PX)
            {
                return MidY;
            }

            return CandidateBulgeY;
        }

        if (SourceStubY > RouteHubCenterY + COLLINEAR_EPSILON_PX
            || TargetStubY > RouteHubCenterY + COLLINEAR_EPSILON_PX)
        {
            double CandidateBulgeY = MidY + Spread;
            double BottommostStubY = Math.Max(SourceStubY, TargetStubY);
            if (CandidateBulgeY > BottommostStubY + COLLINEAR_EPSILON_PX)
            {
                return MidY;
            }

            return CandidateBulgeY;
        }

        return MidY;
    }
}
