using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS
{
    class VectorASCLogReader : CANLogReader
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

        public VectorASCLogReader(uint dataPointLimit = 0)
            : base(new VectorASCLog(), dataPointLimit)
        {
        }

        protected override string FormatRowValues(List<string> rowValues)
        {
            int dataLength;
            if (rowValues.Count >= MinColumns && int.TryParse(rowValues[ColumnIndex.DataLength], out dataLength))
            {
                string formattedLine = rowValues[ColumnIndex.ID];

                // Append message data
                for (int i = ColumnIndex.FirstByte; i < ColumnIndex.FirstByte + dataLength && i < rowValues.Count; i++)
                {
                    // Sanity check
                    Debug.Assert(rowValues[i].Length == ByteLength);

                    // Add to formatted data
                    formattedLine += rowValues[i];
                }

                return formattedLine;
            }
            else if (rowValues[2] == "Statistic:" || rowValues[5] == "error")  // Error / stats frame -- ignore
                return null;
            else // Unknown frame
                throw new DataMisalignedException("Unexpected number of values: " + rowValues.Count);
        }
    }
}
