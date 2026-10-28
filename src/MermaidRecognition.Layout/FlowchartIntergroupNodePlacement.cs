using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Brings outside connectors into the free gap between their related root groups.</summary>
internal static class FlowchartIntergroupNodePlacement
{
    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        List<DiagramGridLayout.GridFrame> Frames, bool Horizontal)
    {
        // Legacy anchor-centre/clamped-pixel placement cannot assign cell
        // ownership. The cell frame fitter retains mandatory group clearance;
        // complete whole-cell endpoint/group candidates provide route-cost
        // placement, including outside connectors, after routing.
        if (Nodes.Any(Node => Node.CellNode != null))
        {
            return false;
        }

        List<EdgeContract> Edges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        List<DiagramGridLayout.GridFrame> Roots = [.. Frames.Where(Frame => !Contract.Groups.Any(Group => Group.Id != Frame.Id
            && FlowchartCompoundEndpointHelper.Contains(Contract, Group.Id, Frame.Id)))];
        if (Roots.Count < 2)
        {
            return false;
        }

        List<DiagramGridLayout.GridNode> Outside = [.. Nodes.Where(Node => Node.GroupId == null && !Node.IsDiamond)];
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id);
        Dictionary<string, DiagramGridLayout.GridFrame> FrameById = Frames.ToDictionary(Frame => Frame.Id);
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        double Main(DiagramGridLayout.GridNode Node) => Horizontal ? Node.X : Node.Y;
        double Near(DiagramGridLayout.GridFrame Frame) => Horizontal ? Math.Min(Frame.Top, Frame.Bottom) : Frame.Left;
        double Far(DiagramGridLayout.GridFrame Frame) => Horizontal ? Math.Max(Frame.Top, Frame.Bottom) : Frame.Right;
        double MainNear(DiagramGridLayout.GridFrame Frame) => Horizontal ? Frame.Left : Math.Min(Frame.Top, Frame.Bottom);
        double MainFar(DiagramGridLayout.GridFrame Frame) => Horizontal ? Frame.Right : Math.Max(Frame.Top, Frame.Bottom);
        HashSet<string> Done = [];
        bool Changed = false;
        foreach (DiagramGridLayout.GridNode? Seed in Outside)
        {
            if (!Done.Add(Seed.Id))
            {
                continue;
            }

            List<DiagramGridLayout.GridNode> Component = [Seed];
            for (int Index = 0; Index < Component.Count; Index++)
            {
                foreach (EdgeContract? Edge in Edges.Where(Edge => Edge.SourceId == Component[Index].Id || Edge.TargetId == Component[Index].Id))
            {
                string Other = Edge.SourceId == Component[Index].Id ? Edge.TargetId : Edge.SourceId;
                    DiagramGridLayout.GridNode? Peer = Outside.FirstOrDefault(Node => Node.Id == Other);
                if (Peer != null && Done.Add(Other))
                    {
                        Component.Add(Peer);
                    }
                }
            }

            HashSet<string> Ids = [.. Component.Select(Node => Node.Id)];
            List<string> Neighbours = [.. Edges.Where(Edge => Ids.Contains(Edge.SourceId) != Ids.Contains(Edge.TargetId))
                .Select(Edge => Ids.Contains(Edge.SourceId) ? Edge.TargetId : Edge.SourceId).Distinct()];
            List<DiagramGridLayout.GridFrame> Owners = [.. Roots.Where(Frame => Neighbours.Any(Id => Id == Frame.Id
                || FlowchartCompoundEndpointHelper.Contains(Contract, Frame.Id, Id))).OrderBy(Near)];
            if (Owners.Count != 2)
            {
                continue;
            }

            double Low = Far(Owners[0]), High = Near(Owners[1]);
            if (Low >= High)
            {
                continue;
            }

            foreach (DiagramGridLayout.GridNode Node in Component)
            {
                List<string> Anchors = [.. Edges.Where(Edge => Edge.SourceId == Node.Id || Edge.TargetId == Node.Id)
                    .Select(Edge => Edge.SourceId == Node.Id ? Edge.TargetId : Edge.SourceId)
                    .Where(Id => Neighbours.Contains(Id))];
                if (Anchors.Count == 0)
                {
                    continue;
                }

                double HalfCross = (Horizontal ? Node.Height : Node.Width) / 2.0;
                double HalfMain = (Horizontal ? Node.Width : Node.Height) / 2.0;
                double AnchorCross(string Id) => ById.TryGetValue(Id, out DiagramGridLayout.GridNode? Box) ? Cross(Box)
                    : Horizontal ? FrameById[Id].CenterY() : FrameById[Id].CenterX();
                double AnchorMain(string Id) => ById.TryGetValue(Id, out DiagramGridLayout.GridNode? Box) ? Main(Box) : FrameById[Id].CenterMain(Horizontal);
                if (Anchors.Any(Id => !ById.ContainsKey(Id) && !FrameById.ContainsKey(Id)))
                {
                    continue;
                }

                string Closest = Anchors.OrderBy(Id => Math.Abs(AnchorCross(Id) - Cross(Node))).First();
                double Preferred = AnchorMain(Closest);
                if (Component.Count == 1 && Anchors.All(FrameById.ContainsKey))
                {
                    Preferred = Anchors.Max(Id => MainNear(FrameById[Id])) + HalfMain + 30;
                }
                else if (Component.Count > 1 && Anchors.All(FrameById.ContainsKey))
                {
                    Preferred = Anchors.Max(Id => MainFar(FrameById[Id])) + HalfMain + 30;
                }

                foreach (DiagramGridLayout.GridNode? Parent in Component.Where(Parent => Parent != Node
                    && Edges.Any(Edge => Edge.SourceId == Parent.Id && Edge.TargetId == Node.Id)
                    && Cross(Parent) >= Low && Cross(Parent) <= High))
                {
                    Preferred = Math.Max(Preferred, Main(Parent) + ((Horizontal ? Parent.Width : Parent.Height) / 2.0) + HalfMain + 30);
                }

                double Start = Low + HalfCross + 30, End = High - HalfCross - 30;
                if (Start > End)
                {
                    continue;
                }

                double CandidateCross = Math.Clamp(Cross(Node), Start, End);
                CandidateCross = Start + DiagramGridMetrics.RoundToGrid(CandidateCross - Start);
                for (int Step = 0; Step < 32; Step++)
                {
                    double CandidateMain = Preferred + (Step * DiagramGridMetrics.GRID_UNIT_PX);
                    CandidateMain = Main(Seed) + (Math.Round((CandidateMain - Main(Seed)) / DiagramGridMetrics.GRID_UNIT_PX)
                        * DiagramGridMetrics.GRID_UNIT_PX);
                    if (CandidateMain - HalfMain < Math.Max(MainNear(Owners[0]), MainNear(Owners[1]))
                        || CandidateMain + HalfMain > Math.Min(MainFar(Owners[0]), MainFar(Owners[1])))
                    {
                        continue;
                    }

                    double X = Horizontal ? CandidateMain : CandidateCross, Y = Horizontal ? CandidateCross : CandidateMain;
                    if (Nodes.Any(Other => Other != Node && Math.Abs(Other.X - X) < ((Other.Width + Node.Width) / 2.0) + 30
                        && Math.Abs(Other.Y - Y) < ((Other.Height + Node.Height) / 2.0) + 30))
                    {
                        continue;
                    }

                    if (Frames.Any(Frame => X + (Node.Width / 2.0) + 30 > Frame.Left && X - (Node.Width / 2.0) - 30 < Frame.Right
                        && Y + (Node.Height / 2.0) + 30 > Math.Min(Frame.Top, Frame.Bottom)
                        && Y - (Node.Height / 2.0) - 30 < Math.Max(Frame.Top, Frame.Bottom)))
                    {
                        continue;
                    }

                    Node.X = X; Node.Y = Y; Changed = true;
                    break;
                }
            }
        }
        return Changed;
    }
}
