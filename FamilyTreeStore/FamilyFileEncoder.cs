using System.Collections.Generic;
//using FamilyTreeLibrary.FamilyData;
//using FamilyTreeLibrary.FamilyTreeStore;


namespace FamilyTreeLibrary.FamilyTreeStore
{
  public interface FamilyFileEncoder
  {
    void StoreFile(IFamilyTreeStoreBaseClass familyTree, string filename, FamilyFileTypeOperation operation, int variant = 0);

    void SetProgressTarget(IProgressReporterInterface progressTarget);

    string GetFileTypeFilter(FamilyFileTypeOperation operation, int variant = 0);

    bool IsKnownFileType(string filename);

    IDictionary<int, string> GetOperationVariantList(FamilyFileTypeOperation operation);
  }
}
