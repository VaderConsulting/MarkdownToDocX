namespace Microsoft.AGL.Core.DataStructures
{


    /// <summary>
    /// Size structure
    /// </summary>
    /// <remarks>
    /// constructor
    /// </remarks>
    /// <param name="width"></param>
    /// <param name="height"></param>
    public struct Size(double width, double height)
    {
    double width = width;
    /// <summary>
    /// width
    /// </summary>
    public double Width
    { get => width; set => width = value;
    }
        double height = height;
    /// <summary>
    /// Height
    /// </summary>
    public double Height
    { get => height; set => height = value;
    }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        public static Size operator /(Size s, double d) => new Size(s.Width / d, s.Height / d);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        public static Size operator *(Size s, double d) => new Size(s.Width * d, s.Height * d);


        /// <summary>
        /// padding the size ( from both sides!)
        /// </summary>
        /// <param name="padding"></param>
        public void Pad(double padding) {
          width += 2*padding;
          height += 2*padding;
      }
  }


}
