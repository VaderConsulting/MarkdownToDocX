using System;
using System.Collections.Generic;
using System.Diagnostics;

using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.RTree;
using MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout;

namespace MermaidRecognition.Layout.Sugiyama.Core.Layout.ProximityOverlapRemoval.MST
{
    internal class MstLineSweeper {
        readonly List<Tuple<int, int, double, double, double>> _proximityEdges;
        readonly Size[] _nodeSizes;
        readonly Point[] _nodePositions;
        readonly bool _forLayers;
        IntervalRTree<int> _intervalTree;
        BinaryHeapPriorityQueue _q;
        int _numberOfOverlaps = 0;

        public MstLineSweeper(List<Tuple<int, int, double, double, double>> proximityEdges, Size[] nodeSizes, Point[] nodePositions, bool forLayers) {
            _proximityEdges = proximityEdges;
            _nodeSizes = nodeSizes;
            _nodePositions = nodePositions;
            _forLayers = forLayers;
            Debug.Assert(nodePositions.Length==nodeSizes.Length);
            _q = new BinaryHeapPriorityQueue(nodeSizes.Length*2); 
        }

        public int Run() {
            InitQueue();
            FindOverlaps();
            return _numberOfOverlaps;
        }

        void FindOverlaps() {
            while (_q.Count > 0) {
                int i = _q.Dequeue();
                if (i < _nodePositions.Length) {
                    FindOverlapsWithInterval(i);
                    AddIntervalToTree(i);
                }
                else {
                    i -= _nodePositions.Length;
                    RemoveIntervalFromTree(i);
                }
            }
        }

        void RemoveIntervalFromTree(int i) => _intervalTree.Remove(GetInterval(i), i);

        void AddIntervalToTree(int i) {
            Interval interval = GetInterval(i);
            if (_intervalTree == null)
            {
                _intervalTree = new IntervalRTree<int>();
            }

            _intervalTree.Add(interval, i);
            
        }

        void FindOverlapsWithInterval(int i) {
            if (_intervalTree == null)
            {
                return;
            }

            Interval interval = GetInterval(i);
            foreach (int j in _intervalTree.GetAllIntersecting(interval)) {
                Tuple<int, int, double, double, double> tuple = OverlapRemoval.GetIdealEdgeLength(i, j,
                    _nodePositions[i], _nodePositions[j], _nodeSizes, _forLayers);

                if (!(tuple.Item3 > 1))
                {
                    return;
                }

                _proximityEdges.Add(tuple);
                _numberOfOverlaps++;
            }
        }

        Interval GetInterval(int i) {
            double w = _nodeSizes[i].Width/2;
            double nodeCenterX = _nodePositions[i].X;
            return new Interval(nodeCenterX-w, nodeCenterX+w);
        }

        void InitQueue() {
            for (int i = 0; i < _nodeSizes.Length; i++) {
                double h = _nodeSizes[i].Height/2;
                double nodeCenterY = _nodePositions[i].Y;
                _q.Enqueue(i, nodeCenterY - h); // enqueue the bottom event
                _q.Enqueue(_nodeSizes.Length + i, nodeCenterY + h); // enqueue the top event
            }
        }
    }
}