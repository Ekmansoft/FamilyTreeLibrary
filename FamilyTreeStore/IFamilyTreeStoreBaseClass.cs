//using System.ComponentModel;
using FamilyTreeLibrary.FamilyData;
using System;
using System.Collections.Generic;
using System.Text;
//using FamilyTreeLibrary.FamilyFileFormat;

namespace FamilyTreeLibrary.FamilyTreeStore
{
  public class FamilyTreeContentClass
  {
    public int families;
    public int submitters;
    public int individuals;
    public int notes;
    public int sources;
    public int repositories;
    public int submissions;
    public int multimediaObjects;
    //public int percent;

    private void AppendStrIfNotZero(ref StringBuilder builder, int value, string str)
    {
      if (value != 0)
      {
        if (builder.Length > 0)
        {
          builder.Append(", ");
        }
        builder.Append(value.ToString() + " " + str);
      }
    }

    public override string ToString()
    {
      StringBuilder builder = new StringBuilder();

      AppendStrIfNotZero(ref builder, individuals, "individuals");
      AppendStrIfNotZero(ref builder, families, "families");
      AppendStrIfNotZero(ref builder, notes, "notes");
      AppendStrIfNotZero(ref builder, sources, "sources");
      AppendStrIfNotZero(ref builder, repositories, "repositories");
      AppendStrIfNotZero(ref builder, submissions, "submissions");
      AppendStrIfNotZero(ref builder, submitters, "submitters");
      AppendStrIfNotZero(ref builder, multimediaObjects, "multimedia-objects");
      //AppendStrIfNotZero(ref builder, percent, "percent");
      return builder.ToString();
    }
  }

  public class FamilyTreeCapabilityClass
  {
    public bool jsonSearch;
  }


  public class ValidationData
  {
    public int familyNo;
    public int submitterNo;
    public int individualNo;
    public int noteNo;
  };

  public enum FamilyTreeCharacterSet
  {
    Unknown,
    Utf8,
    Unicode,
    Ascii,
    Ansel
  }
  public enum SelectIndex
  {
    NoIndex = 0x7fffffff
  }

  public enum PersonDetail
  {
    Name = 0x0001,
    Events = 0x0002,
    Sex = 0x0004,
    Children = 0x0008,
    Parents = 0x0010,

    All = 0xFFFF
  }

  [Flags]
  public enum PersonUpdateType
  {
    Name = 0x0001,
    Events = 0x0002,
    SpouseFamily = 0x0004,
    ChildFamily = 0x0008,
  }
  public interface IFamilyTreeStoreBaseClass
  {

    // Family interface
    void AddFamily(FamilyClass tempFamily);
    FamilyClass GetFamily(String xrefName);
    IEnumerator<FamilyClass> SearchFamily(String familyXrefName = null, IProgressReporterInterface progressReporter = null);

    // Person interface
    bool AddIndividual(IndividualClass tempIndividual);
    bool UpdateIndividual(IndividualClass tempIndividual, PersonUpdateType type);
    IndividualClass GetIndividual(String xrefName = null, uint index = (uint)SelectIndex.NoIndex, PersonDetail detailLevel = PersonDetail.All);
    IEnumerator<IndividualClass> SearchPerson(String individualName = null, IProgressReporterInterface progressReporter = null);
    void SetHomeIndividual(String xrefName);
    string GetHomeIndividual();

    // Multimedia object interface
    void AddMultimediaObject(MultimediaObjectClass tempMultimediaObject);
    IEnumerator<MultimediaObjectClass> SearchMultimediaObject(String mmoString = null, IProgressReporterInterface progressReporter = null);

    // Note interface
    void AddNote(NoteClass tempNote);
    NoteClass GetNote(String xrefName);
    IEnumerator<NoteClass> SearchNote(String noteString = null, IProgressReporterInterface progressReporter = null);

    // Repository interface
    void AddRepository(RepositoryClass tempRepository);
    IEnumerator<RepositoryClass> SearchRepository(String repositoryString = null, IProgressReporterInterface progressReporter = null);

    // Source interface (Move to import?)
    void AddSource(SourceClass tempSource);
    IEnumerator<SourceClass> SearchSource(String sourceString = null, IProgressReporterInterface progressReporter = null);

    // Submission interface (Move to import?)
    void AddSubmission(SubmissionClass tempSubmission);
    IEnumerator<SubmissionClass> SearchSubmission(String submissionString = null, IProgressReporterInterface progressReporter = null);

    // Submitter interface (Move to import?)
    void AddSubmitter(SubmitterClass tempSubmitter);
    void SetSubmitterXref(SubmitterXrefClass tempSubmitterXref);
    IEnumerator<SubmitterClass> SearchSubmitter(String submitterName = null, IProgressReporterInterface progressReporter = null);

    string CreateNewXref(XrefType type);

    // Source information (Move to import?)
    void SetSourceFileType(String type);
    void SetSourceFileTypeVersion(String version);
    void SetSourceFileTypeFormat(String format);
    void SetSourceFileName(String filename);
    string GetSourceFileName();

    void SetSourceName(String source);

    void SetCharacterSet(FamilyTreeCharacterSet charSet);

    void SetDate(FamilyDateTimeClass inDate);

    // Print functions
    //void Print();
    //void PrintShort();

    String GetShortTreeInfo();

    FamilyTreeCapabilityClass GetCapabilities();

    FamilyTreeContentClass GetContents();

    // Validation functions
    //bool ValidateFamilies();

    //bool ValidateIndividuals();

    //bool ValidateTree();

    void Dispose();

  }
}
