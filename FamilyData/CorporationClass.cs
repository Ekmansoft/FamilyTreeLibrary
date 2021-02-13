using System;
using System.Runtime.Serialization;

namespace FamilyTreeLibrary.FamilyData
{
  [DataContract]
  public class CorporationClass
  {
    [DataMember]
    public String name;
    [DataMember]
    public AddressClass address;

    public CorporationClass()
    {
      name = "";
      address = new AddressClass();
    }
  }
}
