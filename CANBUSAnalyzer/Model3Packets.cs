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
    private int drivePowerMax;
    private double chargeTotal;
    private double dischargeTotal;
    private double nominalFullPackEnergy;
    private double nominalRemaining;
    private double buffer;
    private double soc;
    private double dcChargeTotal;
    private double dcCharge;
    private double acChargeTotal;
    private double acCharge;
    private double regenTotal;
    private double regen;
    private double driveTotal;
    private double drive;
    private double discharge;
    private double charge;
    private double tripDistance;
    private MainWindow mainWindow;
    private double? cellTempMax;
    private double? cellTempMin;
    private double? cellVoltMax;
    private double? cellVoltMin;
    private double? cacMin;
    private double? cacMax;

    protected override PacketDefinitions GetPacketDefinitions()
    {
        return PacketDefinitions.GetSMTModel3();    
    }

    public Model3Packets(MainWindow mainWindow) : base(mainWindow) {

      this.mainWindow = mainWindow;

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

      packets.Add(0x132, p = new Packet(0x132, this));
      p.AddValue("Battery voltage", " V", "bpr", (bytes) => volt =
          (bytes[0] + (bytes[1] << 8)) / 100.0);
      /*p.AddValue("Battery current", " A", "b", (bytes) =>
          1000 - ((Int16)((((bytes[3]) << 8) + bytes[2]))) / 10.0);*/
      p.AddValue("Battery current 0 ofs", " A", "b", (bytes) => amp =
          -((Int16)((((bytes[3]) << 8) + bytes[2]))) / 10.0);
      p.AddValue("Battery power", " kW", "bpe", (bytes) => power = amp * volt / 1000.0);
      p.AddValue("Battery power inv", " kW", "bpe", (bytes) => -amp * volt / 1000.0);

      packets.Add(0x2E5, p = new Packet(0x2E5, this));
      p.AddValue("F power", "kW", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 16, 11, true, 0.5, 0));
      p.AddValue("F heat power", "kW", "e", (bytes) =>
        ExtractSignalFromBytes(bytes, 48, 8, true, 0.08, 0));

      packets.Add(0x266, p = new Packet(0x266, this));
      p.AddValue("R power", "kW", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 16, 11, true, 0.5, 0));
      p.AddValue("R heat power", "kW", "e", (bytes) =>
        ExtractSignalFromBytes(bytes, 48, 8, true, 0.08, 0));


      /*packets.Add(0x186, p = new Packet(0x186, this));
      p.AddValue("F torque", "Nm", "ph", (bytes) =>
        ExtractSignalFromBytes(bytes, 27, 13, true, 2, 0));*/
      packets.Add(0x1D4, p = new Packet(0x1D4, this));
      p.AddValue("F torque", "Nm", "pf", (bytes) =>
        (bytes[5] + ((bytes[6] & 0x1F) << 8) - (512 * (bytes[6] & 0x10))) * 0.25);
      packets.Add(0x154, p = new Packet(0x154, this));
      p.AddValue("R torque", "Nm", "pf", (bytes) =>
        (bytes[5] + ((bytes[6] & 0x1F) << 8) - (512 * (bytes[6] & 0x10))) * 0.25);


      packets.Add(0x108, p = new Packet(0x108, this));
      p.AddValue("R torque (108)", "Nm", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 27, 13, true, .22222, 0));

      packets.Add(0x257, p = new Packet(0x257, this));
      p.AddValue("Speed", "km|h", "p", (bytes) =>
                //ExtractSignalFromBytes(bytes, 12, 12, true, 0.08, -40));
                //ExtractSignalFromBytes(bytes, 10, 14, true, 1/*0.05 * miles_to_km*/, 0/*-25 * miles_to_km*/));
                ExtractSignalFromBytes(bytes, 12, 12, false, 0.08, -40, false));
      //((bytes[2] + ((bytes[3] & 0xF) << 8)) - 500) / 20.0 * miles_to_km);

      packets.Add(0x129, p = new Packet(0x129, this));
      p.AddValue("Steering Angle", "Deg", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 16, 14, false, 0.1, -819.2));
      p.AddValue("Steering Speed", "D/S", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 32, 14, false, 0.5, -4096));

      packets.Add(0x118, p = new Packet(0x118, this));
      p.AddValue("Accelerator Pedal", "%", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 32, 8, false, 0.4, 0));
      p.AddValue("Brake Pedal", "On/Off", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 19, 2, false, 1, 0));

      packets.Add(0x3F2, p = new Packet(0x3F2, this));
      p.AddValue("DC Charge total", "kWH", "b", (bytes) => {
        if ((bytes[0] & 7) == 1) {
          dcChargeTotal = (bytes[1] + (bytes[2] << 8) + (bytes[3] << 16) + (bytes[4] << 24)) * 0.001;
          if (mainWindow.trip.dcChargeStart == 0)
            mainWindow.trip.dcChargeStart = dcChargeTotal;
          dcCharge = dcChargeTotal - mainWindow.trip.dcChargeStart;
          return dcChargeTotal;
        } else return null;
      });
      p.AddValue("AC Charge total", "kWH", "b", (bytes) => {
        if ((bytes[0] & 7) == 0) {
          acChargeTotal = (bytes[1] + (bytes[2] << 8) + (bytes[3] << 16) + (bytes[4] << 24)) * 0.001;
          if (mainWindow.trip.acChargeStart == 0)
            mainWindow.trip.acChargeStart = acChargeTotal;
          acCharge = acChargeTotal - mainWindow.trip.acChargeStart;
          return acChargeTotal;
        } else return null;
      });
      p.AddValue("DC Charge", "kWh", "ti",
        (bytes) => dcChargeTotal - mainWindow.trip.dcChargeStart);
      p.AddValue("AC Charge", "kWh", "ti",
        (bytes) => acChargeTotal - mainWindow.trip.acChargeStart);

      p.AddValue("Regen total", "kWH", "b", (bytes) => {
        if ((bytes[0] & 7) == 2) {
          regenTotal = (bytes[1] + (bytes[2] << 8) + (bytes[3] << 16) + (bytes[4] << 24)) * 0.001;
          if (mainWindow.trip.regenStart == 0)
            mainWindow.trip.regenStart = regenTotal;
          regen = regenTotal - mainWindow.trip.regenStart;
          return regenTotal;
        } else return null;
      });
      p.AddValue("Drive total", "kWH", "b", (bytes) => {
        if ((bytes[0] & 7) == 3) {
          driveTotal = (bytes[1] + (bytes[2] << 8) + (bytes[3] << 16) + (bytes[4] << 24)) * 0.001 - regenTotal;
          if (mainWindow.trip.driveStart == 0)
            mainWindow.trip.driveStart = driveTotal;
          drive = driveTotal - mainWindow.trip.driveStart;
          return driveTotal;
        } else return null;
      });

      p.AddValue("Regenerated", "kWh", "tr",
        (bytes) => regen);
      p.AddValue("Energy", "kWh", "tr",
          (bytes) => drive);
      p.AddValue("Regen %", "% ", "tr",
          (bytes) => drive > 0 ? regen / drive * 100 : (double?)null);//,

      packets.Add(0x3B6, p = new Packet(0x3B6, this));
      p.AddValue("Odometer", "Km", "b",
          (bytes) => odometer = (bytes[0] + (bytes[1] << 8) + (bytes[2] << 16) + (bytes[3] << 24)) / 1000.0);

      p.AddValue("Distance", "km", "tr",
          (bytes) => {
            if (mainWindow.trip.odometerStart == 0)
              mainWindow.trip.odometerStart = odometer;
            return tripDistance = odometer - mainWindow.trip.odometerStart;
          });
      p.AddValue("Avg consumption", "wh|km", "tr",
          (bytes) => tripDistance > 0 ? drive / tripDistance * 1000 : (double?)null,
            new int[] { 0x3F2 });


      /*packets.Add(0x3B6, p = new Packet(0x3B6, this));
      p.AddValue("Odometer", "Km", "b",
          (bytes) => odometer = (bytes[0] + (bytes[1] << 8) + (bytes[2] << 16) + (bytes[3] << 24)) / 1000.0);*/

      /*packets.Add(0x154, p = new Packet(0x154, this));
      p.AddValue("Rr torque measured", "Nm", "p", (bytes) => torque =
         (bytes[5] + ((bytes[6] & 0x1F) << 8) - (512 * (bytes[6] & 0x10))) * 0.25);*/

      /*packets.Add(0x108, p = new Packet(0x108, this));
      p.AddValue("Rr motor RPM", "RPM", "",
          (bytes) => rrpm = (bytes[5] + (bytes[6] << 8)) - (512 * (bytes[6] & 0x80)));*/

      packets.Add(0x376, p = new Packet(0x376, this));
      p.AddValue("Inverter temp 1", " C", "c",
        (bytes) => (bytes[0] - 40));
      p.AddValue("Inverter temp 2", " C", "c",
        (bytes) => (bytes[1] - 40));
      p.AddValue("Inverter temp 3", " C", "c",
        (bytes) => (bytes[2] - 40));
      p.AddValue("Inverter temp 4", " C", "c",
        (bytes) => (bytes[4] - 40));

      packets.Add(0x292, p = new Packet(0x292, this));
      p.AddValue("SOC UI", "%", "br", (bytes) => (bytes[0] + ((bytes[1] & 0x3) << 8)) / 10.0);
      p.AddValue("SOC Min", "%", "br", (bytes) => ((bytes[1] >> 2) + ((bytes[2] & 0xF) << 6)) / 10.0);
      p.AddValue("SOC Max", "%", "br", (bytes) => ((bytes[2] >> 4) + ((bytes[3] & 0x3F) << 4)) / 10.0);
      p.AddValue("SOC Avg", "%", "br", (bytes) => ((bytes[3] >> 6) + ((bytes[4]) << 2)) / 10.0);

      packets.Add(0x252, p = new Packet(0x252, this));
      p.AddValue("Max discharge power", "kW", "b", (bytes) => (bytes[2] + (bytes[3] << 8)) / 100.0);
      p.AddValue("Max regen power", "kW", "b", (bytes) => (bytes[0] + (bytes[1] << 8)) / 100.0);

      packets.Add(0x268, p = new Packet(0x268, this));
      p.AddValue("Sys max drive power", "kW", "b", (bytes) => (bytes[2]));
      p.AddValue("Sys max regen power", "kW", "b", (bytes) => (bytes[3]));
      p.AddValue("Sys max heat power", "kW", "b", (bytes) => (bytes[0] * 0.08));
      p.AddValue("Sys heat power", "kW", "b", (bytes) => (bytes[1] * 0.08));

      packets.Add(0x3FE, p = new Packet(0x3FE, this));
      p.AddValue("FL brake est", " C", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 0, 10, false, 1, -40));
      p.AddValue("FR brake est", " C", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 10, 10, false, 1, -40));
      p.AddValue("RL brake est", " C", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 20, 10, false, 1, -40));
      p.AddValue("RR brake est", " C", "p", (bytes) =>
        ExtractSignalFromBytes(bytes, 30, 10, false, 1, -40));



      packets.Add(0x352, p = new Packet(0x352, this));
      p.AddValue("Nominal full pack", "kWh", "br", (bytes) => {
        if ((bytes[0]) == 0)
          return nominalFullPackEnergy = (double)ExtractSignalFromBytes(bytes, 16, 16, false, 0.02, 0);
        else
          return null;
      });
      p.AddValue("Nominal remaining", "kWh", "br", (bytes) => {
        if ((bytes[0]) == 0)
          return nominalRemaining = (double)ExtractSignalFromBytes(bytes, 32, 16, false, 0.02, 0);
        else
          return null;
      });
      p.AddValue("Ideal remaining", "kWh", "r", (bytes) => {
        if ((bytes[0]) == 1)
          return null;
        return (double)ExtractSignalFromBytes(bytes, 32, 16, false, 0.02, 0);
      });
      p.AddValue("Expected remaining", "kWh", "r", (bytes) => {
        if ((bytes[0]) == 1)
          return null;
        return (double)ExtractSignalFromBytes(bytes, 48, 16, false, 0.02, 0);
      });
      p.AddValue("To charge complete", "kWh", "", (bytes) => {
        if ((bytes[0]) == 0)
          return null;
        return (double)ExtractSignalFromBytes(bytes, 48, 16, false, 0.02, 0);
      });
      p.AddValue("Energy buffer", "kWh", "br", (bytes) => {
        if ((bytes[0]) == 1)
          return buffer = (double)ExtractSignalFromBytes(bytes, 16, 16, false, 0.01, 0);
        return null;
      });
      /*p.AddValue("Nominal full pack", "kWh", "br", (bytes) => nominalFullPackEnergy = (bytes[0] + ((bytes[1] & 0x03) << 8)) * 0.1);
      p.AddValue("Nominal remaining", "kWh", "br", (bytes) => nominalRemaining = ((bytes[1] >> 2) + ((bytes[2] & 0x0F) * 64)) * 0.1);
      p.AddValue("Expected remaining", "kWh", "r", (bytes) => ((bytes[2] >> 4) + ((bytes[3] & 0x3F) * 16)) * 0.1);
      p.AddValue("Ideal remaining", "kWh", "r", (bytes) => ((bytes[3] >> 6) + ((bytes[4] & 0xFF) * 4)) * 0.1);
      p.AddValue("To charge complete", "kWh", "", (bytes) => (bytes[5] + ((bytes[6] & 0x03) << 8)) * 0.1);
      p.AddValue("Energy buffer", "kWh", "br", (bytes) => buffer = ((bytes[6] >> 2) + ((bytes[7] & 0x03) * 64)) * 0.1);
      p.AddValue("SOC", "%", "br", (bytes) => soc = (nominalRemaining - buffer) / (nominalFullPackEnergy - buffer) * 100.0);*/

      packets.Add(0x332, p = new Packet(0x332, this));
      p.AddValue("Cell temp max", "C", "cb", (bytes) => {
        if ((bytes[0] & 3) == 0)
          return cellTempMax = ExtractSignalFromBytes(bytes, 16, 8, false, 0.5, -40);
        else return null;
      });
      p.AddValue("Cell temp mid", "C", "cbmp", (bytes) => {
        if ((bytes[0] & 3) == 0)
          return (cellTempMax + cellTempMin) / 2.0;
        else return null;
      });
      p.AddValue("Cell temp min", "C", "cb", (bytes) => {
        if ((bytes[0] & 3) == 0)
          return cellTempMin = ExtractSignalFromBytes(bytes, 24, 8, false, 0.5, -40);
        else return null;
      });

      p.AddValue("Cell volt max", "V", "cb", (bytes) => {
        if ((bytes[0] & 3) == 1)
          return cellVoltMax = ExtractSignalFromBytes(bytes, 2, 12, false, 0.002, 0);
        else return null;
      });
      p.AddValue("Cell volt mid", "V", "cbmp", (bytes) => {
        if ((bytes[0] & 3) == 1)
          return (cellVoltMax + cellVoltMin) / 2.0;
        else return null;
      });
      p.AddValue("Cell volt min", "V", "cb", (bytes) => {
        if ((bytes[0] & 3) == 1)
          return cellVoltMin = ExtractSignalFromBytes(bytes, 16, 12, false, 0.002, 0);
        else return null;
      });



      /*SG_ BMS_thermistorTMin m0: 24 | 8@1 + (0.5, -40)[0 | 0] "DegC" X
           SG_ BMS_modelTMax m0: 32 | 8@1 + (0.5, -40)[0 | 0] "DegC" X
                SG_ BMS_modelTMin m0: 40 | 8@1 + (0.5, -40)[0 | 0] "DegC" X
                     SG_ BMS_brickVoltageMax m1: 2 | 12@1 + (0.002, 0)[0 | 0] "V" X
                          SG_ BMS_brickVoltageMin m1: 16 | 12@1 + (0.002, 0)[0 | 0] "V" X
                               SG_ BMS_brickNumVoltageMax m1: 32 | 7@1 + (1, 1)[0 | 0] "" X
                                    SG_ BMS_brickNumVoltageMin m1: 40 | 7@1 + (1, 1)[0 | 0] "" X*/




      packets.Add(0x212, p = new Packet(0x212, this));
      p.AddValue("Battery temp", "C", "c",
        (bytes) => ((bytes[7]) / 2.0) - 40.0);

      packets.Add(0x321, p = new Packet(0x321, this));
      p.AddValue("Battery inlet", "C", "c",
        (bytes) => ((bytes[0] + ((bytes[1] & 0x3) << 8)) * 0.125) - 40);
      p.AddValue("Powertrain inlet", "C", "c",
        (bytes) => (((((bytes[2] & 0xF) << 8) + bytes[1]) >> 2) * 0.125) - 40);
      p.AddValue("Outside temp", "C", "c",
        (bytes) => ((bytes[3] * 0.5) - 40));
      p.AddValue("Outside temp filtered", "C", "",
        (bytes) => ((bytes[5] * 0.5) - 40));

      packets.Add(0x241, p = new Packet(0x241, this));
      p.AddValue("Battery flow", "lpm", "c",
        (bytes) =>
        ExtractSignalFromBytes(bytes, 0, 9, false, 0.1, 0));
      p.AddValue("Powertrain flow", "lpm", "c",
        (bytes) =>
        ExtractSignalFromBytes(bytes, 22, 9, false, 0.1, 0));

      /*SG_ VCFRONT_pumpBatteryRPMActual m0: 3 | 10@1 + (10, 0)[0 | 0] "rpm" ETH
      SG_ VCFRONT_pumpPT                 m0: 13 | 10@1 + (10, 0)[0 | 0] "rpm" ETH
      SG_ VCFRONT_radiatorFanOutVoltage m1: 8 | 7@1 + (0.2, 0)[0 | 0] "V" ETH
      SG_ VCFRONT_radiatorFanRPMTarget m1: 16 | 10@1 + (10, 0)[0 | 0] "rpm" ETH

      */


      packets.Add(705, p = new Packet(705, this));
      p.AddValue("Battery pump RPM", "RPM", "c", (bytes) => {
        var mux = bytes[0] & 7;
        if (mux == 0)
          return ExtractSignalFromBytes(bytes, 3, 10, false, 10, 0);
        else return null;
      });
      p.AddValue("Powertrain pump RPM", "RPM", "c", (bytes) => {
        var mux = bytes[0] & 7;
        if (mux == 0)
          return ExtractSignalFromBytes(bytes, 13, 10, false, 10, 0);
        else return null;
      });
      p.AddValue("Radiator fan V", "V", "c", (bytes) => {
        var mux = bytes[0] & 7;
        if (mux == 1)
          return ExtractSignalFromBytes(bytes, 8, 7, false, 0.2, 0);
        else return null;
      });
      p.AddValue("Radiator fan target", "RPM", "c", (bytes) => {
        var mux = bytes[0] & 7;
        if (mux == 1)
          return ExtractSignalFromBytes(bytes, 16, 10, false, 10, 0);
        else return null;
      });
      p.AddValue("Radiator fan RPM", "RPM", "c", (bytes) => {
        var mux = bytes[0] & 7;
        if (mux == 1)
          return bytes[4] * 10;
        else return null;
      });
      p.AddValue("mux", "b", "br",
        (bytes) => {
          var mux = bytes[0] & 7;
          for (int i = 0; i < 8; i++)
            UpdateItem("705 m" + mux + " byte " + i, "", "", i, bytes[i], 705);
          return mux;
        });
      p.AddValue("mux", "b", "br",
        (bytes) => {
          var mux = bytes[0] & 7;
          for (int i = 0; i < 8; i+=2)
            UpdateItem("705 m" + mux + " word " + i, "", "", i, bytes[i]<<8+bytes[i+1], 705);
          return mux;
        });

      packets.Add(897, p = new Packet(897, this));
      p.AddValue("mux", "b", "br",
        (bytes) => {
          var mux = bytes[0] & 7;
          for (int i = 0; i < 8; i++)
            UpdateItem("897 m" + mux + " byte " + i, "", "", i, bytes[i], 897);
          return mux;
        });

      /*p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[0];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
          return bytes[1];
          else return null;
      });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[2];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[3];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[4];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[5];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[6];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[7];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
  (bytes) => {
    if ((bytes[0] & 7) == i)
      return bytes[0];
    else return null;
  });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[1];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[2];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[3];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[4];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[5];
          else return null;
        });
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[6];
          else return null;
        });
      i = 1;
      j = 0;
      p.AddValue("m" + i + " byte " + j++, "b", "br",
        (bytes) => {
          if ((bytes[0] & 7) == i)
            return bytes[7];
          else return null;
        });*/

      packets.Add(0x3D2, p = new Packet(0x3D2, this));
      p.AddValue("Charge total", "kWH", "bs",
                (bytes) => {
                  chargeTotal =
                    (bytes[4] +
                    (bytes[5] << 8) +
                    (bytes[6] << 16) +
                    (bytes[7] << 24)) / 1000.0;
                  if (mainWindow.trip.chargeStart == 0)
                    mainWindow.trip.chargeStart = chargeTotal;
                  charge = chargeTotal - mainWindow.trip.chargeStart;
                  return chargeTotal;
                });

      p.AddValue("Discharge total", "kWH", "b",
          (bytes) => {
            dischargeTotal =
                    (bytes[0] +
                    (bytes[1] << 8) +
                    (bytes[2] << 16) +
                    (bytes[3] << 24)) / 1000.0;
            if (mainWindow.trip.dischargeStart == 0)
              mainWindow.trip.dischargeStart = dischargeTotal;
            discharge = dischargeTotal - mainWindow.trip.dischargeStart;
            return dischargeTotal;
          });

      p.AddValue("Discharge cycles", "x", "b",
          (bytes) => nominalFullPackEnergy > 0 ? dischargeTotal / nominalFullPackEnergy : (double?)null,
          new int[] { 0x382 });
      p.AddValue("Charge cycles", "x", "b",
          (bytes) => nominalFullPackEnergy > 0 ? chargeTotal / nominalFullPackEnergy : (double?)null,
          new int[] { 0x382 });

      p.AddValue("RegenFromCharge", "kWh", "tr",
          (bytes) => charge - acCharge - dcCharge,
          new int[] { 0x3F2 });
      p.AddValue("EnergyFromDischarge", "kWh", "tr",
          (bytes) => discharge - (charge - acCharge - dcCharge),
          new int[] { 0x3F2 });
      p.AddValue("Discharge", "kWh", "r",
          (bytes) => discharge);
      p.AddValue("Charge", "kWh", "r",
          (bytes) => charge);
      p.AddValue("Stationary", "kWh", "tr",
          (bytes) => ((discharge - (charge - acCharge - dcCharge)) - drive),
          new int[] { 0x3F2 });

      packets.Add(0x3B2, p = new Packet(0x3B2, this));
      p.AddValue("CAC min", "Ah", "b", (bytes) => {
        if ((bytes[0] & 0x3F) != 0)
          return null;
        return cacMin = ExtractSignalFromBytes(bytes, 19, 13, false, 0.1, 0);
      });
      p.AddValue("CAC avg", "Ah", "b", (bytes) => {
        if ((bytes[0] & 0x3F) != 0)
          return null;
        return ExtractSignalFromBytes(bytes, 6, 13, false, 0.1, 0);
      });
      p.AddValue("CAC max", "Ah", "b", (bytes) => {
        if ((bytes[0] & 0x3F) != 0)
          return null;
        return cacMax = ExtractSignalFromBytes(bytes, 40, 13, false, 0.1, 0);
      });
      p.AddValue("CAC imbalance", "Ah", "bz", (bytes) => {
        if ((bytes[0] & 3) == 1)
          return cacMax - cacMin;
        else
          return null;
      });
      p.AddValue("CAC min brick id", "", "b", (bytes) => {
        if ((bytes[0] & 0x3F) != 0)
          return null;
        return ExtractSignalFromBytes(bytes, 32, 7, false, 1, 1);
      });
      p.AddValue("CAC max brick id", "", "b", (bytes) => {
        if ((bytes[0] & 0x3F) != 0)
          return null;
        return ExtractSignalFromBytes(bytes, 56, 7, false, 1, 1);
      });


      packets.Add(0x401, p = new Packet(0x401, this));
      p.AddValue("Last cell block updated", "xb", "", (bytes) => {
        if (bytes.Length < 8)
          return null; // TODO: Investigate if these are real packets (last cells of a 60 or 75 maybe? or just garbage)
        Int64 data = BitConverter.ToInt64(bytes, 0);
        int cell = 0;
        for (int i = 0; i < 3; i++) {
          var val = ((data >> ((16 * i) + 16)) & 0xFFFF);
          if (val > 0)
            UpdateItem("Cell " + (cell = ((bytes[0]) * 3 + i + 1)).ToString().PadLeft(2) + " voltage"
              , "Vc"
              , "z"
              , (bytes[0]) * 4 + i + 2000
              , val * 0.0001
              , 0x401);
        }
        if (cell > numCells)
          numCells = cell;

        return bytes[0];
      });

    }
  }
}
