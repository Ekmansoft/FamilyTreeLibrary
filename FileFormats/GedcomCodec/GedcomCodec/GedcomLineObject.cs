using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FamilyTreeLibrary.FileFormats.GedcomCodec
{
  class GedcomLineObject
  {
    private static TraceSource trace = new TraceSource("GedcomLineObject", SourceLevels.Warning);
    //public String xrefIdString;
    public IList<GedcomLineData> gedcomLines;
    //public int parseLineNo;
    public GedcomLineObject parent;
    //public GedcomLineObject child;
    private int level;
    //private GedcomFileCharacterSet characterSet;

    public GedcomLineObject(int inLevel)
    {
      //xrefIdString = "";
      //parseLineNo = 0;
      gedcomLines = new List<GedcomLineData>();
      parent = null;
      //child = null;
      level = inLevel;
      //characterSet = inCharacterSet;
    }
    public void Print()
    {
      if (trace.Switch.Level.HasFlag(SourceLevels.Information))
      {
        trace.TraceInformation("GedcomLineObject(" + level + "," + gedcomLines.Count + ")");
        //trace.TraceInformation("  xref  [" + xrefIdString + "]");

        for (int i = 0; i < gedcomLines.Count; i++)
        {
          //gedcomLines[i].Print();
          trace.TraceInformation(gedcomLines[i].tagString);

          if (gedcomLines[i].child != null)
          {
            trace.TraceInformation(" child[" + gedcomLines[i].child.GetLevel() + " " + gedcomLines[i].child.gedcomLines.Count + "]");
          }
          trace.TraceInformation("");
        }
      }
    }
    public void PrintShort()
    {
      //PrintSpaces(level);
      //trace.TraceInformation("xref[" + xrefIdString + "]");

      for (int i = 0; i < gedcomLines.Count; i++)
      {
        gedcomLines[i].PrintShort();
      }
    }
    public void ObjectDecodeStart(String objectName, GedcomLineObject gedcomLineObject)
    {
      if(!trace.Switch.Level.HasFlag(SourceLevels.Information))
      {
        return;
      }
      if (objectName != null)
      {
        trace.TraceInformation(objectName + ":start  ===============================================");
      }

      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        if (lineData.xrefIdString.Length > 0)
        {
          trace.TraceInformation("  xref: " + lineData.xrefIdString + " ");
        }
        //trace.TraceInformation("  individual-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
        trace.TraceInformation(lineData.lineNo + " " + lineData.tagString + " " + lineData.valueString + " ");
        if (lineData.child != null)
        {
          ObjectDecodeStart(null, lineData.child);
        }
      }
      if (objectName != null)
      {
        trace.TraceInformation(objectName + ":end    ===============================================");
      }
    }
    public int GetLevel()
    {
      return level;
    }

    public void Clear()
    {
      while(gedcomLines.Count > 0)
      {
        gedcomLines.RemoveAt(0);
      }
    }
  };
}
