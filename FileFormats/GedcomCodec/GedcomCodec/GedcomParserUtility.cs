using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FamilyTreeLibrary.FileFormats.GedcomCodec
{
  public class GedcomParserUtility
  {
    public bool IsNewLine(char ch)
    {
      switch (ch)
      {
        case '\n': //(char)0x10:
        case '\r': //(char)0x13:
          return true;

        default:
          return false;
      }
    }
    public bool IsBlankChar(char ch)
    {
      switch (ch)
      {
        case ' ':
        case '\t':
          return true;

        default:
          return false;
      }
    }
    public bool IsValidAlpha(char ch)
    {
      if ((ch >= 'a') && (ch <= 'z'))
      {
        return true;
      }
      if ((ch >= 'A') && (ch <= 'Z'))
      {
        return true;
      }
      if (ch == '_')
      {
        return true;
      }
      return false;
    }
    public bool IsValidDigit(char ch)
    {
      if ((ch >= '0') && (ch <= '9'))
      {
        return true;
      }
      return false;
    }
    public bool IsValidAlphaNumerical(char ch)
    {
      if (IsValidAlpha(ch))
      {
        return true;
      }
      if (IsValidDigit(ch))
      {
        return true;
      }
      return false;
    }

    public bool IsValidXrefChar(char ch)
    {
      if (IsValidAlpha(ch))
      {
        return true;
      }
      if (IsValidDigit(ch))
      {
        return true;
      }
      if ((ch == '-') || (ch == ':'))
      {
        return true;
      }
      return false;
    }
  }

}
