#if TEST_MSAGL && ! SILVERLIGHT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Microsoft.AGL.DebugHelpers{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class DebugCurveCollection{
        /// <summary>
        /// 
        /// </summary>
        /// <param name="debugCurves"></param>
        DebugCurveCollection(IEnumerable<DebugCurve> debugCurves) => DebugCurvesArray = debugCurves.ToArray();

        /// <summary>
        /// 
        /// </summary>
        public DebugCurve[] DebugCurvesArray;

        ///<summary>
        ///</summary>
        ///<param name="debugCurves"></param>
        ///<param name="fileName"></param>
        public static void WriteToFile(IEnumerable<DebugCurve> debugCurves, string fileName) {
            Stream stream = File.Open(fileName, FileMode.Create);
            BinaryFormatter bformatter = new BinaryFormatter();
            try {
                bformatter.Serialize(stream, new DebugCurveCollection(debugCurves));
            }
            catch (SerializationException e) {
                Console.WriteLine(e.ToString());
            }
            stream.Close();
        }

        ///<summary>
        ///</summary>
        ///<param name="fileName"></param>
        public static IEnumerable<DebugCurve> ReadFromFile(string fileName) {
            Stream stream = File.Open(fileName, FileMode.Open);
            BinaryFormatter bformatter = new BinaryFormatter();
            DebugCurveCollection dc = null;
            try {
                dc = bformatter.Deserialize(stream) as DebugCurveCollection;
            }
            catch (SerializationException e) {
                Console.WriteLine(e.ToString());
            }
            stream.Close();
            return new List<DebugCurve>(dc.DebugCurvesArray);
        }
    }
}
#endif