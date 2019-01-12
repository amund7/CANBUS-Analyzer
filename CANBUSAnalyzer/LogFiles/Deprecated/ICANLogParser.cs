using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS
{
    [Obsolete("Use CANTools Logs instead")]
    interface ICANLogParser
    {
        string ParseLine(string rawLine);
    }
}
