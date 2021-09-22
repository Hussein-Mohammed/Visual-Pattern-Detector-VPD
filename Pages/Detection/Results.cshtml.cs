using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Coravel.Queuing.Interfaces;
using LongRunningSignalr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using OpenCvSharp;
using VPD1p3.MyLibrary;

namespace VPD1p3.Pages.Detection
{
    public class ResultsModel : PageModel
    {
        public static List<int> TopN_PerClass_Local { get; set; }

        private readonly IHubContext<JobProgressHub> _hubContext;
        private readonly IQueue _queue;
        private IWebHostEnvironment _hostingEnvironment;
        private readonly GlobalVariables _GlobalVariables;
        public ResultsModel(IWebHostEnvironment hostingEnvironment, GlobalVariables GlobalVariables, IQueue queue, IHubContext<JobProgressHub> hubContext)
        {
            _hostingEnvironment = hostingEnvironment;
            _GlobalVariables = GlobalVariables;
            _queue = queue;
            _hubContext = hubContext;
        }

        public void OnGet()
        {
            TopN_PerClass_Local = _GlobalVariables.TopN_PerClass.ToList();

            // Save detection images to their corresponding folders according to the selected Threshold
            MyLibrary.VPD Detector = new MyLibrary.VPD(_hostingEnvironment, _GlobalVariables);
            string FullImages = "FullImages";
            string CroppedImages = "CroppedImages";
            string DetectionsPerImage = "DetectionsPerImage";
            string webRootPath = _hostingEnvironment.WebRootPath;
            string Detections = "Detections";
            string BestDetections = "BestDetections";
            string WorstDetections = "WorstDetections";
            string Detections_Path = Path.Combine(webRootPath, Detections);
            if (!Directory.Exists(Detections_Path))
            {
                Directory.CreateDirectory(Detections_Path);
            }
            // Delete all files in Temp directory before starting to save new files
            Directory.EnumerateFiles(Detections_Path).ToList().ForEach(f => System.IO.File.Delete(f));
            foreach (var dir in Directory.EnumerateDirectories(Detections_Path).ToList())
            {
                Directory.EnumerateFiles(dir).ToList().ForEach(f => System.IO.File.Delete(f));
            }
            Directory.EnumerateDirectories(Detections_Path).ToList().ForEach(f => Directory.Delete(f, true));

            // Create directories per pattern to save detections
            int PatternsCounter = 0;
            foreach (List<MyLibrary.Detection> DetsPerPat in _GlobalVariables.DetectionsPerPattern)
            {
                List<MyLibrary.Detection> ConsideredDetsList = new List<MyLibrary.Detection>();

                int CurrentThreshold = _GlobalVariables.TopN_PerClass[PatternsCounter];
                if (CurrentThreshold < 1)
                    CurrentThreshold = 1;
                
                ConsideredDetsList.AddRange(DetsPerPat.Take(CurrentThreshold).ToList());
                _GlobalVariables.SelectedDetectionsPerPattern.Add(ConsideredDetsList);
                string PatternName = _GlobalVariables.Patterns[PatternsCounter].PatternName;
                string DetsPerPattern_Path = Path.Combine(Detections_Path, PatternName);
                if (!Directory.Exists(DetsPerPattern_Path))
                {
                    Directory.CreateDirectory(DetsPerPattern_Path);
                }

                string FullImages_Path = Path.Combine(DetsPerPattern_Path, FullImages);
                if (!Directory.Exists(FullImages_Path))
                {
                    Directory.CreateDirectory(FullImages_Path);
                }

                string CroppedImages_Path = Path.Combine(DetsPerPattern_Path, CroppedImages);
                if (!Directory.Exists(CroppedImages_Path))
                {
                    Directory.CreateDirectory(CroppedImages_Path);
                }

                string DetectionsPerImage_Path = Path.Combine(DetsPerPattern_Path, DetectionsPerImage);
                if (!Directory.Exists(DetectionsPerImage_Path))
                {
                    Directory.CreateDirectory(DetectionsPerImage_Path);
                }

                string BestDetections_Path = Path.Combine(DetsPerPattern_Path, BestDetections);
                if (!Directory.Exists(BestDetections_Path))
                {
                    Directory.CreateDirectory(BestDetections_Path);
                }

                string WorstDetections_Path = Path.Combine(DetsPerPattern_Path, WorstDetections);
                if (!Directory.Exists(WorstDetections_Path))
                {
                    Directory.CreateDirectory(WorstDetections_Path);
                }

                // Draw Detections Per Image for the current Pattern
                List<List<MyLibrary.Detection>> PerImgDets = new List<List<MyLibrary.Detection>>(_GlobalVariables.Images.FileNames.Count());
                foreach (List<MyLibrary.Detection> OnlyToIterate in _GlobalVariables.DetectionsPerImage)
                {
                    List<MyLibrary.Detection> TempList = new List<MyLibrary.Detection>();
                    PerImgDets.Add(TempList);
                }
                int NamesCounter = 0;
                foreach (string name in _GlobalVariables.Images.FileNames)
                {
                    foreach (MyLibrary.Detection Det in ConsideredDetsList)
                    {
                        if (Det.FileName == name)
                        {
                            PerImgDets[NamesCounter].Add(Det);
                        }
                    }
                    NamesCounter++;
                }
                foreach (List<MyLibrary.Detection> DetsPerImg in PerImgDets)
                {
                    List<MyLibrary.Detection> TempList = new List<MyLibrary.Detection>();
                    string CurrentImagePath = "Empty";
                    string CurrentImageName = "Empty";
                    foreach (MyLibrary.Detection Det in DetsPerImg)
                    {

                        if (Det.labelnum == PatternsCounter)
                        {
                            TempList.Add(Det);
                        }

                        if (CurrentImagePath == "Empty")
                        {
                            CurrentImagePath = Det.FilePath;
                        }

                        if (CurrentImageName == "Empty")
                        {
                            CurrentImageName = Det.FileName;
                        }
                    }
                    TempList = TempList.Take(CurrentThreshold).ToList();

                    if (TempList.Count() > 0)
                    {
                        Mat Current_Image = new Mat(CurrentImagePath, ImreadModes.Color);
                        Detector.DrawDetections(Current_Image, TempList, 1);
                        string DetectionsPerImage_ResultPath = DetectionsPerImage_Path + "\\" + Path.GetFileNameWithoutExtension(CurrentImageName) + ".jpg";
                        Current_Image.ImWrite(DetectionsPerImage_ResultPath);
                    }
                }

                // Draw and save each detection separately to the corresponding directory
                int DetCounter = 0;
                foreach (MyLibrary.Detection Det in ConsideredDetsList)
                {
                    Mat Current_Image = new Mat(Det.FilePath, ImreadModes.Color);
                    List<MyLibrary.Detection> TempDet = new List<MyLibrary.Detection>();
                    TempDet.Add(Det);

                    // Draw detection on the original image
                    Detector.DrawDetections(Current_Image, TempDet, 1);
                    string DetsFullImages_ResultPath = FullImages_Path + "\\" + Path.GetFileNameWithoutExtension(Det.FileName) + "_" + DetCounter + ".jpg";
                    Current_Image.ImWrite(DetsFullImages_ResultPath);

                    // Crop detection from the original image
                    Mat ROI_Detection = new Mat(Current_Image, Det.bbox);
                    string DetsCroppedImages_ResultPath = CroppedImages_Path + "\\" + Path.GetFileNameWithoutExtension(Det.FileName) + "_" + DetCounter + ".jpg";
                    ROI_Detection.ImWrite(DetsCroppedImages_ResultPath);

                    DetCounter++;
                }

                // Save the Best x detections
                int ImgsNum = 3;
                if (ConsideredDetsList.Count() < 3)
                {
                    ImgsNum = ConsideredDetsList.Count();
                }

                DetCounter = 0;
                foreach (MyLibrary.Detection Det in ConsideredDetsList)
                {
                    Mat Current_Image = new Mat(Det.FilePath, ImreadModes.Color);

                    // Crop detection from the original image
                    Mat ROI_Detection = new Mat(Current_Image, Det.bbox);
                    //string DetsCroppedImages_ResultPath = BestDetections_Path + "\\" + Path.GetFileNameWithoutExtension(Det.FileName) + "_" + DetCounter + ".jpg";
                    string DetsCroppedImages_ResultPath = BestDetections_Path + "\\" + DetCounter + ".jpg";
                    ROI_Detection.ImWrite(DetsCroppedImages_ResultPath);

                    ImgsNum--;
                    if (ImgsNum == 0)
                        break;

                    DetCounter++;
                }

                // Save the Worst x Detections
                ImgsNum = 3;
                
                if (ConsideredDetsList.Count() < 3)
                {
                    ImgsNum = ConsideredDetsList.Count();
                }
                ConsideredDetsList.Reverse();
                var Worst = ConsideredDetsList.Take(ImgsNum);
                Worst = Worst.Reverse();

                DetCounter = 0;
                foreach (MyLibrary.Detection Det in Worst)
                {
                    Mat Current_Image = new Mat(Det.FilePath, ImreadModes.Color);

                    // Crop detection from the original image
                    Mat ROI_Detection = new Mat(Current_Image, Det.bbox);
                    //string DetsCroppedImages_ResultPath = WorstDetections_Path + "\\" + Path.GetFileNameWithoutExtension(Det.FileName) + "_" + DetCounter + ".jpg";
                    string DetsCroppedImages_ResultPath = WorstDetections_Path + "\\" + DetCounter + ".jpg";
                    ROI_Detection.ImWrite(DetsCroppedImages_ResultPath);

                    DetCounter++;
                }

                PatternsCounter++;
            }
        }

        public ActionResult OnPostDownloadAll()
        {
            string Detections = "Detections";
            string webRootPath = _hostingEnvironment.WebRootPath;
            string Detections_Path = Path.Combine(webRootPath, Detections);
            string TempPath = webRootPath + "\\Temp";

            Directory.EnumerateFiles(TempPath).ToList().ForEach(f => System.IO.File.Delete(f));
            string archive = TempPath + "\\AllDetections.zip";

            // create a new archive
            ZipFile.CreateFromDirectory(Detections_Path, archive);

            return File("/Temp/AllDetections.zip", "application/zip", "AllDetections.zip");
        }

        public ActionResult OnPostDownloadMultiDet()
        {
            string Detections = "Detections";
            string MultiDets = "MultiDets";
            string DataImages = "DataImages";
            string webRootPath = _hostingEnvironment.WebRootPath;
            string Detections_Path = Path.Combine(webRootPath, Detections);
            string Images_Path = Path.Combine(webRootPath, DataImages);
            string MultiDets_Path = Path.Combine(Detections_Path, MultiDets);
            if (!Directory.Exists(MultiDets_Path))
            {
                Directory.CreateDirectory(MultiDets_Path);
            }
            Directory.EnumerateDirectories(MultiDets_Path).ToList().ForEach(f => Directory.Delete(f, true));

            // Here you draw and save the multi-pattern detections per image.
            MyLibrary.VPD Detector = new MyLibrary.VPD(_hostingEnvironment, _GlobalVariables);
            foreach (string img in _GlobalVariables.Images.FileNames)
            {
                string CurrentImage_Path = Path.Combine(Images_Path, img);
                Mat Current_Image = new Mat(CurrentImage_Path, ImreadModes.Color);

                foreach (List<MyLibrary.Detection> DetPerPatList in _GlobalVariables.SelectedDetectionsPerPattern)
                {
                    foreach (MyLibrary.Detection Det in DetPerPatList)
                    {
                        if (Det.FileName == img)
                        {
                            List<MyLibrary.Detection> TempDet = new List<MyLibrary.Detection>();
                            TempDet.Add(Det);
                            Detector.DrawDetections(Current_Image, TempDet, 1);
                        }
                    }
                }
                string DetectionsPerImage_ResultPath = Path.Combine(MultiDets_Path, img);
                Current_Image.ImWrite(DetectionsPerImage_ResultPath + ".jpg");
            }


            string TempPath = webRootPath + "\\Temp";
            Directory.EnumerateFiles(TempPath).ToList().ForEach(f => System.IO.File.Delete(f));
            string archive = TempPath + "\\MultiDets.zip";

            // create a new archive
            ZipFile.CreateFromDirectory(MultiDets_Path, archive);

            return File("/Temp/MultiDets.zip", "application/zip", "MultiDets.zip");
        }

        public ActionResult OnPostDownloadFull(string PN)
        {
            string Detections = "Detections";
            string webRootPath = _hostingEnvironment.WebRootPath;
            string Detections_Path = Path.Combine(webRootPath, Detections);
            string Pattern_Path = Path.Combine(Detections_Path, PN);
            string FullImgs_Path = Path.Combine(Pattern_Path, "FullImages");

            string TempPath = webRootPath + "\\Temp";
            Directory.EnumerateFiles(TempPath).ToList().ForEach(f => System.IO.File.Delete(f));
            string archive = TempPath + "\\FullImgs_" + PN + ".zip";

            // create a new archive
            ZipFile.CreateFromDirectory(FullImgs_Path, archive);

            return File("/Temp/FullImgs_" + PN + ".zip", "application/zip", "FullImgs_" + PN + ".zip");
        }

        public ActionResult OnPostDownloadCropped(string PN)
        {
            string Detections = "Detections";
            string webRootPath = _hostingEnvironment.WebRootPath;
            string Detections_Path = Path.Combine(webRootPath, Detections);
            string Pattern_Path = Path.Combine(Detections_Path, PN);
            string CroppedImgs_Path = Path.Combine(Pattern_Path, "CroppedImages");

            string TempPath = webRootPath + "\\Temp";
            Directory.EnumerateFiles(TempPath).ToList().ForEach(f => System.IO.File.Delete(f));
            string archive = TempPath + "\\CroppedImgs_" + PN + ".zip";

            // create a new archive
            ZipFile.CreateFromDirectory(CroppedImgs_Path, archive);

            return File("/Temp/CroppedImgs_" + PN + ".zip", "application/zip", "CroppedImgs_" + PN + ".zip");
        }

        public ActionResult OnPostDownloadPerImg(string PN)
        {
            string Detections = "Detections";
            string webRootPath = _hostingEnvironment.WebRootPath;
            string Detections_Path = Path.Combine(webRootPath, Detections);
            string Pattern_Path = Path.Combine(Detections_Path, PN);
            string PerImg_Path = Path.Combine(Pattern_Path, "DetectionsPerImage");

            string TempPath = webRootPath + "\\Temp";
            Directory.EnumerateFiles(TempPath).ToList().ForEach(f => System.IO.File.Delete(f));
            string archive = TempPath + "\\DetectionsPerImg_" + PN + ".zip";

            // create a new archive
            ZipFile.CreateFromDirectory(PerImg_Path, archive);

            return File("/Temp/DetectionsPerImg_" + PN + ".zip", "application/zip", "DetectionsPerImg_" + PN + ".zip");
        }

        public ActionResult OnPostDownloadAllPerPattern(string PN)
        {
            string Detections = "Detections";
            string webRootPath = _hostingEnvironment.WebRootPath;
            string Detections_Path = Path.Combine(webRootPath, Detections);
            string Pattern_Path = Path.Combine(Detections_Path, PN);

            string TempPath = webRootPath + "\\Temp";
            Directory.EnumerateFiles(TempPath).ToList().ForEach(f => System.IO.File.Delete(f));
            string archive = TempPath + "\\Detections_" + PN + ".zip";

            // create a new archive
            ZipFile.CreateFromDirectory(Pattern_Path, archive);

            return File("/Temp/Detections_" + PN + ".zip", "application/zip", "Detections_" + PN + ".zip");
        }

        public void OnPostSelectTopN(int TopN, int PL)
        {
            _GlobalVariables.TopN_PerClass[PL] = TopN;
            //TopN_PerClass_Local[PL] = TopN;
        }

        public Task ApplyChanges(string jobId)
        {
            float ProgressStep = (100.0F) / _GlobalVariables.TopN_PerClass.Count(); ;
            float ProgressPerc = 0.0F;
            _hubContext.Clients.Group(jobId).SendAsync("DrawProgress", Math.Round(ProgressPerc, 0));
            //_hubContext.Clients.Group(jobId).SendAsync("Reload", 0);

            MyLibrary.VPD Detector = new MyLibrary.VPD(_hostingEnvironment, _GlobalVariables);

            string FullImages = "FullImages";
            string CroppedImages = "CroppedImages";
            string DetectionsPerImage = "DetectionsPerImage";
            string webRootPath = _hostingEnvironment.WebRootPath;
            string Detections = "Detections";
            string BestDetections = "BestDetections";
            string WorstDetections = "WorstDetections";
            string Detections_Path = Path.Combine(webRootPath, Detections);
            if (!Directory.Exists(Detections_Path))
            {
                Directory.CreateDirectory(Detections_Path);
            }

            int PL = 0;
            foreach (int Cur_PL in _GlobalVariables.TopN_PerClass)
            {
                if (Cur_PL != TopN_PerClass_Local[PL])
                {
                    TopN_PerClass_Local[PL] = Cur_PL;

                    List<MyLibrary.Detection> ConsideredDetsList = _GlobalVariables.DetectionsPerPattern[PL].ToList();
                    int CurrentThreshold = Cur_PL;
                    if (CurrentThreshold < 1)
                        CurrentThreshold = 1;
                    ConsideredDetsList = ConsideredDetsList.Take(CurrentThreshold).ToList();
                    _GlobalVariables.SelectedDetectionsPerPattern[PL] = ConsideredDetsList.ToList();

                    string PatternName = _GlobalVariables.Patterns[PL].PatternName;
                    string DetsPerPattern_Path = Path.Combine(Detections_Path, PatternName);
                    if (!Directory.Exists(DetsPerPattern_Path))
                    {
                        Directory.CreateDirectory(DetsPerPattern_Path);
                    }
                    Directory.EnumerateDirectories(DetsPerPattern_Path).ToList().ForEach(f => Directory.Delete(f, true));

                    string FullImages_Path = Path.Combine(DetsPerPattern_Path, FullImages);
                    if (!Directory.Exists(FullImages_Path))
                    {
                        Directory.CreateDirectory(FullImages_Path);
                    }

                    string CroppedImages_Path = Path.Combine(DetsPerPattern_Path, CroppedImages);
                    if (!Directory.Exists(CroppedImages_Path))
                    {
                        Directory.CreateDirectory(CroppedImages_Path);
                    }

                    string DetectionsPerImage_Path = Path.Combine(DetsPerPattern_Path, DetectionsPerImage);
                    if (!Directory.Exists(DetectionsPerImage_Path))
                    {
                        Directory.CreateDirectory(DetectionsPerImage_Path);
                    }

                    string BestDetections_Path = Path.Combine(DetsPerPattern_Path, BestDetections);
                    if (!Directory.Exists(BestDetections_Path))
                    {
                        Directory.CreateDirectory(BestDetections_Path);
                    }

                    string WorstDetections_Path = Path.Combine(DetsPerPattern_Path, WorstDetections);
                    if (!Directory.Exists(WorstDetections_Path))
                    {
                        Directory.CreateDirectory(WorstDetections_Path);
                    }

                    // Draw Detections Per Image for the current Pattern
                    List<List<MyLibrary.Detection>> PerImgDets = new List<List<MyLibrary.Detection>>(_GlobalVariables.Images.FileNames.Count());
                    foreach (List<MyLibrary.Detection> OnlyToIterate in _GlobalVariables.DetectionsPerImage)
                    {
                        List<MyLibrary.Detection> TempList = new List<MyLibrary.Detection>();
                        PerImgDets.Add(TempList);
                    }
                    int NamesCounter = 0;
                    foreach (string name in _GlobalVariables.Images.FileNames)
                    {
                        foreach (MyLibrary.Detection Det in ConsideredDetsList)
                        {
                            if (Det.FileName == name)
                            {
                                PerImgDets[NamesCounter].Add(Det);
                            }
                        }
                        NamesCounter++;
                    }
                    foreach (List<MyLibrary.Detection> DetsPerImg in PerImgDets)
                    {
                        List<MyLibrary.Detection> TempList = new List<MyLibrary.Detection>();
                        string CurrentImagePath = "Empty";
                        string CurrentImageName = "Empty";
                        foreach (MyLibrary.Detection Det in DetsPerImg)
                        {

                            if (Det.labelnum == PL)
                            {
                                TempList.Add(Det);
                            }

                            if (CurrentImagePath == "Empty")
                            {
                                CurrentImagePath = Det.FilePath;
                            }

                            if (CurrentImageName == "Empty")
                            {
                                CurrentImageName = Det.FileName;
                            }
                        }
                        TempList = TempList.Take(CurrentThreshold).ToList();

                        if (TempList.Count() > 0)
                        {
                            Mat Current_Image = new Mat(CurrentImagePath, ImreadModes.Color);
                            Detector.DrawDetections(Current_Image, TempList, 1);
                            string DetectionsPerImage_ResultPath = DetectionsPerImage_Path + "\\" + Path.GetFileNameWithoutExtension(CurrentImageName) + ".jpg";
                            Current_Image.ImWrite(DetectionsPerImage_ResultPath);
                        }
                    }

                    // Draw and save each detection separately to the corresponding directory
                    int DetCounter = 0;
                    //float PartialStep = 1.0F / (float)ConsideredDetsList.Count();
                    float OldRange = (float)ConsideredDetsList.Count();
                    float NewRange = ProgressStep;
                    float PartialStep = 1 * NewRange / OldRange;
                    
                    foreach (MyLibrary.Detection Det in ConsideredDetsList)
                    {
                        Mat Current_Image = new Mat(Det.FilePath, ImreadModes.Color);
                        List<MyLibrary.Detection> TempDet = new List<MyLibrary.Detection>();
                        TempDet.Add(Det);

                        // Draw detection on the original image
                        Detector.DrawDetections(Current_Image, TempDet, 1);
                        string DetsFullImages_ResultPath = FullImages_Path + "\\" + Path.GetFileNameWithoutExtension(Det.FileName) + "_" + DetCounter + ".jpg";
                        Current_Image.ImWrite(DetsFullImages_ResultPath);

                        // Crop detection from the original image
                        Mat ROI_Detection = new Mat(Current_Image, Det.bbox);
                        string DetsCroppedImages_ResultPath = CroppedImages_Path + "\\" + Path.GetFileNameWithoutExtension(Det.FileName) + "_" + DetCounter + ".jpg";
                        ROI_Detection.ImWrite(DetsCroppedImages_ResultPath);

                        DetCounter++;

                        //ProgressStep = PartialStep;
                        //ProgressPerc += ProgressStep;
                        ProgressPerc += PartialStep;
                        _hubContext.Clients.Group(jobId).SendAsync("DrawProgress", Math.Round(ProgressPerc, 0));
                    }

                    // Save the Best x detections
                    int ImgsNum = 3;
                    if (ConsideredDetsList.Count() < 3)
                    {
                        ImgsNum = ConsideredDetsList.Count();
                    }

                    DetCounter = 0;
                    foreach (MyLibrary.Detection Det in ConsideredDetsList)
                    {
                        Mat Current_Image = new Mat(Det.FilePath, ImreadModes.Color);

                        // Crop detection from the original image
                        Mat ROI_Detection = new Mat(Current_Image, Det.bbox);
                        //string DetsCroppedImages_ResultPath = BestDetections_Path + "\\" + Path.GetFileNameWithoutExtension(Det.FileName) + "_" + DetCounter + ".jpg";
                        string DetsCroppedImages_ResultPath = BestDetections_Path + "\\" + DetCounter + ".jpg";
                        ROI_Detection.ImWrite(DetsCroppedImages_ResultPath);

                        ImgsNum--;
                        if (ImgsNum == 0)
                            break;

                        DetCounter++;
                    }

                    // Save the Worst x Detections
                    ImgsNum = 3;

                    if (ConsideredDetsList.Count() < 3)
                    {
                        ImgsNum = ConsideredDetsList.Count();
                    }
                    ConsideredDetsList.Reverse();
                    var Worst = ConsideredDetsList.Take(ImgsNum);
                    Worst = Worst.Reverse();

                    DetCounter = 0;
                    foreach (MyLibrary.Detection Det in Worst)
                    {
                        Mat Current_Image = new Mat(Det.FilePath, ImreadModes.Color);

                        // Crop detection from the original image
                        Mat ROI_Detection = new Mat(Current_Image, Det.bbox);
                        //string DetsCroppedImages_ResultPath = WorstDetections_Path + "\\" + Path.GetFileNameWithoutExtension(Det.FileName) + "_" + DetCounter + ".jpg";
                        string DetsCroppedImages_ResultPath = WorstDetections_Path + "\\" + DetCounter + ".jpg";
                        ROI_Detection.ImWrite(DetsCroppedImages_ResultPath);

                        DetCounter++;
                    }
                }
                else
                {
                    ProgressPerc += ProgressStep;
                    _hubContext.Clients.Group(jobId).SendAsync("DrawProgress", Math.Round(ProgressPerc, 0));
                }
                

                PL++;
            }

            if (_GlobalVariables.Random == 1)
            {
                _GlobalVariables.Random = 0;
            }
            else
                _GlobalVariables.Random = 1;

            //_hubContext.Clients.Group(jobId).SendAsync("Reload", 1);

            return Task.CompletedTask;
        }

        public ActionResult OnPostDrawDetections()
        {
            string jobId = Guid.NewGuid().ToString("N");

            //_queue.QueueAsyncTask(() => ApplyChanges(jobId));
            _queue.QueueTask(() => ApplyChanges(jobId));

            ViewData["JobId"] = jobId;

            //_hubContext.Clients.Group(jobId).SendAsync("Reload", 1);
            //return RedirectToPage();
            return Page();
        }

        public ActionResult OnPostDeleteAll()
        {

            return RedirectToPage("/Index");
        }

        //public ActionResult OnPostReload(string jobId)
        //{
        //    //_hubContext.Clients.Group(jobId).SendAsync("Reload", 0);
        //    return RedirectToPage("/Detection/Results");
        //}
    }
}

