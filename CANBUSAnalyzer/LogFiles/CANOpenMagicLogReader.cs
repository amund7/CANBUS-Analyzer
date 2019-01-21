using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS
{
    class CANOpenMagicLogReader : CANLogReader
    {
        private const int MinColumns = 16;
        private static class ColumnIndex
        {
            public const int ID = 5;
        }

        public CANOpenMagicLogReader(uint dataPointLimit = 0)
            : base(new CANLib.CANopen_Magic_Log(), dataPointLimit)
        {
        }

        protected override string FormatRowValues(List<string> rowValues)
        {
            if (rowValues.Count >= MinColumns)
            {
                string formattedLine = ZeroPad(rowValues[ColumnIndex.ID]) + rowValues.Last();

                return formattedLine.Replace("\"", string.Empty).Replace(" ", string.Empty).Replace("0x", string.Empty);
            }
            else // Malformatted or error frame -- ignore
                return null;
        }
    }
}
