using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;


namespace TeslaSCAN {

  public class Trip {
    public double odometerStart;
    public double chargeStart;
    public double dischargeStart;
    public double acChargeStart;
    public double dcChargeStart;
    public double driveStart;
    public double regenStart;

    public Trip(bool totals) {
      if (totals) {
        odometerStart = 0.000001;
        chargeStart = 0.000001;
        dischargeStart = 0.000001;
        acChargeStart = 0.000001;
        dcChargeStart = 0.000001;
        driveStart = 0.000001;
        regenStart = 0.000001;
      }
    }
  }
}