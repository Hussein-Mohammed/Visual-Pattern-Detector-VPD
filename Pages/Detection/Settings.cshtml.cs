using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coravel.Queuing.Interfaces;
using LongRunningSignalr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using OpenCvSharp;
using OpenCvSharp.Features2D;
using OpenCvSharp.Flann;
using VPD1p3.MyLibrary;

namespace VPD1p3.Pages.Detection
{
    public class SettingsModel : PageModel
    {
        public SelectList Options_KeyPoints { get; set; } = new SelectList(new List<string> { "SIFT", "FAST" }, "FAST");
        //public SelectList Options_KeyPoints { get; set; } = new SelectList(GlobalVariables.OptionsOfKeypoints, GlobalVariables.OptionsOfKeypoints[0]);

        [BindProperty]
        public string SelectedKpt { get; set; } = "FAST";

        public static string SelectedKpt_Static { get; set; } = "FAST";
        public static float SelectedParameter { get; set; } = 5;
        public static bool Scale { get; set; } = false;
        public static bool Rotation { get; set; } = false;

        public bool ResultsReady { get; set; }
        public int FilesCounter { get; set; } = 0;
        public int FilesCounter_Old { get; set; } = 0;

        private readonly IHubContext<JobProgressHub> _hubContext;
        private readonly IQueue _queue;
        private IWebHostEnvironment _hostingEnvironment;
        private readonly GlobalVariables _GlobalVariables;
        public SettingsModel(IWebHostEnvironment hostingEnvironment, GlobalVariables GlobalVariables, IQueue queue, IHubContext<JobProgressHub> hubContext)
        {
            _hostingEnvironment = hostingEnvironment;
            _GlobalVariables = GlobalVariables;
            _queue = queue;
            _hubContext = hubContext;
        }

        public void OnGet()
        {
            ViewData["JobId"] = Guid.NewGuid().ToString("N");
            ViewData["AnalysisFinished"] = "False";

            ResultsReady = false;

            foreach (var Style in _GlobalVariables.Patterns)
            {
                Style.Descriptors = "";
                Style.Keypoints.Clear();
            }

            _GlobalVariables.Images.Descriptors = "";
            _GlobalVariables.Images.Keypoints.Clear();

            // Delete all existing analysis data
            _GlobalVariables.DetectedFeatures = null;
            _GlobalVariables.SelectedDetectionsPerPattern.Clear();
            _GlobalVariables.DetectionsPerImage.Clear();
            _GlobalVariables.DetectionsPerPattern.Clear();
        }

        public void OnPostKptSelection()
        {
            SelectedKpt_Static = SelectedKpt;
            _GlobalVariables.Kpts_Detector = SelectedKpt_Static;
        }

        public void OnPostParameter(float Parameter)
        {
            if (SelectedKpt_Static == "SIFT")
                SelectedParameter = (int)Parameter;
            //GlobalVariables.Angle_Diff = (int)Parameter;
            if (SelectedKpt_Static == "FAST")
                SelectedParameter = Parameter;

            _GlobalVariables.Selected_Parameter = Parameter;
        }

        public void OnPostSetKSize(float KSize)
        {
            _GlobalVariables.KernelSize_Perc = KSize / 100;
        }

        public void OnPostTolerate(bool Scale, bool Rotation)
        {
            _GlobalVariables.Scale = Scale;
            _GlobalVariables.Rotation = Rotation;
        }

        public Task DetectPatterns(string jobId)
        {
            float ProgressPerc = 0;

            DirectoryInfo dir = new DirectoryInfo(_GlobalVariables.Images.FilePath);
            int NumOfImages = dir.GetFiles().Length + 2;
            float ImagePerc = 100 / (float)NumOfImages;

            _hubContext.Clients.Group(jobId).SendAsync("DetectProgress", Math.Round(ProgressPerc,0));

            // ***************************************
            // Load and analyse pattern images
            // ***************************************
            MyLibrary.VPD Detector = new MyLibrary.VPD(_hostingEnvironment, _GlobalVariables);

            int LabelledResult = Detector.LoadPatterns();
            if (LabelledResult < 0)
            {
                ProgressPerc = 123;
                _hubContext.Clients.Group(jobId).SendAsync("DetectProgress", Math.Round(ProgressPerc, 0));
                return Task.CompletedTask;
            }
            else
            {
                ProgressPerc += ImagePerc;
                _hubContext.Clients.Group(jobId).SendAsync("DetectProgress", Math.Round(ProgressPerc, 0));
            }
            
            // ****************************************
            // Detect patterns in data
            // ****************************************
            var TestImages = Directory.EnumerateFiles(_GlobalVariables.Images.FilePath, "*.*", SearchOption.AllDirectories).Where(s => s.ToLower().EndsWith(".jpeg") ||
            s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".tif") || s.ToLower().EndsWith(".tiff") || s.ToLower().EndsWith(".png")
            || s.ToLower().EndsWith(".bmp"));

            foreach (string Img in TestImages)
            {
                List<MyLibrary.Detection> TempDetections = new List<MyLibrary.Detection>();
                Mat Current_Image = new Mat(Img, ImreadModes.Grayscale);
                int DetectionResult = Detector.Detect(Current_Image, TempDetections, Img);
                if (DetectionResult < 0)
                {
                    ProgressPerc = 123;
                    _hubContext.Clients.Group(jobId).SendAsync("DetectProgress", Math.Round(ProgressPerc, 0));
                    return Task.CompletedTask;
                }

                TempDetections = TempDetections.OrderByDescending(x => x.strength).ToList();
            
                _GlobalVariables.DetectionsPerImage.Add(TempDetections);

                FilesCounter++;

                ProgressPerc += ImagePerc;
                _hubContext.Clients.Group(jobId).SendAsync("DetectProgress", Math.Round(ProgressPerc, 0));
            }

            // Save detections per pattern
            List<List<MyLibrary.Detection>> TempDets = new List<List<MyLibrary.Detection>>();
            foreach (ImgInfo TempInfo in _GlobalVariables.Patterns)
            {
                List<MyLibrary.Detection> TempList = new List<MyLibrary.Detection>();
                TempDets.Add(TempList);
            }

            foreach (List<MyLibrary.Detection> DetList in _GlobalVariables.DetectionsPerImage)
            {
                foreach (MyLibrary.Detection Det in DetList)
                {
                    TempDets[Det.labelnum].Add(Det);
                }
            }

            // Sort detections per pattern according to strength
            List<List<MyLibrary.Detection>> SortedTempDets = new List<List<MyLibrary.Detection>>();
            foreach (List<MyLibrary.Detection> TempList in TempDets)
            {
                SortedTempDets.Add(TempList.OrderByDescending(x => x.strength).ToList());
            }

            _GlobalVariables.DetectionsPerPattern = SortedTempDets;

            _GlobalVariables.TopN_PerClass.Clear();

            for (int ptrn=0;ptrn<_GlobalVariables.Patterns.Count();ptrn++)
            {
                int CurrentTopN = 6;
                if (CurrentTopN > _GlobalVariables.DetectionsPerPattern[ptrn].Count())
                    CurrentTopN = _GlobalVariables.DetectionsPerPattern[ptrn].Count();

                _GlobalVariables.TopN = CurrentTopN;
                _GlobalVariables.TopN_PerClass.Add(_GlobalVariables.TopN);
            }

            ProgressPerc += ImagePerc;
            _hubContext.Clients.Group(jobId).SendAsync("DetectProgress", Math.Round(ProgressPerc, 0));

            return Task.CompletedTask;
        }

        public void OnPostStartAnalysis()
        {
            string jobId = Guid.NewGuid().ToString("N");
            //_queue.QueueAsyncTask(() => DetectPatterns(jobId));
            _queue.QueueTask(() => DetectPatterns(jobId));

            ViewData["JobId"] = jobId;
        }
    }
}
