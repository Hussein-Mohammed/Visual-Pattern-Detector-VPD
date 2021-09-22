using Microsoft.AspNetCore.Hosting;
using OpenCvSharp;
using OpenCvSharp.Features2D;
using OpenCvSharp.Flann;
using VPD1p3.MyLibrary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VPD1p3.MyLibrary
{
    public class Detection
    {
        public OpenCvSharp.Point centre = new OpenCvSharp.Point(0, 0);
        public int labelnum = -1;
        public float strength = 0.0F;
        public bool correct;
        public Rect bbox = new Rect();
        public List<OpenCvSharp.Point> mypoints = new List<OpenCvSharp.Point>();
        public string FileName;
        public string FilePath;
    }

    public class FeaturesInfo
    {
        public List<int> alllabels = new List<int>();
        public Mat alldescs = new Mat();
        public List<KeyPoint> allpoints = new List<KeyPoint>();
        public List<Point2f> alloffsets = new List<Point2f>();
        public List<Rect> allrects = new List<Rect>();
        public List<Rect> medianrects_by_class = new List<Rect>();
        //public OpenCvSharp.Flann.Index alldescs_index;
    }

    public class Hypothesis
    {
        public int mypt;
        public int nearestpt;
        public Point2f offset;
        public Rect ROI;
    }

    public class DetectorInternalState
    {
        public List<Mat> obj_centre_vcount = new List<Mat>();
        public List<Mat> obj_centre_sdist = new List<Mat>();
        public List<Mat> obj_centre_kplists = new List<Mat>();
        public List<Mat> obj_centre_scale = new List<Mat>();
        public List<Mat> obj_centre_scalecount = new List<Mat>();
        public List<List<Hypothesis>> pt_store = new List<List<Hypothesis>>();
        public List<KeyPoint> detect_kps = new List<KeyPoint>();
    }

    public class VPD
    {
        public FeaturesInfo DetectedFeatures = new FeaturesInfo();
        public DetectorInternalState State = new DetectorInternalState();

        private IWebHostEnvironment _hostingEnvironment;
        private readonly GlobalVariables _GlobalVariables;

        public VPD(IWebHostEnvironment hostingEnvironment, GlobalVariables GlobalVariables)
        {
            _hostingEnvironment = hostingEnvironment;
            _GlobalVariables = GlobalVariables;
        }

        //public int LoadPatterns()
        public int LoadPatterns()
        {
            List<List<int>> widths_by_class = new List<List<int>>(_GlobalVariables.Patterns.Count());
            List<List<int>> heights_by_class = new List<List<int>>(_GlobalVariables.Patterns.Count());

            int Labels_Counter = 0;

            foreach (var Pattern in _GlobalVariables.Patterns)
            {
                //_GlobalVariables.TopN_PerClass.Add(_GlobalVariables.TopN);

                List<int> Widths_Temp = new List<int>();
                List<int> Heights_Temp = new List<int>();
                Mat CurrentDescs_Patterns = new Mat();
                int Keypoints_Num_Counter = 0;

                var PatternImgs = Directory.EnumerateFiles(Pattern.FilePath, "*.*", SearchOption.AllDirectories).Where(s => s.ToLower().EndsWith(".jpeg") ||
                s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".tif") || s.ToLower().EndsWith(".tiff") || s.ToLower().EndsWith(".png")
                || s.ToLower().EndsWith(".bmp"));

                // Determine the smallest and largest image in the current pattern 
                int SmallestAvg = 1000000;
                int LargestAvg = 0;
                string SmallestPatternImg = PatternImgs.ElementAt(0);
                string LargestPatternImg = PatternImgs.ElementAt(0);
                foreach (var img in PatternImgs)
                {
                    Mat CurrentImg = new Mat(img, ImreadModes.Grayscale); //load Grey

                    if (CurrentImg.Cols == 0 || CurrentImg.Rows == 0)
                    {
                        return 1;
                    }

                    int CurrentAverage = (CurrentImg.Cols + CurrentImg.Rows) / 2;
                    if (CurrentAverage > LargestAvg)
                    {
                        LargestAvg = CurrentAverage;
                        LargestPatternImg = img;
                    }
                    if (CurrentAverage < SmallestAvg)
                    {
                        SmallestAvg = CurrentAverage;
                        SmallestPatternImg = img;
                    }
                }

                // **********************************************************
                // Create an extended list of images for the current pattern
                // **********************************************************

                // Apply scaling
                List<Mat> ScaledImages_List = new List<Mat>();
                foreach (var img in PatternImgs)
                {
                    //Mat train_img_c = new Mat(img, ImreadModes.Color); //load RGB
                    Mat CurrentImg = new Mat(img, ImreadModes.Grayscale); //load Grey

                    if (CurrentImg.Cols == 0 || CurrentImg.Rows == 0)
                    {
                        return 1;
                    }

                    ScaledImages_List.Add(CurrentImg);

                    if (_GlobalVariables.Scale)
                    {
                        if (img == SmallestPatternImg)
                        {
                            Mat DownSizedImg = CurrentImg.Resize(new OpenCvSharp.Size(), 0.5, 0.5, InterpolationFlags.Area);
                            ScaledImages_List.Add(DownSizedImg);
                        }

                        if (img == LargestPatternImg)
                        {
                            Mat EnlargedImg = CurrentImg.Resize(new OpenCvSharp.Size(), 2, 2, InterpolationFlags.Cubic);
                            ScaledImages_List.Add(EnlargedImg);
                        }
                    }
                }

                // Apply Rotation
                List<Mat> CurrentImages_List = new List<Mat>();
                foreach (Mat img in ScaledImages_List)
                {
                    CurrentImages_List.Add(img);

                    if (_GlobalVariables.Rotation)
                    {
                        Mat LeftRotation = img.Clone();
                        RotateImage(20, 1, img, LeftRotation);
                        CurrentImages_List.Add(LeftRotation);

                        //var window = new Window("Rotate", image: LeftRotation);
                        //var key = Cv2.WaitKey();
                        //window.Dispose();
                        //Window.DestroyAllWindows();

                        Mat RightRotation = img.Clone();
                        RotateImage(-20, 1, img, RightRotation);
                        CurrentImages_List.Add(RightRotation);

                        //var window2 = new Window("Rotate", image: RightRotation);
                        //var key2 = Cv2.WaitKey();
                        //window2.Dispose();
                        //Window.DestroyAllWindows();
                    }
                }

                // Read the images of the current pattern
                foreach (Mat train_img in CurrentImages_List)
                {
                    // Detect keypoints in the current image
                    KeyPoint[] Current_Keypoints;
                    List<KeyPoint> Current_Keypoints_List = new List<KeyPoint>();
                    var SIFT_Obj = SIFT.Create();//0,3,0.04,CurParam.Edge);

                    if (_GlobalVariables.Kpts_Detector == "FAST")
                    {
                        var fastCPU = FastFeatureDetector.Create(1, true);
                        //var ORB_Kpt = ORB.Create(999999);
                        Current_Keypoints = fastCPU.Detect(train_img);
                        Current_Keypoints_List = Current_Keypoints.ToList();
                        Current_Keypoints_List = Current_Keypoints_List.OrderByDescending(x => x.Response).ToList();
                        int SizeOfKeypoints = Current_Keypoints_List.Count();
                        if (SizeOfKeypoints < 3)
                        {
                            return -1;
                        }

                        int ConsideredKpts = (int)(SizeOfKeypoints * _GlobalVariables.Selected_Parameter / 100.0);
                        if (ConsideredKpts < _GlobalVariables.Min_FAST && SizeOfKeypoints > _GlobalVariables.Min_FAST)
                            ConsideredKpts = _GlobalVariables.Min_FAST;
                        else if (ConsideredKpts < _GlobalVariables.Min_FAST && SizeOfKeypoints <= _GlobalVariables.Min_FAST)
                            ConsideredKpts = SizeOfKeypoints;

                        Current_Keypoints_List = Current_Keypoints_List.Take(ConsideredKpts).ToList();
                        Current_Keypoints = Current_Keypoints_List.ToArray();
                    }
                    else
                    {
                        Current_Keypoints = SIFT_Obj.Detect(train_img);
                        Current_Keypoints_List = Current_Keypoints.ToList();
                    }

                    Keypoints_Num_Counter += Current_Keypoints_List.Count();

                    Mat descriptors = new Mat();
                    SIFT_Obj.Compute(train_img, ref Current_Keypoints, descriptors);

                    Pattern.Keypoints.AddRange(Current_Keypoints);
                    Pattern.Label = Labels_Counter;
                    _GlobalVariables.KptsNum_Patterns.Add(Keypoints_Num_Counter);
                    _GlobalVariables.PatternsLabels.AddRange(Enumerable.Repeat(Labels_Counter, CurrentDescs_Patterns.Rows));

                    // calculate offsets
                    List<Point2f> offsets = new List<Point2f>(Current_Keypoints_List.Count());
                    for (int p = 0; p < Current_Keypoints_List.Count(); p++)
                    {
                        Point2f TempPoint = new Point2f((train_img.Cols / 2.0F - Current_Keypoints_List[p].Pt.X) / Current_Keypoints_List[p].Size, (train_img.Rows / 2.0F - Current_Keypoints_List[p].Pt.Y) / Current_Keypoints_List[p].Size);
                        offsets.Add(TempPoint);
                    }

                    // Gather points to calculate bounding box
                    List<Point2f> tmp_points = new List<Point2f>();
                    for (int p = 0; p < Current_Keypoints_List.Count(); p++)
                        tmp_points.Add(Current_Keypoints_List[p].Pt);

                    Rect bbox = Cv2.BoundingRect(tmp_points);

                    // push_back label, descriptors, points, offsets and rects
                    for (int r = 0; r < descriptors.Rows; r++)
                    {
                        DetectedFeatures.alllabels.Add(Pattern.Label);

                        DetectedFeatures.alldescs.Add(descriptors.Row(r));

                        //var TempArray = new float[descriptors.Row(r).Width * descriptors.Row(r).Height * descriptors.Row(r).ElemSize()];
                        //Marshal.Copy(descriptors.Row(r).Data, TempArray, 0, TempArray.Length);
                        //_GlobalVariables.DetectedFeatures.alldescs.Add(TempArray.ToList());

                        DetectedFeatures.allpoints.Add(Current_Keypoints_List[r]);
                        DetectedFeatures.alloffsets.Add(offsets[r]);
                        Rect TempRect = new Rect(0, 0, (int)(train_img.Cols / Current_Keypoints_List[r].Size), (int)(train_img.Rows / Current_Keypoints_List[r].Size));
                        DetectedFeatures.allrects.Add(TempRect);
                    }

                    // push back width/height by class
                    Widths_Temp.Add(bbox.Width);
                    Heights_Temp.Add(bbox.Height);

                    // widths_by_class[Pattern.Label].Add(bbox.Width);
                    // heights_by_class[Pattern.Label].Add(bbox.Height);
                }

                widths_by_class.Add(Widths_Temp);
                heights_by_class.Add(Heights_Temp);

                // Sort the widths and heights and save the middle entry in order to calculate the median
                for (int i = 0; i < widths_by_class.Count(); i++)
                {
                    //cout<<endl<<"loadTrainingSet:for(n_classes) Checkpoint 1"<<endl;
                    widths_by_class[i] = widths_by_class[i].OrderByDescending(x => x).ToList();
                    heights_by_class[i] = heights_by_class[i].OrderByDescending(x => x).ToList();

                    int MedianWidth = widths_by_class[i][widths_by_class[i].Count() / 2];
                    int MedianHeight = heights_by_class[i][heights_by_class[i].Count() / 2];
                    Rect TempRect = new Rect(0, 0, MedianWidth, MedianHeight);
                    DetectedFeatures.medianrects_by_class.Add(TempRect);

                    int Detection_Kernel = (int)((MedianWidth + MedianHeight) / 2.0 * _GlobalVariables.KernelSize_Perc);

                    if (Detection_Kernel < _GlobalVariables.Min_Ksize)
                        Detection_Kernel = _GlobalVariables.Min_Ksize;
                    _GlobalVariables.KernelSize_PerClass.Add(Detection_Kernel);
                }

                if (DetectedFeatures.alldescs.Rows < 0)
                //if (_GlobalVariables.DetectedFeatures.alldescs.Count() < 0)
                {
                    return 2;
                }

                _GlobalVariables.DetectedFeatures = DetectedFeatures;
                //KDTreeIndexParams TempParam = new KDTreeIndexParams(4);
                //OpenCvSharp.Flann.Index TempIndex = new OpenCvSharp.Flann.Index(_GlobalVariables.DetectedFeatures.alldescs, TempParam);

                //_GlobalVariables.DetectedFeatures.alldescs_index = TempIndex;

                Labels_Counter++;
            }
            return 0;
        }

        //public void Detect(Mat Image, List<Detection> Results, string FullPath)
        public int Detect(Mat Image, List<Detection> Results, string FullPath)
        {
            List<Mat> obj_cs = new List<Mat>();
            int result = Detect1(Image, obj_cs);
            if (result < 0)
            {
                return -1;
            }

            Detect2(obj_cs, Results, FullPath);

            return 0;
        }

        public int Detect1(Mat Image, List<Mat> obj_cs)
        {
            // Detect keypoints in the current image
            KeyPoint[] Current_Keypoints;
            List<KeyPoint> Current_Keypoints_List = new List<KeyPoint>();
            var SIFT_Obj = SIFT.Create();//0,3,0.04,CurParam.Edge);

            if (_GlobalVariables.Kpts_Detector == "FAST")
            {
                var fastCPU = FastFeatureDetector.Create(1, true);
                Current_Keypoints = fastCPU.Detect(Image);
                Current_Keypoints_List = Current_Keypoints.ToList();

                Current_Keypoints_List = Current_Keypoints_List.OrderByDescending(x => x.Response).ToList();
                int SizeOfKeypoints = Current_Keypoints_List.Count();
                if (SizeOfKeypoints < 3)
                {
                    return -1;
                }

                int ConsideredKpts = (int)(SizeOfKeypoints * _GlobalVariables.Selected_Parameter / 100.0);

                if (ConsideredKpts < _GlobalVariables.Min_FAST && SizeOfKeypoints > _GlobalVariables.Min_FAST)
                    ConsideredKpts = _GlobalVariables.Min_FAST;
                else if (ConsideredKpts < _GlobalVariables.Min_FAST && SizeOfKeypoints <= _GlobalVariables.Min_FAST)
                    ConsideredKpts = SizeOfKeypoints;

                Current_Keypoints_List = Current_Keypoints_List.Take(ConsideredKpts).ToList();
                Current_Keypoints = Current_Keypoints_List.ToArray();
            }
            else
            {
                Current_Keypoints = SIFT_Obj.Detect(Image);
                Current_Keypoints_List = Current_Keypoints.ToList();
            }

            State.detect_kps = Current_Keypoints_List;

            Mat descriptors = new Mat();
            SIFT_Obj.Compute(Image, ref Current_Keypoints, descriptors);

            /// Creating vcount, sdist and kplists for each class
            //State.obj_centre_vcount.clear();
            State.obj_centre_sdist.Clear();
            State.obj_centre_kplists.Clear();
            State.obj_centre_scale.Clear();
            State.obj_centre_scalecount.Clear();
            State.pt_store.Clear();
            int votes_rows = Image.Rows;
            int votes_cols = Image.Cols;
            //cout<<endl<<"Creating vcount, sdist and kplists for each class.."<<endl;
            for (int tmpi = 0; tmpi < _GlobalVariables.Patterns.Count(); tmpi++)
            {
                State.obj_centre_sdist.Add(Mat.Zeros(votes_rows, votes_cols, MatType.CV_32FC1));
                State.obj_centre_kplists.Add(Mat.Zeros(votes_rows, votes_cols, MatType.CV_32SC1) - 1);
                State.obj_centre_scale.Add(Mat.Zeros(votes_rows, votes_cols, MatType.CV_32FC1));
                State.obj_centre_scalecount.Add(Mat.Zeros(votes_rows, votes_cols, MatType.CV_32FC1));
            }

            /// Find out nearest neighbour for each point
            for (int d = 0; d < State.detect_kps.Count(); d++)
            {
                var curdesc = descriptors.Row(d);

                int[] knnin = new int[0];
                float[] knndis = new float[0];
                int Trees = 4;

                if (_GlobalVariables.DetectedFeatures.alldescs.Rows < _GlobalVariables.KnnSearch_NumOfNeighbours)
                {
                    _GlobalVariables.KnnSearch_NumOfNeighbours = _GlobalVariables.DetectedFeatures.alldescs.Rows;
                }

                if (_GlobalVariables.KnnSearch_NumOfNeighbours < 100)
                {
                    Trees = 1;
                }

                //if (Trees > _GlobalVariables.KnnSearch_NumOfNeighbours/4)
                //{
                //    Trees = _GlobalVariables.KnnSearch_NumOfNeighbours/4;
                //    //Trees = 1;
                //}

                /// This function takes the query descriptor and returns the nearest 11 neighbours along with their distances
                KDTreeIndexParams TempParam = new KDTreeIndexParams(Trees);
                OpenCvSharp.Flann.Index TempIndex = new OpenCvSharp.Flann.Index(_GlobalVariables.DetectedFeatures.alldescs, TempParam);

                TempIndex.KnnSearch(curdesc, out knnin, out knndis, _GlobalVariables.KnnSearch_NumOfNeighbours, new SearchParams(512));
                //_GlobalVariables.DetectedFeatures.alldescs_index.KnnSearch(curdesc, out knnin, out knndis, _GlobalVariables.KnnSearch_NumOfNeighbours, new SearchParams(500));

                int curx = (int)State.detect_kps[d].Pt.X;
                int cury = (int)State.detect_kps[d].Pt.Y;

                float lambda_d = State.detect_kps[d].Size; /// size of current keypoint

                //std::vector<float> vs(n_classes,0);

                float distb = knndis[_GlobalVariables.KnnSearch_NumOfNeighbours - 1];
                distb *= distb;

                List<bool> classused = new List<bool>();
                classused.AddRange(Enumerable.Repeat(false, _GlobalVariables.Patterns.Count()));

                //cout<<endl<<"detect1:Checkpoint 3"<<endl;
                for (int n = 0; n < _GlobalVariables.KnnSearch_NumOfNeighbours - 1; n++)
                {
                    int curlabel = _GlobalVariables.DetectedFeatures.alllabels[knnin[n]];
                    //float lambda_e = impl->allpoints[knnin[n]].size;
                    float lambda_e = _GlobalVariables.DetectedFeatures.allpoints[knnin[n]].Size; /// size of NN keypoint?

                    if (classused[curlabel])
                        continue;

                    float val = knndis[n];
                    val *= val; // + 1*dist*dist;

                    classused[curlabel] = true;

                    //cv::Point2f offset = impl->alloffsets[knnin[n]] * lambda_d;
                    Point2f offset = _GlobalVariables.DetectedFeatures.alloffsets[knnin[n]] * lambda_d;
                    int votex = (int)(curx + offset.X);
                    int votey = (int)(cury + offset.Y);

                    if (votex >= 0 && votex < State.obj_centre_sdist[curlabel].Cols &&
                            votey >= 0 && votey < State.obj_centre_sdist[curlabel].Rows)
                    {
                        /// Draw the expected centre...
                        /*
                        circle(VotingImage, Point(votex,votey), 3, colour, -1);
                        stringstream VotingImage_Stream;
                        //VotingImage_Stream << "Results/"<<"Kpt-"<<d <<".png";
                        VotingImage_Stream << "Results/"<<"Voting.png";
                        imwrite(VotingImage_Stream.str(),VotingImage);
                        */
                        State.obj_centre_sdist[curlabel].At<float>(votey, votex) += ((val - distb) / _GlobalVariables.KernelSize_PerClass[curlabel]); /// Apply Normalisation here!

                        int curkplist_idx = State.obj_centre_kplists[curlabel].At<int>(votey, votex);
                        Hypothesis temphypo = new Hypothesis();
                        temphypo.nearestpt = knnin[n];
                        temphypo.mypt = d;
                        //temphypo.ROI = impl->allrects[knnin[n]];
                        temphypo.ROI = _GlobalVariables.DetectedFeatures.allrects[knnin[n]];

                        /// width = height because the keypoints are squares
                        temphypo.ROI.Width *= (int)State.detect_kps[d].Size;
                        temphypo.ROI.Height *= (int)State.detect_kps[d].Size;

                        if (curkplist_idx == -1)
                        {
                            /// init kplist index
                            List<Hypothesis> tempvec = new List<Hypothesis>();
                            tempvec.Add(temphypo);
                            State.pt_store.Add(tempvec);
                            State.obj_centre_kplists[curlabel].At<int>(votey, votex) = State.pt_store.Count() - 1;
                        }
                        else
                            State.pt_store[curkplist_idx].Add(temphypo);

                        State.obj_centre_scale[curlabel].At<float>(votey, votex) += (lambda_d / lambda_e);
                        State.obj_centre_scalecount[curlabel].At<float>(votey, votex) += 1;
                    }

                }
            }


            /// create kernel per class for filtering
            for (int cls = 0; cls < _GlobalVariables.Patterns.Count(); cls++)
            {

                int ksize = _GlobalVariables.KernelSize_PerClass[cls];
                Mat kernelx_mask = Mat.Ones(ksize, 1, MatType.CV_32FC1);
                Mat kernely_mask = Mat.Ones(ksize, 1, MatType.CV_32FC1);

                Mat objc_sd = State.obj_centre_sdist[cls];
                Mat dst_sd = new Mat();


                obj_cs.Add(objc_sd); /// Detection Equation
            }

            _GlobalVariables.State = State;

            return 0;
        }

        public void Detect2(List<Mat> obj_cs, List<Detection> Results, string FullPath)
        {
            List<Detection> Detections = new List<Detection>();

            /// Finding detections...

            for (int m = 0; m < obj_cs.Count(); m++)
            {
                int ksize = _GlobalVariables.KernelSize_PerClass[m];

                Mat obj_c = obj_cs[m];
                Mat obj_c_sm = new Mat();
                //cv::GaussianBlur(obj_c, obj_c_sm, cv::Size(), 5);

                OpenCvSharp.Size ksize_Gaussian = new OpenCvSharp.Size();
                ksize = (ksize % 2 == 0) ? ksize - 1 : ksize;
                ksize_Gaussian.Width = ksize;
                ksize_Gaussian.Height = ksize;
                Cv2.GaussianBlur(obj_c, obj_c_sm, ksize_Gaussian, 0);

                Mat obj_c_dil = new Mat(obj_c.Size(), MatType.CV_32FC1);
                Mat minlocarray = new Mat();
                Mat element = Mat.Ones(3, 3, MatType.CV_8UC1);
                element.At<byte>(1, 1) = 0;
                Cv2.Dilate(-obj_c_sm, obj_c_dil, element);
                minlocarray = -obj_c_sm - obj_c_dil;

                /*
                        cv::normalize(obj_c, obj_c, 0, 255, cv::NORM_MINMAX);
                        cv::normalize(obj_c_sm, obj_c_sm, 0, 255, cv::NORM_MINMAX);
                        cv::normalize(obj_c_dil, obj_c_dil, 0, 255, cv::NORM_MINMAX);
                        cv::normalize(minlocarray, minlocarray, 0, 255, cv::NORM_MINMAX);

                        std::stringstream Org, Smoothed, Dilated, Eroded, Final;
                        Org << "DetectionMatrix_Org_Class_" << m << ".png";
                        Smoothed << "DetectionMatrix_Smoothed_Class_" << m << ".png";
                        Dilated << "DetectionMatrix_Dilated_Class_" << m << ".png";
                        Final << "DetectionMatrix_Final_Class_" << m << ".png";
                        imwrite(Org.str(), obj_c);
                        imwrite(Smoothed.str(), obj_c_sm);
                        imwrite(Dilated.str(), obj_c_dil);
                        imwrite(Final.str(), minlocarray);
                */

                for (int _x = 0; _x < minlocarray.Cols; _x++)
                    for (int _y = 0; _y < minlocarray.Rows; _y++)
                    {
                        if (minlocarray.At<float>(_y, _x) <= 0)
                            continue;

                        OpenCvSharp.Point minloc = new OpenCvSharp.Point(_x, _y);
                        //double min = obj_c.at<float>(_y,_x);
                        float min = obj_c_sm.At<float>(_y, _x);

                        // cv::Rect roi( minloc.x*params.vote_scale-50, minloc.y*params.vote_scale-20, 100, 40);
                        // std::cout << "minloc for " << m << " = " << minloc << std::endl;

                        /// Collect all the individual points which voted for this hypothesis
                        List<OpenCvSharp.Point> _mypoints = new List<OpenCvSharp.Point>();
                        List<int> _ptindices = new List<int>();
                        List<int> widths = new List<int>();
                        List<int> heights = new List<int>();

                        for (int indy = minloc.Y - ksize / 2; indy < minloc.Y + ksize / 2; indy++)
                        {
                            for (int indx = minloc.X - ksize / 2; indx < minloc.X + ksize / 2; indx++)
                            {
                                if (indy < 0 || indy >= _GlobalVariables.State.obj_centre_kplists[m].Rows || indx < 0 || indx >= _GlobalVariables.State.obj_centre_kplists[m].Cols)
                                    continue;

                                int curind = _GlobalVariables.State.obj_centre_kplists[m].At<int>(indy, indx);

                                if (curind > -1 && curind < _GlobalVariables.State.pt_store.Count())
                                {
                                    for (int ptind = 0; ptind < _GlobalVariables.State.pt_store[curind].Count(); ptind++)
                                    {
                                        _ptindices.Add(_GlobalVariables.State.pt_store[curind][ptind].mypt);
                                        widths.Add(_GlobalVariables.State.pt_store[curind][ptind].ROI.Width);
                                        heights.Add(_GlobalVariables.State.pt_store[curind][ptind].ROI.Height);
                                    }
                                }
                            }
                        }

                        if (_ptindices.Count() == 0)
                            continue;

                        //std::vector<int> distances, distancessorted;
                        for (int ptind = 0; ptind < _ptindices.Count(); ptind++)
                        {
                            OpenCvSharp.Point curpt = (OpenCvSharp.Point)_GlobalVariables.State.detect_kps[_ptindices[ptind]].Pt;
                            _mypoints.Add(curpt);
                        }

                        widths = widths.OrderByDescending(x => x).ToList();
                        heights = heights.OrderByDescending(x => x).ToList();

                        int medianw = widths[widths.Count() / 2];
                        int medianh = heights[heights.Count() / 2];


                        Detection det = new Detection();
                        det.FilePath = FullPath;
                        det.FileName = Path.GetFileName(FullPath);
                        det.strength = -min;
                        det.centre = minloc;
                        // Rect _bbox = boundingRect(_mypoints);
                        Rect _bbox = new Rect(det.centre.X - medianw / 2, det.centre.Y - medianh / 2, medianw, medianh);

                        det.bbox = _bbox; //boundingRect(bbpoints);
                        det.labelnum = m;
                        det.mypoints = _mypoints;

                        Rect img_rect = new Rect(0, 0, obj_cs[m].Width, obj_cs[m].Height);
                        Rect _interbox = det.bbox & img_rect;
                        float Intersection = _interbox.Width * _interbox.Height;
                        float bbox_Area = det.bbox.Width * det.bbox.Height;
                        if ((Intersection / bbox_Area) < 1.00)
                        {
                            continue;
                        }


                        //if (min < _GlobalVariables.Detection_Threshold) // Uncomment if you want to apply the detection threshold
                        {
                            Detections.Add(det);
                        }
                    }
            }

            removeOverlappingDetections(Detections, Results);
        }

        public void removeOverlappingDetections(List<Detection> input_detections, List<Detection> output_detections)
        {
            /// Eliminate overlapping detections
            List<Tuple<float, int>> allboxes = new List<Tuple<float, int>>();
            List<bool> remaining = Enumerable.Repeat(true, input_detections.Count()).ToList();
            for (int i = 0; i < input_detections.Count(); i++)
            {
                Tuple<float, int> Temp = new Tuple<float, int>(input_detections[i].strength, i);
                allboxes.Add(Temp);
            }

            allboxes = allboxes.OrderByDescending(x => x.Item1).ToList();

            // std::cout << detections.size() << " " << allboxes.size() << std::endl;
            for (int d = 0; d < allboxes.Count() - 1; d++)
            {
                Detection curdet = input_detections[allboxes[d].Item2];
                Rect dbox = curdet.bbox;

                for (int c = d + 1; c < allboxes.Count(); c++)
                {
                    if (remaining[c] == false)
                        continue;
                    Detection testdet = input_detections[allboxes[c].Item2];
                    //if (curdet.labelnum != testdet.labelnum) continue;

                    Rect cbox = testdet.bbox;

                    Rect _interbox = new Rect();
                    _interbox = dbox & cbox;
                    float _inter = _interbox.Width * _interbox.Height;
                    float _union = (cbox.Width * cbox.Height) + (dbox.Width * dbox.Height) - _inter;

                    if (_inter / _union >= _GlobalVariables.Overlap_Threshold)
                        remaining[c] = false;

                    // check if same class boxes are (almost) contained in themselves
                    if (curdet.labelnum == testdet.labelnum)
                    {
                        float cbox_Area = cbox.Width * cbox.Height;
                        float dbox_Area = dbox.Width * dbox.Height;
                        if (((_inter / cbox_Area) > _GlobalVariables.mergeOverlap_Threshold && cbox_Area < dbox_Area) || //c merge_overlap_thresh inside d
                                    ((_inter / dbox_Area) > _GlobalVariables.mergeOverlap_Threshold && dbox_Area < cbox_Area)) //d merge_overlap_thresh inside c
                            remaining[c] = false; // merge
                    }
                }
            }
            // std::cout << remaining.size() << std::endl;
            output_detections.Clear();
            for (int d = 0; d < allboxes.Count(); d++)
                if (remaining[d])
                    output_detections.Add(input_detections[allboxes[d].Item2]);
        }

        public void DrawDetections(Mat img, List<Detection> dets, int width)
        {

            for (int r = 0; r < dets.Count(); r++)
            {
                int labelnum2 = dets[r].labelnum + 1;

                Scalar colour = new Scalar((labelnum2 & 1) * 255, (labelnum2 & 2) * 255, (labelnum2 & 4) * 255);

                string objname = _GlobalVariables.Patterns[dets[r].labelnum].PatternName;

                // Bounding Box
                Cv2.Rectangle(img, dets[r].bbox, new Scalar(255, 255, 255), width * 2);
                // rectangle(img, dets[r].bbox, Scalar(0,0,0),1);
                Cv2.Rectangle(img, dets[r].bbox, colour, width);

                // Convex Hull
                // std::vector<std::vector<Point> > polys;
                // polys.push_back(dets[r].hull);
                // polylines(img, polys, true, colour);

                // Object Centre
                // circle( img, dets[r].centre, 4, Scalar(255,255,255), -1 );
                Cv2.Circle(img, dets[r].centre, 4, colour, -1);

                // Points
                // for(unsigned p=0; p<dets[r].mypoints.size(); p++)
                // {
                //     circle( img, dets[r].mypoints[p], 4, colour );
                // }

                // Object Class
                OpenCvSharp.Point textpt = dets[r].bbox.TopLeft;
                Cv2.PutText(img, objname, textpt, HersheyFonts.HersheyPlain, width, colour);
            }

        }

        public void RotateImage(double angle, double scale, Mat src, Mat dst)
        {
            var imageCenter = new Point2f(src.Cols / 2f, src.Rows / 2f);
            var rotationMat = Cv2.GetRotationMatrix2D(imageCenter, angle, scale);
            Cv2.WarpAffine(src, dst, rotationMat, src.Size());
            using (Mat mask = dst.InRange(new Scalar(0, 0, 0), new Scalar(1, 1, 1)))
            {
                dst.SetTo(new Scalar(255, 255, 255), mask);
            }
        }

    }
}