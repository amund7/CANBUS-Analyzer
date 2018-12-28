using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeslaSCAN;

namespace CANBUS {
  class Model3Packets : Parser {
    private double mechPower;
    private double rDissipation;
    private double volt;
    private double amp;
    private double power;
    private double odometer;
    private double torque;
    private int numCells;
    private int rrpm;

    public Model3Packets() : base() {

      /* tags:
        p: performance
        t: trip
        b: battery
        c: temperature
        f: front drive unit
        s: startup (app will wait until these packets are found before starting 'normal' mode)
        i: imperial
        m: metric
        i: ignore
      */

      Packet p;


      packets.Add(0x266, p = new Packet(0x266, this));
      p.AddValue("Rr inverter 12V", "V12", "", (bytes) => bytes[0] / 10.0);
      p.AddValue("Rr mech power", " kW", "e", (bytes) => mechPower =
          ((bytes[2] + ((bytes[3] & 0x7) << 8)) - (512 * (bytes[3] & 0x4))) / 2.0);
      p.AddValue("Rr mech power HP", "HP", "pf", (bytes) => mechPower * kw_to_hp);
      p.AddValue("Rr dissipation", " kW", "", (bytes) => {
        rDissipation = bytes[1] * 125.0 / 1000.0 - 0.5;
        /*dissipationUpdated = true;
        dissipationTimeStamp = DateTime.Now.Millisecond;*/
        return rDissipation;
      });

      packets.Add(0x132, p = new Packet(0x132, this));
      p.AddValue("Battery voltage", " V", "bpr", (bytes) => volt =
          (bytes[0] + (bytes[1] << 8)) / 100.0);
      p.AddValue("Battery current", " A", "b", (bytes) => amp =
          1000 - ((Int16)((((bytes[3] & 0x7F) << 8) + bytes[2]) << 1)) / 20.0);
      p.AddValue("Battery power", " kW", "bpe", (bytes) => power = amp * volt / 1000.0);

      packets.Add(0x3B6, p = new Packet(0x3B6, this));
      p.AddValue("Odometer", "Km", "b",
          (bytes) => odometer = (bytes[0] + (bytes[1] << 8) + (bytes[2] << 16) + (bytes[3] << 24)) / 1000.0);

      packets.Add(0x154, p = new Packet(0x154, this));
      p.AddValue("Rr torque measured", "Nm", "p", (bytes) => torque =
         (bytes[5] + ((bytes[6] & 0x1F) << 8) - (512 * (bytes[6] & 0x10))) * 0.25);

      packets.Add(0x108, p = new Packet(0x108, this));
      p.AddValue("Rr motor RPM", "RPM", "",
          (bytes) => rrpm = (bytes[5] + (bytes[6] << 8)) - (512 * (bytes[6] & 0x80)));

      packets.Add(0x376, p = new Packet(0x376, this));
      p.AddValue("Outside temp", " C", "e",
        (bytes) => (bytes[0] / 2.0 - 20));
      p.AddValue("Outside temp filtered", " C", "e",
        (bytes) => (bytes[1] / 2.0 - 20));
      p.AddValue("Inside temp", " C", "e",
        (bytes) => (bytes[2] / 2.0 - 20));
      p.AddValue("A/C air temp", " C", "e",
        (bytes) => (bytes[4] / 2.0 - 20));



      packets.Add(0x401, p = new Packet(0x401, this));
      p.AddValue("Last cell block updated", "xb", "", (bytes) => {
        Int64 data = BitConverter.ToInt64(bytes, 0);
        if (bytes[0] == 0x10) {
          int cell = 0;
          for (int i = 1; i < 8; i++)
            UpdateItem("Cell " + (cell = ((bytes[0]) * 4 + i + 1)).ToString().PadLeft(2) + " voltage"
              , "zVC"
              , "z"
              , bytes[0]
              , bytes[i]//((data >> ((14 * i) + 8)) & 0x3FFF) * 0.000305
              , 0x401);
          if (cell > numCells)
            numCells = cell;
          /*var values = items.Where(x => x.Value.unit == "zVC");
          double min = values.Min(x => x.Value.GetValue(false));
          double max = values.Max(x => x.Value.GetValue(false));
          double avg = values.Average(x => x.Value.GetValue(false));
          UpdateItem("Cell min", "Vc", "bz", 0, min, 0x401);
          UpdateItem("Cell avg", "Vc", "bpz", 1, avg, 0x401);
          UpdateItem("Cell max", "Vc", "bz", 2, max, 0x401);
          UpdateItem("Cell diff", "Vcd", "bz", 3, max - min, 0x401);
        } else {
          for (int i = 0; i < 4; i++)
            UpdateItem("Cell " + ((bytes[0] - 24) * 4 + i + 1).ToString().PadLeft(2) + " temp"
              , "zCC"
              , "c"
              , bytes[0]
              , ((Int16)(((data >> ((14 * i) + 6)) & 0xFFFC)) * 0.0122 / 4.0)
              , 0x401);
          var values = items.Where(x => x.Value.unit == "zCC");
          double min = values.Min(x => x.Value.GetValue(false));
          double max = values.Max(x => x.Value.GetValue(false));
          double avg = values.Average(x => x.Value.GetValue(false));
          UpdateItem("Cell temp min", "c", "bcz", 0, min, 0x401);
          UpdateItem("Cell temp avg", "c", "bcpz", 1, avg, 0x401);
          UpdateItem("Cell temp max", "c", "bcz", 2, max, 0x401);
          UpdateItem("Cell temp diff", "Cd", "bcz", 3, max - min, 0x401);
        }*/
          return bytes[0];
       } else return 0;
      });
      /*
      // these are a bit stupid, but they are placeholders for the filters to be generated correctly.
      p.AddValue("Cell temp min", "C", "b", null);
      p.AddValue("Cell temp avg", "C", "bcp", null);
      p.AddValue("Cell temp max", "C", "b", null);
      p.AddValue("Cell temp diff", "Cd", "bc", null);
      p.AddValue("Cell min", "Vc", "b", null);
      p.AddValue("Cell avg", "Vc", "bpzr", null);
      p.AddValue("Cell max", "Vc", "b", null);
      p.AddValue("Cell diff", "Vcd", "bz", null);
      for (int i = 1; i <= 96; i++)
        p.AddValue("Cell " + i.ToString().PadLeft(2) + " voltage"
          , "zVC"
          , "z", null);
      for (int i = 1; i <= 32; i++)
        p.AddValue("Cell " + i.ToString().PadLeft(2) + " temp"
          , "zCC"
          , "c"
          , null);*/



    }
  }
}
