using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Miscellaneous.Routing
{
    public class ShortestPathTree
    {
         Set<Point> _points = [];
         Dictionary<Point, Point> _paret = [];
         Point _root;

        public ShortestPathTree(Point root)
        {
            _root = root;
            _points.Insert(root);
            _paret[_root] = _root;
        }

        public void AddPathToTree(List<Point> path)
        {
            if (!path.First().Equals(_root))
            {
                path.Reverse();
            }

            if (!path.First().Equals(_root))
            {
                return;
            }

            int i;

            for (i = path.Count - 1; i >= 0; i--)
            {
                if (_points.Contains(path[i]))
                {
                    break;
                }
            }

            for (int j = path.Count - 1; j > i; j--)
            {
                if (_points.Contains(path[j]))
                {
                    // shouldn't happen!
                    break;
                }
                _points.Insert(path[j]);
                _paret[path[j]] = path[j - 1];
            }
        }

        public List<Point> GetPathFromRoot(Point s)
        {
            List<Point> path = new List<Point>();
            if (!_points.Contains(s))
            {
                return null;
            }

            for (Point p = s; !p.Equals(_root); p = _paret[p])
            {
                path.Insert(0,p);
            }
            path.Insert(0, _root);
            return path;
        }
    }
}
