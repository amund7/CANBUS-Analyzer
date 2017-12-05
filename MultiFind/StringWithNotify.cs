using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeslaSCAN;

namespace MultiFind {
  public class StringWithNotify : INotifyPropertyChanged {

    public StringWithNotify(int pid, string s, Parser p) {
      _str = s;
      _pid = pid;
      parser = p;
      parser.packets.TryGetValue(pid, out packet);
    }

    private string _str;
    public string Str {
      get { return _str; }
      set {
        if (value != _str) {
          _str = value;
          NotifyPropertyChanged("Str");
          NotifyPropertyChanged("Length");
          NotifyPropertyChanged("Payload");
        }
      }
    }

    public string Packet {
      get { return _str.Substring(0, 3); }
    }

    public string Payload {
      get { return _str.Substring(4,_str.Length-4); }
    }

    private bool _used;
    public bool Used {
      get { return _used; }
      set {
        if (value != _used) {
          _used = value;
          NotifyPropertyChanged("Used");
        }
      }
    }

    private int _pid;
    private Parser parser;
    private Packet packet;

    public int Pid {
      get { return _pid; }
      set {
        if (value != _pid) {
          _pid = value;
          NotifyPropertyChanged("Pid");
        }
      }
    }

    private int _count;
    public int Count {
      get { return _count; }
      set {
        if (value != _count) {
          _count = value;
          NotifyPropertyChanged("Count");
        }
      }
    }

    private int _history;
    public int History {
      get { return _history; }
      set {
        if (value != _history) {
          _history = value;
          NotifyPropertyChanged("History");
        }
      }
    }

    public int Length {
      get { return _str.Length; }
    }

    public string Description {
      get {
        string s = "";
        foreach (var v in packet.values)
          s += v.name + " ";
        return s;
      }
    }

    private bool _stay;
    public bool Stay {
      get { return _stay; }
      set { _stay = value; }
    }
    

    public int[] colors = new int[64];

    public event PropertyChangedEventHandler PropertyChanged;

    public override string ToString() {
      return _str;
    }

    private void NotifyPropertyChanged(String propertyName = "") {
      if (PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }

  }
}
