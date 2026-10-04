using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AGL.DebugHelpers.Persistence
{
    internal class CurveStream {
        string data;
        CurveStreamElement[] curveStreamElements;
        int offset;

        internal CurveStream(string curveData) {
            data=curveData;
            Init();
        }

        void Init() {
            data=data.Trim();
            string[] blocks = data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            curveStreamElements = RefineBlocks(blocks).ToArray();

        }

        static IEnumerable<CurveStreamElement> RefineBlocks(string[] blocks) {
            foreach (string block in blocks) {
                char ch = block[0];
                if (char.IsLetter(ch)) {
                    yield return new CharStreamElement(ch);
                    if (block.Length > 1) {
                        if (double.TryParse(block.Substring(1), out double res))
                            yield return new DoubleStreamElement(res);
                        else yield return null;
                    }
                } else {
                    if (double.TryParse(block, out double res))
                        yield return new DoubleStreamElement(res);
                    else yield return null;
                }
            }
        }

        internal CurveStreamElement GetNextCurveStreamElement() {
            if (offset >= curveStreamElements.Length)
                return null;
            return curveStreamElements[offset++];
        }

        internal CurveStreamElement PickNextCurveStreamElement() {
            if (offset >= curveStreamElements.Length)
                return null;
            return curveStreamElements[offset];
        }
    }
}