using Ekmansoft.FamilyTree.Library.FamilyData;
using System;
using System.Runtime.Serialization;
using System.Text.Json;

namespace Ekmansoft.FamilyTree.Library.FamilyTreeStore
{
  public class SearchDescriptor
  {
    public SearchDescriptor()
    {
      FirstName = null;
      LastName = null;
      BirthName = null;
      BirthDate = null;
      DeathDate = null;
    }
    [DataMember]
    public string FirstName;
    [DataMember]
    public string LastName;
    [DataMember]
    public string BirthName;
    [DataMember]
    public string BirthDate;
    [DataMember]
    public string DeathDate;

    public static SearchDescriptor FromJson(string json)
    {
      if (json.Length == 0)
      {
        return null;
      }
      if (json[0] != '{')
      {
        return null;
      }
      return JsonSerializer.Deserialize<SearchDescriptor>(json);
    }
    public static string ToJson(SearchDescriptor data)
    {
      return JsonSerializer.Serialize(data);
    }
    public static SearchDescriptor GetSearchDescriptor(IndividualClass person)
    {
      SearchDescriptor searchDescriptor = new SearchDescriptor();

      searchDescriptor.FirstName = person.GetPersonalName().GetName(PersonalNameClass.PartialNameType.GivenName);
      searchDescriptor.LastName = person.GetPersonalName().GetName(PersonalNameClass.PartialNameType.Surname);
      searchDescriptor.BirthName = person.GetPersonalName().GetName(PersonalNameClass.PartialNameType.BirthSurname);
      if (searchDescriptor.BirthName.Length == 0)
      {
        searchDescriptor.BirthName = null;
      }
      IndividualEventClass birth = person.GetEvent(IndividualEventClass.EventType.Birth);
      if (!birth.badDate && birth.GetDate().ValidDate())
      {
        DateTime date = birth.GetDate().ToDateTime();
        searchDescriptor.BirthDate = date.ToString("yyyyMMdd");
      }
      IndividualEventClass death = person.GetEvent(IndividualEventClass.EventType.Death);
      if (!death.badDate && death.GetDate().ValidDate())
      {
        DateTime date = death.GetDate().ToDateTime();
        searchDescriptor.DeathDate = date.ToString("yyyyMMdd");
      }

      return searchDescriptor;
    }

  }
}
