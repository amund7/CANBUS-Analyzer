using CANLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS
{
    abstract class CANLogReader
    {
        public static class LogFileExtension
        {
            public const string CSV = ".csv";
            public const string ASC = ".asc";
            public const string TXT = ".txt";
        }

        private bool hasReadHeader;

        public DataWithColumns Log { get; private set; }
        public uint DataPointLimit { get; private set; }

        protected abstract string FormatRowValues(List<string> rowValues);

        protected CANLogReader(DataWithColumns log, uint dataPointLimit = 0)
        {
            if (log == null) throw new ArgumentNullException("Log");

            Log = log;
            DataPointLimit = dataPointLimit;
        }

        public static CANLogReader FromFile(string filename, uint dataPointLimit = 0)
        {
            if (string.IsNullOrEmpty(filename)) throw new ArgumentException("Filename cannot be empty.");

            string fileExt = Path.GetExtension(filename).ToLower();
            switch (fileExt)
            {
                case LogFileExtension.CSV:
                    return new CANOpenMagicLogReader(dataPointLimit);

                case LogFileExtension.ASC:
                    return new VectorASCLogReader(dataPointLimit);

                case LogFileExtension.TXT:
                    return new ScanMyTeslaLogReader(dataPointLimit);

                default: // Default to ScanMyTeslaLogReader format for now
                    return new ScanMyTeslaLogReader(dataPointLimit);
            }

        }

        public string ReadNext(StreamReader streamReader)
        {
            // Read header, if needed
            if (!hasReadHeader)
            {
                Log.ReadHeaders(streamReader);
                hasReadHeader = true;
            }

            List<string> rowValues = Log.ReadNext(streamReader);

            // Reformat to ScanMyTesla internal format
            if (rowValues != null)
                return FormatRowValues(rowValues);
            else
                return null;
        }
    }
}
