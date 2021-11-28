using System;
//using System.Collections.Generic;
using System.IO;
using System.Reflection;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

namespace Ekmansoft.FamilyTree.Library.FamilyTreeStore
{
  public class FamilyUtility
  {
    private string currentDirectory;

    public FamilyUtility()
    {
      currentDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

      if (!Directory.Exists(currentDirectory))
      {
        Directory.CreateDirectory(currentDirectory);
      }
    }

    public string GetCurrentDirectory()
    {
      return currentDirectory;
    }

    public static String MakeFilename(String s)
    {
      foreach (char c in System.IO.Path.GetInvalidFileNameChars())
      {
        s = s.Replace(c, '_');
      }
      return s;
    }

    public static String GetLinefeed()
    {
      return "\r\n";
    }
  }
}
