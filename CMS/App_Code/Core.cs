﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS
 * File:                    /App_Code/Core.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Responsible for handling plugin start-up, ending, cycle events, application errors
 * and database connectivity for those events through a global connector.
 */
using System;
using System.Collections.Generic;
using System.Web;
using UberLib.Connector;
using UberLib.Connector.Connectors;
using System.IO;
using System.Xml;
using System.Threading;

namespace UberCMS
{
    public static class Core
    {
        #region "Enums"
        public enum State
        {
            NotInstalled,
            Stopped,
            Started,
            CriticalFailure
        }
        #endregion

        #region "Variables - Database"
        private static string connHost = null;
        private static int connPort = 3306;
        private static string connDatabase = null;
        private static string connUsername = null;
        private static string connPassword = null;
        #endregion

        #region "Variables"
        public static State state = State.Stopped;
        public static Exception criticalFailureError = null;
        public static string basePath = null;
        public static Connector globalConnector = null;
        private static Thread cycThread = null;
        public static Misc.HtmlTemplates templates = null;
        public static Misc.Settings settings = null;
        public static Misc.EmailQueue emailQueue = null;
        #endregion

        #region "Methods - CMS start/stop/error"
        public static void cmsStart()
        {
            try
            {
                // Set the base-path and check the CMS has been installed
                basePath = AppDomain.CurrentDomain.BaseDirectory;
                if (!File.Exists(basePath + "\\CMS.config"))
                {
                    state = State.NotInstalled;
                    return;
                }
                // Load the connector settings
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(File.ReadAllText(basePath + "\\CMS.config"));
                connHost = doc["settings"]["db"]["host"].InnerText;
                connPort = int.Parse(doc["settings"]["db"]["port"].InnerText);
                connDatabase = doc["settings"]["db"]["database"].InnerText;
                connUsername = doc["settings"]["db"]["username"].InnerText;
                connPassword = doc["settings"]["db"]["password"].InnerText;
                // Set the global connector
                globalConnector = connectorCreate(true);
                // Load and start e-mail queue service
                emailQueue = new Misc.EmailQueue(doc["settings"]["mail"]["host"].InnerText, int.Parse(doc["settings"]["mail"]["port"].InnerText), doc["settings"]["mail"]["username"].InnerText, doc["settings"]["mail"]["password"].InnerText, doc["settings"]["mail"]["address"].InnerText);
                emailQueue.start();
                // Wipe the cache folder
                string cachePath = basePath + "\\Cache";
                if (Directory.Exists(cachePath))
                {
                    // We don't just delete the directory because something could be in-use..hence we try to delete as much as possible
                    try
                    {
                        foreach (string file in Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories))
                            File.Delete(file);
                        foreach (string dir in Directory.GetDirectories(cachePath, "*", SearchOption.AllDirectories))
                            Directory.Delete(dir);
                    }
                    catch
                    {
                    }
                }
                // Load settings
                settings = new Misc.Settings();
                settings.reload(globalConnector);
                // Load templates
                templates = new Misc.HtmlTemplates(globalConnector);
                // Invoke plugins
                foreach (ResultRow plugin in globalConnector.Query_Read("SELECT pluginid, classpath FROM plugins WHERE state='" + (int)Plugins.Base.State.Enabled + "' ORDER BY invoke_order ASC"))
                    try
                    {
                        Misc.Plugins.invokeMethod(plugin["classpath"], "cmsStart", new object[]{ plugin["pluginid"], globalConnector});
                    }
                    catch
                    { }
                // Complete
                state = State.Started;
                // Begin cycler
                cyclerStart();
            }
            catch (Exception ex)
            {
                state = State.CriticalFailure;
                criticalFailureError = ex;
            }
        }
        public static void cmsStop()
        {
            // Stop cycler
            cyclerStop();
            // Stop e-mail queue service
            emailQueue.stop();
            // Set the state
            state = State.Stopped;
            // Inform each plugin
            foreach (ResultRow plugin in globalConnector.Query_Read("SELECT pluginid, classpath FROM plugins WHERE state='" + (int)Plugins.Base.State.Enabled + "' ORDER BY invoke_order ASC"))
                try
                {
                    Misc.Plugins.invokeMethod(plugin["classpath"], "cmsStop", new object[] { plugin["pluginid"], globalConnector });
                }
                catch
                { }
            // Dispose the templates
            templates.dispose();
            templates = null;
            // Dispose settings
            settings.dispose();
            settings = null;
            // Dispose the global connector
            try
            {
                globalConnector.Disconnect();
            }
            finally
            {
                globalConnector = null;
            }
        }
        public static void cmsError(Exception ex)
        {
            if (state == State.Started)
                // Invoke each plugin's handleError method
                foreach (ResultRow plugin in globalConnector.Query_Read("SELECT pluginid, classpath FROM plugins WHERE state='" + (int)Plugins.Base.State.Enabled + "' ORDER BY invoke_order ASC"))
                    try
                    {
                        Misc.Plugins.invokeMethod(plugin["classpath"], "handleError", new object[] { plugin["pluginid"], globalConnector });
                    }
                    catch
                    { }
        }
        #endregion

        #region "Methods - Cycler"
        public static void cyclerStart()
        {
            cycThread = new Thread(cyclerThread);
            cycThread.Start();
        }
        public static void cyclerStop()
        {
            try
            {
                cycThread.Abort();
            }
            catch { }
            cycThread = null;
        }
        public const int cyclerInterval = 100;
        public static void cyclerThread()
        {
            Result plugins = Core.globalConnector.Query_Read("SELECT pluginid, classpath, cycle_interval FROM plugins WHERE state='" + (int)Plugins.Base.State.Enabled + "' ORDER BY invoke_order ASC");
            // Parse the plugins and build a list of classpath <-> intervals
            List<string[]> pluginClassPaths = new List<string[]>();
            List<int> pluginIntervals = new List<int>();
            foreach (ResultRow plugin in plugins)
                if(plugin["cycle_interval"].Length > 0 && plugin["cycle_interval"] != "0")
                {
                    pluginClassPaths.Add(new string[]{ plugin["pluginid"], plugin["classpath"] });
                    pluginIntervals.Add(int.Parse(plugin["cycle_interval"]));
                }
            // Check if we have any intervals, if not we'll just exit...
            if (pluginClassPaths.Count == 0) return;
            // Loop forever (until the thread is aborted) and invoke the plugins periodically
            float[] counters = new float[pluginIntervals.Count];
            int i;
            while (true)
            {
                // Check the counter of each plugin
                for (i = 0; i < pluginIntervals.Count; i++)
                    if (counters[i] > 0 && counters[i] / pluginIntervals[i] > 1)
                    {
                        // Reset timer and invoke method
                        counters[i] = 0;
                        Misc.Plugins.invokeMethod(pluginClassPaths[i][1], "handleCycle", new object[] { pluginClassPaths[i][0], globalConnector });
                    }
                    else
                        // Increment counter
                        counters[i] += cyclerInterval;
                // Sleep...
                Thread.Sleep(cyclerInterval);
            }
        }
        #endregion

        #region "Methods"
        public static Connector connectorCreate(bool persist)
        {
            MySQL conn = new MySQL();
            conn.Settings_Host = connHost;
            conn.Settings_Port = connPort;
            conn.Settings_Database = connDatabase;
            conn.Settings_User = connUsername;
            conn.Settings_Pass = connPassword;
            if (persist)
            {
                conn.Settings_Timeout_Connection = 864000; // 10 days
                conn.Settings_Timeout_Command = 3600; // 1 hour
            }
            conn.Connect();
            return conn;
        }
        #endregion
    }
}