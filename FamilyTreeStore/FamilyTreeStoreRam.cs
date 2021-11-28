using Ekmansoft.FamilyTree.Library.FamilyData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
//using Ekmansoft.FamilyTree.Library.FamilyFileFormat;

namespace Ekmansoft.FamilyTree.Library.FamilyTreeStore
{

  [DataContract]
  [KnownType(typeof(IndividualClass))]
  [KnownType(typeof(FamilyClass))]
  [KnownType(typeof(NoteClass))]
  [KnownType(typeof(SourceClass))]
  [KnownType(typeof(RepositoryClass))]
  [KnownType(typeof(SubmissionClass))]
  [KnownType(typeof(MultimediaObjectClass))]
  [KnownType(typeof(SubmitterClass))]

  public class FamilyTreeStoreRam : IFamilyTreeStoreBaseClass
  {
    [DataMember]
    private static readonly TraceSource trace = new TraceSource("FamilyTreeStoreRam", SourceLevels.Warning);
    // Add trace as datamember to avoid null-problems after reading tree from native file.
    [DataMember]
    private IDictionary<string, FamilyClass> familyList;
    [DataMember]
    private IDictionary<string, IndividualClass> individualList;
    [DataMember]
    private IDictionary<string, NoteClass> noteList;
    [DataMember]
    private IDictionary<string, SourceClass> sourceList;
    [DataMember]
    private IDictionary<string, RepositoryClass> repositoryList;
    [DataMember]
    private IDictionary<string, SubmissionClass> submissionList;
    [DataMember]
    private IDictionary<string, MultimediaObjectClass> multimediaObjectList;
    [DataMember]
    private IDictionary<string, SubmitterClass> submitterList;

    [DataMember]
    private int nextXrefId;


    // About the data file
    [DataMember]
    private FamilyDateTimeClass date;
    [DataMember]
    private SubmitterXrefClass submitterXref;
    [DataMember]
    private String fileType;
    [DataMember]
    private String fileFormat;
    [DataMember]
    private String fileVersion;
    [DataMember]
    private String sourceFileName;
    [DataMember]
    private FamilyTreeCharacterSet characterSet;
    [DataMember]
    private CorporationClass corporation;
    [DataMember]
    private string homePerson;

    //private BackgroundWorker backgroundWorker;

    //public ValidationData validationData;

    public FamilyTreeStoreRam()
    {
      individualList = new Dictionary<String, IndividualClass>();
      familyList = new Dictionary<String, FamilyClass>();
      noteList = new Dictionary<String, NoteClass>();
      sourceList = new Dictionary<String, SourceClass>();
      repositoryList = new Dictionary<String, RepositoryClass>();
      submissionList = new Dictionary<String, SubmissionClass>();
      multimediaObjectList = new Dictionary<String, MultimediaObjectClass>();

      submitterList = new Dictionary<String, SubmitterClass>();
      //submitterList = new List<IndividualClass>();
      corporation = new CorporationClass();
      nextXrefId = 1;
    }

    public FamilyTreeCapabilityClass GetCapabilities()
    {
      return new FamilyTreeCapabilityClass { jsonSearch = false };
    }

    public void Dispose()
    {
      trace.TraceData(TraceEventType.Information, 0, "FamilyTreeStoreRam.Dispose()");
    }

    public void AddFamily(FamilyClass tempFamily)
    {
      if (familyList.ContainsKey(tempFamily.GetXrefName()))
      {
        familyList.Remove(tempFamily.GetXrefName());
      }
      familyList.Add(tempFamily.GetXrefName(), tempFamily);
    }

    public FamilyClass GetFamily(String xrefName)
    {
      if (familyList.ContainsKey(xrefName))
      {
        return (FamilyClass)familyList[xrefName];
      }
      return null;
    }


    public bool AddIndividual(IndividualClass tempIndividual)
    {
      if (tempIndividual.GetXrefName().Length > 0)
      {
        individualList.Add(tempIndividual.GetXrefName(), tempIndividual);
        return true;
      }
      return false;
    }

    public bool UpdateIndividual(IndividualClass tempIndividual, PersonUpdateType updateType)
    {
      if (individualList.ContainsKey(tempIndividual.GetXrefName()))
      {
        IndividualClass updatePerson = individualList[tempIndividual.GetXrefName()];

        if ((updateType & PersonUpdateType.ChildFamily) != 0)
        {
          updatePerson.SetFamilyChildList(tempIndividual.GetFamilyChildList());
        }
        if ((updateType & PersonUpdateType.SpouseFamily) != 0)
        {
          updatePerson.SetFamilySpouseList(tempIndividual.GetFamilySpouseList());
        }
        if ((updateType & PersonUpdateType.Name) != 0)
        {
          updatePerson.SetPersonalName(tempIndividual.GetPersonalName());
        }
        if ((updateType & PersonUpdateType.Events) != 0)
        {
          updatePerson.SetEventList(tempIndividual.GetEventList());
        }

        individualList[tempIndividual.GetXrefName()] = updatePerson;
        return true;
      }
      else
      {
        trace.TraceEvent(TraceEventType.Error, 0, "Error: Can't update {0} as it is not in the database!", tempIndividual.GetXrefName());
      }
      return false;
    }
    public IndividualClass GetIndividual(String xrefName = null, uint index = (uint)SelectIndex.NoIndex, PersonDetail detailLevel = PersonDetail.All)
    {
      if (xrefName == null)
      {
        IEnumerator<IndividualClass> enumerator = individualList.Values.GetEnumerator();
        if (enumerator.MoveNext())
        {
          //DictionaryEntry entry;
          IndividualClass indi;

          indi = (IndividualClass)(enumerator.Current);

          //indi = (IndividualClass)entry.Value;

          if (trace.Switch.Level.HasFlag(SourceLevels.Information))
          {
            trace.TraceInformation("GetIndividual.Returns:");
            indi.Print();
          }
          return indi;
        }
        return null;
      }

      if (!individualList.ContainsKey(xrefName))
      {
        trace.TraceData(TraceEventType.Error, 0, "GetIndividual() key not found:" + xrefName);
        return null;
      }
      return (IndividualClass)individualList[xrefName];
    }

    public void SetHomeIndividual(String xrefName)
    {
      homePerson = xrefName;
    }
    public string GetHomeIndividual()
    {
      return homePerson;
    }

    public IEnumerator<IndividualClass> SearchPerson(String individualName, IProgressReporterInterface progressReporter = null)
    {
      IEnumerator<IndividualClass> enumerator = individualList.Values.GetEnumerator();
      string searchName = "";
      ProgressReporterClass progress = new ProgressReporterClass(individualList.Count);
      int i = 0;

      if (individualName != null)
      {
        searchName = individualName.ToUpper();
      }

      trace.TraceInformation("SearchPerson()");

      while (enumerator.MoveNext())
      {
        String name = enumerator.Current.GetName().ToUpper();

        if (progressReporter != null)
        {
          if (progress.Update(i++))
          {
            progressReporter.ReportProgress(progress.GetPercent());
          }
        }

        if (name != null)
        {
          if (name.Contains(searchName))
          {
            yield return enumerator.Current;
          }
        }
        else
        {
          yield return enumerator.Current;
        }

      }
      trace.TraceInformation("SearchPerson(" + searchName + ") done");
    }

    public IEnumerator<FamilyClass> SearchFamily(String familyXrefName = null, IProgressReporterInterface progressReporter = null)
    {
      IEnumerator<FamilyClass> enumerator = familyList.Values.GetEnumerator(); ;
      ProgressReporterClass progress = new ProgressReporterClass(familyList.Count);
      int i = 0;

      trace.TraceInformation("SearchFamily()");

      while (enumerator.MoveNext())
      {
        if (progressReporter != null)
        {
          if (progress.Update(i++))
          {
            progressReporter.ReportProgress(progress.GetPercent());
          }
        }
        if (familyXrefName != null)
        {
          String name = enumerator.Current.GetXrefName();

          if (name != null)
          {
            if (name.Contains(familyXrefName))
            {
              yield return enumerator.Current;
            }
          }
        }
        else
        {
          yield return enumerator.Current;
        }
      }

      trace.TraceInformation("SearchFamily(" + familyXrefName + "): null");
    }

    public void AddMultimediaObject(MultimediaObjectClass tempMultimediaObject)
    {
      multimediaObjectList.Add(tempMultimediaObject.GetXrefName(), tempMultimediaObject);
    }

    public IEnumerator<MultimediaObjectClass> SearchMultimediaObject(String mmoString = null, IProgressReporterInterface progressReporter = null)
    {
      return null;
    }

    public void AddNote(NoteClass tempNote)
    {
      noteList.Add(tempNote.GetXrefName(), tempNote);
    }

    public NoteClass GetNote(String xrefName)
    {
      return (NoteClass)noteList[xrefName];
    }

    public IEnumerator<NoteClass> SearchNote(String noteString = null, IProgressReporterInterface progressReporter = null)
    {
      IEnumerator<NoteClass> enumerator = noteList.Values.GetEnumerator();
      ProgressReporterClass progress = new ProgressReporterClass(noteList.Count);
      int i = 0;

      trace.TraceInformation("SearchNote()");

      while (enumerator.MoveNext())
      {
        if (progressReporter != null)
        {
          if (progress.Update(i++))
          {
            progressReporter.ReportProgress(progress.GetPercent());
          }
        }
        if (noteString != null)
        {
          String noteStr = enumerator.Current.note;

          if (noteStr != null)
          {
            if (noteStr.Contains(noteString))
            {
              yield return enumerator.Current;
            }
          }
        }
        else
        {
          yield return enumerator.Current;
        }
      }

      trace.TraceInformation("SearchNote(" + noteString + "): null");
    }

    public void AddRepository(RepositoryClass tempRepository)
    {
      repositoryList.Add(tempRepository.GetXrefName(), tempRepository);
    }

    public IEnumerator<RepositoryClass> SearchRepository(String repositoryString = null, IProgressReporterInterface progressReporter = null)
    {
      return null;
    }

    public void AddSource(SourceClass tempSource)
    {
      sourceList.Add(tempSource.GetXrefName(), tempSource);
    }

    public IEnumerator<SourceClass> SearchSource(String sourceString = null, IProgressReporterInterface progressReporter = null)
    {
      return null;
    }

    public void AddSubmission(SubmissionClass tempSubmission)
    {
      submissionList.Add(tempSubmission.GetXrefName(), tempSubmission);
    }

    public IEnumerator<SubmissionClass> SearchSubmission(String submissionString = null, IProgressReporterInterface progressReporter = null)
    {
      return null;
    }

    public void AddSubmitter(SubmitterClass tempSubmitter)
    {
      if (tempSubmitter.GetXrefName() == null)
      {
        trace.TraceInformation("Error: Submitter with empty xref: " + tempSubmitter.GetName());
        return;
      }
      submitterList.Add(tempSubmitter.GetXrefName(), tempSubmitter);
    }
    public void SetSubmitterXref(SubmitterXrefClass tempSubmitterXref)
    {
      submitterXref = tempSubmitterXref;
    }
    public IEnumerator<SubmitterClass> SearchSubmitter(String submitterString = null, IProgressReporterInterface progressReporter = null)
    {
      IEnumerator<SubmitterClass> enumerator = submitterList.Values.GetEnumerator();
      ProgressReporterClass progress = new ProgressReporterClass(submitterList.Count);
      int i = 0;

      while (enumerator.MoveNext())
      {
        if (progressReporter != null)
        {
          if (progress.Update(i++))
          {
            progressReporter.ReportProgress(progress.GetPercent());
          }
        }
        if (submitterString != null)
        {
          String submitterStr = enumerator.Current.GetName();

          if (submitterStr != null)
          {
            if (submitterStr.Contains(submitterString))
            {
              yield return enumerator.Current;
            }
          }
        }
        else
        {
          yield return enumerator.Current;
        }
      }

      trace.TraceInformation("SearchSubmitter(" + submitterString + "): null");
    }

    public void SetSourceFileType(String type)
    {
      fileType = type;
    }
    public void SetSourceFileTypeVersion(String version)
    {
      fileVersion = version;
    }
    public void SetSourceFileTypeFormat(String format)
    {
      fileFormat = format;
    }
    public void SetSourceFileName(String file)
    {
      sourceFileName = file;
    }
    public string GetSourceFileName()
    {
      return sourceFileName;
    }
    public void SetSourceName(String source)
    {
      sourceFileName = source;
    }
    public void SetCharacterSet(FamilyTreeCharacterSet charSet)
    {
      characterSet = charSet;
    }

    public void SetDate(FamilyDateTimeClass inDate)
    {
      date = inDate;
    }

    public void Print()
    {
      bool printItems = false;
      trace.TraceInformation("Tree Overview:");
      trace.TraceInformation(" Families:     " + familyList.Count);
      if (printItems)
      {
        foreach (FamilyClass family in familyList.Values)
        {
          family.Print();
        }
      }

      trace.TraceInformation(" Individuals:  " + individualList.Count);
      if (printItems)
      {
        foreach (IndividualClass individual in individualList.Values)
        {
          individual.Print();
        }
      }
      trace.TraceInformation(" Notes:        " + noteList.Count);
      if (printItems)
      {
        foreach (NoteClass note in noteList.Values)
        {
          note.Print();
        }
      }
      trace.TraceInformation(" Sources:      " + sourceList.Count);
      if (printItems)
      {
        foreach (SourceClass source in sourceList.Values)
        {
          source.Print();
        }
      }
      trace.TraceInformation(" Submitters:   " + submitterList.Count);
      if (printItems)
      {
        foreach (SubmitterClass submitter in submitterList.Values)
        {
          submitter.Print();
        }
      }
      trace.TraceInformation(" Repositories: " + repositoryList.Count);
      if (printItems)
      {
        foreach (RepositoryClass repository in repositoryList.Values)
        {
          repository.Print();
        }
      }
      trace.TraceInformation(" Submissions:  " + submissionList.Count);
      if (printItems)
      {
        foreach (SubmissionClass submission in submissionList.Values)
        {
          submission.Print();
        }
      }
      trace.TraceInformation(" Multimedia:   " + multimediaObjectList.Count);
      if (printItems)
      {
        foreach (MultimediaObjectClass multimediaObject in multimediaObjectList.Values)
        {
          multimediaObject.Print();
        }
      }

    }

    public string CreateNewXref(XrefType type)
    {

      switch (type)
      {
        case XrefType.Individual:
          return "I" + nextXrefId++;

        case XrefType.Family:
          return "F" + nextXrefId++;

        case XrefType.Multimedia:
          return "M" + nextXrefId++;

        case XrefType.Note:
          return "N" + nextXrefId++;

        case XrefType.Source:
          return "S" + nextXrefId++;

        case XrefType.Submission:
          return "U" + nextXrefId++;

        case XrefType.Submitter:
          return "T" + nextXrefId++;

        default:
          return "-" + nextXrefId++;
      }
    }
    public string CreateNewXref_o(XrefType type)
    {
      switch (type)
      {
        case XrefType.Individual:
          return "I" + Guid.NewGuid().ToString();

        case XrefType.Family:
          return "F" + Guid.NewGuid().ToString();

        case XrefType.Multimedia:
          return "M" + Guid.NewGuid().ToString();

        case XrefType.Note:
          return "N" + Guid.NewGuid().ToString();

        case XrefType.Source:
          return "S" + Guid.NewGuid().ToString();

        case XrefType.Submission:
          return "U" + Guid.NewGuid().ToString();

        case XrefType.Submitter:
          return "T" + Guid.NewGuid().ToString();

        default:
          return "-" + Guid.NewGuid().ToString();
      }
    }

    public void PrintShort()
    {
      trace.TraceInformation("F: " + familyList.Count + " I:" + individualList.Count + " N:" + noteList.Count + " S:" + sourceList.Count + " Sm:" + submitterList.Count + " R:" + repositoryList.Count);

      trace.TraceInformation(" Sl:" + submissionList.Count);
      trace.TraceInformation(" M:" + multimediaObjectList.Count);
      trace.TraceInformation("");

    }

    public String GetShortTreeInfo()
    {
      return "Ind:" + individualList.Count + " Fam:" + familyList.Count + " Note:" + noteList.Count + " Src:" + sourceList.Count + " M:" + multimediaObjectList.Count + " Subm:" + submitterList.Count + "/" + submissionList.Count + " Repo:" + repositoryList.Count;
    }



    public bool ValidateFamilies()
    {
      int errorNo = 0;
      int count = 0;
      int progressTickSize = familyList.Count / 100;
      IEnumerator<FamilyClass> enumerator = familyList.Values.GetEnumerator();
      ValidationData validationData;

      validationData = new ValidationData();

      while (enumerator.MoveNext())
      {
        FamilyClass family;

        family = enumerator.Current;

        if (!family.Validate(this, ref validationData))
        {
          errorNo++;
        }
        count++;
      }
      if (validationData != null)
      {
        trace.TraceInformation("Validated: " + validationData.familyNo + " families, " + validationData.submitterNo + " submitters, :" + validationData.individualNo + " individuals, " + validationData.noteNo + " notes");
      }
      trace.TraceInformation("ValidateFamilies: " + errorNo + " errors of " + familyList.Count + " validations.");
      validationData = null;
      return true;
    }

    public bool ValidateIndividuals()
    {
      int errorNo = 0;
      int count = 0;
      int progressTickSize = individualList.Count / 100;
      IEnumerator<IndividualClass> enumerator = individualList.Values.GetEnumerator();
      ValidationData validationData;

      validationData = new ValidationData();

      while (enumerator.MoveNext())
      {
        IndividualClass indi = enumerator.Current;

        if (!indi.Validate(this, ref validationData))
        {
          errorNo++;
        }
        count++;

      }
      if (validationData != null)
      {
        trace.TraceInformation("Validated: " + validationData.familyNo + " families, " + validationData.submitterNo + " submitters, :" + validationData.individualNo + " individuals, " + validationData.noteNo + " notes");
      }
      trace.TraceInformation("ValidateIndividuals: " + errorNo + " errors of " + individualList.Count + " validations.");
      validationData = null;
      return true;
    }


    public bool ValidateTree()
    {
      int errorNo = 0;
      int count = 0;
      int progressTickSize = familyList.Count / 100;
      IEnumerator<FamilyClass> enumerator = familyList.Values.GetEnumerator(); ;
      ValidationData validationData;

      validationData = new ValidationData();

      while (enumerator.MoveNext())
      {
        FamilyClass family;

        family = enumerator.Current;

        if (!family.Validate(this, ref validationData))
        {
          errorNo++;
        }
        count++;

      }

      trace.TraceInformation("Validate: " + familyList.Count);
      trace.TraceInformation("ValidateTree: " + errorNo + " errors of " + familyList.Count + " validations.");

      return (errorNo == 0);
    }

    public FamilyTreeContentClass GetContents()
    {
      FamilyTreeContentClass contents = new FamilyTreeContentClass();

      contents.families = familyList.Count;
      contents.individuals = individualList.Count;
      contents.notes = noteList.Count;
      contents.repositories = repositoryList.Count;
      contents.sources = sourceList.Count;
      contents.submissions = submissionList.Count;
      contents.submitters = submitterList.Count;
      contents.multimediaObjects = multimediaObjectList.Count;

      return contents;

    }
  }

}
