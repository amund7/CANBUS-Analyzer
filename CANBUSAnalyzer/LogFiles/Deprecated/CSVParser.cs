using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS
{
    /// <summary>
    /// Helps parse CANopen Magic CSV files. A stop-gap measure until the more robust log parsers in CANTools are integrated.
    /// </summary>
    [Obsolete("Use CANTools Logs instead")]
    class CSVParser : ICANLogParser
    {
        private const int MinColumns = 16;
        private static class ColumnIndex
        {
            public const int ID = 5;
        }

        public string ParseLine(string rawLine)
        {
            string formattedLine = null;
            if (!string.IsNullOrEmpty(rawLine))
            {
                string[] split = rawLine.Split(',');

                // Ensure we have the expected number of columns
                if (split.Length >= MinColumns)
                {
                    // Raw data is assumed to be in the final array element
                    formattedLine = split[ColumnIndex.ID] + split[split.Length - 1];
                    formattedLine = formattedLine.Replace("\"", string.Empty).Replace(" ", string.Empty).Replace("0x", string.Empty);

                    //// Sanity check
                    //if ((formattedLine.Length <= 3 || formattedLine.Length > 26) && split[6] != @"""E""")
                    //    Console.WriteLine("Unexpected data length:" + formattedLine.Length);
                }
            }

            return formattedLine;
        }
    }
}
