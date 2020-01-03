using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FamilyTreeLibrary.FileFormats.GedcomCodec
{

  public enum GedcomFileCharacterSet
  {
    Unknown,
    Ascii,
    Ansel,
    Unicode, // what is this?
    Utf16BE,
    Utf16LE,
    Utf8
  };
  class GedcomLineData
  {
    private static TraceSource trace = new TraceSource("GedcomLineData", SourceLevels.Warning);
    public String xrefIdString;
    public String tagString;
    public String valueString;
    public int level;
    public uint lineNo;
    public bool valid;
    //public IList<GedcomLineData> subLines;
    //private GedcomFileCharacterSet characterSet;

    public GedcomLineObject child;

    public GedcomLineData(GedcomFileCharacterSet inCharacterSet = GedcomFileCharacterSet.Ascii)
    {
      xrefIdString = "";
      tagString = "";
      valueString = "";
      //subLines = new List<GedcomLineData>();
      child = null;
      //characterSet = inCharacterSet;
      valid = false;
    }
    public void Print()
    {
      trace.TraceInformation("GedcomLineData");
      trace.TraceInformation("  lineNo[" + lineNo + "]");
      trace.TraceInformation("  level [" + level + "]");
      trace.TraceInformation("  tag   [" + tagString + "]");
      trace.TraceInformation("  value [" + valueString + "]");
      trace.TraceInformation("  xref  [" + xrefIdString + "]");
      //trace.TraceInformation("  sub   [" + subLines.Count + "]");

      if (child != null)
      {
        //child.Print();
        trace.TraceInformation("  child :" + child.gedcomLines.Count);

      }
    }
    public void PrintShort()
    {
      trace.TraceInformation(tagString);

      if (child != null)
      {
        child.PrintShort();
        //trace.TraceInformation("  child :" + child.gedcomLines.Count);

      }
    }
    public override string ToString()
    {
      return lineNo + ": [" + level + " " + tagString + " " + xrefIdString + " " + valueString + "]";
    }
    /*public void SetCharacterSet(GedcomFileCharacterSet inCharacterSet)
    {
      characterSet = inCharacterSet;
    }*/

  };
}
