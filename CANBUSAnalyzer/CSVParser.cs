using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS
{
    // LATER: If needed, refactor to support delimited formats other than CANopen Magic
    class CSVParser
    {
        private const int MinColumns = 16;
        private static class ColumnIndex
        {
            public const int ID = 5;
        }

        public string Parse(string rawLine)
        {
            string formattedLine = null;
            if (!string.IsNullOrEmpty(rawLine))
            {
                string[] split = rawLine.Split(',');
                
                // Ensure we have the expected number of columns
                if (split.Length >= MinColumns)
                {
                    // Raw data is assumed to be in the final array element
                    formattedLine = split[ColumnIndex.ID] + " " + split[split.Length - 1];
                    formattedLine = formattedLine.Replace("\"", string.Empty).Replace(" ", string.Empty).Replace("0x", string.Empty);
                }
            }

            return formattedLine;
        }
    }
}
