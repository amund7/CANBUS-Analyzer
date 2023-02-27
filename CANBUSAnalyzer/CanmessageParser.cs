using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CANBUS {
  internal class CanmessageParser : ICANLogParser {
    BinaryReader binaryReader;

    public CanmessageParser(string filename) {
      binaryReader = new BinaryReader(File.OpenRead(filename));
    }

    public string ReadNext() {
      var m = new CanMessage();
      m.id = binaryReader.ReadUInt16();
      m.payload = binaryReader.ReadUInt64();
      var arr = BitConverter.GetBytes(m.payload);
      //if (m.payload.endian)
      Array.Reverse(arr);
      var l = BitConverter.ToUInt64(arr, 0);
      m.timeStamp = binaryReader.ReadInt64();
      return $"{m.id:X3}{l:X16}";
    }
  }
}
