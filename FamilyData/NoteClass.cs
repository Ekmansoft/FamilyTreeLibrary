using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Ekmansoft.FamilyTree.Library.FamilyData
{
  [DataContract]
  public class NoteClass
  {
    private static TraceSource trace = new TraceSource("NoteClass", SourceLevels.Warning);
    [DataMember]
    public String note;
    [DataMember]
    private String xrefName;
    [DataMember]
    private IndividualEventClass updateEvent;
    [DataMember]
    private IList<SourceDescriptionClass> sourceList;
    [DataMember]
    private IList<SourceXrefClass> sourceXrefList;

    public NoteClass(String inNote = null)
    {
      note = inNote;
      //sourceList = new List<SourceClass>();
    }
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
        if ((note != null) && (note.Length > 0))
        {
          note += "\r\n";
        }
      }
      note += inNote;
    }
    public void SetUpdateEvent(IndividualEventClass inUpdateEvent)
    {
      updateEvent = inUpdateEvent;
    }
    public void AddSource(SourceDescriptionClass inSource)
    {
      if (sourceList == null)
      {
        sourceList = new List<SourceDescriptionClass>();
      }
      sourceList.Add(inSource);
    }
    public void AddSourceXref(SourceXrefClass inSource)
    {
      if (sourceXrefList == null)
      {
        sourceXrefList = new List<SourceXrefClass>();
      }
      sourceXrefList.Add(inSource);
    }
    public void Print()
    {
      trace.TraceInformation("Note: " + note);
    }
  }

  //[DataContract]
  public class NoteXrefClass : BaseXrefClass
  {
    public NoteXrefClass(String name) : base(XrefType.Note, name)
    {
    }
  }

}
