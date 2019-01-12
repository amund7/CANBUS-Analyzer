using CANLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS
{
    class ScanMyTeslaLog : DataWithColumns
    {
        public ScanMyTeslaLog() : base(SeparatorType.Space, false, false)
        {
        }
    }
}
