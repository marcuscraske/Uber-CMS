/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber Media
 * File:                    /UberMedia/ThumbnailGeneratorService.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Responsible for generating preview thumbnails for visual-based media; this uses
 * reflection for handling of different media-types i.e. ffmpeg for video.
 */
using System;
using System.Collections.Generic;
using System.Web;
using System.Drawing;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.IO;
using UberLib.Connector;

namespace UberCMS.Plugins.UberMedia
{
    /// <summary>
    /// Responsible for generating thumbnails of media items.
    /// </summary>
    public static class ThumbnailGeneratorService
    {
        #region "Variables"
        /// <summary>
        /// Used for checking if we need to keep the application-pool alive for this service, when true.
        /// </summary>
        public static bool serviceIsActive = false;
        private static bool shutdownOverride = false; // Causes the threads to safely terminate
        public static string status = "Uninitialised";
        private static Thread delegatorThread = null;
        public static List<Thread> threads = new List<Thread>();
        public static List<string[]> queue = new List<string[]>(); // Path of media, output path, handler
        /// <summary>
        /// Responsible for any processors executed by the thumbnail service; this is to ensure the service can be suddenly shutdown.
        /// </summary>
        public static List<Process> processes = new List<Process>();
        #endregion

        #region "Methods - Service start/stop"
        public static void serviceStart()
        {
            serviceIsActive = true;
            if (delegatorThread != null) return;
            status = "Creating pool of threads...";
            // Create thread pool
            Thread th;
            for (int i = 0; i < Core.settings[Base.SETTINGS_KEY].getInt(Base.SETTINGS_THUMBNAIL_THREADS); i++)
            {
                th = new Thread(new ParameterizedThreadStart(processItem));
                threads.Add(th);
            }
            status = "Starting delegator...";
            // Recreate the cache folder if it exists
            if (!Directory.Exists(Core.basePath + "\\Cache"))
                Directory.CreateDirectory(Core.basePath + "\\Cache");
            // Initialise delegator/manager of threads
            delegatorThread = new Thread(new ParameterizedThreadStart(delegator));
            delegatorThread.Start();
            status = "Delegator started...";
            serviceIsActive = false;
        }
        public static void serviceStop()
        {
            status = "Delegator shutting down";
            if (delegatorThread != null)
            {
                delegatorThread.Abort();
                delegatorThread = null;
            }
            shutdownOverride = true;
            foreach (Thread th in threads) th.Abort();
            lock (processes)
                try { foreach (Process p in processes) p.Kill(); }
                catch { }
            threads.Clear();
            shutdownOverride = false;
            status = "Delegator and pool terminated";
            serviceIsActive = false;
        }
        #endregion

        #region "Methods - Delegation"
        /// <summary>
        /// Controls the threads.
        /// </summary>
        private static void delegator(object o)
        {
            while (true && !shutdownOverride)
            {
                lock (threads)
                {
                    Thread th;
                    for (int i = 0; i < threads.Count; i++)
                    {
                        th = threads[i];
                        if (th.ThreadState != System.Threading.ThreadState.WaitSleepJoin && th.ThreadState != System.Threading.ThreadState.Running && queue.Count != 0)
                            lock (queue)
                            {
                                threads[i] = new Thread(new ParameterizedThreadStart(processItem));
                                threads[i].Start(queue[0]);
                                queue.Remove(queue[0]);
                            }
                    }
                }
                Thread.Sleep(100);
                status = "Successfully looped at " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            }
        }
        private static void processItem(object o)
        {
            serviceIsActive = true;
            string[] data = (string[])o;
            try
            {
                typeof(ThumbnailGeneratorService).GetMethod("thumbnailProcessor__" + data[2], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { data[0], data[1] });
            }
            catch { }
            lock (threads)
            {
                int count = 0;
                foreach (Thread th in threads)
                    if (th.ThreadState == System.Threading.ThreadState.Running || th.ThreadState == System.Threading.ThreadState.WaitSleepJoin)
                        count++;
                if(count <= 1)
                    serviceIsActive = false;
            }
        }
        #endregion

        #region "Methods - Providers"
        public static void thumbnailProcessor__ffmpeg(string path, string vitemid)
        {
            string cachePath = Core.basePath + "\\Cache\\" + vitemid + ".png";
            // Initialize ffmpeg
            Process p = new Process();
            p.StartInfo.FileName = "ffmpeg.exe";
            p.StartInfo.WorkingDirectory = Core.basePath + @"\Bin";
            p.StartInfo.Arguments = "-itsoffset -" + Core.settings[Base.SETTINGS_KEY][Base.SETTINGS_THUMBNAIL_SCREENSHOT_TIME] + "  -i \"" + path + "\" -vcodec mjpeg -vframes 1 -an -f rawvideo -s " + Core.settings[Base.SETTINGS_KEY][Base.SETTINGS_THUMBNAIL_WIDTH] + "x" + Core.settings[Base.SETTINGS_KEY][Base.SETTINGS_THUMBNAIL_HEIGHT] + " -y \"" + cachePath + "\"";
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.Start();
            processes.Add(p);
            // Wait for ffmpeg to extract the screen-shot
            try
            {

                while (!p.WaitForExit(Core.settings[Base.SETTINGS_KEY].getInt(Base.SETTINGS_THUMBNAIL_THREAD_TTL)) && !shutdownOverride)
                {
                    Thread.Sleep(20);
                }
                p.Kill();
            }
            catch { }
            // Remove the process
            processes.Remove(p);
            // Read the image into a byte-array and delete the cache file
            if (File.Exists(cachePath))
            {
                try
                {
                    // Read the file into a byte-array
                    MemoryStream ms = new MemoryStream();
                    Image img = Image.FromFile(cachePath);
                    img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                    img.Dispose();
                    byte[] data = ms.ToArray();
                    ms.Dispose();
                    // Upload to the database
                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    parameters.Add("thumbnail", data);
                    parameters.Add("vitemid", vitemid);
                    Core.globalConnector.Query_Execute_Parameters("UPDATE um_virtual_items SET thumbnail_data=@thumbnail WHERE vitemid=@vitemid", parameters);
                }
                catch
                {
                }
                try
                {
                    // Delete the cache file
                    File.Delete(cachePath);
                }
                catch { }
            }
        }
        public static void thumbnailProcessor__youtube(string path, string vitemid)
        {
            try
            {
                // Get the ID
                string vid = File.ReadAllText(path);
                // Download thumbnail from YouTube
                HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create("http://img.youtube.com/vi/" + vid + "/1.jpg");
                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                if (resp.ContentLength == 0) return;
                // Resize and draw the thumbnail
                Bitmap img = (Bitmap)Bitmap.FromStream(resp.GetResponseStream());
                Bitmap thumbnail = new Bitmap(Core.settings[Base.SETTINGS_KEY].getInt(Base.SETTINGS_THUMBNAIL_WIDTH), Core.settings[Base.SETTINGS_KEY].getInt(Base.SETTINGS_THUMBNAIL_HEIGHT));
                Graphics g = Graphics.FromImage(thumbnail);
                g.FillRectangle(new SolidBrush(Color.DarkGray), 0, 0, thumbnail.Width, thumbnail.Height);
                double fitToThumbnailRatio = (img.Width > img.Height ? (double)thumbnail.Width / (double)img.Width : (double)thumbnail.Height / (double)img.Height);
                Rectangle drawArea = new Rectangle();
                drawArea.Width = (int)(fitToThumbnailRatio * (double)img.Width);
                drawArea.Height = (int)(fitToThumbnailRatio * (double)img.Height);
                drawArea.X = (int)(((double)thumbnail.Width - (double)drawArea.Width) / 2);
                drawArea.Y = (int)(((double)thumbnail.Height - (double)drawArea.Height) / 2);
                g.DrawImage(img, drawArea);
                g.Dispose();
                // Convert to a byte array
                MemoryStream ms = new MemoryStream();
                thumbnail.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                thumbnail.Dispose();
                img.Dispose();
                resp.Close();
                byte[] data = ms.ToArray();
                ms.Dispose();
                // Upload to the database
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("thumbnail", data);
                parameters.Add("vitemid", vitemid);
                Core.globalConnector.Query_Execute_Parameters("UPDATE um_virtual_items SET thumbnail_data=@thumbnail WHERE vitemid=@vitemid", parameters);
            }
            catch
            {
            }
        }
        public static void thumbnailProcessor__image(string path, string vitemid)
        {
            try
            {
                Bitmap img = new Bitmap(path);
                Bitmap thumbnail = new Bitmap(Core.settings[Base.SETTINGS_KEY].getInt(Base.SETTINGS_THUMBNAIL_WIDTH), Core.settings[Base.SETTINGS_KEY].getInt(Base.SETTINGS_THUMBNAIL_HEIGHT));
                Graphics g = Graphics.FromImage(thumbnail);
                g.FillRectangle(new SolidBrush(Color.DarkGray), 0, 0, thumbnail.Width, thumbnail.Height);
                double fitToThumbnailRatio = (img.Width > img.Height ? (double)thumbnail.Width / (double)img.Width : (double)thumbnail.Height / (double)img.Height);
                Rectangle drawArea = new Rectangle();
                drawArea.Width = (int)(fitToThumbnailRatio * (double)img.Width);
                drawArea.Height = (int)(fitToThumbnailRatio * (double)img.Height);
                drawArea.X = (int)(((double)thumbnail.Width - (double)drawArea.Width) / 2);
                drawArea.Y = (int)(((double)thumbnail.Height - (double)drawArea.Height) / 2);
                g.DrawImage(img, drawArea);
                g.Dispose();
                // Convert to a byte array
                MemoryStream ms = new MemoryStream();
                thumbnail.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                thumbnail.Dispose();
                img.Dispose();
                byte[] data = ms.ToArray();
                ms.Dispose();
                // Upload to the database
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("thumbnail", data);
                parameters.Add("vitemid", vitemid);
                Core.globalConnector.Query_Execute_Parameters("UPDATE um_virtual_items SET thumbnail_data=@thumbnail WHERE vitemid=@vitemid", parameters);
            }
            catch
            {
            }
        }
        public static void thumbnailProcessor__audio(string path, string vitemid)
        {
#if UBERMEDIA
            try
            {
                Tags.ID3.ID3Info i = new Tags.ID3.ID3Info(path, true);
                // Ensure data and pictures were found
                if (i.HaveException || !i.ID3v2Info.HaveTag || i.ID3v2Info.AttachedPictureFrames.Count == 0)
                    return;
                Image img = i.ID3v2Info.AttachedPictureFrames[0].Picture;
                Bitmap thumbnail = new Bitmap(Core.settings[Base.SETTINGS_KEY].getInt(Base.SETTINGS_THUMBNAIL_WIDTH), Core.settings[Base.SETTINGS_KEY].getInt(Base.SETTINGS_THUMBNAIL_HEIGHT));
                Graphics g = Graphics.FromImage(thumbnail);
                g.FillRectangle(new SolidBrush(Color.Black), 0, 0, thumbnail.Width, thumbnail.Height);
                double fitToThumbnailRatio = (img.Width > img.Height ? (double)thumbnail.Width / (double)img.Width : (double)thumbnail.Height / (double)img.Height);
                Rectangle drawArea = new Rectangle();
                drawArea.Width = (int)(fitToThumbnailRatio * (double)img.Width);
                drawArea.Height = (int)(fitToThumbnailRatio * (double)img.Height);
                drawArea.X = (int)(((double)thumbnail.Width - (double)drawArea.Width) / 2);
                drawArea.Y = (int)(((double)thumbnail.Height - (double)drawArea.Height) / 2);
                g.DrawImage(img, drawArea);
                g.Dispose();
                // Convert to a byte array
                MemoryStream ms = new MemoryStream();
                thumbnail.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                thumbnail.Dispose();
                img.Dispose();
                byte[] data = ms.ToArray();
                ms.Dispose();
                // Upload to the database
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("thumbnail", data);
                parameters.Add("vitemid", vitemid);
                Core.globalConnector.Query_Execute_Parameters("UPDATE um_virtual_items SET thumbnail_data=@thumbnail WHERE vitemid=@vitemid", parameters);
            }
            catch
            {
            }
#endif
        }
        #endregion

        #region "Methods"
        public static void addItem(string path_media, string vitemid, string handler)
        {
            lock (queue) queue.Add(new string[] { path_media, vitemid, handler });
        }
        #endregion
    }
}