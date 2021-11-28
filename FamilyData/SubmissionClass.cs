using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Ekmansoft.FamilyTree.Library.FamilyData
{
  [DataContract]
  public class SubmissionClass
  {
    private static TraceSource trace = new TraceSource("SubmissionClass", SourceLevels.Warning);
    [DataMember]
    public String note;
    [DataMember]
    private String xrefName;
    [DataMember]
    private SubmitterXrefClass submitterXref;
    [DataMember]
    private string familyFile;
    [DataMember]
    private string temple;
    [DataMember]
    private string ancestorGenerations;
    [DataMember]
    private string descendantGenerations;
    [DataMember]
    private string ordinance;
    [DataMember]
    private String automatedRecordId;

    public void SetXrefName(String name)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      xrefName = name;
    }
    public String GetXrefName()
    {
      return xrefName;
    }
    public void Concatenate(String inNote, bool addLF)
    {
      if (addLF)
      {
        note += "\n";
      }
      note += inNote;
    }
    public void Print()
    {
      trace.TraceInformation("Submission: " + note);
    }
    public void SetSubmitter(SubmitterXrefClass inSubmitterXref)
    {
      submitterXref = inSubmitterXref;
    }
    public string GetSubmitterXref()
    {
      return submitterXref.GetXrefName();
    }
    public void SetFamilyFile(string familyFile)
    {
      this.familyFile = familyFile;
    }
    public string GetFamilyFile()
    {
      return familyFile;
    }
    public void SetTemple(string str)
    {
      this.temple = str;
    }
    public string GetTemple()
    {
      return temple;
    }
    public void SetAncestorGenerations(string gen)
    {
      ancestorGenerations = gen;
    }
    public string GetAncestorGenerations()
    {
      return ancestorGenerations;
    }
    public void SetDescendantGenerations(string gen)
    {
      descendantGenerations = gen;
    }
    public string GetDescendantGenerations()
    {
      return descendantGenerations;
    }
    public void SetOrdinance(string str)
    {
      ordinance = str;
    }
    public string GetOrdinance()
    {
      return ordinance;
    }
    public void SetAutoRecId(string str)
    {
      automatedRecordId = str;
    }
    public string GetAutoRecId()
    {
      return automatedRecordId;
    }

  }

  //  [DataContract]
  public class SubmissionXrefClass : BaseXrefClass
  {
    public SubmissionXrefClass(String name) : base(XrefType.Submission, name)
    {
    }
  }
}
