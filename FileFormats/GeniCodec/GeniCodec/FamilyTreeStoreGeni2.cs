using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
//using Microsoft.Win32;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Web;
//using System.Web.Script;
using System.Text.Json;
//using System.Web.Script.Serialization;
using FamilyTreeLibrary.FamilyData;
using FamilyTreeLibrary.FamilyTreeStore;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO.Compression;
//using FamilyStudioFormsGui.WindowsGui.FamilyWebBrowser;

namespace FamilyTreeLibrary.FileFormats.GeniCodec
{
  [DataContract]
  public class FamilyTreeStoreGeni2 : IFamilyTreeStoreBaseClass, IDisposable
  {
    private static readonly TraceSource trace = new TraceSource("FamilyTreeStoreGeni2", SourceLevels.Warning);

    private String sourceFileName;
    private const int CACHE_CLEAR_DELAY = 3600 * 24 * 7; // one week
    private FamilyTimer authenticationTimer;
    private GeniAccessStats stats;
    private string homePerson;
    private GeniAppAuthenticationClass appAuthentication;
    private CompletedCallback completedCallback;
    private const int MaxProfilesToSearch = 150;
    private const int MaxRetryCount = 5;
    private const int DefaultRetryTime = 2000;
    private static readonly HttpClient httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });

    protected virtual void Dispose(bool managed)
    {
      trace.TraceData(TraceEventType.Information, 0, "FamilyTreeStoreGeni2 dispose");
      if (managed)
      {
        /*if (authenticationWebBrowser != null)
        {
          authenticationWebBrowser.Dispose();
        }*/
      }
      if (authenticationTimer != null)
      {
        authenticationTimer.Dispose();
        authenticationTimer = null;
      }
    }
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    private void CheckAuthentication()
    {
      //AuthenticateApp();

      trace.TraceData(TraceEventType.Warning, 0, "Geni.com authentication:" + appAuthentication.ToString());

      if (appAuthentication.IsValid())
      {
        trace.TraceData(TraceEventType.Information, 0, "Geni.com authentication ok ");
        if (appAuthentication.TimeToReauthenticate())
        {
          AuthenticateApp();
        }
      }
      else
      {
        trace.TraceData(TraceEventType.Warning, 0, "Geni.com authentication not ok !!");
        AuthenticateApp();
      }
      if (geniTreeSize == null)
      {
        GetTreeStats();
      }
      if (appAuthentication.IsValid() && !AuthenticationTimerIsRunning())
      {
        StartAuthenticationTimer();
      }
    }

    private class AccessStats
    {
      public int attempt;
      public int fetchSuccess;
      public int cacheSuccess;
      public int failureRetry;
      public int failure;
      public TimeSpan slowestFetch;

      public override string ToString()
      {
        return "attempts: " + attempt + " fetch success:" + fetchSuccess + " cache success:" + cacheSuccess + " fail/retry:" + failureRetry + " failed:" + failure + " slowest:" + slowestFetch;
      }
      public void Print()
      {
        trace.TraceInformation(ToString());
      }
    }

    private class GeniAccessStats
    {
      public AccessStats GetIndividual;
      public AccessStats GetFamily;
      public AccessStats SearchIndividual;

      public GeniAccessStats()
      {
        GetIndividual = new AccessStats();
        GetFamily = new AccessStats();
        SearchIndividual = new AccessStats();
      }
      public override string ToString()
      {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("GetIndividuals:");
        builder.AppendLine(GetIndividual.ToString());
        builder.AppendLine("GetFamilies:");
        builder.AppendLine(GetFamily.ToString());
        builder.AppendLine("SearchIndividuals:");
        builder.AppendLine(SearchIndividual.ToString());
        return builder.ToString();
      }
    }

    [DataContract]
    private class GeniCache
    {
      [DataMember]
      private IDictionary<string, IndividualClass> individuals;
      [DataMember]
      private IDictionary<string, FamilyClass> families;
      [DataMember]
      private IDictionary<string, List<string>> parentsI2fReference;
      [DataMember]
      private IDictionary<string, List<string>> childrenI2fReference;
      [DataMember]
      private IDictionary<string, List<string>> parentsF2iReference;
      [DataMember]
      private IDictionary<string, List<string>> childrenF2iReference;
      [DataMember]
      private DateTime latestUpdate;

      private TimeSpan maxCacheTime = TimeSpan.FromDays(1);

      class AddFamilyEvent : EventArgs
      {
        public FamilyClass family;
        public AddFamilyEvent(FamilyClass family)
        {
          this.family = family;
        }
      }
      class AddIndividualEvent : EventArgs
      {
        public IndividualClass individual;
        public AddIndividualEvent(IndividualClass individual)
        {
          this.individual = individual;
        }
      }

      public GeniCache()
      {
        individuals = new Dictionary<string, IndividualClass>();
        families = new Dictionary<string, FamilyClass>();
        latestUpdate = DateTime.Now;
        parentsF2iReference = new Dictionary<string, List<string>>();
        childrenF2iReference = new Dictionary<string, List<string>>();
        parentsI2fReference = new Dictionary<string, List<string>>();
        childrenI2fReference = new Dictionary<string, List<string>>();
      }

      void UpdateF2iReferences(FamilyClass family)
      {
        int checkNo = 0;
        int addedNo = 0;
        IList<IndividualXrefClass> children = family.GetChildList();
        IList<IndividualXrefClass> parents = family.GetParentList();
        lock (childrenF2iReference)
        {
          foreach (IndividualXrefClass individual in children)
          {
            if (!childrenF2iReference.ContainsKey(individual.GetXrefName()))
            {
              childrenF2iReference.Add(individual.GetXrefName(), new List<string>());
            }
            if (!childrenF2iReference[individual.GetXrefName()].Contains(family.GetXrefName()))
            {
              childrenF2iReference[individual.GetXrefName()].Add(family.GetXrefName());
              addedNo++;
            }
            checkNo++;
          }
        }
        lock (parentsF2iReference)
        {
          foreach (IndividualXrefClass individual in parents)
          {
            if (!parentsF2iReference.ContainsKey(individual.GetXrefName()))
            {
              parentsF2iReference.Add(individual.GetXrefName(), new List<string>());
            }
            if (!parentsF2iReference[individual.GetXrefName()].Contains(family.GetXrefName()))
            {
              parentsF2iReference[individual.GetXrefName()].Add(family.GetXrefName());
              addedNo++;
            }
            checkNo++;
          }
        }
      }

      void UpdateI2fReferences(IndividualClass individual)
      {
        int checkNo = 0;
        int addedNo = 0;
        lock (childrenI2fReference)
        {
          IList<FamilyXrefClass> childFamilies = individual.GetFamilyChildList();
          foreach (FamilyXrefClass family in childFamilies)
          {
            if (!childrenI2fReference.ContainsKey(family.GetXrefName()))
            {
              childrenI2fReference.Add(family.GetXrefName(), new List<string>());
            }
            if (!childrenI2fReference[family.GetXrefName()].Contains(individual.GetXrefName()))
            {
              childrenI2fReference[family.GetXrefName()].Add(individual.GetXrefName());
              addedNo++;
            }
            checkNo++;
          }
        }
        lock (parentsI2fReference)
        {
          IList<FamilyXrefClass> spouseFamilies = individual.GetFamilySpouseList();
          foreach (FamilyXrefClass family in spouseFamilies)
          {
            if (!parentsI2fReference.ContainsKey(family.GetXrefName()))
            {
              parentsI2fReference.Add(family.GetXrefName(), new List<string>());
            }
            if (!parentsI2fReference[family.GetXrefName()].Contains(individual.GetXrefName()))
            {
              parentsI2fReference[family.GetXrefName()].Add(individual.GetXrefName());
            }
            checkNo++;
          }
          addedNo++;
        }
      }

      void CheckI2fReferences(ref FamilyClass family)
      {
        if (parentsI2fReference.ContainsKey(family.GetXrefName()))
        {
          IList<string> parents = parentsI2fReference[family.GetXrefName()];
          if (parents.Count > family.GetParentList().Count)
          {
            trace.TraceData(TraceEventType.Warning, 0, family.GetXrefName() + " missing parents in family " + parents.Count + " > " + family.GetParentList().Count);
            foreach (string parent in parents)
            {
              family.AddRelation(new IndividualXrefClass(parent), FamilyClass.RelationType.Parent);
            }
          }
        }
        if (childrenI2fReference.ContainsKey(family.GetXrefName()))
        {
          IList<string> children = childrenI2fReference[family.GetXrefName()];
          if (children.Count > family.GetChildList().Count)
          {
            trace.TraceData(TraceEventType.Warning, 0, family.GetXrefName() + " missing children in family " + children.Count + " > " + family.GetParentList().Count);
            foreach (string child in children)
            {
              family.AddRelation(new IndividualXrefClass(child), FamilyClass.RelationType.Child);
            }
          }
        }
      }

      void CheckF2iReferences(ref IndividualClass individual)
      {
        if (parentsF2iReference.ContainsKey(individual.GetXrefName()))
        {
          IList<string> spouses = parentsF2iReference[individual.GetXrefName()];
          if (spouses.Count > individual.GetFamilySpouseList().Count)
          {
            trace.TraceData(TraceEventType.Verbose, 0, individual.GetXrefName() + " missing spouse to individual " + spouses.Count + " > " + individual.GetFamilySpouseList().Count);
            foreach (string parent in spouses)
            {
              individual.AddRelation(new FamilyXrefClass(parent), IndividualClass.RelationType.Spouse);
              trace.TraceData(TraceEventType.Verbose, 0, individual.GetXrefName() + " adding spouse-family to individual " + parent + " to " + individual.GetName());
            }
          }
        }
        if (childrenI2fReference.ContainsKey(individual.GetXrefName()))
        {
          IList<string> children = childrenI2fReference[individual.GetXrefName()];
          if (children.Count > individual.GetFamilyChildList().Count)
          {
            trace.TraceData(TraceEventType.Verbose, 0, individual.GetXrefName() + " missing child-family in individual " + children.Count + " > " + individual.GetFamilyChildList().Count);
            foreach (string child in children)
            {
              individual.AddRelation(new FamilyXrefClass(child), IndividualClass.RelationType.Child);
              trace.TraceData(TraceEventType.Verbose, 0, individual.GetXrefName() + " adding child-family to individual " + child + " to " + individual.GetName());
            }
          }
        }
      }

      void RemoveI2fReferences(string familyXref)
      {
        if (parentsI2fReference.ContainsKey(familyXref))
        {
          parentsI2fReference.Remove(familyXref);
        }
        if (childrenI2fReference.ContainsKey(familyXref))
        {
          childrenI2fReference.Remove(familyXref);
        }
      }


      void RemoveF2iReferences(string individualXref)
      {
        if (parentsF2iReference.ContainsKey(individualXref))
        {
          parentsF2iReference.Remove(individualXref);
        }
        if (childrenI2fReference.ContainsKey(individualXref))
        {
          childrenI2fReference.Remove(individualXref);
        }
      }

        void CacheFamily(FamilyClass family)
      {
        trace.TraceInformation("CacheFamily " + family.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);

        lock (families)
        {
          if (!families.ContainsKey(family.GetXrefName()))
          {
            trace.TraceInformation("cached family-2 " + family.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);

            latestUpdate = DateTime.Now;
            families.Add(family.GetXrefName(), family);
            UpdateF2iReferences(family);
          }
          else
          {
            trace.TraceInformation("skipped family " + family.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);
          }
        }

      }

      void CacheIndividual(IndividualClass individual)
      {
        trace.TraceInformation("CacheIndidvidual " + individual.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);
        lock (individuals)
        {
          if (!individuals.ContainsKey(individual.GetXrefName()))
          {
            trace.TraceInformation("cached individual-2 " + individual.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);

            individuals.Add(individual.GetXrefName(), individual);
            UpdateI2fReferences(individual);
            latestUpdate = DateTime.Now;
          }
          else
          {
            trace.TraceInformation("skipped individual " + individual.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);
          }
        }
      }

      private void Clear()
      {
        Print();
        individuals.Clear();
        families.Clear();
        trace.TraceInformation(" Geni Cache cleared! " + families.Count + " families and " + individuals.Count + " people");
      }

      public bool CheckIndividual(string xrefName)
      {
        if (latestUpdate.AddSeconds(CACHE_CLEAR_DELAY) < DateTime.Now)
        {
          Clear();
        }
        if (individuals.ContainsKey(xrefName))
        {
          IndividualClass individual = individuals[xrefName];

          DateTime latestUpdate = individual.GetLatestUpdate();

          if ((DateTime.Now - latestUpdate) < maxCacheTime)
          {
            return true;
          }
          individuals.Remove(xrefName);
          RemoveF2iReferences(xrefName);
        }
        return false;
      }

      public void AddIndividual(IndividualClass individual)
      {
        if (individual.GetXrefName().Length == 0)
        {
          trace.TraceEvent(TraceEventType.Error, 0, "AddIndividual():error: no xref!");
        }
        else
        {
          bool relations = false;
          trace.TraceInformation("cached individual " + individual.GetXrefName());

          if (individual.GetFamilyChildList() != null)
          {
            if (individual.GetFamilyChildList().Count > 0)
            {
              relations = true;
            }
          }
          if (individual.GetFamilySpouseList() != null)
          {
            if (individual.GetFamilySpouseList().Count > 0)
            {
              relations = true;
            }
          }
          if (!relations)
          {
            if (individual.GetPublic())
            {
              string url = "";
              IList<string> urls = individual.GetUrlList();
              if(urls.Count > 0)
              {
                url = urls[0];
              }
              trace.TraceData(TraceEventType.Warning, 0, "Warning, person has no relations! " + individual.GetXrefName() + " " + url + " " + individual.GetName());
            }
            CheckF2iReferences(ref individual);
          }
          CacheIndividual(individual);
          latestUpdate = DateTime.Now;
        }
      }

      public IndividualClass GetIndividual(string xrefName)
      {
        if (CheckIndividual(xrefName))
        {
          return individuals[xrefName];
        }
        return null;
      }

      public IEnumerator<IndividualClass> GetIndividualIterator()
      {
        return individuals.Values.GetEnumerator();
      }

      public bool CheckFamily(string xrefName)
      {
        if (families.ContainsKey(xrefName))
        {
          FamilyClass family= families[xrefName];

          DateTime latestUpdate = family.GetLatestUpdate();

          if ((DateTime.Now - latestUpdate) < maxCacheTime)
          {
            return true;
          }
          families.Remove(xrefName);
          RemoveI2fReferences(xrefName);
        }
        return false;
      }


      public void AddFamily(FamilyClass family)
      {
        if (family.GetXrefName().Length == 0)
        {
          trace.TraceEvent(TraceEventType.Error, 0, "error: no xref!");
        }
        else
        {
          if (!families.ContainsKey(family.GetXrefName()))
          {
            trace.TraceInformation("cached family " + family.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);

            CacheFamily(family);
          }
          else
          {
            trace.TraceData(TraceEventType.Information, 0, "family " + family.GetXrefName() + " already in cache!");
          }
        }
      }

      public FamilyClass GetFamily(string xrefName)
      {
        if (latestUpdate.AddSeconds(CACHE_CLEAR_DELAY) < DateTime.Now)
        {
          Clear();
        }
        if (CheckFamily(xrefName))
        {
          return families[xrefName];
        }
        return null;
      }
      public IEnumerator<FamilyClass> GetFamilyIterator()
      {
        return families.Values.GetEnumerator();
      }
      public int GetFamilyNo()
      {
        return families.Count;
      }
      public int GetIndividualNo()
      {
        return individuals.Count;
      }
      public void Print()
      {
        trace.TraceInformation(" Geni Cache includes " + families.Count +
          " families and " + individuals.Count +
          " people. Latest update " + latestUpdate +
          " now:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
      }

    }

    [DataMember]
    GeniCache cache;

    private HttpGeniTreeSize geniTreeSize;

    /*
     * request: https://www.geni.com/api/profile-<id>/photos
     */

    public class HttpPhotoSizes
    {
      public string large { get; set; }
      public string medium { get; set; }
      public string small { get; set; }
      public string thumb { get; set; }
      public string print { get; set; }
      public string thumb2 { get; set; }
      public string original { get; set; }
      public string url { get; set; }
    }

    public class HttpPhotoDate
    {
      public int day { get; set; }
      public int month { get; set; }
      public int year { get; set; }
      public string formatted_date { get; set; }
    }

    public class HttpPhotoResult
    {
      public string id { get; set; }
      public string guid { get; set; }
      public string created_at { get; set; }
      public string updated_at { get; set; }
      public string content_type { get; set; }
      public string url { get; set; }
      public string album_id { get; set; }
      public HttpPhotoSizes sizes { get; set; }
      public List<string> tags { get; set; }
      public HttpPhotoDate date { get; set; }
    }

    public class HttpPhotoRootObject
    {
      public List<HttpPhotoResult> results { get; set; }
      public int total_count { get; set; }
    }

    /*
     * https://www.geni.com/platform/oauth/request_token 
     */

    private class HttpAuthenticateResponse
    {
      public string access_token { get; set; }
      public string refresh_token { get; set; }
      public int expires_in { get; set; }
    }

    /*
    ** https://www.geni.com/api/stats/world-family-tree
    */

    private class HttpGeniTreeSize
    {
      public string formatted_size { get; set; }
      public int size { get; set; }
    }


    /*private class HttpSearchPerson
    {
      public string id { get; set; }
      public string name { get; set; }
    }*/

    /*
     *  https://www.geni.com/api/profile/search?names=kalle
     */

    private class HttpSearchPersonResult
    {
      public string prev_page { get; set; }
      public int page { get; set; }
      public string next_page { get; set; }
      public List<HttpPerson> results { get; set; }
    }

    /*
     * https://www.geni.com/api/profile-<id>/immediate-family?
     */

    private class HttpDate
    {
      public int day { get; set; }
      public int month { get; set; }
      public int year { get; set; }
      public bool circa { get; set; }
      public override string ToString()
      {
        string dStr = "";
        if (circa)
        {
          dStr = "ca ";
        }
        return dStr + year + "-" + month + "-" + day;
        //return base.ToString();
      }
    }

    private class HttpLocation
    {
      public string city { get; set; }
      public string place_name { get; set; }
      public string county { get; set; }
      public string state { get; set; }
      public string country { get; set; }
      public string country_code { get; set; }
      public double latitude { get; set; }
      public double longitude { get; set; }
      public override string ToString()
      {
        string locStr = "";
        if (place_name != null)
        {
          locStr += "," + place_name;
        }
        if (city != null)
        {
          locStr += "," + city;
        }
        if (state != null)
        {
          locStr += "," + state;
        }
        if (county != null)
        {
          locStr += "," + county;
        }
        if (country != null)
        {
          locStr += "," + country;
        }
        if (country_code != null)
        {
          locStr += "," + country_code;
        }
        if (!double.IsNaN(latitude))
        {
          locStr += "," + latitude;
        }
        if (!double.IsNaN(longitude))
        {
          locStr += "," + longitude;
        }
        return locStr;
      }
    }

    private class HttpEvent
    {
      public HttpDate date { get; set; }
      public HttpLocation location { get; set; }
      public override string ToString()
      {
        string evStr = "";
        if (location != null)
        {
          evStr += location.ToString() + ", ";
        }
        if (date != null)
        {
          evStr += date.ToString();
        }
        return "HttpEvent:" + evStr;
      }
    }

    private class HttpUnionRelation
    {
      public string rel { get; set; }
    }


    private class HttpPerson
    {
      public string id { get; set; }
      public string url { get; set; }
      public string merged_into { get; set; }
      public bool @public { get; set; }
      public bool is_alive { get; set; }
      public bool big_tree { get; set; }
      public string cause_of_death { get; set; }
      public HttpLocation current_residence { get; set; }
      public bool deleted { get; set; }
      public string profile_url { get; set; }
      public string guid { get; set; }
      public string email { get; set; }
      public string language { get; set; }
      public string status { get; set; }
      public string name { get; set; }
      public string first_name { get; set; }
      public string middle_name { get; set; }
      public string maiden_name { get; set; }
      public string last_name { get; set; }
      public string display_name { get; set; }
      public List<string> unions { get; set; }
      public List<string> nicknames { get; set; }
      public string gender { get; set; }
      public string about_me { get; set; }
      public string created_at { get; set; }
      public string updated_at { get; set; }
      public HttpEvent birth { get; set; }
      public HttpEvent baptism { get; set; }
      public HttpEvent death { get; set; }
      public HttpEvent burial { get; set; }
      public HttpPhotoSizes mugshot_urls { get; set; }
      public IDictionary<string, HttpUnionRelation> edges { get; set; }
      public override string ToString()
      {
        return id;
      }
    }

    private class HttpGetIndividualResult
    {
      public HttpPerson focus { get; set; }
      public IDictionary<string, HttpPerson> nodes { get; set; }
    }

    /*
     * https://www.geni.com/api/union-<id>
     */

    private class HttpFamilyResponse
    {
      public string id { get; set; }
      public string url { get; set; }
      public string guid { get; set; }
      public HttpEvent marriage { get; set; }
      public HttpEvent divorce { get; set; }
      public string status { get; set; }
      public List<string> partners { get; set; }
      public List<string> children { get; set; }
      public List<string> adopted_children { get; set; }
      public List<string> foster_children { get; set; }
    }

    /*
     * https://www.geni.com/api/user/max-family
     */

    private class HttpMaxFamilyResponse
    {
      public List<HttpPerson> results { get; set; }
      public int page { get; set; }
      public string next_page { get; set; }
    }


    public FamilyTreeStoreGeni2(CompletedCallback callback, GeniAppAuthenticationClass appAuthentication)
    {
      trace.TraceData(TraceEventType.Information, 0, "FamilyTreeStoreGeni2 created");

      this.appAuthentication = appAuthentication;

      geniTreeSize = null;

      cache = new GeniCache();

      stats = new GeniAccessStats();

      this.completedCallback = callback;
      authenticationTimer = new FamilyTimer();// System.Windows.Forms.Timer();
      authenticationTimer.Elapsed += AuthenticationTimer_Tick;
    }

    private bool AuthenticationTimerIsRunning()
    {
      if (authenticationTimer != null)
      {
        return authenticationTimer.Enabled;
      }
      return false;
    }

    private void StartAuthenticationTimer()
    {
      if (authenticationTimer == null)
      {
        trace.TraceData(TraceEventType.Information, 0, "Authentication timer removed!");
        return;
      }

      if (authenticationTimer.Enabled)
      {
        authenticationTimer.Stop();
      }

      if (!authenticationTimer.Enabled)
      {
        if (appAuthentication.GetExpiryTime() > DateTime.Now)
        {
          TimeSpan expireTime = appAuthentication.GetExpiryTime() - DateTime.Now;
          // Make timer expire a minute before time ends
          authenticationTimer.Interval = (expireTime.TotalSeconds - 60) * 1000;
        }
        else
        {
          authenticationTimer.Interval = 50;
        }
        authenticationTimer.AutoReset = false;
        authenticationTimer.Start();
        trace.TraceData(TraceEventType.Information, 0, "Authentication timer started:" + authenticationTimer.Interval + " " + appAuthentication.GetExpiryTime());
      }
      else
      {
        trace.TraceData(TraceEventType.Error, 0, "Authentication timer already started!");
      }
    }

    private void AuthenticationTimer_Tick(object sender, EventArgs e)
    {
      trace.TraceData(TraceEventType.Information, 0, "Authentication timer expires!");
      CheckAuthentication();
    }

    private string GetWebData(string mainURL, string secondaryURL, string requestDescription, int numberOfRetries)
    {
      string returnLine = null;
      bool failure = false;
      int retryCount = 0;
      GeniWebResultType resultClass = GeniWebResultType.Ok;
      int delayTime = DefaultRetryTime;

      do
      {
        string sURL = mainURL;
        failure = false;
        CheckGeniAuthentication();

        try
        {
          if ((resultClass == GeniWebResultType.FailedRetrySimple) && (secondaryURL != null))
          {
            sURL = secondaryURL;
          }

          WebRequest webRequestGetUrl;
          webRequestGetUrl = WebRequest.Create(sURL);
          webRequestGetUrl.Headers.Add("Authorization", String.Format("Bearer {0}", Uri.EscapeDataString(appAuthentication.GetAccessToken())));
          webRequestGetUrl.Headers.Add("Accept-Encoding", "gzip,deflate");
          trace.TraceInformation(requestDescription + " = " + sURL + " " + DateTime.Now);

          HttpWebResponse response = (HttpWebResponse)webRequestGetUrl.GetResponse();

          GeniWebResultType result = ClassifyWebResponse(response);
          if (result == GeniWebResultType.Ok)
          {
            Stream stream = null;

            switch (response.ContentEncoding.ToUpperInvariant())
            {
              case "GZIP":
                stream = new GZipStream(response.GetResponseStream(), CompressionMode.Decompress);
                break;
              case "DEFLATE":
                stream = new DeflateStream(response.GetResponseStream(), CompressionMode.Decompress);
                break;

              default:
                stream = response.GetResponseStream();
                //stream.ReadTimeout = ;
                break;
            }
            StreamReader objReader = new StreamReader(stream);

            returnLine = objReader.ReadToEnd();
            stream.Close();
          }
          else if (result == GeniWebResultType.OkTooFast)
          {
            trace.TraceData(TraceEventType.Warning, 0, "Running too fast...Breaking 10 s!");
            trace.TraceData(TraceEventType.Information, 0, "Headers " + response.Headers);
            Thread.Sleep(10000);
          }
          else 
          {
            trace.TraceData(TraceEventType.Warning, 0, "Result type " + result);
            trace.TraceData(TraceEventType.Information, 0, "Headers " + response.Headers);
          }
        }
        catch (WebException e)
        {
          HttpWebResponse httpResponse = (HttpWebResponse)e.Response;
          int httpResponseStatus = -1;
          if (httpResponse != null)
          {
            httpResponseStatus = GetHttpResponseStatus(httpResponse);
          }
          resultClass = ClassifyErrorWebResponse(httpResponseStatus);
          failure = true;

          if ((retryCount > 0) || (resultClass != GeniWebResultType.FailedRetrySimple))
          {
            if ((stats.GetIndividual.failureRetry++ > 0) || (resultClass != GeniWebResultType.FailedRetrySimple))
            {
              trace.TraceData(TraceEventType.Warning, 0, requestDescription + " WebException:" + retryCount + "/" + numberOfRetries + " " + httpResponseStatus + ": " + resultClass);
            }
            else
            {
              trace.TraceData(TraceEventType.Information, 0, requestDescription + " WebException:" + retryCount + "/" + numberOfRetries + " " + httpResponseStatus + ": " + resultClass);
            }

            if (retryCount == numberOfRetries)
            {
              trace.TraceData(TraceEventType.Warning, 0, "url:" + sURL);
              stats.GetIndividual.Print();
              trace.TraceData(TraceEventType.Warning, 0, "WebException: " + e.ToString());
            }

            if (e.Response != null)
            {
              trace.TraceData(TraceEventType.Information, 0, "Exception.Response.Headers: " + e.Response.Headers);
            }
            else
            {
              trace.TraceData(TraceEventType.Information, 0, "Exception.Response == null");
            }


            if (resultClass == GeniWebResultType.FailedReauthenticationNeeded)
            {
              appAuthentication.ForceReauthentication();
              CheckAuthentication();
            }
            else if (resultClass != GeniWebResultType.FailedRetrySimple)
            {
              Thread.Sleep(delayTime);

              delayTime = delayTime * 2;
            }
          }
        }
        catch (System.IO.IOException e)
        {
          stats.GetIndividual.failureRetry++;
          trace.TraceData(TraceEventType.Warning, 0, requestDescription + " IOException " + retryCount + "/" + numberOfRetries);
          if (retryCount == numberOfRetries)
          {
            trace.TraceData(TraceEventType.Warning, 0, "url:" + sURL);
            stats.GetIndividual.Print();
            trace.TraceData(TraceEventType.Warning, 0, "IOException: " + e.ToString());
          }
          failure = true;

          Thread.Sleep(delayTime);

          delayTime = delayTime * 2;
        }
        catch (System.OperationCanceledException e)
        {
          stats.GetIndividual.failureRetry++;
          trace.TraceData(TraceEventType.Warning, 0, requestDescription + " OpCanceledException " + retryCount + "/" + numberOfRetries);
          if (retryCount == numberOfRetries)
          {
            trace.TraceData(TraceEventType.Warning, 0, "url:" + sURL);
            stats.GetIndividual.Print();
            trace.TraceData(TraceEventType.Warning, 0, "OpCanceledException: " + e.ToString());
          }
          failure = true;

          Thread.Sleep(delayTime);

          delayTime = delayTime * 2;
        }
        catch (System.Exception e)
        {
          stats.GetIndividual.failureRetry++;
          trace.TraceData(TraceEventType.Warning, 0, requestDescription + " other Exception " + retryCount + "/" + numberOfRetries);
          if (retryCount == numberOfRetries)
          {
            trace.TraceData(TraceEventType.Warning, 0, "url:" + sURL);
            stats.GetIndividual.Print();
            trace.TraceData(TraceEventType.Warning, 0, "Other Exception: " + e.ToString());
          }
          failure = true;

          Thread.Sleep(delayTime);

          delayTime = delayTime * 2;
        }
        if ((returnLine != null) && ((returnLine.StartsWith("<!DOCTYPE") || returnLine.StartsWith("<HTML") || returnLine.StartsWith("<html"))))
        {
          trace.TraceData(TraceEventType.Warning, 0, requestDescription + ":Bad response format. Don't parse.");
          trace.TraceData(TraceEventType.Warning, 0, "**********************************************************-start");
          trace.TraceData(TraceEventType.Warning, 0, "{0}:{1}", returnLine.Length, returnLine);
          trace.TraceData(TraceEventType.Warning, 0, "**********************************************************-end");
          stats.GetIndividual.failureRetry++;
          trace.TraceData(TraceEventType.Warning, 0, requestDescription + " Format FAILURE " + retryCount + "/" + numberOfRetries + " " + sURL);
          failure = true;
          Thread.Sleep(10000);
        }
      } while (failure && (retryCount++ < numberOfRetries));

      if (returnLine == null)
      {
        trace.TraceData(TraceEventType.Error, 0, requestDescription + " Failed to receive any valid response from the server despite " + numberOfRetries + " retries!");
        return null;
      }

      if ((returnLine.StartsWith("<!DOCTYPE") || returnLine.StartsWith("<HTML") || returnLine.StartsWith("<html")))
      {
        trace.TraceData(TraceEventType.Warning, 0, requestDescription + ":Bad response format. Don't parse.");
        trace.TraceData(TraceEventType.Warning, 0, "**********************************************************-start");
        trace.TraceData(TraceEventType.Warning, 0, "{0}:{1}", returnLine.Length, returnLine);
        trace.TraceData(TraceEventType.Warning, 0, "**********************************************************-end");
        CheckAuthentication();
        return null;
      }
      if (trace.Switch.Level.HasFlag(SourceLevels.Information))
      {
        trace.TraceInformation("**********************************************************-start:");
        trace.TraceInformation("{0}:{1}", returnLine.Length, returnLine);
        trace.TraceInformation("**********************************************************-end:");
      }

      return returnLine;
    }

    private void CheckGeniAuthentication()
    {
      if (!appAuthentication.IsValid() && !appAuthentication.ForceReauthenticationOngoing())
      {
        trace.TraceData(TraceEventType.Warning, 0, "Geni authentication not valid..." + appAuthentication.ToString());
        appAuthentication.ForceReauthentication();
        CheckAuthentication();
      }
    }

    private string GetWebData2(string mainURL, string secondaryURL, string requestDescription, int numberOfRetries)
    {
      return PostWebData(mainURL, null, requestDescription, numberOfRetries);
    }

    private string PostWebData(string url, string postData, string requestDescription, int numberOfRetries)
    {
      string returnLine = null;
      bool failure = false;
      int retryCount = 0;
      //GeniWebResultType resultClass = GeniWebResultType.Ok;
      int delayTime = DefaultRetryTime;

      do
      {
        string sURL = url;
        failure = false;
        CheckGeniAuthentication();

        trace.TraceInformation(requestDescription + " = " + sURL + " " + DateTime.Now);

        try
        {
          HttpResponseMessage response = null;

          if (postData != null)
          {
            HttpContent content = new StringContent(postData, Encoding.UTF8, "application/json");
            //content.Headers.Add("Authorization", String.Format("Bearer {0}", Uri.EscapeDataString(appAuthentication.GetAccessToken())));
            response = httpClient.PostAsync(url, content).Result;
          }
          else
          {
            HttpCompletionOption httpCompletion = new HttpCompletionOption();
            response = httpClient.GetAsync(url, httpCompletion).Result;
          }
          //httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Uri.EscapeDataString(appAuthentication.GetAccessToken()));


          if (ClassifyHttpResponse(response) == GeniWebResultType.OkTooFast)
          {
            trace.TraceData(TraceEventType.Warning, 0, "Running too fast...Breaking 10 s!");
            trace.TraceData(TraceEventType.Information, 0, "Headers " + response.Headers);
            Thread.Sleep(10000);
          }

          response.EnsureSuccessStatusCode();
          returnLine = response.Content.ReadAsStringAsync().Result;
        }
        catch (HttpRequestException e)
        {
          stats.GetIndividual.failureRetry++;
          trace.TraceData(TraceEventType.Information, 0, "Exception.Response.Headers: " + e.ToString());

          Thread.Sleep(delayTime);

          delayTime = delayTime * 2;
        }
        catch (System.Exception e)
        {
          stats.GetIndividual.failureRetry++;
          trace.TraceData(TraceEventType.Warning, 0, requestDescription + " other Exception " + retryCount + "/" + numberOfRetries);
          if (retryCount == numberOfRetries)
          {
            trace.TraceData(TraceEventType.Warning, 0, "url:" + sURL);
            stats.GetIndividual.Print();
            trace.TraceData(TraceEventType.Warning, 0, "Other Exception: " + e.ToString());
          }
          failure = true;

          Thread.Sleep(delayTime);

          delayTime = delayTime * 2;
        }
        if ((returnLine != null) && ((returnLine.StartsWith("<!DOCTYPE") || returnLine.StartsWith("<HTML") || returnLine.StartsWith("<html"))))
        {
          trace.TraceData(TraceEventType.Warning, 0, requestDescription + ":Bad response format. Don't parse.");
          trace.TraceData(TraceEventType.Warning, 0, "**********************************************************-start");
          trace.TraceData(TraceEventType.Warning, 0, "{0}:{1}", returnLine.Length, returnLine);
          trace.TraceData(TraceEventType.Warning, 0, "**********************************************************-end");
          stats.GetIndividual.failureRetry++;
          trace.TraceData(TraceEventType.Warning, 0, requestDescription + " Format FAILURE " + retryCount + "/" + numberOfRetries + " " + sURL);
          failure = true;
          Thread.Sleep(10000);
        }
      } while (failure && (retryCount++ < numberOfRetries));

      if (returnLine == null)
      {
        trace.TraceData(TraceEventType.Error, 0, requestDescription + " Failed to receive any valid response from the server despite " + numberOfRetries + " retries!");
        return null;
      }

      if ((returnLine.StartsWith("<!DOCTYPE") || returnLine.StartsWith("<HTML") || returnLine.StartsWith("<html")))
      {
        trace.TraceData(TraceEventType.Warning, 0, requestDescription + ":Bad response format. Don't parse.");
        trace.TraceData(TraceEventType.Warning, 0, "**********************************************************-start");
        trace.TraceData(TraceEventType.Warning, 0, "{0}:{1}", returnLine.Length, returnLine);
        trace.TraceData(TraceEventType.Warning, 0, "**********************************************************-end");
        CheckAuthentication();
        return null;
      }
      if (trace.Switch.Level.HasFlag(SourceLevels.Information))
      {
        trace.TraceInformation("**********************************************************-start:");
        trace.TraceInformation("{0}:{1}", returnLine.Length, returnLine);
        trace.TraceInformation("**********************************************************-end:");
      }

      return returnLine;
    }

    private string FetchRootPerson()
    {
      string sLine = null;
      DateTime startTime = DateTime.Now;

      trace.TraceInformation("FetchRootPerson()");

      sLine = GetWebData("https://www.geni.com/api/user/max-family", null, "FetchRootPerson()", MaxRetryCount);

      if (sLine != null)
      {
        HttpMaxFamilyResponse maxFamilyResponse = JsonSerializer.Deserialize<HttpMaxFamilyResponse>(sLine);

        foreach (HttpPerson person in maxFamilyResponse.results)
        {
          if (person.id != null)
          {
            trace.TraceInformation("FetchRootPerson() = " + person.id + " " + (DateTime.Now - startTime) + "s");
            return person.id;
          }
        }
      }
      else
      {
        trace.TraceData(TraceEventType.Error, 0, "FetchRootPerson() FAILED due to server problems (no data returned)! " + " " + (DateTime.Now - startTime) + "s");
      }
      return null;
    }

    private IndividualClass TransformRecordToIndividual(DataRow personRow)
    {
      return null;
    }
    public void SetHomeIndividual(String xrefName)
    {
      homePerson = xrefName;
    }
    public string GetHomeIndividual()
    {
      return homePerson;
    }

    public void AddFamily(FamilyClass tempFamily)
    {
    }

    private enum GeniWebResultType
    {
      Ok,
      OkTooFast,
      FailedRetry,
      FailedRetrySimple,
      FailedReauthenticationNeeded
    }
    private GeniWebResultType ClassifyWebResponse(WebResponse response)
    {
      if (response != null)
      {
        if (response.Headers["X-API-Rate-Remaining"] != null)
        {
          if (Convert.ToInt32(response.Headers["X-API-Rate-Remaining"]) < 5)
          {
            return GeniWebResultType.OkTooFast;
          }
        }
      }
      else
      {
        return GeniWebResultType.FailedRetry;
      }
      return GeniWebResultType.Ok;
    }

    private GeniWebResultType ClassifyHttpResponse(HttpResponseMessage response)
    {
      if (response != null)
      {
        IEnumerator<string> rateRemaining = response.Content.Headers.GetValues("X-API-Rate-Remaining").GetEnumerator();
        if (rateRemaining.MoveNext())
        {
          if (Convert.ToInt32(rateRemaining.Current) < 5)
          {
            rateRemaining.Dispose();
            return GeniWebResultType.OkTooFast;
          }
          rateRemaining.Dispose();
        }
      }
      return GeniWebResultType.Ok;
    }

    private int GetHttpResponseStatus(HttpWebResponse response)
    {
      if (response != null)
      {
        trace.TraceData(TraceEventType.Information, 0, "Exception url: " + response.ResponseUri);
        if ((response.Headers != null) && (response.Headers["Status"] != null))
        {
          trace.TraceData(TraceEventType.Information, 0, "Http response: " + response.Headers["Status"].ToString());
        }
        int rspStatus = (int)response.StatusCode;

        trace.TraceData(TraceEventType.Information, 0, "Result code:" + rspStatus);
        return rspStatus;
      }
      return -3;
    }

    private GeniWebResultType ClassifyErrorWebResponse(int httpResponse)
    {
      GeniWebResultType result = GeniWebResultType.Ok;

      switch(httpResponse)
      {
        case 401:
          result = GeniWebResultType.FailedReauthenticationNeeded;
          break;
        case 403:
        case 404:
          result = GeniWebResultType.FailedRetrySimple;
          break;
        default:
          result = GeniWebResultType.FailedRetry;
          break;
      }
      return result;
    }

    private string GetXref(string longXref, string type)
    {
      int startPos = longXref.IndexOf(type);
      if (startPos < 0)
      {
        trace.TraceData(TraceEventType.Warning, 0, "Error in xref:" + type + ":" + longXref);
        return "error" + longXref;
      }
      return longXref.Substring(startPos);
    }


    public FamilyClass GetFamily(String familyXrefName)
    {
      stats.GetFamily.attempt++;
      DateTime startTime = DateTime.Now;
      if (familyXrefName == null)
      {
        trace.TraceData(TraceEventType.Warning, 0, "GetFamily(null) = ");
        stats.GetFamily.failure++;
        stats.GetFamily.Print();
        return null;
      }
      if (familyXrefName.IndexOf("union-") < 0)
      {
        trace.TraceData(TraceEventType.Warning, 0, "Warning: strange xref in GetFamily" + familyXrefName);
      }
      if (cache.CheckFamily(familyXrefName))
      {
        trace.TraceInformation("GetFamily(" + familyXrefName + ") cached");
        return cache.GetFamily(familyXrefName);
      }
      trace.TraceInformation("GetFamily(" + familyXrefName + ") start " + startTime);

      string sLine = GetWebData("https://www.geni.com/api/" + familyXrefName, null, "GetFamily " + familyXrefName, MaxRetryCount);
      if (sLine != null)
      {
        HttpFamilyResponse familyResponse = JsonSerializer.Deserialize<HttpFamilyResponse>(sLine);

        if (familyResponse.id != null)
        {
          FamilyClass family = new FamilyClass();
          // Ignore "union-"
          family.SetXrefName(familyResponse.id);
          if (familyResponse.marriage != null)
          {
            FamilyDateTimeClass date = null;
            if (familyResponse.marriage.date != null)
            {
              date = new FamilyDateTimeClass(familyResponse.marriage.date.year, familyResponse.marriage.date.month, familyResponse.marriage.date.day);
            }
            family.AddEvent(new IndividualEventClass(IndividualEventClass.EventType.FamMarriage, date));
          }
          if (familyResponse.divorce != null)
          {
            FamilyDateTimeClass date = null;
            if (familyResponse.divorce.date != null)
            {
              date = new FamilyDateTimeClass(familyResponse.divorce.date.year, familyResponse.divorce.date.month, familyResponse.divorce.date.day);
            }
            family.AddEvent(new IndividualEventClass(IndividualEventClass.EventType.FamDivorce, date));
          }
          if (familyResponse.partners != null)
          {
            foreach (string partner in familyResponse.partners)
            {
              family.AddRelation(new IndividualXrefClass(GetXref(partner, "profile-")), FamilyClass.RelationType.Parent);
            }
          }
          IList<string> fosterChildren = new List<string>();
          IList<string> adoptedChildren = new List<string>();
          if (familyResponse.adopted_children != null)
          {
            foreach (string child in familyResponse.adopted_children)
            {
              adoptedChildren.Add(GetXref(child, "profile-"));
            }
          }
          if (familyResponse.foster_children != null)
          {
            foreach (string child in familyResponse.foster_children)
            {
              fosterChildren.Add(GetXref(child, "profile-"));
            }
          }
          if (familyResponse.children != null)
          {
            foreach (string child in familyResponse.children)
            {
              if (adoptedChildren.Contains(GetXref(child, "profile-")))
              {
                family.AddRelation(new IndividualXrefClass(GetXref(child, "profile-"), PedigreeType.Adopted), FamilyClass.RelationType.Child);
              }
              else if (fosterChildren.Contains(GetXref(child, "profile-")))
              {
                family.AddRelation(new IndividualXrefClass(GetXref(child, "profile-"), PedigreeType.Foster), FamilyClass.RelationType.Child);
              }
              else
              {
                family.AddRelation(new IndividualXrefClass(GetXref(child, "profile-")), FamilyClass.RelationType.Child);
              }
            }
          }
          if (family.GetXrefName() != "")
          {
            trace.TraceInformation("GetFamily(" + familyXrefName + ") done " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            cache.AddFamily(family);

            stats.GetFamily.fetchSuccess++;

            if (familyXrefName != family.GetXrefName())
            {
              trace.TraceData(TraceEventType.Error, 0, "GetFamily() Error:wrong family returned:" + familyXrefName + "!=" + family.GetXrefName());
              trace.TraceData(TraceEventType.Error, 0, "Request:" + "https://www.geni.com/api/" + familyXrefName);
              trace.TraceData(TraceEventType.Error, 0, "Response:" + sLine);
            }

            {
              TimeSpan deltaTime = DateTime.Now - startTime;
              if (deltaTime > stats.GetFamily.slowestFetch)
              {
                stats.GetFamily.slowestFetch = deltaTime;
                trace.TraceInformation("GetFamily() slowest " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + deltaTime);
                stats.GetFamily.Print();
              }
            }
            return family;
          }
          else
          {
            trace.TraceEvent(TraceEventType.Error, 0, "GetFamily() FAILURE  (no xref returned) no data in result:" + sLine);
          }
        }
        else
        {
          trace.TraceEvent(TraceEventType.Error, 0, "GetFamily() FAILURE  (no data returned) no data in result:" + sLine);
        }
      }
      else
      {
        trace.TraceEvent(TraceEventType.Error, 0, "GetFamily() FAILURE: no data returned from server:" + sLine);
      }
      //Console.ReadLine();

      trace.TraceInformation("GetFamily(" + familyXrefName + ") = null" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
      stats.GetFamily.failure++;
      stats.GetFamily.Print();

      {
        TimeSpan deltaTime = DateTime.Now - startTime;

        if (deltaTime > stats.GetFamily.slowestFetch)
        {
          stats.GetFamily.slowestFetch = deltaTime;
          trace.TraceInformation("GetFamily() slowest " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + deltaTime);
          stats.GetFamily.Print();
        }
      }

      trace.TraceEvent(TraceEventType.Error, 0, "GetFamily() FAILURE: return null:");
      return null;
    }

    public bool AddIndividual(IndividualClass tempIndividual)
    {
      return false;
    }
    public bool UpdateIndividual(IndividualClass tempIndividual, PersonUpdateType updateType)
    {
      return false;
    }

    private string GetUrlToken(string url, string token)
    {
      if (url != null)
      {
        //trace.TraceInformation("FindUrlToken:" + url + " " + token);

        int tokenPos = url.IndexOf(token);
        if (tokenPos >= 0)
        {
          string tStr = url.Substring(tokenPos + token.Length);

          int endPos = tStr.IndexOf('&');
          if (endPos >= 0)
          {
            return tStr.Substring(0, endPos);
          }
          return tStr;
        }
        trace.TraceInformation("GetUrlToken(" + url + " " + token + ") not found!");
        return null;
      }
      //trace.TraceInformation("FindUrlToken:null " + token);
      trace.TraceInformation("GetUrlToken(null," + token + ") not found!");
      return null;

    }


    public bool CallbackArmed()
    {
      return completedCallback != null;
    }

    private bool AuthenticateApp()
    {
      DateTime startTime = DateTime.Now;
      string sLine = null;
      trace.TraceData(TraceEventType.Warning, 0, "AuthenticateApp()");

      if (appAuthentication.HasRefreshToken())
      {
        // https://www.geni.com/platform/oauth/request_token?client_id=YOUR_APP_ID&redirect_uri=YOUR_URL&client_secret=YOUR_APP_SECRET&grant_type=refresh_token&refresh_token=REFRESH_TOKEN

        string refreshUrl = "https://www.geni.com/platform/oauth/request_token?client_id=" + appAuthentication.GetClientId() + "&redirect_uri=" +
           HttpUtility.UrlEncode("https://improveyourtree.com/FamilyTree/Geni/LoginOk") + "&client_secret=" + appAuthentication.GetClientSecret() + "&grant_type=refresh_token&refresh_token=" + appAuthentication.GetRefreshToken();

        trace.TraceData(TraceEventType.Warning, 0, "AuthenticateApp() refreshurl:" + refreshUrl);

        sLine = GetWebData(refreshUrl, null, "AuthenticateApp(refresh)", MaxRetryCount);
        trace.TraceData(TraceEventType.Warning, 0, "AuthenticateApp() sLine:[" + sLine + "]");
      }
      else
      {
        string requestUrl = "https://www.geni.com/platform/oauth/request_token?client_id=" + appAuthentication.GetClientId() + "&client_secret=" + appAuthentication.GetClientSecret();
        trace.TraceData(TraceEventType.Warning, 0, "AuthenticateApp() request:" + requestUrl);
        sLine = GetWebData(requestUrl, null, "AuthenticateApp(request)", MaxRetryCount);
      }
      if (sLine != null)
      {
        HttpAuthenticateResponse authenticationResponse = JsonSerializer.Deserialize<HttpAuthenticateResponse>(sLine);

        if ((authenticationResponse != null) && (authenticationResponse.access_token != null))
        {
          appAuthentication.UpdateAuthenticationData(authenticationResponse.access_token, authenticationResponse.refresh_token, Convert.ToInt32(authenticationResponse.expires_in), DateTime.Now, true);
          httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Uri.EscapeDataString(appAuthentication.GetAccessToken()));

          //StartAuthenticationTimer();
          trace.TraceData(TraceEventType.Warning, 0, "AuthenticateApp() Done access_token:" + authenticationResponse.access_token + " expires_in:" + authenticationResponse.expires_in + " refresh:" + authenticationResponse.refresh_token + " " + (DateTime.Now - startTime) + "s");
          return true;
        }
        else
        {
          trace.TraceData(TraceEventType.Error, 0, "AuthenticateApp() failed!");
        }
      }
      else
      {
        trace.TraceData(TraceEventType.Error, 0, "AuthenticateApp() FAILED due to server problems (no data returned)!" + " " + (DateTime.Now - startTime) + "s");
      }
      return false;
    }

    private void GetTreeStats()
    {
      DateTime startTime = DateTime.Now;
      trace.TraceInformation("GetTreeStats() " + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

      string sLine = GetWebData("https://www.geni.com/api/stats/world-family-tree", null, "GetTreeStats()", MaxRetryCount);

      if (sLine != null)
      {
        geniTreeSize = JsonSerializer.Deserialize<HttpGeniTreeSize>(sLine);

        trace.TraceInformation("GetTreeStats() OK in " + (DateTime.Now - startTime) + "s");
      }
      else
      {
        trace.TraceData(TraceEventType.Error, 0, "GetTreeStats() FAILED due to server problems (no data returned)!" + " " + (DateTime.Now - startTime) + "s");
      }
    }

    private void GetPhotos(string xrefName, ref IndividualClass person)
    {
      DateTime startTime = DateTime.Now;
      trace.TraceInformation("GetPhotos() " + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

      string photoUrl = "https://www.geni.com/api/" + xrefName + "/photos";

      string sLine = GetWebData(photoUrl, null, "GetPhotos()" + xrefName, 0);

      HttpPhotoRootObject photoObject = null;

      if (sLine != null)
      {
        try
        {
          photoObject = JsonSerializer.Deserialize<HttpPhotoRootObject>(sLine);
        }
        catch (JsonException ex)
        {
          trace.TraceData(TraceEventType.Error, 0, "DeserializeObject<HttpPhotoRootObject> failed\n " + sLine + "\n " + ex.ToString());
        }

        if ((photoObject != null) && (photoObject.results != null))
        {
          foreach(HttpPhotoResult photo in photoObject.results)
          {
            if(photo.sizes != null)
            {
              DecodePhotos(ref person, photo.sizes);
            }
          }
        }
        trace.TraceInformation("GetPhotos() OK in " + (DateTime.Now - startTime) + "s");
      }
      else
      {
        trace.TraceData(TraceEventType.Error, 0, "GetPhotos() FAILED due to server problems (no data returned)!" + " " + (DateTime.Now - startTime) + "s");
      }
    }

    private bool MapPlaceValidPosition(HttpLocation location)
    {
      if ((location.latitude != 0.0) || (location.longitude != 0.0))
      {
        return (location.latitude >= -400.0) && (location.latitude <= 400.0) && (location.longitude >= -400.0) && (location.longitude <= 400.0);
      }
      return false;
    }

    private MapPosition TranslateLocationToMapPlace(HttpLocation location)
    {
      if (MapPlaceValidPosition(location))
      {
        return new MapPosition(location.longitude, location.longitude);
      }
      return null;
    }


    private AddressClass TranslateLocation(HttpLocation location)
    {
      AddressClass address = new AddressClass();

      if (location.place_name != null)
      {
        address.AddAddressPart(AddressPartClass.AddressPartType.Line1, location.place_name);
      }
      if (location.city != null)
      {
        address.AddAddressPart(AddressPartClass.AddressPartType.City, location.city);
      }
      if (location.county != null)
      {
        address.AddAddressPart(AddressPartClass.AddressPartType.State, location.county);
      }
      if (location.country != null)
      {
        address.AddAddressPart(AddressPartClass.AddressPartType.Country, location.country);
      }
      if (address.GetAddressPart(AddressPartClass.AddressPartType.Country) != null)
      {
        if (location.country_code != null)
        {
          address.AddAddressPart(AddressPartClass.AddressPartType.Country, location.country_code);
        }
      }
      if (location.state != null)
      {
        address.AddAddressPart(AddressPartClass.AddressPartType.State, location.state);
      }
      return address;
    }

    private void DecodePhotos(ref IndividualClass individual, HttpPhotoSizes photos)
    {
      bool imageAdded = false;
      if (photos.original != null)
      {
        if (photos.original.Length > 0)
        {
          individual.AddMultimediaLink(CreateMultmediaLink(photos.original));
          imageAdded = true;
        }
      }
      if (photos.large != null)
      {
        if (photos.large.Length > 0)
        {
          individual.AddMultimediaLink(CreateMultmediaLink(photos.large));
          imageAdded = true;
        }
      }
      if (!imageAdded && (photos.medium != null))
      {
        if (photos.medium.Length > 0)
        {
          individual.AddMultimediaLink(CreateMultmediaLink(photos.medium));
          imageAdded = true;
        }
      }
      if (!imageAdded && (photos.thumb != null))
      {
        if (photos.thumb.Length > 0)
        {
          individual.AddMultimediaLink(CreateMultmediaLink(photos.thumb));
          imageAdded = true;
        }
      }
      if (!imageAdded && (photos.thumb2 != null))
      {
        if (photos.thumb2.Length > 0)
        {
          individual.AddMultimediaLink(CreateMultmediaLink(photos.thumb2));
          imageAdded = true;
        }
      }
      if (!imageAdded && (photos.print != null))
      {
        if (photos.print.Length > 0)
        {
          individual.AddMultimediaLink(CreateMultmediaLink(photos.print));
          imageAdded = true;
        }
      }
      if (photos.url != null)
      {
        individual.AddMultimediaLink(new MultimediaLinkClass("text/html", photos.url));
      }
    }


    private IndividualEventClass TranslateEvent(HttpEvent httpEv, IndividualEventClass.EventType type)
    {
      IndividualEventClass ev = new IndividualEventClass(type);

      if (httpEv.date != null)
      {
        FamilyDateTimeClass date = new FamilyDateTimeClass(
          httpEv.date.year,
          httpEv.date.month,
          httpEv.date.day);
        if (httpEv.date.circa)
        {
          date.SetApproximate(true);
        }
        ev.SetDate(date);
      }
      if (httpEv.location != null)
      {
        ev.AddAddress(TranslateLocation(httpEv.location));

        if (MapPlaceValidPosition(httpEv.location))
        {
          PlaceStructureClass place = new PlaceStructureClass();
          place.SetMapPosition(httpEv.location.latitude, httpEv.location.longitude);
          ev.AddPlace(place);
          //trace.TraceData(TraceEventType.Warning, 0, "lat:" + httpEv.location.latitude + " long:" + httpEv.location.longitude);
        }
      }
      return ev;
    }


    private MultimediaLinkClass CreateMultmediaLink(string filename)
    {
      if (filename.IndexOf(".jpg") >= 0)
      {
        return new MultimediaLinkClass("image/jpeg", filename);
      }
      if (filename.IndexOf(".gif") >= 0)
      {
        return new MultimediaLinkClass("image/gif", filename);
      }
      if (filename.IndexOf(".png") >= 0)
      {
        return new MultimediaLinkClass("image/png", filename);
      }
      if (filename.IndexOf(".bmp") >= 0)
      {
        return new MultimediaLinkClass("image/bmp", filename);
      }
      return new MultimediaLinkClass("image/jpeg", filename);
    }

    private IndividualClass DecodeIndividual(HttpPerson person)
    {
      if (person != null)
      {
        IndividualClass individual = new IndividualClass();
        PersonalNameClass name = new PersonalNameClass();

        individual.SetPublic(person.@public);
        if (person.id != null)
        {
          individual.SetXrefName(person.id);
        }
        if (person.merged_into != null)
        {
          //individual.SetXrefName(person.id);
          trace.TraceEvent(TraceEventType.Warning, 0, person.display_name + " merged into " + person.merged_into + " del " + person.deleted);
        }
        if (person.deleted)
        {
          //individual.SetXrefName(person.id);
          trace.TraceEvent(TraceEventType.Warning, 0, person.display_name + " deleted " + person.deleted);
        }
        if (person.first_name != null)
        {
          name.SetName(PersonalNameClass.PartialNameType.GivenName, person.first_name);
        }
        if (person.middle_name != null)
        {
          name.SetName(PersonalNameClass.PartialNameType.MiddleName, person.middle_name);
        }
        if (person.last_name != null)
        {
          name.SetName(PersonalNameClass.PartialNameType.Surname, person.last_name);
        }
        if (person.maiden_name != null)
        {
          name.SetName(PersonalNameClass.PartialNameType.BirthSurname, person.maiden_name);
        }
        if (person.display_name != null)
        {
          name.SetName(PersonalNameClass.PartialNameType.PublicName, person.display_name);
        }
        if (person.name != null)
        {
          if (name.GetName().Length < 2)
          {
            name.SetName(PersonalNameClass.PartialNameType.PublicName, person.name);
          }
        }

        name.SanityCheck();

        individual.SetPersonalName(name);
        if (person.gender != null)
        {
          switch (person.gender)
          {
            case "male":
              individual.SetSex(IndividualClass.IndividualSexType.Male);
              break;
            case "female":
              individual.SetSex(IndividualClass.IndividualSexType.Female);
              break;
            default:
              individual.SetSex(IndividualClass.IndividualSexType.Unknown);
              trace.TraceData(TraceEventType.Warning, 0, "Bad sex  " + individual.GetXrefName() + " " + person.gender);
              break;
          }
        }
        individual.SetIsAlive(person.is_alive);

        if (person.birth != null)
        {
          IndividualEventClass ev = TranslateEvent(person.birth, IndividualEventClass.EventType.Birth);

          //CheckBadEventDate(ev, individual);
          individual.AddEvent(ev);
        }
        if (person.baptism != null)
        {
          IndividualEventClass ev = TranslateEvent(person.baptism, IndividualEventClass.EventType.Baptism);
          individual.AddEvent(ev);
        }

        if (person.death != null)
        {
          IndividualEventClass ev = TranslateEvent(person.death, IndividualEventClass.EventType.Death);
          individual.AddEvent(ev);
        }
        if (person.burial != null)
        {
          IndividualEventClass ev = TranslateEvent(person.burial, IndividualEventClass.EventType.Burial);
          individual.AddEvent(ev);
        }

        if (person.about_me != null)
        {
          individual.AddNote(new NoteClass(person.about_me.Replace("\n", "\r\n")));
        }
        if (person.current_residence != null)
        {
          individual.AddAddress(TranslateLocation(person.current_residence));
        }
        if (person.profile_url != null)
        {
          individual.AddUrl(person.profile_url);
        }
        if (person.mugshot_urls != null)
        {
          DecodePhotos(ref individual, person.mugshot_urls);
          /* More photos can be fetched using https://www.geni.com/api/profile-122248213/photos */
        }
        if (!UpdateRelationsFromEdges(person.edges, ref individual))
        {
          trace.TraceInformation(" relations " + individual.GetXrefName() + " no relation updates..");
        }
        //GetPhotos(individual.GetXrefName(), ref individual);

        return individual;
      }

      return null;
    }

    bool UpdateRelations(IDictionary<string, HttpPerson> personList, ref IndividualClass individual)
    {
      bool updated = false;

      foreach (KeyValuePair<string, HttpPerson> nodePersonPair in personList)
      {
        if ((nodePersonPair.Key.IndexOf("profile-") == 0) && (nodePersonPair.Key == individual.GetXrefName()))
        {
          if (nodePersonPair.Value != null)
          {
            HttpPerson nodePerson = (HttpPerson)nodePersonPair.Value;

            if (!UpdateRelationsFromEdges(nodePerson.edges, ref individual))
            {
              trace.TraceInformation(" Added " + individual.GetXrefName() + " no relation updates..");
            }
          }
        }
      }
      return updated;
    }

    bool UpdateRelationsFromEdges(IDictionary<string, HttpUnionRelation> edgeList, ref IndividualClass individual)
    {
      bool updated = false;

      if (edgeList != null)
      {
        foreach (KeyValuePair<string, HttpUnionRelation> edgePair in edgeList)
        {
          if (edgePair.Value != null)
          {
            HttpUnionRelation union = edgePair.Value;

            if (union.rel == "child")
            {
              individual.AddRelation(new FamilyXrefClass(edgePair.Key), IndividualClass.RelationType.Child);
              updated = true;
            }
            else if (union.rel == "partner")
            {
              individual.AddRelation(new FamilyXrefClass(edgePair.Key), IndividualClass.RelationType.Spouse);
              updated = true;
            }
          }
        }
      }
      if (!updated)
      {
        if (trace.Switch.Level.HasFlag(SourceLevels.Information))
        {
          if (edgeList == null)
          {
            trace.TraceInformation("edgelist = null");
          }
          else
          {
            trace.TraceInformation("edgelist = " + edgeList + " " + edgeList.Count);
          }
        }
      }
      return updated;
    }



    public IndividualClass GetIndividual(String xrefName, uint index = (uint)SelectIndex.NoIndex, PersonDetail detailLevel = PersonDetail.All)
    {
      DateTime startTime = DateTime.Now;
      stats.GetIndividual.attempt++;

      if (xrefName != null)
      {
        if (xrefName.IndexOf("profile-") < 0)
        {
          trace.TraceData(TraceEventType.Warning, 0, "Warning: strange xref name in GetIndividual" + xrefName);
        }
        if (cache.CheckIndividual(xrefName))
        {
          trace.TraceInformation("GetIndividual(" + xrefName + ") cached");
          stats.GetIndividual.cacheSuccess++;
          IndividualClass person = cache.GetIndividual(xrefName);

          if(person.GetXrefName() != xrefName)
          {
            trace.TraceData(TraceEventType.Error, 0, "Wrong person in cache!" + xrefName + " != " + person.GetXrefName());
          }
          return person;
        }
      }
      trace.TraceInformation("GetIndividual(" + xrefName + ") start " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

      if ((xrefName == null) || (xrefName.Length == 0))
      {
        xrefName = FetchRootPerson();
        trace.TraceInformation("GetIndividual(null==root) :" + xrefName);
      }

      string sLine;
      string getPersonUrl;
      if (appAuthentication.IsValid())
      {
        getPersonUrl = "https://www.geni.com/api/" + xrefName + "/immediate-family?only_ids=true&fields=first_name,middle_name,nicknames,last_name,maiden_name,name,suffix,occupation,gender,birth,baptism,death,burial,cause_of_death,unions,id,about_me,is_alive,profile_url,mugshot_urls,public";
        sLine = GetWebData(getPersonUrl, "https://www.geni.com/api/" + xrefName, "GetIndividual(" + xrefName + ")", MaxRetryCount);
      }
      else
      {
        getPersonUrl = "https://www.geni.com/api/" + xrefName;
        sLine = GetWebData(getPersonUrl, null, "GetIndividual-simple()" + xrefName, MaxRetryCount);
      }


      if (sLine != null)
      {
        IndividualClass focusPerson = null;

        if (sLine.IndexOf("{\"focus\"") == 0)
        {
          HttpGetIndividualResult getIndividualResult = null;
          try
          {
            getIndividualResult = JsonSerializer.Deserialize<HttpGetIndividualResult>(sLine);
          }
          catch(JsonException ex)
          {
            trace.TraceData(TraceEventType.Error, 0, "DeserializeObject<HttpGetIndividualResult> failed\n " + sLine + "\n " + ex.ToString());
          }
          if ((getIndividualResult != null) && (getIndividualResult.focus != null))
          {
            if (getIndividualResult.focus.id != xrefName)
            {
              trace.TraceData(TraceEventType.Warning, 0, "xrefName on focus person not as expected. Response updated: " + xrefName + " => " + getIndividualResult.focus.id);
              xrefName = getIndividualResult.focus.id;
            }

            foreach (KeyValuePair<string, HttpPerson> nodePersonPair in getIndividualResult.nodes)
            {
              if (nodePersonPair.Key.IndexOf("profile-") == 0)
              {
                if (!cache.CheckIndividual(nodePersonPair.Key))
                {
                  if (nodePersonPair.Value != null)
                  {
                    HttpPerson nodePerson = (HttpPerson)nodePersonPair.Value;

                    IndividualClass nodeIndividual = DecodeIndividual(nodePerson);

                    if (nodeIndividual != null)
                    {
                      cache.AddIndividual(nodeIndividual);
                    }
                  }
                }
                else
                {
                  trace.TraceInformation(" Person " + nodePersonPair.Key + " skipped, already cached");
                }
              }
              else if (nodePersonPair.Key.IndexOf("union-") == 0)
              {
                FamilyClass family = new FamilyClass();

                family.SetXrefName(nodePersonPair.Key);

                if (!cache.CheckFamily(family.GetXrefName()))
                {
                  if (nodePersonPair.Value != null)
                  {
                    HttpPerson nodePersonUnion = (HttpPerson)nodePersonPair.Value;

                    foreach (KeyValuePair<string, HttpUnionRelation> edgePair in nodePersonUnion.edges)
                    {
                      if (edgePair.Value != null)
                      {
                        HttpUnionRelation union = edgePair.Value;

                        if (union.rel == "child")
                        {
                          family.AddRelation(new IndividualXrefClass(edgePair.Key), FamilyClass.RelationType.Child);
                          trace.TraceInformation("  added child " + edgePair.Key);
                        }
                        else if (union.rel == "partner")
                        {
                          family.AddRelation(new IndividualXrefClass(edgePair.Key), FamilyClass.RelationType.Parent);
                          trace.TraceInformation("  added partner " + edgePair.Key);
                        }
                        // cache
                      }
                    }
                  }

                  if (!cache.CheckFamily(family.GetXrefName()))
                  {
                    //family.Print();

                    // No marriage or divorce info this way, so fetch that as well.
                    FamilyClass family2 = GetFamily(family.GetXrefName());

                    if (family2 != null)
                    {
                      //family2.Print();
                      cache.AddFamily(family);
                    }
                    else
                    {
                      cache.AddFamily(family);
                    }
                  }
                }
              }
            }
            if(!cache.CheckIndividual(xrefName))
            {
              if (getIndividualResult.focus != null)
              {
                trace.TraceData(TraceEventType.Warning, 0, "Warning!! focus person " + xrefName + " not found in nodes!!");
                /*trace.TraceData(TraceEventType.Error, 0, "request!" + getPersonUrl);
                trace.TraceData(TraceEventType.Error, 0, "returned!" + sLine);
                if (!cache.CheckIndividual(getIndividualResult.focus.id))
                {
                  focusPerson = DecodeIndividual(getIndividualResult.focus);
                  trace.TraceData(TraceEventType.Information, 0, "Setting focus person !" + xrefName + ", " + focusPerson.GetXrefName());

                  // For some reason there is no "edges" section within "focus"...
                  if (!UpdateRelations(getIndividualResult.nodes, ref focusPerson))
                  {
                    trace.TraceInformation("focusperson added " + focusPerson.GetXrefName() + " no relation updates..");
                  }
                  cache.AddIndividual(focusPerson);
                }*/
              }

            }
          }
        }
        else
        {
          HttpPerson individualResult = JsonSerializer.Deserialize<HttpPerson>(sLine);
          if (individualResult != null)
          {
            cache.AddIndividual(DecodeIndividual(individualResult));
          }
          else
          {
            trace.TraceData(TraceEventType.Error, 0, "Error: deserializing data " + xrefName + " " + sLine);
          }
        }
        if (cache.CheckIndividual(xrefName))
        {
          focusPerson = cache.GetIndividual(xrefName);
        }
        else
        {
          trace.TraceData(TraceEventType.Error, 0, "Error: Requesed person " + xrefName + " not found!");
        }

        if(focusPerson == null)
        {
          trace.TraceData(TraceEventType.Error, 0, "Focus person not found!" + xrefName);
          trace.TraceData(TraceEventType.Error, 0, "request!" + getPersonUrl);
          trace.TraceData(TraceEventType.Error, 0, "returned!" + sLine);
        }
        else if (focusPerson.GetXrefName() != xrefName)
        {
          trace.TraceData(TraceEventType.Error, 0, "Wrong person returned!" + xrefName + " != " + focusPerson.GetXrefName());
          trace.TraceData(TraceEventType.Error, 0, "request!" + getPersonUrl);
          trace.TraceData(TraceEventType.Error, 0, "returned!" + sLine);
        }
        trace.TraceInformation("GetIndividual() done " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        stats.GetIndividual.fetchSuccess++;
        TimeSpan deltaTime = DateTime.Now - startTime;

        if (deltaTime > stats.GetIndividual.slowestFetch)
        {
          stats.GetIndividual.slowestFetch = deltaTime;
          trace.TraceInformation("GetIndividual() slowest " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + deltaTime);
          stats.GetIndividual.Print();

        }
        return focusPerson;
        
      }
      else
      {
        trace.TraceData(TraceEventType.Error, 0, "GetIndividual() FAILED! due to server problems (no data returned) in " + (DateTime.Now - startTime) + "s");
      }

      stats.GetIndividual.failure++;
      stats.GetIndividual.Print();
      return null;
    }

    public IEnumerator<IndividualClass> SearchPerson(String individualName, IProgressReporterInterface progressReporter = null)
    {
      DateTime startTime = DateTime.Now;

      stats.SearchIndividual.attempt++;
      trace.TraceInformation("SearchPerson(" + individualName + ") " + startTime);
      if ((individualName == null) || (individualName.Length == 0))
      {
        IEnumerator<IndividualClass> personIterator = cache.GetIndividualIterator();

        if (personIterator != null)
        {
          IList<IndividualClass> personList = new List<IndividualClass>();
          int index = 0;
          while (personIterator.MoveNext())
          {
            trace.TraceInformation("SearchPerson():add from cache " + personIterator.Current);
            personList.Add(personIterator.Current);
            stats.SearchIndividual.fetchSuccess++;
          }

          foreach(IndividualClass person in personList)
          {
            if(progressReporter != null)
            {
              progressReporter.ReportProgress(100.0 * (double)index / (double)personList.Count, "Checking " + index + " / " + personList.Count);
            }
            index++;
            yield return person;
          }
          trace.TraceInformation("SearchPerson():done " + (DateTime.Now - startTime) + "s");
        }
      }
      else
      {
        //CheckAuthentication();

        string searchPersonUrl = "https://www.geni.com/api/profile/search?names=" + individualName + "&&fields=first_name,middle_name,nicknames,last_name,maiden_name,name,suffix,occupation,gender,birth,baptism,death,burial,cause_of_death,id,about_me,is_alive,mugshot_urls,public";
        string sLine = GetWebData(searchPersonUrl, null, "SearchIndividual()" + individualName, MaxRetryCount);
        if (sLine != null)
        {
          int handledResults = 0;
          do
          {
            HttpSearchPersonResult searchPersonResult = JsonSerializer.Deserialize<HttpSearchPersonResult>(sLine);

            if (searchPersonResult != null)
            {
              trace.TraceEvent(TraceEventType.Information, 0, "next page " + searchPersonResult.next_page + " prev " + searchPersonResult.prev_page + " page " + searchPersonResult.page);
              if (searchPersonResult.results != null)
              {
                foreach (HttpPerson person in searchPersonResult.results)
                {
                  stats.SearchIndividual.fetchSuccess++;
                  handledResults++;
                  trace.TraceInformation("SearchPerson():add from web " + person);
                  yield return DecodeIndividual(person);
                }
              }
              else
              {
                HttpPerson onePerson = JsonSerializer.Deserialize<HttpPerson>(sLine);
                yield return DecodeIndividual(onePerson);
              }
              trace.TraceEvent(TraceEventType.Information, 0, " successes " + stats.SearchIndividual.fetchSuccess);
            }
            sLine = null;
            if ((searchPersonResult.next_page != null) && (searchPersonResult.next_page.Length > 0) && (handledResults < MaxProfilesToSearch))
            {
              sLine = GetWebData(searchPersonResult.next_page, null, "SearchIndividual()" + individualName + " page " + (searchPersonResult.page + 1), MaxRetryCount);
            }
          } while (sLine != null);
        }
        else
        {
          trace.TraceData(TraceEventType.Error, 0, "SearchPerson() FAILED  (no data returned) in  " + (DateTime.Now - startTime) + "s");
          yield return null;
        }
      }
    }

    private void UpdateIndividualCache()
    {
      IEnumerator<FamilyClass> familyIterator = cache.GetFamilyIterator();
      List<FamilyClass> familyList = new List<FamilyClass>();

      while (familyIterator.MoveNext())
      {
        familyList.Add(familyIterator.Current);
      }
      foreach (FamilyClass family in familyList)
      {
        IList<IndividualXrefClass> parents = family.GetParentList();

        if (parents != null)
        {
          foreach (IndividualXrefClass personXref in parents)
          {
            if (!cache.CheckIndividual(personXref.GetXrefName()))
            {
              trace.TraceInformation("UpdateIndividualCache() " + personXref.GetXrefName());
              IndividualClass person = GetIndividual(personXref.GetXrefName());
            }
          }
        }
        IList<IndividualXrefClass> children = family.GetChildList();

        if (children != null)
        {
          foreach (IndividualXrefClass personXref in children)
          {
            if (!cache.CheckIndividual(personXref.GetXrefName()))
            {
              trace.TraceInformation("UpdateIndividualCache() " + personXref.GetXrefName());
              IndividualClass person = GetIndividual(personXref.GetXrefName());
            }
          }
        }
      }

    }
    private void UpdateFamilyCache()
    {
      IEnumerator<IndividualClass> individualIterator = cache.GetIndividualIterator();
      List<IndividualClass> individualList = new List<IndividualClass>();

      while (individualIterator.MoveNext())
      {
        individualList.Add(individualIterator.Current);
      }
      foreach (IndividualClass person in individualList)
      {
        IList<FamilyXrefClass> childFamilies = person.GetFamilyChildList();

        if (childFamilies != null)
        {
          foreach (FamilyXrefClass familyXref in childFamilies)
          {
            if (!cache.CheckFamily(familyXref.GetXrefName()))
            {
              trace.TraceInformation("UpdateFamilyCache() " + familyXref.GetXrefName());
              FamilyClass family = GetFamily(familyXref.GetXrefName());
            }
          }
        }
        IList<FamilyXrefClass> spouseFamilies = person.GetFamilySpouseList();
        if (spouseFamilies != null)
        {
          foreach (FamilyXrefClass familyXref in spouseFamilies)
          {
            if (!cache.CheckFamily(familyXref.GetXrefName()))
            {
              trace.TraceInformation("UpdateFamilyCache() " + familyXref.GetXrefName());
              FamilyClass family = GetFamily(familyXref.GetXrefName());
            }
          }
        }
      }

    }
    private void UpdateCaches()
    {
      UpdateIndividualCache();
      UpdateFamilyCache();
    }

    public IEnumerator<FamilyClass> SearchFamily(String familyXrefName = null, IProgressReporterInterface progressReporter = null)
    {
      if (familyXrefName == null)
      {
        IEnumerator<FamilyClass> familyIterator = cache.GetFamilyIterator();

        trace.TraceInformation("SearchFamily():start:");
        if (familyIterator != null)
        {
          List<FamilyClass> familyList = new List<FamilyClass>();
          while (familyIterator.MoveNext())
          {
            trace.TraceInformation("SearchFamily():add:" + familyIterator.Current);
            familyList.Add(familyIterator.Current);
          }
          foreach(FamilyClass family in familyList)
          {
            yield return family;
          }
          trace.TraceInformation("SearchFamily():end:");
        }
      }
    }

    public void AddMultimediaObject(MultimediaObjectClass tempMultimediaObject)
    {
    }

    public IEnumerator<MultimediaObjectClass> SearchMultimediaObject(String mmoString = null, IProgressReporterInterface progressReporter = null)
    {
      return null;
    }

    public void AddNote(NoteClass tempNote)
    {
    }

    public NoteClass GetNote(String xrefName)
    {
      return null;
    }
    public IEnumerator<NoteClass> SearchNote(String noteString = null, IProgressReporterInterface progressReporter = null)
    {
      return null;
    }

    public void AddRepository(RepositoryClass tempRepository)
    {
    }

    public IEnumerator<RepositoryClass> SearchRepository(String repositoryString = null, IProgressReporterInterface progressReporter = null)
    {
      return null;
    }

    public void AddSource(SourceClass tempSource)
    {
    }

    public IEnumerator<SourceClass> SearchSource(String sourceString = null, IProgressReporterInterface progressReporter = null)
    {
      return null;
    }

    public void AddSubmission(SubmissionClass tempSubmission)
    {
    }

    public IEnumerator<SubmissionClass> SearchSubmission(String submissionString = null, IProgressReporterInterface progressReporter = null)
    {
      return null;
    }

    public void AddSubmitter(SubmitterClass tempSubmitter)
    {
    }

    public void SetSubmitterXref(SubmitterXrefClass tempSubmitterXref)
    {
    }
    public IEnumerator<SubmitterClass> SearchSubmitter(String noteString = null, IProgressReporterInterface progressReporter = null)
    {
      return null;
    }

    public void SetSourceFileType(String type)
    {
    }
    public void SetSourceFileTypeVersion(String version)
    {
    }
    public void SetSourceFileTypeFormat(String format)
    {
    }

    public void SetSourceFileName(string file)
    {
      sourceFileName = file;
    }
    public string GetSourceFileName()
    {
      return sourceFileName;
    }
    public void SetSourceName(String source)
    {
    }

    public void SetCharacterSet(FamilyTreeCharacterSet charSet)
    {
    }

    public void SetDate(FamilyDateTimeClass inDate)
    {
    }

    public string CreateNewXref(XrefType type)
    {
      return "";
    }

    public void PrintShort()
    {
      trace.TraceInformation(stats.ToString());
      trace.TraceInformation(GetShortTreeInfo());
    }

    public String GetShortTreeInfo()
    {
      StringBuilder builder = new StringBuilder();
      if (geniTreeSize != null)
      {
        string cacheInfo = "; Cached " + cache.GetIndividualNo() + " individuals and " + cache.GetFamilyNo() + " families...";

        builder.AppendLine("Web tree includes " + geniTreeSize.size + " people" + cacheInfo);
      }
      FamilyTreeContentClass contents = GetContents();
      builder.AppendLine("I:" + contents.individuals + " F:" + contents.families + " N:" + contents.notes);
      builder.AppendLine(stats.ToString());
      return builder.ToString();
    }

    public FamilyTreeContentClass GetContents()
    {
      FamilyTreeContentClass contents = new FamilyTreeContentClass();

      if (geniTreeSize == null)
      {
        GetTreeStats();
      }

      if (geniTreeSize == null)
      {
        contents.families = cache.GetFamilyNo();
        contents.individuals = cache.GetIndividualNo();
      }
      else
      {
        contents.individuals = geniTreeSize.size;
      }
      return contents;
    }

    public void SetFile(String fileName)
    {
      trace.TraceInformation("SetFile("+fileName+"):");
    }

  }
}
