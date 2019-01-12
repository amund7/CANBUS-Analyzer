using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS
{
    class ScanMyTeslaLogReader : CANLogReader
    {
        public ScanMyTeslaLogReader(uint dataPointLimit = 0) : base(new ScanMyTeslaLog(), dataPointLimit)
        {
        }

        protected override string FormatRowValues(List<string> rowValues)
        {
            // No transformation needed -- all data will be in the first column
            if (rowValues.Count == 1)
                return rowValues[0];
            else
                throw new DataMisalignedException("Unexpected data format in row: " + string.Join(" ", rowValues));
        }
    }
}
