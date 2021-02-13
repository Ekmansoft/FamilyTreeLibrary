using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace FamilyTreeLibrary.FamilyData
{
  [DataContract]
  public class MultimediaObjectClass
  {
    private static TraceSource trace = new TraceSource("MultimediaObjectClass", SourceLevels.Warning);
    [DataMember]
    private String format;
    [DataMember]
    private String title;
    [DataMember]
    private IList<NoteClass> noteList;
    [DataMember]
    private String xrefName;

    public MultimediaObjectClass()
    {
      format = "";
      title = "";
      xrefName = "";
    }
    public void SetXrefName(String name)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      xrefName = name;
    }
    public void SetFormat(String inFormat)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      format = inFormat;
    }
    public string GetFormat()
    {
      //trace.TraceInformation("SetXrefName:" + name);
      return format;
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
    public void AddNote(NoteClass note)
    {
      if (noteList == null)
      {
        noteList = new List<NoteClass>();
      }
      noteList.Add(note);
    }
    public IList<NoteClass> GetNoteList()
    {
      return noteList;
    }


    public String GetXrefName()
    {
      //trace.TraceInformation("SetXrefName:" + name);
      return xrefName;
    }

    public void Print()
    {
      trace.TraceInformation("MultimediaObjectClass:" + xrefName);
    }
  }

  [DataContract]
  public class MultimediaLinkClass
  {
    private static TraceSource trace = new TraceSource("MultimediaLinkClass", SourceLevels.Warning);
    [DataMember]
    private String format;
    [DataMember]
    private String mediaType;
    [DataMember]
    private String link;
    [DataMember]
    private String title;
    [DataMember]
    private IList<NoteClass> noteList;
    [DataMember]
    private IList<NoteXrefClass> noteXrefList;

    public MultimediaLinkClass()
    {
      format = "";
      link = "";
    }
    public MultimediaLinkClass(String inFormat, String inLink)
    {
      format = inFormat;
      link = inLink;
    }
    public void SetLink(String inLink)
    {
      //trace.TraceInformation("MultimediaLinkClass::SetLink(" + inLink + ")");
      link = inLink;
    }
    public String GetLink()
    {
      //trace.TraceInformation("MultimediaLinkClass::SetLink(" + inLink + ")");
      return link;
    }
    public void SetFormat(String inFormat)
    {
      //trace.TraceInformation("MultimediaLinkClass::SetFormat(" + inFormat + ")");
      format = inFormat;
    }
    public String GetFormat()
    {
      //trace.TraceInformation("MultimediaLinkClass::SetLink(" + inLink + ")");
      return format;
    }
    public void SetMediaType(String mediaType)
    {
      //trace.TraceInformation("MultimediaLinkClass::SetFormat(" + inFormat + ")");
      this.mediaType = mediaType;
    }
    public String GetMediaType()
    {
      //trace.TraceInformation("MultimediaLinkClass::SetLink(" + inLink + ")");
      return mediaType;
    }
    public void SetTitle(String inTitle)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      title = inTitle;
    }
    public void Print()
    {
      trace.TraceInformation("MultimediaLinkClass:" + format + "," + link);
      if (noteList == null)
      {
        trace.TraceInformation("  notes:" + noteList.Count);
      }
      if (noteXrefList == null)
      {
        trace.TraceInformation("  noteXrefList:" + noteXrefList.Count);
      }

    }
    public void AddNote(NoteClass note)
    {
      if (noteList == null)
      {
        noteList = new List<NoteClass>();
      }
      noteList.Add(note);
    }
    public void AddNoteXref(String inNote)
    {
      if (noteXrefList == null)
      {
        noteXrefList = new List<NoteXrefClass>();
      }
      noteXrefList.Add(new NoteXrefClass(inNote));
    }
    public void AddNoteXref(NoteXrefClass inNote)
    {
      if (noteXrefList == null)
      {
        noteXrefList = new List<NoteXrefClass>();
      }
      noteXrefList.Add(inNote);
    }


  }

  [DataContract]
  public class MultimediaXrefClass : BaseXrefClass
  {
    public MultimediaXrefClass(String name) : base(XrefType.Multimedia, name)
    {
    }
  }
}
