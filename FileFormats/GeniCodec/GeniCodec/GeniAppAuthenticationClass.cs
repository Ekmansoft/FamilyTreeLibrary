using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
//using FamilyTreeWebTools.Services;

namespace FamilyTreeLibrary.FileFormats.GeniCodec
{
  public delegate void GeniAuthenticationUpdateCb(string accessToken, string refreshToken, int expiresIn);
  public class GeniAppAuthenticationClass
  {
    //private HttpAuthenticateResponse response
    private string accessToken;
    private string refreshToken;
    //private string userId;
    private int expiresIn;
    private long useCount;
    //private DateTime receptionTime;
    private DateTime receptionTime;
    private bool forceReauthentication;
    private string clientId;
    private string clientSecret;
    private static TraceSource trace = new TraceSource("GeniAppAuthenticationClass", SourceLevels.Warning);
    private readonly GeniAuthenticationUpdateCb tokenUpdateCallback;

    public GeniAppAuthenticationClass(GeniAuthenticationUpdateCb callback, string tClientId, string tClientSecret)
    {
      receptionTime = DateTime.MinValue;
      useCount = 0;
      expiresIn = 0;
      forceReauthentication = false;
      tokenUpdateCallback = callback;
      clientId = tClientId;
      clientSecret = tClientSecret;
    }


    public string GetClientId()
    {
      //response = null;
      return clientId;
    }

    public string GetClientSecret()
    {
      //response = null;
      return clientSecret;
    }

    public bool UpdateAuthenticationData(string accessToken, string refreshToken, int expiresIn, DateTime authenticationTime, bool saveToDb = false)
    {
      trace.TraceData(TraceEventType.Information, 0, "UpdateAuthenticationData: old access:" + ToString());
      this.expiresIn = expiresIn;
      this.accessToken = accessToken;
      this.refreshToken = refreshToken;
      receptionTime = authenticationTime;
      useCount = 0;
      trace.TraceData(TraceEventType.Information, 0, "UpdateAuthenticationData: new access:" + ToString());
      //this.response = response;
      forceReauthentication = false;

      if (saveToDb)
      {
        //FamilyDbContextClass.UpdateGeniAuthentication(userId, accessToken, refreshToken, expiresIn);
        tokenUpdateCallback?.Invoke(accessToken, refreshToken, expiresIn);
      }
      trace.TraceData(TraceEventType.Information, 0, "Updated geni authentication!");

      return true;
    }
    public bool IsValid()
    {
      if (!String.IsNullOrEmpty(accessToken) && (receptionTime.AddSeconds(expiresIn) >= DateTime.Now))
      {
        return true;
      }
      return false;
    }
    public void ForceReauthentication()
    {
      trace.TraceData(TraceEventType.Warning, 0, "SetInvalid: " + ToString());
      //expiresIn = 0;
      forceReauthentication = true;
      trace.TraceData(TraceEventType.Warning, 0, "SetInvalid-post: " + ToString());
    }
    public bool ForceReauthenticationOngoing()
    {
      return forceReauthentication;
    }
    public bool HasRefreshToken()
    {
      return !String.IsNullOrEmpty(refreshToken);
    }
    public bool TimeToReauthenticate()
    {
      bool value = forceReauthentication || (refreshToken != null) && (GetExpiryTime() < DateTime.Now.AddSeconds(60));
      trace.TraceData(TraceEventType.Information, 0, "TimeToReauthenticate:" + value + " " + ToString());
      return value;
    }
    public string GetRefreshToken()
    {
      return refreshToken;
    }
    public string GetAccessToken()
    {
      useCount++;
      return accessToken;
    }
    public int GetExpiresIn()
    {
      return expiresIn;
    }
    public override string ToString()
    {
      return "received:" + receptionTime.ToString("yyyy-MM-dd HH:mm:ss") +
          " auth:" + accessToken +
          " refresh:" + refreshToken +
          " expiry: " + expiresIn +
          " forceReauth: " + forceReauthentication +
          " useCount: " + useCount +
          " now: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
          " expiry: " + GetExpiryTime().ToString("yyyy-MM-dd HH:mm:ss");

    }
    public long GetUseCount()
    {
      return useCount;
    }
    public DateTime GetExpiryTime()
    {
      if (forceReauthentication)
      {
        return DateTime.Now.AddMinutes(-1);
      }
      return receptionTime.AddSeconds(expiresIn);
    }
  }
}
