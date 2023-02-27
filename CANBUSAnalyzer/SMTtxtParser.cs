using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS {
  internal class SMTtxtParser : ICANLogParser {
    private StreamReader streamReader;


    public SMTtxtParser(string fileName) {
      streamReader = new StreamReader(File.OpenRead(fileName));
    }

    public string ReadNext() {
      return streamReader.ReadLine();
    }
  }
}