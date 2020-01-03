using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
//using FamilyTreeLibrary.FileFormats.GedcomCodec;
using FamilyTreeLibrary.FamilyTreeStore;

namespace FamilyTreeLibrary.FileFormats.GedcomCodec
{
  class GedcomLineStatus
  {
    private static TraceSource trace = new TraceSource("GedcomLineStatus", SourceLevels.Warning);
    private int linePos;
    private char[] lineBuffer;
    private int maxLineLength;

    private String tempString;
    private GedcomLineData lineData;
    //bool isUtf8Format;
    private GedcomFileCharacterSet characterSet;

    private GedcomParserUtility parser;
    private uint lineNo;
    private FileImportResult importResult;
    private string lineFeedString;
    //private FileBuffer fileBuffer;

    public GedcomFileCharacterSet GetCharacterSet()
    {
      return characterSet;
    }
    public void SetCharacterSet(GedcomFileCharacterSet inCharacterSet)
    {
      characterSet = inCharacterSet;
    }

    public void DebugStringAdd(string str)
    {
      /*if (importResult == null)
      {
        importResult = "";
      }*/
      //importResultString += str + "\n";
      importResult.AddString(str);

    }

    public FileImportResult GetDebugString()
    {
      return importResult;

    }
    public void SetDebugString(FileImportResult result)
    {
      importResult = result;
    }



    /*enum LineState
    {
      Start,
      LevelDone,
      Text
    };*/
    //LineState state;

    public GedcomLineStatus(ref FileImportResult importResult, string linefeed)
    {
      maxLineLength = 512;
      lineBuffer = new char[maxLineLength];
      ResetLine();
      parser = new GedcomParserUtility();
      //isUtf8Format = true;
      characterSet = GedcomFileCharacterSet.Ascii;
      //fileBuffer = inFileBuffer;
      lineNo = 1;
      lineFeedString = linefeed;
      this.importResult = importResult;
    }

    private void ResetLine()
    {
      linePos = 0;
      //state = LineState.Start;
    }

    private char GetOffsetCharacter(int offset, char character)
    {
      switch (offset)
      {
        case 0xc2:
          return (char)(character - 0x80 + 0x80);

        case 0xc3:
          return (char)(character - 0x80 + 0xc0);

        case 0xc4:
          return (char)(character - 0x80 + 0x100);

        case 0xc5:
          return (char)(character - 0x80 + 0x140);

        case 0xc6:
          return (char)(character - 0x80 + 0x180);

        case 0xc7:
          return (char)(character - 0x80 + 0x1C0);

        case 0xc8:
          return (char)(character - 0x80 + 0x200);

        case 0xc9:
          return (char)(character - 0x80 + 0x240);

        case 0xca:
          return (char)(character - 0x80 + 0x280);

        case 0xcb:
          return (char)(character - 0x80 + 0x2c0);

        case 0xcc:
          return (char)(character - 0x80 + 0x300);

        case 0xcd:
          return (char)(character - 0x80 + 0x340);

        case 0xce:
          return (char)(character - 0x80 + 0x380);

        case 0xcf:
          return (char)(character - 0x80 + 0x3c0);

        case 0xd0:
          return (char)(character - 0x80 + 0x400);

        case 0xd1:
          return (char)(character - 0x80 + 0x440);

        case 0xd2:
          return (char)(character - 0x80 + 0x480);

        case 0xd3:
          return (char)(character - 0x80 + 0x4c0);

        case 0xd4:
          return (char)(character - 0x80 + 0x500);

        case 0xd5:
          return (char)(character - 0x80 + 0x540);

        case 0xd6:
          return (char)(character - 0x80 + 0x580);

        case 0xd7:
          return (char)(character - 0x80 + 0x5c0);

        case 0xd8:
          return (char)(character - 0x80 + 0x600);

        case 0xd9:
          return (char)(character - 0x80 + 0x640);

        case 0xda:
          return (char)(character - 0x80 + 0x680);

        case 0xdb:
          return (char)(character - 0x80 + 0x6c0);

        case 0xdc:
          return (char)(character - 0x80 + 0x700);

        case 0xdd:
          return (char)(character - 0x80 + 0x740);

        case 0xde:
          return (char)(character - 0x80 + 0x740);

        case 0xdf:
          return (char)(character - 0x80 + 0x740);

        default:
          return 'y';

      }

      //return 'x';
    }

    private void AddChar(char ch)
    {
      if (linePos >= maxLineLength - 1)
      {
        char[] newBuffer = new char[maxLineLength * 2];
        for (int i = 0; i < linePos; i++)
        {
          newBuffer[i] = lineBuffer[i];
        }
        maxLineLength = maxLineLength * 2;
        lineBuffer = newBuffer;
      }
      lineBuffer[linePos] = ch;
      linePos++;
    }

    private int DecodeLevelString(String levelString, ref bool parseError)
    {
      int tempLevel = 0;
      int strPos = 0;

      while (strPos < levelString.Length)
      {
        if ((levelString[strPos] >= '0') && (levelString[strPos] <= '9'))
        {
          tempLevel = tempLevel * 10 + (levelString[strPos] - '0');
        }
        else
        {
          parseError = true;
        }
        strPos++;
      }
      if (parseError)
      {
        DebugStringAdd("Line:" + lineNo + " Unknown characters in level string:[" + levelString + "] decodes level as " + tempLevel);
      }
      return tempLevel;
    }

    private bool DecodeXrefString(String tString)
    {
      //int tempXref = 0;
      int strPos = 1;

      if (tString[0] != '@')
      {
        return false;
      }
      if (tString[tString.Length - 1] != '@')
      {
        return false;
      }

      while (strPos < (tString.Length - 1))
      {
        if (parser.IsValidXrefChar(tString[strPos]))
        {
          lineData.xrefIdString += tString[strPos];
          strPos++;
        }
        else
        {
          lineData.xrefIdString = "";
          return false;
        }
      }
      return true;
    }
    private bool DecodeTagString(String tString)
    {
      int strPos = 0;

      while (strPos < tString.Length)
      {
        if (parser.IsValidAlphaNumerical(tString[strPos]))
        {
          lineData.tagString += tString[strPos];
        }
        else
        {
          return false;
        }
        strPos++;
      }
      return true;
    }
    private bool DecodeValueString(String tString)
    {
      int strPos = 0;
      int offsetData = 0;

      switch (GetCharacterSet())
      {
        case GedcomFileCharacterSet.Utf8:
          {
            tString = tString.Normalize();

            while (strPos < tString.Length)
            {
              if (offsetData == 0)
              {
                if ((tString[strPos] >= 0xc2) && (tString[strPos] <= 0xdf))
                {
                  offsetData = tString[strPos];
                }
                else
                {
                  lineData.valueString += tString[strPos];
                }
              }
              else
              {
                lineData.valueString += GetOffsetCharacter(offsetData, tString[strPos]);
                offsetData = 0;
              }
              strPos++;
            }
          }
          break;

        case GedcomFileCharacterSet.Ascii:
        default:
          {
            while (strPos < tString.Length)
            {
              lineData.valueString += tString[strPos];
              strPos++;
            }
          }
          break;


      }

      return true;
    }

    public GedcomLineData DecodeLine()
    {
      int parsePos = 0;
      bool failure = false;
      if (linePos == 0)
      {
        lineNo++;
        return null;
      }
      //trace.TraceInformation("DecodeLine start");

      lineData = new GedcomLineData();


      if (trace.Switch.Level.HasFlag(SourceLevels.Information))
      {
        DebugStringAdd("Line:" + lineNo + " [line start]");
        for (int i = 0; i < linePos; i++)
        {
          DebugStringAdd(lineBuffer[i].ToString());
        }
        DebugStringAdd("");
        DebugStringAdd("[line end]");
      }

      tempString = "";

      while (parser.IsBlankChar(lineBuffer[parsePos]))
      {
        parsePos++;
      }
      while (!parser.IsBlankChar(lineBuffer[parsePos]))
      {
        tempString += lineBuffer[parsePos++];
      }
      lineData.level = DecodeLevelString(tempString, ref failure);

      while (parser.IsBlankChar(lineBuffer[parsePos]))
      {
        parsePos++;
      }
      if (lineBuffer[parsePos] == '@')
      {
        tempString = "";

        while (!parser.IsBlankChar(lineBuffer[parsePos]))
        {
          tempString += lineBuffer[parsePos++];
        }
        if (!DecodeXrefString(tempString))
        {
          DebugStringAdd("Line:" + lineNo + " invalid xref id string!");
          failure = true;
        }
        while (parser.IsBlankChar(lineBuffer[parsePos]))
        {
          parsePos++;
        }
      }
      tempString = "";
      while (!parser.IsBlankChar(lineBuffer[parsePos]) && (parsePos < linePos))
      {
        tempString += lineBuffer[parsePos++];
      }
      if (!DecodeTagString(tempString) && !failure)
      {
        DebugStringAdd("Line:" + lineNo + " invalid tag!");
        failure = true;
      }
      tempString = "";

      // Only eat first blank character in value part.
      if (parser.IsBlankChar(lineBuffer[parsePos]))
      {
        parsePos++;
      }
      while (parsePos < linePos)
      {
        tempString += lineBuffer[parsePos++];
      }
      if (!DecodeValueString(tempString) && !failure)
      {
        DebugStringAdd("Line:" + lineNo + " invalid value!");
        failure = true;
      }
      if ((lineData.tagString.Length < 3) && !failure)
      {
        DebugStringAdd("Line:" + lineNo + " invalid tag: " + lineData.tagString);
        failure = true;
      }
      if (!failure)
      {
        lineData.valid = true;
      }
      lineData.lineNo = lineNo;
      linePos = 0;
      lineNo++;

      if (trace.Switch.Level.HasFlag(SourceLevels.Information))
      {
        lineData.Print();
      }

      return lineData;
    }

    public uint ReadLine(ref GedcomParserProgress progress)
    {
      ResetLine();

      bool endOfLine = false;
      uint noOfChars = 0;
      int lfCnt = 0;
      bool lineEndStarted = false;

      while ((progress.position < progress.size) && !endOfLine)
      {
        char ch;
        ch = (char)progress.data[progress.position++];

        if (lineEndStarted || parser.IsNewLine(ch))
        {
          if (lineFeedString[lfCnt++] != ch)
          {
            DebugStringAdd("Line:" + lineNo + " Inconsistent line feeds!");
            if((progress.data.Length > progress.position) && (parser.IsNewLine((char)progress.data[progress.position + 1])))
            {
              progress.position++;
            }
            endOfLine = true;
          }
          if (lfCnt == lineFeedString.Length)
          {
            endOfLine = true;
          }
          lineEndStarted = true;
        }
        else
        {
          AddChar(ch);
          noOfChars++;
        }
      }

      //trace.TraceInformation("ReadLine end");
      return noOfChars;
    }

    public override string ToString()
    {
      return lineData.ToString();
    }

    public uint GetLineNo()
    {
      return lineNo;
    }

    public FileImportResult GetImportResultString()
    {
      return this.importResult;
    }

  }
}
