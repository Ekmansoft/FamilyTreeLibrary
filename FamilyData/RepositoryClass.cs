using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace FamilyTreeLibrary.FamilyData
{
  [DataContract]
  public class RepositoryClass
  {
    private static TraceSource trace = new TraceSource("RepositoryClass", SourceLevels.Warning);
    [DataMember]
    public String name;
    [DataMember]
    private String xrefName;
    [DataMember]
    private AddressClass address;
    [DataMember]
    private IList<NoteClass> noteList;
    [DataMember]
    private IList<NoteXrefClass> noteXrefList;

    public void SetXrefName(String name)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      xrefName = name;
    }
    public String GetXrefName()
    {
      return xrefName;
    }
    public void SetName(String name)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      this.name = name;
    }
    public String GetName()
    {
      return name;
    }
    public void AddAddressPart(AddressPartClass inAddress)
    {
      if (address == null)
      {
        address = new AddressClass();
      }
      address.AddAddressPart(inAddress);
    }
    public void AddAddress(AddressClass inAddress)
    {
      address = inAddress;
    }
    public void AddAddressPart(AddressPartClass.AddressPartType AddressPartType, String inAddress)
    {
      AddressPartClass newAddress = new AddressPartClass(AddressPartType, inAddress);

      if (address == null)
      {
        address = new AddressClass();
      }
      address.AddAddressPart(newAddress);
    }
    public AddressClass GetAddress()
    {
      return address;
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
    public void AddNoteXref(String note)
    {
      if (noteXrefList == null)
      {
        noteXrefList = new List<NoteXrefClass>();
      }
      noteXrefList.Add(new NoteXrefClass(note));
    }
    public void AddNoteXref(NoteXrefClass note)
    {
      if (noteXrefList == null)
      {
        noteXrefList = new List<NoteXrefClass>();
      }
      noteXrefList.Add(note);
    }
    public IList<NoteXrefClass> GetNoteXrefList()
    {
      return noteXrefList;
    }
    public void Print()
    {
      trace.TraceInformation("Repository: " + xrefName + ":" + name);
    }
  }
  [DataContract]
  public class RepositoryXrefClass : BaseXrefClass
  {
    public RepositoryXrefClass(String name) : base(XrefType.Repository, name)
    {
    }
  }

}
