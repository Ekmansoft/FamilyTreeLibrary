using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
//using Ekmansoft.FamilyTree.Library.FamilyTreeStore;

namespace Ekmansoft.FamilyTree.Library.FamilyTreeStore
{
  public enum FamilyFileTypeOperation
  {
    Open,
    Save,
    Import,
    Export
  };

  public class FileImportResult
  {
    private IList<string> importResultList;

    public FileImportResult()
    {
      importResultList = new List<string>();
    }

    public void AddString(string str)
    {
      importResultList.Add(str);
    }

    public void WriteToFile(string filename)
    {
      string directory = "/tmp/";
      if (!Directory.Exists(directory))
      {
        directory = "";
      }

      if ((directory.Length > 0) && filename.Contains(directory))
      {
        filename = filename.Substring(directory.Length);
      }
      using (StreamWriter writer = new StreamWriter(directory + FamilyUtility.MakeFilename(filename + "_import_result.txt")))
      {
        foreach (string str in importResultList)
        {
          writer.Write(str + FamilyUtility.GetLinefeed());
        }
      }
    }
    public override string ToString()
    {
      StringBuilder strBuilder = new StringBuilder();

      foreach (string str in importResultList)
      {
        strBuilder.Append(str + FamilyUtility.GetLinefeed());
      }
      return strBuilder.ToString();
    }
  }

  public delegate void CompletedCallback(Boolean result);

  public interface IFamilyFileType
  {
    bool IsKnownFileType(String fileName);

    IFamilyTreeStoreBaseClass CreateFamilyTreeStore(String fileName, CompletedCallback callback);

    bool OpenFile(String fileName, ref IFamilyTreeStoreBaseClass inFamilyTree, CompletedCallback callback);

    bool SetProgressTarget(BackgroundWorker inBackgroundWorker);

    string GetFileTypeFilter(FamilyFileTypeOperation operation);
  }

  public abstract class FamilyFileTypeBaseClass : IFamilyFileType
  {
    public abstract bool IsKnownFileType(String fileName);

    public abstract IFamilyTreeStoreBaseClass CreateFamilyTreeStore(String fileName, CompletedCallback callback);

    public abstract bool OpenFile(String fileName, ref IFamilyTreeStoreBaseClass inFamilyTree, CompletedCallback callback);

    public abstract bool SetProgressTarget(BackgroundWorker inBackgroundWorker);

    public virtual string GetFileTypeFilter(FamilyFileTypeOperation operation)
    {
      return null;
    }
    public virtual string GetFileTypeWebName()
    {
      return null;
    }
    public virtual bool GetFileTypeCreatesStorage()
    {
      return false;
    }
    public virtual FileImportResult GetImportResult()
    {
      return null;
    }
    public abstract void Dispose();
  }
}
