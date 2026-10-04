using System;
using System.Collections.Generic;
using System.Diagnostics;

using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms;
using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms.CycleRemoval;

namespace MermaidRecognition.Layout.Sugiyama.Core.ProjectionSolver{
    /// <summary>
    /// desired variable separation
    /// </summary>
    /// <param name="variableSeparation"></param>
    internal class UniformOneDimensionalSolver(double variableSeparation)
    {
        readonly Dictionary<int, double> idealPositions = [];
        readonly double varSepartion = variableSeparation;
        readonly List<UniformSolverVar> varList = [];

        readonly Set<IntPair> constraints = [];
        BasicGraph<IntPair> graph;

//        delegate IEnumerable<NudgerConstraint> Edges(int i);
//
//        delegate int End(NudgerConstraint constraint);

//        Edges outEdgesDel;
//        Edges inEdgesDel;
//        End sourceDelegate;
//        End targetDelegate;
//        Supremum minDel;
//        Supremum maxDel;

        internal void SetLowBound(double bound, int id){
            UniformSolverVar v = Var(id);
            v.LowBound = Math.Max(bound, v.LowBound);
        }

        UniformSolverVar Var(int id) => varList[id];

        internal void SetUpperBound(int id, double bound){
            UniformSolverVar v = Var(id);
            v.UpperBound = Math.Min(bound, v.UpperBound);
        }



        internal void Solve() => SolveByRegularSolver();

        readonly SolverShell solverShell = new SolverShell();

        void SolveByRegularSolver() {
            CreateVariablesForBounds();
            for (int i = 0; i < varList.Count; i++) {
                UniformSolverVar v = varList[i];
                if (v.IsFixed)
                {
                    solverShell.AddFixedVariable(i, v.Position);
                }
                else {
                    solverShell.AddVariableWithIdealPosition(i, idealPositions[i]);
                    if (v.LowBound != double.NegativeInfinity)
                    {
                        //    solverShell.AddLeftRightSeparationConstraint(GetBoundId(v.LowBound), i, varSepartion);
                        constraints.Insert(new IntPair(GetBoundId(v.LowBound), i));
                    }

                    if (v.UpperBound != double.PositiveInfinity)
                    {
                        constraints.Insert(new IntPair(i, GetBoundId(v.UpperBound)));
                    }
                }
            }
           
            CreateGraphAndRemoveCycles();

            foreach (IntPair edge in graph.Edges) {
                double w = 0.0;
                if(edge.First<varList.Count)
                {
                    w +=varList[edge.First].Width;
                }

                if (edge.Second < varList.Count)
                {
                    w += varList[edge.Second].Width;
                }

                w /= 2;
                solverShell.AddLeftRightSeparationConstraint(edge.First, edge.Second, varSepartion + w);
            }
            solverShell.Solve();

            for (int i = 0; i < varList.Count; i++)
            {
                varList[i].Position = solverShell.GetVariableResolvedPosition(i);
            }
        }

        int GetBoundId(double bound) => boundsToInt[bound];

        void CreateVariablesForBounds(){
            foreach (UniformSolverVar v in varList){
                if(v.IsFixed)
                {
                    continue;
                }

                if (v.LowBound != double.NegativeInfinity)
                {
                    RegisterBoundVar(v.LowBound);
                }

                if (v.UpperBound != double.PositiveInfinity)
                {
                    RegisterBoundVar(v.UpperBound);
                }
            }
        }

        readonly Dictionary<double, int> boundsToInt = [];
        void RegisterBoundVar(double bound){
            if (!boundsToInt.ContainsKey(bound)){
                int varIndex=varList.Count + boundsToInt.Count;
                boundsToInt[bound] = varIndex;
                solverShell.AddFixedVariable(varIndex, bound);
            }
        }

        
        void CreateGraphAndRemoveCycles(){
            //edges in the graph go from a smaller value to a bigger value
            graph = new BasicGraph<IntPair>(constraints, varList.Count+boundsToInt.Count);
            //removing cycles
            IEnumerable<IEdge> feedbackSet = CycleRemoval<IntPair>.GetFeedbackSet(graph);
            if(feedbackSet!=null)
            {
                foreach (IEdge edge in feedbackSet)
                {
                    graph.RemoveEdge(edge as IntPair);
                }
            }
        }

        internal double GetVariablePosition(int id) => varList[id].Position;

        internal void AddConstraint(int i, int j) => constraints.Insert(new IntPair(i, j));

        internal void AddVariable(int id, double currentPosition, double idealPosition, double width){
            idealPositions[id] = idealPosition;
            AddVariable(id, currentPosition, false, width);

        }

        internal void AddFixedVariable(int id, double position) => AddVariable(id, position, true, 0); //0 for width

        void AddVariable(int id, double position, bool isFixed, double width) {
            Debug.Assert(id==varList.Count);
            varList.Add(new UniformSolverVar { IsFixed = isFixed, Position = position, Width=width });
        }
    }
}