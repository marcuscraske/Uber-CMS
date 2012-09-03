/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber Media
 * File:                    /UberMedia/ConversionService.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Responsible as a service for converting media to different formats.
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.IO;

namespace UberCMS.Plugins.UberMedia
{
    public static class ConversionService
    {
        #region "Variables"
        /// <summary>
        /// Used for checking if we need to keep the application-pool alive for this service, when true.
        /// </summary>
        public static bool serviceIsActive = false;
        public static Thread threadDelegator = null;
        public static List<Thread> threads = new List<Thread>();
        public static string[] threadStatus = null;
        public static List<ConversionInfo> queue = new List<ConversionInfo>();
        public static List<Process> processes = new List<Process>();
        public static string status = "Unstarted.";
        #endregion

        #region "Methods - Start/stop"
        public static void startService()
        {
            if (threadDelegator != null) return; // Ensure the service is not already running
            status = "Starting service...";
            lock (threads)
            {
                lock (processes)
                {
                    // Create thread pool
                    int threadCount = Core.settings[Base.SETTINGS_KEY].getInt(Base.SETTINGS_CONVERSION_THREADS);
                    for (int i = 0; i < threadCount; i++)
                        threads.Add(new Thread(processItem));
                    threadStatus = new string[threadCount];
                    status = "Created thread-pool with " + threadCount + " threads...";
                }
            }
            // Create delegator
            threadDelegator = new Thread(conversionDelegate);
            threadDelegator.Start();
        }
        public static void stopService()
        {
            if (threadDelegator == null) return;
            status = "Stopping service..";
            lock (threads)
            {
                lock (processes)
                {
                    // Stop delegator thread
                    threadDelegator.Abort();
                    threadDelegator = null;
                    // Abort/end each thread
                    foreach (Thread th in threads)
                        th.Abort();
                    // End each process started by the thread-pool
                    foreach (Process p in processes)
                        try
                        {
                            p.CloseMainWindow();
                            p.Kill();
                        }
                        catch { }
                    // Clear the lists
                    processes.Clear();
                }
            }
            status = "Stopped successfully at " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + ".";
            threadDelegator = null;
            serviceIsActive = false;
        }
        #endregion

        #region "Methods - Threading"
        public static void conversionDelegate()
        {
            while (true)
            {
                lock (threads)
                {
                    lock (queue)
                    {
                        if (queue.Count > 0)
                        {
                            status = "Attempting to delegate items in the queue... at " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + "...";
                            Thread th;
                            ConversionInfo ci;
                            for (int i = 0; i < threads.Count && queue.Count > 0; i++)
                            {
                                th = threads[i];
                                if (th.ThreadState == System.Threading.ThreadState.Unstarted || th.ThreadState == System.Threading.ThreadState.Stopped || th.ThreadState == System.Threading.ThreadState.Aborted)
                                {
                                    System.Diagnostics.Debug.WriteLine("creating thread - " + th.ThreadState + " - " + queue.Count);
                                    // Ensure it's aborted
                                    try
                                    {
                                        th.Abort();
                                    }
                                    catch { }
                                    // Create and start a new thread
                                    th = new Thread(new ParameterizedThreadStart(processItem));
                                    ci = queue[0];
                                    ci.threadIndex = i;
                                    th.Start(ci);
                                    threads[i] = th;
                                    // Remove the item
                                    queue.RemoveAt(0);
                                }
                            }
                            serviceIsActive = true;
                        }
                        else
                        {
                            // Output status for delegator
                            bool active = false;
                            foreach(Thread th in threads)
                                if (th.ThreadState == System.Threading.ThreadState.Running || th.ThreadState == System.Threading.ThreadState.WaitSleepJoin)
                                {
                                    active = true;
                                    break;
                                }
                            if (!active) status = "Idle; successfully looped at " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + "...";
                            if (serviceIsActive && !active)
                                // State has changed - index all drives
                                Indexer.indexAllDrives();
                            serviceIsActive = active;
                        }
                    }
                }
                Thread.Sleep(400);
            }
        }
        public static void processItem(object data)
        {
            ConversionInfo ci = (ConversionInfo)data;
            if(ci.threadIndex != -1) threadStatus[ci.threadIndex] = "Preparing to convert " + ci.pathSource;
            // Check if the output file exists, if so delete it
            if(File.Exists(ci.pathOutput))
                try
                {
                    File.Delete(ci.pathOutput);
                }
                catch(Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to delete a file: " + ex.Message);
                    return;
                }
            // Build the process to convert the file
            Process proc = new Process();
            lock(processes)
                processes.Add(proc);
            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            proc.StartInfo.FileName = "ffmpeg.exe";
            proc.StartInfo.WorkingDirectory = Core.basePath + @"\Bin";
            // Build arguments
            StringBuilder args = new StringBuilder();
            args.Append("-i \"").Append(ci.pathSource).Append("\"");
            args.Append(" -acodec copy");
            if (ci.videoBitrate != -1) args.Append(" -b ").Append(ci.videoBitrate);
            if (ci.audioBitrate != -1) args.Append(" -ab ").Append(ci.audioBitrate);
            if (ci.audioSampleRate != -1) args.Append(" -ar ").Append(ci.audioSampleRate);
            if (ci.videoResolution.Width != -1 && ci.videoResolution.Height != -1) args.Append(" -s ").Append(ci.videoResolution.Width).Append("x").Append(ci.videoResolution.Height);
            args.Append(" \"").Append(ci.pathOutput).Append("\"");
            proc.StartInfo.Arguments = args.ToString();
            // Start the process and wait for it to exit - unless it surpasses the max-time
            proc.Start();
            DateTime start = DateTime.Now;
            int timeout = Core.settings[Base.SETTINGS_KEY].getInt(Base.SETTINGS_CONVERSION_TIMEOUT);
            while (DateTime.Now.Subtract(start).TotalSeconds < timeout && !proc.HasExited)
            {
                if (ci.threadIndex != -1) threadStatus[ci.threadIndex] = "Converting " + ci.pathSource;
                Thread.Sleep(100); // We'll wait until the process exits or until the thread has ran too long (past the timeout period)
            }
            bool failed = !proc.HasExited;
            // Ensure the process is killed
            try
            {
                proc.Kill();
            }
            catch { }
            // Remove the process from the list
            lock(processes)
                processes.Remove(proc);
            // Verify the new file exists, if so perform action unless the process did not exit (hence failed)
            try
            {
                if (File.Exists(ci.pathOutput))
                {
                    if (!failed)
                    {
                        // Ensure file integrity by checking the new file is not empty
                        FileInfo fi = new FileInfo(ci.pathOutput);
                        if (fi.Length == 0)
                        {
                            // Conversion failed, delete output and take no action
                            File.Delete(ci.pathOutput);
                        }
                        else
                        {
                            switch (ci.actionOriginal)
                            {
                                case ConversionAction.Nothing: break;
                                case ConversionAction.Delete:
                                    try
                                    {
                                        File.Delete(ci.pathSource);
                                    }
                                    catch { }
                                    break;
                                case ConversionAction.Move:
                                    try
                                    {
                                        string filename = Path.GetFileNameWithoutExtension(ci.pathSource);
                                        string ext = Path.GetExtension(ci.pathSource);
                                        if (File.Exists(ci.actionOriginalArgs + "\\" + filename + ext))
                                        {
                                            int incr = 0;
                                            while (File.Exists(ci.actionOriginalArgs + "\\" + filename + " (" + incr + ")" + ext) && incr < int.MaxValue)
                                                incr++;
                                            try
                                            {
                                                File.Move(ci.pathSource, ci.actionOriginalArgs + "\\" + filename + " (" + incr + ")" + ext);
                                            }
                                            catch { }
                                        }
                                        else
                                            File.Move(ci.pathSource, ci.actionOriginalArgs + "\\" + filename + ext);
                                    }
                                    catch { }
                                    break;
                                case ConversionAction.Rename_Extension_With_Bk:
                                    try
                                    {
                                        File.Move(ci.pathSource, ci.pathSource + ".bk");
                                    }
                                    catch { }
                                    break;
                            }
                        }
                    }
                    else
                        // Delete the output file if it exists - most likely incomplete
                        try
                        {
                            File.Delete(ci.pathOutput);
                        }
                        catch { }
                }
            }
            catch { }
            // Run the indexer for the original drive again
            Indexer.indexDrive(ci.phy_pfolderid, ci.phy_path, ci.phy_allowsynopsis);
            // Null status (to idle)
            if (ci.threadIndex != -1) threadStatus[ci.threadIndex] = null;
        }
        #endregion
    }
    public class ConversionInfo
    {
        public string phy_pfolderid = null;
        public bool phy_allowsynopsis = false;
        public string phy_path = null;
        public string pathSource = null;
        public string pathOutput = null;
        public int videoBitrate = -1;
        public int audioBitrate = -1;
        public int audioSampleRate = -1;
        public Size videoResolution = new Size(-1, -1);
        public ConversionAction actionOriginal = ConversionAction.Nothing;
        public string actionOriginalArgs = null;
        public int threadIndex = -1;
    }
    public enum ConversionAction
    {
        Nothing = 0,
        Delete = 1,
        Rename_Extension_With_Bk = 2,
        Move = 3,
    }
}