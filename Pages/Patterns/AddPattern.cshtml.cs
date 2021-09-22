using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenCvSharp;
using VPD1p3.MyLibrary;

namespace VPD1p3.Pages.Patterns
{
    [IgnoreAntiforgeryToken]
    public class AddPatternModel : PageModel
    {
        // temp variables
        public ImgInfo Imgs_Local = new ImgInfo();
        public IEnumerable<string> ValidFiles { get; set; }

        // flags to control the logic
        public bool DuplicatedPN { get; set; } = false;
        public bool ExistingPN { get; set; } = false;
        public bool InvalidForm { get; set; } = false;

        public static string PatternName { get; set; }

        // needed to get the wwwroot directory
        private IWebHostEnvironment _hostingEnvironment;

        private readonly TempVariables_Patterns_Singleton _TempVariables_Patterns_Singleton;
        private readonly GlobalVariables _GlobalVariables;

        [Obsolete]
        public AddPatternModel(IWebHostEnvironment hostingEnvironment, TempVariables_Patterns_Singleton TempVariables_Patterns_Singleton, GlobalVariables GlobalVariables)
        {
            _hostingEnvironment = hostingEnvironment;
            _TempVariables_Patterns_Singleton = TempVariables_Patterns_Singleton;
            _GlobalVariables = GlobalVariables;
        }

        public void OnGet()
        {
            _TempVariables_Patterns_Singleton.Used = false;
            _TempVariables_Patterns_Singleton.TempImgInfo.Descriptors = "";
            _TempVariables_Patterns_Singleton.TempImgInfo.FileNames.Clear();
            _TempVariables_Patterns_Singleton.TempImgInfo.PatternName = "";
            _TempVariables_Patterns_Singleton.TempImgInfo.Keypoints.Clear();
            _TempVariables_Patterns_Singleton.TempImgInfo.Label = 0;
            _TempVariables_Patterns_Singleton.TempImgInfo.NumberOfFiles = 0;
            _TempVariables_Patterns_Singleton.TempImgInfo.FilePath = "";

            // Delete all files in Temp directory before starting to save new files
            string folderName = "Temp";
            string webRootPath = _hostingEnvironment.WebRootPath;
            string newPath = Path.Combine(webRootPath, folderName);
            if (!Directory.Exists(newPath))
            {
                Directory.CreateDirectory(newPath);
            }

            Directory.EnumerateFiles(newPath).ToList().ForEach(f => System.IO.File.Delete(f));
            foreach (var dir in Directory.EnumerateDirectories(newPath).ToList())
            {
                Directory.EnumerateFiles(dir).ToList().ForEach(f => System.IO.File.Delete(f));
            }
            Directory.EnumerateDirectories(newPath).ToList().ForEach(f => Directory.Delete(f));

            ViewData["ValidFiles"] = 0;

            ViewData["confirmation"] = "No name has been assigned yet !";
        }

        public ActionResult OnPostDeletePN(string PN)
        {
            for (int SamePN = 0; SamePN < _GlobalVariables.Patterns.Count(); SamePN++)
            {
                if (PN == _GlobalVariables.Patterns[SamePN].PatternName)
                {
                    _GlobalVariables.Patterns.Remove(_GlobalVariables.Patterns[SamePN]);
                    break;
                }
            }

            this.OnGet();

            return Page();
        }

        public void OnPostPatternName(string PName_Post = "Nothing")
        {
            PatternName = PName_Post;
            foreach (var Pattern in _GlobalVariables.Patterns)
            {
                if (PatternName == Pattern.PatternName)
                {
                    DuplicatedPN = true;
                    ViewData["confirmation"] = $"The uploaded files wil be added to: {PName_Post}";
                    break;
                }
            }

            if (!DuplicatedPN)
                ViewData["confirmation"] = $"{PName_Post} !";
        }

        [Obsolete] 
        public ActionResult OnPostUpload(List<IFormFile> files)
        {

            if (files != null && files.Count > 0)
            {
                // Save the files and the ImgInfo information in temp folders and temp variables
                string folderName = "Temp";
                string webRootPath = _hostingEnvironment.WebRootPath;
                string newPath = Path.Combine(webRootPath, folderName);
                if (!Directory.Exists(newPath))
                {
                    Directory.CreateDirectory(newPath);
                }

                //// Delete all files in Temp directory before starting to save new files
                //Directory.EnumerateFiles(newPath).ToList().ForEach(f => System.IO.File.Delete(f));
                //foreach (var dir in Directory.EnumerateDirectories(newPath).ToList())
                //{
                //    Directory.EnumerateFiles(dir).ToList().ForEach(f => System.IO.File.Delete(f));
                //}
                //Directory.EnumerateDirectories(newPath).ToList().ForEach(f => Directory.Delete(f));

                // Save selected files to the Temp directory
                foreach (IFormFile item in files)
                {
                    if (item.Length > 0)
                    {
                        string fileName = ContentDispositionHeaderValue.Parse(item.ContentDisposition).FileName.Trim('"');
                        string fullPath = Path.Combine(newPath, fileName);
                        using (var stream = new FileStream(fullPath, FileMode.Create))
                        {
                            item.CopyTo(stream);
                        }

                        //TempVariables.TempImgInfo.FileNames.Add(fileName);
                        //CurrentPerUserImgInfo.TempImgInfo.FileNames.Add(fileName);
                        //_TempVariables_Patterns_Singleton.TempImgInfo.FileNames.Add(fileName);


                    }
                }

                //TempVariables.TempImgInfo.FilePath = newPath;
                _TempVariables_Patterns_Singleton.TempImgInfo.FilePath = newPath;
                //CurrentPerUserImgInfo.TempImgInfo.FilePath = newPath; 

                ValidFiles = Directory.EnumerateFiles(newPath, "*.*", SearchOption.AllDirectories).Where(s => s.ToLower().EndsWith(".jpeg") ||
                s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".tif") || s.ToLower().EndsWith(".tiff") || s.ToLower().EndsWith(".png")
                || s.ToLower().EndsWith(".bmp"));

                // Convert ".tif" files to ".jpg"
                foreach (var img in ValidFiles)
                {
                    if (Path.GetExtension(img).ToLower() == ".tif" || Path.GetExtension(img).ToLower() == ".tiff")
                    {
                        Mat Current_Image = new Mat(img, ImreadModes.Color);
                        string Path_of_Converted_Image = newPath + "\\" + Path.GetFileNameWithoutExtension(img) + ".jpg";
                        Current_Image.ImWrite(Path_of_Converted_Image);

                        _TempVariables_Patterns_Singleton.TempImgInfo.FileNames.Add(Path.GetFileNameWithoutExtension(img) + ".jpg");

                        System.IO.File.Delete(img);
                    }
                    else
                    {
                        _TempVariables_Patterns_Singleton.TempImgInfo.FileNames.Add(Path.GetFileName(img));
                    }
                }

                ViewData["ValidFiles"] = ValidFiles.Count();
                //TempVariables.TempImgInfo.NumberOfFiles = ValidFiles.Count();
                _TempVariables_Patterns_Singleton.TempImgInfo.NumberOfFiles += ValidFiles.Count();
                //CurrentPerUserImgInfo.TempImgInfo.NumberOfFiles = ValidFiles.Count();

                //TempVariables.Used = true;
                _TempVariables_Patterns_Singleton.Used = true;
                //CurrentPerUserImgInfo.Used = true;

                //return this.Content("Success");
                //ViewData["Uploaded"] = ValidFiles.Count() + "valid files have been uploaded!";
                return Page();
            }
            //return this.Content("Fail");
            return Page();
        }

    
        public ActionResult OnPostAddPatternFiles()
        {
            string TempDir = "Temp";
            string RootDir = _hostingEnvironment.WebRootPath;
            string DirToDelete = Path.Combine(RootDir, TempDir);
            if (PatternName != null && Directory.EnumerateFiles(DirToDelete).Count() > 0)
            {
                // check whether the handwriting style exist
                int SamePN = 0;
                for (; SamePN < _GlobalVariables.Patterns.Count(); SamePN++)
                {
                    if (PatternName == _GlobalVariables.Patterns[SamePN].PatternName)
                    {
                        ExistingPN = true;
                        break;
                    }
                }

                // if the style does not exist, then create a new one
                if (!ExistingPN)
                {
                    string folderName = "Patterns";
                    string SubFolderName = PatternName;
                    string webRootPath = _hostingEnvironment.WebRootPath;
                    string newPath = Path.Combine(webRootPath, folderName);
                    if (!Directory.Exists(newPath))
                    {
                        Directory.CreateDirectory(newPath);
                    }

                    string StylePath = Path.Combine(newPath, SubFolderName);
                    if (!Directory.Exists(StylePath))
                    {
                        Directory.CreateDirectory(StylePath);
                    }

                    foreach (var file in Directory.GetFiles(_TempVariables_Patterns_Singleton.TempImgInfo.FilePath))
                        System.IO.File.Copy(file, Path.Combine(StylePath, Path.GetFileName(file)), true);

                    ImgInfo TempImg = new ImgInfo();
                    TempImg.FilePath = StylePath;
                    TempImg.PatternName = PatternName;
                    TempImg.FileNames.AddRange(_TempVariables_Patterns_Singleton.TempImgInfo.FileNames);
                    TempImg.NumberOfFiles = _TempVariables_Patterns_Singleton.TempImgInfo.NumberOfFiles;

                    _GlobalVariables.Patterns.Add(TempImg);
                }

                // if the style exist, add the new files to the existed one
                else
                {
                    foreach (var file in Directory.GetFiles(_TempVariables_Patterns_Singleton.TempImgInfo.FilePath))
                        System.IO.File.Copy(file, Path.Combine(_GlobalVariables.Patterns[SamePN].FilePath, Path.GetFileName(file)), true);

                    foreach (var FileName_Temp in _TempVariables_Patterns_Singleton.TempImgInfo.FileNames)
                    {
                        bool NameFound = false;
                        foreach (var FileName_Pattern in _GlobalVariables.Patterns[SamePN].FileNames)
                        {
                            if (FileName_Temp == FileName_Pattern)
                            {
                                NameFound = true;
                                break;
                            }
                        }

                        if (!NameFound)
                        {
                            _GlobalVariables.Patterns[SamePN].NumberOfFiles++;
                            _GlobalVariables.Patterns[SamePN].FileNames.Add(FileName_Temp);
                        }

                    }
                }


                // Delete temporary files directories and objects
                //_TempVariables_Singleton.TempImgInfo.FileNames.Clear();

                Directory.EnumerateFiles(DirToDelete).ToList().ForEach(f => System.IO.File.Delete(f));
                foreach (var dir in Directory.EnumerateDirectories(DirToDelete).ToList())
                {
                    Directory.EnumerateFiles(dir).ToList().ForEach(f => System.IO.File.Delete(f));
                }
                Directory.EnumerateDirectories(DirToDelete).ToList().ForEach(f => System.IO.Directory.Delete(f));

                _TempVariables_Patterns_Singleton.Used = false;

                this.OnGet();

                return Page();
            }
            else
            {
                Directory.EnumerateFiles(DirToDelete).ToList().ForEach(f => System.IO.File.Delete(f));
                foreach (var dir in Directory.EnumerateDirectories(DirToDelete).ToList())
                {
                    Directory.EnumerateFiles(dir).ToList().ForEach(f => System.IO.File.Delete(f));
                }
                Directory.EnumerateDirectories(DirToDelete).ToList().ForEach(f => System.IO.Directory.Delete(f));

                InvalidForm = true;
                return Page();
            }
        }

    }
}
