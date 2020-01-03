using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FamilyTreeLibrary.FileFormats.GedcomCodec
{
  class GedcomParserProgress
  {
    public Byte[] data;

    public long size;
    public long position;

    public GedcomParserProgress(Byte[] dataPointer, long inDataSize)
    {
      data = dataPointer;
      size = inDataSize;
      position = 0;
    }
    public bool IsEndOfFile()
    {
      return position >= size;
    }
  }

}
