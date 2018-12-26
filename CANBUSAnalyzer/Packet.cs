//#define disablebluetooth
using System;
using System.Collections.Generic;


namespace TeslaSCAN
{

  public class Packet {
    public int id;
    Parser parser;
    public List<Value> values;

    public Packet(int id, Parser parser) {
      this.id = id;
      this.parser = parser;
      values = new List<Value>();
    }
    public void AddValue(string name, string unit, string tag, Func<byte[], double?> formula, int[] additionalPackets = null) {
      List<int> list = new List<int>();
      list.Add(id);
      if (additionalPackets!=null)
        foreach (int i in additionalPackets)
          list.Add(i);
      values.Add(new Value(name, unit, tag, formula, list));
    }
    public void Update(byte[] bytes) {
      foreach (var val in values)
        if (val.formula != null)
          try {
            double? d = val.formula(bytes);
            if (d != null)
              parser.UpdateItem(val.name, val.unit, val.tag, val.index, (double)d, id); // This guy sorts by packet ID
          } catch (Exception e) { Console.WriteLine(e.ToString()); }
    }
  }
}