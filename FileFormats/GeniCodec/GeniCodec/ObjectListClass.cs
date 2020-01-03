using System;
using System.Timers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FamilyTreeLibrary.FileFormats.GeniCodec
{

  public class FamilyAuthenticationEvent : EventArgs
  {
    public string url;
    public FamilyAuthenticationEvent(string inUrl)
    {
      this.url = inUrl;
    }
  }

  public class FamilyTimer : System.Timers.Timer
  {
    
  }

  // This may vbe obsolete...
  enum ObjectParserState
  {
    Null,
    TagStart,
    Tag,
    TagEnd,
    Assign,
    Value,
    ValueQuotedStart,
    ValueQuoted,
    ValueQuotedEnd,

  }

  enum EscapeMode
  {
    Null,
    UnknownYet,
    Quote,
    HexNumerical
  };

  class ObjectList
  {
    private static TraceSource trace = new TraceSource("Geni.ObjectList..obsolete", SourceLevels.Warning);
    //private ObjectList child;
    static int objNo;

    private IList<ObjectList> childList;
    private ObjectList parent;
    private string tag;
    private string value;
    public ObjectParserState parserState;
    private int level;
    private bool valueList;
    private bool printDecode;

    public ObjectList()
    {
      //child = null;
      childList = null;
      parent = null;
      tag = null;
      value = null;
      parserState = ObjectParserState.Null;
      objNo++;
      level = 0;
      valueList = false;
      printDecode = false;
      //printDecode = true;
    }
    public void SetTag(string tagStr)
    {
      tag = tagStr;
    }
    public string GetTag()
    {
      if (tag != null)
      {
        return tag;
      }
      return "";
    }
    public void SetValue(string valueStr)
    {
      value = valueStr;
      /*if(tag == "guid")
      {
        trace.TraceInformation("level=" + level + "guid=" + valueStr);
      }*/
    }
    public string GetValue()
    {
      if (value != null)
      {
        return value;
      }
      return "";
    }
    public void SetParent(ObjectList parentObj)
    {
      parent = parentObj;
      level = parent.level + 1;
    }
    /*public void SetChild(ObjectList childObj)
    {
      child = childObj;
      child.SetParent(this);
    }*/
    public ObjectList AddChild(ObjectList parentObj)
    {
      //trace.TraceInformation("AddChild()" + parentObj.level);
      if (parentObj.childList == null)
      {
        parentObj.childList = new List<ObjectList>();
      }
      ObjectList child = new ObjectList();
      parentObj.childList.Add(child);
      child.SetParent(parentObj);

      return child;
    }
    public IList<ObjectList> GetChildList()
    {
      if (childList != null)
      {
        return childList;
      }
      return new List<ObjectList>();
    }
    /*public void AddSibling(ObjectList childObj)
    {
      trace.TraceInformation("Addsibling()" + this.level);
      if (parent != null)
      {
        ObjectList sibling = new ObjectList();
        sibling.SetParent(parent);
        parent.AddChild(sibling);
      }
      else
      {
        trace.TraceInformation("Addsibling without parent curr:{0}, state:{1}", this, this.parserState);
      }
    }*/

    public override string ToString()
    {
      string tStr = "l:" + level;

      if (parserState != ObjectParserState.Null)
      {
        tStr += ",p:" + parserState.ToString();
      }

      if (tag != null)
      {
        tStr += ",tag:[" + tag + "]";
      }
      if (value != null)
      {
        tStr += ",value:[" + value + "]";
      }
      /*if (child != null)
      {
        tStr += "\nchild:" + child;
      }*/
      if (childList != null)
      {
        tStr += ",childList:" + childList.Count + ":";
        /*foreach (ObjectList obj in childList)
        {
          //tStr += "\nchild:";
          tStr += obj.ToString();
        }*/
      }
      return tStr;
    }

    public void PrintFull()
    {
      trace.TraceInformation(ToString());
      if (childList != null)
      {
        foreach (ObjectList child in childList)
        {
          child.PrintFull();
        }
      }
    }
    private int CharToHex(char ch)
    {
      if ((ch >= '0') && (ch <= '9'))
      {
        return ch - '0';
      }
      if ((ch >= 'a') && (ch <= 'f'))
      {
        return ch - 'a' + 10;
      }
      if ((ch >= 'A') && (ch <= 'F'))
      {
        return ch - 'A' + 10;
      }
      return 0;
    }
    public ObjectList ParseGeniObjectList(string objectListString)
    {
      ObjectList rootObject = new ObjectList();
      ObjectList currentObject = AddChild(rootObject);
      //int cnt = 0;
      string tTag = "";
      string tValue = "";
      //bool skipNextQuotation = false;
      EscapeMode escapeMode = EscapeMode.Null;
      string escapeString = "";

      if (printDecode)
      {
        trace.TraceInformation("ParseGeniObjectList({0})", objectListString.Length);
        trace.TraceInformation("**********************************************************-start");
        trace.TraceInformation(objectListString);
        trace.TraceInformation("**********************************************************-end");
      }


      for (int cnt = 0; cnt < objectListString.Length; cnt++)
      {
        char ch = objectListString[cnt];

        //trace.TraceInformation("{0}, char:{1} ", currentObject, ch);

        switch (currentObject.parserState)
        {
          case ObjectParserState.Null:
            if (ch == '{')
            {
              //ObjectList child = new ObjectList();

              //child.SetParent(currentObject);
              currentObject = AddChild(currentObject);
              //currentObject = child;
              //currentObject.parserState = ObjectParserState.TagStart;
            }
            else if (ch == ',')
            {
              //ObjectList sibling = new ObjectList();

              //sibling.SetParent(currentObject.parent);
              if (currentObject.parent != null)
              {
                currentObject = AddChild(currentObject.parent);
              }
              else
              {
                trace.TraceInformation("No Parent! curr:{0}, state:{1}, char:{2} ", currentObject, currentObject.parserState, ch);
              }
              //currentObject = sibling;
              //currentObject.parserState = ObjectParserState.TagStart;
            }
            else if (ch == '"')
            {
              currentObject.parserState = ObjectParserState.Tag;
            }
            else if ((ch == '}') || (ch == ']'))
            {
              if (currentObject.parent != null)
              {
                //trace.TraceInformation("<==ToParent:" + currentObject.level + "char:" + cnt + "/" + objectListString.Length);
                currentObject = currentObject.parent;
              }
              else
              {
                trace.TraceInformation("No Parent! curr:{0}, state:{1}, char:{2} ", currentObject, currentObject.parserState, ch);
              }

            }
            else
            {
              trace.TraceInformation("Unhandled character curr:{0}, state:{1}, char:{2} ", currentObject, currentObject.parserState, ch);
            }
            break;
          case ObjectParserState.TagStart:
            if (ch == '"')
            {
              currentObject.parserState = ObjectParserState.Tag;
            }
            else
            {
              trace.TraceInformation("Unhandled character curr:{0}, state:{1}, char:{2} ", currentObject, currentObject.parserState, ch);
            }
            break;
          case ObjectParserState.Tag:
            if (ch == '"')
            {
              currentObject.parserState = ObjectParserState.TagEnd;
            }
            else
            {
              tTag += ch;
            }
            break;
          case ObjectParserState.TagEnd:
            if (ch == ':')
            {
              currentObject.SetTag(tTag);
              tTag = "";
              currentObject.parserState = ObjectParserState.Assign;
            }
            else
            {
              trace.TraceInformation("Unhandled character curr:{0}, state:{1}, char:{2} ", currentObject, currentObject.parserState, ch);
            }
            break;
          case ObjectParserState.Assign:
            if (ch == '"')
            {
              currentObject.parserState = ObjectParserState.ValueQuoted;
            }
            else if (ch == '{')
            {
              //ObjectList child = new ObjectList();

              //child.SetParent(currentObject);
              currentObject.parserState = ObjectParserState.Null;
              currentObject = AddChild(currentObject);
              //currentObject = child;
              //
            }
            else if (ch == '[')
            {
              //ObjectList child = new ObjectList();

              currentObject.parserState = ObjectParserState.Null;
              //child.SetParent(currentObject);
              currentObject = AddChild(currentObject);
              //currentObject = child;
              currentObject.parserState = ObjectParserState.Assign;
              currentObject.valueList = true;
            }
            else
            {
              currentObject.parserState = ObjectParserState.Value;
              tValue += ch;
            }
            break;
          case ObjectParserState.ValueQuotedStart:
            if (ch == '"')
            {
              currentObject.parserState = ObjectParserState.ValueQuoted;
            }
            else
            {
              trace.TraceInformation("Unhandled character curr:{0}, state:{1}, char:{2} ", currentObject, currentObject.parserState, ch);
            }
            break;
          case ObjectParserState.ValueQuoted:
            if (escapeMode != EscapeMode.Null)
            {
              if (escapeMode == EscapeMode.UnknownYet)
              {
                switch (ch)
                {
                  case '"':
                    escapeMode = EscapeMode.Null;
                    tValue += ch;
                    break;
                  case 'u':
                    escapeMode = EscapeMode.HexNumerical;
                    escapeString = "";
                    break;
                }
              }
              else if (escapeMode == EscapeMode.HexNumerical)
              {
                if (escapeString.Length < 4)
                {
                  escapeString += ch;
                }
                if (escapeString.Length == 4)
                {
                  int charValue = 0;

                  for (int i = 0; i < 4; i++)
                  {
                    charValue = (charValue << 4) + CharToHex(escapeString[i]);
                  }
                  tValue += (char)charValue;
                  escapeMode = EscapeMode.Null;
                }
              }
            }
            else if (ch == '"')
            {
              currentObject.SetValue(tValue);
              tValue = "";
              currentObject.parserState = ObjectParserState.ValueQuotedEnd;
            }
            else if (ch == '\\')
            {
              escapeMode = EscapeMode.UnknownYet;
            }
            else
            {
              tValue += ch;
            }
            break;
          case ObjectParserState.ValueQuotedEnd:
            if (ch == ',')
            {
              bool listMode = currentObject.valueList;
              //ObjectList sibling = new ObjectList();

              //sibling.SetParent(currentObject.parent);
              currentObject.parserState = ObjectParserState.Null;
              currentObject = AddChild(currentObject.parent);
              //currentObject.parserState = ObjectParserState.Null;
              //currentObject = sibling;
              if (listMode)
              {
                currentObject.valueList = true;
                currentObject.parserState = ObjectParserState.Assign;

              }
            }
            else if ((ch == '}') || (ch == ']'))
            {
              if (currentObject.parent != null)
              {
                currentObject.parserState = ObjectParserState.Null;
                //trace.TraceInformation("<==ToParent:" + currentObject.level);
                currentObject = currentObject.parent;
              }
              else
              {
                trace.TraceInformation("No Parent! curr:{0}, state:{1}, char:{2} ", currentObject, currentObject.parserState, ch);
              }

            }
            else
            {
              trace.TraceInformation("Unhandled character curr:{0}, state:{1}, char:{2} ", currentObject, currentObject.parserState, ch);
            }
            break;
          case ObjectParserState.Value:
            if (ch == ',')
            {
              currentObject.SetValue(tValue);
              tValue = "";
              //ObjectList sibling = new ObjectList();

              //sibling.SetParent(currentObject.parent);
              currentObject.parserState = ObjectParserState.Null;
              currentObject = AddChild(currentObject.parent);
              //currentObject.parserState = ObjectParserState.Null;
              //currentObject = sibling;
            }
            else if (ch == '}')
            {
              currentObject.SetValue(tValue);
              tValue = "";
              currentObject.parserState = ObjectParserState.Null;
              //ObjectList sibling = new ObjectList();

              //sibling.SetParent(currentObject.parent);
              //trace.TraceInformation("<==ToParent:" + currentObject.level + "char:" + cnt + "/" + objectListString.Length);
              currentObject = currentObject.parent;
              //currentObject.parserState = ObjectParserState.Null;
              //currentObject = sibling;
            }
            else
            {
              tValue += ch;
            }
            break;

          default:
            {
              trace.TraceInformation("Unhandled character curr:{0}, state:{1}, char:{2} ", currentObject, currentObject.parserState, ch);
            }
            break;

        }
      }
      if (printDecode)
      {
        trace.TraceInformation("currentObj:" + currentObject);

        trace.TraceInformation("rootObj:" + rootObject);

        rootObject.PrintFull();
      }

      return rootObject;
    }
  }


}
