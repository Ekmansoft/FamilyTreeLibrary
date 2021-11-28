using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Ekmansoft.FamilyTree.Library.FamilyData
{
  [DataContract]
  public class SubmitterClass
  {
    private static TraceSource trace = new TraceSource("SubmitterClass", SourceLevels.Warning);
    [DataMember]
    private AddressClass address;
    [DataMember]
    private String xrefName;
    [DataMember]
    private PersonalNameClass personalName;
    [DataMember]
    private IList<IndividualEventClass> eventList;

    public SubmitterClass()
    {
      personalName = new PersonalNameClass();

    }
    public void SetPersonalName(PersonalNameClass name)
    {
      //trace.TraceInformation("IndividualClass.SetPersonalName(" + name.GetName() + ")");
      personalName = name;
      personalName.SanityCheck();
    }
    public PersonalNameClass GetPersonalName()
    {
      return personalName;
    }
    public String GetName()
    {
      return personalName.GetName();
    }
    public void AddAddress(AddressPartClass.AddressPartType AddressPartType, String inAddress)
    {
      if (address == null)
      {
        address = new AddressClass();
      }
      address.AddAddressPart(new AddressPartClass(AddressPartType, inAddress));
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
    public AddressClass GetAddress()
    {
      if (address == null)
      {
        return new AddressClass();
      }
      return address;
    }
    public void AddEvent(IndividualEventClass eventData)
    {
      if (eventList == null)
      {
        eventList = new List<IndividualEventClass>();
      }
      eventList.Add(eventData);
    }
    public IList<IndividualEventClass> GetEventList()
    {
      if (eventList == null)
      {
        return new List<IndividualEventClass>();
      }
      return eventList;
    }
    public void SetXrefName(String name)
    {
      //trace.TraceInformation("IndividualClass.SetXrefName(" + name + ")");

      xrefName = name;
    }
    public String GetXrefName()
    {
      return xrefName;
    }
    public void Print()
    {
      trace.TraceInformation(GetName().ToString());
    }
  }
  [DataContract]
  public class SubmitterXrefClass : BaseXrefClass
  {
    public SubmitterXrefClass(String name) : base(XrefType.Submitter, name)
    {
    }
  }

}
