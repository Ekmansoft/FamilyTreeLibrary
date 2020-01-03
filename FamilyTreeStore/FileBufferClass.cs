using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;

namespace FamilyTreeLibrary.FileFormats
{
  public class FileBufferClass
  {
    private static TraceSource trace = new TraceSource("FileBufferClass", SourceLevels.Warning);
    private Byte[] buffer;
    private int size;

    public long ReadFile(String fileName)
    {
      Stream inFile;
      trace.TraceInformation("Filebuffer.Readfile(" + fileName + ")");
      inFile = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);

      if (inFile == null)
      {
        size = 0;
        trace.TraceInformation("File=null");
        return size;
      }
      trace.TraceInformation("File size:" + inFile.Length);

      buffer = new Byte[inFile.Length];
      if (buffer == null)
      {
        size = 0;
        trace.TraceInformation("Buffer=null");
        return size;
      }

      size = inFile.Read(buffer, 0, (int)inFile.Length);

      inFile.Close();

      return size;
    }

    public Byte[] GetBuffer()
    {
      return buffer;
    }
    public long GetSize()
    {
      return size;
    }
  };
}
