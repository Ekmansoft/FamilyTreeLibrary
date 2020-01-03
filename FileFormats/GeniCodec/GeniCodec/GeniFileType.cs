using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel;
using FamilyTreeLibrary.FamilyData;
//using FamilyTreeLibrary.FamilyFileFormat;
using FamilyTreeLibrary.FamilyTreeStore;

namespace FamilyTreeLibrary.FileFormats.GeniCodec
{
  public class GeniFileType : FamilyFileTypeBaseClass
  {
    private static TraceSource trace = new TraceSource("GeniFileType", SourceLevels.Warning);
    //private bool printFlag;
    private FamilyTreeStoreGeni2 GeniStore = null;

    public GeniFileType()
    {
      trace.TraceData(TraceEventType.Error, 0, "GeniFileType create");
    }

    public override void Dispose()
    {
      trace.TraceData(TraceEventType.Error, 0, "GeniFileType dispose");
      if (GeniStore != null)
      {
        GeniStore.Dispose();
      }
    }

    public override bool IsKnownFileType(String fileName)
    {
      if (fileName.ToLower().Contains("geni.com"))
      {
        return true;
      }
      return false;
    }

    public override IFamilyTreeStoreBaseClass CreateFamilyTreeStore(String fileName, CompletedCallback callback)
    {
      if (GeniStore == null)
      {
        GeniStore = new FamilyTreeStoreGeni2(callback, null);
      }

      trace.TraceInformation("GeniFileType::CreateFamilyTreeStore( " + fileName + ")");

      GeniStore.SetFile(fileName);
      return (IFamilyTreeStoreBaseClass)GeniStore;
    }

    public override bool OpenFile(String fileName, ref IFamilyTreeStoreBaseClass inFamilyTree, CompletedCallback callback)
    {
      if (GeniStore != null)
      {
        trace.TraceData(TraceEventType.Warning, 0, "Tree is not null");
      }
      GeniStore = (FamilyTreeStoreGeni2)inFamilyTree;

      trace.TraceInformation("GeniFileType::OpenFile( " + fileName + ")");
      GeniStore.SetFile(fileName);
      if (!GeniStore.CallbackArmed())
      {
        callback(true);
      }
      return true;
    }
    public override bool SetProgressTarget(BackgroundWorker inBackgroundWorker)
    {
      trace.TraceInformation("GeniFileType::SetProgressTarget ");
      //backgroundWorker = inBackgroundWorker;
      return false;
    }
    public override string GetFileTypeWebName()
    {
      return "Geni.com";
    }    
  }
}
