/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber Media
 * File:                    /UberMedia/Indexer.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 *  Responsible for running in the background and indexing each drive/folder
 *  added by the user; the files in the drives are virtually indexed in the database
 *  if their extension matches the type of file to be indexed in the folder (specified
 *  by the user). The indexer may also invoke the thumbnail generation service to generate
 *  thumbnails of media.
 */
using System;
using System.Collections.Generic;
using System.Web;
using UberLib.Connector;
using System.Threading;
using System.IO;
using System.Text;

namespace UberCMS.Plugins.UberMedia
{
    /// <summary>
    /// Used to pass values to an indexer thread.
    /// </summary>
    public struct ThreadIndexerAttribs
    {
        public Connector Connector;
        public string pfolderid;
        public string base_path;
        public bool allow_web_synopsis;
    }
    /// <summary>
    /// Responsible for indexing media files in a virtual file-system for faster searching and for the collation and manipulation of media data.
    /// </summary>
    public static class Indexer
    {
        #region "Variables"
        /// <summary>
        /// Used for checking if we need to keep the application-pool alive for this service, when true.
        /// </summary>
        public static bool serviceIsActive = false;
        public static bool Terminate = false;
        public static Dictionary<string, string> threadPoolStatus = new Dictionary<string, string>(); // Stores the status's of drive-indexing
        public static Dictionary<string, Thread> threadPool = new Dictionary<string, Thread>(); // Stores all the active threads; string = pfolderid.
        #endregion

        #region "Methods - Start/stop indexer(s)"
        public static void indexAllDrives()
        {
            foreach (ResultRow drive in Core.globalConnector.Query_Read("SELECT pfolderid, physicalpath, allow_web_synopsis FROM um_physical_folders ORDER BY title ASC"))
                Indexer.indexDrive(drive["pfolderid"], drive["physicalpath"], drive["allow_web_synopsis"].Equals("1"));
        }
        public static void indexDrive(string pfolderid, string path, bool allow_web_synopsis)
        {
            if (!Directory.Exists(path) || Terminate) return; // Doesnt exist/down? Leave...
            lock (threadPool)
            {
                // Check if a pre-existing thread exists
                if (threadPool.ContainsKey(pfolderid))
                    return; // Already running, return
                updateStatus(pfolderid, "Initializing thread for " + pfolderid);
                // Build the args to pass to the thread
                ThreadIndexerAttribs tia = new ThreadIndexerAttribs();
                tia.base_path = path;
                tia.pfolderid = pfolderid;
                tia.allow_web_synopsis = allow_web_synopsis;
                tia.Connector = Core.connectorCreate(false);
                // Create and start thread
                Thread th = new Thread(new ParameterizedThreadStart(threadIndexer));
                th.Start(tia);
                // Add thread to pool
                threadPool.Add(pfolderid, th);
            }
        }
        public static void terminateThreadPool()
        {
            Terminate = true;
            Thread.Sleep(100);
            lock (threadPool)
            {
                foreach (KeyValuePair<string, Thread> thr in threadPool) thr.Value.Abort();
                threadPool.Clear();
            }
            Terminate = false;
            serviceIsActive = false;
        }
        public static void terminateIndexer(string pfolderid)
        {
            lock (threadPool)
            {
                if (threadPool.ContainsKey(pfolderid))
                {
                    try
                    {
                        threadPool[pfolderid].Abort();
                    }
                    catch { }
                    threadPool.Remove(pfolderid);
                }
            }
        }
        #endregion

        #region "Methods - Delegation"
        static void threadIndexer(object obj)
        {
            if (Terminate) return;
            serviceIsActive = true;
            ThreadIndexerAttribs tia = (ThreadIndexerAttribs)obj;
            // If we retrieve synopsis's, we should ensure the film-information service is ready
            if (tia.allow_web_synopsis)
            {
                updateStatus(tia.pfolderid, "Waiting for film-information service to start...");
                while (FilmInformation.state != FilmInformation.State.Started)
                    Thread.Sleep(200);
            }
            // Begin mapping extensions...
            updateStatus(tia.pfolderid, "Mapping extensions to type UID");
            tia.pfolderid = Utils.Escape(tia.pfolderid);
            // Build extensions->typeid map and a list of processible (thumbnail-wise) items
            Dictionary<string, string> ExtensionsMap = new Dictionary<string, string>();
            // Builds thumbnail extensions -> thumbnail handler/processor
            Dictionary<string, string> ThumbnailExts = new Dictionary<string, string>();
            string[] t;
            foreach (ResultRow type in tia.Connector.Query_Read("SELECT it.uid, it.extensions, it.thumbnail FROM um_physical_folder_types AS pf LEFT OUTER JOIN um_item_types AS it ON it.typeid=pf.typeid WHERE pf.pfolderid='" + tia.pfolderid + "'"))
            {
                if (Terminate) return;
                t = type["extensions"].Split(',');
                foreach (string s in t) if (s.Length > 0 && !ExtensionsMap.ContainsKey(s))
                    {
                        if (type["thumbnail"].Length > 0) ThumbnailExts.Add(s.ToLower(), type["thumbnail"]);
                        ExtensionsMap.Add(s.ToLower(), type["uid"]);
                    }
            }
            // Cache for parent addresses
            Dictionary<string, string> Cache = new Dictionary<string, string>();
            // Index each folder
            updateStatus(tia.pfolderid, "Indexing directories");
            foreach (string directory in Directory.GetDirectories(tia.base_path, "*", SearchOption.AllDirectories))
            {
                if (Terminate) return;
                if (tia.Connector.Query_Count("SELECT COUNT('') FROM um_virtual_items WHERE pfolderid='" + tia.pfolderid + "' AND type_uid='100' AND phy_path='" + Utils.Escape(directory.Remove(0, tia.base_path.Length)) + "'") == 0)
                {
                    // Create the virtual folder
                    string parentFolder = getParentVITEMID(tia.pfolderid, directory.Remove(0, tia.base_path.Length), ref Cache, tia.Connector);
                    tia.Connector.Query_Execute("INSERT INTO um_virtual_items (pfolderid, type_uid, title, phy_path, parent, date_added) VALUES('" + tia.pfolderid + "', '100', '" + Utils.Escape(Path.GetFileName(directory)) + "', '" + Utils.Escape(directory.Remove(0, tia.base_path.Length)) + "', " + (parentFolder != null ? "'" + Utils.Escape(parentFolder) + "'" : "NULL") + ", NOW());");
                }
            }
            // Index each file
            updateStatus(tia.pfolderid, "Indexing files");
            string filename, ext, title, desc;
            foreach (string file in Directory.GetFiles(tia.base_path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (Terminate) return;
                    filename = file.Replace("/", "\\").Substring(file.LastIndexOf('\\') + 1);
                    ext = filename.LastIndexOf('.') != -1 ? filename.Substring(filename.LastIndexOf('.') + 1).ToLower() : "";
                    title = ext.Length > 0 ? filename.Remove(filename.Length - (ext.Length + 1), (ext.Length + 1)) : filename;
                    if (ext.Length > 0 && ExtensionsMap.ContainsKey(ext) && tia.Connector.Query_Count("SELECT COUNT('') FROM um_virtual_items WHERE pfolderid='" + tia.pfolderid + "' AND type_uid != '100' AND phy_path='" + Utils.Escape(file.Remove(0, tia.base_path.Length)) + "'") == 0)
                    {
                        if (tia.allow_web_synopsis)
                            desc = FilmInformation.getFilmSynopsis(title, tia.Connector);
                        else
                            desc = string.Empty;
                        string parentFolder = getParentVITEMID(tia.pfolderid, file.Remove(0, tia.base_path.Length), ref Cache, tia.Connector);
                        string vitemid = tia.Connector.Query_Scalar("INSERT INTO um_virtual_items (pfolderid, type_uid, title, description, phy_path, parent, date_added) VALUES('" + tia.pfolderid + "', '" + Utils.Escape(ExtensionsMap[ext]) + "', '" + Utils.Escape(title) + "', '" + Utils.Escape(desc) + "', '" + Utils.Escape(file.Remove(0, tia.base_path.Length)) + "', " + (parentFolder != null ? "'" + Utils.Escape(parentFolder) + "'" : "NULL") + ", NOW()); SELECT LAST_INSERT_ID();").ToString();
                        if (ThumbnailExts.ContainsKey(ext)) ThumbnailGeneratorService.addItem(file, vitemid, ThumbnailExts[ext]);
                    }
                }
                catch(Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("CRITICAL ERROR - INDEXER - " + ex.Message + " - " + ex.StackTrace);
                }
            }
            updateStatus(tia.pfolderid, "Verifying index integrity");
            const string invalidQuery = "DELETE FROM um_virtual_items WHERE "; // Query for deleting invalid virtual items
            StringBuilder invalidItemsQuery = new StringBuilder(invalidQuery);
            // Verify the integrity of every item (unless the path is empty - meaning it's a virtual item e.g. youtube)
            foreach (ResultRow r in tia.Connector.Query_Read("SELECT vitemid, type_uid, phy_path FROM um_virtual_items WHERE pfolderid='" + Utils.Escape(tia.pfolderid) + "' AND phy_path != ''"))
            {
                if (Terminate) return;
                if ((r["type_uid"] == "100" && !Directory.Exists(tia.base_path + r["phy_path"])) || (r["type_uid"] != "100" && !File.Exists(tia.base_path + r["phy_path"])))
                    invalidItemsQuery.Append("vitemid='" + Utils.Escape(r["vitemid"]) + "' OR ");
            }
            // Check the query has invalid items to remove, if so execute the query
            if (invalidItemsQuery.Length > invalidQuery.Length)
                tia.Connector.Query_Execute(invalidItemsQuery.Remove(invalidItemsQuery.Length - 3, 3).ToString());
            updateStatus(tia.pfolderid, "Removing self from pool");
            // Remove self from pool
            lock (threadPool)
            {
                if (threadPool.ContainsKey(tia.pfolderid)) threadPool.Remove(tia.pfolderid);
                int count = 0;
                foreach (KeyValuePair<string, Thread> th in threadPool)
                    if (th.Value.ThreadState == System.Threading.ThreadState.Running || th.Value.ThreadState == System.Threading.ThreadState.WaitSleepJoin)
                        count++;
                if (count < 1)
                    serviceIsActive = false;
            }
            updateStatus(tia.pfolderid, "Thread finished");
        }
        #endregion

        #region "Methods - Status"
        private static void updateStatus(string pfolderid, string text)
        {
            lock (threadPoolStatus)
            {
                if (threadPoolStatus.ContainsKey(pfolderid)) threadPoolStatus[pfolderid] = text;
                else threadPoolStatus.Add(pfolderid, text);
            }
        }
        public static string getStatus(string pfolderid)
        {
            return threadPoolStatus.ContainsKey(pfolderid) ? threadPoolStatus[pfolderid] : "Thread has not executed";
        }
        #endregion

        #region "Methods"
        public static string getParentVITEMID(string pfolderid, string pathofmedium, ref Dictionary<string, string> Cache, Connector Connector)
        {
            if (Cache.ContainsKey(pathofmedium)) return Cache[pathofmedium];
            int lsep = pathofmedium.IndexOf('\\');
            int rsep = pathofmedium.LastIndexOf('\\');
            if (lsep == rsep) return null; // Item is at the top
            else
            {
                string t = Connector.Query_Scalar("SELECT vitemid FROM um_virtual_items WHERE type_uid='100' AND pfolderid='" + Utils.Escape(pfolderid) + "' AND phy_path='" + Utils.Escape(pathofmedium.Substring(0, rsep)) + "'").ToString();
                Cache.Add(pathofmedium, t);
                return t;
            }
        }
        #endregion
    }
}