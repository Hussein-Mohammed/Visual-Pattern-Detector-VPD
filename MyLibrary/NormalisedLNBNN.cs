using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

using System.Runtime.InteropServices;
using OpenCvSharp.Flann;

namespace HAT3_WebApp.MyLibrary
{

    public class LocalNBNN_Results
    {
        public float Votes = 0;
        public int Label = 0;
        public string Directory;
    }

    class NormalisedLNBNN
    {
        public List<LocalNBNN_Results> NNSearch_LNBNN_Priori(Mat QueryDescs, List<int> labels, OpenCvSharp.Flann.Index finder, int numClasses, List<int> Ktps_Num_Known)
        {
            LocalNBNN_Results[] Results_Array = new LocalNBNN_Results[numClasses];

            float AllVotes = 0;

            /// Rotate over all the descriptors of the test file and do the classification
            for (int d = QueryDescs.Rows - 1; d >= 0; d--)
            {

                int neighbours = numClasses;
                var curdesc = QueryDescs.Row(d);

                int[] knnin;
                float[] knndis;

                /// This function takes the query descriptor and returns the nearest 11 neighbours along with their distances
                finder.KnnSearch(curdesc, out knnin, out knndis, neighbours, new SearchParams(500));

                //double distb = Temp_Knndis[Temp_Knndis.Count()-1];
                float distb = knndis[neighbours - 1];

                List<bool> classused = new List<bool>();
                classused.AddRange(Enumerable.Repeat(false, numClasses));

                ///Rotate over the neighbours and do the voting
                for (int n = 0; n < neighbours - 1; n++)
                {

                    int curlabel = (labels[knnin[n]]);

                    /// skip if this class is voted for
                    if (classused[curlabel])
                        continue;

                    float val = knndis[n];

                    classused[curlabel] = true;

                    if (Results_Array[curlabel] == null)
                        Results_Array[curlabel] = new LocalNBNN_Results();

                    Results_Array[curlabel].Label = curlabel;
                    Results_Array[curlabel].Votes += distb - val;
                }
            }

            for (int votes = 0; votes < Results_Array.Count(); votes++)
            {
                if (Results_Array[votes] == null)
                    Results_Array[votes] = new LocalNBNN_Results();

                Results_Array[votes].Votes = Results_Array[votes].Votes / Ktps_Num_Known[votes];
                AllVotes += Results_Array[votes].Votes;

                Results_Array[votes].Label = votes;

            }

            for (int votes = 0; votes < Results_Array.Count(); votes++)
            {
                Results_Array[votes].Votes = 100 * (Results_Array[votes].Votes / AllVotes);
            }

            List<LocalNBNN_Results> Result = Results_Array.OfType<LocalNBNN_Results>().ToList();

            return Result;

        }
    }
}
