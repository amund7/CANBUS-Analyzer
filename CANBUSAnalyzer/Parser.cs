using CANBUS;
using DBCLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

//#define disablebluetooth
//#define VERBOSE

namespace TeslaSCAN
{
  [Serializable]
  public abstract class Parser
  {
    enum KnownDBC
    {
      None = 0x0,
      Model3 = 0x1,
      ModelSAWD = 0x2,
      ModelSRWD = 0x4
    }

    static KnownDBC use_DBC = KnownDBC.None;

    static bool use_hardcoded_rules = (use_DBC == KnownDBC.None);
    
    private PacketDefinitions _definitions;
    protected internal PacketDefinitions Definitions
    {
        get
        {
            if (_definitions == null)
                _definitions = GetPacketDefinitions();

            return _definitions;
        }
    }

    protected abstract PacketDefinitions GetPacketDefinitions();

    public Dictionary<string, ListElement> items;
    public SortedList<uint, Packet> packets;
    public List<List<ListElement>> ignoreList;
    public const double miles_to_km = 1.609344;
    public const double kw_to_hp = 1.34102209;
    public const double nm_to_ftlb = 0.737562149;
    long time; // if I was faster I'd use 'short time'.... :)
    public int numUpdates;
    public char[] tagFilter;
    private bool fastLogEnabled;
    private StreamWriter fastLogStream;
    private List<Value> fastLogItems;
    char separator = ',';
    Stopwatch logTimer;

    static double ExtractSignalFromBytes(byte[] bytes, Message.Signal signal)
    {
      int startByte = (int)signal.StartBit / 8;
      int startBit = (int)signal.StartBit % 8;
      int numBits = (int)signal.BitSize;

      Debug.Assert(numBits > 0);

      Debug.Assert(signal.ByteOrder == Message.Signal.ByteOrderEnum.LittleEndian);

      Debug.Assert(numBits < 64);
      long totalInteger = 0;

      bool negative = false;
      int bitsConsumed = 0;
      while (numBits > 0)
      {
        int bitsToConsume = Math.Min(numBits, 8 - startBit);

        int mask = ((1 << bitsToConsume) - 1);

        int v = bytes[startByte];
        v >>= startBit;
        v &= mask;
        v <<= bitsConsumed;

        totalInteger += v;

        if (signal.ValueType == Message.Signal.ValueTypeEnum.Signed)
        {
          if (bitsToConsume == numBits)
          {
            int hibit = (1 << (numBits - 1));
            if ((v & hibit) != 0)
            {
              negative = true;
            }
          }
        }

        bitsConsumed += bitsToConsume;
        numBits -= bitsToConsume;
        startBit = 0;
        startByte++;
      }

      double totalDouble = totalInteger;

      if (negative)
      {
        totalDouble -= (1 << bitsConsumed);
      }

      totalDouble *= signal.ScaleFactor;
      totalDouble += signal.Offset;

      return totalDouble;
    }

    public Parser() {
      items = new Dictionary<string, ListElement>();
      packets = new SortedList<uint, Packet>();
      // time = SystemClock.ElapsedRealtime() + 1000;

      Packet p;

      if (use_DBC != KnownDBC.None) {
        Reader reader = new DBCLib.Reader();
        string dbcPath = @"C:\git\CANBUS-Analyzer\DBCTools\Samples";
        switch (use_DBC) {
          case KnownDBC.Model3:
            dbcPath += @"\tesla_model3.dbc";
            break;
          case KnownDBC.ModelSAWD:
            dbcPath += @"\tesla_models_awd.dbc";
            break;
          case KnownDBC.ModelSRWD:
            dbcPath += @"\tesla_models_rwd.dbc";
            break;
        }

        List<object> entries = reader.Read(dbcPath);
        foreach (object entry in entries) {
          if (entry is Message) {
            Message message = (Message)entry;

            packets.Add(message.Id, p = new Packet(message.Id, this));
            foreach (Message.Signal signal in message.Signals) {
              p.AddValue(
                signal.Name.Replace("_", " "),
                signal.Unit,
                signal.Name,
                (bytes) => ExtractSignalFromBytes(bytes, signal),
                null
                );
            }
          }
        }
      }

      packets.Add(0x1, p = new Packet(0x1, this));
      p.AddValue("Byte 0", "b", "br",
        (bytes) => (bytes[0]));
      p.AddValue("Byte 1", "b", "br",
        (bytes) => (bytes[1]));
      p.AddValue("Byte 2", "b", "br",
        (bytes) => (bytes[2]));
      p.AddValue("Byte 3", "b", "br",
        (bytes) => (bytes[3]));
      p.AddValue("Byte 4", "b", "br",
        (bytes) => (bytes[4]));
      p.AddValue("Byte 5", "b", "br",
        (bytes) => (bytes[5]));
      p.AddValue("Byte 6", "b", "br",
        (bytes) => (bytes[6]));
      p.AddValue("Byte 7", "b", "br",
        (bytes) => (bytes[7]));

      packets.Add(0x2, p = new Packet(0x2, this));
      p.AddValue("Word 0", "b", "br",
        (bytes) => (bytes[0] + (bytes[1] << 8)));
      p.AddValue("Word 1", "b", "br",
        (bytes) => (bytes[2] + (bytes[3] << 8)));
      p.AddValue("Word 2", "b", "br",
       (bytes) => (bytes[4] + (bytes[5] << 8)));
      p.AddValue("Word 3", "b", "br",
       (bytes) => (bytes[6] + (bytes[7] << 8)));

      packets.Add(0x3, p = new Packet(0x3, this));
      p.AddValue("Int 0", "b", "br",
       (bytes) => (bytes[0] + (bytes[1] << 8)) - (512 * (bytes[1] & 0x80)));
      p.AddValue("Int 1", "b", "br",
        (bytes) => (bytes[2] + (bytes[3] << 8)) - (512 * (bytes[3] & 0x80)));
      p.AddValue("Int 2", "b", "br",
        (bytes) => (bytes[4] + (bytes[5] << 8)) - (512 * (bytes[5] & 0x80)));
      p.AddValue("Int 3", "b", "br",
       (bytes) => (bytes[6] + (bytes[7] << 8)) - (512 * (bytes[7] & 0x80)));

      packets.Add(0x5, p = new Packet(0x5, this));
      p.AddValue("12 bit 0", "b", "br",
       (bytes) => (bytes[0] + ((bytes[1] & 0x0F) << 4)));
      p.AddValue("12 bit 1", "b", "br",
        (bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 8)));
      p.AddValue("12 bit 3", "b", "br",
        (bytes) => (bytes[3] + ((bytes[4] & 0x0F) << 4)));
      p.AddValue("12 bit 4", "b", "br",
       (bytes) => (((bytes[4] & 0xF0) >> 4) + ((bytes[5]) << 8)));
      p.AddValue("12 bit 5", "b", "br",
        (bytes) => (bytes[6] + ((bytes[7] & 0x0F) << 4)));

      packets.Add(0x6, p = new Packet(0x6, this));
      p.AddValue("Temp 1", " C", "h",
        (bytes) => (bytes[1] - 40));
      p.AddValue("Temp 2", " C", "h",
        (bytes) => (bytes[0] - 40));
      p.AddValue("Temp 3", " C", "h",
        (bytes) => (bytes[2] - 40));
      p.AddValue("Temp 4", " C", "h",
        (bytes) => (bytes[3] - 40));
      p.AddValue("Temp 5", " C", "h",
        (bytes) => (bytes[4] - 40));
      p.AddValue("Temp 6", " C", "h",
        (bytes) => (bytes[5] - 40));
      p.AddValue("Temp 7", " C", "h",
        (bytes) => (bytes[6] - 40));
      p.AddValue("Temp 8", " C", "h",
        (bytes) => (bytes[7] - 40));


    }

    internal static Parser FromSource(PacketDefinitions.DefinitionSource source)
    {
        switch(source)
        {
            case PacketDefinitions.DefinitionSource.SMTModelS:
                return new ModelSPackets();

            case PacketDefinitions.DefinitionSource.SMTModel3:
                return new Model3Packets();

            default: // Defaults to Model S (ScanMyTesla)
                return new ModelSPackets();
        }
    }

    public List<Value> GetAllValues()
    {
      List<Value> result = new List<TeslaSCAN.Value>();
      foreach (var p in packets)
      {
        foreach (var v in p.Value.values)
        {
          result.Add(v);
        }
      }
      return result;
    }

    private bool ParsePacket(string raw, uint id, byte[] bytes)
    {
      bool knownPacket = false;

      if (packets.ContainsKey(id))
      {
        knownPacket = true;
        packets[id].Update(bytes);
        numUpdates++;
        /*if (time < SystemClock.ElapsedRealtime()) {
          UpdateItem("x  Packets per second", "xp", "", 0, numUpdates, 0xFFF);
          numUpdates = 0;
          time = SystemClock.ElapsedRealtime() + 1000;
          foreach (var item in items.Where(x => x.Value.LimitsChanged()).Select(x => x.Value))
          {
            adapter.Touch(item);
          }
        }*/
      }

      return knownPacket;
    }

    public void UpdateItem(string name, string unit, string tag, int index, double value, uint id)
    {
      ListElement l;
      items.TryGetValue(name, out l);
      if (l == null)
      {
        items.Add(name, l = new ListElement(name, unit, tag, index, value, id));
        //mainActivity.currentTab.AddElements(l);
        /*adapter.GetContext().RunOnUiThread(() => {
          adapter.items = mainActivity.currentTab.GetItems(this);
          adapter.NotifyChange();
        });*/
      }
      else l.SetValue(value);
    }

    public List<ListElement> GetDefaultItems()
    {
      return items
        .Values
        .OrderBy(x => x.index)
        .OrderBy(x => { x.selected = false; return x.unit; })
        .ToList<ListElement>();
    }

    public List<ListElement> GetItems(string tag)
    {
      if (string.IsNullOrEmpty(tag))
      {
        return GetDefaultItems();
      }
      var charArray = tag.ToCharArray(); // I'll cache it to be nice to the CPU cycles
      tagFilter = charArray;
      return items
        .OrderBy(x => x.Value.index)
        .OrderBy(x => x.Value.unit)
        .Where(x => x.Value.tag?.IndexOfAny(charArray) >= 0)
        .Select(x => { x.Value.selected = false; return x.Value; })
        .ToList();
    }

    public List<Value> GetValues(string tag)
    {
      var charArray = tag.ToCharArray(); // I'll cache it to be nice to the CPU cycles
      tagFilter = charArray;
      List<Value> values = new List<Value>();
      foreach (var packet in packets)
      {
        foreach (var value in packet.Value.values)
        {
          if ((value.tag.IndexOfAny(charArray) >= 0) || (tag == ""))
          {
            values.Add(value);
          }
        }
      }

      return values
        //.OrderBy(x => x.index)
        //.OrderBy(x => x.unit)
        .ToList();
    }

    public string[] GetCANFilter(List<Value> items)
    {
      var f = items.FirstOrDefault();
      uint filter = 0;
      if (f != null)
      {
        filter = f.packetId.First();
      }
      int mask = 0;

      List<uint> ids = new List<uint>();
      foreach (var item in items)
      {
        foreach (var id in item.packetId)
        {
          if (!ids.Exists(x => x == id))
          {
            ids.Add(id);
          }
        }
      }

      foreach (var id in ids)
      {
        for (int bit = 0; bit < 11; bit++)
        {
          if (((id >> bit) & 1) != ((filter >> bit) & 1))
          {
            mask |= 1 << bit;
            //filter &= ~(1 << bit);
          }
        }
      }
      mask = ~mask & 0x7FF;
      Console.WriteLine(Convert.ToString(mask, 2).PadLeft(11, '0'));
      Console.WriteLine("{0,4} filter: {1,3:X} mask: {2,3:X}", 1, filter, mask);
      List<string> result = new List<string>();
      result.Add(Convert.ToString(mask, 16));
      result.Add(Convert.ToString(filter, 16));
      foreach (int id in ids)
      {
        result.Add(Convert.ToString(id, 16));
      }
      return result.ToArray();
    }

    public string[] GetCANFilter(string tag)
    {
      uint filter = 0;
      int mask = 0;
      List<uint> ids = new List<uint>();
      foreach (var packet in packets.Values)
      {
        foreach (var value in packet
          .values
          .Where(x => (x.tag.IndexOfAny(tag.ToCharArray()) >= 0) || tag == ""))
        {
          if (!ids.Exists(x => x == packet.id))
          {
            ids.Add(packet.id);
          }
        }
      }

      if (tag.Contains('z'))
      {
        ids.Add(0x6F2);
      }

      foreach (var id in ids)
      {
        if (filter == 0)
        {
          filter = id;
        }
        for (int bit = 0; bit < 11; bit++)
        {
          if (((id >> bit) & 1) != ((filter >> bit) & 1))
          {
            mask |= 1 << bit;
            //filter &= ~(1 << bit);
          }
        }
      }

      mask = ~mask & 0x7FF;
      Console.WriteLine(Convert.ToString(mask, 2).PadLeft(11, '0'));
      Console.WriteLine("{0,4} filter: {1,3:X} mask: {2,3:X}", 1, filter, mask);
      List<string> result = new List<string>();
      result.Add(Convert.ToString(mask, 16));
      result.Add(Convert.ToString(filter, 16));
      foreach (int id in ids)
      {
        result.Add(Convert.ToString(id, 16));
      }
      return result.ToArray();
    }

    // returns true IF startup=true AND all packets tagged with 's' have been received.

    public List<uint> GetCANids(string tag)
    {
      List<uint> ids = new List<uint>();
      foreach (var packet in packets.Values)
      {
        foreach (var value in packet
          .values
          .Where(x => (x.tag.IndexOfAny(tag.ToCharArray()) >= 0) || tag == ""))
        {
          if (!ids.Exists(x => x == packet.id))
          {
            ids.Add(packet.id);
          }
        }
      }
      return ids;
    }

    public bool Parse(string input, int idToFind, out bool knownPacket)
    {
      knownPacket = false;

      if (!input.Contains('\n'))
      {
        return false;
      }
      if (input.StartsWith(">"))
      {
        input = input.Substring(1);
      }
      List<string> lines = input?.Split('\n').ToList();
      lines.Remove(lines.Last());

      bool found = false;

      foreach (var line in lines)
      {
        try
        {
          /*if (!((line.Length == 11) && line.StartsWith("562")) &&
              !((line.Length == 15) && line.StartsWith("116") || line.StartsWith("222")) &&
              !((line.Length == 17) && (line.StartsWith("210") || line.StartsWith("115"))) &&
               (line.Length != 19))
          { // testing an aggressive garbage collector! // 11)
#if VERBOSE
            Console.WriteLine("GC " + line);
#endif
            continue;
          }*/
#if VERBOSE
          Console.WriteLine(line);
#endif
          uint id = 0;
          if (!uint.TryParse(line.Substring(0, 3), System.Globalization.NumberStyles.HexNumber, null, out id))
          {
            continue;
          }
          string[] raw = new string[(line.Length - 3) / 2];
          int r = 0;
          int i;
          for (i = 3; i < line.Length - 1; i += 2)
          {
            raw[r++] = line.Substring(i, 2);
          }
          List<byte> bytes = new List<byte>();
          i = 0;
          byte b = 0;
          for (i = 0; i < raw.Length; i++)
          {
            if ((raw[i].Length != 2) || !byte.TryParse(raw[i], System.Globalization.NumberStyles.HexNumber, null, out b))
            {
              break;
            }
            else
            {
              bytes.Add(b);
            }
          }
#if disablebluetooth
          if (fastLogEnabled)
          {
            fastLogStream.WriteLine(line);
          }
#endif
          if (bytes.Count == raw.Length)
          { // try to validate the parsing 
            knownPacket = ParsePacket(line, id, bytes.ToArray());
            if (idToFind > 0)
            {
              if (idToFind == id)
              {
                found = true;
              }
            }
          }
        }
        catch (Exception e)
        {
          Console.WriteLine(e.ToString());
        }

        /*if (startup)
        {
          bool foundAll = true;
          foreach (var p in packets)
          {
            foreach (var v in p.Value.values)
            {
              if (v.tag.Contains('s') &&
                  !items.ContainsKey(v.name))
              {
                foundAll = false;
                break;
              }
            }
          }
          return foundAll;
        }*/
      }
      if (found)
      {
        return true;
      }
      return false;
    }
  }
}
