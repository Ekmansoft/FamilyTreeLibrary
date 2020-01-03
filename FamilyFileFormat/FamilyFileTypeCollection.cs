using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel;
using FamilyTreeLibrary.FamilyData;
using FamilyTreeLibrary.FileFormats.GedcomCodec;
//using FamilyTreeLibrary.FileFormats.AnarkivCodec;
//using FamilyTreeLibrary.FileFormats.GeniCodec;
//using FamilyTreeLibrary.FileFormats.MyHeritageCodec;
using FamilyTreeLibrary.FileFormats.TextCodec;
//using FamilyTreeLibrary.FileFormats.XmlCodec;
using FamilyTreeLibrary.FamilyTreeStore;

namespace FamilyTreeLibrary.FamilyFileFormat
{
  public class FamilyFileTypeCollection : IFamilyFileType
  {
    private IList<FamilyFileTypeBaseClass> codecList;
    private readonly static TraceSource trace = new TraceSource("FamilyFileTypeCollection", SourceLevels.Warning);
    private FamilyFileType selectedType;

    enum FamilyFileType
    {
      Unknown,
      Xml,
      Gedcom,
      Anarkiv,
      Geni,
      Text
    };

//    private IList<FamilyFileTypeBaseClass> codecList2; 




    public FamilyFileTypeCollection()
    {
      trace.TraceInformation("FamilyFileTypeCollection::FamilyFileTypeCollection()");
      selectedType = FamilyFileType.Unknown;

      codecList = new List<FamilyFileTypeBaseClass>();

      //FamilyFileTypeBaseClass gedcomCodec;
      //FamilyFileTypeBaseClass anarkivCodec;
      //FamilyFileTypeBaseClass geniCodec;

      //gedcomCodec = new GedcomDecoder();
      //codecList.Add(new XmlFileType());
      codecList.Add(new GedcomDecoder());

      //anarkivCodec = new AnarkivFileType();
      //codecList.Add(new AnarkivFileType());

      //geniCodec = new GeniFileType();
      //codecList.Add(new GeniFileType());
      //codecList.Add(new MyHeritageFileType());
      codecList.Add(new TextDecoder());
    }

    private FamilyFileType GetType(FamilyFileTypeBaseClass codec)
    {
      /*if (codec.GetType() == typeof(XmlFileType))
      {
        return FamilyFileType.Xml;
      }*/
      if (codec.GetType() == typeof(GedcomDecoder))
      {
        return FamilyFileType.Gedcom;
      }
      /*if (codec.GetType() == typeof(AnarkivFileType))
      {
        return FamilyFileType.Anarkiv;
      }*/
      /*if (codec.GetType() == typeof(GeniFileType))
      {
        return FamilyFileType.Geni;
      }*/
      if (codec.GetType() == typeof(TextDecoder))
      {
        return FamilyFileType.Text;
      }
      return FamilyFileType.Unknown;
    }

    
    public bool IsKnownFileType(String fileName)
    {
      foreach (FamilyFileTypeBaseClass fileType in codecList)
      {
        trace.TraceInformation("IsKnownFileType - check " + fileType.GetType());

        if (fileType.IsKnownFileType(fileName))
        {
          selectedType = GetType(fileType);
          return true;
        }
      }
      return false;
    }

    public string GetFileTypeFilter(FamilyFileTypeOperation operation)
    {
      string filterStr = "";

      foreach (FamilyFileTypeBaseClass fileType in codecList)
      {
        if (fileType.GetFileTypeFilter(operation) != null)
        {
          trace.TraceInformation("GetFileTypeFilter - add " + fileType.GetType() + ":" + fileType.GetFileTypeFilter(operation));
          if (filterStr.Length > 0)
          {
            filterStr += "|";
          }
          filterStr += fileType.GetFileTypeFilter(operation);
        }
        else
        {
          trace.TraceInformation("GetFileTypeFilter - ignore " + fileType.GetType());
        }
      }


      if (filterStr.Length > 0)
      {
        filterStr += "|";
      }
      filterStr += "All files|*.*";
      return filterStr;
    }

    public string GetWebTypeList()
    {
      string filterStr = "";

      foreach (FamilyFileTypeBaseClass fileType in codecList)
      {
        if (fileType.GetFileTypeWebName() != null)
        {
          trace.TraceInformation("GetWebTypeList - add " + fileType.GetType() + ":" + fileType.GetFileTypeWebName());

          filterStr += "{" + fileType.GetFileTypeWebName() + "}";
        }
        else
        {
          trace.TraceInformation("GetWebTypeList - ignore " + fileType.GetType());
        }
      }


      return filterStr;
    }

    /*public bool OpenWeb(String fileName, FamilyTreeStoreBaseClass inFamilyTree)
    {
      foreach (FamilyFileTypeBaseClass fileType in codecList)
      {
        trace.TraceInformation("OpenWeb(" + fileName + "):" + fileType.GetType() + ":" + fileType.IsKnownFileType(fileName));
        if (fileType.GetFileTypeWebName() == fileName)
        {
          //selectedType = GetType(fileType);
          return fileType.OpenFile(fileName, inFamilyTree);
        }
      }
      return false;
    }*/

    public IFamilyTreeStoreBaseClass CreateFamilyTreeStore(String fileName, CompletedCallback callback)
    {
      if((fileName == null) || (fileName == ""))
      {
        FamilyTreeStoreRam nativeStore = new FamilyTreeStoreRam();

        callback(true);
        return nativeStore;
      }
      foreach (FamilyFileTypeBaseClass fileType in codecList)
      {
        trace.TraceInformation("CreateFamilyTreeStore " + fileType.GetType() + ":" + fileType.IsKnownFileType(fileName));
        if (fileType.IsKnownFileType(fileName))
        {
          selectedType = GetType(fileType);
          return fileType.CreateFamilyTreeStore(fileName, callback);
        }
      }
      trace.TraceInformation("CreateFamilyTreeStore():null");
      callback(false);
      return null;
    }

    public bool OpenFile(String fileName, ref IFamilyTreeStoreBaseClass inFamilyTree, CompletedCallback callback)
    {
      foreach (FamilyFileTypeBaseClass fileType in codecList)
      {
        trace.TraceInformation("OpenFile(" + fileName + "):" + fileType.GetType() + ":" + fileType.IsKnownFileType(fileName));
        if (fileType.IsKnownFileType(fileName))
        {
          //selectedType = GetType(fileType);
          return fileType.OpenFile(fileName, ref inFamilyTree, callback);
        }
      }
      callback(false);
      return false;
    }

    public bool SetProgressTarget(BackgroundWorker inBackgroundWorker)
    {
      foreach (FamilyFileTypeBaseClass fileType in codecList)
      {
        trace.TraceInformation("SetProgressTarget() " + fileType.GetType());
        if(selectedType == GetType(fileType))
        {
          //selectedType = GetType(fileType);
          return fileType.SetProgressTarget(inBackgroundWorker);
        }
      }

      return false;
    }

  }

  public class FamilyFileEncoderCollection : FamilyFileEncoder
  {
    private IList<FamilyFileEncoder> encoderList;
    //private GedcomEncoder gedcomEncoder;
    //private XmlEncoder xmlEncoder;
    private FamilyFileEncoder selectedEncoder;
    private IProgressReporterInterface storedProgressTarget;
    private static TraceSource trace;
    private class EncoderMap
    {
      public FamilyFileEncoder encoder;
      public int variant;
      public string filter;
    }
    private IList<EncoderMap> encoderMapList;

    public FamilyFileEncoderCollection()
    {
      trace = new TraceSource("FamilyFileEncoderCollection", SourceLevels.Warning);
      //gedcomEncoder = new GedcomEncoder();
      //xmlEncoder = new XmlEncoder();
      selectedEncoder = null;

      encoderList = new List<FamilyFileEncoder>();
      //encoderList.Add(new XmlEncoder());
      encoderList.Add(new GedcomEncoder());
      encoderMapList = new List<EncoderMap>();

    }

    public void StoreFile(IFamilyTreeStoreBaseClass familyTree, string filename, FamilyFileTypeOperation operation, int variant = 0)
    {
      trace.TraceInformation("FamilyFileEncoderCollection.StoreFile({0},{1})", filename, variant);

      if (encoderMapList.Count == 0)
      {
        // We should never have to generate a filter 
        // list with only one variant at this level...
        // The variant parameter has another meaning in this 
        // class compared to real encoder classes...
        GetFileTypeFilter(operation);
      }
      if(variant < encoderMapList.Count)
      {
        selectedEncoder = encoderMapList[variant].encoder;
        if (storedProgressTarget != null)
        {
          selectedEncoder.SetProgressTarget(storedProgressTarget);
          storedProgressTarget = null;
        }
        selectedEncoder.StoreFile(familyTree, filename, operation, encoderMapList[variant].variant);
        return;
      }
      foreach(FamilyFileEncoder encoder in encoderList)
      {
        if(encoder.IsKnownFileType(filename))
        {
          selectedEncoder = encoder;
          if (storedProgressTarget != null)
          {
            encoder.SetProgressTarget(storedProgressTarget);
            storedProgressTarget = null;
          }
          encoder.StoreFile(familyTree, filename, operation, variant);
        }
      }
    }
    public void SetProgressTarget(IProgressReporterInterface progressTarget)
    {
      trace.TraceInformation("FamilyFileEncoderCollection.SetProgressTarget()");
      if (selectedEncoder != null)
      {
        selectedEncoder.SetProgressTarget(progressTarget);
      }
      else
      {
        storedProgressTarget = progressTarget;
      }
        
    }

    private void AppendFilterStrings(ref string str)
    {
      if ((str.Length > 0) && (str[str.Length-1] != '|'))
      {
        str += "|";
      }
    }
    public string GetFileTypeFilter(FamilyFileTypeOperation operation, int variant = 0)
    {
      string filter = "";
      encoderMapList.Clear();
      foreach (FamilyFileEncoder encoder in encoderList)
      {
        if (encoder.GetFileTypeFilter(operation) != null)
        {
          IDictionary<int,string> opList = encoder.GetOperationVariantList(operation);
          if ((opList != null) && (opList.Count > 0))
          {
            foreach(KeyValuePair<int,string> op in opList)
            {
              EncoderMap entry = new EncoderMap();

              entry.filter = encoder.GetFileTypeFilter(operation, op.Key);
              entry.encoder = encoder;
              entry.variant = op.Key;
              filter += entry.filter;
              encoderMapList.Add(entry);
              AppendFilterStrings(ref filter);
            }
          }
          else
          {
            EncoderMap entry = new EncoderMap();

            entry.filter = encoder.GetFileTypeFilter(operation);
            entry.encoder = encoder;
            entry.variant = 0;

            filter += entry.filter;
            encoderMapList.Add(entry);

            AppendFilterStrings(ref filter);
          }

        }
      }
      filter += "All files|*.*";
      return filter;
    }
    public bool IsKnownFileType(string filename)
    {
      foreach (FamilyFileEncoder encoder in encoderList)
      {
        if(encoder.IsKnownFileType(filename))
        {
          return true;
        }
      }
      return false;
    }
    public IDictionary<int, string> GetOperationVariantList(FamilyFileTypeOperation operation)
    {
      if (operation == FamilyFileTypeOperation.Export)
      {
        IDictionary<int, string> opList = new Dictionary<int, string>();

        opList.Add(0, "Normal");
        opList.Add(1, "Extensive");

        return opList;
      }
      return null;
    }

  }
}
