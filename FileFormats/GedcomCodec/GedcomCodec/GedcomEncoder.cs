using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.ComponentModel;
using FamilyTreeLibrary.FamilyData;
//using FamilyTreeLibrary.FamilyFileFormat;
using FamilyTreeLibrary.FamilyTreeStore;

namespace FamilyTreeLibrary.FileFormats.GedcomCodec
{
  public class GedcomEncoder : FamilyTreeStore.FamilyFileEncoder
  {
    private static TraceSource trace = new TraceSource("GedcomEncoder", SourceLevels.Warning);
    private Encoding enc;
    //private BackgroundWorker workerProgressTarget;
    private IProgressReporterInterface workerProgressTarget;

    class XrefMapperClass
    {
      //public string oldXref;
      public string newXref;
      public int noOfReferences;
      public int noOfDefinitions;
      public IList<XrefRelation> referencedFrom;

      public class XrefRelation
      {
        public string xref;
        public bool child;

        public XrefRelation(bool child, string xref)
        {
          this.xref = xref;
          this.child = child;
        }
      }

      public XrefMapperClass(string newXref, bool defined = false)
      {
        this.newXref = newXref;
        this.noOfReferences = 0;
        this.noOfDefinitions = 0;
        this.referencedFrom = new List<XrefRelation>();
      }

      public void CheckAndSetDefined(bool defined)
      {
        if (defined)
        {
          noOfDefinitions++;
        }
        else
        {
          noOfReferences++;
        }
      }
    }
    class GedcomMappers
    {
      private IFamilyTreeStoreBaseClass familyTree;
      private IDictionary<string, XrefMapperClass> individualXrefMapper;
      private IDictionary<string, XrefMapperClass> familyXrefMapper;
      private IDictionary<string, XrefMapperClass> multimediaXrefMapper;
      private IDictionary<string, XrefMapperClass> noteXrefMapper;
      private IDictionary<string, XrefMapperClass> repositoryXrefMapper;
      private IDictionary<string, XrefMapperClass> sourceXrefMapper;
      private IDictionary<string, XrefMapperClass> submissionXrefMapper;
      private IDictionary<string, XrefMapperClass> submitterXrefMapper;

      public GedcomMappers(IFamilyTreeStoreBaseClass familyTree)
      {
        this.familyTree = familyTree;
        individualXrefMapper = new Dictionary<string, XrefMapperClass>();
        familyXrefMapper = new Dictionary<string, XrefMapperClass>();
        multimediaXrefMapper = new Dictionary<string, XrefMapperClass>();
        noteXrefMapper = new Dictionary<string, XrefMapperClass>();
        repositoryXrefMapper = new Dictionary<string, XrefMapperClass>();
        sourceXrefMapper = new Dictionary<string, XrefMapperClass>();
        submissionXrefMapper = new Dictionary<string, XrefMapperClass>();
        submitterXrefMapper = new Dictionary<string, XrefMapperClass>();
      }

      public void AddReference(XrefType type, string fileXref, bool defined, string referencedFrom = null, bool child = false)
      {
        IDictionary<string, XrefMapperClass> mapper = GetMapper(type);
        if (!mapper.ContainsKey(fileXref))
        {
          mapper.Add(fileXref, new XrefMapperClass(fileXref, defined));
        }
        mapper[fileXref].CheckAndSetDefined(defined);
        if(referencedFrom != null)
        {
          mapper[fileXref].referencedFrom.Add(new XrefMapperClass.XrefRelation(child, referencedFrom));

        }
      }

      public IDictionary<string, XrefMapperClass> GetMapper(XrefType type)
      {
        switch (type)
        {
          case XrefType.Individual:
            return individualXrefMapper;

          case XrefType.Family:
            return familyXrefMapper;

          case XrefType.Multimedia:
            return multimediaXrefMapper;

          case XrefType.Note:
            return noteXrefMapper;

          case XrefType.Source:
            return sourceXrefMapper;

          case XrefType.Repository:
            return repositoryXrefMapper;

          case XrefType.Submission:
            return submissionXrefMapper;

          case XrefType.Submitter:
            return submitterXrefMapper;

          default:
            //DebugStringAdd("Unknown xref tag type:" + type);
            return null;
        }
      }

    }
    private GedcomMappers xrefMappers;

    public GedcomEncoder()
    {
      enc = new UTF8Encoding(true, true);

      //parent.SetProgressTarget();

      xrefMappers = new GedcomMappers(null);

    }

    private void WriteChar(FileStream file, char ch)
    {
      //string value = "\u00C4 \uD802\u0033 \u00AE";
      string value = "";

      value += ch;

      try
      {
        byte[] bytes = enc.GetBytes(value);

        /*if (bytes.Length > 1)
        {
          trace.TraceInformation("Encoded {0:X4} => {1:X4},{2:X4}", (int)ch, bytes[0], bytes[1]);

        }*/
        foreach (var byt in bytes)
        {
          file.WriteByte(byt);
          //Console.Write("{0:X2} ", byt);
        }
        //Console.WriteLine();

        //string value2 = enc.GetString(bytes);
        //Console.WriteLine(value2);
      }
      catch (EncoderFallbackException e)
      {
        trace.TraceInformation("Unable to encode {0} at index {1}",
                          e.IsUnknownSurrogate() ?
                             String.Format("U+{0:X4} U+{1:X4}",
                                           Convert.ToUInt16(e.CharUnknownHigh),
                                           Convert.ToUInt16(e.CharUnknownLow)) :
                             String.Format("U+{0:X4}",
                                           Convert.ToUInt16(e.CharUnknown)),
                          e.Index);
      }
    }

    private void WriteData(FileStream file, String inStr)
    {
      for (int i = 0; i < inStr.Length; i++)
      {
        char ch = inStr.ElementAt(i);

        WriteChar(file, ch);
      }
    }

    private string Linefeed()
    {
      return FamilyUtility.GetLinefeed();
    }

    private void WriteHeader(FileStream file, string filename)
    {
      byte [] bomMark_UTF8 = { 0xEF, 0xBB, 0xBF };
      DateTime now = DateTime.Now;
      FamilyDateTimeClass nowFam = new FamilyDateTimeClass(now.Date.Year, now.Date.Month, now.Date.Day);

      nowFam.SetTime(now.TimeOfDay.Hours, now.TimeOfDay.Minutes, now.TimeOfDay.Seconds);

      foreach (var byt in bomMark_UTF8)
      {
        file.WriteByte(byt);
        //Console.Write("{0:X2} ", byt);
      }
      // 0 @I6000000005790470652@ INDI
      /*
      1 SOUR Geni.com
      2 VERS 1.0
      1 DATE 24 FEB 2013
      2 TIME 02:44:33
      1 SUBM @S0@
      1 GEDC
      2 VERS 5.5
      2 FORM LINEAGE-LINKED
      1 CHAR UTF-8
      0 @I6000000005790470652@ INDI
       1 NAME Kenneth /Ekman/
        2 GIVN Kenneth
        2 SURN Ekman
       1 SEX M
       1 RESI
        2 PLAC Varvsgatan 10E
        2 ADDR
         3 CITY Skellefteå
         3 STAE (AC)
         3 POST 93134
         3 CTRY Sverige
         3 NOTE {geni:place_name} Varvsgatan 10E
       1 BIRT
        2 DATE 25 MAR 1965
        2 PLAC Sweden
        2 ADDR
         3 CTRY Sweden
            */
      WriteData(file, "0 HEAD" + Linefeed());
      if (filename != null)
      {
        WriteData(file, "1 SOUR FamilyStudio:" + filename + Linefeed());
      }
      else
      {
        WriteData(file, "1 SOUR FamilyStudio" + Linefeed());
      }
      WriteData(file, "2 VERS 0.8" + Linefeed());
      WriteData(file, "1 DATE " + nowFam.ToGedcomDateString() + Linefeed());
      WriteData(file, "2 TIME " + nowFam.ToGedcomTimeString() + Linefeed());
      WriteData(file, "1 SUBM jhkjh " + Linefeed());
      WriteData(file, "1 GEDC" + Linefeed());
      WriteData(file, "2 VERS 5.5" + Linefeed());
      WriteData(file, "2 FORM LINEAGE-LINKED" + Linefeed());
      WriteData(file, "1 CHAR UTF-8" + Linefeed());
    }

    private bool GetGedcomEventString(IndividualEventClass.EventType ev, ref string evStr)
    {
      switch (ev)
      {
        case IndividualEventClass.EventType.Birth:
          evStr = "BIRT";
          return true;
        case IndividualEventClass.EventType.Christening:
          evStr = "CHR";
          return true;
        case IndividualEventClass.EventType.BaptismLDS:
          evStr = "BAPL";
          return true;
        case IndividualEventClass.EventType.Baptism:
          evStr = "BAPM";
          return true;
        case IndividualEventClass.EventType.Adoption:
          evStr = "ADOP";
          return true;
        case IndividualEventClass.EventType.BarMitzwah:
          evStr = "BARM";
          return true;
        case IndividualEventClass.EventType.BasMitzwah:
          evStr = "BASM";
          return true;
        case IndividualEventClass.EventType.Blessing:
          evStr = "BLES";
          return true;
        case IndividualEventClass.EventType.AdultChristening:
          evStr = "CHRA";
          return true;

        case IndividualEventClass.EventType.Confirmation:
          evStr = "CONF";
          return true;
        case IndividualEventClass.EventType.Education:
          evStr = "EDUC";
          return true;
        case IndividualEventClass.EventType.Graduation:
          evStr = "GRAD";
          return true;
        case IndividualEventClass.EventType.Occupation:
          evStr = "OCCU";
          return true;
        case IndividualEventClass.EventType.FirstCommunion:
          evStr = "FCOM";
          return true;
        case IndividualEventClass.EventType.Ordination:
          evStr = "ORDN";
          return true;
        case IndividualEventClass.EventType.NobilityTitle:
          evStr = "TITL";
          return true;
        case IndividualEventClass.EventType.Nationality:
          evStr = "NATI";
          return true;
        case IndividualEventClass.EventType.NumberOfChildren:
          evStr = "NCHI";
          return true;
        case IndividualEventClass.EventType.NumberOfMarriages:
          evStr = "NMR";
          return true;
        case IndividualEventClass.EventType.Naturalization:
          evStr = "NATU";
          return true;
        case IndividualEventClass.EventType.Emigration:
          evStr = "EMIG";
          return true;
        case IndividualEventClass.EventType.Immigration:
          evStr = "IMMI";
          return true;
        case IndividualEventClass.EventType.Residence:
          evStr = "RESI";
          return true;
        case IndividualEventClass.EventType.Census:
          evStr = "CENS";
          return true;
        case IndividualEventClass.EventType.Possesions:
          evStr = "PROP";
          return true;
        case IndividualEventClass.EventType.Probate:
          evStr = "PROB";
          return true;
        //case IndividualEventClass.EventType.Military:
        //  evStr = "MILI";
        //  return true;
        case IndividualEventClass.EventType.Religion:
          evStr = "RELI";
          return true;
        case IndividualEventClass.EventType.Will:
          evStr = "WILL";
          return true;
        case IndividualEventClass.EventType.Retired:
          evStr = "RETI";
          return true;
        //case IndividualEventClass.EventType.Fact:
        //  evStr = "FACT";
        //  return true;
        case IndividualEventClass.EventType.Endowment:
          evStr = "ENDL";
          return true;
        case IndividualEventClass.EventType.SealingChild:
          evStr = "SLGC";
          return true;

        case IndividualEventClass.EventType.Caste:
          evStr = "CAST";
          return true;
        case IndividualEventClass.EventType.PhysicalDescription:
          evStr = "DSCR";
          return true;
        case IndividualEventClass.EventType.IdentityNumber:
          evStr = "IDNO";
          return true;
        case IndividualEventClass.EventType.SocialSecurityNumber:
          evStr = "SSN";
          return true;
        case IndividualEventClass.EventType.Death:
          evStr = "DEAT";
          return true;
        case IndividualEventClass.EventType.Burial:
          evStr = "BURI";
          return true;
        case IndividualEventClass.EventType.Cremation:
          evStr = "CREM";
          return true;
        case IndividualEventClass.EventType.GeneralEvent:
          evStr = "EVEN";
          return true;

        case IndividualEventClass.EventType.RecordUpdate:
          evStr = "CHAN";
          return true;

        default:
          trace.TraceInformation("GedcomEncoder: Ind.Event.unhandled event type:" + ev);
          return false;
      }

    }

    void WriteTag(FileStream file, string tag, int level, string data)
    {
      if ((data.IndexOf('\n') >= 0) || (data.IndexOf('\r') >= 0))
      {
        trace.TraceInformation("GedcomEncoder: Warning linefeed in single line!:" + level + " " + tag + " " + data);
      }
      WriteData(file, level + " " + tag + " " + data + Linefeed());
    }

    void WriteTagWithCont(FileStream file, string tag, int startLevel, string data)
    {
      if (data != null)
      {
        char[] lineFeeds = new char[] { '\n', '\r' };
        const int MaxLineLength = 240;
        int lineStart = 0, lineEnd, pos, noteLength;
        string line;
        string noteStr = data;

        /*noteStr = noteStr.Replace("\x2013", "-");
        noteStr = noteStr.Replace("\x2019", "\'");
        noteStr = noteStr.Replace("\x201D", "\"");
        noteStr = noteStr.Replace("\x2020", "+");
        noteStr = noteStr.Replace("\x2022", ".");
        noteStr = noteStr.Replace("\x2026", "...");
        noteStr = noteStr.Replace("\x2039", "...");*/
        noteLength = noteStr.Length;

        lineEnd = noteLength;

        if ((pos = noteStr.IndexOfAny(lineFeeds, lineStart)) >= 0)
        {
          lineEnd = pos;
        }
        line = noteStr.Substring(lineStart, lineEnd - lineStart);

        if (line.Length > MaxLineLength)
        {
          string subLine = line.Substring(0, MaxLineLength);
          int subStrPos = MaxLineLength;
          WriteData(file, startLevel + " " + tag + " " + subLine + Linefeed());

          while (subStrPos < line.Length)
          {
            int length = MaxLineLength;

            if (length + subStrPos > line.Length)
            {
              length = line.Length - subStrPos;
            }
            subLine = line.Substring(subStrPos, length);
            WriteData(file, (startLevel + 1) + " CONC " + subLine + Linefeed());
            subStrPos += MaxLineLength;
          }
        }
        else
        {
          WriteData(file, startLevel + " " + tag + " " + line + Linefeed());
        }
        while (lineEnd < noteLength - 1)
        {
          lineStart = lineEnd + 1;

          if (noteStr[lineStart] == '\n') // windows style linefeeds
          {
            lineStart++;
          }

          lineEnd = noteLength;

          if ((pos = noteStr.IndexOfAny(lineFeeds, lineStart)) >= 0)
          {
            lineEnd = pos;
          }
          line = noteStr.Substring(lineStart, lineEnd - lineStart);
          if (line.Length > MaxLineLength)
          {
            string subLine = line.Substring(0, MaxLineLength);
            int subStrPos = MaxLineLength;
            WriteData(file, (startLevel + 1) + " CONT " + subLine + Linefeed());

            while (subStrPos < line.Length)
            {
              int length = MaxLineLength;

              if (length + subStrPos > line.Length)
              {
                length = line.Length - subStrPos;
              }
              subLine = line.Substring(subStrPos, length);
              WriteData(file, (startLevel + 1) + " CONC " + subLine + Linefeed());
              subStrPos += MaxLineLength;
            }
          }
          else
          {
            WriteData(file, (startLevel + 1) + " CONT " + line + Linefeed());
          }
        }
      }
    }


    void WriteNoteList(FileStream file, IList<NoteClass> noteList, int startLevel)
    {
      if (noteList != null)
      {
        foreach (NoteClass note in noteList)
        {
          if (note.note != null)
          {
            string noteStr = note.note;

            WriteTagWithCont(file, "NOTE", startLevel, noteStr);
          }
        }
      }
    }

    void WriteSourceList(FileStream file, IList<SourceDescriptionClass> sourceList, int startLevel)
    {
      if (sourceList != null)
      {
        foreach (SourceDescriptionClass source in sourceList)
        {
          if (source.GetDescription() != null)
          {
            string noteStr = source.GetDescription();

            WriteTagWithCont(file, "SOUR", startLevel, noteStr);
          }
        }
      }
    }

    void WriteAddress(FileStream file, AddressClass address, int startLevel)
    {
      if (address != null)
      {
        //IList<AddressPartClass> addressPartList = address.GetAddressPartList();

        AddressPartClass startAddressLine = address.GetAddressPart(AddressPartClass.AddressPartType.StreetAddress);

        IList<AddressPartClass> addressPartList = address.GetAddressPartList();

        if (addressPartList != null)
        {
          if (startAddressLine != null)
          {
            WriteTagWithCont(file, "ADDR", startLevel, startAddressLine.ToString());
          }
          else
          {
            WriteData(file, startLevel + " ADDR" + Linefeed());
          }
          foreach (AddressPartClass addressPart in addressPartList)
          {
            string addressStr = "";
            int level = 0;

            if (addressPart.GetAddressPartType() != AddressPartClass.AddressPartType.StreetAddress)
            {
              if (GetGedcomAddressPartString(addressPart.GetAddressPartType(), ref level, ref addressStr))
              {
                WriteData(file, (level + startLevel) + " " + addressStr + " " + addressPart + Linefeed());
              }
              else
              {
                trace.TraceInformation("Ind.Event.unhandled address format:" + addressPart.GetAddressPartType());
              }
            }
          }
        }
      }
    }

    void WriteSourceXrefList(FileStream file, IList<SourceXrefClass> sourceXrefList, int startLevel)
    {
      if (sourceXrefList != null)
      {
        foreach (SourceXrefClass sourceXref in sourceXrefList)
        {
          WriteTag(file, "SOUR", startLevel, "@" + sourceXref.GetXrefName() + "@");
          xrefMappers.AddReference(XrefType.Source, sourceXref.GetXrefName(), false);
        }
      }
    }
    void WriteNoteXrefList(FileStream file, IList<NoteXrefClass> noteXrefList, int startLevel)
    {
      if (noteXrefList != null)
      {
        foreach (NoteXrefClass noteXref in noteXrefList)
        {
          WriteTag(file, "NOTE", startLevel, "@" + noteXref.GetXrefName() + "@");
          xrefMappers.AddReference(XrefType.Note, noteXref.GetXrefName(), true);
        }
      }
    }


    void WritePlace(FileStream file, PlaceStructureClass place, int startLevel)
    {
      if (place != null)
      {
        //IList<AddressPartClass> addressPartList = address.GetAddressPartList();

        WriteTag(file, "PLAC", startLevel, place.GetPlace());

        string placeHierarchy = place.GetPlaceHierarchy();

        if ((placeHierarchy != null) && (placeHierarchy.Length > 0))
        {
          WriteTag(file, "FORM", startLevel + 1, placeHierarchy);
        }
        MapPosition  mapPos = place.GetMapPosition();

        if (mapPos != null)
        {
          string nsString = "N";
          string ewString = "E";

          if (mapPos.latitude < 0)
          {
            nsString = "S";
          }
          if (mapPos.longitude < 0)
          {
            nsString = "W";
          }
          WriteTag(file, "MAP", startLevel + 1, "");
          WriteTag(file, "LATI", startLevel + 2, nsString + mapPos.latitude.ToString());
          WriteTag(file, "LONG", startLevel + 2, ewString + mapPos.longitude.ToString());
        }
        {
          IList<SourceXrefClass> sourceXrefList = place.GetSourceXrefList();

          WriteSourceXrefList(file, sourceXrefList, startLevel + 1);
        }
        {
          IList<SourceDescriptionClass> sourceList = place.GetSourceList();

          WriteSourceList(file, sourceList, 2);
        }
        {
          IList<NoteXrefClass> noteXrefList = place.GetNoteXrefList();

          WriteNoteXrefList(file, noteXrefList, 2);
        }
        {
          IList<NoteClass> noteList = place.GetNoteList();

          WriteNoteList(file, noteList, startLevel + 1);
        }


      }
    }


    private string Append(string s1, string s2, string prePostString)
    {
      string addStr = prePostString + s2 + prePostString;
      if (s1.Length > 0)
      {
        return s1 + " " + addStr;
      }
      return addStr;
    }
    private string CheckAppend(PersonalNameClass name, string s1, PersonalNameClass.PartialNameType type, string prePostStr = "")
    {
      string partialName = name.GetName(type);
      if (partialName.Length > 0)
      {
        s1 = Append(s1, partialName, prePostStr);
      }
      return s1;
    }

    private string GetFullGedcomName(PersonalNameClass name)
    {
      string nameStr = "";

      nameStr = CheckAppend(name, nameStr, PersonalNameClass.PartialNameType.NamePrefix);
      nameStr = CheckAppend(name, nameStr, PersonalNameClass.PartialNameType.GivenName);
      nameStr = CheckAppend(name, nameStr, PersonalNameClass.PartialNameType.MiddleName);
      nameStr = CheckAppend(name, nameStr, PersonalNameClass.PartialNameType.Nickname);
      nameStr = CheckAppend(name, nameStr, PersonalNameClass.PartialNameType.SurnamePrefix);
      string surname = name.GetName(PersonalNameClass.PartialNameType.Surname);
      nameStr = CheckAppend(name, nameStr, PersonalNameClass.PartialNameType.Surname, "/");

      string birthSurname = name.GetName(PersonalNameClass.PartialNameType.BirthSurname);
      if (birthSurname.Length > 0)
      {
        bool include = true;
        if (surname.Length > 0)
        {
          if (surname.Equals(birthSurname))
          {
            include = false;
          }
        }
        if (include)
        {
          nameStr = CheckAppend(name, nameStr, PersonalNameClass.PartialNameType.BirthSurname);
        }
      }
      nameStr = CheckAppend(name, nameStr, PersonalNameClass.PartialNameType.Suffix);
      trace.TraceInformation("GetFullGedcomName():" + name);
      return nameStr;
    }

    private void CheckAndWritePartialName(PersonalNameClass name, PersonalNameClass.PartialNameType type, FileStream file, int level, string gedcomTag)
    {
      string partialName = name.GetName(type);
      if (partialName.Length > 0)
      {
        WriteData(file, level + " " + gedcomTag + " " + partialName + Linefeed());
      }
    }


    private void WritePersonalName(FileStream file, PersonalNameClass name)
    {
      //PersonalNameClass name = person.GetPersonalName();
      string nameStr = name.GetName(PersonalNameClass.PartialNameType.PublicName, true);

      WriteData(file, "1 NAME " + GetFullGedcomName(name) + Linefeed());

      CheckAndWritePartialName(name, PersonalNameClass.PartialNameType.NamePrefix, file, 2, "NPFX");
      CheckAndWritePartialName(name, PersonalNameClass.PartialNameType.GivenName, file, 2, "GIVN");
      CheckAndWritePartialName(name, PersonalNameClass.PartialNameType.Nickname, file, 2, "NICK");
      CheckAndWritePartialName(name, PersonalNameClass.PartialNameType.SurnamePrefix, file, 2, "SPFX");
      CheckAndWritePartialName(name, PersonalNameClass.PartialNameType.Surname, file, 2, "SURN");
      CheckAndWritePartialName(name, PersonalNameClass.PartialNameType.Suffix, file, 2, "NSFX");
    }



    private void WriteIndividual(FileStream file, IndividualClass person, bool includeReferences = true)
    {
      WriteData(file, "0 @" + person.GetXrefName() + "@ INDI" + Linefeed());

      WritePersonalName(file, person.GetPersonalName());

      xrefMappers.AddReference(XrefType.Individual, person.GetXrefName(), true);

      switch (person.GetSex())
      {
        case IndividualClass.IndividualSexType.Female:
          WriteData(file, "1 SEX F" + Linefeed());
          break;

        case IndividualClass.IndividualSexType.Male:
          WriteData(file, "1 SEX M" + Linefeed());
          break;

        default:
          break;
      }

      {
        IList<IndividualEventClass> eventList = person.GetEventList();

        if (eventList != null)
        {
          foreach (IndividualEventClass ev in eventList)
          {
            string evStr = "";

            if (GetGedcomEventString(ev.GetEventType(), ref evStr))
            {
              WriteData(file, "1 " + evStr + Linefeed());
              if ((ev.GetDate() != null) && (ev.GetDate().ToGedcomDateString().Length > 0))
              {
                WriteData(file, "2 DATE " + ev.GetDate().ToGedcomDateString() + Linefeed());
              }

              if (ev.GetNoteXrefList() != null)
              {
                IList<NoteXrefClass> noteXrefList = ev.GetNoteXrefList();

                WriteNoteXrefList(file, noteXrefList, 2);
              }
              if (ev.GetNoteList() != null)
              {
                IList<NoteClass> noteList = ev.GetNoteList();

                WriteNoteList(file, noteList, 2);
              }

              if (ev.GetSourceXrefList() != null)
              {
                IList<SourceXrefClass> sourceXrefList = ev.GetSourceXrefList();

                WriteSourceXrefList(file, sourceXrefList, 2);
              }
              if (ev.GetSourceList() != null)
              {
                IList<SourceDescriptionClass> sourceList = ev.GetSourceList();

                WriteSourceList(file, sourceList, 2);
              }

              AddressClass address = ev.GetAddress();

              if (address != null)
              {
                WriteAddress(file, address, 2);
              }

              PlaceStructureClass place = ev.GetPlace();

              if (place != null)
              {
                WritePlace(file, place, 2);
              }
            }
          }
        }
      }

      if (includeReferences)
      {
        {
          IList<FamilyXrefClass> familyList = person.GetFamilySpouseList();

          if (familyList != null)
          {
            foreach (FamilyXrefClass famXref in familyList)
            {
              if (famXref.GetXrefName().Length == 0)
              {
                trace.TraceEvent(TraceEventType.Error, 0, "error: no xref!");
              }
              xrefMappers.AddReference(XrefType.Family, famXref.GetXrefName(), false, person.GetXrefName(), false);

              WriteData(file, "1 FAMS @" + famXref.GetXrefName() + "@" + Linefeed());
            }
          }
        }
        {
          IList<FamilyXrefClass> familyList = person.GetFamilyChildList();

          if (familyList != null)
          {
            foreach (FamilyXrefClass famXref in familyList)
            {
              xrefMappers.AddReference(XrefType.Family, famXref.GetXrefName(), false, person.GetXrefName(), true);
              WriteData(file, "1 FAMC @" + famXref.GetXrefName() + "@" + Linefeed());
            }
          }
        }

        {
          IList<NoteXrefClass> noteXrefList = person.GetNoteXrefList();

          WriteNoteXrefList(file, noteXrefList, 1);
        }
        {
          IList<NoteClass> noteList = person.GetNoteList();

          WriteNoteList(file, noteList, 1);
        }
        {
          IList<SourceXrefClass> sourceXrefList = person.GetSourceXrefList();

          WriteSourceXrefList(file, sourceXrefList, 1);
        }
      }
      {
        IList<SourceDescriptionClass> sourceList = person.GetSourceList();

        WriteSourceList(file, sourceList, 1);
      }
      {
        IList<String> rfnList = person.GetPermanentRFNList();

        if (rfnList != null)
        {
          foreach (String str in rfnList)
          {
            WriteData(file, "1 RFN " + str + Linefeed());
          }
        }
      }
      {
        IList<MultimediaLinkClass> mmLinkList = person.GetMultimediaLinkList();

        if (mmLinkList != null)
        {
          foreach (MultimediaLinkClass link in mmLinkList)
          {
            WriteData(file, "1 OBJE" + Linefeed());
            WriteData(file, "2 FORM " + link.GetFormat() + Linefeed());
            WriteData(file, "2 FILE " + link.GetLink() + Linefeed());
          }
        }


      }
    }


    private bool GetGedcomFamilyEventString(IndividualEventClass.EventType ev, ref string evStr)
    {
      switch (ev)
      {
        case IndividualEventClass.EventType.FamEngagement:
          evStr = "ENGA";
          return true;
        case IndividualEventClass.EventType.FamMarriageBann:
          evStr = "MARB";
          return true;
        case IndividualEventClass.EventType.FamMarriage:
          evStr = "MARR";
          return true;
        case IndividualEventClass.EventType.FamMarriageContract:
          evStr = "MARC";
          return true;
        case IndividualEventClass.EventType.FamMarriageLicense:
          evStr = "MARL";
          return true;
        case IndividualEventClass.EventType.FamMarriageSettlement:
          evStr = "MARS";
          return true;
        case IndividualEventClass.EventType.FamGeneralEvent:
          evStr = "EVEN";
          return true;
        case IndividualEventClass.EventType.FamCensus:
          evStr = "CENS";
          return true;
        case IndividualEventClass.EventType.FamDivorce:
          evStr = "DIV";
          return true;
        case IndividualEventClass.EventType.FamDivorceFiled:
          evStr = "DIVF";
          return true;
        case IndividualEventClass.EventType.FamAnnulment:
          evStr = "ANUL";
          return true;
        case IndividualEventClass.EventType.FamRecordChange:
          evStr = "CHAN";
          return true;
        default:
          trace.TraceInformation("GedcomEncoder: Family.Event.unhandled event type:" + ev);
          return false;
      }

    }

    bool GetGedcomAddressPartString(AddressPartClass.AddressPartType addressType, ref int levelOffset, ref string gedcomString)
    {

      levelOffset = 1;
      switch (addressType)
      {
        case AddressPartClass.AddressPartType.StreetAddress:
          gedcomString = "ADDR";
          levelOffset = 0;
          return true;
        case AddressPartClass.AddressPartType.Line1:
          gedcomString = "ADR1";
          return true;
        case AddressPartClass.AddressPartType.Line2:
          gedcomString = "ADR2";
          return true;
        case AddressPartClass.AddressPartType.PostCode:
          gedcomString = "POST";
          return true;
        case AddressPartClass.AddressPartType.City:
          gedcomString = "CITY";
          return true;
        case AddressPartClass.AddressPartType.State:
          gedcomString = "STAE";
          return true;
        case AddressPartClass.AddressPartType.Country:
          gedcomString = "CTRY";
          return true;
        case AddressPartClass.AddressPartType.PhoneNumber:
          gedcomString = "PHON";
          levelOffset = 0;
          return true;

        //case AddressPartClass.AddressPartType.Place: // From the PLACE_STRUCTURE
        //  gedcomString = "PLAC";
        //  levelOffset = 0;
        //  return true;
        // these are not according to specs
        //case AddressPartClass.AddressPartType.Note:
        //case AddressPartClass.AddressPartType.Location:
        //case AddressPartClass.AddressPartType.EmailAddress:
        //  gedcomString = "NOTE";
        //  return true;
        default:
          trace.TraceInformation("unhandled address format:" + addressType);
          return false;

    }
  }


    private void WriteFamily(FileStream file, FamilyClass family, IFamilyTreeStoreBaseClass familyTree, bool includeReferences = true)
    {
      WriteData(file, "0 @" + family.GetXrefName() + "@ FAM" + Linefeed());
      if (family.GetXrefName().Length == 0)
      {
        trace.TraceEvent(TraceEventType.Error, 0, "error: no xref!");
      }
      xrefMappers.AddReference(XrefType.Family, family.GetXrefName(), true);

      {
        IList<IndividualXrefClass> parentList = family.GetParentList();
        if (parentList != null)
        {
          foreach (IndividualXrefClass individualXref in parentList)
          {
            string spouseString = "HUSB";

            IndividualClass individual = familyTree.GetIndividual(individualXref.GetXrefName(), (uint)SelectIndex.NoIndex, PersonDetail.Sex);

            if(individual != null)
            {
              if (individual.GetSex() == IndividualClass.IndividualSexType.Female)
              {
                spouseString = "WIFE";
              }
            }
            individual = null;

            xrefMappers.AddReference(XrefType.Individual, individualXref.GetXrefName(), false, family.GetXrefName(), false);
            WriteData(file, "1 " + spouseString + " @" + individualXref.GetXrefName() + "@" + Linefeed());
          }
        }
      }

      if (includeReferences)
      {
        {
          IList<IndividualXrefClass> childList = family.GetChildList();
          if (childList != null)
          {
            foreach (IndividualXrefClass individualXref in childList)
            {
              xrefMappers.AddReference(XrefType.Individual, individualXref.GetXrefName(), false, family.GetXrefName(), true);
              WriteData(file, "1 CHIL @" + individualXref.GetXrefName() + "@" + Linefeed());
            }
          }
        }
        {
          IList<NoteXrefClass> noteXrefList = family.GetNoteXrefList();

          WriteNoteXrefList(file, noteXrefList, 1);
        }
        {
          IList<SourceXrefClass> sourceXrefList = family.GetSourceXrefList();

          WriteSourceXrefList(file, sourceXrefList, 1);
        }
      }
      {
        IList<NoteClass> noteList = family.GetNoteList();

        WriteNoteList(file, noteList, 1);
      }
      {
        IList<SourceDescriptionClass> sourceList = family.GetSourceList();

        WriteSourceList(file, sourceList, 1);
      }

      {
        IList<IndividualEventClass> eventList = family.GetEventList();
        if (eventList != null)
        {
          foreach (IndividualEventClass ev in eventList)
          {
            string evStr = "";

            if (GetGedcomFamilyEventString(ev.GetEventType(), ref evStr))
            {
              WriteData(file, "1 " + evStr + Linefeed());
              if (ev.GetDate() != null)
              {
                WriteData(file, "2 DATE " + ev.GetDate().ToGedcomDateString() + Linefeed());
              }

              if (ev.GetSourceList() != null)
              {
                IList<SourceDescriptionClass> sourceList = ev.GetSourceList();

                WriteSourceList(file, sourceList, 2);
              }
              if (includeReferences)
              {
                if (ev.GetSourceXrefList() != null)
                {
                  IList<SourceXrefClass> sourceXrefList = ev.GetSourceXrefList();

                  WriteSourceXrefList(file, sourceXrefList, 2);
                }
              }
              AddressClass address = ev.GetAddress();

              if (address != null)
              {
                WriteAddress(file, address, 2);
              }
              if (ev.GetNoteList() != null)
              {
                WriteNoteList(file, ev.GetNoteList(), 2);
              }
            }
          }
        }
      }

    }

    void DebugPrint(string str)
    {
      trace.TraceInformation("str:");
      for (int i = 0; i < str.Length; i++)
      {
        trace.TraceInformation("[" + (int)str[i] + "]");
      }
    }

    private void WriteNote(FileStream file, NoteClass note)
    {
      string noteStr;
      int startStrPos = 0, endStrPos = 0;
      const string lineFeedStr = "\n\r";
      char [] lineFeedChars = lineFeedStr.ToCharArray();
      const int GedcomMaxLineLength = 240;
      bool concatenate = false, concatenateNext = false;
      string wrStr = "";

      if (note.note == null)
      {
        noteStr = null;
      }
      else
      {
        noteStr = note.note.Normalize();
      }

      if (noteStr == null)
      {
        noteStr = "";
      }

      endStrPos = noteStr.IndexOfAny(lineFeedChars, startStrPos);

      if(endStrPos < 0)
      {
        endStrPos = noteStr.Length;
      }

      if (endStrPos - startStrPos > GedcomMaxLineLength)
      {
        endStrPos = startStrPos + GedcomMaxLineLength;
        concatenate = true;
      }
      wrStr += noteStr.Substring(startStrPos, (endStrPos - startStrPos));
      WriteData(file, "0 @" + note.GetXrefName() + "@ NOTE " + wrStr + Linefeed());
      xrefMappers.AddReference(XrefType.Note, note.GetXrefName(), true);

      while (endStrPos < (noteStr.Length - 1))
      {
        startStrPos = endStrPos;
        while (((noteStr[startStrPos] == '\n') || (noteStr[startStrPos] == '\r')) && (startStrPos < noteStr.Length))
        {
          startStrPos++;
        }

        if (startStrPos < noteStr.Length)
        {
          endStrPos = noteStr.IndexOfAny(lineFeedChars, startStrPos);

          wrStr = "";

          if (endStrPos < 0)
          {
            endStrPos = noteStr.Length;
          }
          if (endStrPos - startStrPos > GedcomMaxLineLength)
          {
            endStrPos = startStrPos + GedcomMaxLineLength;
            concatenate = true;
          }
          else
          {
            concatenate = false;
          }
          wrStr += noteStr.Substring(startStrPos, (endStrPos - startStrPos));

          if (!concatenateNext)
          {
            WriteData(file, "1 CONT " + wrStr + Linefeed());
          }
          else
          {
            WriteData(file, "1 CONC " + wrStr + Linefeed());
          }
          concatenateNext = concatenate;
        }
      }
    }

    private void WriteSubmitter(FileStream file, SubmitterClass person)
    {
      PersonalNameClass name = person.GetPersonalName();
      string nameStr = "";

      WriteData(file, "0 @" + person.GetXrefName() + "@ SUBM" + Linefeed());
      xrefMappers.AddReference(XrefType.Submitter, person.GetXrefName(), true);

      nameStr += name.GetName();

      if (nameStr.Length > 0)
      {
        WriteData(file, "1 NAME " + nameStr + Linefeed());
      }

      AddressClass address = person.GetAddress();
      if (address != null)
      {
        WriteAddress(file, address, 1);
      }

      IList<IndividualEventClass> eventList = person.GetEventList();
      if (eventList != null)
      {
        foreach (IndividualEventClass ev in eventList)
        {
          if (ev.GetEventType() == IndividualEventClass.EventType.RecordUpdate)
          {
            WriteData(file, "1 CHAN" + Linefeed());
            if (ev.GetDate() != null)
            {
              WriteData(file, "2 DATE " + ev.GetDate().ToGedcomDateString() + Linefeed());
            }

          }
        }
      }
    }

    private void WriteSubmission(FileStream file, SubmissionClass submission)
    {

      WriteData(file, "0 @" + submission.GetXrefName() + "@ SUBN" + Linefeed());
      xrefMappers.AddReference(XrefType.Submission, submission.GetXrefName(), true);

      if ((submission.GetSubmitterXref() != null) && (submission.GetSubmitterXref().Length > 0))
      {
        WriteData(file, "1 SUBM " + submission.GetSubmitterXref() + Linefeed());
      }

      if ((submission.GetFamilyFile() != null) && (submission.GetFamilyFile().Length > 0))
      {
        WriteData(file, "1 FAMF " + submission.GetFamilyFile() + Linefeed());
      }
      if ((submission.GetAncestorGenerations() != null) && (submission.GetAncestorGenerations().Length > 0))
      {
        WriteData(file, "1 ANCE " + submission.GetAncestorGenerations() + Linefeed());
      }
      if ((submission.GetTemple() != null) && (submission.GetTemple().Length > 0))
      {
        WriteData(file, "1 TEMP " + submission.GetTemple() + Linefeed());
      }
      if ((submission.GetDescendantGenerations() != null) && (submission.GetDescendantGenerations().Length > 0))
      {
        WriteData(file, "1 DESC " + submission.GetDescendantGenerations() + Linefeed());
      }
      if ((submission.GetOrdinance() != null) && (submission.GetOrdinance().Length > 0))
      {
        WriteData(file, "1 ORDI " + submission.GetOrdinance() + Linefeed());
      }
      if ((submission.GetAutoRecId() != null) && (submission.GetAutoRecId().Length > 0))
      {
        WriteData(file, "1 RIN " + submission.GetAutoRecId() + Linefeed());
      }
    }

    private void WriteRepository(FileStream file, RepositoryClass repo)
    {
      WriteData(file, "0 @" + repo.GetXrefName() + "@ REPO" + Linefeed());

      xrefMappers.AddReference(XrefType.Repository, repo.GetXrefName(), true);
      if (repo.GetName().Length > 0)
      {
        WriteData(file, "1 NAME " + repo.GetName() + Linefeed());
      }

      AddressClass address = repo.GetAddress();
      if (address != null)
      {
        WriteAddress(file, address, 1);
      }

      IList<NoteClass> noteList = repo.GetNoteList();
      if (noteList != null)
      {
        WriteNoteList(file, noteList, 1);
      }
    }

    private void WriteMultimediaObject(FileStream file, MultimediaObjectClass mmo)
    {
      WriteData(file, "0 @" + mmo.GetXrefName() + "@ OBJE" + Linefeed());
      xrefMappers.AddReference(XrefType.Multimedia, mmo.GetXrefName(), true);
      WriteData(file, "1 FORM " + mmo.GetFormat() + Linefeed());

      if (mmo.GetTitle().Length > 0)
      {
        WriteData(file, "1 TITL " + mmo.GetTitle() + Linefeed());
      }

      IList<NoteClass> noteList = mmo.GetNoteList();
      if (noteList != null)
      {
        WriteNoteList(file, noteList, 1);
      }
    }

    private void WriteTrailer(FileStream file)
    {
      WriteData(file, "0 TRLR" + Linefeed());
    }

    public void SetProgressTarget(IProgressReporterInterface progressTarget)
    {
      workerProgressTarget = progressTarget;
    }


    public void StoreFile(IFamilyTreeStoreBaseClass familyTree, string filename, FamilyFileTypeOperation operation, int variant = 0)
    {
      //FamilyForm2 parentObj = null;

      if (trace.Switch.Level.HasFlag(SourceLevels.Information))
      {
        trace.TraceInformation("GedcomEncoder::StoreFile(" + filename + ") start " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        trace.TraceData(TraceEventType.Verbose, 0, familyTree.GetShortTreeInfo());
        //familyTree.Print();
      }

      /*if(parent != null)
      {
        if(parent.GetType() == typeof(FamilyForm2))
        {
          parentObj.SaveFile(filename);
        }
      }*/
      if (familyTree != null)
      {
        FamilyTreeContentClass contents = familyTree.GetContents();
        //int printPercent;
        FileStream file = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Write);

        WriteHeader(file, familyTree.GetSourceFileName());

        {
          IEnumerator<IndividualClass> iterator;

          if (workerProgressTarget != null)
          {
            //int printPercent = iterator.;
            workerProgressTarget.ReportProgress(0, "Exporting people 1/9 ");
          }
          iterator = familyTree.SearchPerson(null, workerProgressTarget);
          if (iterator != null)
          {
            int cnt = 0;
            while (iterator.MoveNext())
            {
              IndividualClass person = iterator.Current;
              if ((workerProgressTarget != null) && (contents.individuals > 0))
              {
                workerProgressTarget.ReportProgress(cnt * 100 / contents.individuals);
              }

              WriteIndividual(file, person);
              trace.TraceInformation("GedcomEncoder::Exporting individuals " + cnt++);
            }
          }
        }

        {
          IEnumerator<FamilyClass> iterator;

          if (workerProgressTarget != null)
          {
            workerProgressTarget.ReportProgress(0, "Exporting families 2/9 ");
          }
          iterator = familyTree.SearchFamily(null, workerProgressTarget);

          if (iterator != null)
          {
            int cnt = 0;
            while (iterator.MoveNext())
            {
              FamilyClass family = iterator.Current;

              if ((workerProgressTarget != null) && (contents.families > 0))
              {
                workerProgressTarget.ReportProgress(cnt * 100 / contents.families);
              }

              WriteFamily(file, family, familyTree);
              trace.TraceInformation("GedcomEncoder::Exporting families " + cnt++);
            }
          }
        }

        if (variant == 1)
        {
          int missingDefinitions;
          int retryCnt = 0;
          do
          {
            missingDefinitions = 0;
            {
              IDictionaryEnumerator missingPersonDefinisions = (IDictionaryEnumerator)xrefMappers.GetMapper(XrefType.Individual).GetEnumerator();
              IList<string> missingRefs = new List<string>();
              int cnt = 0;

              while (missingPersonDefinisions.MoveNext())
              {
                XrefMapperClass XrefInfo = (XrefMapperClass)missingPersonDefinisions.Value;

                if (XrefInfo.noOfDefinitions == 0)
                {
                  trace.TraceInformation("Missing person xref " + XrefInfo.newXref + "!");
                  missingRefs.Add(XrefInfo.newXref);
                  missingDefinitions++;
                }
              }
              if ((workerProgressTarget != null) && (missingDefinitions > 0))
              {
                workerProgressTarget.ReportProgress(0, "Exporting missing persons 3/9 ");
              }
              foreach (string xref in missingRefs)
              {
                IndividualClass person = familyTree.GetIndividual(xref);

                trace.TraceInformation("GedcomEncoder::Missing person " + cnt++ + "/" + missingRefs.Count);
                if (workerProgressTarget != null)
                {
                  workerProgressTarget.ReportProgress(cnt * 100 / missingRefs.Count);
                }
                if (person != null)
                {
                  WriteIndividual(file, person, false);
                }
                else
                {
                  trace.TraceEvent(TraceEventType.Error, 0, "Error: person " + xref + " not found!");
                }

              }
            }

            {
              IDictionaryEnumerator missingFamilyDefinitions = (IDictionaryEnumerator)xrefMappers.GetMapper(XrefType.Family).GetEnumerator();
              IList<string> missingRefs = new List<string>();
              int cnt = 0;

              while (missingFamilyDefinitions.MoveNext())
              {
                XrefMapperClass XrefInfo = (XrefMapperClass)missingFamilyDefinitions.Value;

                if (XrefInfo.noOfDefinitions == 0)
                {
                  trace.TraceInformation("GedcomEncoder::Missing family xref " + XrefInfo.newXref + "!");
                  missingRefs.Add(XrefInfo.newXref);
                  missingDefinitions++;
                }
              }

              if ((workerProgressTarget != null) && (missingDefinitions > 0))
              {
                workerProgressTarget.ReportProgress(0, "Exporting missing families 4/9 ");
              }
              foreach (string xref in missingRefs)
              {
                FamilyClass family = familyTree.GetFamily(xref);

                trace.TraceInformation("GedcomEncoder::Missing family " + cnt++ + "/" + missingRefs.Count);
                if (workerProgressTarget != null)
                {
                  workerProgressTarget.ReportProgress(cnt * 100 / missingRefs.Count);
                }
                if (family != null)
                {
                  WriteFamily(file, family, familyTree, false);
                }
                else
                {
                  trace.TraceEvent(TraceEventType.Error, 0, "Error: Family " + xref + " not found!");
                }

              }
            }
            trace.TraceInformation("Missing definitions = " + missingDefinitions);
          } while ((missingDefinitions > 0) && (++retryCnt < 10));
        }
        else if (variant == 0)
        {
          IDictionaryEnumerator missingPersonDefinisions = (IDictionaryEnumerator)xrefMappers.GetMapper(XrefType.Individual).GetEnumerator();

          if (workerProgressTarget != null)
          {
            workerProgressTarget.ReportProgress(0, "Exporting missing persons 3/9 ");
          }
          while (missingPersonDefinisions.MoveNext())
          {
            XrefMapperClass XrefInfo = (XrefMapperClass)missingPersonDefinisions.Value;

            if (XrefInfo.noOfDefinitions == 0)
            {
              IndividualClass person = new IndividualClass();

              person.SetPersonalName(new PersonalNameClass(PersonalNameClass.PartialNameType.PublicName, "<not exported>"));
              person.SetXrefName(XrefInfo.newXref);
              if (XrefInfo.referencedFrom.Count != XrefInfo.noOfReferences)
              {
                trace.TraceData(TraceEventType.Warning, 0, "indi warning: " + XrefInfo.referencedFrom.Count + "!=" + XrefInfo.noOfReferences + " in " + XrefInfo.newXref);
              }
              foreach (XrefMapperClass.XrefRelation rel in XrefInfo.referencedFrom)
              {
                if(rel.child)
                {
                  person.AddRelation(new FamilyXrefClass(rel.xref), IndividualClass.RelationType.Child);
                }
                else
                {
                  person.AddRelation(new FamilyXrefClass(rel.xref), IndividualClass.RelationType.Spouse);
                }
              }
              WriteIndividual(file, person, false);
            }
          }

          {
            IDictionaryEnumerator missingFamilyDefinitions = (IDictionaryEnumerator)xrefMappers.GetMapper(XrefType.Family).GetEnumerator();

            if (workerProgressTarget != null)
            {
              workerProgressTarget.ReportProgress(0, "Exporting missing families 4/9 ");
            }
            while (missingFamilyDefinitions.MoveNext())
            {
              XrefMapperClass XrefInfo = (XrefMapperClass)missingFamilyDefinitions.Value;

              if (XrefInfo.noOfDefinitions == 0)
              {
                FamilyClass family = new FamilyClass();

                family.SetXrefName(XrefInfo.newXref);
                if(XrefInfo.referencedFrom.Count != XrefInfo.noOfReferences)
                {
                  trace.TraceData(TraceEventType.Warning, 0, "fam warning: " + XrefInfo.referencedFrom.Count + "!=" + XrefInfo.noOfReferences + " in " + XrefInfo.newXref);
                }
                foreach (XrefMapperClass.XrefRelation rel in XrefInfo.referencedFrom)
                {
                  if (rel.child)
                  {
                    family.AddRelation(new IndividualXrefClass(rel.xref), FamilyClass.RelationType.Child);
                  }
                  else
                  {
                    family.AddRelation(new IndividualXrefClass(rel.xref), FamilyClass.RelationType.Parent);
                  }
                }
                WriteFamily(file, family, familyTree);
              }
            }
          }
        }

        {
          IEnumerator<NoteClass> iterator;

          if (workerProgressTarget != null)
          {
            workerProgressTarget.ReportProgress(0, "Exporting notes 5/9");
          }
          iterator = familyTree.SearchNote(null, workerProgressTarget);

          if (iterator != null)
          {
            int cnt = 0;
            while (iterator.MoveNext())
            {
              NoteClass note = iterator.Current;

              if ((workerProgressTarget != null) && (contents.notes > 0))
              {
                workerProgressTarget.ReportProgress(cnt * 100 / contents.notes);
              }
              WriteNote(file, note);
              cnt++;
            }
          }
        }

        {
          IEnumerator<MultimediaObjectClass> iterator;

          if (workerProgressTarget != null)
          {
            workerProgressTarget.ReportProgress(0, "Exporting multimedia 6/9");
          }
          iterator = familyTree.SearchMultimediaObject(null, workerProgressTarget);

          if (iterator != null)
          {
            int cnt = 0;
            while (iterator.MoveNext())
            {
              MultimediaObjectClass mmo= iterator.Current;

              if ((workerProgressTarget != null) && (contents.multimediaObjects > 0))
              {
                workerProgressTarget.ReportProgress(cnt * 100 / contents.multimediaObjects);
              }
              WriteMultimediaObject(file, mmo);
              cnt++;
            }
          }
        }

        {
          IEnumerator<RepositoryClass> iterator;

          if (workerProgressTarget != null)
          {
            workerProgressTarget.ReportProgress(0, "Exporting repositories 7/9");
          }
          iterator = familyTree.SearchRepository(null, workerProgressTarget);

          if (iterator != null)
          {
            int cnt = 0;
            while (iterator.MoveNext())
            {
              RepositoryClass note = iterator.Current;

              if ((workerProgressTarget != null) && (contents.repositories > 0))
              {
                workerProgressTarget.ReportProgress(cnt * 100 / contents.repositories);
              }
              WriteRepository(file, note);
              cnt++;
            }
          }
        }

        {
          IEnumerator<SubmitterClass> iterator;

          if (workerProgressTarget != null)
          {
            workerProgressTarget.ReportProgress(0, "Exporting submitters 8/9");
          }
          iterator = familyTree.SearchSubmitter(null, workerProgressTarget);

          if (iterator != null)
          {
            int cnt = 0;
            while (iterator.MoveNext())
            {
              SubmitterClass submitter = iterator.Current;

              if ((workerProgressTarget != null) && (contents.submitters > 0))
              {
                workerProgressTarget.ReportProgress(cnt * 100 / contents.submitters);
              }
              WriteSubmitter(file, submitter);
              cnt++;
            }
          }
        }

        {
          IEnumerator<SubmissionClass> iterator;

          if (workerProgressTarget != null)
          {
            workerProgressTarget.ReportProgress(0, "Exporting submissions 9/9");
          }
          iterator = familyTree.SearchSubmission(null, workerProgressTarget);

          if (iterator != null)
          {
            int cnt = 0;
            while (iterator.MoveNext())
            {
              SubmissionClass submission = iterator.Current;

              if ((workerProgressTarget != null) && (contents.submissions > 0))
              {
                workerProgressTarget.ReportProgress(cnt * 100 / contents.submissions);
              }
              WriteSubmission(file, submission);
              cnt++;
            }
          }
        }

        WriteTrailer(file);

        file.Close();
      }
      trace.TraceInformation("GedcomEncoder::StoreFile() done " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    public string GetFileTypeFilter(FamilyFileTypeOperation operation, int variant = 0)
    {
      if (operation == FamilyFileTypeOperation.Export)
      {
        switch(variant)
        {
          case 0:
          return "GEDCOM Files|*.ged";
          case 1:
          return "GEDCOM Files Extensive|*.ged";
          default:
            break;
        }
      }
      return null;
    }
    
    public bool IsKnownFileType(string filename)
    {
      if(filename.ToLower().IndexOf(".ged") >= 0)
      {
        return true;
      }
      return false;
    }
    public IDictionary<int,string> GetOperationVariantList(FamilyFileTypeOperation operation)
    {
      if (operation == FamilyFileTypeOperation.Export)
      {
        IDictionary<int, string> opList = new Dictionary<int, string>();

        opList.Add(0, "Normal");
        opList.Add(1, "Extensive");

        return opList;
      }
      return null;
    }
  }


}
