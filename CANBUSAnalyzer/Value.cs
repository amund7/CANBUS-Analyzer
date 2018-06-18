using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Runtime.Serialization;

namespace TeslaSCAN {


  /* a Value is an item read from the car, or calculated.
   * Values are initated at the startup of the program, and doesn't care 
   * if they have been read or not, they exist anyways.
   * This means they will reserve packetId space for filters etc, even if
   * the car hasn't sent that packet yet.
   * */

  [System.Diagnostics.DebuggerDisplay("{ToString()}")]
  public class Value {
    public string name;
    public string unit;
    public string tag;
    public int index;
    static int count;
    public Func<byte[], double?> formula;
    public List<int> packetId;

    public Value(string name, string unit, string tag, Func<byte[], double?> formula, List<int> packetId) {
      this.name = name;
      this.unit = unit;
      this.index = count++;
      this.formula = formula;
      this.tag = tag;
      this.packetId = packetId;
    }

    public Value() { } // for serializer
  }

}