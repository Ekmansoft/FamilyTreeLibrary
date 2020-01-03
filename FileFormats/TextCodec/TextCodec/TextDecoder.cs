using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using FamilyTreeLibrary.FamilyData;
//using FamilyTreeLibrary.FamilyFileFormat;
using FamilyTreeLibrary.FileFormats;
using FamilyTreeLibrary.FamilyTreeStore;

namespace FamilyTreeLibrary.FileFormats.TextCodec
{

  public class TextDecoder : FamilyFileTypeBaseClass
  {
    private static TraceSource trace = new TraceSource("TextDecoder", SourceLevels.Warning);
    private IFamilyTreeStoreBaseClass familyTree;
    private BackgroundWorker backgroundWorker;
    private bool printMemory;
    private FileBufferClass fileBuffer;
    private XrefMapLists xrefMapLists;
    private MemoryClass memory;
    //private TextParserSettings parserSettings;

    class XrefMapClass
    {
      public string newXref;
      public int noOfReferences;
      public int noOfDefinitions;

      public XrefMapClass(string newXref, bool defined = false)
      {
        this.newXref = newXref;
        this.noOfReferences = 0;
        this.noOfDefinitions = 0;
      }

      public void CheckAndSetDefined(bool defined)
      {
        if(defined)
        {
          noOfDefinitions++;
        }
        else
        {
          noOfReferences++;
        }
      }
    }

    class XrefMapList
    {
      private IDictionary<string, XrefMapClass> xrefMap;
      private IFamilyTreeStoreBaseClass familyTree;
      private XrefType type;

      public XrefMapList(IFamilyTreeStoreBaseClass familyTree, XrefType type)
      {
        xrefMap = new Dictionary<string, XrefMapClass>();
        this.familyTree = familyTree;
        this.type = type;
      }
      public string GetXRef(string fileXref, bool defined = false)
      {
        if (xrefMap.ContainsKey(fileXref))
        {
          xrefMap[fileXref].CheckAndSetDefined(defined);
          return xrefMap[fileXref].newXref;
        }
        string newXref = familyTree.CreateNewXref(type);
        xrefMap.Add(fileXref, new XrefMapClass(newXref, defined));
        xrefMap[fileXref].CheckAndSetDefined(defined);
        return newXref;
      }
      public void ShowIntegrity(TraceSource trace, bool verbose = false)
      {
        IDictionaryEnumerator enumerator = (IDictionaryEnumerator)xrefMap.GetEnumerator();
        int cnt = 0;
        int references = 0;
        int warnings = 0;

        while (enumerator.MoveNext())
        {
          XrefMapClass xrefData = (XrefMapClass)enumerator.Value;
          string oldXref = (string)enumerator.Key;
          if (xrefData.noOfDefinitions == 0)
          {
            if (verbose && (trace != null))
            {
              trace.TraceData(TraceEventType.Information, 0, type + ": Unknown xref: @" + oldXref + "@ referenced " + xrefData.noOfReferences + " times but never defined!");
            }
            warnings++;
          }
          else if (xrefData.noOfDefinitions != 1)
          {
            if (verbose && (trace != null))
            {
              trace.TraceData(TraceEventType.Information, 0, type + ": Unknown xref: @" + oldXref + "@ referenced " + xrefData.noOfReferences + " times and multiply defined " + xrefData.noOfDefinitions + " times!");
            }
            warnings++;
          }
          if (xrefData.noOfReferences == 0)
          {
            if (verbose && (trace != null))
            {
              trace.TraceData(TraceEventType.Information, 0, type + ": xref: @" + oldXref + "@ never referenced.");
            }
            warnings++;
          }

          cnt++;
          references += xrefData.noOfReferences;
        }
        if (trace != null)
        {
          trace.TraceData(TraceEventType.Information, 0, type + ": Checked : " + cnt + " xrefs with " + references + " references and " + warnings + " warnings,");
        }

      }
    }

    class XrefMapLists
    {
      private IFamilyTreeStoreBaseClass familyTree;
      private IDictionary<XrefType, XrefMapList> xrefLists;

      public XrefMapLists(IFamilyTreeStoreBaseClass familyTree)
      {
        this.familyTree = familyTree;
        xrefLists = new Dictionary<XrefType, XrefMapList>();
      }

      public XrefMapList GetMapper(XrefType type)
      {
        if (!xrefLists.ContainsKey(type))
        {
          xrefLists.Add(type, new XrefMapList(familyTree, type));
        }
        return xrefLists[type];
      }

      public string GetLocalXRef(XrefType type, string fileXref, bool defined = false)
      {
        return GetMapper(type).GetXRef(fileXref, defined);
      }

      public void Analyze(TraceSource trace)
      {
        foreach(XrefMapList xrefList in xrefLists.Values)
        {
          xrefList.ShowIntegrity(trace);
        }
      }

    }

    class BufferParseState
    {
      public bool pageBreak;

      private FileBufferClass fileBuffer;
      private int filePos;
      private byte[] fileDataBuffer;
      private List<string> pageBreakLines;
      private double progressPercent;

      public BufferParseState(FileBufferClass fileBuffer, List<string> pageBreakLines)
      {
        pageBreak = true;
        this.fileBuffer = fileBuffer;
        filePos = 0;

        fileDataBuffer = fileBuffer.GetBuffer();

        this.pageBreakLines = pageBreakLines;

        progressPercent = -1.0;
      }

      public char GetNextChar()
      {
        if (!EndOfFile())
        {
          char nextChar;
          do
          {
            nextChar = (char)fileDataBuffer[filePos++];

            if ((nextChar == '\n') || (nextChar == '\r'))
            {
              pageBreak = true;
            }
            else
            {
              pageBreak = false;
            }
          } while (IsThisLinePageBreak() && !EndOfFile());
          return nextChar;
        }
        return '\0';
      }

      private bool IsThisLinePageBreak()
      {
        if(EndOfFile())
        {
          return true;
        }
        if ((fileDataBuffer[filePos] == '\n') || (fileDataBuffer[filePos] == '\r'))
        {
          return false;
        }
        for (int i = 0; i < pageBreakLines.Count; i++)
        {
          string searchStr = pageBreakLines[i];
          int pos = filePos;

          while (pos < fileDataBuffer.Length)
          {
            if (fileDataBuffer[pos] == searchStr[0])
            {
              bool match = true;
              int j = 0;
              foreach (char ch in searchStr)
              {
                if (ch != fileDataBuffer[pos + j++])
                {
                  match = false;
                  break;
                }
              }
              if (match)
              {
                byte ch;
                do
                {
                  ch = fileDataBuffer[filePos++];
                } while (((ch != '\n') && (ch != '\r')) && filePos < fileDataBuffer.Length);
                return true;
              }
            }
            if ((fileDataBuffer[pos] == '\n') || (fileDataBuffer[pos] == '\r'))
            {
              break;
            }
            pos++;
          }
        }
        return false;
      }

      public void MoveToPreviousLineStart()
      {
        while((filePos > 0) && (fileDataBuffer[filePos] != '\n')&& (fileDataBuffer[filePos] != '\r'))
        {
          filePos--;
        }
      }

      public bool EndOfFile()
      {
        if (filePos >= fileBuffer.GetSize())
        {
          return true;
        }
        return false;
      }

      public bool UpdateProgress()
      {
        double currentProgress = 100.0 * (double)filePos / (double)fileBuffer.GetSize();

        if(currentProgress - progressPercent > 0.2)
        {
          return true;
        }
        return false;
      }
      public double GetProgress()
      {
        double currentProgress = 100.0 * (double)filePos / (double)fileBuffer.GetSize();

        progressPercent = currentProgress;
        return progressPercent;
      }
    }

    enum ParsePersonState
    {
      Name,
      //FirstName,
      EventToken,
      Birth, 
      Baptism,
      Occupation,
      Lived,
      //Fosterchild,
      Move,
      Death,
      Burial,
      SpouseFamily,
      ChildFamily,
      Changed,
      Source,


    }
    [DataContract]
    class EventDataString
    {
      [DataMember]
      public ParsePersonState type;
      [DataMember]
      public string start;
      [DataMember]
      public string end;

      public EventDataString(ParsePersonState type, string start, string end)
      {
        this.type = type;
        this.start = start;
        this.end = end;
      }
    };

    [DataContract]
    class TextParserSettings
    {
      [DataMember]
      public List<EventDataString> eventList;
      [DataMember]
      public List<String> pageBreakStrings;
    }

    public override void Dispose()
    {
      //throw new NotImplementedException();
    }

    TextParserSettings GetParserSettings(string filename)
    {
      TextParserSettings settings = null;
      FileStream readSettings;
      try
      {
        readSettings = new FileStream(filename, FileMode.Open);
      }
      catch (FileNotFoundException e)
      {
        trace.TraceInformation("FileNotFoundException:" + e.ToString());
        readSettings = null;
      }

      if (readSettings != null)
      {
        DataContractSerializer serializer = new DataContractSerializer(typeof(TextParserSettings));
        try
        {
          settings = (TextParserSettings)serializer.ReadObject(readSettings);
        }
        catch (SerializationException e)
        {
          trace.TraceInformation("SerializationException:" + e.ToString());
        }
        readSettings.Close();
      }
      if (settings == null)
      {
        settings = new TextParserSettings();

        settings.eventList = new List<EventDataString>();
        settings.eventList.Add(new EventDataString(ParsePersonState.Birth, "Född ", ""));
        settings.eventList.Add(new EventDataString(ParsePersonState.Baptism, "Döpt ", ""));
        settings.eventList.Add(new EventDataString(ParsePersonState.Move, "Flyttade ", ". "));
        settings.eventList.Add(new EventDataString(ParsePersonState.Lived, "Levde ", ""));
        settings.eventList.Add(new EventDataString(ParsePersonState.Occupation, "(Yrke) ", ""));
        settings.eventList.Add(new EventDataString(ParsePersonState.Death, "Död ", ""));
        settings.eventList.Add(new EventDataString(ParsePersonState.Burial, "Begravd ", ". "));
        settings.eventList.Add(new EventDataString(ParsePersonState.Source, "Källa", ""));
        //settings.eventList.Add(new EventDataString(ParsePersonState.Fosterchild, "Fosterbarn ", ". "));
        settings.eventList.Add(new EventDataString(ParsePersonState.SpouseFamily, "[Partner i gifte ", ".] "));
        settings.eventList.Add(new EventDataString(ParsePersonState.ChildFamily, "[Barn i gifte ", ".] "));
        settings.eventList.Add(new EventDataString(ParsePersonState.Changed, "Ändrad ", ""));

        settings.pageBreakStrings = new List<string>();
        settings.pageBreakStrings.Add("            ");
        settings.pageBreakStrings.Add("------------");
        FileStream storeSettings = new FileStream(filename, FileMode.Create);

        DataContractSerializer serializer = new DataContractSerializer(typeof(TextParserSettings));

        serializer.WriteObject(storeSettings, (TextParserSettings)settings);
        storeSettings.Close();
      }
      return settings;
    }

    public TextDecoder()
    {
    
    }

    

    public bool ReadFile(String filename, ref IFamilyTreeStoreBaseClass inFamilyTree)
    {
      printMemory = false;// true;

      trace.TraceInformation("TextDecoder::Readfile(" + filename + ") Start " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

      familyTree = inFamilyTree;

      trace.TraceInformation(familyTree.GetShortTreeInfo());
      //familyTree.Print();

      if (printMemory)
      {
        memory = new MemoryClass();

        memory.PrintMemory();
      }

      xrefMapLists = new XrefMapLists(inFamilyTree);

      fileBuffer = new FileBufferClass();

      fileBuffer.ReadFile(filename);

      trace.TraceInformation("TextDecoder::Readfile() size " + fileBuffer.GetSize());
      if (printMemory)
      {
        memory.PrintMemory();
      }

      String HeadString = "";

      trace.TraceInformation("Text file " + filename + " read ok, size " + fileBuffer.GetSize());

      if (fileBuffer.GetSize() < 12)
      {
        trace.TraceInformation("Text file too small!: " + filename + ", size:" + fileBuffer.GetSize());
        return false;
      }

      Byte[] fileDataBuffer = fileBuffer.GetBuffer();

      for (int i = 0; i < 12; i++)
      {
        trace.TraceInformation(" data:" + (int)fileDataBuffer[i]);
        HeadString += (char)fileDataBuffer[i];
      }
      trace.TraceInformation("");

      familyTree.SetSourceFileType("Text");

      if (printMemory)
      {
        memory.PrintMemory();
      }

      if (printMemory)
      {
        memory.PrintMemory();
      }
      //familyTree.Print();
      trace.TraceData(TraceEventType.Verbose, 0, familyTree.GetShortTreeInfo());
      trace.TraceInformation("TextDecoder::Readfile() Done " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

      string directory = "/tmp/";
      if (!Directory.Exists(directory))
      {
        //Directory.CreateDirectory(directory);
        directory = "";
      }

      if ((directory.Length > 0) && filename.Contains(directory))
      {
        filename = filename.Substring(directory.Length);
      }
      Parse(directory + FamilyUtility.MakeFilename(filename + "_text_parsed_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".txt"));

      xrefMapLists.Analyze(trace);
      return true;

    }

    enum TextParseState
    {
      Start1,
      Start2,
      Start3,
      ReadPerson,
      DecodePerson,
      End,
    };


    enum DateParseState
    {
      Year,
      Month,
      Day
    }

    FamilyDateTimeClass ParseDateString(ref string dateStr)
    {
      string localDate = dateStr;
      bool approxDate = false;
      int dateStartPos = -1;
      int parsePos = 0;
      int pos;
      if((pos = localDate.IndexOf("omkring")) >= 0)
      {
        approxDate = true;
        dateStartPos = pos;
        parsePos = pos + 8; // strlen("omkring ")
      }

      if(parsePos == 0)
      {
        while(parsePos < dateStr.Length)
        {
          if((dateStr[parsePos] >= '0') && (dateStr[parsePos] <= '9'))
          {
            break;
          }
          parsePos++;
        }

      }
      if ((parsePos >= dateStr.Length) || (dateStr[parsePos] < '0') || (dateStr[parsePos] > '9'))
      {
        return null;
      }
      FamilyDateTimeClass date = new FamilyDateTimeClass();

      if(dateStartPos < 0)
      {
        dateStartPos = parsePos;
      }
      
      DateParseState state = DateParseState.Year;
      string partialString = "";
      int year = -1, month = -1, day = -1;
      while (parsePos < localDate.Length)
      {
        char ch = localDate[parsePos++];

        if((ch >= '0') && (ch <= '9'))
        {
          partialString += ch;
        }
        if (ch == '-')
        {
          switch(state)
          {
            case DateParseState.Year:
              if (partialString.Length > 0)
              {
                year = Convert.ToInt32(partialString);
                //dateType = FamilyDateTimeClass.FamilyDateType.Year;
                state = DateParseState.Month;
              }
              partialString = "";
              break;
            case DateParseState.Month:
              if (partialString.Length > 0)
              {
                month = Convert.ToInt32(partialString);
                //dateType = FamilyDateTimeClass.FamilyDateType.YearMonth;
                state = DateParseState.Day;
              }
              partialString = "";
              break;
            case DateParseState.Day:
              trace.TraceInformation("weird...");
              break;
          }
          
        }
        else if ((ch == ' ') || (parsePos >= (localDate.Length)))
        {
          if(partialString.Length > 0)
          {
            switch (state)
            {
              case DateParseState.Year:
                year = Convert.ToInt32(partialString);
                //dateType = FamilyDateTimeClass.FamilyDateType.Year;
                break;
              case DateParseState.Month:
                month = Convert.ToInt32(partialString);
                //dateType = FamilyDateTimeClass.FamilyDateType.YearMonth;
                break;
              case DateParseState.Day:
                day = Convert.ToInt32(partialString);
                //dateType = FamilyDateTimeClass.FamilyDateType.YearMonthDay;
                break;
            }
            partialString = "";
          }
          date = new FamilyDateTimeClass(year, month, day);
          date.SetApproximate(approxDate);
          string outString = "";

          if(dateStartPos > 0)
          {
            outString = dateStr.Substring(0, dateStartPos);
          }
          if(parsePos < localDate.Length)
          {
            outString += dateStr.Substring(parsePos);
          }
          if (outString.IndexOf(" i ") == 0)
          {
            outString = outString.Substring(3);
          }
          else if (outString.IndexOf("i ") == 0)
          {
            outString = outString.Substring(2);
          }
          dateStr = outString;
          return date;
        }


      }
      return null;

    }


    IndividualEventClass DecodeEventType(string eventString, IndividualEventClass.EventType evType, bool note)
    {
      IndividualEventClass ev = new IndividualEventClass(evType);
      FamilyDateTimeClass date;
      string tempString = eventString;

      date = ParseDateString(ref tempString);
      if (date != null)
      {
        ev.SetDate(date);
      }
      if (tempString.Length > 0)
      {
        if (note)
        {
          ev.AddNote(new NoteClass(tempString));
        }
        else
        {
          AddressClass address = new AddressClass();
          address.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, tempString);
          ev.AddAddress(address);
        }
      }
      trace.TraceInformation(evType + ":" + eventString + " => " + tempString + " " + ev);
      return ev;
    }


    void DecodeEvent(ref IndividualClass person, ParsePersonState evType, string eventString)
    {
      switch (evType)
      {
        case ParsePersonState.Birth:
          {
            person.AddEvent(DecodeEventType(eventString, IndividualEventClass.EventType.Birth, false));
          }
          break;
        case ParsePersonState.Baptism:
          {
            person.AddEvent(DecodeEventType(eventString, IndividualEventClass.EventType.Baptism, false));
          }
          break;
        case ParsePersonState.Death:
          {
            person.AddEvent(DecodeEventType(eventString, IndividualEventClass.EventType.Death, false));
          }
          break;

        case ParsePersonState.Burial:
          {
            person.AddEvent(DecodeEventType(eventString, IndividualEventClass.EventType.Burial, false));
          }
          break;

        case ParsePersonState.Occupation:
          {
            person.AddEvent(DecodeEventType(eventString, IndividualEventClass.EventType.Occupation, true));
          }
          break;

        /*case ParsePersonState.Fosterchild:
          {
            IndividualEventClass occupation = new IndividualEventClass(IndividualEventClass.EventType.Adoption);
            occupation.AddNote(eventString);
            trace.TraceInformation("fosterchild " + eventString + " => " + occupation);
            person.AddEvent(occupation);
          }
          break;*/

        case ParsePersonState.Move:
          {
            person.AddEvent(DecodeEventType(eventString, IndividualEventClass.EventType.Immigration, true));
          }
          break;

        case ParsePersonState.Source:
          {
            SourceDescriptionClass source = new SourceDescriptionClass(eventString);

            trace.TraceInformation("source " + eventString + " => " + source);
            person.AddSource(source);
          }
          break;

        case ParsePersonState.Lived:
          {
            person.AddEvent(DecodeEventType(eventString, IndividualEventClass.EventType.Residence, true));
          }
          break;

        case ParsePersonState.Changed:
          {
            person.AddEvent(DecodeEventType(eventString, IndividualEventClass.EventType.RecordUpdate, true));
          }
          break;

        case ParsePersonState.ChildFamily:
          {
            string familyId = "";

            foreach (char ch in eventString)
            {
              if ((ch >= '0') && (ch <= '9') || (ch == ':'))
              {
                familyId += ch;
              }
            }
            string familyXref = xrefMapLists.GetMapper(XrefType.Family).GetXRef(familyId, false);
            person.AddRelation(new FamilyXrefClass(familyXref), IndividualClass.RelationType.Child);
            trace.TraceInformation("child in " + familyId + "=" + familyXref);
            FamilyClass family = familyTree.GetFamily(familyXref);

            if(family == null)
            {
              family = new FamilyClass();
              family.SetXrefName(familyXref);
            }
            family.AddRelation(new IndividualXrefClass(person.GetXrefName()), FamilyClass.RelationType.Child);
            familyTree.AddFamily(family);
          }
          break;

        case ParsePersonState.SpouseFamily:
          {
            string familyId = "";

            foreach (char ch in eventString)
            {
              if ((ch >= '0') && (ch <= '9') || (ch == ':'))
              {
                familyId += ch;
              }
            }
            string familyXref = xrefMapLists.GetMapper(XrefType.Family).GetXRef(familyId, false);
            person.AddRelation(new FamilyXrefClass(familyXref), IndividualClass.RelationType.Spouse);
            trace.TraceInformation("spouse in " + familyId + "=" + familyXref);
            FamilyClass family = familyTree.GetFamily(familyXref);

            if (family == null)
            {
              family = new FamilyClass();
              family.SetXrefName(familyXref);
            }
            family.AddRelation(new IndividualXrefClass(person.GetXrefName()), FamilyClass.RelationType.Parent);
            familyTree.AddFamily(family);
          }
          break;


      }

    }

    class SubStringCompare : IComparer<SubStringInstance>
    {
      virtual public int Compare(SubStringInstance a, SubStringInstance b)
      {
        return -a.start.CompareTo(b.start);
      }
    }

    class SubStringInstance
    {
      public int start;
      public int end;
      public ParsePersonState type;
      public SubStringInstance(int start, int end, ParsePersonState type)
      {
        this.start = start;
        this.end = end;
        this.type = type;
      }
    }

    string FindSubString(string data, ref int offset, EventDataString str, ref List<SubStringInstance> subList)
    {
      int startPos = data.IndexOf(str.start);

      if (startPos >= 0)
      {
        if (str.end.Length > 0)
        {
          int endPos = data.Substring(startPos + str.start.Length).IndexOf(str.end);

          if (endPos >= 0)
          {
            endPos += startPos + str.start.Length + str.end.Length;

            subList.Add(new SubStringInstance(offset + startPos, offset + endPos, str.type));

            offset = offset + endPos;
            if (endPos < data.Length)
            {
              return data.Substring(endPos);
            }
          }
          else
          {
            trace.TraceInformation("No end found " + str.type + " " + data);

          }
        }
        else
        {
          int endPos = data.Length;

          subList.Add(new SubStringInstance(offset + startPos, offset + endPos, str.type));
        }
      }
      return null;
    }


    List<SubStringInstance> CheckSubstrings(string id, string data, TextParserSettings parserSettings)
    {
      List<SubStringInstance> subList = new List<SubStringInstance>();

      foreach(EventDataString str in parserSettings.eventList)
      {
        int offset = 0;
        string newSubString = FindSubString(data, ref offset, str, ref subList);

        while(newSubString != null)
        {
          newSubString = FindSubString(newSubString, ref offset, str, ref subList);
        }
      }

      //trace.TraceInformation(data);
      trace.TraceInformation("subs:" + subList.Count);
      //int totLength = 0;
      string usage = "";

      for (int i = 0; i < data.Length; i++)
      {
        usage += " ";
      }
      SubStringCompare comparer = new SubStringCompare();
      subList.Sort(comparer);

      int lastStart = -1;
      foreach (SubStringInstance inst in subList)
      {
        trace.TraceInformation(inst.type + " start:" + inst.start + " end:" + inst.end);
        if (lastStart >= 0)
        {
          if(lastStart < inst.end)
          {
            inst.end = lastStart;
          }
        }

        for(int i = inst.start; i < inst.end; i++)
        {
          string oldStr = usage.Substring(i, 1);

          if(oldStr != " ")
          {
            trace.TraceInformation("warning overlap at " + i);
          }

          usage = usage.Substring(0, i) + inst.type.ToString()[0] + usage.Substring(i + 1);
          //usage. = 'k'; //(char)inst.type.ToString()[0];

        }
        lastStart = inst.start;
      }
      trace.TraceInformation(id);
      trace.TraceInformation(data);
      trace.TraceInformation(usage);
      return subList;
    }

    void ParsePerson(string id, string data, TextParserSettings parserSettings)
    {
      ParsePersonState state = ParsePersonState.Name;

      IndividualClass person = new IndividualClass();

      person.SetXrefName(id);

      int strPos = 0;
      string nameStr = "";
      PersonalNameClass name = new PersonalNameClass();

      List<SubStringInstance> subList = CheckSubstrings(id, data, parserSettings);

      foreach(SubStringInstance item in subList)
      {
        EventDataString  thisType = null;

        foreach(EventDataString str in parserSettings.eventList)
        {
          if(str.type == item.type)
          {
            thisType = str;
          }
        }
        if(thisType != null)
        {
          DecodeEvent(ref person, item.type, data.Substring(item.start + thisType.start.Length, item.end - item.start - thisType.start.Length - thisType.end.Length));
        }
      }

      while(strPos < data.Length)
      {
        //string token = GetToken(ref strPos);
        char ch = data[strPos++];


        switch(state)
        {
          case ParsePersonState.Name:
            if(ch == '.')
            {
              int firstNameStart;
              int firstNameLength;
              int lastNameStart;
              int lastNameLength;
              if (nameStr.IndexOf(',') >= 0)
              {
                firstNameStart = nameStr.IndexOf(',') + 1;
                while ((firstNameStart < nameStr.Length) && (nameStr[firstNameStart] == ' '))
                {
                  firstNameStart++;
                }
                firstNameLength = nameStr.Length - firstNameStart;
                lastNameStart = 0;
                lastNameLength = nameStr.IndexOf(',');
              }
              else
              {
                firstNameStart = 0;
                if (nameStr.LastIndexOf(' ') >= 0)
                {
                  firstNameLength = nameStr.LastIndexOf(' ');
                  lastNameStart = firstNameLength + 1;
                  lastNameLength = nameStr.Length - firstNameLength - 1;
                }
                else
                {
                  firstNameLength = nameStr.Length;
                  lastNameStart = firstNameLength;
                  lastNameLength = 0;
                }
              }

              if (firstNameLength > 0)
              {
                name.SetName(PersonalNameClass.PartialNameType.GivenName, nameStr.Substring(firstNameStart, firstNameLength));
              }
              if (lastNameLength > 0)
              {
                name.SetName(PersonalNameClass.PartialNameType.BirthSurname, nameStr.Substring(lastNameStart, lastNameLength));
              }
              person.SetPersonalName(name);
              state = ParsePersonState.EventToken;
            }
            else
            {
              nameStr += ch;
            }
            break;

        }

      }
      person.Print();
      familyTree.AddIndividual(person);


    }



    private void Parse(string decodeFilename)
    {
      TextParseState state = TextParseState.Start1;
      int dashCount = 0;
      int lineFeedCount = 0;
      string parsedId = "";
      string parseString = "";

      FamilyUtility utility = new FamilyUtility();
      TextParserSettings parserSettings = GetParserSettings(utility.GetCurrentDirectory() + "\\TextDecoderSettings.xml");

      BufferParseState parseState = new BufferParseState(fileBuffer, parserSettings.pageBreakStrings);

      
      System.IO.StreamWriter personFile = new System.IO.StreamWriter(decodeFilename, false, Encoding.UTF8, 4096);


      while(!parseState.EndOfFile())
      {
        char ch = parseState.GetNextChar();

        switch (state)
        {
          case TextParseState.Start1:
            if (ch == '-')
            {
              dashCount++;
            }
            else if (ch != ' ')
            {
              dashCount = 0;
            }
            if ((dashCount >= 3) && (ch == ' '))
            {
              state = TextParseState.Start2;
              dashCount = 0;
            }
            break;
          case TextParseState.Start2:
            if ((ch >= '0') && (ch <= '9') || (ch == '.') || (ch == ':'))
            {
              parsedId += ch;
            }
            if ((parsedId != "") && (ch == ' '))
            {
              state = TextParseState.Start3;
            }
            break;
          case TextParseState.Start3:
            if (ch == '-')
            {
              dashCount++;
            }
            if (dashCount >= 3)
            {
              if (ch == '\r')
              {
                state = TextParseState.ReadPerson;
                dashCount = 0;
              }
            }
            break;
          case TextParseState.ReadPerson:
            if ((ch != '\r') && (ch != '\n'))
            {
              parseString += ch;
              lineFeedCount = 0;
            }
            else if (ch == '\r')
            {
              if (lineFeedCount++ == 0)
              {
                parseString += " ";
              }
              /*if(lineFeedCount >= 3)
              {
                lineFeedCount = 0;
                state = TextParseState.DecodePerson;
              }*/
            }

            if (ch == '-')
            {
              dashCount++;
            }
            else
            {
              dashCount = 0;
            }
            if (dashCount >= 3)
            {
              dashCount = 0;
              state = TextParseState.DecodePerson;
            }
            break;
          case TextParseState.DecodePerson:

            if (parseString.LastIndexOf("---") == (parseString.Length - 3))
            {
              parseString = parseString.Substring(0, parseString.Length - 3);
              //filePos -= 5;
              parseState.MoveToPreviousLineStart();
            }
            personFile.WriteLine(parsedId + ":" + parseString);
            string newXrefId = xrefMapLists.GetMapper(XrefType.Individual).GetXRef(parsedId, true);
            ParsePerson(newXrefId, parseString, parserSettings);
            //backgroundWorker;progresschanged

            parsedId = "";
            parseString = "";
            state = TextParseState.Start1;
            break;
          //case TextParseState.End:
          //  break;
        }
        if(parseState.UpdateProgress())
        {
          backgroundWorker.ReportProgress((int)parseState.GetProgress(), "Importing...");
        }
      }

      if ((parsedId.Length > 0) && (parseString.Length > 0))
      {
        string newXrefId = xrefMapLists.GetMapper(XrefType.Individual).GetXRef(parsedId, true);
        personFile.WriteLine(parsedId + ":" + newXrefId + ":" + parseString);
        ParsePerson(newXrefId, parseString, parserSettings);
        parsedId = "";
        parseString = "";
      }


      personFile.Close();

      backgroundWorker = null;
      trace.TraceInformation("Text file parsing finished at " );
    }

    public override bool IsKnownFileType(String fileName)
    {
      if (fileName.ToLower().Contains(".txt"))
      {
        return true;
      }
      return false;
    }

    public override IFamilyTreeStoreBaseClass CreateFamilyTreeStore(String fileName, CompletedCallback callback)
    {
      trace.TraceInformation("TextDecoder::CreateFamilyTreeStore( " + fileName + ")");
      callback(true);
      return null; // new FamilyTreeStoreRam();
    }

    public override bool OpenFile(String fileName, ref IFamilyTreeStoreBaseClass inFamilyTree, CompletedCallback callback)
    {
      trace.TraceInformation("TextDecoder::OpenFile( " + fileName + ")");
      bool result = ReadFile(fileName, ref inFamilyTree);
      callback(result);
      return result;
    }
    public override bool SetProgressTarget(BackgroundWorker inBackgroundWorker)
    {
      trace.TraceInformation("TextCodec::SetProgressTarget 2");
      backgroundWorker = inBackgroundWorker;
      return true;
    }
    public override string GetFileTypeFilter(FamilyFileTypeOperation operation)
    {
      if (operation == FamilyFileTypeOperation.Import)
      {
        return "Text|*.txt";
      }
      return null;
    }

  }
}
