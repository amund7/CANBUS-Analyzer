//#define VERBOSE


using OxyPlot;
using OxyPlot.Wpf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Xamarin.Forms.Dynamic;

namespace TeslaSCAN {

  public class ListElement : INotifyPropertyChanged {
    public int packetId { get; set; }
    public string idHex { get { return System.Convert.ToString(packetId, 16).ToUpper().PadLeft(3,'0'); } }
    public string name { get; set; }
    private double value;
    public double Current { get { return value; } }

    public ConcurrentStack<DataPoint> Points { get; set; }
    public LineSeries Line { get; private set; }

    public string unit { get; set; }
    public int index;
    public double max { get; set; }
    public double min { get; set; }
    public bool changed;
    public bool selected;
    public string tag;
    public int viewType;
    public long timeStamp;
    public List<int> bits = new List<int>();
    public int numBits { get { return bits.Any() ? bits.Last() - bits.First() + 1: 0; } }
    public double scaling { get; set; }
    public double previous;

    public override string ToString() {
      return value.ToString();
    }

    public double Convert(double val, bool convertToImperial) {
      if (!convertToImperial)
        return val;
      if (unit.ToUpper() == "C" || unit == "zCC")
        return val * 1.8 + 32;
      if (unit == "Nm")
        return val * Parser.nm_to_ftlb;
      if (unit == "wh|km")
        return val * Parser.miles_to_km;
      if (unit.ToLower().Contains("km"))
        return val / Parser.miles_to_km;
      return val;
    }

    public string GetUnit(bool convertToImperial) {
      if (!convertToImperial)
        return unit;
      if (unit.ToUpper() == "C" || unit == "zCC")
        return "F";
      if (unit == "Nm")
        return "LbFt";
      if (unit == "wh|km")
        return "wh|mi";
      if (unit.ToLower().Contains("km"))
        return unit.ToLower().Replace("km", "mi");
      return unit;
    }

    public double GetValue(bool convertToImperial) {
      if (!convertToImperial)
        return value;
      else
        return Convert(value, convertToImperial);
    }

    public void SetValue(double val) {
      previous = value;
      changed = value != val;
      value = val;
      if (value > max)
        max = value;
      if (value < min)
        min = value;
      if (changed)
        NotifyPropertyChanged("Current");
#if VERBOSE
            Console.WriteLine(this.name + " " + val);
#endif
      Points.Push(new DataPoint(OxyPlot.Axes.DateTimeAxis.ToDouble(DateTime.Now), value));
      NotifyPropertyChanged("Points");
      /*if (Points.Count > 1) {
        double dt = Points[Points.Count - 1].X - Points[Points.Count - 2].X;
        double a = dt / (0.99 + dt);
        Points[Points.Count - 1] = new DataPoint(Points[Points.Count - 1].X, Points[Points.Count - 2].Y * (1-a) + Points[Points.Count - 1].Y * a);
      }*/
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public void NotifyPropertyChanged(String propertyName = "") {
      if (PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }


    public ListElement(string name, string unit, string tag, int index, double value,  int packetId) {
      this.packetId = packetId;
      this.name = name;
      this.value = value;
      this.unit = unit;
      this.tag = tag;
      this.index = index;
      min = max = value;
      changed = true;

      Points = new ConcurrentStack<DataPoint>();
    }
  }


  public class ValueLimit {
    public double min, max;
    public bool changed;
    public string ToString() {
      return min + "/" + max;
    }

    public ValueLimit(double value) {
      changed = true;
      min = max = value;
    }
  }



  [Serializable]
  public class Parser {

    public Dictionary<string, ListElement> items;
    public SortedList<int, Packet> packets;
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

    public Parser() {
      items = new Dictionary<string, ListElement>();
      packets = new SortedList<int, Packet>();

      Packet p;
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



    public List<Value> GetAllValues() {
      List<Value> result = new List<TeslaSCAN.Value>();
      foreach (var p in packets)
        foreach (var v in p.Value.values)
          result.Add(v);
      return result;
    }


    private void ParsePacket(string raw, int id, byte[] bytes) {
      if (packets.ContainsKey(id)) {
        packets[id].Update(bytes);
        numUpdates++;
        /*if (time < SystemClock.ElapsedRealtime()) {
          UpdateItem("x  Packets per second", "xp", "", 0, numUpdates, 0xFFF);
          numUpdates = 0;
          time = SystemClock.ElapsedRealtime() + 1000;
          foreach (var item in items.Where(x => x.Value.LimitsChanged()).Select(x => x.Value))
            adapter.Touch(item);
        }*/
      }
    }

    public void UpdateItem(string name, string unit, string tag, int index, double value, int id) {
      ListElement l;
      items.TryGetValue(name, out l);
      if (l == null) {
        items.Add(name, l = new ListElement(name, unit, tag, index, value, id));
        //mainActivity.currentTab.AddElements(l);
        /*adapter.GetContext().RunOnUiThread(() => {
          adapter.items = mainActivity.currentTab.GetItems(this);
          adapter.NotifyChange();
        });*/
      } else l.SetValue(value);
    }


    public List<ListElement> GetDefaultItems() {
      return items
        .Values
        .OrderBy(x => x.index)
        .OrderBy(x => { x.selected = false; return x.unit; })
        .ToList<ListElement>();
    }

    public List<ListElement> GetItems(string tag) {
      if (tag=="" || tag==null)
        return GetDefaultItems();
      var charArray = tag.ToCharArray(); // I'll cache it to be nice to the CPU cycles
      tagFilter = charArray;
      return items
        .OrderBy(x => x.Value.index)
        .OrderBy(x => x.Value.unit)
        .Where(x => x.Value.tag?.IndexOfAny(charArray) >= 0)
        .Select(x => { x.Value.selected = false; return x.Value; })
        .ToList();
    }

    public List<Value> GetValues(string tag) {
      var charArray = tag.ToCharArray(); // I'll cache it to be nice to the CPU cycles
      tagFilter = charArray;
      List<Value> values = new List<Value>();
      foreach (var packet in packets)
        foreach (var value in packet.Value.values)
          if (value.tag.IndexOfAny(charArray) >= 0 || tag=="")
            values.Add(value);

      return values
        //.OrderBy(x => x.index)
        //.OrderBy(x => x.unit)
        .ToList();
    }


    public string[] GetCANFilter(List<Value> items) {
      var f=items.FirstOrDefault();
      int filter=0;
      if (f != null)
        filter = f.packetId.First();
      int mask = 0;

      List<int> ids = new List<int>();
      foreach (var item in items)
        foreach (var id in item.packetId)
          if (!ids.Exists(x => x == id))
            ids.Add(id);

      foreach (var id in ids) {
        for (int bit = 0; bit < 11; bit++)
          if (((id >> bit) & 1) != ((filter >> bit) & 1)) {
            mask |= 1 << bit;
            //filter &= ~(1 << bit);
          }
      }
      mask = ~mask & 0x7FF;
      Console.WriteLine(Convert.ToString(mask, 2).PadLeft(11, '0'));
      Console.WriteLine("{0,4} filter: {1,3:X} mask: {2,3:X}", 1, filter, mask, 1, 1);
      List<string> result = new List<string>();
      result.Add(Convert.ToString(mask, 16));
      result.Add(Convert.ToString(filter, 16));
      foreach (int id in ids)
        result.Add(Convert.ToString(id, 16));
      return result.ToArray();
    }

    public string[] GetCANFilter(string tag) {
      int filter = 0;
      int mask = 0;
      List<int> ids = new List<int>();
      foreach (var packet in packets.Values)
        foreach (var value in packet
          .values
          .Where(x => x.tag.IndexOfAny(tag.ToCharArray()) >= 0 || tag==""))
          if (!ids.Exists(x=>x == packet.id))
          ids.Add(packet.id);

      if (tag.Contains('z'))
        ids.Add(0x6F2);     

      foreach (var id in ids) {
        if (filter == 0)
          filter = id;
        for (int bit = 0; bit < 11; bit++)
          if (((id >> bit) & 1) != ((filter >> bit) & 1)) {
            mask |= 1 << bit;
            //filter &= ~(1 << bit);
          }
      }
      mask = ~mask & 0x7FF;
      Console.WriteLine(Convert.ToString(mask, 2).PadLeft(11, '0'));
      Console.WriteLine("{0,4} filter: {1,3:X} mask: {2,3:X}", 1, filter, mask, 1, 1);
      List<string> result = new List<string>();
      result.Add(Convert.ToString(mask, 16));
      result.Add(Convert.ToString(filter, 16));
      foreach (int id in ids)
        result.Add(Convert.ToString(id, 16));
      return result.ToArray();
    }

    // returns true IF startup=true AND all packets tagged with 's' have been received.

    public List<int> GetCANids(string tag) {
      int filter = 0;
      int mask = 0;
      List<int> ids = new List<int>();
      foreach (var packet in packets.Values)
        foreach (var value in packet
          .values
          .Where(x => x.tag.IndexOfAny(tag.ToCharArray()) >= 0 || tag == ""))
          if (!ids.Exists(x => x == packet.id))
            ids.Add(packet.id);
      return ids;
    }


      public bool Parse(string input, int idToFind) {
      if (!input.Contains('\n'))
        return false;
      if (input.StartsWith(">"))
        input = input.Substring(1);
      List<string> lines = input?.Split('\n').ToList();
      lines.Remove(lines.Last());

      bool found = false;

      foreach (var line in lines)
        try {
          /*if (!(line.Length == 11 && line.StartsWith("562")) &&
              !(line.Length == 15 && line.StartsWith("116") || line.StartsWith("222")) &&
              !(line.Length == 17 && (line.StartsWith("210")||line.StartsWith("115"))) &&
               line.Length != 19) { // testing an aggressive garbage collector! // 11)
#if VERBOSE
            Console.WriteLine("GC " + line);
#endif
            continue;
          }*/
#if VERBOSE
          Console.WriteLine(line);
#endif
          int id = 0;
          if (!int.TryParse(line.Substring(0,3), System.Globalization.NumberStyles.HexNumber, null, out id))
            continue;
          string[] raw = new string[(line.Length - 3) / 2];
          int r = 0;
          int i;
          for (i = 3; i < line.Length-1; i += 2)
            raw[r++] = line.Substring(i,2);
          List<byte> bytes = new List<byte>();
          i = 0;
          byte b = 0;
          for (i = 0; i < raw.Length; i++)
            if (raw[i].Length != 2 || !byte.TryParse(raw[i], System.Globalization.NumberStyles.HexNumber, null, out b))
              break;
            else bytes.Add(b);
#if disablebluetooth
          if (fastLogEnabled)
            fastLogStream.WriteLine(line);
#endif
          if (bytes.Count == raw.Length) { // try to validate the parsing 
            ParsePacket(line, id, bytes.ToArray());
            if (idToFind>0)
              if (idToFind == id)
                found=true;
          }
        } catch (Exception e) { Console.WriteLine(e.ToString()); };

      /*if (startup) {
        bool foundAll = true;
        foreach (var p in packets)
          foreach (var v in p.Value.values)
            if (v.tag.Contains('s') &&
            !items.ContainsKey(v.name)) {
              foundAll = false;
              break;
            }
        return foundAll;
      }*/
      if (found) return true;
      return false;
    }


  }
}

