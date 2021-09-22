using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace VPD1p3.MyLibrary
{
    public class ImgInfo
    {
        public string Descriptors { get; set; }

        [Display(Name = "Pattern Name")]
        public string PatternName { get; set; }

        //[Display(Name = "Image Name")]
        //public string ImageName { get; set; }

        [Display(Name = "Number of Files")]
        public int NumberOfFiles { get; set; }

        public int Label { get; set; }
        public List<string> FileNames = new List<string>();
        public string FilePath { get; set; }
        public List<KeyPoint> Keypoints = new List<KeyPoint>();
    }
}
