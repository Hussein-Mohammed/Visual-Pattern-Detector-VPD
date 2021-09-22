using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VPD1p3.MyLibrary
{
    public class GlobalVariables
    {
        public List<ImgInfo> Patterns = new List<ImgInfo>();

        public ImgInfo Images = new ImgInfo();

        public float Counter { get; set; } = 0.0F;
        public int KernelSize = 50;
        public int KnnSearch_NumOfNeighbours = 11;
        public int KnnSearch_Checks = 256;
        public int Min_FAST = 5;
        public int Min_Ksize = 5;
        public int TopN = 1;
        public List<int> KernelSize_PerClass = new List<int>();
        public List<int> TopN_PerClass = new List<int>();
        public List<int> KptsNum_Patterns = new List<int>();
        public List<int> PatternsLabels = new List<int>();
        public List<string> PatternsNames = new List<string>();
        public float KernelSize_Perc = 0.4F;
        public float Detection_Threshold = -1000;
        public float Overlap_Threshold = 0.2F;
        public float mergeOverlap_Threshold = 0.2F;        
        public float Selected_Parameter = 5;
        public string Kpts_Detector = "FAST";
        public List<List<Detection>> DetectionsPerImage = new List<List<Detection>>();
        public List<List<Detection>> DetectionsPerPattern = new List<List<Detection>>();
        public List<List<Detection>> SelectedDetectionsPerPattern = new List<List<Detection>>();

        public FeaturesInfo DetectedFeatures = new FeaturesInfo();
        public DetectorInternalState State = new DetectorInternalState();

        public bool Scale { get; set; } = false;
        public bool Rotation { get; set; } = false;

        public int Random { get; set; } = 0;
    }
}
