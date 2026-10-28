using System;

namespace MermaidRecognition.Layout.Sugiyama.Layout.MDS {
    /// <summary>
    /// Class for graoh layout with Multidimensional Scaling.
    /// </summary>
    internal static class Transform {
        /// <summary>
        /// Rotates a 2D configuration clockwise by a given angle.
        /// </summary>
        /// <param name="x">Coordinate vector.</param>
        /// <param name="y">Coordinate vector.</param>
        /// <param name="angle">Angle between 0 and 360.</param>
        public static void Rotate(double[] x, double[] y, double angle) {
            double sin = Math.Sin(angle * Math.PI / 180);
            double cos = Math.Cos(angle * Math.PI / 180);
            for (int i = 0; i < x.Length; i++) {
                double xNew = (cos * x[i]) + (sin * y[i]);
                double yNew = (cos * y[i]) - (sin * x[i]);
                x[i] = xNew;
                y[i] = yNew;
            }
        }
    }
}
