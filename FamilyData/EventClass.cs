using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Ekmansoft.FamilyTree.Library.FamilyData
{
  [DataContract]
  public class IndividualEventClass
  {
    private static TraceSource trace = new TraceSource("IndividualEventClass", SourceLevels.Warning);
    public enum EventType
    {
      Unknown,
      Birth,

      Baptism,
      BaptismLDS,
      Christening,
      AdultChristening,

      Adoption,
      BarMitzwah,
      BasMitzwah,
      Blessing,
      FirstCommunion,
      Retired,

      Caste,
      PhysicalDescription,
      IdentityNumber,
      Nationality,
      NumberOfChildren,
      NumberOfMarriages,
      Possesions,
      Religion,
      SocialSecurityNumber,
      NobilityTitle,
      //Fact,
      //Military,

      Confirmation,
      Probate,
      Will,
      Naturalization,
      Census,
      GeneralEvent,
      Education,
      Graduation,
      Occupation,
      Ordination,
      Residence,
      Emigration,
      Immigration,

      Death,
      Cremation,
      Burial,

      Endowment,
      SealingChild,
      SealingSpouse,

      RecordUpdate,

      // Family events!
      FamEngagement,
      FamMarriageBann,
      FamMarriageContract,
      FamMarriageLicense,
      FamMarriage,
      FamMarriageSettlement,
      FamCensus,
      FamDivorceFiled,
      FamDivorce,
      FamAnnulment,
      FamGeneralEvent,
      FamRecordChange,

      AllEvents
    };

    public enum ParentType
    {
      Husband,
      Wife
    };

    [DataContract]
    public class ParentAgePropertyClass
    {
      [DataMember]
      public ParentType parent;
      [DataMember]
      public string ageAtEvent;

      public ParentAgePropertyClass(ParentType type, string age)
      {
        this.parent = type;
        this.ageAtEvent = age;
      }
    }
    [DataMember]
    private EventType eventType;
    [DataMember]
    private FamilyDateTimeClass date;
    [DataMember]
    private AddressClass address;
    [DataMember]
    private PlaceStructureClass place;
    [DataMember]
    private IList<NoteClass> noteList;
    [DataMember]
    private IList<NoteXrefClass> noteXrefList;
    [DataMember]
    private String eventTypeString;
    [DataMember]
    private FamilyXrefClass familyXref;
    [DataMember]
    private IList<SourceDescriptionClass> sourceList;
    [DataMember]
    private IList<SourceXrefClass> sourceXrefList;
    [DataMember]
    private IList<ParentAgePropertyClass> parentAgeList;
    [DataMember]
    private String cause;
    [DataMember]
    private IList<MultimediaLinkClass> multimediaLinkList;
    [DataMember]
    public bool badDate = false;



    public IndividualEventClass(EventType inEventType = EventType.Unknown, FamilyDateTimeClass inDate = null)
    {
      eventType = inEventType;
      if (inDate != null)
      {
        date = inDate;
        if (date.badYear)
        {
          badDate = true;
        }
      }
      else
      {
        date = new FamilyDateTimeClass();
      }

      //address = new AddressClass();
      //noteList = new List<NoteClass>();
      //noteXrefList = new List<NoteXrefClass>();
      //sourceList = new List<SourceDescriptionClass>();

    }
    /*    public IndividualEventClass(EventType inEventType = EventType.Unknown)
        {
          eventType = inEventType;
          date = new FamilyDateTimeClass();
          //address = new AddressClass();
          //noteList = new List<NoteClass>();
          //noteXrefList = new List<NoteXrefClass>();
          //sourceList = new List<SourceClass>();
        }*/

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
    public void AddPlace(PlaceStructureClass place)
    {
      this.place = place;
    }
    public PlaceStructureClass GetPlace()
    {
      return place;
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
    public void AddSource(SourceDescriptionClass source)
    {
      if (sourceList == null)
      {
        sourceList = new List<SourceDescriptionClass>();
      }
      sourceList.Add(source);
    }
    public IList<SourceDescriptionClass> GetSourceList()
    {
      return sourceList;
    }
    public void AddSourceXref(SourceXrefClass source)
    {
      if (sourceXrefList == null)
      {
        sourceXrefList = new List<SourceXrefClass>();
      }
      sourceXrefList.Add(source);
    }
    public IList<SourceXrefClass> GetSourceXrefList()
    {
      return sourceXrefList;
    }

    public void SetDate(FamilyDateTimeClass inDate)
    {
      if ((date != null) && date.ValidDate())
      {
        trace.TraceData(TraceEventType.Warning, 0, "Adding date, where date already exists:" + date.ToString() + " " + inDate.ToString());
      }
      date = inDate;
    }
    public FamilyDateTimeClass GetDate()
    {
      return date;
    }
    public void AddMultimediaLink(MultimediaLinkClass multimediaLink)
    {
      //trace.TraceInformation(this,"IndividualClass.SetDate(" + eventType + "," + date + ")");
      if (multimediaLinkList == null)
      {
        multimediaLinkList = new List<MultimediaLinkClass>();
      }
      multimediaLinkList.Add(multimediaLink);
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

    public void SetEventType(EventType inType)
    {
      eventType = inType;
    }
    public void SetEventType(String inType)
    {
      eventTypeString = inType;
    }
    public EventType GetEventType()
    {
      return eventType;
    }
    public void SetFamilyXref(FamilyXrefClass inFamilyXref)
    {
      familyXref = inFamilyXref;
    }
    public void SetCause(String inCause)
    {
      cause = inCause;
    }

    public void AddParentAge(ParentType type, string age)
    {
      if (parentAgeList == null)
      {
        parentAgeList = new List<ParentAgePropertyClass>();
      }
      parentAgeList.Add(new ParentAgePropertyClass(type, age));
    }

    public IList<ParentAgePropertyClass> GetParentAgeList()
    {
      return parentAgeList;
    }

    public string ToString(bool showType = true)
    {
      string tString = "";

      if (showType)
      {
        tString += eventType.ToString() + ":";
      }
      tString += date;

      if (address != null)
      {
        tString += address.ToString();
      }
      return tString;
    }

  }


}
