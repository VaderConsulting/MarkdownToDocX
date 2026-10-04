using System;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;


namespace MermaidRecognition.Layout.Sugiyama.Core.Layout
{
    /// <summary>
    /// The base class of the Graph,Node and Edge classes
    /// </summary>
#if TEST_MSAGL
    
#endif
    public abstract class GeometryObject
    {

        GeometryObject geometryParent;
        /// <summary>
        /// the parent of the object
        /// </summary>
        public GeometryObject GeometryParent
        { get => geometryParent; set => geometryParent = value;
        }

        /// <summary>
        /// Storage for any data algorithms may want to store temporarily.
        /// </summary>
#if TEST_MSAGL
        [NonSerialized]
#endif

        public object AlgorithmData;

        /// <summary>
        /// keeps the back pointer to the user data
        /// </summary>
        public object UserData { get; set; }

        event EventHandler<LayoutChangeEventArgs> beforeLayoutChangeEvent;

        /// <summary>
        /// event signalling that the layout is about to change
        /// </summary>
        public virtual event EventHandler<LayoutChangeEventArgs> BeforeLayoutChangeEvent
        { add => beforeLayoutChangeEvent += value; remove => beforeLayoutChangeEvent -= value;
        }

        /// <summary>
        /// gets or sets the boundary box of a GeometryObject
        /// </summary>
        public abstract Rectangle BoundingBox { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newValue"></param>
        public virtual void RaiseLayoutChangeEvent(object newValue)
        {
            if (beforeLayoutChangeEvent != null)
            {
                beforeLayoutChangeEvent(this, new LayoutChangeEventArgs { DataAfterChange = newValue });
            }
        }


    }

}
