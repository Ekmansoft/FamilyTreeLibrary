using System.Runtime.Serialization;

namespace Ekmansoft.FamilyTree.Library.FamilyData
{
  [DataContract]
  public class FlagOpticsClass
  {
    public enum Shape
    {
      Dot,
      Flag
    }
    public enum Color
    {
      Yellow,
      Green,
      Red,
      Blue,
      Black
    }
    public Shape shape;
    public Color color;

    public FlagOpticsClass()
    {
      shape = Shape.Dot;
      color = Color.Yellow;
    }
  }
  [DataContract]
  public class IndividualFilterClass
  {
    public string commentTextString;
    public FlagOpticsClass flagOptics;


    public IndividualFilterClass()
    {
      commentTextString = "";
      flagOptics = new FlagOpticsClass();
    }
  }
}
