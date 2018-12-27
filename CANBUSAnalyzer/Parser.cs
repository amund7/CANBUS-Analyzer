﻿using DBCLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

//#define disablebluetooth
//#define VERBOSE

namespace TeslaSCAN
{
  [Serializable]
  public class Parser
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

    public Dictionary<string, ListElement> items;
    public SortedList<uint, Packet> packets;
    public List<List<ListElement>> ignoreList;
    public const double miles_to_km = 1.609344;
    public const double kw_to_hp = 1.34102209;
    public const double nm_to_ftlb = 0.737562149;
    double nominalFullPackEnergy;
    double amp;
    double volt;
    double power;
    double mechPower;
    double fMechPower;
    double speed;
    double drivePowerMax;
    double torque;
    double chargeTotal;
    double dischargeTotal;
    double odometer;
    //double tripDistance;
    //double charge;
    //double discharge;
    //bool metric=true;
    //long time; // if I was faster I'd use 'short time'.... :)
    public int numUpdates;
    int numCells;
    public char[] tagFilter;
    //private bool fastLogEnabled;
    //private StreamWriter fastLogStream;
    //private List<Value> fastLogItems;
    //Stopwatch logTimer;
    private double frTorque;
    private double dcChargeTotal;
    private double acChargeTotal;
    private double regenTotal;
    private double energy;
    private double regen;
    //private double acCharge;
    //private double dcCharge;
    private double nominalRemaining;
    private double buffer;
    private double soc;
    private double fl;
    private double fr;
    private double rl;
    private double rr;
    private int frpm;
    private int rrpm;
    private bool feet;
    private bool seat;
    private bool win;
    private double dcOut;
    private double dcIn;
    private double rInput;
    private double fInput;
    private double fDissipation;
    private double combinedMechPower;
    private double rDissipation;
    private double hvacPower;
    private bool dissipationUpdated;
    private int dissipationTimeStamp;
    private int statorTemp;
    private int inverterTemp;

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

    public Parser()
    {
      items = new Dictionary<string, ListElement>();
      packets = new SortedList<uint, Packet>();
      // time = SystemClock.ElapsedRealtime() + 1000;

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

      if (use_DBC != KnownDBC.None)
      {
        Reader reader = new DBCLib.Reader();
        string dbcPath = @"C:\git\CANBUS-Analyzer\DBCTools\Samples";
        switch (use_DBC)
        {
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
        foreach (object entry in entries)
        {
          if (entry is Message)
          {
            Message message = (Message)entry;

            packets.Add(message.Id, p = new Packet(message.Id, this));
            foreach (Message.Signal signal in message.Signals)
            {
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
      else
      {
        /*packets.Add(0x256, p = new Packet(0x256, this));
        p.AddValue("Metric", "bool", "s", (bytes) => {
          metric = Convert.ToBoolean(bytes[3] & 0x80);
          if (metric) {
            foreach (var packet in packets)
              foreach (var v in packet.Value.values)
                if (v.tag.Contains("i"))
                  packet.Value.values.Remove(v);
          } else {
            foreach (var packet in packets)
              foreach (var v in packet.Value.values)
                if (v.tag.Contains("m"))
                  packet.Value.values.Remove(v);
          }
          return metric ? 1 : 0;
        });*/

        packets.Add(0x102, p = new Packet(0x102, this));
        p.AddValue("Battery voltage", " V", "bpr", (bytes) => volt =
          (bytes[0] + (bytes[1] << 8)) / 100.0);
        p.AddValue("Battery current", " A", "b", (bytes) => amp =
          1000 - ((Int16)((((bytes[3] & 0x7F) << 8) + bytes[2]) << 1)) / 20.0);
        p.AddValue("Battery power", " kW", "bpe", (bytes) => power = amp * volt / 1000.0);
        //p.AddValue("cell average", "Vc", "bp", (bytes) => numCells > 70 ? volt / numCells : (double?)null);
        //p.AddValue("negative terminal", "C", (bytes) => ((bytes[6] + ((bytes[7] & 0x07) << 8))) * 0.1 - 10);

        packets.Add(0x210, p = new Packet(0x210, this));
        p.AddValue("DC-DC current", "A12", "b", (bytes) => bytes[4]);
        p.AddValue("DC-DC voltage", "V12", "b", (bytes) => bytes[5] / 10.0);
        p.AddValue("DC-DC coolant inlet", "C", "c", (bytes) => ((bytes[2] - (2 * (bytes[2] & 0x80))) * 0.5) + 40);
        p.AddValue("DC-DC input power", "W", "be", (bytes) => dcIn = (bytes[3] * 16));
        p.AddValue("DC-DC output power", "W", "b", (bytes) => dcOut = (bytes[4] * bytes[5] / 10.0));
        p.AddValue("DC-DC efficiency", "%", "e", (bytes) => dcOut / dcIn * 100.0);
        p.AddValue("HV power", " kW", "e", (bytes) => power - dcIn / 1000.0);
        p.AddValue("Heating/cooling", "kW", "eh", (bytes) =>
        {
          if (dissipationUpdated ||
              (DateTime.Now.Millisecond > dissipationTimeStamp + 2000))
          {
            hvacPower = hvacPower * 0.99 + (power - (rInput + fInput) - (dcIn / 1000.0)) * 0.01;
            dissipationUpdated = false;
            return hvacPower;
          }
          else return (double?)null;
        }, new int[] { 0x102, 0x266, 0x2E5 });

        packets.Add(0x306, p = new Packet(0x306, this));
        p.AddValue("Rr coolant inlet", "C", "c", (bytes) => bytes[5] == 0 ? (double?)null : bytes[5] - 40);
        p.AddValue("Rr inverter PCB", "C", "", (bytes) => bytes[0] - 40);
        p.AddValue("Rr stator", "C", "cp", (bytes) => statorTemp = bytes[2] - 40);
        p.AddValue("Rr DC capacitor", "C", "", (bytes) => bytes[3] - 40);
        p.AddValue("Rr heat sink", "C", "c", (bytes) => bytes[4] - 40);
        p.AddValue("Rr inverter", "C", "c", (bytes) => inverterTemp = bytes[1] - 40);
        p.AddValue("Rr stator %", "%", "c", (bytes) => (bytes[7] * .4));
        p.AddValue("Rr inverter %", "%", "c", (bytes) => (bytes[6] * .4));
        /*p.AddValue("Rr stator max", "%", "c", (bytes) => (bytes[7] *.4 / 100.0 * statorTemp ));
        p.AddValue("Rr inverter max", "%", "c", (bytes) => (bytes[6] * .4 / 100.0 * inverterTemp));*/

        packets.Add(0x1D4, p = new Packet(0x1D4, this));
        p.AddValue("Fr torque measured", "Nm", "pf", (bytes) => frTorque =
          (bytes[5] + ((bytes[6] & 0x1F) << 8) - (512 * (bytes[6] & 0x10))) * 0.25);
        p.AddValue("Rr/Fr torque bias", "%", "pf",
          (bytes) => Math.Abs(frTorque) + Math.Abs(torque) == 0 ? 50 : Math.Abs(torque) / (Math.Abs(frTorque) + Math.Abs(torque)) * 100);

        packets.Add(0x154, p = new Packet(0x154, this));
        p.AddValue("Rr torque measured", "Nm", "p", (bytes) => torque =
          (bytes[5] + ((bytes[6] & 0x1F) << 8) - (512 * (bytes[6] & 0x10))) * 0.25);
        //p.AddValue("Pedal position A", "%", "",  (bytes) => bytes[2] * 0.4);
        p.AddValue("Watt pedal", "%", "i", (bytes) => bytes[3] * 0.4);
        /*p.AddValue("HP 'measured'", "HP", "p",
          (bytes) => (torque * rpm / 9549 * kw_to_hp));*/

        packets.Add(0x2E5, p = new Packet(0x2E5, this));
        p.AddValue("Fr mech power", " kW", "fe", (bytes) => fMechPower =
          ((bytes[2] + ((bytes[3] & 0x7) << 8)) - (512 * (bytes[3] & 0x4))) / 2.0);
        p.AddValue("Fr dissipation", " kW", "f", (bytes) => fDissipation = bytes[1] * 125.0 / 1000.0);
        p.AddValue("Fr input power", " kW", "e", (bytes) => fInput = fMechPower + fDissipation);
        p.AddValue("Fr mech power HP", "HP", "pf", (bytes) => fMechPower * kw_to_hp);
        p.AddValue("Fr stator current", "A", "f", (bytes) => bytes[4] + ((bytes[5] & 0x7) << 8));
        p.AddValue("Fr drive power max", " kW", "bc", (bytes) => drivePowerMax =
          (((bytes[6] & 0x3F) << 5) + ((bytes[5] & 0xF0) >> 3)) + 1);
        p.AddValue("Mech power combined", " kW", "f", (bytes) => combinedMechPower = mechPower + fMechPower);
        p.AddValue("HP combined", "HP", "pf", (bytes) => (mechPower + fMechPower) * kw_to_hp);
        p.AddValue("Fr efficiency", "%", "e", (bytes) => Math.Abs(fMechPower) > Math.Abs(fInput) ? 100 : fMechPower / fInput * 100.0);
        p.AddValue("Fr+Rr efficiency", "%", "e", (bytes) => Math.Abs(mechPower + fMechPower) > Math.Abs(rInput + fInput) ? 100 : mechPower / rInput * 100.0);

        packets.Add(0x266, p = new Packet(0x266, this));
        p.AddValue("Rr inverter 12V", "V12", "", (bytes) => bytes[0] / 10.0);
        p.AddValue("Rr mech power", " kW", "e", (bytes) => mechPower =
          ((bytes[2] + ((bytes[3] & 0x7) << 8)) - (512 * (bytes[3] & 0x4))) / 2.0);
        p.AddValue("Rr dissipation", " kW", "", (bytes) =>
        {
          rDissipation = bytes[1] * 125.0 / 1000.0 - 0.5;
          dissipationUpdated = true;
          dissipationTimeStamp = DateTime.Now.Millisecond;
          return rDissipation;
        });
        p.AddValue("Rr input power", " kW", "e", (bytes) => rInput = mechPower + rDissipation);
        p.AddValue("Rr mech power HP", "HP", "p", (bytes) => mechPower * kw_to_hp);
        p.AddValue("Rr stator current", "A", "", (bytes) => bytes[4] + ((bytes[5] & 0x7) << 8));
        p.AddValue("Rr regen power max", "KW", "b", (bytes) => (bytes[7] * 4) - 200);
        p.AddValue("Rr drive power max", "KW", "b", (bytes) => drivePowerMax =
          (((bytes[6] & 0x3F) << 5) + ((bytes[5] & 0xF0) >> 3)) + 1);
        p.AddValue("Rr efficiency", "%", "e", (bytes) => Math.Abs(mechPower) > Math.Abs(rInput) ? 100 : mechPower / rInput * 100.0);
        p.AddValue("Non-propulsive power", "kW", "e", (bytes) => power - (rInput + fInput));
        p.AddValue("Car efficiency", "%", "e", (bytes) => Math.Abs(mechPower + fMechPower) > Math.Abs(power) ? 100 : (mechPower + fMechPower) / power * 100.0);

        packets.Add(0x145, p = new Packet(0x145, this));
        p.AddValue("Fr torque estimate", "Nm", "f",
          (bytes) => ((bytes[0] + ((bytes[1] & 0xF) << 8)) - (512 * (bytes[1] & 0x8))) / 2);

        packets.Add(0x116, p = new Packet(0x116, this));
        p.AddValue("Rr torque estimate", "Nm", "",
          (bytes) => ((bytes[0] + ((bytes[1] & 0xF) << 8)) - (512 * (bytes[1] & 0x8))) / 2);
        p.AddValue("Speed", "km|h", "",
          (bytes) => speed = ((bytes[2] + ((bytes[3] & 0xF) << 8)) - 500) / 20.0 * miles_to_km);
        /*p.AddValue("Consumption", "wh|km", "p",
          (bytes) => power / speed * 1000,
          new int[] { 0x102 });*/

        packets.Add(0x382, p = new Packet(0x382, this));
        p.AddValue("Nominal full pack", "kWh", "br", (bytes) => nominalFullPackEnergy = (bytes[0] + ((bytes[1] & 0x03) << 8)) * 0.1);
        p.AddValue("Nominal remaining", "kWh", "br", (bytes) => nominalRemaining = ((bytes[1] >> 2) + ((bytes[2] & 0x0F) * 64)) * 0.1);
        p.AddValue("Expected remaining", "kWh", "r", (bytes) => ((bytes[2] >> 4) + ((bytes[3] & 0x3F) * 16)) * 0.1);
        p.AddValue("Ideal remaining", "kWh", "r", (bytes) => ((bytes[3] >> 6) + ((bytes[4] & 0xFF) * 4)) * 0.1);
        p.AddValue("To charge complete", "kWh", "", (bytes) => (bytes[5] + ((bytes[6] & 0x03) << 8)) * 0.1);
        p.AddValue("Energy buffer", "kWh", "br", (bytes) => buffer = ((bytes[6] >> 2) + ((bytes[7] & 0x03) * 64)) * 0.1);
        p.AddValue("SOC", "%", "br", (bytes) => soc = (nominalRemaining - buffer) / (nominalFullPackEnergy - buffer) * 100.0);
        p.AddValue("Usable full pack", "kWh", "br", (bytes) => (nominalFullPackEnergy - buffer));
        p.AddValue("Usable remaining", "kWh", "br", (bytes) => (nominalRemaining - buffer));

        packets.Add(0x302, p = new Packet(0x302, this));
        p.AddValue("SOC Min", "%", "br", (bytes) => (bytes[0] + ((bytes[1] & 0x3) << 8)) / 10.0);
        p.AddValue("SOC UI", "%", "br", (bytes) => ((bytes[1] >> 2) + ((bytes[2] & 0xF) << 6)) / 10.0);

        p.AddValue("DC Charge total", "kWH", "bs",
          (bytes) =>
          {
            if (bytes[2] >> 4 == 0)
            {
              dcChargeTotal =
                (bytes[4] +
                (bytes[5] << 8) +
                (bytes[6] << 16) +
                (bytes[7] << 24)) / 1000.0;
              /*if (mainActivity.currentTab.trip.dcChargeStart == 0)
              {
                mainActivity.currentTab.trip.dcChargeStart = dcChargeTotal;
              }
              dcCharge = dcChargeTotal - mainActivity.currentTab.trip.dcChargeStart;*/
              return dcChargeTotal;
            }
            else return (double?)null;
          });

        p.AddValue("AC Charge total", "kWH", "bs",
          (bytes) =>
          {
            if (bytes[2] >> 4 == 1)
            {
              acChargeTotal =
                (bytes[4] +
                (bytes[5] << 8) +
                (bytes[6] << 16) +
                (bytes[7] << 24)) / 1000.0;
              /*if (mainActivity.currentTab.trip.acChargeStart == 0)
              {
                mainActivity.currentTab.trip.acChargeStart = acChargeTotal;
              }
              acCharge = acChargeTotal - mainActivity.currentTab.trip.acChargeStart;*/
              return acChargeTotal;
            }
            else
            {
              return null;
            }
          });
        /*p.AddValue("DC Charge", "kWh", "ti",
          (bytes) => dcChargeTotal - mainActivity.currentTab.trip.dcChargeStart);
        p.AddValue("AC Charge", "kWh", "ti",
          (bytes) => acChargeTotal - mainActivity.currentTab.trip.acChargeStart);*/

        packets.Add(0x3D2, p = new Packet(0x3D2, this));
        p.AddValue("Charge total", "kWH", "bs",
          (bytes) =>
          {
            chargeTotal =
              (bytes[0] +
              (bytes[1] << 8) +
              (bytes[2] << 16) +
              (bytes[3] << 24)) / 1000.0;
            /*if (mainActivity.currentTab.trip.chargeStart == 0)
            {
              mainActivity.currentTab.trip.chargeStart = chargeTotal;
            }
            charge = chargeTotal - mainActivity.currentTab.trip.chargeStart;*/
            return chargeTotal;
          });

        const double charge = 0;
        const double discharge = 0;
        const double acCharge = 0;
        const double dcCharge = 0;

        p.AddValue("Discharge total", "kWH", "b",
          (bytes) =>
          {
            dischargeTotal =
              (bytes[4] +
              (bytes[5] << 8) +
              (bytes[6] << 16) +
              (bytes[7] << 24)) / 1000.0;
            /*if (mainActivity.currentTab.trip.dischargeStart == 0)
            {
              mainActivity.currentTab.trip.dischargeStart = dischargeTotal;
            }
            discharge = dischargeTotal - mainActivity.currentTab.trip.dischargeStart;*/
            return dischargeTotal;
          });
        p.AddValue("Regenerated", "kWh", "tr",
          (bytes) => regen = charge - acCharge - dcCharge);
        p.AddValue("Energy", "kWh", "tr",
          (bytes) => energy = discharge - regen);
        p.AddValue("Discharge", "kWh", "r",
          (bytes) => discharge);
        p.AddValue("Charge", "kWh", "r",
          (bytes) => charge);
        p.AddValue("Regen total", "kWH", "b",
          (bytes) => regenTotal = chargeTotal - acChargeTotal - dcChargeTotal,
          new int[] { 0x302 });
        p.AddValue("Regen %", "% ", "tr",
          (bytes) => energy > 0 ? regen / discharge * 100 : (double?)null);//,
                                                                           //new int[] { 0x302 });

        p.AddValue("Discharge cycles", "x", "b",
          (bytes) => nominalFullPackEnergy > 0 ? dischargeTotal / nominalFullPackEnergy : (double?)null,
          new int[] { 0x382 });
        p.AddValue("Charge cycles", "x", "b",
          (bytes) => nominalFullPackEnergy > 0 ? chargeTotal / nominalFullPackEnergy : (double?)null,
          new int[] { 0x382 });

        const double tripDistance = 0;

        packets.Add(0x562, p = new Packet(0x562, this));
        p.AddValue("Battery odometer", "Km", "b",
          (bytes) => odometer = (bytes[0] + (bytes[1] << 8) + (bytes[2] << 16) + (bytes[3] << 24)) / 1000.0 * miles_to_km);
        /*p.AddValue("Trip distance", "km", "tsr",
          (bytes) => {
            if (mainActivity.currentTab.trip.odometerStart == 0)
            {
              mainActivity.currentTab.trip.odometerStart = odometer;
            }
            return tripDistance = odometer - mainActivity.currentTab.trip.odometerStart;*
          });*/
        p.AddValue("Trip consumption", "wh|km", "tr",
          (bytes) => tripDistance > 0 ? energy / tripDistance * 1000 : (double?)null,
          new int[] { 0x3D2 });
        /*p.AddValue("Lifetime consumption", "wh/km", "bt",
          (bytes) => odometer > 0 ? dischargeTotal / odometer * 1000 : (double?)null);*/

        packets.Add(0x115, p = new Packet(0x115, this));
        p.AddValue("Fr motor RPM", "RPM", "",
          (bytes) => frpm = (bytes[4] + (bytes[5] << 8)) - (512 * (bytes[5] & 0x80)));
        // 0x115 --- DIS_motorRPM = (data[4] + (data[5]<<8)) - (512 * (data[5]&0x80));

        packets.Add(0x106, p = new Packet(0x106, this));
        p.AddValue("Rr motor RPM", "RPM", "",
          (bytes) => rrpm = (bytes[4] + (bytes[5] << 8)) - (512 * (bytes[5] & 0x80)));

        packets.Add(0x232, p = new Packet(0x232, this));
        p.AddValue("Max discharge power", "kW", "b", (bytes) => (bytes[2] + (bytes[3] << 8)) / 100.0);
        p.AddValue("Max regen power", "kW", "b", (bytes) => (bytes[0] + (bytes[1] << 8)) / 100.0);

        packets.Add(0x168, p = new Packet(0x168, this));
        p.AddValue("Brake pedal", "%", "i",
          (bytes) => (bytes[0] + (bytes[1] << 8)) - 3239);

        packets.Add(0x00E, p = new Packet(0x00E, this));
        p.AddValue("Steering angle", "deg", "i",
          (bytes) => (((bytes[0] << 8) + bytes[1] - 8200.0) / 10.0));

        packets.Add(0x338, p = new Packet(0x338, this));
        p.AddValue("Rated range", "km", "br",
          (bytes) => (bytes[0] + (bytes[1] << 8)) * miles_to_km);
        p.AddValue("Typical range", "km", "br",
          (bytes) => (bytes[2] + (bytes[3] << 8)) * miles_to_km);
        p.AddValue("Full rated range", "km", "br",
          (bytes) => (bytes[0] + (bytes[1] << 8)) * miles_to_km / (soc == 0.0 ? 100.0 : soc) * 100.0);
        p.AddValue("Full typical range", "km", "br",
          (bytes) => (bytes[2] + (bytes[3] << 8)) * miles_to_km / (soc == 0.0 ? 100.0 : soc) * 100.0);

        packets.Add(0x2A8, p = new Packet(0x2A8, this));
        p.AddValue("Front left", "WRPM", "e",
          (bytes) => fl = (bytes[4] + (bytes[3] << 8)) * 0.7371875 / 9.73);
        p.AddValue("Front right", "WRPM", "e",
          (bytes) => fr = (bytes[6] + (bytes[5] << 8)) * 0.7371875 / 9.73);
        p.AddValue("Front drive ratio", ":1", "e",
          (bytes) => fl + fr > 20 ? frpm / ((fl + fr) / 2) : (double?)null,
          new int[] { 0x115 });

        packets.Add(0x288, p = new Packet(0x288, this));
        p.AddValue("Rear left", "WRPM", "e",
          (bytes) => rl = (bytes[4] + (bytes[3] << 8)) * 0.7371875 / 9.73);
        p.AddValue("Rear right", "WRPM", "e",
          (bytes) => rr = (bytes[7] + (bytes[6] << 8)) * 0.7371875 / 9.73);
        p.AddValue("Rear drive ratio", ":1", "e",
          (bytes) => rl + rr > 20 ? rrpm / ((rl + rr) / 2) : (double?)null,
          new int[] { 0x106 });

        packets.Add(0x6F2, p = new Packet(0x6F2, this));
        p.AddValue("Last cell block updated", "xb", "", (bytes) =>
        {
          Int64 data = BitConverter.ToInt64(bytes, 0);
          if (bytes[0] < 24)
          {
            int cell = 0;
            for (int i = 0; i < 4; i++)
              UpdateItem("Cell " + (cell = ((bytes[0]) * 4 + i + 1)).ToString().PadLeft(2) + " voltage"
                , "zVC"
                , "z"
                , bytes[0]
                , ((data >> ((14 * i) + 8)) & 0x3FFF) * 0.000305
                , 0x6F2);
            if (cell > numCells)
              numCells = cell;
          }
          else
            for (int i = 0; i < 4; i++)
              UpdateItem("Cell " + ((bytes[0] - 24) * 4 + i + 1).ToString().PadLeft(2) + " temp"
                , "zCC"
                , "c"
                , bytes[0]
                , ((Int16)(((data >> ((14 * i) + 6)) & 0xFFFC)) * 0.0122 / 4.0)
                , 0x6F2);
          return bytes[0];
        });

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
        {
          p.AddValue("Cell " + i.ToString().PadLeft(2) + " voltage"
            , "zVC"
            , "z", null);
        }
        for (int i = 1; i <= 32; i++)
        {
          p.AddValue("Cell " + i.ToString().PadLeft(2) + " temp"
            , "zCC"
            , "c"
            , null);
        }

        packets.Add(0x754, p = new Packet(0x754, this));
        p.AddValue("Last 51E block updated", "xb", "", (bytes) =>
        {
          Int64 data = BitConverter.ToInt64(bytes, 0);
          //int cell = 0;
          /*for (int i = 0; i < 4; i++)
          {
            UpdateItem("Cell " + (cell = ((bytes[0]) * 4 + i + 1)).ToString().PadLeft(2) + " voltage"
              , "zVC"
              , "z"
              , bytes[0]
              , ((data >> ((14 * i) + 8)) & 0x3FFF) * 0.000305
              , p.id);
          }
          if (cell > numCells)
          {
            numCells = cell;
          }
        } else*/
          //if ((bytes[0]) == 0)
          for (int i = 0; i < 8; i += 2)
          {
            UpdateItem("754 Block " + (bytes[0]) + ":" + i.ToString().PadLeft(2)
              , "zzz"
              , "c"
              , bytes[0]
              , bytes[i] + (bytes[i + 1] << 8)
              , 0x754);
          }
          return bytes[0];
        });

        packets.Add(0x125, p = new Packet(0x125, this));
        p.AddValue("125 0", "km", "br",
          (bytes) => (/*bytes[2]/256.0 + */ (bytes[0] << 8) - (512 * (bytes[0] & 0x80))));
        p.AddValue("125 1", "km", "br",
          (bytes) => (/*bytes[2]/256.0 + */ (bytes[1] << 8) - (512 * (bytes[1] & 0x80))));

        p.AddValue("125 2", "km", "br",
         (bytes) => (/*bytes[2]/256.0 + */ ((bytes[2] & 0xF0) << 4) /*- (512 * (bytes[2] & 0x80)))*/));

        //p.AddValue("125 2", "km", "br",
        //  (bytes) => (bytes[1] + (bytes[0] << 8))-((bytes[0]<<8)& 0x80));
        //  (bytes) => (bytes[2]));
        //p.AddValue("125 3", "km", "br",
        //  (bytes) => (bytes[1] + (bytes[0] << 8))-((bytes[0]<<8)& 0x80));
        //  (bytes) => (bytes[3]));
        //p.AddValue("125 1", "km", "br",
        //  (bytes) => (bytes[2] + (bytes[3] << 8)));

        packets.Add(0x126, p = new Packet(0x126, this));
        p.AddValue("126 Stator current", " A", "b", (bytes) => amp =
          ((Int16)((((bytes[3] & 0x7F) << 8) + bytes[2]) << 1)) / 2.0);
        p.AddValue("126 Battery Voltage", "V", "br",
          (bytes) => (bytes[0] + (bytes[1] << 8) - (512 * (bytes[1] & 0x80))) / 2.0);
        p.AddValue("126 4", "km", "br",
          (bytes) => (bytes[4]));
        p.AddValue("126 5", "km", "br",
          (bytes) => (bytes[5]));
        p.AddValue("126 6", "km", "br",
          (bytes) => (bytes[6]));
        p.AddValue("126 7", "km", "br",
          (bytes) => (bytes[7]));

        packets.Add(0x1F8, p = new Packet(0x1F8, this));
        p.AddValue("1F8 0-1", "km", "br", (bytes) =>
          (((bytes[0] << 8) + bytes[1] - 500)));
        //  (bytes) => (bytes[1] + (bytes[0] << 8))/* - (512 * (bytes[1] & 0x80))*/);
        /*p.AddValue("1F8 2", "km", "br",
          (bytes) => (bytes[2]));*/
        /* p.AddValue("1F8 3", "km", "br",
          (bytes) => (bytes[3]));*/
        p.AddValue("1F8 4", "km", "br",
          (bytes) => ((bytes[4] + ((bytes[5] & 0xF) << 8)) - 2000));
        /*p.AddValue("1F8 5", "km", "br",
          (bytes) => (bytes[5]));*/
        /*p.AddValue("1F8 6", "km", "br",
          (bytes) => (bytes[6]));
        p.AddValue("1F8 7", "km", "br",
          (bytes) => (bytes[7]));*/

        packets.Add(0x2AA, p = new Packet(0x2AA, this));
        p.AddValue("HVAC feet", "km", "br",
          (bytes) =>
          {
            var set1 = bytes[2] & 0x07;
            feet = false;
            seat = false;
            win = false;
            switch (set1)
            {
              case 1:
                seat = true;
                break;
              case 2:
                feet = true;
                seat = true;
                break;
              case 3:
                feet = true;
                break;
              case 4:
                feet = true;
                win = true;
                break;
              case 5:
                win = true;
                break;
              case 6:
                feet = true;
                seat = true;
                win = true;
                break;
              case 7:
                seat = true;
                win = true;
                break;
            }
            return feet ? 1 : 0;
          });
        p.AddValue("HVAC seat", "km", "br",
          (bytes) => seat ? 1 : 0);
        p.AddValue("HVAC window", "km", "br",
          (bytes) => win ? 1 : 0);

        p.AddValue("HVAC recycle", "km", "br",
          (bytes) =>
          {
            return (bytes[3] & 0x10) >> 4;
          });

        // 0xDEADBEEF

        p.AddValue("HVAC recycle2", "0", "eh",
          (bytes) =>
          {
            return (bytes[3] & 0x8) >> 3;
          });

        p.AddValue("HVAC A/C", "km", "br",
          (bytes) =>
          {
            var set3 = bytes[4] & 0x01;
            return set3;
          });
        p.AddValue("HVAC on/off", "km", "br",
          (bytes) =>
          (bytes[3] & 0x10) >> 4 == 0 ? 1 : 0);

        p.AddValue("HVAC fan speed", "km", "br",
          (bytes) => (bytes[2] & 0xf0) >> 4);

        p.AddValue("HVAC Temp1", "km", "br",
          (bytes) => bytes[0] / 2);
        p.AddValue("HVAC Temp2", "km", "br",
          (bytes) => bytes[1] / 2);

        /*p.AddValue("Last 754 block updated", "xb", "", (bytes) => {
          Int64 data = BitConverter.ToInt64(bytes, 0);
          int cell = 0;
          /*for (int i = 0; i < 4; i++)
          {
            UpdateItem("Cell " + (cell = ((bytes[0]) * 4 + i + 1)).ToString().PadLeft(2) + " voltage"
              , "zVC"
              , "z"
              , bytes[0]
              , ((data >> ((14 * i) + 8)) & 0x3FFF) * 0.000305
              , p.id);
          }
          if (cell > numCells)
          {
            numCells = cell;
          }
        } else*/
        /*  for (int i = 0; i < 7; i+=2)
          UpdateItem("754 Block " + (bytes[0]) + ":" + i.ToString().PadLeft(2)
            , "zzZ"
            , "c"
            , bytes[0]
            , bytes[i] + (bytes[i + 1] << 8)
            , 0x754);
          return bytes[0];
        });*/

        packets.Add(0x51E, p = new Packet(0x51E, this));
        p.AddValue("Last 51E block updated", "xb", "", (bytes) =>
        {
          Int64 data = BitConverter.ToInt64(bytes, 0);
          //int cell = 0;
          /*for (int i = 0; i < 4; i++)
          {
            UpdateItem("Cell " + (cell = ((bytes[0]) * 4 + i + 1)).ToString().PadLeft(2) + " voltage"
              , "zVC"
              , "z"
              , bytes[0]
              , ((data >> ((14 * i) + 8)) & 0x3FFF) * 0.000305
              , p.id);
          }
          if (cell > numCells)
          {
            numCells = cell;
          }
        } else*/
          if ((bytes[0] & 0xF0 >> 4) == 1)
          {
            for (int i = 0; i < 8; i += 2)
            {
              UpdateItem("51E Block " + (bytes[0] & 0xF0 >> 4) + ":" + i.ToString().PadLeft(2)
                , "zzz"
                , "c"
                , bytes[0]
                , bytes[i] + (bytes[i + 1] << 8)
                , 0x51E);
            }
          }
          return bytes[0] & 0xF0 >> 4;
        });

        packets.Add(0x222, p = new Packet(0x222, this));
        p.AddValue("Charge rate", "??", "br",
          (bytes) => (bytes[0] + (bytes[1] << 8)) / 100.0);
        p.AddValue("Charger volt", "V", "br",
          (bytes) => (bytes[2] + (bytes[3] << 8)) / 100.0);

        packets.Add(0x262, p = new Packet(0x262, this));

        p.AddValue("DC Charge amps1", "??", "br",
          (bytes) => ((Int16)((((bytes[3] & 0x7F) << 8) + bytes[2]) << 1)) / 20.0);
        //(bytes) =>  ((Int16)((((bytes[3]) << 8) + bytes[2]))) / 20.0);
        p.AddValue("DC Charge amps2", "??", "br",
          (bytes) => ((Int16)((((bytes[5] & 0x7F) << 8) + bytes[4]) << 1)) / 20.0);
        //(bytes) => ((Int16)((((bytes[5]) << 8) + bytes[4]))) / 20.0);
        p.AddValue("DC Charge amps3", "??", "br",
          (bytes) => ((Int16)((((bytes[7] & 0x7F) << 8) + bytes[6]) << 1)) / 20.0);
        //(bytes) => ((Int16)((((bytes[7]) << 8) + bytes[6]))) / 20.0);
        //(bytes) => ((Int16)((((bytes[7] & 0x7F) << 8) + bytes[6]) << 1)) / 50.0);
        //(bytes) => (bytes[6] + (bytes[7] << 8)) / 100.0);
        p.AddValue("Charge port volt", "V", "br",
          (bytes) => (bytes[0] + (bytes[1] << 8)) / 13.65);

        packets.Add(0x258, p = new Packet(0x258, this));
        p.AddValue("258 byte 7", "C", "c", (bytes) => bytes[7]);

        packets.Add(0x31A, p = new Packet(0x31A, this));
        p.AddValue("Battery inlet", "C", "e",
          (bytes) => (bytes[0] + ((bytes[1] & 0x03) << 8) - 320) / 8.0);
        //  (bytes) => (bytes[0] *0.4 ));
        //  (bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 8)));

        p.AddValue("PT inlet", "C", "e",
          (bytes) => (bytes[4] + ((bytes[5] & 0x03) << 8) - 320) / 8.0);
        //(bytes) => (bytes[4] *0.4 ));
        //31A - temperaturer. 0, 4:  F / 10->C

        /*p.AddValue("Battery bytes 2+3", "C", "e",
          (bytes) => (bytes[2] + ((bytes[3] & 0x03) << 8) - 320) / 8.0);
        p.AddValue("Battery bytes 6+7", "C", "e",
          (bytes) => (bytes[6] + ((bytes[7] & 0x03) << 8) - 320) / 8.0);*/

        /*p.AddValue("Battery 2+3", "C", "e",
          (bytes) => (bytes[2] + ((bytes[3] & 0x03) << 8)) / 8.0 - 40);
          //(bytes) => (bytes[0] *0.4 ));
          //(bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 8)));

        p.AddValue("DU inlet 6+7", "C", "e",
          (bytes) => (bytes[6] + ((bytes[7] & 0x03) << 8)) / 8.0 - 40);*/

        packets.Add(0x26A, p = new Packet(0x26A, this));
        p.AddValue("THC_batteryHeaterTemp", "C", "e",
          (bytes) => ((bytes[0] + ((bytes[1] & 0x7) << 8)) * 0.125) - 40);
        p.AddValue("Coolant heater exit", "C", "e",
          (bytes) => (bytes[0] + ((bytes[1] & 0x03) << 8) - 320) / 8.0);
        p.AddValue("THC_batteryHeaterReq", "b", "e",
          (bytes) => (bytes[1] & 0x8) >> 3);
        p.AddValue("THC_batteryHeaterState", "b", "e",
          (bytes) => (bytes[2] & 0x70) >> 4);
        //(bytes) => (bytes[0] *0.4 ));
        //(bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 8)));
        /*
         * #THC human decode
          THC_batteryHeaterState = "Undefined"
          heater_state = []
          heater_state.append("Off")
          heater_state.append("Startup")
          heater_state.append("BAT_IN_HEAT_CK")
          heater_state.append("Run")
          heater_state.append("Overtemp")
          heater_state.append("Suspended")
          heater_state.append("Undefined")
          heater_state.append("Undefined")

        THC (thermal controller) found at 
        https://github.com/apach3guy/CAN3/blob/master/thc.py
        */

        packets.Add(0x318, p = new Packet(0x318, this));
        p.AddValue("Outside temp", " C", "e",
          (bytes) => (bytes[0] / 2.0 - 40));
        p.AddValue("Outside temp filtered", " C", "e",
          (bytes) => (bytes[1] / 2.0 - 40));
        p.AddValue("Inside temp", " C", "e",
          (bytes) => (bytes[2] / 2.0 - 40));
        p.AddValue("A/C air temp", " C", "e",
          (bytes) => (bytes[4] / 2.0 - 40));
        //318 - temperaturer. 0, 1, 2, 4:  / 2 - 40 = C

        packets.Add(0x3F8, p = new Packet(0x3F8, this));
        p.AddValue("Floor vent L", " C", "e",
          (bytes) => ((bytes[4] + (bytes[5] << 8)) / 10.0) - 40);
        p.AddValue("Floor vent R", " C", "e",
          (bytes) => ((bytes[6] + (bytes[7] << 8)) / 10.0) - 40);
        p.AddValue("Mid vent L", " C", "e",
          (bytes) => ((bytes[0] + (bytes[1] << 8)) / 10.0) - 40);
        p.AddValue("Mid vent R", " C", "e",
          (bytes) => ((bytes[2] + (bytes[3] << 8)) / 10.0) - 40);
        //3F8 - as int. tror dette er 4 tempavlesninger evt innblåstemperatur, F / 10->C

        packets.Add(0x388, p = new Packet(0x388, this));
        p.AddValue("Heater L", " C", "h",
          (bytes) => (bytes[1] - 40));
        p.AddValue("Heater R", " C", "h",
          (bytes) => (bytes[0] - 40));
        p.AddValue("Temp 1", " C", "h",
          (bytes) => (bytes[2] - 40));
        p.AddValue("Temp 2", " C", "h",
          (bytes) => (bytes[3] - 40));
        p.AddValue("Temp 3", " C", "h",
          (bytes) => (bytes[4] - 40));
        p.AddValue("Temp 4", " C", "h",
          (bytes) => (bytes[5] - 40));

        //packets.Add(0x388, p = new Packet(0x388, this));
        /*p.AddValue("Floor L-40", " C", "h",
          (bytes) => (bytes[1]  -40));
        p.AddValue("Floor R-40", " C", "h",
          (bytes) => (bytes[0]  -40));*/
        /*p.AddValue("Temp 1-40", " C", "h",
          (bytes) => (bytes[2] -40));
        p.AddValue("Temp 2-40", " C", "h",
          (bytes) => (bytes[3] -40));
        p.AddValue("Temp 3-40", " C", "h",
          (bytes) => (bytes[4]  -40));
        p.AddValue("Temp 4-40", " C", "h",
          (bytes) => (bytes[5]  -40));*/
        //388 - temperaturer!0 - 1: / 4 = C, 2,3,4,5: / 2 - 40 = C
        /*p.AddValue("Floor L/4", " C", "h",
          (bytes) => (bytes[1] / 4));
        p.AddValue("Floor R/4", " C", "h",
          (bytes) => (bytes[0] / 4));
        p.AddValue("Temp 1/4", " C", "h",
          (bytes) => (bytes[2] / 4));
        p.AddValue("Temp 2/4", " C", "h",
          (bytes) => (bytes[3] / 4));
        p.AddValue("Temp 3/4", " C", "h",
          (bytes) => (bytes[4] / 4));
        p.AddValue("Temp 4/4", " C", "h",
          (bytes) => (bytes[5] / 4));*/

        packets.Add(0x308, p = new Packet(0x308, this));
        p.AddValue("Louver 1", "b", "e",
          (bytes) => bytes[0] > 0 ? ((bytes[0] - 15.0) / 219.0) * 100.0 : (double?)null);
        p.AddValue("Louver 2", "b", "e",
          (bytes) => bytes[1] > 0 ? ((bytes[1] - 15.0) / 219.0) * 100.0 : (double?)null);
        p.AddValue("Louver 3", "b", "e",
          (bytes) => bytes[2] > 0 ? ((bytes[2] - 15.0) / 219.0) * 100.0 : (double?)null);
        p.AddValue("Louver 4", "b", "e",
          (bytes) => bytes[3] > 0 ? ((bytes[3] - 15.0) / 219.0) * 100.0 : (double?)null);
        p.AddValue("Louver 5", "b", "e",
          (bytes) => bytes[4] > 0 ? ((bytes[4] - 15.0) / 219.0) * 100.0 : (double?)null);
        p.AddValue("Louver 6", "b", "e",
          (bytes) => bytes[5] > 0 ? ((bytes[5] - 15.0) / 219.0) * 100.0 : (double?)null);
        p.AddValue("Louver 7", "b", "e",
          (bytes) => bytes[6] > 0 ? ((bytes[6] - 15.0) / 219.0) * 100.0 : (double?)null);
        p.AddValue("Louver 8", "b", "e",
          (bytes) => bytes[7] > 0 ? ((bytes[7] - 15.0) / 219.0) * 100.0 : (double?)null);
        //388 - temperaturer!0 - 1: / 4 = C, 2,3,4,5: / 2 - 40 = C

        packets.Add(0x32A, p = new Packet(0x32A, this));
        p.AddValue("Battery pump 1", "%", "bc",
          (bytes) => (bytes[0]) & 0x7F);
        p.AddValue("Battery pump 2", "%", "bc",
          (bytes) => (bytes[1]));
        p.AddValue("Powertrain pump", "%", "bc",
          (bytes) => (bytes[2]));
        p.AddValue("Powertrain pump 2", "%", "bc",
          (bytes) => (bytes[7] > 0 ? bytes[7] : (double?)null));
        p.AddValue("Radiator bypass", "%", "bc",
          (bytes) => (bytes[3]));
        p.AddValue("Chiller bypass", "%", "bc",
          (bytes) => (bytes[4]));
        p.AddValue("Coolant heater", "%", "bch",
          (bytes) => (bytes[5]));
        p.AddValue("PTC air heater", "%", "bch",
          (bytes) => (bytes[6]));

        packets.Add(0x33A, p = new Packet(0x33A, this));
        p.AddValue("Refrigerant temp", "C", "e",
          (bytes) => (bytes[0] + ((bytes[1] & 0x07) << 8) - 320) / 8.0);

        packets.Add(0x3AA, p = new Packet(0x3AA, this));
        p.AddValue("Series/Parallel", "%", "e",
          (bytes) => (bytes[0] & 0x80) == 0x80 ? 0 : 100);

        /*p.AddValue("33A 12 bit 0", "b", "br",
          (bytes) => (bytes[0] + ((bytes[1] & 0x0F) << 8)));
        p.AddValue("33A 12 bit 1", "b", "br",
          (bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 4)));
        p.AddValue("33A 12 bit 3", "b", "br",
          (bytes) => (bytes[3] + ((bytes[4] & 0x0F) << 8)));
        p.AddValue("33A 12 bit 4", "b", "br",
          (bytes) => (((bytes[4] & 0xF0) >> 4) + ((bytes[5]) << 4)));
        p.AddValue("33A 12 bit 5", "b", "br",
          (bytes) => (bytes[6] + ((bytes[7] & 0x0F) << 8)));*/

        packets.Add(0x35A, p = new Packet(0x35A, this));
        /*p.AddValue("35A 12 bit 0", "b", "br",
          (bytes) => (bytes[0] + ((bytes[1] & 0x0F) << 8)));
        p.AddValue("35A 12 bit 1", "b", "br",
          (bytes) => (((bytes[1] & 0xF0) >> 4) + ((bytes[2]) << 4)));
        p.AddValue("35A 12 bit 3", "b", "br",
          (bytes) => (bytes[3] + ((bytes[4] & 0x0F) << 8)));
        p.AddValue("35A 12 bit 4", "b", "br",
          (bytes) => (((bytes[4] & 0xF0) >> 4) + ((bytes[5]) << 4)));
        p.AddValue("35A 12 bit 5", "b", "br",
          (bytes) => (bytes[6] + ((bytes[7] & 0x0F) << 8)));*/

        p.AddValue("THC_totalPowerConsumedHV", "b", "br",
          (bytes) => (bytes[2] + (bytes[3] << 8)));
        p.AddValue("THC_totalPowerConsumed12V", "b", "br",
          (bytes) => (bytes[4] + ((bytes[5] & 0xF) << 8)));
        p.AddValue("THC_HVPowerLimit", "b", "br",
          (bytes) => (bytes[6] + (bytes[7] << 8)) / 100);
        p.AddValue("THC_limitedBatteryHeater", "b", "br",
          (bytes) => (bytes[5] & 0x10) >> 4);
        p.AddValue("THC_limitedCompressor", "b", "br",
          (bytes) => (bytes[5] & 0x20) >> 5);
        p.AddValue("THC_limitedPtcHeater", "b", "br",
          (bytes) => (bytes[5] & 0x40) >> 6);
      }

      packets.Add(0x4, p = new Packet(0x4, this));
      p.AddValue("Nibble 00", "b", "br",
        (bytes) => (bytes[0] & 0x0F));
      p.AddValue("Nibble 01", "b", "br",
        (bytes) => (bytes[0] & 0xF0) >> 4);
      p.AddValue("Nibble 10", "b", "br",
        (bytes) => (bytes[1] & 0x0F));
      p.AddValue("Nibble 11", "b", "br",
        (bytes) => (bytes[1] & 0xF0) >> 4);
      p.AddValue("Nibble 20", "b", "br",
        (bytes) => (bytes[2] & 0x0F));
      p.AddValue("Nibble 21", "b", "br",
        (bytes) => (bytes[2] & 0xF0) >> 4);
      p.AddValue("Nibble 30", "b", "br",
        (bytes) => (bytes[3] & 0x0F));
      p.AddValue("Nibble 31", "b", "br",
        (bytes) => (bytes[3] & 0xF0) >> 4);
      p.AddValue("Nibble 40", "b", "br",
        (bytes) => (bytes[4] & 0x0F));
      p.AddValue("Nibble 41", "b", "br",
        (bytes) => (bytes[4] & 0xF0) >> 4);
      p.AddValue("Nibble 50", "b", "br",
        (bytes) => (bytes[5] & 0x0F));
      p.AddValue("Nibble 51", "b", "br",
        (bytes) => (bytes[5] & 0xF0) >> 4);
      p.AddValue("Nibble 60", "b", "br",
        (bytes) => (bytes[6] & 0x0F));
      p.AddValue("Nibble 61", "b", "br",
        (bytes) => (bytes[6] & 0xF0) >> 4);
      p.AddValue("Nibble 70", "b", "br",
        (bytes) => (bytes[7] & 0x0F));
      p.AddValue("Nibble 71", "b", "br",
        (bytes) => (bytes[7] & 0xF0) >> 4);

      //p.AddValue("268 Int 2-3", "C", "c",
      //  (bytes) => ((bytes[2]) + ((bytes[3] & 0xF0) << 8)) /*- (512 * (bytes[3] & 0x80))*/);

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
        if (id == 0x6F2)
        {
          if (bytes[0] >= 24)
          {
            var values = items.Where(x => x.Value.unit == "zCC");
            double min = values.Min(x => x.Value.GetValue(false));
            double max = values.Max(x => x.Value.GetValue(false));
            double avg = values.Average(x => x.Value.GetValue(false));
            UpdateItem("Cell temp min", "c", "bcz", 0, min, 0x6F2);
            UpdateItem("Cell temp avg", "c", "bcpz", 1, avg, 0x6F2);
            UpdateItem("Cell temp max", "c", "bcz", 2, max, 0x6F2);
            UpdateItem("Cell temp diff", "Cd", "bcz", 3, max - min, 0x6F2);
          }
          else
          {
            var values = items.Where(x => x.Value.unit == "zVC");
            double min = values.Min(x => x.Value.GetValue(false));
            double max = values.Max(x => x.Value.GetValue(false));
            double avg = values.Average(x => x.Value.GetValue(false));
            UpdateItem("Cell min", "Vc", "bz", 0, min, 0x6F2);
            UpdateItem("Cell avg", "Vc", "bpz", 1, avg, 0x6F2);
            UpdateItem("Cell max", "Vc", "bz", 2, max, 0x6F2);
            UpdateItem("Cell diff", "Vcd", "bz", 3, max - min, 0x6F2);
          }
        }
        /*if (time < SystemClock.ElapsedRealtime())
        {
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
