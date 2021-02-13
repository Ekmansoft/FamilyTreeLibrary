using System.Diagnostics;

namespace FamilyTreeLibrary.FamilyTreeStore
{
  public class MemoryClass
  {
    private static TraceSource trace = new TraceSource("MemoryClass", SourceLevels.Warning);
    private Process process;
    Process processMem;

    public MemoryClass()
    {
      process = Process.GetCurrentProcess();
    }

    public void PrintProcessDelta(Process process1, Process process2)
    {
      trace.TraceInformation("PrintProcessDelta: " + process1.Handle + "," + process2.Handle);
      if ((process1.NonpagedSystemMemorySize64 - process2.NonpagedSystemMemorySize64) != 0)
      {
        trace.TraceInformation("  NonpagedSystemMemorySize64: " + (process1.NonpagedSystemMemorySize64 - process2.NonpagedSystemMemorySize64));
      }
      if ((process1.PagedMemorySize64 - process2.PagedMemorySize64) != 0)
      {
        trace.TraceInformation("  PagedMemorySize64:          " + (process1.PagedMemorySize64 - process2.PagedMemorySize64));
      }
      if ((process1.PeakPagedMemorySize64 - process2.PeakPagedMemorySize64) != 0)
      {
        trace.TraceInformation("  PeakPagedMemorySize64:      " + (process1.PeakPagedMemorySize64 - process2.PeakPagedMemorySize64));
      }
      if ((process1.PeakVirtualMemorySize64 - process2.PeakVirtualMemorySize64) != 0)
      {
        trace.TraceInformation("  PeakVirtualMemorySize64:    " + (process1.PeakVirtualMemorySize64 - process2.PeakVirtualMemorySize64));
      }
      if ((process1.PeakWorkingSet64 - process2.PeakWorkingSet64) != 0)
      {
        trace.TraceInformation("  PeakWorkingSet64:           " + (process1.PeakWorkingSet64 - process2.PeakWorkingSet64));
      }
      if ((process1.PrivateMemorySize64 - process2.PrivateMemorySize64) != 0)
      {
        trace.TraceInformation("  PrivateMemorySize64:        " + (process1.PrivateMemorySize64 - process2.PrivateMemorySize64));
      }
      if ((process1.VirtualMemorySize64 - process2.VirtualMemorySize64) != 0)
      {
        trace.TraceInformation("  VirtualMemorySize64:        " + (process1.VirtualMemorySize64 - process2.VirtualMemorySize64));
      }
      if ((process1.WorkingSet64 - process2.WorkingSet64) != 0)
      {
        trace.TraceInformation("  WorkingSet64:               " + (process1.WorkingSet64 - process2.WorkingSet64));
      }
    }

    public void PrintProcessMem(Process process)
    {
      trace.TraceInformation("PrintProcessMem: " + process.MainWindowTitle);
      trace.TraceInformation("  NonpagedSystemMemorySize64: " + process.NonpagedSystemMemorySize64);
      trace.TraceInformation("  PagedMemorySize64:          " + process.PagedMemorySize64);
      trace.TraceInformation("  PeakPagedMemorySize64:      " + process.PeakPagedMemorySize64);
      trace.TraceInformation("  PeakVirtualMemorySize64:    " + process.PeakVirtualMemorySize64);
      trace.TraceInformation("  PeakWorkingSet64:           " + process.PeakWorkingSet64);
      trace.TraceInformation("  PrivateMemorySize64:        " + process.PrivateMemorySize64);
      trace.TraceInformation("  VirtualMemorySize64:        " + process.VirtualMemorySize64);
      trace.TraceInformation("  WorkingSet64:               " + process.WorkingSet64);
    }
    public void PrintProcessMemShort(Process process)
    {
      trace.TraceInformation("Process mem: NPSMS:" + process.NonpagedSystemMemorySize64 + " PMS:" + process.PagedMemorySize64 + " PSMS:" + process.PagedSystemMemorySize64 + " PPMS:" + process.PeakPagedMemorySize64 + " PVMS:" + process.PeakVirtualMemorySize64 + " PWS:" + process.PeakWorkingSet64 + " PMS:" + process.PrivateMemorySize64 + " VMS:" + process.VirtualMemorySize64 + " WS:" + process.WorkingSet64);
    }

    /*    public void PrintProcess(Process process)
        {

            //trace.TraceInformation("Process mem-delta: " + (process.NonpagedSystemMemorySize64 - processMem.NonpagedSystemMemorySize64) + "," + (process.PagedMemorySize64 - processMem.PagedMemorySize64) + "," + (process.PagedSystemMemorySize64 - processMem.PagedSystemMemorySize64) + "," + (process.PeakPagedMemorySize64 - processMem.PeakPagedMemorySize64) + "," + (process.PeakVirtualMemorySize64 - processMem.PeakVirtualMemorySize64) + "," + (process.PeakWorkingSet64 - processMem.PeakWorkingSet64) + "," + (process.PrivateMemorySize64 - processMem.PrivateMemorySize64) + "," + (process.VirtualMemorySize64 - processMem.VirtualMemorySize64) + "," + (process.WorkingSet64 - processMem.WorkingSet64));
            //trace.TraceInformation("Process mem-delta: " + (process.NonpagedSystemMemorySize64 - processMem.NonpagedSystemMemorySize64) + "," + (process.PagedMemorySize64 - processMem.PagedMemorySize64) + "," + (process.PagedSystemMemorySize64 - processMem.PagedSystemMemorySize64) + "," + (process.PeakPagedMemorySize64 - processMem.PeakPagedMemorySize64) + "," + (process.PeakVirtualMemorySize64 - processMem.PeakVirtualMemorySize64) + "," + (process.PeakWorkingSet64 - processMem.PeakWorkingSet64) + "," + (process.PrivateMemorySize64 - processMem.PrivateMemorySize64) + "," + (process.VirtualMemorySize64 - processMem.VirtualMemorySize64) + "," + (process.WorkingSet64 - processMem.WorkingSet64));
            PrintProcessDelta(process, processMem);
          }

          processMem = process;
        }*/
    public void PrintMemory()
    {
      Process process2 = Process.GetCurrentProcess();
      //PrintProcess(process);
      PrintProcessMem(process2);
      if (processMem != null)
      {
        PrintProcessDelta(process, processMem);
        processMem = process;
      }
      //trace.TraceInformation("Process mem: " + process.NonpagedSystemMemorySize64 + "," + process.PagedMemorySize64 + "," + process.PagedSystemMemorySize64 + "," + process.PeakPagedMemorySize64 + "," + process.PeakVirtualMemorySize64 + "," + process.PeakWorkingSet64 + "," + process.PrivateMemorySize64 + "," + process.VirtualMemorySize64 + "," + process.WorkingSet64);
    }

  }
}
