using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace FamilyTreeLibrary.FamilyData
{
  [DataContract]
  public class SourceEventClass
  {
    [DataMember]
    public String eventDescription;
    [DataMember]
    public FamilyDateTimeClass date;
    [DataMember]
    public String place;
  }
  [DataContract]
  public class SourceDataClass
  {
    [DataMember]
    public String agency;
    [DataMember]
    public IList<NoteClass> noteList;
    [DataMember]
    private IList<NoteXrefClass> noteXrefList;
    [DataMember]
    public IList<SourceEventClass> eventList;
    [DataMember]
    private String automatedRecordId;

    public SourceDataClass()
    {
      //noteList = new List<NoteClass>();
      //noteXrefList = new List<NoteXrefClass>();
      //eventList = new List<SourceEventClass>();
    }
    public void AddAgency(String inAgency)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      agency = inAgency;
    }
    public void AddNote(NoteClass inNote)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (noteList == null)
      {
        noteList = new List<NoteClass>();
      }
      noteList.Add(inNote);
    }
    public void AddNoteXref(NoteXrefClass inNote)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (noteXrefList == null)
      {
        noteXrefList = new List<NoteXrefClass>();
      }
      noteXrefList.Add(inNote);
    }
    public void AddEvent(SourceEventClass newEvent)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (eventList == null)
      {
        eventList = new List<SourceEventClass>();
      }
      eventList.Add(newEvent);
    }
    public void SetAutomatedRecordId(String recordId)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      automatedRecordId = recordId;
    }

  };
  [DataContract]
  public class SourceClass
  {
    [DataMember]
    private String sourceDescription;
    [DataMember]
    private String title;
    [DataMember]
    private String xrefName;
    [DataMember]
    private String page;
    [DataMember]
    private String qualityOfData;
    [DataMember]
    private String author;
    [DataMember]
    private String abbreviation;
    [DataMember]
    private String publication;
    [DataMember]
    private String text;
    [DataMember]
    private IList<MultimediaLinkClass> multimediaLinkList;
    [DataMember]
    private IList<NoteClass> noteList;
    [DataMember]
    private IList<NoteXrefClass> noteXrefList;
    [DataMember]
    private FamilyDateTimeClass date;
    [DataMember]
    private SourceDataClass sourceData;
    [DataMember]
    private IList<RepositoryXrefClass> repositoryXrefList;
    [DataMember]
    private IndividualEventClass changeNote;

    public SourceClass(String sourceDescr = null)
    {
      //multimediaLinkList = new List<MultimediaLinkClass>();
      //noteList = new List<NoteClass>();
      //noteXrefList = new List<NoteXrefClass>();
      //date = new FamilyDateTimeClass();
      //sourceData = new SourceDataClass();
      //changeNote = new IndividualEventClass();
      sourceDescription = sourceDescr;
    }
    public string GetDescription()
    {
      return sourceDescription;
    }
    public void SetDescription(string sourceDescr)
    {
      sourceDescription = sourceDescr;
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
    public void SetDate(FamilyDateTimeClass inDate)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      date = inDate;
    }
    public FamilyDateTimeClass GetDate()
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (date == null)
      {
        return new FamilyDateTimeClass();
      }
      return date;
    }
    public void AddNote(NoteClass inNote)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (noteList == null)
      {
        noteList = new List<NoteClass>();
      }
      noteList.Add(inNote);
    }
    public void AddNoteXref(NoteXrefClass inNote)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (noteXrefList == null)
      {
        noteXrefList = new List<NoteXrefClass>();
      }
      noteXrefList.Add(inNote);
    }
    public void SetPage(String inPage)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      page = inPage;
    }
    public void SetQualityOfData(String inQualityOfData)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      qualityOfData = inQualityOfData;
    }
    public void AddAuthor(String inAuthor)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      author = inAuthor;
    }
    public void SetSourceData(SourceDataClass inSourceData)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      sourceData = inSourceData;
    }
    public SourceDataClass GetSourceData()
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (sourceData == null)
      {
        return new SourceDataClass();
      }
      return sourceData;
    }
    public void SetTitle(String inTitle)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      title = inTitle;
    }
    public string GetTitle()
    {
      //trace.TraceInformation("SetXrefName:" + name);
      return title;
    }
    public void SetAbbreviation(String inAbbreviation)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      abbreviation = inAbbreviation;
    }
    public void SetPublicationFacts(String inPublication)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      publication = inPublication;
    }
    public void SetText(String inText)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      text = inText;
    }
    public void AddRepositoryXref(RepositoryXrefClass repoXref)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (repositoryXrefList == null)
      {
        repositoryXrefList = new List<RepositoryXrefClass>();
      }
      repositoryXrefList.Add(repoXref);
    }
    public void SetChange(IndividualEventClass inChangeNote)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      changeNote = inChangeNote;
    }

    
    
    public void Print()
    {
      //trace.TraceInformation("Source: " + name);
    }
    public void AddMultimediaLink(MultimediaLinkClass multimediaLink)
    {
      //trace.TraceInformation("IndividualClass.SetDate(" + eventType + "," + date + ")");
      if (multimediaLinkList == null)
      {
        multimediaLinkList = new List<MultimediaLinkClass>();
      }
      multimediaLinkList.Add(multimediaLink);
    }
  }

  [DataContract]
  public class SourceDescriptionClass
  {
    [DataMember]
    private String sourceDescription;
    [DataMember]
    private String text;
    [DataMember]
    private IList<NoteClass> noteList;
    [DataMember]
    private IList<NoteXrefClass> noteXrefList;

    public SourceDescriptionClass(String sourceDescr = null)
    {
      //multimediaLinkList = new List<MultimediaLinkClass>();
      //noteList = new List<NoteClass>();
      //noteXrefList = new List<NoteXrefClass>();
      //date = new FamilyDateTimeClass();
      //sourceData = new SourceDataClass();
      //changeNote = new IndividualEventClass();
      sourceDescription = sourceDescr;
    }
    public string GetDescription()
    {
      return sourceDescription;
    }
    public void SetDescription(string sourceDescr)
    {
      sourceDescription = sourceDescr;
    }
    public void SetText(String inText)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      text = inText;
    }
   public void AddNote(NoteClass inNote)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (noteList == null)
      {
        noteList = new List<NoteClass>();
      }
      noteList.Add(inNote);
    }
    public void AddNoteXref(NoteXrefClass inNote)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (noteXrefList == null)
      {
        noteXrefList = new List<NoteXrefClass>();
      }
      noteXrefList.Add(inNote);
    }

    public void Print()
    {
      //trace.TraceInformation("Source: " + name);
    }
  }
  [DataContract]
  public class SourceXrefClass : BaseXrefClass
  {
    [DataMember]
    private String page;
    [DataMember]
    private String qualityOfData;
    [DataMember]
    private FamilyDateTimeClass date;
    [DataMember]
    private IList<NoteClass> noteList;
    [DataMember]
    private IList<NoteXrefClass> noteXrefList;
    [DataMember]
    private IList<MultimediaLinkClass> multimediaLinkList;

    public SourceXrefClass(String name) : base(XrefType.Source, name)
    {
    }

    public void SetPage(String inPage)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      page = inPage;
    }
    public void SetQualityOfData(String inQualityOfData)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      qualityOfData = inQualityOfData;
    }
    public void SetDate(FamilyDateTimeClass inDate)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      date = inDate;
    }
    public void AddNote(NoteClass inNote)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (noteList == null)
      {
        noteList = new List<NoteClass>();
      }
      noteList.Add(inNote);
    }
    public void AddNoteXref(NoteXrefClass inNote)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (noteXrefList == null)
      {
        noteXrefList = new List<NoteXrefClass>();
      }
      noteXrefList.Add(inNote);
    }
    public void AddMultimediaLink(MultimediaLinkClass multimediaLink)
    {
      //trace.TraceInformation("IndividualClass.SetDate(" + eventType + "," + date + ")");
      if (multimediaLinkList == null)
      {
        multimediaLinkList = new List<MultimediaLinkClass>();
      }
      multimediaLinkList.Add(multimediaLink);
    }
  }
}
