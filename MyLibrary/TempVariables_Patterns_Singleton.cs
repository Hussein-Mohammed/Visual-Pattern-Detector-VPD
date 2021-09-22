using OpenCvSharp;
using OpenCvSharp.Flann;
using VPD1p3.MyLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VPD1p3.MyLibrary
{
    public class TempVariables_Patterns_Singleton
    {
        public ImgInfo TempImgInfo = new ImgInfo();
        public bool Used = false;
    }
}
