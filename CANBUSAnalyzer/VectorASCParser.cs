using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CANBUS
{
    class VectorASCParser : ICANLogParser
    {
        private const int MinColumns = 7;
        private const int IDLength = 3;
        private const int ByteLength = 2;

        private static class ColumnIndex
        {
            public const int ID = 2;
            public const int DataLength = 5;
            public const int FirstByte = 6;
        }

        private Regex regexLine;
        private bool isHeaderDone;

        public VectorASCParser()
        {
            regexLine = new Regex(@"^\s*((?<Data>[^ ]+)\s+)+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        public string ParseLine(string rawLine)
        {
            string formattedLine = null;
            if (!string.IsNullOrEmpty(rawLine))
            {
                // Look for "// version" line to mark the end of the file header
                if (isHeaderDone || (isHeaderDone = IsVersionLine(rawLine)))
                {
                    Match m = regexLine.Match(rawLine);

                    // Ensure we have the expected number of columns
                    if (m.Success && m.Groups["Data"].Captures.Count >= MinColumns)
                    {
                        CaptureCollection capData = m.Groups["Data"].Captures;

                        // Ensure that this is a valid (non-error) frame
                        string id = ZeroPadID(capData[ColumnIndex.ID].Value);
                        int dataLength;
                        if (id.Length == IDLength && int.TryParse(capData[ColumnIndex.DataLength].Value, out dataLength))
                        {
                            // Start with the message ID
                            formattedLine = id;

                            // Append message data
                            for (int i = ColumnIndex.FirstByte; i < ColumnIndex.FirstByte + dataLength && i < capData.Count; i++)
                            {
                                // Sanity check
                                Debug.Assert(capData[i].Value.Length == ByteLength);

                                // Add to formatted data
                                formattedLine += capData[i].Value;
                            }
                        }
                        else
                            Debug.WriteLine("Unpadded value: " + capData[2].Value);

                    }
                    else
                        Debug.WriteLine("Skipping Line: " + rawLine);
                }
            }

            return formattedLine;
        }

        private string ZeroPadID(string s)
        {
            if (s != null && s.Length < IDLength)
                s = s.PadLeft(3, '0');

            return s;
        }

        private bool IsVersionLine(string rawLine)
        {
            return rawLine != null && rawLine.StartsWith("// version");
        }
    }
}
