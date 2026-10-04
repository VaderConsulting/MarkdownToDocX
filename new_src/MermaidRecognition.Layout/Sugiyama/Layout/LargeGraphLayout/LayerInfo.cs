using System.ComponentModel;

//using TestWpfViewer.Annotations;

namespace MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout
{
    public class ILayerInfo : INotifyPropertyChanged
    {
        public virtual string getLabel() => "";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler? handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

         bool _isVisible;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                OnPropertyChanged("IsVisible");
            }
        }

        bool _isActive;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged("IsActive");
            }
        }
    }

    public class ZoomLayerInfo(int zoomLevel) : ILayerInfo
    {
        public int ZoomLevel = zoomLevel;

        //public List<ISublayerInfo> SublayerInfos = new List<ISublayerInfo>(); 
         NodesSublayerInfo _nodesSublayerInfo = new NodesSublayerInfo();

        public NodesSublayerInfo NodesSlInf
        { get => _nodesSublayerInfo; set => _nodesSublayerInfo = value;
        }

        EdgesSublayerInfo _edgesSublayerInfo = new EdgesSublayerInfo();

        public EdgesSublayerInfo EdgesSlInf
        { get => _edgesSublayerInfo; set => _edgesSublayerInfo = value;
        }

        CellsSublayerInfo _cellsSublayerInfo = new CellsSublayerInfo();
        public CellsSublayerInfo CellsSlInf
        { get => _cellsSublayerInfo; set => _cellsSublayerInfo = value;
        }

        public override string getLabel() => "level " + ZoomLevel;
    }

    public class SkeletonLayerInfo(int zoomLevel) : ILayerInfo
    {
        public int ZoomLevel = zoomLevel;
        public override string getLabel() => "skeleton of level " + ZoomLevel;
    }

    public class LooseLayerInfo : ILayerInfo
    {
        public override string getLabel() => "unassigned";
    }

    public interface ISublayerInfo
    {
        string getLabel();
    }

    public class NodesSublayerInfo : ISublayerInfo, INotifyPropertyChanged
    {
         bool _isVisible = true;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                OnPropertyChanged("IsVisible");
            }
        }
        public string getLabel() => "nodes";

        public event PropertyChangedEventHandler? PropertyChanged;

        //[NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName = null)//[CallerMemberName]
        {
            PropertyChangedEventHandler? handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class EdgesSublayerInfo : ISublayerInfo, INotifyPropertyChanged
    {
         bool _isVisible;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                OnPropertyChanged("IsVisible");
            }
        }
        public string getLabel() => "edges";

        public event PropertyChangedEventHandler? PropertyChanged;

        //[NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName = null)//[CallerMemberName]
        {
            PropertyChangedEventHandler? handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class CellsSublayerInfo : ISublayerInfo, INotifyPropertyChanged
    {
         bool _isVisible = false;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                OnPropertyChanged("IsVisible");
            }
        }
        public string getLabel() => "cells";

        public event PropertyChangedEventHandler? PropertyChanged;

        //[NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName = null)//[CallerMemberName]
        {
            PropertyChangedEventHandler? handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
