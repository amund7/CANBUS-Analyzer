using System.Windows;
using System.Windows.Threading;

namespace CANBUS
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
      var comException = e.Exception as System.Runtime.InteropServices.COMException;
      if ((comException != null) && (comException.ErrorCode == -2147221040))
      {
        e.Handled = true;
      }
    }

    public static string StartupDBCFilename = null;

    private void Application_Startup(object sender, StartupEventArgs e) {
      for (int i = 0; i < e.Args.Length; ++i)
      {
        if (string.Compare(e.Args[i], "/dbc", true) == 0)
        {
          if (i + 1 < e.Args.Length)
          {
            StartupDBCFilename = e.Args[i + 1];
            i++;
          }
          continue;
        }
      }
    }
  }
}
