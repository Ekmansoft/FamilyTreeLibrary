using System;
//using System.Collections.Generic;
using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace FamilyTreeLibrary.FamilyData
{
  [DataContract]
  public enum XrefType
  {
    Individual,
    Family,
    Multimedia,
    Note,
    Repository,
    Source,
    Submission,
    Submitter
  }
  [DataContract]
  public abstract class BaseXrefClass
  {
    private static TraceSource trace = new TraceSource("BaseXrefClass", SourceLevels.Warning);

    [DataMember]
    private XrefType type;
    [DataMember]
    private String xrefName;

    public BaseXrefClass(XrefType type, string xref)
    {
      this.type = type;
      xrefName = xref;
    }

    public String GetXrefName()
    {
      return xrefName;
    }
    public void SetXrefName(String name)
    {
      xrefName = name;
    }
    public void Print()
    {
      trace.TraceInformation("xref:" + type + ":[" + xrefName + "]");
    }
    public override int GetHashCode()
    {
      int hashCode = 0;
      for (int i = 0; i < xrefName.Length; i++)
      {
        hashCode += (int)((xrefName[i] - '0') * (int)Math.Pow(10, (xrefName.Length - i - 1)));
      }
      return hashCode;
    }
    public override string ToString()
    {
      return type + ":" + xrefName;
    }
  }
}
