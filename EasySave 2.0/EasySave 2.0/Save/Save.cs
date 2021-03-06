using System;
using System.IO;
using Projet.Logs;
using System.Diagnostics;
using Projet.Languages;
using EasySave_2._0;
using Projet.WorkSoftwares;
using System.Windows.Controls;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading;
using Projet.Priority;
using Projet.Size;

namespace Projet.SaveSystem
{/// <summary>
/// The class that manages the save system
/// </summary>
    public class Save
    {
        private readonly string SourceDir;
        private readonly string TargetDir;
        private readonly bool Full;
        public LogState CurrentStateLog;
        private LogDaily CurrentDailyLog;
        private readonly Stopwatch ProcessTime;
        private long CryptTime;
        private bool FirstProcess = true;
        private string[] PriorityExtensions;
        private int CurrentIndexFolder = -5;
        private int TotalFolders;

        public Save(string source, string target, bool full)
        {
            SourceDir = source;
            TargetDir = target;
            Full = full;
            ProcessTime = new Stopwatch();
        }


        /// <summary>
        /// Fetches basic data like copy type or directory size then call ProcessCopy
        /// </summary>
        public (DirectoryInfo source, DirectoryInfo target, int error) Copy()
        {

            var app = WorkSoftware.GetJsonApplication();
            Process[] pname = Process.GetProcessesByName(app.Application);

            var tempPrio = Priority.Priority.GetJsonPriority();
            PriorityExtensions = new string[tempPrio.Count];
            tempPrio.Values.CopyTo(PriorityExtensions, 0);

            if (pname.Length > 0) return (null, null, 1);
            string copyType = "Partial";
            if (Full) copyType = "Complete";

            DirectoryInfo sourceDirInfo = new DirectoryInfo(SourceDir);
            if (!sourceDirInfo.Exists) return (null, null, 2);
            long filesNumber = Directory.GetFiles(SourceDir, "*", SearchOption.AllDirectories).Length;
            long filesSize = DirSize(sourceDirInfo);
            if (filesSize == 0) return (null, null, 3);
            CurrentStateLog = new LogState(copyType, SourceDir, TargetDir, filesNumber, filesSize);
            CurrentDailyLog = new LogDaily(copyType);
            DirectoryInfo targetDirInfo = new DirectoryInfo(TargetDir);

            TotalFolders = Directory.GetDirectories(SourceDir).Length;

            if (!targetDirInfo.Exists)
            {
                Directory.CreateDirectory(TargetDir);
            }

            return (sourceDirInfo, targetDirInfo, 0);
        }
        /// <summary>
        /// Process the copy, writing logs at the same time
        /// </summary>
        /// <param name="source">Source Directory</param>
        /// <param name="target">Target Directory</param>
        /// 

        public void ProcessCopy(DirectoryInfo source, DirectoryInfo target, ProgressBar progressBar, object sender, DoWorkEventArgs e, int workerId)
        {
            CurrentStateLog.Display();
            long filesSize;
            Directory.CreateDirectory(target.FullName);
            
            foreach (FileInfo fi in source.GetFiles())
            {
                MainWindow.CheckProcesses();
                var autorizedSize = Size.Size.GetJsonSize().Size;
                if(fi.Length > autorizedSize)
                {
                    MainWindow.PauseAllExceptOne(workerId, sender);
                }
                bool ShouldProcess = false;

                foreach (string prio in PriorityExtensions)
                {
                    
                    if (fi.Extension == prio)
                    {
                        ShouldProcess = FirstProcess;
                    }

                }

                if (ShouldProcess == true)
                {
                    ProcessTime.Start();
                    if (Full == false)
                    {
                        if (File.GetLastWriteTime(target.FullName) != File.GetLastWriteTime(fi.FullName))
                        {
                            CheckException(fi, target);
                        }
                    }
                    else
                    {
                        CheckException(fi, target);
                    }
                    //progressBar.Value = CurrentStateLog.Progress;

                    filesSize = fi.Length;
                    ProcessTime.Stop();
                    CurrentDailyLog.Update(filesSize, ProcessTime.ElapsedMilliseconds, fi.Name, target.Name, CryptTime);
                    CurrentStateLog.Update(filesSize);
                    ProcessTime.Reset();

                    if (fi.Length > autorizedSize)
                    {
                    MainWindow.PlayAllExceptOne(workerId);
                    }
                    MainWindow.Workers[workerId - 1].WorkEvent.WaitOne();
                }
                if ((sender as BackgroundWorker).WorkerReportsProgress == true)
                {
                    List<long> param = new List<long>() { CurrentStateLog.RemainingFiles, CurrentStateLog.RemainingFilesSize };
                    (sender as BackgroundWorker).ReportProgress(CurrentStateLog.Progress, param);
                }
                for (int i = 0; i <= 100; i++)
                {
                    if ((sender as BackgroundWorker).CancellationPending == true)
                    {
                        e.Cancel = true;
                        return;
                    }
                    //System.Threading.Thread.Sleep(250);
                }
                //MainWindow.Workers[workerId - 1].WorkEvent.WaitOne();

            }


            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {

                CurrentIndexFolder++;
                DirectoryInfo nextTargetSubDir =
                                target.CreateSubdirectory(diSourceSubDir.Name);
                CurrentStateLog.Save();
                ProcessCopy(diSourceSubDir, nextTargetSubDir, progressBar, sender, e, workerId);

            }
            if (CurrentIndexFolder == TotalFolders)
            {
                if (FirstProcess)
                {
                    FirstProcess = false;
                    DirectoryInfo sourceDirectory = new DirectoryInfo(SourceDir);
                    DirectoryInfo targetDirectory = new DirectoryInfo(TargetDir);
                    ProcessCopy(sourceDirectory, targetDirectory, progressBar, sender, e, workerId);
                }
            }
            //CurrentStateLog.End();

        }
        /// <summary>
        /// Returns directory's size, in bytes
        /// </summary>
        /// <param name="directory">The directory to measure</param>
        /// <returns></returns>

        private static long DirSize(DirectoryInfo directory)
        {
            long size = 0;
            FileInfo[] fis = directory.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            DirectoryInfo[] dis = directory.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += DirSize(di);
            }
            return size;
        }
        private void CheckException(FileInfo source, DirectoryInfo target)
        {
            try
            {
                CryptTime = Crypt.CryptOrSave(source, target);
            }
            catch (IOException)
            {
                CryptTime = -1;
            }
        }
    }
}