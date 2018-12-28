﻿using System;
using System.ComponentModel;
using TeslaSCAN;

namespace CANBUS
{
  public class StringWithNotify : INotifyPropertyChanged
  {
    public StringWithNotify(uint pid, string s, Parser p, MainWindow mainwindow)
    {
      _pid = pid;
      _str = s;
      parser = p;
      parser.packets.TryGetValue(pid, out packet);
      mainWindow = mainwindow;
    }

    private string _str;
    public string Str
    {
      get
      {
        return _str;
      }
      set
      {
        if (value != _str)
        {
          _str = value;
          NotifyPropertyChanged("Str");
          NotifyPropertyChanged("Length");
          NotifyPropertyChanged("Payload");
        }
      }
    }

    public string Packet
    {
      get
      {
        return _str.Substring(0, 3);
      }
    }

    public string Payload
    {
      get
      {
        return _str.Substring(4, _str.Length - 4);
      }
    }

    private bool _used;
    public bool Used
    {
      get
      {
        return _used;
      }
      set
      {
        if (value != _used)
        {
          _used = value;
          NotifyPropertyChanged("Used");
        }
      }
    }

    private uint _pid;
    private Parser parser;
    private Packet packet;

    public uint Pid
    {
      get
      {
        return _pid;
      }
      set
      {
        if (value != _pid)
        {
          _pid = value;
          NotifyPropertyChanged("Pid");
        }
      }
    }

    private int _count;
    public int Count
    {
      get
      {
        return _count;
      }
      set
      {
        if (value != _count)
        {
          _count = value;
          NotifyPropertyChanged("Count");
        }
      }
    }

    private int _history;
    public int History
    {
      get
      {
        return _history;
      }
      set
      {
        if (value != _history)
        {
          _history = value;
          NotifyPropertyChanged("History");
        }
      }
    }

    public int Length
    {
      get
      {
        return _str.Length;
      }
    }

    public string Description
    {
      get
      {
        string s = "";
        if (packet != null)
        {
          foreach (var v in packet.values)
          {
            s += v.name + " ";
          }
        }

        return s;
      }
    }

    public string Verbose
    {
      get
      {
        return _values;
      }
      set
      {
        if (value != _values)
        {
          _values = value;
          NotifyPropertyChanged("Verbose");
        }
      }
    }
    public bool Stay { get; set; }

    public int[] colors = new int[64];
    private string _values;
    private MainWindow mainWindow;

    public event PropertyChangedEventHandler PropertyChanged;

    public override string ToString()
    {
      return _str;
    }

    private void NotifyPropertyChanged(String propertyName = "")
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
