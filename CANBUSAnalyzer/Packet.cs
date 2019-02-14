using System;
using System.Collections.Generic;

namespace TeslaSCAN
{
  public class Packet
  {
    public uint id;
    Parser parser;
    public List<Value> values;
    public double currentMultiplexer;

    public Packet(uint id, Parser parser)
    {
      this.id = id;
      this.parser = parser;
      values = new List<Value>();
    }

    public void AddValue(string name, string unit, string tag, Func<byte[], object> formula, int[] additionalPackets = null)
    {
      List<uint> list = new List<uint>();
      list.Add(id);
      if (additionalPackets != null)
      {
        foreach (uint i in additionalPackets)
        {
          list.Add(i);
        }
      }

      values.Add(new Value(name, unit, tag, formula, list));
    }

    public void Update(byte[] bytes)
    {
      foreach (var val in values)
      {
        //if (val.formula != null)
        {
          try
          {
            //double? d = val.formula(bytes);
            //if (d.HasValue)
            {
              // sorts by packet ID
              parser.UpdateItem(val.name, val.unit, val.tag, val.index, val.formula(bytes), id);
            }
          }
          catch (Exception e)
          {
            Console.WriteLine(e.ToString());
          }
        }
      }
    }
  }
}
