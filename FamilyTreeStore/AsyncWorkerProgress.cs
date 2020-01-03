using System;
using System.ComponentModel;
using System.Diagnostics;

namespace FamilyTreeLibrary.FamilyTreeStore
{
  public delegate void WorkProgressHandler(int JobId, int progressPercent, string text = null);
  public delegate bool CheckIfStopRequested(int JobId);

  public interface IAsyncWorkerProgressInterface : IDisposable
  {
    void DoWork(object sender, DoWorkEventArgs e);
    void ProgressChanged(object sender, ProgressChangedEventArgs e);
    void Completed(object sender, RunWorkerCompletedEventArgs e);
  }
  public interface IAsyncWorkerThreadInterface : IDisposable
  {
    void DoWork(object sender, DoWorkEventArgs e);
    //void Completed(object sender, RunWorkerCompletedEventArgs e);
  }

  public interface IProgressReporterInterface
  {
    void ReportProgress(double progressPercent, string progressText = null);
    void Completed(string completedText = null);
    bool CheckIfStopRequested();
  }
  public class AsyncWorkerProgress : IProgressReporterInterface
  {
    private DateTime startTime;
    private double currentProgress;
    private string currentProgressText;
    private static TraceSource trace;
    private int jobId;
    private bool stopRequested = false;

    private WorkProgressHandler progressHandlerFcn;
    private CheckIfStopRequested stopRequestHandlerFcn;

    public AsyncWorkerProgress(int JobId, WorkProgressHandler progressHandler, CheckIfStopRequested stopRequestHandler = null)
    {
      trace = new TraceSource("FamilyFormProgress", SourceLevels.Warning);
      progressHandlerFcn = progressHandler;
      stopRequestHandlerFcn = stopRequestHandler;
      startTime = DateTime.Now;
      currentProgress = 0.0;
      currentProgressText = "";
      this.jobId = JobId;
    }

    public bool CheckIfStopRequested()
    {
      return stopRequested;
    }

    public void ReportProgress(double progressPercent, string progressText = null)
    {
      TimeSpan deltaTime;
      DateTime estimatedEndTime;
      string endTimeString = "";

      if (progressText != null)
      {
        currentProgressText = progressText;
      }
      if (progressPercent < currentProgress)
      {
        trace.TraceInformation("FamilyFormProgress::ReportProgress(" + progressPercent + " < " + currentProgress + ") =>" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " restart!");
        startTime = DateTime.Now;
      }
      deltaTime = DateTime.Now - startTime;
      currentProgress = progressPercent;
      if ((progressPercent > 0.02) && (startTime != DateTime.Now))
      {
        estimatedEndTime = DateTime.Now.AddSeconds((100.0 - progressPercent) * deltaTime.TotalSeconds / progressPercent);
        trace.TraceInformation("FamilyFormProgress::ReportProgress(" + progressPercent + ")" + DateTime.Now + ", elapsed:" + deltaTime.TotalSeconds + ",estimated time in seconds:" + deltaTime.TotalSeconds * 100.0 / progressPercent + ",end:" + estimatedEndTime);
        endTimeString = " Estimated done at " + estimatedEndTime;
      }
      if (progressHandlerFcn != null)
      {
        progressHandlerFcn(this.jobId, (int)progressPercent, currentProgressText + endTimeString);
      }
      if (stopRequestHandlerFcn != null)
      {
        stopRequested = stopRequestHandlerFcn(this.jobId);
      }
    }

    public void Completed(string completedText = null)
    {
      string text = "";

      if (completedText != null)
      {
        text = completedText;
      }
      trace.TraceInformation("FamilyFormProgress::Completed(" + text + ")" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

      if (progressHandlerFcn != null)
      {
        progressHandlerFcn(this.jobId, -1, completedText);
      }
    }

    public override string ToString()
    {
      TimeSpan delta = DateTime.Now.Subtract(startTime);
      return delta.ToString(@"hh\:mm\:ss") + " " + this.jobId + " " + currentProgress.ToString("F2") + "%";
    }
  }

}
