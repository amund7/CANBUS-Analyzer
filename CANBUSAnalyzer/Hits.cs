using System;
using System.IO;

namespace CANBUS
{
  class Hits
  {
    public string path { get; }
    public string filename { get; }

    public Hits(string path, string filename)
    {
      this.path = path;
      try
      {
        this.filename = Path.GetFileName(filename);
      }
      catch (Exception)
      {
        this.filename = filename;
      }
    }
  }
}
