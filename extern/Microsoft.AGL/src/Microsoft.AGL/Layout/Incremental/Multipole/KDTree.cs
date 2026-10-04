using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.MinEnclosingDisc;

namespace Microsoft.AGL.Layout.Incremental.Multipole
{
    /// <summary>
    /// A KDTree recursively divides particles in a 2D space into a balanced tree structure by doing horizontal splits for wide bounding boxes and vertical splits for tall bounding boxes.
    /// </summary>
    public class KDTree
    {
        Particle[] particles;
        internal InternalKdNode root; 
        List<LeafKdNode> leaves;
        private Particle[] particlesBy(Particle.Dim d) => (from Particle p in particles
                                                           orderby p.pos(d)
                                                           select p).ToArray();

        /// <summary>
        /// Create a KDTree over the specified particles, with the leaf partitions each containing bucketSize particles.
        /// </summary>
        /// <param name="particles"></param>
        /// <param name="bucketSize"></param>
        public KDTree(Particle[] particles, int bucketSize)
        {
            this.particles = particles;
            Particle[][] ps = new Particle[][] {
                particlesBy(Particle.Dim.Horizontal),
                particlesBy(Particle.Dim.Vertical)};
            leaves = [];
            LeafKdNode l = new LeafKdNode(ps);
            leaves.Add(l);
            root = l.Split(out LeafKdNode r);
            leaves.Add(r);
            SplitQueue splitQueue = new SplitQueue(bucketSize);
            splitQueue.Enqueue(l, r);
            while (splitQueue.Count > 0)
            {
                l = splitQueue.Dequeue();
                l.Split(out r);
                leaves.Add(r);
                splitQueue.Enqueue(l, r);
            }
        }
        /// <summary>
        /// Compute forces between particles using multipole approximations.
        /// </summary>
        /// <param name="precision"></param>
        public void ComputeForces(int precision) {
            root.computeMultipoleCoefficients(precision);
            foreach (LeafKdNode l in leaves)
            {
                l.ComputeForces();
                List<KdNode> stack = [root];
                while (stack.Count > 0)
                {
                    KdNode v = stack.Last();
                    stack.RemoveAt(stack.Count - 1);
                    if (!l.intersects(v))
                    {
                        foreach (Particle p in l.particles[0])
                        {
                            p.force -= v.multipoleCoefficients.ApproximateForce(p.point);
                        }
                    }
                    else
                    {
                        if (v is LeafKdNode leaf)
                        {
                            foreach (Particle p in l.particles[0])
                            {
                                foreach (Particle q in leaf.particles[0])
                                {
                                    if (p != q)
                                    {
                                        p.force += MultipoleCoefficients.Force(p.point, q.point);
                                    }
                                }
                            }
                        }
                        else
                        {
                            InternalKdNode n = v as InternalKdNode;
                            stack.Add(n.leftChild);
                            stack.Add(n.rightChild);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Particles used in KDTree multipole force approximations
        /// </summary>
        /// <remarks>
        /// Create particle at point
        /// </remarks>
        /// <param name="point"></param>
        public class Particle(Point point)
        {
            internal enum Dim { Horizontal = 0, Vertical = 1 };
            internal Point force = new Point(0, 0);
            internal Point point = point;
            internal bool splitLeft;
            internal double pos(Dim d) => d == Dim.Horizontal ? point.X : point.Y;
        }
        internal abstract class KdNode
        {
            internal InternalKdNode parent;
            internal Disc med;
            internal MultipoleCoefficients multipoleCoefficients;
            internal bool intersects(KdNode v)
            {
                Point d = v.med.Center - med.Center;
                double l = d.Length;
                return l < v.med.Radius + med.Radius;
            }
            internal abstract void computeMultipoleCoefficients(int precision);
        }
        internal class LeafKdNode : KdNode
        {
            internal Particle[][] particles;
            internal Point[] ps;
            internal LeafKdNode(Particle[][] particles)
            {
                Debug.Assert(particles[0].Length == particles[1].Length);
                this.particles = particles;
                ComputeMED();
            }
            internal override void computeMultipoleCoefficients(int precision) => multipoleCoefficients = new MultipoleCoefficients(precision, med.Center, ps);
            internal Disc ComputeMED()
            {
                int n = Size();
                ps = new Point[n];
                for (int i = 0; i < n; ++i)
                {
                    ps[i] = particles[0][i].point;
                }
                return med = MinimumEnclosingDisc.LinearComputation(ps);
            }
            private double Min(Particle.Dim d) => particles[(int)d][0].pos(d);
            internal int Size() => particles[0].Length;
            private double Max(Particle.Dim d) => particles[(int)d][Size() - 1].pos(d);
            private double Dimension(Particle.Dim d) => Max(d) - Min(d);
            internal InternalKdNode Split(out LeafKdNode rightSibling)
            {
                Particle.Dim splitDirection =
                    Dimension(Particle.Dim.Horizontal) > Dimension(Particle.Dim.Vertical)
                    ? Particle.Dim.Horizontal : Particle.Dim.Vertical;
                Particle.Dim nonSplitDirection =
                    splitDirection == Particle.Dim.Horizontal
                    ? Particle.Dim.Vertical : Particle.Dim.Horizontal;
                int n = Size(), nLeft = n / 2, nRight = n - nLeft;
                Particle[][]
                    leftParticles = new Particle[][] { new Particle[nLeft], new Particle[nLeft] },
                    rightParticles = new Particle[][] { new Particle[nRight], new Particle[nRight] };
                int lCtr = 0, rCtr = 0;
                for (int i = 0; i < n; ++i)
                {
                    Particle p = particles[(int)splitDirection][i];
                    if (i < nLeft)
                    {
                        leftParticles[(int)splitDirection][i] = p;
                        p.splitLeft = true;
                    }
                    else
                    {
                        rightParticles[(int)splitDirection][i - nLeft] = p;
                        p.splitLeft = false;
                    }
                }
                for (int i = 0; i < n; ++i)
                {
                    Particle p = particles[(int)nonSplitDirection][i];
                    if (p.splitLeft)
                    {
                        leftParticles[(int)nonSplitDirection][lCtr++] = p;
                    }
                    else
                    {
                        rightParticles[(int)nonSplitDirection][rCtr++] = p;
                    }
                }
                Debug.Assert(lCtr == nLeft);
                Debug.Assert(rCtr == nRight);
                Disc parentMED = med;
                particles = leftParticles;
                ComputeMED();
                rightSibling = new LeafKdNode(rightParticles);
                return new InternalKdNode(parentMED, this, rightSibling);
            }
            internal void ComputeForces()
            {
                foreach (Particle u in particles[0])
                {
                    foreach (Particle v in particles[0])
                    {
                        if (u != v)
                        {
                            u.force += MultipoleCoefficients.Force(u.point, v.point);
                        }
                    }
                }
            }
        }
        internal class InternalKdNode : KdNode
        {
            internal KdNode leftChild;
            internal KdNode rightChild;
            internal InternalKdNode(Disc med, KdNode left, KdNode right)
            {
                this.med = med;
                parent = left.parent;
                if (parent != null)
                {
                    if (parent.leftChild == left)
                    {
                        parent.leftChild = this;
                    }
                    else
                    {
                        Debug.Assert(parent.rightChild == left);
                        parent.rightChild = this;
                    }
                }
                leftChild = left;
                rightChild = right;
                left.parent = this;
                right.parent = this;
            }
            internal override void computeMultipoleCoefficients(int precision)
            {
                leftChild.computeMultipoleCoefficients(precision);
                rightChild.computeMultipoleCoefficients(precision);
                multipoleCoefficients = new MultipoleCoefficients(med.Center, leftChild.multipoleCoefficients, rightChild.multipoleCoefficients);
            }
        }
        class SplitQueue(int B) : Queue<LeafKdNode>
        {
            int B = B;

            public void Enqueue(LeafKdNode l, LeafKdNode r)
            {
                if (l.Size() > B)
                {
                    Enqueue(l);
                }
                if (r.Size() > B)
                {
                    Enqueue(r);
                }
            }
        }
    }
}
