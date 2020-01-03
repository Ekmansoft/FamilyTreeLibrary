using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using FamilyTreeLibrary.FamilyData;
using FamilyTreeLibrary.FamilyTreeStore;

namespace FamilyTreeLibrary.FileFormats.GedcomCodec
{
  class TagStack
  {
    private IList<string> tagList;

    public TagStack()
    {
      tagList = new List<string>();
    }

    public void AddTag(string tag)
    {
      tagList.Add(tag);
    }

    public string GetTagStack()
    {
      string tags = "";

      for(int i = 0; i < tagList.Count; i++)
      {
        if(i > 0)
        {
          tags += ".";
        }
        tags += tagList[i];        
      }
      return tags;
    }
    public void RemoveLast()
    {
      if (tagList.Count > 0)
      {
        tagList.RemoveAt(tagList.Count - 1);
      }
    }
    public int GetLevel()
    {
      return tagList.Count - 1;
    }

  }

  class GedcomTreeDecoderClass
  {
    private IFamilyTreeStoreBaseClass familyTree;
    private int decodedLines;
    GedcomFileCharacterSet characterSet;
    private GedcomParserUtility parser;
    //private bool NotesDecoded = false;
    private bool decodeGedcomBody;
    private bool decodingCompleted;
    //private bool geniAddressNoteSubtagParsed;
    private FileImportResult importResult;
    private GedcomMappers xrefMappers;
    private AnselDecoder anselDecoder;
    private static TraceSource trace;

    //private ExportSoftwareType exportSoftware;

    class XrefMapperClass
    {
      //public string oldXref;
      public string newXref;
      public int noOfReferences;
      public int noOfDefinitions;

      public XrefMapperClass(string newXref, bool defined = false)
      {
        this.newXref = newXref;
        this.noOfReferences = 0;
        this.noOfDefinitions = 0;
      }
      /*public XrefMapperClass()
      {
        this.newXref = null;
        noOfReferences = 1;
      }*/

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


      private string GetXRef(ref IDictionary<string, XrefMapperClass> mapper, XrefType type, string fileXref, bool defined)
      {
        if (mapper.ContainsKey(fileXref))
        {
          mapper[fileXref].CheckAndSetDefined(defined);
          return mapper[fileXref].newXref;
        }
        //resultStr = "I" + Guid.NewGuid().ToString();
        string newXref = familyTree.CreateNewXref(type);
        mapper.Add(fileXref, new XrefMapperClass(newXref, defined));
        mapper[fileXref].CheckAndSetDefined(defined);
        return newXref;
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


      public string GetLocalXRef(XrefType type, string fileXref, bool defined = false)
      {
        switch (type)
        {
          case XrefType.Individual:
            return GetXRef(ref individualXrefMapper, type, fileXref, defined);

          case XrefType.Family:
            return GetXRef(ref familyXrefMapper, type, fileXref, defined);

          case XrefType.Multimedia:
            return GetXRef(ref multimediaXrefMapper, type, fileXref, defined);

          case XrefType.Note:
            return GetXRef(ref noteXrefMapper, type, fileXref, defined);

          case XrefType.Source:
            return GetXRef(ref sourceXrefMapper, type, fileXref, defined);

          case XrefType.Repository:
            return GetXRef(ref repositoryXrefMapper, type, fileXref, defined);

          case XrefType.Submission:
            return GetXRef(ref submissionXrefMapper, type, fileXref, defined);

          case XrefType.Submitter:
            return GetXRef(ref submitterXrefMapper, type, fileXref, defined);

          default:
            //DebugStringAdd("Unknown xref tag type:" + type);
            return "";
        }
      }

    }

    private class UnhandledTag
    {
      public string tagName;
      public int count;

      public UnhandledTag(string name)
      {
        tagName = name;
        count = 0;
      }
    }

    private IDictionary<string, int> unhandledTagList;

    enum ExportSoftwareType
    {
      Unknown,
      GeniDotCom
    };


    public GedcomTreeDecoderClass(ref IFamilyTreeStoreBaseClass inFamilyTree, ref FileImportResult importResult)
    {
      trace = new TraceSource("GedcomTreeDecoderClass", SourceLevels.Warning);
      familyTree = inFamilyTree;

      decodedLines = 0;
      characterSet = GedcomFileCharacterSet.Utf8;
      parser = new GedcomParserUtility();

      if (importResult == null)
      {
        return;
      }

      xrefMappers = new GedcomMappers(inFamilyTree);


      //exportSoftware = ExportSoftwareType.Unknown;

      decodeGedcomBody = false;
      decodingCompleted = false;
      //geniAddressNoteSubtagParsed = false;
      unhandledTagList = new Dictionary<string, int>();
      anselDecoder = new AnselDecoder();

      this.importResult = importResult;
    }

    public void DebugStringAdd(string str)
    {
      importResult.AddString(str);
    }

    public void SetImportResult(ref FileImportResult importResult)
    {
      this.importResult = importResult;
    }
    public FileImportResult GetImportResult()
    {
      return this.importResult;
    }


    void AddUnhandledTag(TagStack tagStack, GedcomLineData lineData)
    {
      if(unhandledTagList.ContainsKey(tagStack.GetTagStack()))
      {
        unhandledTagList[tagStack.GetTagStack()]++;
      }
      else
      {
        unhandledTagList.Add(tagStack.GetTagStack(), 1);
      }

    }

    private void HandleUnknownTag(TagStack tagStack, GedcomLineData lineData)
    {
      DebugStringAdd("Line: " + lineData.lineNo + " :Unknown tag " + tagStack.GetTagStack() + ": " + lineData.level + " tag:" + lineData.tagString + " value:[" + lineData.valueString + "] xref:[" + lineData.xrefIdString + "]");

      AddUnhandledTag(tagStack, lineData);
      CheckUndecodedChildren(tagStack, lineData.child);
    }

    private bool CheckTag(ref TagStack tagStack, GedcomLineData lineData)
    {
      while ((tagStack.GetLevel() + 1) > lineData.level)
      {
        tagStack.RemoveLast();
      }
      if ((tagStack.GetLevel() + 1) == lineData.level)
      {
        tagStack.AddTag(lineData.tagString);
      }
      else if (tagStack.GetLevel() > lineData.level)
      {
        HandleUnknownTag(tagStack, lineData);
        DebugStringAdd("Line: " + lineData.lineNo + " :Bad tag level " + tagStack.GetTagStack() + " " + lineData.level + " " + lineData.tagString + " value:[" + lineData.valueString + "] xref:[" + lineData.xrefIdString + "]");
        return false;
      }
      if (tagStack.GetLevel() == lineData.level)
      {
        return true;
      }
      HandleUnknownTag(tagStack, lineData);
      DebugStringAdd("Line: " + lineData.lineNo + " :Bad tag level " + tagStack.GetTagStack() + " " + lineData.level + " " + lineData.tagString + " value:[" + lineData.valueString + "] xref:[" + lineData.xrefIdString + "]");
      return false;
    }

    private void CheckUndecodedChildren(TagStack tagStack, GedcomLineObject gedcomLineObject)
    {
      if (gedcomLineObject != null)
      {
        for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
        {
          GedcomLineData lineData = gedcomLineObject.gedcomLines[i];
          if (CheckTag(ref tagStack, lineData))
          {
            //tagStack.AddTag(firstLineData.tagString);
            //tagStack.AddTag(lineData.tagString);
            HandleUnknownTag(tagStack, lineData);
            //tagStack.RemoveLast();
          }
        }
        //DebugStringAdd("Warning: " + tagStack.GetTagStack() + " has " + gedcomLineObject.gedcomLines.Count + " undecoded children");
        decodedLines += gedcomLineObject.gedcomLines.Count;
      }
    }

    private bool DecodeGedcomHead(TagStack tagStack, GedcomLineObject gedcomLineObject)
    {
      gedcomLineObject.ObjectDecodeStart("Head", gedcomLineObject);
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        //DebugStringAdd("  submitter-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
        if (CheckTag(ref tagStack, lineData))
        {
          //tagStack.AddTag(lineData.tagString);
          switch (lineData.tagString)
          {
            /*case "HEAD":
              {
                DebugStringAdd("Line: " + lineData.lineNo + " HEAD tag : " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;*/

            case "DATE":
              {
                FamilyDateTimeClass tempDate = new FamilyDateTimeClass();
                if (!DecodeGedcomDate(tagStack, lineData, ref tempDate))
                {
                  DebugStringAdd("Line: " + lineData.lineNo + ": Unable to decode " + tagStack.GetTagStack() + " tag: " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                }
                else
                {
                  //DebugStringAdd("decoded date: " + tempDate);
                  familyTree.SetDate(tempDate);
                }
              }
              break;

            case "SUBM":
              {
                if (ValidateXrefName(lineData.valueString))
                {
                  SubmitterXrefClass submitterXref = new SubmitterXrefClass(xrefMappers.GetLocalXRef(XrefType.Submitter, GetXrefName(lineData.valueString)));

                  familyTree.SetSubmitterXref(submitterXref);
                }
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "GEDC":
              {
                if (lineData.child != null)
                {
                  for (int j = 0; j < lineData.child.gedcomLines.Count; j++)
                  {
                    GedcomLineData subLineData = lineData.child.gedcomLines[j];

                    if (CheckTag(ref tagStack, subLineData))
                    {
                      //tagStack.AddTag(firstLineData.tagString);
                      //tagStack.AddTag(subLineData.tagString);
                      switch (subLineData.tagString)
                      {
                        case "VERS":
                          familyTree.SetSourceFileTypeVersion(subLineData.valueString);
                          break;
                        case "FORM":
                          familyTree.SetSourceFileTypeFormat(subLineData.valueString);
                          break;
                        default:
                          HandleUnknownTag(tagStack, subLineData);
                          //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown tag " + tagStack.GetTagStack() +" :" + subLineData.valueString);
                          CheckUndecodedChildren(tagStack, subLineData.child);
                          break;
                      }
                      //tagStack.RemoveLast();
                    }
                    //CheckUndecodedChildren(tagStack, subLineData.child);
                    //DebugStringAdd("Line: " + lineData.lineNo + " HEAD.GEDC." + subLineData.tagString + ":" + subLineData.valueString);
                  }
                }
                decodedLines += lineData.child.gedcomLines.Count;
              }
              break;

            case "SOUR":
              {
                familyTree.SetSourceName(lineData.valueString);

                /*if (lineData.valueString == "Geni.com")
                {
                  exportSoftware = ExportSoftwareType.GeniDotCom;
                }*/

                if (lineData.child != null)
                {
                  for (int j = 0; j < lineData.child.gedcomLines.Count; j++)
                  {
                    GedcomLineData subLineData = lineData.child.gedcomLines[j];

                    if (CheckTag(ref tagStack, subLineData))
                    {
                      //tagStack.AddTag(firstLineData.tagString);
                      //tagStack.AddTag(subLineData.tagString);
                      switch (subLineData.tagString)
                      {
                        case "VERS":
                          DebugStringAdd("Line: " + subLineData.lineNo + ":" + tagStack.GetTagStack() + ":" + subLineData.valueString);
                          break;
                        case "NAME":
                          DebugStringAdd("Line: " + subLineData.lineNo + ":" + tagStack.GetTagStack() + ":" + subLineData.valueString);
                          break;
                        case "PHON":
                          DebugStringAdd("Line: " + subLineData.lineNo + ":" + tagStack.GetTagStack() + ":" + subLineData.valueString);
                          break;
                        case "CORP":
                          {
                            CorporationClass tempCorporation = new CorporationClass();
                            DebugStringAdd("Line: " + subLineData.lineNo + ":" + tagStack.GetTagStack() + ":" + subLineData.valueString);
                            //CheckUndecodedChildren("HEAD.CORP", subLineData.child);
                            if (subLineData.child != null)
                            {
                              //AddressClass tempAddress = new AddressClass();
                              if (DecodeGedcomCorporation(tagStack, subLineData.child, ref tempCorporation))
                              {
                                //tempAdd

                              }
                            }
                          }
                          break;
                        case "DATA":
                          DebugStringAdd("Line: " + subLineData.lineNo + ": " + tagStack.GetTagStack() + " " + subLineData.valueString);
                          if (subLineData.child != null)
                          {
                            for (int k = 0; k < subLineData.child.gedcomLines.Count; k++)
                            {
                              GedcomLineData subSubLineData = subLineData.child.gedcomLines[k];

                              if (CheckTag(ref tagStack, subLineData))
                              {
                                //tagStack.AddTag(firstLineData.tagString);
                                //tagStack.AddTag(subSubLineData.tagString);
                                switch (subSubLineData.tagString)
                                {
                                  case "DATE":
                                    DebugStringAdd("Line: " + subSubLineData.lineNo + ": " + tagStack.GetTagStack() + " Date: " + subSubLineData.valueString);
                                    break;
                                  case "COPR":
                                    DebugStringAdd("Line: " + subSubLineData.lineNo + ": " + tagStack.GetTagStack() + " Corporation: " + subSubLineData.valueString);
                                    break;
                                  default:
                                    HandleUnknownTag(tagStack, subLineData);
                                    //DebugStringAdd("Line: " + subSubLineData.lineNo + ": Unknown tag " + tagStack.GetTagStack() + " :" + subSubLineData.valueString);
                                    CheckUndecodedChildren(tagStack, subLineData.child);
                                    break;
                                }
                                //tagStack.RemoveLast();
                              }
                            }
                            decodedLines += subLineData.child.gedcomLines.Count;
                          }

                          break;
                        default:
                          HandleUnknownTag(tagStack, subLineData);
                          //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown tag " + tagStack.GetTagStack() + ":" + subLineData.valueString);
                          CheckUndecodedChildren(tagStack, subLineData.child);
                          break;
                      }
                      //tagStack.RemoveLast();
                    }
                    //DebugStringAdd("Line: " + lineData.lineNo + " " + tagStack.GetTagStack() + "." + subLineData.tagString + ":" + subLineData.valueString);
                  }
                  decodedLines += lineData.child.gedcomLines.Count;
                }
              }
              break;

            case "DEST":
              {
                DebugStringAdd("Line: " + lineData.lineNo + " " + tagStack.GetTagStack() + " Destination system : " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "SUBN":
              {
                if (ValidateXrefName(lineData.valueString))
                {
                  SubmissionXrefClass submissionXref = new SubmissionXrefClass(xrefMappers.GetLocalXRef(XrefType.Submission, GetXrefName(lineData.valueString)));

                  //familyTree.AddSubmission(submissionXref);
                  DebugStringAdd("Line: " + lineData.lineNo + " " + tagStack.GetTagStack() + " Submission: " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                }
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "FILE":
              {
                DebugStringAdd("Line: " + lineData.lineNo + " " + tagStack.GetTagStack() + " File : " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
              }
              break;

            case "COPR":
              {
                DebugStringAdd("Line: " + lineData.lineNo + " " + tagStack.GetTagStack() + " Corporation : " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
              }
              break;

            case "LANG":
              {
                DebugStringAdd("Line: " + lineData.lineNo + " " + tagStack.GetTagStack() + " Language : " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
              }
              break;

            case "NOTE":
              {
                String tempNote = lineData.valueString;
                //DebugStringAdd("HEAD.NOTE tag : " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                if (lineData.child != null)
                {
                  DecodeGedcomStringConcatenation(tagStack, lineData.child, ref tempNote);
                }
                DebugStringAdd("Line: " + lineData.lineNo + " " + tagStack.GetTagStack() + " Note : {" + tempNote + "}" + tempNote.Length);
              }
              break;


            case "CHAR":
              //DebugStringAdd("Line: " + lineData.lineNo + " " + tagStack.GetTagStack() + " Character set " +lineData.valueString);
              switch (lineData.valueString)
              {
                case "ANSEL":
                  familyTree.SetCharacterSet(FamilyTreeCharacterSet.Ansel);
                  characterSet = GedcomFileCharacterSet.Ansel;
                  //lineData.SetCharacterSet(GedcomFileCharacterSet.Ansel);
                  break;
                case "ASCII":
                  familyTree.SetCharacterSet(FamilyTreeCharacterSet.Ascii);
                  characterSet = GedcomFileCharacterSet.Ascii;
                  //lineData.SetCharacterSet(GedcomFileCharacterSet.Ascii);
                  break;
                case "UNICODE":
                  familyTree.SetCharacterSet(FamilyTreeCharacterSet.Unicode);
                  characterSet = GedcomFileCharacterSet.Unicode;
                  //lineData.SetCharacterSet(GedcomFileCharacterSet.Unicode);
                  break;
                case "UTF-8":
                  familyTree.SetCharacterSet(FamilyTreeCharacterSet.Utf8);
                  characterSet = GedcomFileCharacterSet.Utf8;
                  //lineData.SetCharacterSet(GedcomFileCharacterSet.Utf8);
                  break;
                default:
                  DebugStringAdd("Line: " + lineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " character set " + lineData.valueString);
                  break;
              }
              if (lineData.child != null)
              {
                for (int j = 0; j < lineData.child.gedcomLines.Count; j++)
                {
                  GedcomLineData subLineData = lineData.child.gedcomLines[j];

                  if (CheckTag(ref tagStack, subLineData))
                  {
                    //tagStack.AddTag(firstLineData.tagString);
                    //tagStack.AddTag(lineData.tagString);
                    switch (subLineData.tagString)
                    {
                      case "VERS":
                        DebugStringAdd("Line: " + subLineData.lineNo + ": " + tagStack.GetTagStack() + " Character version: " + subLineData.valueString);
                        break;

                      default:
                        DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown tag " + tagStack.GetTagStack() + " " + subLineData.valueString);
                        break;
                    }
                    //tagStack.RemoveLast();
                  }
                }
                decodedLines += lineData.child.gedcomLines.Count;
              }
              break;

            default:
              {
                HandleUnknownTag(tagStack, lineData);
                //DebugStringAdd("Line: " + lineData.lineNo + ": Error: Unknown " + tagStack.GetTagStack() + " tag type: " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;
          }
        }
        //tagStack.RemoveLast();
      }

      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    public void SetCharacterSet(GedcomFileCharacterSet charSet)
    {
      characterSet = charSet;
    }
    public GedcomFileCharacterSet GetCharacterSet()
    {
      return characterSet;
    }
    private bool DecodeGedcomStringConcatenation(TagStack tagStack, GedcomLineObject gedcomLineObject, ref String inString)
    {
      gedcomLineObject.ObjectDecodeStart("String", gedcomLineObject);

      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        //DebugStringAdd("  submitter-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
        if (CheckTag(ref tagStack, lineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(lineData.tagString);
          switch (lineData.tagString)
          {
            case "CONC":
              {
                inString += lineData.valueString;
              }
              break;

            case "CONT":
              {
                CheckLineFeed(ref inString);
                //inString += "\n";
                inString += lineData.valueString;
              }
              break;

            default:
              {
                HandleUnknownTag(tagStack, lineData);
                //DebugStringAdd("Line: " + lineData.lineNo + ": Error: " + tagStack.GetTagStack() + " sub tag type: " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;
          }
        }
        //tagStack.RemoveLast();
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    private bool DecodeGedcomCorporation(TagStack tagStack, GedcomLineObject gedcomLineObject, ref CorporationClass corporation)
    {
      gedcomLineObject.ObjectDecodeStart("Corporation", gedcomLineObject);
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        //DebugStringAdd("  submitter-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
        if (CheckTag(ref tagStack, lineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(lineData.tagString);
          switch (lineData.tagString)
          {
            case "ADDR":
              {
                AddressClass tempAddress = corporation.address;
                string streetAddressString = "";
                if (lineData.valueString.Length > 0)
                {
                  //tempAddress.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, subLineData.valueString);
                  streetAddressString += lineData.valueString;
                  //DebugStringAdd("Warning! addr contains data" + subLineData.tagString + ":" + subLineData.valueString);
                }
                if (lineData.child != null)
                {
                  if (DecodeGedcomAddress(tagStack, lineData.child, ref tempAddress, ref streetAddressString))
                  {
                    //tempEvent.AddAddress(tempAddress);
                  }
                }
                if (streetAddressString.Length > 0)
                {
                  tempAddress.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, streetAddressString);
                }
                corporation.address = tempAddress;
              }
              break;

            case "PHON":
              {
                corporation.address.AddAddressPart(AddressPartClass.AddressPartType.PhoneNumber, lineData.valueString);
              }
              break;

            default:
              {
                HandleUnknownTag(tagStack, lineData);
                //DebugStringAdd("Line: " + lineData.lineNo + ": Error: " + tagStack.GetTagStack() + " sub tag type: " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    private bool DecodeGedcomSubmitterRecord(TagStack tagStack, GedcomLineObject gedcomLineObject, string xrefIdString)
    {
      gedcomLineObject.ObjectDecodeStart("Submitter", gedcomLineObject);

      SubmitterClass tempIndividual = new SubmitterClass();

      if (xrefIdString.Length > 0)
      {
        //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
        tempIndividual.SetXrefName(xrefMappers.GetLocalXRef(XrefType.Submitter, xrefIdString, true));
      }
      else
      {
        return false;
      }
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        if (CheckTag(ref tagStack, lineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //DebugStringAdd("  submitter-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
          //tagStack.AddTag(lineData.tagString);
          switch (lineData.tagString)
          {
            case "NAME":
              {
                PersonalNameClass tempName = new PersonalNameClass();

                tempName.SetName(PersonalNameClass.PartialNameType.NameString, lineData.valueString);

                tempIndividual.SetPersonalName(tempName);

                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "LANG":
              {
                DebugStringAdd("Line: " + lineData.lineNo + ": " + tagStack.GetTagStack() + " submitter language: " + lineData.valueString);
              }
              break;

            case "CHAN":
              {
                DebugStringAdd("Line: " + lineData.lineNo + ": " + tagStack.GetTagStack() + " submitter change: " + lineData.valueString);
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomChange(tagStack, lineData.child, ref tempEvent);
                  tempIndividual.AddEvent(tempEvent);
                }
                else
                {
                  DebugStringAdd("Line: " + lineData.lineNo + ": NOTE " + tagStack.GetTagStack() + " no children: " + lineData.valueString);
                }
              }
              break;

            case "ADDR":
              {
                //DebugStringAdd("Subm.ADDR tag type: " + lineData.tagString + " " + lineData.valueString);
                AddressClass tempAddress = tempIndividual.GetAddress();
                string streetAddressString = "";
                if (lineData.valueString.Length > 0)
                {
                  //tempAddress.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, subLineData.valueString);
                  streetAddressString += lineData.valueString;
                  //DebugStringAdd("Warning! addr contains data" + subLineData.tagString + ":" + subLineData.valueString);
                }
                if (lineData.child != null)
                {
                  if (DecodeGedcomAddress(tagStack, lineData.child, ref tempAddress, ref streetAddressString))
                  {
                    //tempEvent.AddAddress(tempAddress);
                  }
                }
                if (streetAddressString.Length > 0)
                {
                  tempAddress.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, streetAddressString);
                }
                tempIndividual.AddAddress(tempAddress);
              }
              break;

            case "PHON":
              {
                //DebugStringAdd("Subm.ADDR tag type: " + lineData.tagString + " " + lineData.valueString);
                tempIndividual.AddAddress(AddressPartClass.AddressPartType.PhoneNumber, lineData.valueString);
              }
              break;
            default:
              {
                HandleUnknownTag(tagStack, lineData);
                //DebugStringAdd("Line: " + lineData.lineNo + ": Error: Unknown " + tagStack.GetTagStack() + " tag type: " + lineData.level + ":" + lineData.tagString + ":" + lineData.valueString);
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;
          }
          //tagStack.RemoveLast();
        }
      }


      decodedLines += gedcomLineObject.gedcomLines.Count;
      //familyTree.submitterList.Add(tempIndividual);
      familyTree.AddSubmitter(tempIndividual);
      return true;
    }

    private string ProcessCombiningDiacriticalMarks(string unicodeString)
    {
      switch(characterSet)
      {
        case GedcomFileCharacterSet.Unicode:
        case GedcomFileCharacterSet.Utf16BE:
        case GedcomFileCharacterSet.Utf16LE:
          return unicodeString.Normalize();

        case GedcomFileCharacterSet.Ansel:
          {
            return anselDecoder.DecodeString(unicodeString).Normalize();
          }
        default:
          return unicodeString;
      }
    }

    private bool DecodeGedcomName(TagStack tagStack, GedcomLineObject gedcomLineObject, ref PersonalNameClass tempName)
    {
      gedcomLineObject.ObjectDecodeStart("Name", gedcomLineObject);

      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];

        subLineData.valueString = ProcessCombiningDiacriticalMarks(subLineData.valueString);

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "GIVN":

              //DebugStringAdd("given name:" + subLineData.valueString + "<>" + tempName.GetName());
              tempName.SetName(PersonalNameClass.PartialNameType.GivenName, subLineData.valueString);
              //DebugStringAdd("Name sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
              break;

            case "SURN":
              //DebugStringAdd("surname:" + subLineData.valueString + "<>" + tempName.GetName());
              tempName.SetName(PersonalNameClass.PartialNameType.Surname, subLineData.valueString);
              //DebugStringAdd("Name sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
              /*if (subLineData.valueString.Length > 5)
              {
                if (subLineData.valueString.Substring(0, 4) == "Lind")
                {
                  DebugStringAdd("name = " + subLineData.valueString);

                  for (int j = 0; j < subLineData.valueString.Length; j++)
                  {
                    trace.TraceInformation((int)subLineData.valueString[j]);
                    trace.TraceInformation(" ");
                  }
                  DebugStringAdd();
                }
              }*/
              break;

            case "NICK":
              tempName.SetName(PersonalNameClass.PartialNameType.Nickname, subLineData.valueString);
              //DebugStringAdd("Name sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
              break;

            case "NSFX":
              tempName.SetName(PersonalNameClass.PartialNameType.Suffix, subLineData.valueString);
              //DebugStringAdd("Name sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
              break;

            /*case "_MAR": // Geni.com
              if (exportSoftware == ExportSoftwareType.GeniDotCom)
              {
                tempName.SetName(PersonalNameClass.PartialNameType.BirthSurname, subLineData.valueString);
                //DebugStringAdd("Name sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
              }
              else
              {
                DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " sub-tag(geni?): " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
              }
              break;*/

            case "NOTE":
              {
                NoteClass tempNote = new NoteClass();

                if (subLineData.valueString.Length > 0)
                {
                  subLineData.valueString = ProcessCombiningDiacriticalMarks(subLineData.valueString);
                  if (ValidateXrefName(subLineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(subLineData.valueString)));

                    tempName.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(subLineData.valueString, false);
                  }
                }
                if (subLineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, subLineData.child, ref tempNote);
                }
                tempName.AddNote(tempNote);
              }
              break;

            case "SOUR":
              {
                SourceChoiceClass tempSource;
                if(ValidateXrefName(subLineData.valueString))
                {
                  tempSource = new SourceChoiceClass(true, xrefMappers.GetLocalXRef(XrefType.Source, GetXrefName(subLineData.valueString)));
                }
                else
                {
                  tempSource = new SourceChoiceClass(false, subLineData.valueString);
                }

                //DebugStringAdd("Unknown NOTE sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                DecodeGedcomSourceCitation(tagStack, subLineData, ref tempSource);
                if (tempSource.GetXrefType())
                {
                  tempName.AddSourceXref(tempSource.sourceXref);
                }
                else
                {
                  tempName.AddSource(tempSource.source);
                }
              }
              break;

            default:
              HandleUnknownTag(tagStack, subLineData);
              //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown name sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
              CheckUndecodedChildren(tagStack, subLineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    void CheckLineFeed(ref string str)
    {
      if (str.Length > 0)
      {
        str += "\n";
      }
    }

    private bool DecodeGedcomAddress(TagStack tagStack, GedcomLineObject gedcomLineObject, ref AddressClass tempAddress, ref string addressPart)
    {
      gedcomLineObject.ObjectDecodeStart("Adress", gedcomLineObject);
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];

        subLineData.valueString = ProcessCombiningDiacriticalMarks(subLineData.valueString);

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "CONT":
              {
                //tempAddress.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, subLineData.valueString);
                CheckLineFeed(ref addressPart);
                //addressPart += "\n";
                addressPart += subLineData.valueString;
              }
              break;

            case "POST":
              {
                tempAddress.AddAddressPart(AddressPartClass.AddressPartType.PostCode, subLineData.valueString);
              }
              break;

            case "CITY":
              {
                tempAddress.AddAddressPart(AddressPartClass.AddressPartType.City, subLineData.valueString);
              }
              break;

            case "STAE":
              {
                tempAddress.AddAddressPart(AddressPartClass.AddressPartType.State, subLineData.valueString);
              }
              break;

            case "CTRY":
              {
                tempAddress.AddAddressPart(AddressPartClass.AddressPartType.Country, subLineData.valueString);
              }
              break;

            case "PHON":
              {
                tempAddress.AddAddressPart(AddressPartClass.AddressPartType.PhoneNumber, subLineData.valueString);
              }
              break;

            case "ADDR":
              {
                tempAddress.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, subLineData.valueString);
              }
              break;

            case "ADR1":
              {
                tempAddress.AddAddressPart(AddressPartClass.AddressPartType.Line1, subLineData.valueString);
              }
              break;

            case "ADR2":
              {
                tempAddress.AddAddressPart(AddressPartClass.AddressPartType.Line2, subLineData.valueString);
              }
              break;

            /*case "NOTE": // geni.com. violates standard?
              if (exportSoftware == ExportSoftwareType.GeniDotCom)
              {
                //String tempAddressData = "";
                //AddressPartClass.AddressPartType addressType = AddressPartClass.AddressPartType.Unknown;

                if (!geniAddressNoteSubtagParsed)
                {
                  DebugStringAdd("address geni sub-tag no 1 found (NOTE): " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);

                  geniAddressNoteSubtagParsed = true;
                }


                //DebugStringAdd("address geni sub-tag (NOTE): " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                if (subLineData.valueString.Substring(0, 6) == "{geni:")
                {
                  int splitIndex = subLineData.valueString.IndexOf(' ');
                  String geniTag = subLineData.valueString.Substring(0, splitIndex);
                  String geniData = subLineData.valueString.Substring(splitIndex + 1); // skip space
                  //DebugStringAdd("address geni sub-tag (NOTE): detected at " + splitIndex + "[" + geniTag + "][" + geniData + "]");

                  switch (geniTag)
                  {
                    case "{geni:county}":
                      CheckLineFeed(ref addressPart);
                      addressPart += geniData;
                      break;

                    case "{geni:place_name}":
                      CheckLineFeed(ref addressPart);
                      addressPart += geniData;
                      break;

                    case "{geni:location_name}":
                      CheckLineFeed(ref addressPart);
                      addressPart += geniData;
                      break;

                    default:
                      HandleUnknownTag(tagStack, subLineData);
                      //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " geni-sub-tag : " + splitIndex + "[" + geniTag + "][" + geniData + "]");
                      CheckUndecodedChildren(tagStack, subLineData.child);
                      break;

                  }
                }
                if (subLineData.child != null)
                {
                  DecodeGedcomStringConcatenation(tagStack, subLineData.child, ref addressPart);
                }
              }
              else
              {
                //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " tag (geni.com mismatch)?: " + subLineData.tagString + ":" + subLineData.valueString);
                HandleUnknownTag(tagStack, subLineData);
                CheckUndecodedChildren(tagStack, subLineData.child);
              }
              break;*/

            default:
              HandleUnknownTag(tagStack, subLineData);
              //DebugStringAdd("Line: " + subLineData.lineNo + ": Error: Unknown " + tagStack.GetTagStack() + " sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
              CheckUndecodedChildren(tagStack, subLineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    private bool DecodeGedcomNoteStructure(TagStack tagStack, GedcomLineObject gedcomLineObject, ref NoteClass tempNote)
    {
      gedcomLineObject.ObjectDecodeStart("NoteStructure", gedcomLineObject);
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];
        subLineData.valueString = ProcessCombiningDiacriticalMarks(subLineData.valueString);

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "CONT":
              {
                tempNote.Concatenate(subLineData.valueString, true);
              }
              break;
            case "CONC":
              {
                tempNote.Concatenate(subLineData.valueString, false);
              }
              break;
            case "SOUR":
              {
                SourceChoiceClass tempSource;
                if (ValidateXrefName(subLineData.valueString))
                {
                  tempSource = new SourceChoiceClass(true, xrefMappers.GetLocalXRef(XrefType.Source, GetXrefName(subLineData.valueString)));
                }
                else
                {
                  tempSource = new SourceChoiceClass(false, subLineData.valueString);
                }
                //DebugStringAdd("NOTE:SOUR sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                DecodeGedcomSourceCitation(tagStack, subLineData, ref tempSource);
                //tempNote.AddSource(tempSource);
                if (tempSource.GetXrefType())
                {
                  tempNote.AddSourceXref(tempSource.sourceXref);
                }
                else
                {
                  tempNote.AddSource(tempSource.source);
                }
              }
              break;
            case "CHAN":
              {
                if (subLineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomChange(tagStack, subLineData.child, ref tempEvent);
                  tempNote.SetUpdateEvent(tempEvent);
                }
                else
                {
                  DebugStringAdd("Line: " + subLineData.lineNo + ": " + tagStack.GetTagStack() + " no children: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                }
              }
              break;
            default:
              HandleUnknownTag(tagStack, subLineData);
              //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown tag " + tagStack.GetTagStack() + ":" + subLineData.valueString);
              CheckUndecodedChildren(tagStack, subLineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    /*bool IsDigitsOnly(string str)
    {
      foreach (char c in str)
      {
        if ((c < '0') || (c > '9'))
        {
          return false;
        }
      }
      return true;
    }

    private bool ParseInt(string str, ref int result)
    {
      if(str.Length < 8)
      {
        if(IsDigitsOnly(str))
        {
          int i = 0;

          foreach(char c in str)
          {
            i = (i * 10) + (c - '0');
          }

          result = i;
          return true;
        }
      }
      return false;
    }*/

    private bool DecodeGedcomEventDetail(TagStack tagStack, GedcomLineObject gedcomLineObject, IndividualEventClass.EventType eventType, ref IndividualEventClass tempEvent)
    {
      bool familyType = false;

      gedcomLineObject.ObjectDecodeStart("EventDetail", gedcomLineObject);

      if((eventType >= IndividualEventClass.EventType.FamEngagement) && (eventType <= IndividualEventClass.EventType.FamRecordChange))
      {
        familyType = true;
      }

      tempEvent.SetEventType(eventType);
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];

        subLineData.valueString = ProcessCombiningDiacriticalMarks(subLineData.valueString);

        FamilyDateTimeClass tempDate = new FamilyDateTimeClass();

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "DATE":
              {
                if (!DecodeGedcomDate(tagStack, subLineData, ref tempDate))
                {
                  DebugStringAdd("Line: " + subLineData.lineNo + ": Unable to decode " + tagStack.GetTagStack() + " tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                }
                else
                {
                  //DebugStringAdd("decoded date: " + tempDate);
                  tempEvent.SetDate(tempDate);
                }
              }
              break;

            case "PLAC":
              {
                //String tempString = "";
                PlaceStructureClass place = new PlaceStructureClass();


                if (subLineData.valueString.Length > 0)
                {
                  place.SetPlace(subLineData.valueString);
                  //tempEvent.AddAddressPart(AddressPartClass.AddressPartType.Place, subLineData.valueString);
                }
                if (subLineData.child != null)
                {
                  if (DecodeGedcomPlaceStructure(tagStack, subLineData, ref place))
                  {
                  }
                }
                tempEvent.AddPlace(place);
                //tempString = ProcessCombiningDiacriticalMarks(tempString);

                //CheckUndecodedChildren("EVEN.PLAC", subLineData.child);
                //tempEvent.AddAddressPart(AddressPartClass.AddressPartType.Place, tempString);
              }
              break;

            case "ADDR":
              {
                AddressClass tempAddress = new AddressClass();
                string streetAddressString = "";
                if (subLineData.valueString.Length > 0)
                {
                  //tempAddress.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, subLineData.valueString);
                  streetAddressString += subLineData.valueString;
                  //DebugStringAdd("Warning! addr contains data" + subLineData.tagString + ":" + subLineData.valueString);
                }
                if (subLineData.child != null)
                {
                  if (DecodeGedcomAddress(tagStack, subLineData.child, ref tempAddress, ref streetAddressString))
                  {
                    //tempEvent.AddAddress(tempAddress);
                  }
                }
                if (streetAddressString.Length > 0)
                {
                  tempAddress.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, streetAddressString);
                }
                tempEvent.AddAddress(tempAddress);
              }
              break;


            case "NOTE":
              {
                NoteClass tempNote = new NoteClass();

                if (subLineData.valueString.Length > 0)
                {
                  subLineData.valueString = ProcessCombiningDiacriticalMarks(subLineData.valueString);
                  if (ValidateXrefName(subLineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(subLineData.valueString)));

                    tempEvent.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(subLineData.valueString, false);
                  }
                }
                if (subLineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, subLineData.child, ref tempNote);
                }
                tempEvent.AddNote(tempNote);
              }
              break;

            case "SOUR":
              {
                SourceChoiceClass tempSource;
                if (ValidateXrefName(subLineData.valueString))
                {
                  tempSource = new SourceChoiceClass(true, xrefMappers.GetLocalXRef(XrefType.Source, GetXrefName(subLineData.valueString)));
                }
                else
                {
                  tempSource = new SourceChoiceClass(false, subLineData.valueString);
                }

                //DebugStringAdd("Unknown NOTE sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                DecodeGedcomSourceCitation(tagStack, subLineData, ref tempSource);
                if (tempSource.GetXrefType())
                {
                  tempEvent.AddSourceXref(tempSource.sourceXref);
                }
                else
                {
                  tempEvent.AddSource(tempSource.source);
                }
                //SourceClass source = new SourceClass(subLineData.valueString);

                //DebugStringAdd("SOUR tag: " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                //DecodeGedcomSourceCitation(tagStack, subLineData, ref source);
                //tempEvent.AddSource(source);
              }
              break;

            case "OBJE":
              {
                if (subLineData.child != null)
                {
                  MultimediaLinkClass tempMultimediaLink = new MultimediaLinkClass();

                  DecodeGedcomMultimediaLink(tagStack, subLineData.child, tempMultimediaLink);

                  tempEvent.AddMultimediaLink(tempMultimediaLink);
                }
              }
              break;

            case "TYPE":
              {
                //DebugStringAdd("decoded date: " + tempDate);
                tempEvent.SetEventType(subLineData.valueString);
              }
              break;

            case "CAUS":
              {
                //DebugStringAdd("decoded date: " + tempDate);
                tempEvent.SetCause(subLineData.valueString);
              }
              break;

            case "FAMC":
              {
                if (ValidateXrefName(subLineData.valueString))
                {
                  FamilyXrefClass familyXref = new FamilyXrefClass(xrefMappers.GetLocalXRef(XrefType.Family, GetXrefName(subLineData.valueString)));

                  tempEvent.SetFamilyXref(familyXref);
                }
              }
              break;

            /*case "COMM": // Anarkiv 7.0 non-standard!
            case "CONT": // Anarkiv 7.0 non-standard!
              //case "MILI": // Anarkiv 7.0 non-standard!
              //case "FACT": // Anarkiv 7.0 non-standard!
              {
                //DebugStringAdd("decoded date: " + tempDate);
              }
              break;*/

            default:
              if (familyType)
              {
                switch (subLineData.tagString)
                {
                  case "HUSB":
                  case "WIFE":
                    //DebugStringAdd("Line: " + subLineData.lineNo + ": Husband/wife event " + tagStack.GetTagStack() + ":" + subLineData.valueString);
                    if (subLineData.child != null)
                    {
                      IndividualEventClass.ParentType parentType = IndividualEventClass.ParentType.Wife;
                      if(subLineData.tagString == "HUSB")
                      {
                        parentType = IndividualEventClass.ParentType.Husband;
                      }

                      for (int j = 0; j < subLineData.child.gedcomLines.Count; j++)
                      {
                        GedcomLineData subSubLineData = subLineData.child.gedcomLines[j];

                        if (CheckTag(ref tagStack, subSubLineData))
                        {
                          //tagStack.AddTag(firstLineData.tagString);
                          //tagStack.AddTag(subSubLineData.tagString);
                          switch (subSubLineData.tagString)
                          {
                            case "AGE":
                              tempEvent.AddParentAge(parentType, subSubLineData.valueString);
                              //DebugStringAdd("Line: " + subSubLineData.lineNo + ": Husband/wife-age event " + tagStack.GetTagStack() + ":" + subSubLineData.valueString);
                              break;

                            default:
                              HandleUnknownTag(tagStack, subLineData);
                              //DebugStringAdd("Line: " + subSubLineData.lineNo + ": Unknown tag " + tagStack.GetTagStack() + ":" + subSubLineData.valueString);
                              CheckUndecodedChildren(tagStack, subLineData.child);
                              break;
                          }
                          //tagStack.RemoveLast();
                        }
                      }
                      decodedLines += gedcomLineObject.gedcomLines.Count;
                    }
                    break;

                  default:
                    HandleUnknownTag(tagStack, subLineData);
                    //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown tag " + tagStack.GetTagStack() + ":" + subLineData.valueString);
                    CheckUndecodedChildren(tagStack, subLineData.child);
                    break;
                }
              }
              else
              {
                HandleUnknownTag(tagStack, subLineData);
                //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown tag " + tagStack.GetTagStack() + ":" + subLineData.valueString);
                CheckUndecodedChildren(tagStack, subLineData.child);
              }
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;

      return true;
    }


    private IndividualEventClass.EventType DecodeGedcomIndividualEventType(TagStack tagStack, GedcomLineData lineData)
    {
      IndividualEventClass.EventType eventType = IndividualEventClass.EventType.Unknown;
      switch (lineData.tagString)
      {
        case "BIRT":
          eventType = IndividualEventClass.EventType.Birth;
          break;

        case "BAPL":
          eventType = IndividualEventClass.EventType.BaptismLDS;
          break;
        case "BAPM":
          eventType = IndividualEventClass.EventType.Baptism;
          break;
        case "CHR":
          eventType = IndividualEventClass.EventType.Christening;
          break;
        case "CHRA":
          eventType = IndividualEventClass.EventType.AdultChristening;
          break;

        case "ADOP":
          eventType = IndividualEventClass.EventType.Adoption;
          break;
        case "BARM":
          eventType = IndividualEventClass.EventType.BarMitzwah;
          break;
        case "BASM":
          eventType = IndividualEventClass.EventType.BasMitzwah;
          break;
        case "BLES":
          eventType = IndividualEventClass.EventType.Blessing;
          break;
        case "FCOM":
          eventType = IndividualEventClass.EventType.FirstCommunion;
          break;
        case "RETI":
          eventType = IndividualEventClass.EventType.Retired;
          break;

        case "CAST":
          eventType = IndividualEventClass.EventType.Caste;
          break;
        case "DSCR":
          eventType = IndividualEventClass.EventType.PhysicalDescription;
          break;
        case "IDNO":
          eventType = IndividualEventClass.EventType.IdentityNumber;
          break;
        case "NATI":
          eventType = IndividualEventClass.EventType.Nationality;
          break;
        case "NCHI":
          eventType = IndividualEventClass.EventType.NumberOfChildren;
          break;
        case "NMR":
          eventType = IndividualEventClass.EventType.NumberOfMarriages;
          break;
        case "PROP":
          eventType = IndividualEventClass.EventType.Possesions;
          break;
        case "RELI":
          eventType = IndividualEventClass.EventType.Religion;
          break;
        case "SSN":
          eventType = IndividualEventClass.EventType.SocialSecurityNumber;
          break;
        case "TITL":
          eventType = IndividualEventClass.EventType.NobilityTitle;
          break;
        //case "FACT":
        //  eventType = IndividualEventClass.EventType.Fact;
        //  break;
        //case "MILI": // Anarkiv non-standard tag
        //  eventType = IndividualEventClass.EventType.Military;
        //  break;


        case "CONF":
          eventType = IndividualEventClass.EventType.Confirmation;
          break;
        case "PROB":
          eventType = IndividualEventClass.EventType.Probate;
          break;
        case "WILL":
          eventType = IndividualEventClass.EventType.Will;
          break;
        case "NATU":
          eventType = IndividualEventClass.EventType.Naturalization;
          break;
        case "ORDN":
          eventType = IndividualEventClass.EventType.Ordination;
          break;

        case "EDUC":
          eventType = IndividualEventClass.EventType.Education;
          break;
        case "EVEN":
          eventType = IndividualEventClass.EventType.GeneralEvent;
          break;
        case "GRAD":
          eventType = IndividualEventClass.EventType.Graduation;
          break;
        case "CENS":
          eventType = IndividualEventClass.EventType.Census;
          break;
        case "OCCU":
          eventType = IndividualEventClass.EventType.Occupation;
          break;
        case "RESI":
          eventType = IndividualEventClass.EventType.Residence;
          break;
        case "IMMI":
          eventType = IndividualEventClass.EventType.Immigration;
          break;
        case "EMIG":
          eventType = IndividualEventClass.EventType.Emigration;
          break;

        case "DEAT":
          eventType = IndividualEventClass.EventType.Death;
          break;
        case "CREM":
          eventType = IndividualEventClass.EventType.Cremation;
          break;
        case "BURI":
          eventType = IndividualEventClass.EventType.Burial;
          break;

        // LDS events (uses other but simlar coding..)
        case "SLGC":
          eventType = IndividualEventClass.EventType.SealingChild;
          break;
        case "SLGS":
          eventType = IndividualEventClass.EventType.SealingSpouse;
          break;
        case "ENDL":
          eventType = IndividualEventClass.EventType.Endowment;
          break;

        default:
          HandleUnknownTag(tagStack, lineData);
          //DebugStringAdd("Line: " + lineData.lineNo + " :Unknown tag " + tagStack.GetTagStack() + ":" + lineData.valueString);
          CheckUndecodedChildren(tagStack, lineData.child);
          break;
      }
      return eventType;
    }

    private bool DecodeGedcomMultimediaLink(TagStack tagStack, GedcomLineObject gedcomLineObject, MultimediaLinkClass tempMultimediaLink)
    {
      gedcomLineObject.ObjectDecodeStart("MultimediaLink", gedcomLineObject);

      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "FORM":
              {
                tempMultimediaLink.SetFormat(subLineData.valueString);
                //CheckUndecodedChildren(tagStack, subLineData.child);
                if (subLineData.child != null)
                {
                  for (int j = 0; j < subLineData.child.gedcomLines.Count; j++)
                  {
                    GedcomLineData sub2LineData = subLineData.child.gedcomLines[i];
                    switch (sub2LineData.tagString)
                    {
                      case "MEDI":
                        {
                          tempMultimediaLink.SetMediaType(sub2LineData.valueString);
                          CheckUndecodedChildren(tagStack, sub2LineData.child);
                        }
                        break;

                      default:
                        HandleUnknownTag(tagStack, sub2LineData);
                        break;
                    }
                  }
                }
              }
              break;

            case "TITL":
              {
                String titleString = subLineData.valueString;
                if (subLineData.child != null)
                {
                  DecodeGedcomStringConcatenation(tagStack, subLineData.child, ref titleString);
                }
                tempMultimediaLink.SetTitle(titleString);
                //tempMultimediaLink.SetTitle(subLineData.valueString);
              }
              break;

            case "FILE":
              {
                tempMultimediaLink.SetLink(subLineData.valueString);
                CheckUndecodedChildren(tagStack, subLineData.child);
              }
              break;

            case "NOTE":
              {
                NoteClass tempNote = new NoteClass();// noteString = "";
                if (subLineData.valueString.Length > 0)
                {
                  if (ValidateXrefName(subLineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(subLineData.valueString)));

                    tempMultimediaLink.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(subLineData.valueString, false);
                  }
                }
                if (subLineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, subLineData.child, ref tempNote);
                }
                tempMultimediaLink.AddNote(tempNote);
              }
              break;

            default:
              HandleUnknownTag(tagStack, subLineData);
              //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown tag " + tagStack.GetTagStack() + ":" + subLineData.valueString);
              CheckUndecodedChildren(tagStack, subLineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    private bool DecodeGedcomChange(TagStack tagStack, GedcomLineObject gedcomLineObject, ref IndividualEventClass tempEvent)
    {
      gedcomLineObject.ObjectDecodeStart("Change", gedcomLineObject);

      tempEvent.SetEventType(IndividualEventClass.EventType.RecordUpdate);
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];
        FamilyDateTimeClass tempDate = new FamilyDateTimeClass();

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "DATE":
              {
                if (!DecodeGedcomDate(tagStack, subLineData, ref tempDate))
                {
                  DebugStringAdd("Line: " + subLineData.lineNo + ": Unable to decode " + tagStack.GetTagStack() + " tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                }
                else
                {
                  tempEvent.SetDate(tempDate);
                }
              }
              break;

            case "NOTE":
              {
                /*              String tempNote = subLineData.valueString;

                              if (subLineData.child != null)
                              {
                                DecodeGedcomStringConcatenation(subLineData.child, ref tempNote);
                              }
                              tempEvent.AddNote(tempNote);*/
                NoteClass tempNote = new NoteClass();

                if (subLineData.valueString.Length > 0)
                {
                  if (ValidateXrefName(subLineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(subLineData.valueString)));

                    tempEvent.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(subLineData.valueString, false);
                  }
                }
                if (subLineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, subLineData.child, ref tempNote);
                }
                tempEvent.AddNote(tempNote);
              }
              break;

            default:
              HandleUnknownTag(tagStack, subLineData);
              //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown tag " + tagStack.GetTagStack() + ":" + subLineData.valueString);
              CheckUndecodedChildren(tagStack, subLineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    private bool DecodeGedcomSourceEvent(TagStack tagStack, GedcomLineObject gedcomLineObject, ref SourceEventClass tempSourceEvent)
    {
      gedcomLineObject.ObjectDecodeStart("SourceEvent", gedcomLineObject);
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "DATE":
              {
                //FamilyDateTimeClass tempDate = new FamilyDateTimeClass();

                if (!DecodeGedcomDate(tagStack, subLineData, ref tempSourceEvent.date))
                {
                  DebugStringAdd("Line: " + subLineData.lineNo + ": Unable to decode date tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                }
                else
                {
                  //tempSource.SetDate(tempDate);
                }
              }
              break;

            case "PLAC":
              {
                tempSourceEvent.eventDescription = subLineData.valueString;
              }
              break;


            default:
              HandleUnknownTag(tagStack, subLineData);
              //DebugStringAdd("" + tagStack.GetTagStack() + " sub-tag: " + i + "[" + subLineData.level + "]:[" + subLineData.tagString + "] " + subLineData.valueString);
              CheckUndecodedChildren(tagStack, subLineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    private bool DecodeGedcomSourceCitationData(TagStack tagStack, GedcomLineObject gedcomLineObject, ref SourceXrefClass tempSource)
    {
      gedcomLineObject.ObjectDecodeStart("Source", gedcomLineObject);
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "DATE":
              {
                FamilyDateTimeClass tempDate = new FamilyDateTimeClass();

                if (!DecodeGedcomDate(tagStack, subLineData, ref tempDate))
                {
                  DebugStringAdd("Line: " + subLineData.lineNo + ": Unable to decode " + tagStack.GetTagStack() + " tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                }
                else
                {
                  tempSource.SetDate(tempDate);
                }
              }
              break;

            case "TEXT":
              {
                String textString = subLineData.valueString;
                if (subLineData.child != null)
                {
                  DecodeGedcomStringConcatenation(tagStack, subLineData.child, ref textString);
                }
                tempSource.AddNote(new NoteClass(textString));
                /*if (subLineData.valueString.Length > 0)
                {
                  tempSource.AddNote(subLineData.valueString);
                }*/
              }
              break;


            case "NOTE":
              {
                NoteClass tempNote = new NoteClass();// noteString = "";
                if (subLineData.valueString.Length > 0)
                {
                  if (ValidateXrefName(subLineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(subLineData.valueString)));

                    tempSource.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(subLineData.valueString, false);
                  }
                }
                if (subLineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, subLineData.child, ref tempNote);
                }
                tempSource.AddNote(tempNote);
              }
              break;


            default:
              HandleUnknownTag(tagStack, subLineData);
              //DebugStringAdd("" + tagStack.GetTagStack() + " sub-tag: " + i + "[" + subLineData.level + "]:[" + subLineData.tagString + "] " + subLineData.valueString);
              CheckUndecodedChildren(tagStack, subLineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    private bool DecodeGedcomSourceRecordData(TagStack tagStack, GedcomLineObject gedcomLineObject, ref SourceClass tempSource)
    {
      gedcomLineObject.ObjectDecodeStart("SourceRecordData", gedcomLineObject);

      SourceDataClass sourceData = tempSource.GetSourceData();
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "EVEN":
              {
                //DebugStringAdd("Source.DATA.EVEN sub-tag: " + i + "[" + subLineData.level + "]:[" + subLineData.tagString + "] " + subLineData.valueString);
                //CheckUndecodedChildren("Source.DATA.EVEN", subLineData.child);
                {
                  String tempNote = subLineData.valueString;

                  if (subLineData.child != null)
                  {
                    SourceEventClass sourceEvent = new SourceEventClass();
                    //DecodeGedcomStringConcatenation(subLineData.child, ref tempNote);
                    if (DecodeGedcomSourceEvent(tagStack, subLineData.child, ref sourceEvent))
                    {
                      sourceData.AddEvent(sourceEvent);
                    }
                  }
                  sourceData.AddNote(new NoteClass(tempNote));
                }
              }
              break;

            case "AGNC":
              {
                sourceData.AddAgency(subLineData.valueString);
              }
              break;

            case "NOTE":
              {
                NoteClass tempNote = new NoteClass();// noteString = "";
                if (subLineData.valueString.Length > 0)
                {
                  if (ValidateXrefName(subLineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(subLineData.valueString)));

                    tempSource.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(subLineData.valueString, false);
                  }
                }
                if (subLineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, subLineData.child, ref tempNote);
                }
                sourceData.AddNote(tempNote);

              }
              break;

            case "REFN":
            case "RIN":
              {
                sourceData.SetAutomatedRecordId(subLineData.valueString);
                CheckUndecodedChildren(tagStack, subLineData.child);
              }
              break;

            default:
              HandleUnknownTag(tagStack, subLineData);
              //DebugStringAdd("Line: " + subLineData.lineNo + ": " + tagStack.GetTagStack() + " sub-tag: " + i + "[" + subLineData.level + "]:[" + subLineData.tagString + "] " + subLineData.valueString);
              CheckUndecodedChildren(tagStack, subLineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }



    private bool DecodeGedcomSourceCitationReference(TagStack tagStack, GedcomLineObject gedcomLineObject, ref SourceXrefClass tempSource)
    {
      gedcomLineObject.ObjectDecodeStart("SourceCitationRef", gedcomLineObject);

      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];
        FamilyDateTimeClass tempDate = new FamilyDateTimeClass();

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "DATA":
              {
                if (subLineData.child != null)
                {
                  DecodeGedcomSourceCitationData(tagStack, subLineData.child, ref tempSource);
                }
              }
              break;

            case "OBJE":
              {
                if (subLineData.child != null)
                {
                  MultimediaLinkClass tempMultimediaLink = new MultimediaLinkClass();

                  DecodeGedcomMultimediaLink(tagStack, subLineData.child, tempMultimediaLink);

                  tempSource.AddMultimediaLink(tempMultimediaLink);
                }
              }
              break;

            case "PAGE":
              {
                //DebugStringAdd("Source.OBJE sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                tempSource.SetPage(subLineData.valueString);
              }
              break;

            case "QUAY":
              {
                //DebugStringAdd("Source.OBJE sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                tempSource.SetQualityOfData(subLineData.valueString);
              }
              break;

            case "NOTE":
              {
                //DebugStringAdd("Source.OBJE sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                NoteClass tempNote = new NoteClass();// noteString = "";
                if (subLineData.valueString.Length > 0)
                {
                  if (ValidateXrefName(subLineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(subLineData.valueString)));

                    tempSource.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(subLineData.valueString, false);
                  }
                }
                if (subLineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, subLineData.child, ref tempNote);
                }
                tempSource.AddNote(tempNote);
              }
              break;


            default:
              HandleUnknownTag(tagStack, subLineData);
              //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
              CheckUndecodedChildren(tagStack, subLineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }

      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    private bool DecodeGedcomSourceCitationDescription(TagStack tagStack, GedcomLineObject gedcomLineObject, ref SourceDescriptionClass tempSource)
    {
      gedcomLineObject.ObjectDecodeStart("SourceCitationDescr", gedcomLineObject);

      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData subLineData = gedcomLineObject.gedcomLines[i];
        FamilyDateTimeClass tempDate = new FamilyDateTimeClass();

        if (CheckTag(ref tagStack, subLineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(subLineData.tagString);
          switch (subLineData.tagString)
          {
            case "CONC":
              {
                string str = tempSource.GetDescription();
                str += subLineData.valueString;
                tempSource.SetDescription(str);
              }
              break;

            case "CONT":
              {
                string str = tempSource.GetDescription();
                CheckLineFeed(ref str);
                str += subLineData.valueString;
                tempSource.SetDescription(str);
              }
              break;

            case "NOTE":
              {
                //DebugStringAdd("Source.OBJE sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                NoteClass tempNote = new NoteClass();// noteString = "";
                if (subLineData.valueString.Length > 0)
                {
                  if (ValidateXrefName(subLineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(subLineData.valueString)));

                    tempSource.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(subLineData.valueString, false);
                  }
                }
                if (subLineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, subLineData.child, ref tempNote);
                }
                tempSource.AddNote(tempNote);
              }
              break;

            case "TEXT":
              {
                String textString = subLineData.valueString;
                if (subLineData.child != null)
                {
                  DecodeGedcomStringConcatenation(tagStack, subLineData.child, ref textString);
                }
                tempSource.AddNote(new NoteClass(textString));
                /*if (subLineData.valueString.Length > 0)
                {
                  tempSource.AddNote(subLineData.valueString);
                }*/
              }
              break;

            /*case "OBJE":
              {
                if (subLineData.child != null)
                {
                  MultimediaLinkClass tempMultimediaLink = new MultimediaLinkClass();

                  DecodeGedcomMultimediaLink(tagStack, subLineData.child, tempMultimediaLink);

                  tempSource.AddMultimediaLink(tempMultimediaLink);
                }
              }
              break;

            case "QUAY":
              {
                //DebugStringAdd("Source.OBJE sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                tempSource.SetQualityOfData(subLineData.valueString);
              }
              break;*/

            default:
              HandleUnknownTag(tagStack, subLineData);
              //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
              CheckUndecodedChildren(tagStack, subLineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }

      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      return true;
    }

    public class SourceChoiceClass
    {
      private bool xrefType;
      public SourceChoiceClass(bool xrefType, string str)
      {
        this.xrefType = xrefType;

        if(xrefType)
        {
          sourceXref = new SourceXrefClass(str);
        }
        else
        {
          source = new SourceDescriptionClass(str);
        }
      }
      public SourceDescriptionClass source;
      public SourceXrefClass sourceXref;
      public bool GetXrefType() 
      {
        return xrefType;
      }

    }


    private bool DecodeGedcomSourceCitation(TagStack tagStack, GedcomLineData lineData, ref SourceChoiceClass source)
    {
      if (lineData.child != null)
      {
        if (source.GetXrefType())
        {
          if (!DecodeGedcomSourceCitationReference(tagStack, lineData.child, ref source.sourceXref))
          {
            DebugStringAdd("Line: " + lineData.lineNo + ": source citation decode problem: " + tagStack.GetTagStack() + " : " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
            return false;
          }
        }
        else
        {
          if (!DecodeGedcomSourceCitationDescription(tagStack, lineData.child, ref source.source))
          {
            DebugStringAdd("Line: " + lineData.lineNo + ": source citation decode problem: " + tagStack.GetTagStack() + " : " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
            return false;
          }
        }
      }
      return true;
    }


    private bool DecodeGedcomIndividualRecord(TagStack tagStack, GedcomLineObject gedcomLineObject, string xrefIdString)
    {
      gedcomLineObject.ObjectDecodeStart("Individual", gedcomLineObject);

      IndividualClass tempIndividual = new IndividualClass();

      if (xrefIdString.Length > 0)
      {
        //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
        tempIndividual.SetXrefName(xrefMappers.GetLocalXRef(XrefType.Individual, xrefIdString, true));
      }
      else
      {
        return false;
      }
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        //DebugStringAdd("  individual-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
        if (CheckTag(ref tagStack, lineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(lineData.tagString);
          switch (lineData.tagString)
          {
            /*case "INDI":
              {
                if (i == 0 && (lineData.xrefIdString.Length > 0))
                {
                  //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
                  tempIndividual.SetXrefName(GetLocalXRef(XrefType.Individual, lineData.xrefIdString));//lineData.xrefIdString);
                }
              }
              break;*/

            case "NAME":
              {
                PersonalNameClass tempName = new PersonalNameClass();

                lineData.valueString = ProcessCombiningDiacriticalMarks(lineData.valueString);

                string fullNameString = lineData.valueString.Trim();
                bool formattedNameStringFound = false;

                int lastNameStart = fullNameString.IndexOf('/');

                if ((lastNameStart >= 0) && (lastNameStart < (fullNameString.Length - 1)))
                {
                  string firstName = fullNameString.Substring(0, lastNameStart).Trim();
                  int lastNameEnd = fullNameString.Substring(lastNameStart + 1).IndexOf('/');

                  if(lastNameEnd >= 0)
                  {
                    int realLastNameEnd = lastNameEnd + lastNameStart + 1;
                    string lastName = fullNameString.Substring(lastNameStart + 1, lastNameEnd);

                    if(realLastNameEnd < (fullNameString.Length - 1))
                    {
                      tempName.SetName(PersonalNameClass.PartialNameType.Suffix, fullNameString.Substring(realLastNameEnd + 1));
                    }
                    tempName.SetName(PersonalNameClass.PartialNameType.GivenName, firstName);
                    tempName.SetName(PersonalNameClass.PartialNameType.Surname, lastName);
                    formattedNameStringFound = true;
                  }
                }
                if(!formattedNameStringFound)
                {
                  tempName.SetName(PersonalNameClass.PartialNameType.NameString, lineData.valueString);
                }

                if (lineData.child != null)
                {
                  //DebugStringAdd("name child " + lineData.child.gedcomLines.Count);
                  DecodeGedcomName(tagStack, lineData.child, ref tempName);
                }

                tempIndividual.SetPersonalName(tempName);
              }
              break;

            case "SEX":
              {
                if (lineData.valueString.Length > 0)
                {
                  if (lineData.valueString[0] == 'F')
                  {
                    tempIndividual.SetSex(IndividualClass.IndividualSexType.Female);
                  }
                  else if (lineData.valueString[0] == 'M')
                  {
                    tempIndividual.SetSex(IndividualClass.IndividualSexType.Male);
                  }
                }
              }
              break;

            case "FAMC":
              {
                FamilyXrefClass relationXref = new FamilyXrefClass(xrefMappers.GetLocalXRef(XrefType.Family, GetXrefName(lineData.valueString)));

                if (lineData.child != null)
                {
                  for (int j = 0; j < lineData.child.gedcomLines.Count; j++)
                  {
                    GedcomLineData subLineData = lineData.child.gedcomLines[j];

                    if (CheckTag(ref tagStack, subLineData))
                    {
                      //tagStack.AddTag(firstLineData.tagString);
                      //tagStack.AddTag(subLineData.tagString);
                      switch (subLineData.tagString)
                      {
                        case "PEDI":
                          // Pedigree type not yet decoded...familyTree.gedcomVersion = subLineData.valueString;
                          switch (subLineData.valueString)
                          {
                            case "adopted":
                              relationXref.SetPedigreeType(PedigreeType.Adopted);
                              break;
                            case "birth":
                              relationXref.SetPedigreeType(PedigreeType.Birth);
                              break;
                            case "foster":
                              relationXref.SetPedigreeType(PedigreeType.Foster);
                              break;
                            case "sealing":
                              relationXref.SetPedigreeType(PedigreeType.Sealing);
                              break;
                            default:
                              DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown INDI.FAMC.PEDI type:" + subLineData.valueString);
                              break;
                          }
                          break;

                        case "NOTE":
                          {
                            NoteClass tempNote = new NoteClass();// noteString = "";
                            if (subLineData.valueString.Length > 0)
                            {
                              if (ValidateXrefName(subLineData.valueString))
                              {
                                NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(subLineData.valueString)));

                                relationXref.AddNoteXref(note);
                              }
                              else
                              {
                                tempNote.Concatenate(subLineData.valueString, false);
                              }
                            }
                            if (subLineData.child != null)
                            {
                              DecodeGedcomNoteStructure(tagStack, subLineData.child, ref tempNote);
                            }
                            relationXref.AddNote(tempNote);

                          }
                          break;
                        default:
                          HandleUnknownTag(tagStack, subLineData);
                          //DebugStringAdd("Line: " + lineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " tag type:[" + lineData.level + ":" + lineData.tagString + ":" + lineData.valueString + "]" + i);
                          CheckUndecodedChildren(tagStack, subLineData.child);
                          //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " sub-tag " + subLineData.tagString + ":" + subLineData.valueString);
                          break;
                      }
                      //tagStack.RemoveLast();
                    }
                  }
                  decodedLines += lineData.child.gedcomLines.Count;
                }
                tempIndividual.AddRelation(relationXref, IndividualClass.RelationType.Child);
              }
              break;

            case "FAMS":
              {
                FamilyXrefClass relationXref = new FamilyXrefClass(xrefMappers.GetLocalXRef(XrefType.Family, GetXrefName(lineData.valueString)));
                if (lineData.child != null)
                {
                  for (int j = 0; j < lineData.child.gedcomLines.Count; j++)
                  {
                    GedcomLineData subLineData = lineData.child.gedcomLines[j];

                    if (CheckTag(ref tagStack, subLineData))
                    {
                      //tagStack.AddTag(firstLineData.tagString);
                      //tagStack.AddTag(subLineData.tagString);
                      switch (subLineData.tagString)
                      {
                        case "PEDI":
                          // Pedigree type not yet decoded...familyTree.gedcomVersion = subLineData.valueString;
                          switch (subLineData.valueString)
                          {
                            case "adopted":
                              relationXref.SetPedigreeType(PedigreeType.Adopted);
                              break;
                            case "birth":
                              relationXref.SetPedigreeType(PedigreeType.Birth);
                              break;
                            case "foster":
                              relationXref.SetPedigreeType(PedigreeType.Foster);
                              break;
                            case "sealing":
                              relationXref.SetPedigreeType(PedigreeType.Sealing);
                              break;
                            default:
                              DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " type:" + subLineData.valueString);
                              break;
                          }
                          break;

                        case "NOTE":
                          {
                            NoteClass tempNote = new NoteClass();// noteString = "";
                            if (subLineData.valueString.Length > 0)
                            {
                              if (ValidateXrefName(subLineData.valueString))
                              {
                                NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(subLineData.valueString)));

                                relationXref.AddNoteXref(note);
                              }
                              else
                              {
                                tempNote.Concatenate(subLineData.valueString, false);
                              }
                            }
                            if (subLineData.child != null)
                            {
                              DecodeGedcomNoteStructure(tagStack, subLineData.child, ref tempNote);
                            }
                            relationXref.AddNote(tempNote);

                          }
                          break;
                        default:
                          HandleUnknownTag(tagStack, subLineData);
                          //DebugStringAdd("Line: " + lineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " tag type:[" + lineData.level + ":" + lineData.tagString + ":" + lineData.valueString + "]" + i);
                          CheckUndecodedChildren(tagStack, subLineData.child);
                          //DebugStringAdd("Line: " + subLineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " sub-tag " + subLineData.tagString + ":" + subLineData.valueString);
                          break;
                      }
                      //tagStack.RemoveLast();
                    }
                  }
                  decodedLines += lineData.child.gedcomLines.Count;
                }

                tempIndividual.AddRelation(relationXref, IndividualClass.RelationType.Spouse);
              }
              break;

            //case "MISC": // Anarkiv 7.0 non-standard...
            //case "DIV":  // Anarkiv 7.0 non-standard...
            case "NOTE":
              {
                NoteClass tempNote = new NoteClass();// noteString = "";
                if (lineData.valueString.Length > 0)
                {
                  if (ValidateXrefName(lineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(lineData.valueString)));

                    tempIndividual.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(lineData.valueString, false);
                  }
                }
                if (lineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, lineData.child, ref tempNote);
                }
                tempIndividual.AddNote(tempNote);

              }
              break;

            case "SUBM":
              {
                SubmitterXrefClass submitterXref = new SubmitterXrefClass(xrefMappers.GetLocalXRef(XrefType.Submitter, GetXrefName(lineData.valueString)));

                tempIndividual.AddSubmitter(submitterXref);
              }
              break;

            case "PHON":
              {
                tempIndividual.AddAddress(AddressPartClass.AddressPartType.PhoneNumber, lineData.valueString);
              }
              break;

            /*case "_EMAIL": // GENI.COM
              if (exportSoftware == ExportSoftwareType.GeniDotCom)
              {
                tempIndividual.AddAddress(AddressPartClass.AddressPartType.EmailAddress, lineData.valueString);
              }
              else
              {
                HandleUnknownTag(tagStack, lineData);
                //DebugStringAdd("Line: " + lineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " sub-tag(geni?): " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;*/

            case "BIRT":

            case "BAPL":
            case "BAPM":
            case "CHR":
            case "CHRA":

            case "ADOP":
            case "BARM":
            case "BASM":
            case "BLES":
            case "FCOM":
            case "RETI":

            case "CAST":
            case "DSCR":
            case "IDNO":
            case "NATI":
            case "NCHI":
            case "NMR":
            case "PROP":
            case "RELI":
            case "SSN":
            case "TITL":
            case "FACT": // gedcom 5.5.1
            //case "MILI": // ANarkiv 7.0

            case "CONF":
            case "PROB":
            case "WILL":
            case "NATU":
            case "ORDN":
            case "CENS":
            case "EDUC":
            case "EVEN":
            case "GRAD":
            case "OCCU":
            case "RESI":
            case "EMIG":
            case "IMMI":

            case "DEAT":
            case "CREM":
            case "BURI":

            // LDS events...
            case "ENDL":
            case "SLGC":
            case "SLGS":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, DecodeGedcomIndividualEventType(tagStack, lineData), ref tempEvent);

                  tempIndividual.AddEvent(tempEvent); //eventType, tempDate);
                }
                else if (lineData.valueString.Length > 0)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  tempEvent.SetEventType(DecodeGedcomIndividualEventType(tagStack, lineData));
                  tempEvent.AddNote(new NoteClass(lineData.valueString));

                  tempIndividual.AddEvent(tempEvent); //eventType, tempDate);

                  if (lineData.tagString == "DEAT")
                  {
                    tempIndividual.SetIsAlive(false);
                  }
                }
              }
              break;


            case "OBJE":
              {
                if (lineData.child != null)
                {
                  MultimediaLinkClass tempMultimediaLink = new MultimediaLinkClass();

                  DecodeGedcomMultimediaLink(tagStack, lineData.child, tempMultimediaLink);

                  tempIndividual.AddMultimediaLink(tempMultimediaLink);
                }
              }
              break;

            case "CHAN":
              {
                //DebugStringAdd("CHANGE tag: " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomChange(tagStack, lineData.child, ref tempEvent);
                  tempIndividual.AddEvent(tempEvent);
                }
                else
                {
                  DebugStringAdd("Line: " + lineData.lineNo + ": " + tagStack.GetTagStack() + " no children: " + lineData.valueString);
                }
              }
              break;

            case "SOUR":
              {
                SourceChoiceClass tempSource;
                if (ValidateXrefName(lineData.valueString))
                {
                  tempSource = new SourceChoiceClass(true, xrefMappers.GetLocalXRef(XrefType.Source, GetXrefName(lineData.valueString)));
                }
                else
                {
                  tempSource = new SourceChoiceClass(false, lineData.valueString);
                }
                //DebugStringAdd("NOTE:SOUR sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                DecodeGedcomSourceCitation(tagStack, lineData, ref tempSource);
                //tempNote.AddSource(tempSource);
                if (tempSource.GetXrefType())
                {
                  tempIndividual.AddSourceXref(tempSource.sourceXref);
                }
                else
                {
                  tempIndividual.AddSource(tempSource.source);
                }
                //SourceChoiceClass source = new SourceClass(lineData.valueString);
                //DebugStringAdd("SOUR tag: " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                //DecodeGedcomSourceCitation(tagStack, lineData, ref source);
                //tempIndividual.AddSource(source);
              }
              break;

            //case "COMM": // Anarkiv 7.0 non-standard!
            //case "CONT": // Anarkiv 7.0 non-standard!
            //case "MILI": // Anarkiv 7.0 non-standard!
            //case "FACT": // Anarkiv 7.0 non-standard!
            /*            {
                          //DebugStringAdd("decoded date: " + tempDate);
                        }
                        break;*/
            case "RIN":
              {
                tempIndividual.SetSpecialRecordId(IndividualClass.IndividualSpecialRecordIdType.AutomatedRecordId, lineData.valueString);
              }
              break;

            case "AFN":
              {
                tempIndividual.SetSpecialRecordId(IndividualClass.IndividualSpecialRecordIdType.AncestralFileNumber, lineData.valueString);
              }
              break;

            case "REFN":
              {
                tempIndividual.SetSpecialRecordId(IndividualClass.IndividualSpecialRecordIdType.UserReferenceNumber, lineData.valueString);
              }
              break;

            case "RFN":
              {
                //tempIndividual.SetSpecialRecordId(IndividualClass.IndividualSpecialRecordIdType.PermanentRecordFileNumber, lineData.valueString);
                tempIndividual.AddPermanentRFN(lineData.valueString);
              }
              break;

            default:
              HandleUnknownTag(tagStack, lineData);
              //DebugStringAdd("Line: " + lineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " tag type:[" + lineData.level + ":" + lineData.tagString + ":" + lineData.valueString + "]" + i);
              CheckUndecodedChildren(tagStack, lineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }

      decodedLines += gedcomLineObject.gedcomLines.Count;

      /*      if (!NotesDecoded)
            {

              for (int i = 0; i < 10000000; i++)
              {
                IndividualClass t2Indi = new IndividualClass();

                if (i % 100000 == 0)
                {
                  DebugStringAdd("Adding many indies: " + i);
                }
                //familyTree.familyList.Add(tempFamily);
                //familyTree.familyList.Add(t2Family);
                familyTree.individualList.Add(tempIndividual);
                familyTree.individualList.Add(t2Indi);
              }
              NotesDecoded = true;
            }*/
/*      if ("I363591374620005883" == tempIndividual.GetXrefName())
      {
        DebugStringAdd("indi:" + tempIndividual.GetHashCode() + ":" + tempIndividual.GetXrefName());
        tempIndividual.Print();
      }

      if (325038587 == tempIndividual.GetHashCode())
      {
        DebugStringAdd("indi:" + tempIndividual.GetHashCode() + ":" + tempIndividual.GetXrefName());
        tempIndividual.Print();
      }*/

      //familyTree.individualList.Add(tempIndividual.GetXrefName(), tempIndividual);
      if ((tempIndividual.GetXrefName() != null) && (tempIndividual.GetXrefName().Length > 0))
      {
        familyTree.AddIndividual(tempIndividual);
      }
      else
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[0];
        DebugStringAdd("Line: " + lineData.lineNo + ": Error in " + tagStack.GetTagStack() + " tag type:[" + lineData.level + ":" + lineData.tagString + ":" + lineData.valueString + "] no tag");
      }
      return true;
    }

    private bool ValidateXrefName(String xrefName)
    {
      if (xrefName.Length < 3)
      {
        return false;
      }
      if (xrefName[0] != '@')
      {
        return false;
      }
      if (xrefName[xrefName.Length - 1] != '@')
      {
        return false;
      }
      for (int i = 1; i < xrefName.Length - 1; i++)
      {
        if (!parser.IsValidXrefChar(xrefName[i]))
        {
          return false;
        }
      }

      return true;

    }
    private String GetXrefName(String xrefName)
    {
      String tempXref = "";

      for (int i = 1; i < xrefName.Length - 1; i++)
      {
        tempXref += xrefName[i];
      }

      return tempXref;
    }

    private bool DecodeGedcomDate(TagStack tagStack, GedcomLineData lineData, ref FamilyDateTimeClass date)
    {
      if (lineData.valueString.Length > 0)
      {
        String dateString = lineData.valueString;
        int year = 0, month = 0, day = 0;
        int strPos = 0;
        String monthStr = "";
        FamilyDateTimeClass newDate = null;
        String token = "";
        bool approximate = false;
        FamilyDateTimeClass.FamilyDateType timeType = FamilyDateTimeClass.FamilyDateType.Unknown;


        // Day
        while ((strPos < dateString.Length) && parser.IsValidAlpha(dateString[strPos]))
        {
          token += dateString[strPos];
          strPos++;
        }
        if (token == "ABT")
        {
          approximate = true;
        }

        while ((strPos < dateString.Length) && parser.IsBlankChar(dateString[strPos]))
        {
          strPos++;
        }
        // Day
        while ((strPos < dateString.Length) && parser.IsValidDigit(dateString[strPos]))
        {
          day = day * 10 + (dateString[strPos] - '0');
          strPos++;
        }
        while ((strPos < dateString.Length) && parser.IsBlankChar(dateString[strPos]))
        {
          strPos++;
        }

        if ((strPos < dateString.Length) && (dateString[strPos] == '/'))
        {
          strPos++;
          while ((strPos < dateString.Length) && parser.IsValidDigit(dateString[strPos]))
          {
            month = month * 10 + (dateString[strPos] - '0');
            strPos++;
          }
        }
        else
        {
          // Month
          while ((strPos < dateString.Length) && parser.IsValidAlpha(dateString[strPos]))
          {
            monthStr += dateString[strPos];
            strPos++;
          }
          if ((strPos < dateString.Length) && (dateString[strPos] == '.'))
          {
            strPos++;
          }
        }
        if ((strPos < dateString.Length) && (dateString[strPos] == '?'))
        {
          strPos++;
          approximate = true;
        }
        while ((strPos < dateString.Length) && parser.IsBlankChar(dateString[strPos]))
        {
          strPos++;
        }
        if ((strPos < dateString.Length) && (dateString[strPos] == '?'))
        {
          strPos++;
          approximate = true;
        }
        while ((strPos < dateString.Length) && parser.IsBlankChar(dateString[strPos]))
        {
          strPos++;
        }

        // Year
        while ((strPos < dateString.Length) && parser.IsValidDigit(dateString[strPos]))
        {
          year = year * 10 + (dateString[strPos] - '0');
          strPos++;
        }
        if ((day > 100) && (year == 0))
        {
          year = day;
          day = 0;
        }

        if (monthStr.Length > 2)
        {
          switch (monthStr.ToUpper())
          {
            case "JAN":
              month = 1;
              break;
            case "FEB":
              month = 2;
              break;
            case "MAR":
              month = 3;
              break;
            case "APR":
              month = 4;
              break;
            case "MAY":
              month = 5;
              break;
            case "JUN":
              month = 6;
              break;
            case "JUL":
              month = 7;
              break;
            case "AUG":
              month = 8;
              break;
            case "SEP":
              month = 9;
              break;
            case "OCT":
              month = 10;
              break;
            case "NOV":
              month = 11;
              break;
            case "DEC":
              month = 12;
              break;
            default:
              //DebugStringAdd("Warning: Unknown month[" + monthStr + "][" + dateString + "]");
              month = 0;
              break;
          }
        }

        if (year != 0)
        {
          timeType = FamilyDateTimeClass.FamilyDateType.Year;
          if ((month > 0) && (month <= 12))
          {
            timeType = FamilyDateTimeClass.FamilyDateType.YearMonth;
            if ((day > 0) && (day <= 31))
            {
              timeType = FamilyDateTimeClass.FamilyDateType.YearMonthDay;
            }

          }

        }

        //DebugStringAdd("date: " + dateString);
        //DebugStringAdd("DecodeDate: y:" + year + " ms:" + monthStr + " m:" + month + " d:" + day);
        if (year < 100)
        {
          bool validDateChar = false;
          foreach(char ch in dateString)
          {
            if((ch >= '0') && (ch <= '9'))
            {
              validDateChar = true;
            }
          }
          if (validDateChar)
          {
            newDate = new FamilyDateTimeClass(dateString);
            timeType = FamilyDateTimeClass.FamilyDateType.DateString;
          }
          else
          {
            return false;
          }
        }
        else
        {
          newDate = new FamilyDateTimeClass(year, month, day);
        }
        newDate.SetDateType(timeType);
        newDate.SetApproximate(approximate);

        if (lineData.child != null)
        {
          for (int j = 0; j < lineData.child.gedcomLines.Count; j++)
          {
            GedcomLineData subLineData = lineData.child.gedcomLines[j];

            if (CheckTag(ref tagStack, subLineData))
            {
              //tagStack.AddTag(firstLineData.tagString);
              //tagStack.AddTag(subLineData.tagString);
              switch (subLineData.tagString)
              {
                case "TIME":
                  Int32 h = -1, m = -1, s = -1;
                  if (subLineData.valueString.Length >= 2)
                  {
                    h = Convert.ToInt32(subLineData.valueString.Substring(0, 2));

                    if ((subLineData.valueString.Length >= 5) && (subLineData.valueString[2] == ':'))
                    {
                      m = Convert.ToInt32(subLineData.valueString.Substring(3, 2));

                      if ((subLineData.valueString.Length >= 8) && (subLineData.valueString[5] == ':'))
                      {
                        s = Convert.ToInt32(subLineData.valueString.Substring(6, 2));
                      }
                    }
                  }
                  newDate.SetTime(h, m, s);
                  break;
                default:
                  HandleUnknownTag(tagStack, subLineData);
                  //DebugStringAdd("Line: " + subLineData.lineNo + ": Invalid GEDCOM tag " + tagStack.GetTagStack() + ":" + subLineData.valueString);
                  CheckUndecodedChildren(tagStack, subLineData.child);
                  break;
              }
              //tagStack.RemoveLast();
            }
            //CheckUndecodedChildren("HEAD.GEDC", subLineData.child);
            //DebugStringAdd("HEAD.GEDC sub-tag " + subLineData.tagString + ":" + subLineData.valueString);
          }
          decodedLines += lineData.child.gedcomLines.Count;
        }


        date = newDate;
        return true;
      }
      else
      {
        DebugStringAdd("Error: " + tagStack.GetTagStack() + " Empty date in tag:" + lineData.tagString + " value:[" + lineData.valueString + "] at line:" + lineData.lineNo);
      }
      return false;
    }

    private double TryDecodeDouble(string numberStr)
    {
      double result = Double.NaN;
      try
      {
        result = Convert.ToDouble(numberStr);
      }
      catch
      {
        trace.TraceInformation("Failed decoding double " + numberStr);
      }
      return result;
    }

    private double TryDecodePosString(string posStr)
    {
      if (posStr.Length > 3)
      {
        string textStr = posStr.Substring(0, 1);

        IList<string> strings = new List<string> { "E", "W", "N", "S" };

        if (strings.Contains(textStr, StringComparer.InvariantCultureIgnoreCase))
        {
          return TryDecodeDouble(posStr.Substring(1));
        }
      }
      return double.NaN;
    }

    private bool DecodeGedcomPlaceStructure(TagStack tagStack, GedcomLineData lineData, ref PlaceStructureClass place)
    {
      if (lineData.valueString.Length > 0)
      {
        place.SetPlace(lineData.valueString);
      }
      if (lineData.child != null)
      {
        for (int j = 0; j < lineData.child.gedcomLines.Count; j++)
        {
          GedcomLineData line = lineData.child.gedcomLines[j];

          if (CheckTag(ref tagStack, line))
          {
            //tagStack.AddTag(firstLineData.tagString);
            //tagStack.AddTag(line.tagString);
            switch (line.tagString)
            {
              case "FORM":
                place.SetPlaceHierarchy(line.valueString);
                break;

              case "SOUR":
                {
                  SourceChoiceClass tempSource;
                  if (ValidateXrefName(line.valueString))
                  {
                    tempSource = new SourceChoiceClass(true, xrefMappers.GetLocalXRef(XrefType.Source, GetXrefName(line.valueString)));
                  }
                  else
                  {
                    tempSource = new SourceChoiceClass(false, line.valueString);
                  }
                  //DebugStringAdd("NOTE:SOUR sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                  DecodeGedcomSourceCitation(tagStack, line, ref tempSource);
                  //tempNote.AddSource(tempSource);
                  if (tempSource.GetXrefType())
                  {
                    place.AddSourceXref(tempSource.sourceXref);
                  }
                  else
                  {
                    place.AddSource(tempSource.source);
                  }

                }
                //SourceChoiceClass source = new SourceClass(line.valueString);

                //DecodeGedcomSourceCitation(tagStack, lineData, ref source);
                //place.AddSource(source);
                break;

              case "NOTE":
                NoteClass note = new NoteClass();

                if (line.valueString.Length > 0)
                {
                  if (ValidateXrefName(line.valueString))
                  {
                    NoteXrefClass noteXref = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(line.valueString)));

                    place.AddNoteXref(noteXref);
                  }
                  else
                  {
                    note.Concatenate(line.valueString, false);
                  }
                }
                if (line.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, line.child, ref note);
                }
                place.AddNote(note);
                break;

              case "MAP":
                if (line.child != null)
                {
                  MapPosition mapPos = new MapPosition(Double.NaN, Double.NaN);

                  foreach (GedcomLineData mapLine in line.child.gedcomLines)
                  {
                    if (mapLine.tagString == "LATI")
                    {
                      mapPos.latitude = TryDecodeDouble(mapLine.valueString);
                    }
                    if (mapLine.tagString == "LONG")
                    {
                      mapPos.longitude = TryDecodeDouble(mapLine.valueString);
                    }
                  }
                  if (!double.IsNaN(mapPos.latitude) && !double.IsNaN(mapPos.longitude))
                  {
                    place.SetMapPosition(mapPos);
                  }
                  else
                  {
                    DebugStringAdd("Error: " + tagStack.GetTagStack() + " Bad map location format :" + lineData.tagString + " value:[" + lineData.valueString + "] at line:" + lineData.lineNo);
                  }
                }
                break;

              default:
                //DebugStringAdd("Line: " + line.lineNo + ": Unknown " + tagStack.GetTagStack() + " tag type: " + line.tagString + " " + line.valueString);
                HandleUnknownTag(tagStack, lineData);
                CheckUndecodedChildren(tagStack, lineData.child);
                break;
            }
            //tagStack.RemoveLast();
          }
        }

      }
      return true;
    }

    private bool DecodeGedcomFamilyRecord(TagStack tagStack, GedcomLineObject gedcomLineObject, string xrefIdString)
    {
      gedcomLineObject.ObjectDecodeStart("Family", gedcomLineObject);

      FamilyClass tempFamily = new FamilyClass();
      if (xrefIdString.Length > 0)
      {
        //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
        tempFamily.SetXrefName(xrefMappers.GetLocalXRef(XrefType.Family, xrefIdString, true));
      }
      else
      {
        return false;
      }
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        //DebugStringAdd("  fam-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
        if (CheckTag(ref tagStack, lineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(lineData.tagString);
          switch (lineData.tagString)
          {
            /*case "FAM":
              {
                if (i == 0 && (lineData.xrefIdString.Length > 0))
                {
                  //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
                  tempFamily.SetXrefName(GetLocalXRef(XrefType.Family, lineData.xrefIdString));
                }
              }
              break;*/


            case "HUSB":
            case "WIFE":
              {
                if (ValidateXrefName(lineData.valueString))
                {
                  IndividualXrefClass father = new IndividualXrefClass(xrefMappers.GetLocalXRef(XrefType.Individual, GetXrefName(lineData.valueString)));

                  tempFamily.AddRelation(father, FamilyClass.RelationType.Parent);
                }
              }
              break;

            /*          case "WIFE":
                        {
                          IndividualXrefClass mother = new IndividualXrefClass(GetXrefName(lineData.valueString));

                          tempFamily.AddRelation(mother, FamilyClass.RelationType.Mother);
                        }
                        break;*/

            case "CHIL":
              {
                IndividualXrefClass child = new IndividualXrefClass(xrefMappers.GetLocalXRef(XrefType.Individual, GetXrefName(lineData.valueString)));

                tempFamily.AddRelation(child, FamilyClass.RelationType.Child);
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "SUBM":
              {
                SubmitterXrefClass submitter = new SubmitterXrefClass(xrefMappers.GetLocalXRef(XrefType.Submitter, GetXrefName(lineData.valueString)));

                tempFamily.AddSubmitter(submitter);
              }
              break;


            // Here we have a problem! <<FAMILY_EVENT_STRUCTURE>> is not decoded!
            case "MARR":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamMarriage, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;

            case "ENGA":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamEngagement, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;

            case "MARB":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamMarriageBann, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;

            case "MARC":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamMarriageContract, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;

            case "MARL":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamMarriageLicense, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;

            case "MARS":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamMarriageSettlement, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;

            case "EVEN":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamGeneralEvent, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;

            case "CENS":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamCensus, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;

            case "DIV":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamDivorce, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;

            case "DIVF":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamDivorceFiled, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;

            case "ANUL":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomEventDetail(tagStack, lineData.child, IndividualEventClass.EventType.FamAnnulment, ref tempEvent);

                  tempFamily.AddEvent(tempEvent);
                }
              }
              break;


            case "CHAN":
              {
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomChange(tagStack, lineData.child, ref tempEvent);
                  tempFamily.AddEvent(tempEvent);
                }
                else
                {
                  DebugStringAdd("Line: " + lineData.lineNo + ": " + tagStack.GetTagStack() + " no children: " + lineData.valueString);
                }
              }
              break;

            case "NCHI":
              {
                int numberOfChildren = 0;

                for (int j = 0; j < lineData.valueString.Length; j++)
                {
                  numberOfChildren = numberOfChildren * 10 + (lineData.valueString[j] - '0');
                }

                tempFamily.SetNumberOfChildren(numberOfChildren);
              }
              break;

            case "SOUR":
              {
                SourceChoiceClass tempSource;
                if (ValidateXrefName(lineData.valueString))
                {
                  tempSource = new SourceChoiceClass(true, xrefMappers.GetLocalXRef(XrefType.Source, GetXrefName(lineData.valueString)));
                }
                else
                {
                  tempSource = new SourceChoiceClass(false, lineData.valueString);
                }
                //DebugStringAdd("NOTE:SOUR sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                DecodeGedcomSourceCitation(tagStack, lineData, ref tempSource);
                //tempNote.AddSource(tempSource);
                if (tempSource.GetXrefType())
                {
                  tempFamily.AddSourceXref(tempSource.sourceXref);
                }
                else
                {
                  tempFamily.AddSource(tempSource.source);
                }
                //SourceChoiceClass tempSource = new SourceClass(lineData.valueString);
                //DebugStringAdd("Unknown NOTE sub-tag: " + subLineData.level + ":" + subLineData.tagString + " " + subLineData.valueString);
                //DecodeGedcomSourceCitation(tagStack, lineData, ref tempSource);
                //tempFamily.AddSource(tempSource);
              }
              break;

            case "NOTE":
              {
                NoteClass tempNote = new NoteClass();

                if (lineData.valueString.Length > 0)
                {
                  if (ValidateXrefName(lineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(lineData.valueString)));

                    tempFamily.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(lineData.valueString, false);
                  }
                }
                if (lineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, lineData.child, ref tempNote);
                }
                tempFamily.AddNote(tempNote);
              }
              break;

            case "OBJE":
              {
                if (lineData.child != null)
                {
                  MultimediaLinkClass tempMultimediaLink = new MultimediaLinkClass();

                  DecodeGedcomMultimediaLink(tagStack, lineData.child, tempMultimediaLink);

                  tempFamily.AddMultimediaLink(tempMultimediaLink);
                }
              }
              break;

            case "RIN":
              {
                tempFamily.SetSpecialRecordId(FamilyClass.FamilySpecialRecordIdType.AutomatedRecordId, lineData.valueString);
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "REFN":
              {
                tempFamily.SetSpecialRecordId(FamilyClass.FamilySpecialRecordIdType.UserReferenceNumber, lineData.valueString);
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            default:
              HandleUnknownTag(tagStack, lineData);
              //DebugStringAdd("Line: " + lineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " tag type: " + lineData.tagString + " " + lineData.valueString);
              CheckUndecodedChildren(tagStack, lineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;

      /*if (!NotesDecoded)
      {

        for (int i = 0; i < 10000000; i++)
        {
          FamilyClass t2Family = new FamilyClass();

          if (i % 100000 == 0)
          {
            DebugStringAdd("Adding many families: " + i);
          }
          familyTree.familyList.Add(tempFamily);
          familyTree.familyList.Add(t2Family);
        }
        NotesDecoded = true;
      }*/
      //familyTree.familyList.Add(tempFamily.GetXrefName(), tempFamily);
      familyTree.AddFamily(tempFamily);
      return true;
    }
    private bool DecodeGedcomMultimediaRecord(TagStack tagStack, GedcomLineObject gedcomLineObject, string xrefIdString)
    {
      gedcomLineObject.ObjectDecodeStart("MultimediaRecord", gedcomLineObject);

      MultimediaObjectClass tempMultimediaObject = new MultimediaObjectClass();

      if (xrefIdString.Length > 0)
      {
        //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
        tempMultimediaObject.SetXrefName(xrefMappers.GetLocalXRef(XrefType.Multimedia, xrefIdString, true));
      }
      else
      {
        return false;
      }

      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        /*if (i == 0)
        {
          if (lineData.xrefIdString.Length > 0)
          {
            //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
            tempMultimediaObject.SetXrefName(GetLocalXRef(XrefType.Multimedia, lineData.xrefIdString));
          }
          else
          {
            DebugStringAdd("Error: " + tagStack.GetTagStack() + " No Note xref !: " + lineData.tagString + " " + lineData.valueString);
          }
        }
        if (lineData.level == 1)*/
        {
          //DebugStringAdd("  submitter-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
          if (CheckTag(ref tagStack, lineData))
          {
            //tagStack.AddTag(firstLineData.tagString);
            //tagStack.AddTag(lineData.tagString);
            switch (lineData.tagString)
            {
              case "FORM":
                {
                  tempMultimediaObject.SetFormat(lineData.valueString);
                }
                break;

              case "TITL":
                {
                  tempMultimediaObject.SetTitle(lineData.valueString);
                }
                break;

              default:
                //if (lineData.valueString.Length > 0)
                {
                  HandleUnknownTag(tagStack, lineData);
                  //DebugStringAdd("Line: " + lineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " tag type: " + lineData.tagString + " " + lineData.valueString);
                  CheckUndecodedChildren(tagStack, lineData.child);
                }
                break;
            }
            //tagStack.RemoveLast();
          }
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;


      //familyTree.multimediaObjectList.Add(tempMultimediaObject);
      familyTree.AddMultimediaObject(tempMultimediaObject);
      return true;
    }

    private bool DecodeGedcomNoteRecord(TagStack tagStack, GedcomLineObject gedcomLineObject, string xrefIdString)
    {
      gedcomLineObject.ObjectDecodeStart("Note", gedcomLineObject);

      NoteClass tempNote = new NoteClass();

      if (xrefIdString.Length > 0)
      {
        //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
        tempNote.SetXrefName(xrefMappers.GetLocalXRef(XrefType.Note, xrefIdString, true));
      }
      else
      {
        return false;
      }
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];
        lineData.valueString = ProcessCombiningDiacriticalMarks(lineData.valueString);

        //DebugStringAdd("  submitter-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
        if (CheckTag(ref tagStack, lineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(lineData.tagString);
          switch (lineData.tagString)
          {
            case "NOTE":
              {
                if (lineData.xrefIdString.Length > 0)
                {
                  //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
                  tempNote.SetXrefName(xrefMappers.GetLocalXRef(XrefType.Note, lineData.xrefIdString, true));
                  DebugStringAdd("Error: " + tagStack.GetTagStack() + " bad Note xref !: " + lineData.tagString + " " + lineData.valueString);
                }
                else
                {
                  DebugStringAdd("Error: " + tagStack.GetTagStack() + " No Note xref !: " + lineData.tagString + " " + lineData.valueString);
                }
                if (lineData.valueString.Length > 0)
                {
                  tempNote.Concatenate(lineData.valueString, false);
                }
              }
              break;

            case "CONC":
              {
                tempNote.Concatenate(lineData.valueString, false);
              }
              break;

            case "CONT":
              {
                tempNote.Concatenate(lineData.valueString, true);
              }
              break;

            case "CHAN":
              {
                //DebugStringAdd("CHANGE tag: " + lineData.level + ":" + lineData.tagString + " " + lineData.valueString);
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomChange(tagStack, lineData.child, ref tempEvent);
                  tempNote.SetUpdateEvent(tempEvent);
                }
              }
              break;

            default:
              HandleUnknownTag(tagStack, lineData);
              //DebugStringAdd("Line: " + lineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " tag type: " + lineData.tagString + " " + lineData.valueString);
              CheckUndecodedChildren(tagStack, lineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;

/*      if (!NotesDecoded)
      {

        for (int i = 0; i < 100000; i++)
        {
          if (i % 1000 == 0)
          {
            DebugStringAdd("Adding many notes: " + i);
          }
          familyTree.noteList.Add(tempNote.GetXrefName(), tempNote);
        }
        NotesDecoded = true;
      }*/
      //familyTree.noteList.Add(tempNote.GetXrefName(), tempNote);
      familyTree.AddNote(tempNote);
      return true;
    }

    private bool DecodeGedcomRepositoryRecord(TagStack tagStack, GedcomLineObject gedcomLineObject, string xrefIdString)
    {
      gedcomLineObject.ObjectDecodeStart("RepositoryRecord", gedcomLineObject);

      RepositoryClass tempRepository = new RepositoryClass();

      if (xrefIdString.Length > 0)
      {
        //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
        tempRepository.SetXrefName(xrefMappers.GetLocalXRef(XrefType.Repository, xrefIdString, true));
      }
      else
      {
        return false;
      }
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        //DebugStringAdd("  submitter-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
        if (CheckTag(ref tagStack, lineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(lineData.tagString);
          switch (lineData.tagString)
          {
            /*case "REPO":
              if (lineData.xrefIdString.Length > 0)
              {
                //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
                tempRepository.SetXrefName(GetLocalXRef(XrefType.Repository, lineData.xrefIdString));
              }
              break;*/

            case "NAME":
              //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
              tempRepository.SetName(lineData.valueString);
              break;

            case "NOTE":
              {
                NoteClass tempNote = new NoteClass();

                if (lineData.valueString.Length > 0)
                {
                  if (ValidateXrefName(lineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(lineData.valueString)));

                    tempRepository.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(lineData.valueString, false);
                  }
                }
                if (lineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, lineData.child, ref tempNote);
                }
                //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
                tempRepository.AddNote(new NoteClass(lineData.valueString));
              }
              break;

            case "ADDR":
              {
                //DebugStringAdd("Subm.ADDR tag type: " + lineData.tagString + " " + lineData.valueString);
                AddressClass tempAddress = new AddressClass();
                string streetAddressString = "";
                if (lineData.valueString.Length > 0)
                {
                  //tempAddress.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, subLineData.valueString);
                  streetAddressString += lineData.valueString;
                  //DebugStringAdd("Warning! addr contains data" + subLineData.tagString + ":" + subLineData.valueString);
                }
                if (lineData.child != null)
                {
                  if (DecodeGedcomAddress(tagStack, lineData.child, ref tempAddress, ref streetAddressString))
                  {
                    //tempEvent.AddAddress(tempAddress);
                  }
                }
                if (streetAddressString.Length > 0)
                {
                  tempAddress.AddAddressPart(AddressPartClass.AddressPartType.StreetAddress, streetAddressString);
                }
                tempRepository.AddAddress(tempAddress);
              }
              break;

            default:
              HandleUnknownTag(tagStack, lineData);
              //DebugStringAdd("Line: " + lineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " tag type: " + lineData.tagString + " " + lineData.valueString);
              CheckUndecodedChildren(tagStack, lineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;
      //familyTree.repositoryList.Add(tempRepository);
      familyTree.AddRepository(tempRepository);
      return true;
    }

    private bool DecodeGedcomSourceRecord(TagStack tagStack, GedcomLineObject gedcomLineObject, string xrefIdString)
    {
      gedcomLineObject.ObjectDecodeStart("SourceRecord", gedcomLineObject);

      SourceClass tempSource = new SourceClass();

      if (xrefIdString.Length > 0)
      {
        //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
        tempSource.SetXrefName(xrefMappers.GetLocalXRef(XrefType.Source, xrefIdString, true));
      }
      else
      {
        return false;
      }
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        //DebugStringAdd("  submitter-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
        if (CheckTag(ref tagStack, lineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(lineData.tagString);
          switch (lineData.tagString)
          {
            /*case "SOUR":
              {
                if (i == 0 && (lineData.xrefIdString.Length > 0))
                {
                  //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
                  tempSource.SetXrefName(GetLocalXRef(XrefType.Source, lineData.xrefIdString, true));
                }
              }
              break;*/

            case "DATA":
              {
                if (lineData.child != null)
                {
                  DecodeGedcomSourceRecordData(tagStack, lineData.child, ref tempSource);
                }
              }
              break;

            case "AUTH":
              {
                String authorString = lineData.valueString;
                if (lineData.child != null)
                {
                  DecodeGedcomStringConcatenation(tagStack, lineData.child, ref authorString);
                }
                tempSource.AddAuthor(authorString);
              }
              break;

            case "TITL":
              {
                String titleString = lineData.valueString;
                if (lineData.child != null)
                {
                  DecodeGedcomStringConcatenation(tagStack, lineData.child, ref titleString);
                }
                tempSource.SetTitle(titleString);
              }
              break;

            case "ABBR":
              {
                tempSource.SetAbbreviation(lineData.valueString);
              }
              break;

            case "PUBL":
              {
                String publicationString = lineData.valueString;
                if (lineData.child != null)
                {
                  DecodeGedcomStringConcatenation(tagStack, lineData.child, ref publicationString);
                }
                tempSource.SetPublicationFacts(publicationString);
              }
              break;

            case "TEXT":
              {
                String textString = lineData.valueString;
                if (lineData.child != null)
                {
                  DecodeGedcomStringConcatenation(tagStack, lineData.child, ref textString);
                }
                tempSource.SetText(textString);
              }
              break;

            case "REPO":
              {
                if((lineData.valueString.Length > 0) && ValidateXrefName(lineData.valueString))
                {
                  RepositoryXrefClass repositoryXref = new RepositoryXrefClass(xrefMappers.GetLocalXRef(XrefType.Repository, GetXrefName(lineData.valueString)));

                  tempSource.AddRepositoryXref(repositoryXref);
                }
                if (lineData.child != null)
                {
                  GedcomLineObject subLineData = lineData.child;

                  foreach(GedcomLineData line in subLineData.gedcomLines)
                  {
                    switch(line.tagString)
                    {
                      case "NOTE":
                        {
                          NoteClass tempNote = new NoteClass();

                          if (line.valueString.Length > 0)
                          {
                            line.valueString = ProcessCombiningDiacriticalMarks(line.valueString);
                            if (ValidateXrefName(line.valueString))
                            {
                              NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(line.valueString)));

                              tempSource.AddNoteXref(note);
                            }
                            else
                            {
                              tempNote.Concatenate(line.valueString, false);
                            }
                          }
                          if (line.child != null)
                          {
                            DecodeGedcomNoteStructure(tagStack, line.child, ref tempNote);
                          }
                          tempSource.AddNote(tempNote);
                        }
                        break;

                      default:
                        HandleUnknownTag(tagStack, lineData);
                        //DebugStringAdd("Error: " + tagStack.GetTagStack() + " tag type: " + lineData.tagString + " " + lineData.valueString);
                        CheckUndecodedChildren(tagStack, lineData.child);
                        break;

                    }
                  }
                  
                }
              }
              break;

            case "OBJE":
              {
                if (lineData.child != null)
                {
                  MultimediaLinkClass tempMultimediaLink = new MultimediaLinkClass();

                  DecodeGedcomMultimediaLink(tagStack, lineData.child, tempMultimediaLink);

                  tempSource.AddMultimediaLink(tempMultimediaLink);
                }
              }
              break;

            case "NOTE":
              {
                NoteClass tempNote = new NoteClass();

                if (lineData.valueString.Length > 0)
                {
                  if (ValidateXrefName(lineData.valueString))
                  {
                    NoteXrefClass note = new NoteXrefClass(xrefMappers.GetLocalXRef(XrefType.Note, GetXrefName(lineData.valueString)));

                    tempSource.AddNoteXref(note);
                  }
                  else
                  {
                    tempNote.Concatenate(lineData.valueString, false);
                  }
                }
                if (lineData.child != null)
                {
                  DecodeGedcomNoteStructure(tagStack, lineData.child, ref tempNote);
                }
                tempSource.AddNote(tempNote);
              }
              break;

            case "CHAN":
              {
                //DebugStringAdd("Source change: " + lineData.valueString);
                if (lineData.child != null)
                {
                  IndividualEventClass tempEvent = new IndividualEventClass();

                  DecodeGedcomChange(tagStack, lineData.child, ref tempEvent);
                  tempSource.SetChange(tempEvent);
                }
                else
                {
                  DebugStringAdd("NOTE " + tagStack.GetTagStack() + " no children: " + lineData.valueString);
                }
              }
              break;

            default:
              HandleUnknownTag(tagStack, lineData);
              //DebugStringAdd("Error: " + tagStack.GetTagStack() + " tag type: " + lineData.tagString + " " + lineData.valueString);
              CheckUndecodedChildren(tagStack, lineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;


      //familyTree.sourceList.Add(tempSource);
      familyTree.AddSource(tempSource);
      return true;
    }

    private bool DecodeGedcomSubmissionRecord(TagStack tagStack, GedcomLineObject gedcomLineObject, string xrefIdString)
    {
      gedcomLineObject.ObjectDecodeStart("SubmissionRecord", gedcomLineObject);

      SubmissionClass tempSubmission = new SubmissionClass();

      if (xrefIdString.Length > 0)
      {
        //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
        tempSubmission.SetXrefName(xrefMappers.GetLocalXRef(XrefType.Submission, xrefIdString, true));
      }
      else
      {
        return false;
      }
      for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
      {
        GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

        //DebugStringAdd("  submitter-line: " + lineData.level + " " + lineData.tagString + " " + lineData.valueString + " ");
        if (CheckTag(ref tagStack, lineData))
        {
          //tagStack.AddTag(firstLineData.tagString);
          //tagStack.AddTag(lineData.tagString);
          switch (lineData.tagString)
          {
            /*case "SUBN":
              if (lineData.xrefIdString.Length > 0)
              {
                //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
                tempSubmission.SetXrefName(GetLocalXRef(XrefType.Submission, lineData.xrefIdString));
              }
              break;*/

            case "SUBM":
              //DebugStringAdd("  xref: " + lineData.xrefIdString + " ");
              SubmitterXrefClass submitterXref = new SubmitterXrefClass(xrefMappers.GetLocalXRef(XrefType.Submitter, GetXrefName(lineData.valueString)));

              tempSubmission.SetSubmitter(submitterXref);
              if (lineData.child != null)
              {
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "FAMF":
              //DebugStringAdd("Line: " + lineData.lineNo + ": " + tagStack.GetTagStack() + " Family file name: " + lineData.valueString);
              tempSubmission.SetFamilyFile(lineData.valueString);
              if (lineData.child != null)
              {
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "TEMP":
              //DebugStringAdd("Line: " + lineData.lineNo + ": " + tagStack.GetTagStack() + " Temple name: " + lineData.valueString);
              tempSubmission.SetTemple(lineData.valueString);
              if (lineData.child != null)
              {
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "ANCE":
              //DebugStringAdd("Line: " + lineData.lineNo + ": " + tagStack.GetTagStack() + " Generation of ancestors: " + lineData.valueString);
              tempSubmission.SetAncestorGenerations(lineData.valueString);
              if (lineData.child != null)
              {
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "DESC":
              //DebugStringAdd("Line: " + lineData.lineNo + ": " + tagStack.GetTagStack() + " Generation of descendants: " + lineData.valueString);
              tempSubmission.SetDescendantGenerations(lineData.valueString);
              if (lineData.child != null)
              {
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            case "ORDI":
              //DebugStringAdd("Line: " + lineData.lineNo + ": " + tagStack.GetTagStack() + " Religious ordinance: " + lineData.valueString);
              tempSubmission.SetOrdinance(lineData.valueString);
              if (lineData.child != null)
              {
                CheckUndecodedChildren(tagStack, lineData.child);
              }
              break;

            default:
              HandleUnknownTag(tagStack, lineData);
              //DebugStringAdd("Line: " + lineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " tag type: " + lineData.tagString + " " + lineData.valueString);
              CheckUndecodedChildren(tagStack, lineData.child);
              break;
          }
          //tagStack.RemoveLast();
        }
      }
      decodedLines += gedcomLineObject.gedcomLines.Count;

      //familyTree.submissionList.Add(tempSubmission);
      familyTree.AddSubmission(tempSubmission);
      return true;
    }

    private bool DecodeUnknownObject(TagStack tagStack, GedcomLineObject gedcomLineObject, string xrefIdString = null)
    {
      if (gedcomLineObject != null)
      {
        gedcomLineObject.ObjectDecodeStart("Unknown", gedcomLineObject);

        for (int i = 0; i < gedcomLineObject.gedcomLines.Count; i++)
        {
          GedcomLineData lineData = gedcomLineObject.gedcomLines[i];

          /*if ((gedcomLineObject.GetLevel() + 1) != lineData.level)
          {
            DebugStringAdd("Line: " + lineData.lineNo + ": Bad level " + tagStack.GetTagStack() + " object type:[" + lineData.level + ":" + lineData.tagString + ":" + lineData.valueString + "]");

          }*/

          HandleUnknownTag(tagStack, lineData);
          if (CheckTag(ref tagStack, lineData))
          {
            //tagStack.AddTag(firstLineData.tagString);
            //tagStack.AddTag(lineData.tagString);
            //DebugStringAdd("Line: " + firstLineData.lineNo + ": Unknown " + tagStack.GetTagStack() + " object type:[" + firstLineData.level + ":" + firstLineData.tagString + ":" + firstLineData.valueString + "]");
            CheckUndecodedChildren(tagStack, lineData.child);
            //tagStack.RemoveLast();
          }
        }
        decodedLines += gedcomLineObject.gedcomLines.Count;
      }
      return true;
    }

    public void DecodeObject(GedcomLineObject gedcomLineObject)
    {
      //DebugStringAdd("DecodeObject Count=" + gedcomLineObject.gedcomLines.Count);

      //gedcomLineObject.PrintShort();
      TagStack tagStack = new TagStack();


      if (trace.Switch.Level.HasFlag(SourceLevels.Information))
      {
        //gedcomLineObject.ObjectDecodeStart("ObjectRoot", gedcomLineObject);
      }
      if (gedcomLineObject.gedcomLines.Count > 0)
      {
        foreach (GedcomLineData firstLineData in gedcomLineObject.gedcomLines)
        {

          if (firstLineData.level != 0)
          {
            DebugStringAdd("Error: First line data not on level 0 " + firstLineData.level + ", " + firstLineData.tagString + "," + firstLineData.xrefIdString);

            gedcomLineObject.PrintShort();
            return;
          }
          if (CheckTag(ref tagStack, firstLineData))
          {
            //tagStack.AddTag(firstLineData.tagString);
            switch (firstLineData.tagString)
            {
              case "HEAD":
                if (firstLineData.child != null)
                {
                  if (DecodeGedcomHead(tagStack, firstLineData.child))
                  {
                    decodeGedcomBody = true;
                  }
                }
                break;
              case "SUBM":
                if (decodeGedcomBody)
                {
                  if ((firstLineData.child != null) && (firstLineData.xrefIdString.Length > 0))
                  {
                    if (DecodeGedcomSubmitterRecord(tagStack, firstLineData.child, firstLineData.xrefIdString))
                    {
                    }
                  }
                }
                else
                {
                  DebugStringAdd("Error: Object in incorrect place:" + firstLineData.level + ", " + firstLineData.tagString);
                }
                break;
              case "INDI":
                if (decodeGedcomBody)
                {
                  if ((firstLineData.child != null) && (firstLineData.xrefIdString.Length > 0))
                  {
                    if (DecodeGedcomIndividualRecord(tagStack, firstLineData.child, firstLineData.xrefIdString))
                    {
                    }
                  }
                }
                else
                {
                  DebugStringAdd("Error: Object in incorrect place:" + firstLineData.level + ", " + firstLineData.tagString);
                }
                break;
              case "FAM":
                if (decodeGedcomBody)
                {
                  if ((firstLineData.child != null) && (firstLineData.xrefIdString.Length > 0))
                  {
                    if (DecodeGedcomFamilyRecord(tagStack, firstLineData.child, firstLineData.xrefIdString))
                    {
                    }
                  }
                }
                else
                {
                  DebugStringAdd("Error: Object in incorrect place:" + firstLineData.level + ", " + firstLineData.tagString);
                }
                break;
              case "OBJE":
                if (decodeGedcomBody)
                {
                  if ((firstLineData.child != null) && (firstLineData.xrefIdString.Length > 0))
                  {

                    if (DecodeGedcomMultimediaRecord(tagStack, firstLineData.child, firstLineData.xrefIdString))
                    {
                    }
                  }
                }
                else
                {
                  DebugStringAdd("Error: Object in incorrect place:" + firstLineData.level + ", " + firstLineData.tagString);
                }
                break;
              case "NOTE":
                if (decodeGedcomBody)
                {
                  if ((firstLineData.child != null) && (firstLineData.xrefIdString.Length > 0))
                  {
                    if (DecodeGedcomNoteRecord(tagStack, firstLineData.child, firstLineData.xrefIdString))
                    {
                    }
                  }
                }
                else
                {
                  DebugStringAdd("Error: Object in incorrect place:" + firstLineData.level + ", " + firstLineData.tagString);
                }
                break;
              case "REPO":
                if (decodeGedcomBody)
                {
                  if ((firstLineData.child != null) && (firstLineData.xrefIdString.Length > 0))
                  {
                  if (DecodeGedcomRepositoryRecord(tagStack, firstLineData.child, firstLineData.xrefIdString))
                  {
                  }
                  }
                }
                else
                {
                  DebugStringAdd("Error: Object in incorrect place:" + firstLineData.level + ", " + firstLineData.tagString);
                }
                break;
              case "SOUR":
                if (decodeGedcomBody)
                {
                  if ((firstLineData.child != null) && (firstLineData.xrefIdString.Length > 0))
                  {
                    if (DecodeGedcomSourceRecord(tagStack, firstLineData.child, firstLineData.xrefIdString))
                    {
                    }
                  }
                }
                else
                {
                  DebugStringAdd("Error: Object in incorrect place:" + firstLineData.level + ", " + firstLineData.tagString);
                }
                break;
              case "SUBN":
                if (decodeGedcomBody)
                {
                  if ((firstLineData.child != null) && (firstLineData.xrefIdString.Length > 0))
                  {
                    if (DecodeGedcomSubmissionRecord(tagStack, firstLineData.child, firstLineData.xrefIdString))
                    {
                    }
                  }
                }
                else
                {
                  DebugStringAdd("Error: Object in incorrect place:" + firstLineData.level + ", " + firstLineData.tagString);
                }
                break;
              case "TRLR":
                //DebugStringAdd("TrailerObject:start");
                if (decodeGedcomBody)
                {
                  decodeGedcomBody = false;
                  decodingCompleted = true;
                  CheckUndecodedChildren(tagStack, firstLineData.child);
                }
                else
                {
                  DebugStringAdd("Error: Object in incorrect place:" + firstLineData.level + ", " + firstLineData.tagString);
                }
                //DebugStringAdd("TrailerObject:end");
                break;
              default:
                if (decodeGedcomBody)
                {
                  if (DecodeUnknownObject(tagStack, firstLineData.child, firstLineData.xrefIdString))
                  {
                  }
                }
                break;
            }
          }
          //tagStack.RemoveLast();
        }
        /*if (tagStack.GetLevel() != 0)
        {
          DebugStringAdd("Error: tagstack not empty:[" + tagStack.GetTagStack() + "]");
        }*/
      }

    }

    private void ShowXrefMapperIntegrity(IDictionary<string, XrefMapperClass> mapper, string listType, bool summary)
    {
      IDictionaryEnumerator enumerator = (IDictionaryEnumerator)mapper.GetEnumerator();
      int cnt = 0;
      int references = 0;
      int warnings = 0;

      while (enumerator.MoveNext())
      {
        XrefMapperClass xrefData = (XrefMapperClass)enumerator.Value;
        string oldXref = (string)enumerator.Key;
        if (xrefData.noOfDefinitions == 0)
        {
          if (!summary)
          {
            DebugStringAdd(listType + ": Unknown xref: @" + oldXref + "@ referenced " + xrefData.noOfReferences + " times but never defined!");
          }
          warnings++;
        }
        else if (xrefData.noOfDefinitions != 1)
        {
          if (!summary)
          {
            DebugStringAdd(listType + ": Unknown xref: @" + oldXref + "@ referenced " + xrefData.noOfReferences + " times and multiply defined " + xrefData.noOfDefinitions + " times!");
          }
          warnings++;
        }
        if (xrefData.noOfReferences == 0)
        {
          if (!summary)
          {
            DebugStringAdd(listType + ": xref: @" + oldXref + "@ never referenced.");
          }
          warnings++;
        }

        cnt++;
        references += xrefData.noOfReferences;
      }
      if (summary)
      {
        DebugStringAdd(listType + ": Checked : " + cnt + " xrefs with " + references + " references and " + warnings + " warnings,");
      }

    }

    public void ShowUnknownTags()
    {
      IDictionaryEnumerator enumerator = (IDictionaryEnumerator)unhandledTagList.GetEnumerator();

      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Individual), "Individuals",       false);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Family),     "Families",          false);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Note),       "Notes",             false);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Multimedia), "MultimediaObjects", false);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Submission), "Submissions",       false);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Submitter),  "Submitters",        false);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Source),     "Sources",           false);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Repository), "Repositories",      false);
      while (enumerator.MoveNext())
      {
        //int count = (int)enumerator.Value;
        //string tag = (string)enumerator.Key;
        DebugStringAdd("Unknown tag: " + (string)enumerator.Key + " occurred " + (int)enumerator.Value + " times.");
      }

      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Individual), "Individuals", true);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Family),     "Families", true);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Note),       "Notes", true);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Multimedia), "MultimediaObjects", true);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Submission), "Submissions", true);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Submitter),  "Submitters", true);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Source),     "Sources", true);
      ShowXrefMapperIntegrity(xrefMappers.GetMapper(XrefType.Repository), "Repositories", true);
    }


    public int GetDecodedLines()
    {
      return decodedLines;
    }
    public bool DecodingCompleted()
    {
      return decodingCompleted;
    }
  }
}
