using CANLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS
{
    class VectorASCLog : DataWithColumns
    {
        public DateTime? Timestamp { get; private set; }
        public string Options { get; private set; }
        public string Comment { get; private set; }
        public string Version { get; private set; }

        public VectorASCLog() : base(SeparatorType.MultiSpace, false, true)
        {
        }

        protected override void ReadCustomHeader(StreamReader streamReader)
        {
            // Assumes first four rows have header information
            for (int i = 0; i < 4 && !streamReader.EndOfStream; i++)
            {
                string s = streamReader.ReadLine();
                switch(i)
                {
                    case 0: // Capture timestamp
                        DateTime dt;
                        if (s.Length > 32 && s.StartsWith("date ") && DateTime.TryParse(s.Substring(9, 7) + s.Substring(32) + s.Substring(15, 16), out dt))
                            Timestamp = dt;
                        break;

                    case 1: // Options
                        Options = s;
                        break;

                    case 2: // Log comment
                        Comment = s;
                        break;

                    case 3:
                        if (s.StartsWith("// version "))
                            Version = s.Substring(11);
                        break;
                }
            }
           
        }

        protected override void WriteCustomHeader(StreamWriter streamWriter)
        {
            // TODO: ?
            base.WriteCustomHeader(streamWriter);
        }
    }
}
