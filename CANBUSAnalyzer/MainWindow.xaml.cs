using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using TeslaSCAN;
using Xamarin.Forms.Dynamic;

namespace CANBUS {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {

    //ConcurrentQueue<Hits> hits = new ConcurrentQueue<Hits>();
    bool run = false;
    //private Parser parser;
    //ConcurrentDictionary<int, string> runningTasks = new ConcurrentDictionary<int, string>();
    //SortedList<int, string> runningTasks = new SortedList<int, string>();
    ObservableCollection<StringWithNotify> runningTasks = new ObservableCollection<StringWithNotify>();
    private Timer timer;
    private Stopwatch stopwatch;
    private StreamReader inputStream;
    private Parser parser;
    private bool interpret_as;
    private int interpret_source;
    private int packet;
    private int UpdateCount;
    private long prevUpdate;
    private long prevBitsUpdate;

    BindableTwoDArray<char> MyBindableTwoDArray { get; set; }

    public MainWindow() {
      InitializeComponent();
      MyBindableTwoDArray = new BindableTwoDArray<char>(8, 8);
      PathList.ItemsSource = runningTasks;
      parser = new Parser();
      HitsDataGrid.ItemsSource = parser.items;
      //HitsDataGrid.DataContext = parser.items;
      stopwatch = new Stopwatch();
      stopwatch.Start();
      //AnalyzeResults.ItemsSource = parser.items.Values;

      Graph.Axes.Add(new OxyPlot.Wpf.TimeSpanAxis());
      Graph.Axes[0].Position = AxisPosition.Bottom;
      var linearAxis = new OxyPlot.Wpf.LinearAxis();
      linearAxis.Position = AxisPosition.Left;
      Graph.Axes.Add(linearAxis);

      Analyze_Packets_Click(null, null);

    }



    private void Load_Button_Click(object sender, RoutedEventArgs e) {
      run = false;
      OpenFileDialog openFileDialog1 = new OpenFileDialog();
      openFileDialog1.Filter = "txt|*.txt";
      if ((bool)openFileDialog1.ShowDialog())
        if (openFileDialog1.FileName != null) {

          inputStream = File.OpenText(openFileDialog1.FileName);

          Title = openFileDialog1.FileName;

          //runningTasks.Clear();
          timer?.Dispose();

          foreach (var v in parser.items.Values)
            if (v.Points == null)
              v.Points = new List<DataPoint>();
            else
              v.Points.Clear();

          //timer = new Timer(timerCallback, null, 10, 1);
          run = true;
          Thread thread = new Thread(loop);
          thread.Start();
        }
    }

    void loop() {
      while (run)
        timerCallback(null);
    }

    private void timerCallback(object state) {
      try {
        string line;
        if (state is string)
          line = state as string;
        else
          line = inputStream.ReadLine();
        if (inputStream.EndOfStream)
          run = false;
        if (line == null)
          return;
        parser.Parse(line + "\n", 0);
        string s = line;
        for (int i = 3; i < s.Length; i += 3)
          s = s.Insert(i, " ");
        string p = "";
        if (s.Length > 3)
          p = s.Substring(0, 3);
        int pac;

        if (s.StartsWith("508"))
          Dispatcher.Invoke(() => {
            StringBuilder vin = new StringBuilder(KeywordTextBox.Text);
            if (vin.Length < 17)
              vin = new StringBuilder("VIN                              ");
            int temp, idx;
            int.TryParse(s.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out idx);
            for (int i = 2; i < s.Length - 2; i += 2)
              if (int.TryParse(s.Substring(i, 2), System.Globalization.NumberStyles.HexNumber, null, out temp))
                if (temp != 0)
                  vin[idx * 6 + (i)] = (char)temp;

            KeywordTextBox.Text = vin.ToString();
          });

        if (int.TryParse(p, System.Globalization.NumberStyles.HexNumber, null, out pac)) {
          var l = runningTasks.Where(x => x.Str.StartsWith(p)).FirstOrDefault();
          if (l == null)
            Dispatcher.Invoke(() =>
            runningTasks.Add(new StringWithNotify(pac, s, parser)));
          else
            l.Str = s;

          if (l != null) {
            l.Used = parser.items.Any(x => x.Value.packetId == pac);
            l.Count++;
          }

          if (pac == packet)
            if (prevBitsUpdate < stopwatch.ElapsedMilliseconds) {
              Dispatcher.Invoke(() => {
                updateBits((StringWithNotify)PathList.SelectedItem, s);
              });
              prevBitsUpdate = stopwatch.ElapsedMilliseconds + 40;
            }

              if (prevUpdate < stopwatch.ElapsedMilliseconds) {
            Dispatcher.Invoke(() => {
              Graph.InvalidatePlot(true);
            });
            prevUpdate = stopwatch.ElapsedMilliseconds + 1000;
          }

        }
      }
      catch (Exception e) { Console.WriteLine(e.Message); }
    }

    void updateBits(StringWithNotify sel, string s) {
      if (sel == null)
        return;
      string bits = "";
      string rawbits = "";
      int temp;

      for (int i = 2; i < s.Length - 2; i += 2)
        if (int.TryParse(s.Substring(i, 3), System.Globalization.NumberStyles.HexNumber, null, out temp)) {
          bits += Convert.ToString(temp, 2).PadLeft(8, '0')/* + " " + Convert.ToString(temp, 16).PadLeft(2, '0').ToUpper() + " " + (char)temp /*+ " " + temp*/ + "\n";
          rawbits += Convert.ToString(temp, 2).PadLeft(8, '0');
        }
      /*for (int i = 0; i < bits.Length; i += 2)
        bits = bits.Insert(i, " ");*/

      for (int j = 0; j <= rawbits.Length - 16; j += 8) {
        var inside = double.Parse(rawbits.Substring(j, 8)) * .25;
        var outside = double.Parse(rawbits.Substring(j + 8, 8)) * 0.5 - 40.0;
        if (inside < 30 && inside > 10 && outside > -5 && outside < 20) {
          bits += "\n" + inside;
          bits += "\n" + outside;
        }
      }

      BitBox.Inlines.Clear();

      var bc = new BrushConverter();
      int index = 0;
      for (int i = 0; i < 64; i++) {

        if (index >= bits.Length)
          break;

        if (bits[index] == '0' || bits[index] == '1') {
          BitBox.Inlines.Add(
            new Run(
              bits[index].ToString()) {
              Background = sel.colors[i] != 0 ?
            (Brush)bc.ConvertFrom
            ("#" + Convert.ToString((sel.colors[i]*8), 16).PadRight(6, 'C')) :
            Brushes.White
            });
          index++;
        }


        while (index < bits.Length && bits[index] != '0' && bits[index] != '1') {
          BitBox.Inlines.Add(
          new Run(
            bits[index].ToString()));
          index++;
        }
      }
      //else
      //  BitBox.Text = bits;
      /*Dispatcher.Invoke(() => {
        for (int i = 0; i < 8; i++)
          MyBindableTwoDArray[i, 0] = rawbits[i];
      });*/
    }



    private void Button_Click_1(object sender, RoutedEventArgs e) {
      run = false;
      timer?.Dispose();
    }



    private void HitsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) {
      try {
        Hits dataRow = (Hits)HitsDataGrid.SelectedItem;
        int index = HitsDataGrid.CurrentCell.Column.DisplayIndex;
        Console.WriteLine(index);
        Console.WriteLine(dataRow);
        if (index == 0)
          System.Diagnostics.Process.Start(dataRow.path);
        else
          System.Diagnostics.Process.Start(dataRow.path + '\\' + dataRow.filename);
      }
      catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void PathList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
      try {
        //parser.items = new ObservableDictionary<string, ListElement>();
        List<int> packetList = new List<int>();
        string pStart = null;
        string s = null;
        foreach (var sel in PathList.SelectedItems) {
          pStart = (s = ((StringWithNotify)sel).Str).Substring(0, 3);
          int.TryParse(pStart, System.Globalization.NumberStyles.HexNumber, null, out packet);
          packetList.Add(packet);
        }

        if (s != null)
          updateBits(PathList.SelectedItem as StringWithNotify, s);

        var items = parser.items.Where(x => packetList.Contains(x.Value.packetId) && !x.Value.name.Contains("updated"));


        HitsDataGrid.ItemsSource = items;
        HitsDataGrid.DataContext = parser.items;

        Graph.Series.Clear();

        foreach (var i in items)
          Graph.Series.Add(
            new LineSeries() { StrokeThickness = 1, LineStyle = LineStyle.Solid, Title = i.Value.name, ItemsSource = i.Value.Points });

        Graph.InvalidatePlot(true);

        /*s = "";
        foreach (var sel in PathList.SelectedItems) {        
          Packet p = parser.packets[(sel as StringWithNotify).Pid];
          foreach (var v in p.values)
            s += v.formula.ToString() +'\n';
        }
        Formula.Content = s;*/
      }
      catch (Exception ex) { MessageBox.Show(ex.Message); interpret_as = false; }
    }

    private void PathList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
      try {
        string pStart = null;
        foreach (var sel in PathList.SelectedItems)
          pStart = ((StringWithNotify)sel).Str.Substring(0, 3);
        string line = null;
        switch (e.Key) {
          case System.Windows.Input.Key.Right:
            do
              line = inputStream.ReadLine();
            while (!line.StartsWith(pStart));
            timerCallback(line);
            break;
        }
      }
      catch (Exception ex) { Console.WriteLine(ex.Message); }
    }

    private void HitsDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
      try {
      }
      catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private async void Color_Click(object sender, RoutedEventArgs e) {
      var sel = PathList.SelectedItem as StringWithNotify;
      string s = "";
      //sel.Str = Convert.ToString(sel.Pid, 16).ToUpper().PadLeft(3, '0');// + " 00 00 00 00 00 00 00 00";
      //PathList_SelectionChanged(null, null);
      //await Dispatcher.InvokeAsync(async () => {
      int bit = 63;
      timerCallback(
        Convert.ToString(sel.Pid, 16).ToUpper().PadLeft(3, '0') +
        Convert.ToString(0, 16).PadRight(0 + 1, 'F').PadLeft(16, '0'));

      for (int i = 0; i < 16; i++)
        for (int j = 1; j < 16; j = (j << 1) + 1) {
          //await Task.Delay(100);
          //sel.colors.Clear();
          timerCallback(
            Convert.ToString(sel.Pid, 16).ToUpper().PadLeft(3, '0') +
            Convert.ToString(j, 16).PadRight(i + 1, 'F').PadLeft(16, '0'));

          foreach (var item in parser.items.Where(x => x.Value.packetId == sel.Pid)) {
            if (item.Value.changed) {
              Console.WriteLine(bit + " " + item.Value.name);
              //sel.colors.Insert(0,bit);
              item.Value.bits.Insert(0, bit);
            }
          }
          bit--;
        }
      //sel.colors=sel.colors.Reverse();
      int colorCounter = 0;
      foreach (var item in parser.items.Where(x => x.Value.packetId == sel.Pid)) {
        colorCounter++;
        foreach (var b in item.Value.bits)
          sel.colors[b] = colorCounter;
        //sel.colors.Add(item.Value.bits.First());
        //sel.colors.Add(item.Value.bits.Last());
      }
      PathList_SelectionChanged(null, null);
      // });
    }

    private void Button_Click_2(object sender, RoutedEventArgs e) {
      timer?.Dispose();
      timer = new Timer(timerCallback, null, 10, 1);
      run = true;
    }

    private void Button_Click_3(object sender, RoutedEventArgs e) {
      Packet p;
      foreach (var sel in PathList.SelectedItems) {
        packet = (sel as StringWithNotify).Pid;
        parser.packets.TryGetValue(packet, out p);
        if (p == null) {
          p = new Packet(packet, parser);
          parser.packets.Add(packet, p);
        }

        foreach (var v in parser.packets[interpret_source].values)
          if (interpret_source!=packet)
            p.AddValue(Convert.ToString(packet, 16) + " " + v.name, v.unit, v.tag, v.formula);
      }

      PathList_SelectionChanged(null, null);

    }

    private void Button_Click_4(object sender, RoutedEventArgs e) {
      interpret_source = packet;
      CopyIDButton.Content = Convert.ToString(packet, 16);
    }

    private void Button_Click_5(object sender, RoutedEventArgs e) {
      //inputStream.
    }

    private void Analyze_Packets_Click(object sender, RoutedEventArgs e) {
      foreach (var p in parser.packets) {
        string s = "";
        int bit = 63;
        parser.Parse(
                  Convert.ToString(p.Key, 16).ToUpper().PadLeft(3, '0') +
                  Convert.ToString(0, 16).PadRight(0 + 1, 'F').PadLeft(16, '0') + '\n', 0);

        for (int i = 0; i < 16; i++)
          for (int j = 1; j < 16; j = (j << 1) + 1) {
            //await Task.Delay(100);
            //sel.colors.Clear();
            parser.Parse(
                      Convert.ToString(p.Key, 16).ToUpper().PadLeft(3, '0') +
                      Convert.ToString(j, 16).PadRight(i + 1, 'F').PadLeft(16, '0') + '\n', 0);

            foreach (var item in parser.items.Where(x => x.Value.packetId == p.Key)) {
              if (item.Value.changed) {
                Console.WriteLine(bit + " " + item.Value.name);
                //sel.colors.Insert(0,bit);
                item.Value.bits.Insert(0, bit);
              }
            }
            bit--;
          }
        //sel.colors=sel.colors.Reverse();
        /*int colorCounter = 0;
        foreach (var item in parser.items.Where(x => x.Value.packetId == sel.Pid)) {
          colorCounter++;
          foreach (var b in item.Value.bits)
            sel.colors[b] = colorCounter;*/
        //sel.colors.Add(item.Value.bits.First());
        //sel.colors.Add(item.Value.bits.Last());
      }
      //PathList_SelectionChanged(null, null);
      // });

      AnalyzeResults.ItemsSource = parser.items.Values;
    }

    private void As_Byte_Click_7(object sender, RoutedEventArgs e) {
      interpret_source = 1;
      Button_Click_3(null, null);
    }

    private void As_Word_Click_8(object sender, RoutedEventArgs e) {
      interpret_source = 2;
      Button_Click_3(null, null);
    }

    private void As_Int_Click_9(object sender, RoutedEventArgs e) {
      interpret_source = 3;
      Button_Click_3(null, null);
    }

    private void Delete_Click_10(object sender, RoutedEventArgs e) {
      foreach (var sel in HitsDataGrid.SelectedItems) {
        var item = ((KeyValuePair<string, ListElement>)sel).Value as ListElement;
        parser.packets[item.packetId].values.Remove(
          parser.packets[item.packetId].values.Where(x=>x.name==item.name).FirstOrDefault());

        //PathList.ItemsSource = null;
        parser.items.Remove(((KeyValuePair<string,ListElement>)sel).Key);
        //parser.packets
      }
      PathList_SelectionChanged(null, null);
    }

    private void Window_Closed(object sender, EventArgs e) {
      run = false;
    }
  }
}


