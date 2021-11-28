using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Ekmansoft.FamilyTree.Library.FamilyData
{
  [DataContract]
  public class AddressPartClass
  {
    public enum AddressPartType
    {
      //Unknown,

      StreetAddress,
      Line1,
      Line2,
      City,
      PostCode,
      //Place,
      //Location,
      State,
      //County, // gedcom Geni.com
      Country,

      PhoneNumber,
      //EmailAddress,
      //WebAddress,

      //Note
    }
    [DataMember]
    private AddressPartType type;
    [DataMember]
    private String address;

    public AddressPartClass(AddressPartType inType, String inAddress)
    {
      type = inType;
      address = inAddress;

    }
    public override string ToString()
    {
      return address;
    }
    public AddressPartType GetAddressPartType()
    {
      return type;
    }
  }
  [DataContract]
  public class AddressClass
  {
    [DataMember]
    private IList<AddressPartClass> addressList;

    public AddressClass()
    {
      addressList = new List<AddressPartClass>();

    }
    public void AddAddressPart(AddressPartClass.AddressPartType inType, String inAddress)
    {
      AddressPartClass addressPart = new AddressPartClass(inType, inAddress);
      addressList.Add(addressPart);

    }
    public void AddAddressPart(AddressPartClass inAddress)
    {
      addressList.Add(inAddress);

    }
    public IList<AddressPartClass> GetAddressPartList()
    {
      return addressList;
    }
    public AddressPartClass GetAddressPart(AddressPartClass.AddressPartType type)
    {
      if (addressList != null)
      {
        foreach (AddressPartClass addressPart in addressList)
        {
          if (addressPart.GetAddressPartType() == type)
          {
            return addressPart;
          }
        }
      }
      return null;
    }
    public override string ToString()
    {
      string tString = "";

      foreach (AddressPartClass addressPart in addressList)
      {
        tString += " " + addressPart.ToString();
      }
      return tString;
    }
  }
}
