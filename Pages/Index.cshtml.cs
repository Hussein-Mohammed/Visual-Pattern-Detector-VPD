using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using VPD1p3.MyLibrary;

namespace VPD1p3.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private IWebHostEnvironment _hostingEnvironment;
        private readonly GlobalVariables _GlobalVariables;

        public IndexModel(ILogger<IndexModel> logger, IWebHostEnvironment hostingEnvironment, GlobalVariables GlobalVariables)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            _GlobalVariables = GlobalVariables;
        }

        public void OnGet()
        {
            // Delete all directories and files in the "Patterns" directory
            string Patterns = "Patterns";
            string webRootPath = _hostingEnvironment.WebRootPath;
            string Path_Patterns = Path.Combine(webRootPath, Patterns);
            if (!Directory.Exists(Path_Patterns))
            {
                Directory.CreateDirectory(Path_Patterns);
            }

            foreach (var dir in Directory.EnumerateDirectories(Path_Patterns).ToList())
            {
                Directory.EnumerateFiles(dir).ToList().ForEach(f => System.IO.File.Delete(f));
            }
            Directory.EnumerateDirectories(Path_Patterns).ToList().ForEach(f => System.IO.Directory.Delete(f,true));

            // Delete all directories and files in the "DataImages" directory
            string DataImages = "DataImages";
            string Path_DataImages = Path.Combine(webRootPath, DataImages);
            if (!Directory.Exists(Path_DataImages))
            {
                Directory.CreateDirectory(Path_DataImages);
            }

            foreach (var dir in Directory.EnumerateDirectories(Path_DataImages).ToList())
            {
                Directory.EnumerateFiles(dir).ToList().ForEach(f => System.IO.File.Delete(f));
            }
            Directory.EnumerateDirectories(Path_DataImages).ToList().ForEach(f => Directory.Delete(f,true));
            Directory.EnumerateFiles(Path_DataImages).ToList().ForEach(f => System.IO.File.Delete(f));

            // Delete all files in "Results"
            string ResultPath = webRootPath + "\\Detections";
            if (!Directory.Exists(ResultPath))
            {
                Directory.CreateDirectory(ResultPath);
            }
            Directory.EnumerateFiles(ResultPath).ToList().ForEach(f => System.IO.File.Delete(f));
            Directory.EnumerateDirectories(ResultPath).ToList().ForEach(f => Directory.Delete(f, true));

            // delete the temp files
            string TempPath = webRootPath + "\\Temp";
            if (!Directory.Exists(TempPath))
            {
                Directory.CreateDirectory(TempPath);
            }
            Directory.EnumerateFiles(TempPath).ToList().ForEach(f => System.IO.File.Delete(f));
            foreach (var dir in Directory.EnumerateDirectories(TempPath).ToList())
            {
                Directory.EnumerateFiles(dir).ToList().ForEach(f => System.IO.File.Delete(f));
            }
            Directory.EnumerateDirectories(TempPath).ToList().ForEach(f => Directory.Delete(f,true));

            // Delete GlobalVariables
            _GlobalVariables.Images = null;
            _GlobalVariables.Patterns.Clear();
            _GlobalVariables.PatternsNames.Clear();
            _GlobalVariables.DetectionsPerImage.Clear();
            _GlobalVariables.DetectionsPerPattern.Clear();
            _GlobalVariables.KernelSize_PerClass.Clear();
            _GlobalVariables.KptsNum_Patterns.Clear();
            _GlobalVariables.PatternsLabels.Clear();
            _GlobalVariables.State = null;
            _GlobalVariables.DetectedFeatures = null;
            // Initialise ImgInfo for Data Images
            ImgInfo TempImg = new ImgInfo();
            TempImg.FilePath = "";
            TempImg.PatternName = "DataImages";
            TempImg.FileNames = new List<string>();
            TempImg.NumberOfFiles = 0;

            _GlobalVariables.Images = TempImg;
        }
    }
}
