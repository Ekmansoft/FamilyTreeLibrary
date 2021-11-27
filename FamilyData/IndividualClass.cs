using FamilyTreeLibrary.FamilyTreeStore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace FamilyTreeLibrary.FamilyData
{
  [DataContract]
  public class PersonalNameClass : ICloneable
  {
    private static TraceSource trace = new TraceSource("PersonalNameClass", SourceLevels.Warning);

    public enum PartialNameType
    {
      Unknown,

      NamePrefix,
      GivenName,
      MiddleName,
      Nickname,
      SurnamePrefix,
      Surname,
      BirthSurname,
      Suffix,
      // Above types can be written and read. 
      // Below types can only be read
      NameString,
      PublicName = NameString
    };

    [DataMember]
    private IDictionary<PartialNameType, string> partialNameList;
    [DataMember]
    private IList<SourceDescriptionClass> sourceList;
    [DataMember]
    private IList<SourceXrefClass> sourceXrefList;
    [DataMember]
    private IList<NoteClass> noteList;
    [DataMember]
    private IList<NoteXrefClass> noteXrefList;
    //private NameType nameType;
    //private String nameString;

    public PersonalNameClass(PartialNameType type = PartialNameType.Unknown, string name = null)
    {
      partialNameList = new Dictionary<PartialNameType, string>();
      //sourceList = new List<SourceClass>();
      //sourceXrefList = new List<SourceXrefClass>();
      //noteList = new List<NoteClass>();
      //noteXrefList = new List<NoteXrefClass>();
      if (name != null)
      {
        partialNameList.Add(type, name);
      }
    }

    private string Append(string s1, string s2)
    {
      if (s1.Length > 0)
      {
        return s1 + " " + s2;
      }
      return s2;
    }

    private string CheckAppend(string s1, PartialNameType type)
    {
      if (partialNameList.ContainsKey(type))
      {
        s1 = Append(s1, partialNameList[type]);
      }
      return s1;
    }

    private string GetComplexNameTypes(PartialNameType type)
    {
      string name = "";
      if ((type == PartialNameType.PublicName) || (type == PartialNameType.NameString))
      {
        name = CheckAppend(name, PartialNameType.NamePrefix);
        name = CheckAppend(name, PartialNameType.GivenName);
        name = CheckAppend(name, PartialNameType.MiddleName);
        name = CheckAppend(name, PartialNameType.Nickname);
        name = CheckAppend(name, PartialNameType.SurnamePrefix);
        name = CheckAppend(name, PartialNameType.Surname);

        if (partialNameList.ContainsKey(PartialNameType.BirthSurname))
        {
          bool include = true;
          if (partialNameList.ContainsKey(PartialNameType.Surname))
          {
            if (partialNameList[PartialNameType.BirthSurname] == partialNameList[PartialNameType.Surname])
            {
              include = false;
            }
          }
          if (include)
          {
            name = CheckAppend(name, PartialNameType.BirthSurname);
          }
        }
        name = CheckAppend(name, PartialNameType.Suffix);
        trace.TraceInformation("GetComplexNameTypes(" + type + "):" + name);
      }
      return name;
    }

    public String GetName(PartialNameType type = PartialNameType.PublicName, bool gedcomFormat = false)
    {
      if (partialNameList.Count > 0)
      {
        if (partialNameList.ContainsKey(type))
        {

          {
            trace.TraceInformation("GetName(" + type + "):" + partialNameList[type]);
          }
          return partialNameList[type];
        }

        return GetComplexNameTypes(type);
      }
      return "";
    }

    public bool IsNameTypeReadable(PartialNameType type)
    {
      return true;
    }
    public bool IsNameTypeWritable(PartialNameType type)
    {
      if (type == PartialNameType.PublicName)
      {
        return false;
      }

      return true;
    }

    public void SetName(PartialNameType type, String name)
    {
      trace.TraceInformation("SetName(" + type + "," + name + ")");

      if (partialNameList.ContainsKey(type))
      {
        if (partialNameList[type].IndexOf(name) != 0)
        {
          return;
        }
        if (name.IndexOf(partialNameList[type]) == -1)
        {
          if (partialNameList[type].IndexOf(name) >= 0)
          {
            return;
          }
          else if (name.IndexOf(partialNameList[type]) >= 0)
          {
            // Replace current name with new, more complete, name
          }
          else
          {
            // Append names
            name = name + " " + partialNameList[type];
          }
        }
        partialNameList.Remove(type);
      }
      partialNameList.Add(type, name);
    }


    public void AddSource(SourceDescriptionClass source)
    {
      if (sourceList == null)
      {
        sourceList = new List<SourceDescriptionClass>();
      }
      sourceList.Add(source);
    }
    public void AddSourceXref(SourceXrefClass sourceXref)
    {
      if (sourceXrefList == null)
      {
        sourceXrefList = new List<SourceXrefClass>();
      }
      sourceXrefList.Add(sourceXref);
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

    /*public IDictionary<PartialNameType, PartialNameClass> GetPartialNameList()
    {
      return partialNameList;
    }*/

    public void SanityCheck()
    {

    }

    public override string ToString()
    {
      return GetName(PartialNameType.PublicName);
    }

    public object Clone()
    {
      return MemberwiseClone();
    }
  }





  [DataContract]
  public class IndividualClass : ICloneable
  {
    private static TraceSource trace = new TraceSource("IndividualClass", SourceLevels.Warning);
    public enum IndividualSexType
    {
      Unknown,
      Male,
      Female
    };
    public enum RelationType
    {
      Child,
      Spouse
    };

    public enum IndividualSpecialRecordIdType
    {
      PermanentRecordFileNumber,
      AutomatedRecordId,
      AncestralFileNumber,
      UserReferenceNumber
    }

    public enum Alive
    {
      Unknown,
      Yes,
      No
    }

    public IndividualClass()
    {
      personalName = new PersonalNameClass();
      sex = IndividualSexType.Unknown;
      //familyChildList = new List<FamilyXrefClass>();
      //familySpouseList = new List<FamilyXrefClass>();
      //submitterList = new List<IndividualXrefClass>();
      //permanentRFN_List = new List<String>();
      //noteList = new List<NoteClass>();
      //noteXrefList = new List<NoteXrefClass>();
      //address = new AddressClass();
      //eventList = new List<IndividualEventClass>();
      //multimediaLinkList = new List<MultimediaLinkClass>();
      //sourceList = new List<SourceClass>();
      isAlive = Alive.Unknown;
      @public = true;
      xrefName = "";
      MarkUpdate();
    }


    public void SetXrefName(String name)
    {
      trace.TraceInformation("IndividualClass.SetXrefName(" + name + ")");

      xrefName = name;
      MarkUpdate();
    }
    public String GetXrefName()
    {
      return xrefName;
    }

    public void SetPublic(bool @public)
    {
      trace.TraceInformation("IndividualClass.SetPublic(" + @public + ")");

      this.@public = @public;
      MarkUpdate();
    }
    public bool GetPublic()
    {
      return @public;
    }

    public void SetSex(IndividualSexType inSex)
    {
      trace.TraceInformation("IndividualClass.SetSex(" + sex + ")");
      sex = inSex;
      MarkUpdate();
    }
    public IndividualSexType GetSex()
    {
      return sex;
    }

    public void SetPersonalName(PersonalNameClass name)
    {
      trace.TraceInformation("IndividualClass.SetPersonalName(" + name.GetName() + ")");

      personalName = name;
      personalName.SanityCheck();
      MarkUpdate();
    }
    public PersonalNameClass GetPersonalName()
    {
      return personalName;
    }

    public void AddEvent(IndividualEventClass.EventType eventType, FamilyDateTimeClass date)
    {
      trace.TraceInformation("IndividualClass.SetDate(" + eventType + "," + date + ")");
      if (eventList == null)
      {
        eventList = new List<IndividualEventClass>();
      }
      eventList.Add(new IndividualEventClass(eventType, date));
      MarkUpdate();
    }

    public void AddEvent(IndividualEventClass eventData)
    {
      if (eventList == null)
      {
        eventList = new List<IndividualEventClass>();
      }
      eventList.Add(eventData);
      MarkUpdate();
    }

    public IList<IndividualEventClass> GetEventList(IndividualEventClass.EventType requestedEventTypes = IndividualEventClass.EventType.AllEvents)
    {
      if (eventList == null)
      {
        return new List<IndividualEventClass>();
      }
      return eventList;
    }
    public void SetEventList(IList<IndividualEventClass> events)
    {
      eventList = events;
      MarkUpdate();
    }
    public IndividualEventClass GetEvent(IndividualEventClass.EventType requestedEventType)
    {
      if (eventList != null)
      {
        foreach (IndividualEventClass ev in eventList)
        {
          if (ev.GetEventType() == requestedEventType)
          {
            return ev;
          }
        }
      }
      return null;
    }

    public void AddRelation(FamilyXrefClass familyRelation, RelationType relation)
    {
      trace.TraceInformation("IndividualClass.AddRelation(" + familyRelation.GetXrefName() + "," + relation + ")");
      switch (relation)
      {
        case RelationType.Spouse:
          if (familySpouseList == null)
          {
            familySpouseList = new List<FamilyXrefClass>();
          }
          familySpouseList.Add(familyRelation);
          break;

        case RelationType.Child:
          if (familyChildList == null)
          {
            familyChildList = new List<FamilyXrefClass>();
          }
          familyChildList.Add(familyRelation);
          break;

        default:
          break;
      }
      MarkUpdate();
    }

    public void AddAddress(AddressPartClass.AddressPartType AddressPartType, String inAddress)
    {
      if (address == null)
      {
        address = new AddressClass();
      }
      address.AddAddressPart(new AddressPartClass(AddressPartType, inAddress));
      MarkUpdate();
    }

    public void AddAddress(AddressClass inAddress)
    {
      address = inAddress;
      //addressList.Add(new AddressPartClass(AddressPartType, address));
      MarkUpdate();
    }

    public AddressClass GetAddress()
    {
      if (address == null)
      {
        return new AddressClass();
      }

      return address;
      //addressList.Add(new AddressPartClass(AddressPartType, address));
    }

    public void AddSubmitter(SubmitterXrefClass submitter)
    {
      if (submitterList == null)
      {
        submitterList = new List<SubmitterXrefClass>();
      }
      submitterList.Add(submitter);
      MarkUpdate();
    }
    public IList<SubmitterXrefClass> GetSubmitterList(string submitterFilter = null)
    {
      if (submitterList == null)
      {
        return new List<SubmitterXrefClass>();
      }
      return submitterList;
    }

    public void AddPermanentRFN(String rfn)
    {
      if (permanentRFN_List == null)
      {
        permanentRFN_List = new List<String>();
      }
      permanentRFN_List.Add(rfn);
      MarkUpdate();
    }

    public IList<String> GetPermanentRFNList()
    {
      if (permanentRFN_List == null)
      {
        return new List<String>();
      }
      return permanentRFN_List;
    }

    public void AddNoteXref(NoteXrefClass noteXref)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (noteXrefList == null)
      {
        noteXrefList = new List<NoteXrefClass>();
      }
      noteXrefList.Add(noteXref);
      MarkUpdate();
    }
    public IList<NoteXrefClass> GetNoteXrefList(string noteFilter = null)
    {
      if (noteXrefList == null)
      {
        return new List<NoteXrefClass>();
      }
      return noteXrefList;
    }

    public void AddNote(NoteClass note)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      if (noteList == null)
      {
        noteList = new List<NoteClass>();
      }
      noteList.Add(note);
      MarkUpdate();
    }
    public IList<NoteClass> GetNoteList(string noteFilter = null)
    {
      if (noteList == null)
      {
        return new List<NoteClass>();
      }
      return noteList;
    }

    public void AddMultimediaLink(MultimediaLinkClass multimediaLink)
    {
      //trace.TraceInformation("IndividualClass.AddMultimediaLink(" + multimediaLink + ")");
      if (multimediaLinkList == null)
      {
        multimediaLinkList = new List<MultimediaLinkClass>();
      }
      multimediaLinkList.Add(multimediaLink);
      MarkUpdate();
    }
    public IList<MultimediaLinkClass> GetMultimediaLinkList()
    {
      //trace.TraceInformation("IndividualClass.AddMultimediaLink(" + multimediaLink + ")");
      if (multimediaLinkList != null)
      {
        return multimediaLinkList;
      }
      return new List<MultimediaLinkClass>();
    }


    public FamilyDateTimeClass GetDate(IndividualEventClass.EventType eventType)
    {
      //trace.TraceInformation("IndividualClass.GetDate(" + eventType + ")");

      if (eventList != null)
      {
        foreach (IndividualEventClass individualEvent in eventList)
        {
          if (individualEvent.GetEventType() == eventType)
          {
            return individualEvent.GetDate();
          }
        }
      }
      return new FamilyDateTimeClass();
    }

    public String GetName()
    {
      return personalName.GetName();
    }
    public void AddUrl(string url)
    {
      if (urlList == null)
      {
        urlList = new List<string>();
      }
      urlList.Add(url);
      MarkUpdate();
    }
    public IList<string> GetUrlList(string sourceFilter = null)
    {
      if (urlList == null)
      {
        return new List<string>();
      }
      return urlList;
    }
    public void AddSource(SourceDescriptionClass source)
    {
      if (sourceList == null)
      {
        sourceList = new List<SourceDescriptionClass>();
      }
      sourceList.Add(source);
      MarkUpdate();
    }
    public IList<SourceDescriptionClass> GetSourceList(string sourceFilter = null)
    {
      if (sourceList == null)
      {
        return new List<SourceDescriptionClass>();
      }
      return sourceList;
    }
    public void AddSourceXref(SourceXrefClass source)
    {
      if (sourceXrefList == null)
      {
        sourceXrefList = new List<SourceXrefClass>();
      }
      sourceXrefList.Add(source);
      MarkUpdate();
    }
    public IList<SourceXrefClass> GetSourceXrefList()
    {
      if (sourceXrefList == null)
      {
        return new List<SourceXrefClass>();
      }
      return sourceXrefList;
    }
    public void SetSpecialRecordId(IndividualSpecialRecordIdType type, String recordId)
    {
      //trace.TraceInformation("SetXrefName:" + name);
      //automatedRecordId = recordId;
      if (specialRecordList == null)
      {
        specialRecordList = new Dictionary<IndividualSpecialRecordIdType, string>();
      }
      if (specialRecordList.ContainsKey(type))
      {
        trace.TraceData(TraceEventType.Error, 0, "Trying to add " + type + ":" + recordId + " which seem to be in already:" + specialRecordList.Count);
      }
      else
      {
        specialRecordList.Add(type, recordId);
      }
      MarkUpdate();
    }

    public override int GetHashCode()
    {
      if (hashCodeValid)
      {
        return hashCode;
      }
      hashCode = 0;

      for (int i = 0; i < xrefName.Length; i++)
      {
        hashCode += (int)((xrefName[i] - '0') * (int)Math.Pow(10, (xrefName.Length - i - 1)));
      }
      hashCodeValid = true;

      return hashCode;
    }

    public Alive GetIsAlive()
    {
      return isAlive;
    }
    public void SetIsAlive(bool isAlive)
    {
      if (isAlive)
      {
        this.isAlive = Alive.Yes;
      }
      else
      {
        this.isAlive = Alive.No;
      }
      MarkUpdate();
    }
    public void SetIsAlive(Alive isAlive)
    {
      this.isAlive = isAlive;
      MarkUpdate();
    }

    public bool Validate(IFamilyTreeStoreBaseClass familyTree, ref ValidationData validationData, String callerXrefId = null) // IList<IndividualClass> individualList)
    {
      bool allFound = true;
      bool callerFound = (callerXrefId == null);
      //ValidationData validationData = new ValidationData();

      //trace.TraceInformation("Validate: " + this.GetXrefName());
      if (familyChildList != null)
      {
        foreach (FamilyXrefClass person in familyChildList)
        {
          String xrefName = person.GetXrefName();
          bool found = false;

          if (callerXrefId == xrefName)
          {
            callerFound = true;
          }

          FamilyClass family;

          //family = (FamilyClass)familyTree.familyList[person.GetXrefName()];
          family = familyTree.GetFamily(person.GetXrefName());

          found = (family != null);
          if (!found)
          {
            //
            if (trace.Switch.Level.HasFlag(SourceLevels.Information))
            {
              Print();
              trace.TraceInformation(" Family-child not found: " + xrefName);
            }
            allFound = false;
            //return false;
          }
          else
          {
            //trace.TraceInformation("Found family-child : " + xrefName);
          }
          validationData.familyNo++;
        }
      }
      if (familySpouseList != null)
      {
        foreach (FamilyXrefClass person in familySpouseList)
        {
          String xrefName = person.GetXrefName();
          bool found = false;

          if (callerXrefId == xrefName)
          {
            callerFound = true;
          }
          FamilyClass family;

          family = familyTree.GetFamily(person.GetXrefName());

          found = (family != null);
          if (!found)
          {
            //
            if (trace.Switch.Level.HasFlag(SourceLevels.Information))
            {
              Print();
              trace.TraceInformation(" Spouse not found: " + xrefName);
            }
            allFound = false;
            //return false;
          }
          else
          {
            //trace.TraceInformation("Found spouse: " + xrefName);
          }
          validationData.individualNo++;
        }
      }
      if (noteXrefList != null)
      {
        foreach (NoteXrefClass noteXref in noteXrefList)
        {
          String xrefName = noteXref.GetXrefName();
          bool found = false;

          if (callerXrefId == xrefName)
          {
            callerFound = true;
          }
          NoteClass note;

          note = familyTree.GetNote(noteXref.GetXrefName());

          found = (note != null);
          if (!found)
          {
            if (trace.Switch.Level.HasFlag(SourceLevels.Information))
            {
              Print();
              trace.TraceInformation(" Note not found: " + xrefName);
            }
            allFound = false;
            //return false;
          }
          else
          {
            //trace.TraceInformation("Found spouse: " + xrefName);
          }
          validationData.noteNo++;
        }
      }
      //trace.TraceInformation("Validate: " + this.GetXrefName() + " = " + allFound);

      return allFound && callerFound;
    }

    public IList<FamilyXrefClass> GetFamilyChildList()
    {
      if (familyChildList == null)
      {
        return new List<FamilyXrefClass>();
      }
      return familyChildList;
    }

    public void SetFamilyChildList(IList<FamilyXrefClass> childList)
    {
      familyChildList = childList;
      MarkUpdate();
    }

    public IList<FamilyXrefClass> GetFamilySpouseList()
    {
      if (familySpouseList == null)
      {
        return new List<FamilyXrefClass>();
      }
      return familySpouseList;
    }
    public void SetFamilySpouseList(IList<FamilyXrefClass> spouseList)
    {
      familySpouseList = spouseList;
      MarkUpdate();
    }

    public override string ToString()
    {
      return GetXrefName() + ":" + GetPersonalName();
    }

    public void Print()
    {
      trace.TraceInformation("Individual:" + xrefName + " sex:" + sex + " n:[" + GetName() + "]");
      if ((eventList != null) && (eventList.Count > 0))
      {
        trace.TraceInformation(" eventList:" + eventList.Count);
        foreach (IndividualEventClass ev in eventList)
        {
          trace.TraceInformation("   " + ev.GetDate() + ":" + ev.GetEventType());
        }
      }
      else
      {
        trace.TraceInformation(" eventList:-");
      }
      if ((familyChildList != null) && (familyChildList.Count > 0))
      {
        trace.TraceInformation(" familyChildList:" + familyChildList.Count);
        foreach (FamilyXrefClass xref in familyChildList)
        {
          trace.TraceInformation("   " + xref);
        }
      }
      else
      {
        trace.TraceInformation(" familyChildList:-");
      }
      if ((familySpouseList != null) && (familySpouseList.Count > 0))
      {
        trace.TraceInformation(" familySpouseList:" + familySpouseList.Count);
        foreach (FamilyXrefClass xref in familySpouseList)
        {
          trace.TraceInformation("   " + xref);
        }
      }
      else
      {
        trace.TraceInformation(" familySpouseList:-");
      }
      if ((submitterList != null) && (submitterList.Count > 0))
      {
        trace.TraceInformation(" submitterList:" + submitterList.Count);
        foreach (SubmitterXrefClass xref in submitterList)
        {
          trace.TraceInformation("   " + xref);
        }
      }
      else
      {
        trace.TraceInformation(" submitterList:-");
      }
      if ((permanentRFN_List != null) && permanentRFN_List.Count > 0)
      {
        trace.TraceInformation(" permanentRFN_List:" + permanentRFN_List.Count);
      }
      else
      {
        trace.TraceInformation(" permanentRFN_List:-");
      }
      if ((noteList != null) && (noteList.Count > 0))
      {
        trace.TraceInformation(" noteList:" + noteList.Count);
      }
      else
      {
        trace.TraceInformation(" noteList:-");
      }
      if ((noteXrefList != null) && (noteXrefList.Count > 0))
      {
        trace.TraceInformation(" noteXrefList:" + noteXrefList.Count);
      }
      else
      {
        trace.TraceInformation(" noteXrefList:-");
      }
      if (address != null)
      {
        trace.TraceInformation(" address:" + address.ToString());
      }
      else
      {
        trace.TraceInformation(" address:-");
      }
      if ((multimediaLinkList != null) && (multimediaLinkList.Count > 0))
      {
        trace.TraceInformation(" multimediaLinkList:" + multimediaLinkList.Count);
      }
      else
      {
        trace.TraceInformation(" multimediaLinkList:-");
      }
      if ((sourceList != null) && (sourceList.Count > 0))
      {
        trace.TraceInformation(" sourceList:" + sourceList.Count);
      }
      else
      {
        trace.TraceInformation(" sourceList:-");
      }
      if (specialRecordList != null)
      {
        trace.TraceInformation(" specialRecordList:" + specialRecordList.Count);
      }
      else
      {
        trace.TraceInformation(" specialRecordList:(none)");
      }

      trace.TraceInformation("Individual:" + xrefName + "-end");
    }

    public object Clone()
    {
      return MemberwiseClone();
    }

    public void MarkUpdate()
    {
      latestUpdate = DateTime.Now;
    }
    public DateTime GetLatestUpdate()
    {
      return latestUpdate;
    }

    [DataMember]
    private String xrefName;
    [DataMember]
    private PersonalNameClass personalName;
    [DataMember]
    private bool @public;

    [DataMember]
    private IndividualSexType sex;
    [DataMember]
    private Alive isAlive;
    [DataMember]
    private IList<FamilyXrefClass> familyChildList;
    [DataMember]
    private IList<FamilyXrefClass> familySpouseList;
    [DataMember]
    private IList<SubmitterXrefClass> submitterList;
    [DataMember]
    private IList<String> permanentRFN_List;
    [DataMember]
    private IList<NoteClass> noteList;
    [DataMember]
    private IList<NoteXrefClass> noteXrefList;
    [DataMember]
    private AddressClass address;
    [DataMember]
    private IList<IndividualEventClass> eventList;
    [DataMember]
    private IList<MultimediaLinkClass> multimediaLinkList;
    [DataMember]
    private IList<string> urlList;
    [DataMember]
    private IList<SourceDescriptionClass> sourceList;
    [DataMember]
    private IList<SourceXrefClass> sourceXrefList;
    //[DataMember]
    //private String automatedRecordId;
    [DataMember]
    private int hashCode;
    [DataMember]
    private bool hashCodeValid;
    [DataMember]
    private IDictionary<IndividualSpecialRecordIdType, string> specialRecordList;

    private DateTime latestUpdate;
  }

  [DataContract]
  public class IndividualXrefClass : BaseXrefClass
  {
    //private static TraceSource trace = new TraceSource("IndividualXrefClass", SourceLevels.Warning);
    [DataMember]
    private PedigreeType pedigreeType;

    public IndividualXrefClass(String name, PedigreeType pedigreeType = PedigreeType.Birth) : base(XrefType.Individual, name)
    {
      //xrefName = name;
      this.pedigreeType = pedigreeType;
    }
    public PedigreeType GetPedigreeType()
    {
      return this.pedigreeType;
    }
  }

}
