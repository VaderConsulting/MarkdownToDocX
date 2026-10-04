using System;

using MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Visibility {
    internal class Diagonal {

        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", Start, End);
        internal Point Start => leftTangent.End.Point;

        internal Point End => rightTangent.End.Point;

        internal Diagonal(Tangent leftTangent, Tangent rightTangent) {
            LeftTangent = leftTangent;
            RightTangent = rightTangent;
        }

        Tangent leftTangent;

        internal Tangent LeftTangent { get => leftTangent; set => leftTangent = value;
        }
        Tangent rightTangent;

        internal Tangent RightTangent { get => rightTangent; set => rightTangent = value;
        }

        RBNode<Diagonal> rbNode;

        internal RBNode<Diagonal> RbNode { get => rbNode; set => rbNode = value;
        }
    }
}
