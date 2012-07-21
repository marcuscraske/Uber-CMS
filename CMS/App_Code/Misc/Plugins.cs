﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS
 * File:                    /App_Code/Misc/Plugins.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Responsible for providing a range of useful methods for plugins.
 */
using System;
using System.Collections.Generic;
using System.Web;
using System.Reflection;
using UberLib.Connector;
using Ionic.Zip;
using System.IO;
using System.Xml;
using System.Text;

namespace UberCMS.Misc
{
    public static class Plugins
    {
        /// <summary>
        /// Dynamically invokes a static method.
        /// </summary>
        /// <param name="classPath"></param>
        /// <param name="methodName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static bool invokeMethod(string classPath, string methodName, object[] parameters)
        {
            try
            {
                Type t = Assembly.GetExecutingAssembly().GetType(classPath, false);
                if (t == null) return false;
                MethodInfo m = t.GetMethod(methodName);
                m.Invoke(null, parameters);
                return true;
            }
            catch(MissingMethodException)
            {
                return false;
            }
        }
        /// <summary>
        /// Dynamically invokes a static method and returns the object; if the method does not exist, null is returned.
        /// </summary>
        /// <param name="classPath"></param>
        /// <param name="methodName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object invokeMethodReturn(string classPath, string methodName, object[] parameters)
        {
            try
            {
                Type t = Assembly.GetExecutingAssembly().GetType(classPath, false);
                if (t == null) return null;
                MethodInfo m = t.GetMethod(methodName);
                return m.Invoke(null, parameters);
            }
            catch (MissingMethodException)
            {
                return null;
            }
        }
        /// <summary>
        /// Returns the classpath of the plugin responsible for the passed request.
        /// 
        /// If a 404 occurs, this method will return a method able to handle the error;
        /// if no plugin is able to handle a 404, null is returned.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static string[] getRequestPlugin(Connector conn, HttpRequest request)
        {
            string page = request.QueryString["page"] ?? "home";
            string[] classPath = null;
            // Lookup the URL rewrite table for a responsible plugin
            if (page != null)
            {
                Result res = conn.Query_Read("SELECT p.pluginid, p.classpath FROM urlrewriting AS u LEFT OUTER JOIN plugins AS p ON p.pluginid=u.pluginid WHERE u.parent IS NULL AND u.title LIKE '" + Utils.Escape(page.Replace("%", "")) + "' AND p.state='" + (int)UberCMS.Plugins.Base.State.Enabled + "' ORDER BY p.invoke_order LIMIT 1");
                if (res.Rows.Count != 0)
                    classPath = new string[] { res[0]["pluginid"], res[0]["classpath"] };
            }
            // Finsihed...return classpath
            return classPath;
        }
        /// <summary>
        /// Returns the class-path for the 404 page.
        /// </summary>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static string[] getRequest404(Connector conn)
        {
            Result res = conn.Query_Read("SELECT pluginid, classpath FROM plugins WHERE state='" + (int)UberCMS.Plugins.Base.State.Enabled + "' AND handles_404='1' ORDER BY invoke_order ASC LIMIT 1");
            if (res.Rows.Count != 0)
                return new string[] { res[0]["pluginid"], res[0]["classpath"] };
            else
                return null;
        }
        /// <summary>
        /// Extracts a zip file to a folder.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="destinationPath"></param>
        /// <returns></returns>
        public static string extractZip(string path, string destinationPath)
        {
            try
            {
                using (ZipFile file = new ZipFile(path))
                {
                    foreach (ZipEntry entry in file)
                        entry.Extract(destinationPath, ExtractExistingFileAction.OverwriteSilently);
                }
            }
            catch (Exception ex)
            {
                return "Exception occurred whilst unzipping file '" + path + "' to '" + destinationPath + "' - " + ex.Message;
            }
            return null;
        }
        /// <summary>
        /// Executes an SQL query file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static string executeSQL(string path, Connector conn)
        {
            try
            {
                if (!File.Exists(path))
                    throw new Exception("SQL script '" + path + "' could not be found!");
                else
                {
                    StringBuilder statements = new StringBuilder();
                    // Build the new list of statements to be executed by stripping out any comments
                    string data = File.ReadAllText(path).Replace("\r", string.Empty);
                    int commentIndex;
                    foreach (string line in data.Split('\n'))
                    {
                        commentIndex = line.IndexOf("--");
                        if (commentIndex == -1)
                            statements.Append(line).Append("\r\n");
                        else if (commentIndex < line.Length)
                            statements.Append(line.Substring(0, commentIndex)).Append("\r\n");
                    }
                    // Execute the statements
                    conn.Query_Execute(statements.ToString());
                    return null;
                }
            }
            catch (Exception ex)
            {
                return "Failed to execute SQL file '" + path + "' - " + ex.Message + " - " + ex.GetBaseException().Message + "!";
            }
        }
        /// <summary>
        /// Gets the directory of a plugin; returns null if not found.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static string getPluginBasePath(string pluginid, Connector conn)
        {
            string basePath = (string)conn.Query_Scalar("SELECT directory FROM plugins WHERE pluginid='" + Utils.Escape(pluginid) + "'");
            if(basePath == null)
                return null;
            else
                return Core.basePath + "\\App_Code\\Plugins\\" + basePath;
        }
        /// <summary>
        /// Installs a plugin content directory into the main /Content directory;
        /// existing files will be over-written!
        /// 
        /// Files ending with .file will have their extension removed, useful for JavaScript files.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string contentInstall(string path)
        {
            try
            {
                string destPath;
                string destDirectory;
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    destPath = Core.basePath + "\\Content" + file.Substring(path.Length);
                    if (destPath.EndsWith(".file"))
                        destPath = destPath.Remove(destPath.Length - 5, 5);
                    destDirectory = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDirectory)) Directory.CreateDirectory(destDirectory);
                    File.Copy(file, destPath, true);
                }
            }
            catch (Exception ex)
            {
                return "Failed to install content - " + ex.Message + " - " + ex.GetBaseException().Message + "!";
            }
            return null;
        }
        /// <summary>
        /// Uninstalls a plugin content directory from the main /Content directory.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string contentUninstall(string path)
        {
            try
            {
                string destPath;
                string destDirectory;
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    destPath = Core.basePath + "\\Content" + file.Substring(path.Length);
                    if (destPath.EndsWith(".file"))
                        destPath = destPath.Remove(destPath.Length - 5, 5);
                    try
                    {
                        File.Delete(destPath);
                        destDirectory = Path.GetDirectoryName(destPath);
                        if (Directory.Exists(destDirectory) && Directory.GetFiles(destDirectory).Length == 0)
                            // Attempt to delete the directory - we could get no files back due to permissions not allowing us to access certain files,
                            // it's not critical the directory is deleted so we can ignore it...
                            Directory.Delete(destDirectory);
                    }
                    catch{}
                }
            }
            catch (Exception ex)
            {
                return "Failed to uninstall content - " + ex.Message + " - " + ex.GetBaseException().Message + "!";
            }
            return null;
        }
        /// <summary>
        /// Installs templates within a directory into the database and reloads the templates collection.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static string templatesInstall(string path, Connector conn)
        {
            try
            {
                Core.templates.readDumpToDb(conn, path);
                Core.templates.reloadDb(conn);
            }
            catch (Exception ex)
            {
                return "Error occurred installing templates - '" + path + "' - " + ex.Message + "!";
            }
            return null;
        }
        /// <summary>
        /// Uninstalls templates based on their pkey/parent-key and reloads the template collection.
        /// </summary>
        /// <param name="pkey"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static string templatesUninstall(string pkey, Connector conn)
        {
            try
            {
                conn.Query_Execute("DELETE FROM html_templates WHERE pkey='" + Utils.Escape(pkey) + "'");
                Core.templates.reloadDb(conn);
            }
            catch (Exception ex)
            {
                return "Error occurred uninstalling templates - '" + pkey + "' - " + ex.Message;
            }
            return null;
        }
        /// <summary>
        /// Reserves a specified array of URLs for a plugin.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="parent"></param>
        /// <param name="urls"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static string reserveURLs(string pluginid, string parent, string[] urls, Connector conn)
        {
            // Check we have URLs to actually reserve
            if (urls.Length == 0) return null;
            try
            {
                // Build query
                string escapedPluginid = Utils.Escape(pluginid);
                string escapedParent = parent != null ? "'" + Utils.Escape(parent) + "'" : "NULL";
                StringBuilder statement = new StringBuilder("INSERT INTO urlrewriting (pluginid, parent, title) VALUES");
                foreach (string url in urls)
                    statement.Append("('" + escapedPluginid + "', " + escapedParent + ", '" + Utils.Escape(url) + "'),");
                statement.Remove(statement.Length - 1, 1);
                // Insert into the database
                conn.Query_Execute(statement.ToString());
                return null;
            }
            catch (Exception ex)
            {
                return "Failed to reserve URLs - " + ex.Message + " - " + ex.GetBaseException().Message + "!";
            }
        }
        /// <summary>
        /// Unreserves URLs for a specified plugin.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static string unreserveURLs(string pluginid, Connector conn)
        {
            try
            {
                conn.Query_Execute("DELETE FROM urlrewriting WHERE pluginid='" + Utils.Escape(pluginid) + "'");
                return null;
            }
            catch (Exception ex)
            {
                return "Failed to unreserve urls - " + ex.Message + " - " + ex.GetBaseException().Message + "!";
            }
        }
        /// <summary>
        /// Installs a plugin from either a zip-file or from a given path; if the install is from a zip, the zip file
        /// will not be deleted by this process (therefore you'll need to delete it after invoking this method).
        /// </summary>
        /// <param name="basePath"></param>
        /// <param name="pathIsZipFile"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static string install(string basePath, bool pathIsZipFile, Connector conn)
        {
            string error, tempPath = null, pluginDir = null;
            if (pathIsZipFile)
            {
                // Create cache folder if it doesn't exist
                try
                {
                    if (!Directory.Exists(Core.basePath + "\\Cache"))
                        Directory.CreateDirectory(Core.basePath + "\\Cache");
                }
                catch (Exception ex)
                {
                    return "Error occurred creating cache directory: " + ex.Message;
                }
                // Create directory for zip extraction
                tempPath = Core.basePath + "\\Cache\\" + DateTime.Now.ToString("yyyyMMddHHmmssffff");
                if (Directory.Exists(tempPath))
                    try
                    {
                        Directory.Delete(tempPath, true);
                    }
                    catch (Exception ex)
                    {
                        return "The temporary directory '" + tempPath + "' already exists and could not be deleted - " + ex.Message + "!";
                    }
                // Create directory
                try
                {
                    Directory.CreateDirectory(tempPath);
                }
                catch (Exception ex)
                {
                    return "Could not create extraction directory - " + ex.Message + "!";
                }
                // Extract zip
                error = extractZip(basePath, tempPath);
                if (error != null) return error;
            }
            // Load the config
            XmlDocument doc = new XmlDocument();
            if (pathIsZipFile)
            {
                try
                {
                    doc.LoadXml(File.ReadAllText(tempPath + "\\Config.xml"));
                }
                catch (Exception ex)
                {
                    try { Directory.Delete(tempPath, true); } catch { }
                    return "Could not load plugin configuration - " + ex.Message + "!";
                }
            }
            else
            {
                try
                {
                    doc.LoadXml(File.ReadAllText(basePath + "\\Config.xml"));
                }
                catch (Exception ex)
                {
                    return "Could not load plugin configuration - " + ex.Message + "!";
                }
            }
            // Read config values
            string directory;
            string title;
            string classpath;
            string cycleInterval;
            string invokeOrder;
            bool handles404;
            bool handlesRequestStart;
            bool handlesRequestEnd;
            try
            {
                directory = doc["settings"]["directory"].InnerText;
                title = doc["settings"]["title"].InnerText;
                classpath = doc["settings"]["classpath"].InnerText;
                cycleInterval = doc["settings"]["cycle_interval"].InnerText;
                invokeOrder = doc["settings"]["invoke_order"].InnerText;
                handles404 = doc["settings"]["handles_404"].InnerText.Equals("1");
                handlesRequestStart = doc["settings"]["handles_request_start"].InnerText.Equals("1");
                handlesRequestEnd = doc["settings"]["handles_request_end"].InnerText.Equals("1");
            }
            catch(Exception ex)
            {
                if(pathIsZipFile)
                    try { Directory.Delete(tempPath, true); }
                    catch { }
                return "Could not read configuration, it's most likely a piece of data is missing; this could be a plugin designed for a different version of Uber CMS - " + ex.Message + "!";
            }    
            if (pathIsZipFile)
            {
                // Check plugin directory doesn't exist
                pluginDir = Core.basePath + "\\App_code\\Plugins\\" + directory;
                if (Directory.Exists(pluginDir))
                {
                    try { Directory.Delete(tempPath, true); }
                    catch { }
                    return "Failed to create new plugin directory - '" + pluginDir + "' already exists! The plugin may already be installed...";
                }
                // Move extracted directory
                try
                {
                    Directory.Move(tempPath, pluginDir);
                }
                catch (Exception ex)
                {
                    try { Directory.Delete(tempPath, true); }
                    catch { }
                    return "Failed to move extracted directory '" + tempPath + "' to '" + pluginDir + "' - " + ex.Message + "!";
                }
            }
            // Insert into the database
            try
            {
                conn.Query_Execute("INSERT INTO plugins (title, directory, classpath, cycle_interval, invoke_order, state, handles_404, handles_request_start, handles_request_end) VALUES('" + Utils.Escape(title) + "', '" + Utils.Escape(directory) + "', '" + Utils.Escape(classpath) + "', '" + Utils.Escape(cycleInterval) + "', '" + Utils.Escape(invokeOrder) + "', '" + (int)(UberCMS.Plugins.Base.State.Disabled) + "', '" + (handles404 ? "1" : "0") + "', '" + (handlesRequestStart ? "1" : "0") + "', '" + (handlesRequestEnd ? "1" : "0") + "')");
            }
            catch (Exception ex)
            {
                if (pathIsZipFile)
                {
                    // Delete the directory we copied - error occurred during installation, no point of wasting space/risking probabal future issues
                    try
                    {
                        Directory.Delete(pluginDir, true);
                    }
                    catch { }
                }
                return "Failed to insert plugin into database - " + ex.Message + " - " + ex.GetBaseException().Message + "!";
            }
            return null;
        }
        /// <summary>
        /// Completely uninstalls a plugin.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static string uninstall(string pluginid, bool deletePath, Connector conn)
        {
            // Check if the plugin is enabled - else disable it
            Result info = conn.Query_Read("SELECT state, directory, classpath FROM plugins WHERE pluginid='" + pluginid + "'");
            if (info.Rows.Count != 1) return "Plugin does not exist!";
            bool enabled = true;
            try
            {
                enabled = (UberCMS.Plugins.Base.State)int.Parse(info[0]["state"]) == UberCMS.Plugins.Base.State.Enabled;
            }
            catch(Exception ex)
            {
                return "Could not determine state of plugin - " + ex.Message + "!";
            }
            if (enabled)
                try
                {
                    // Attempt to disable the plugin - else we'll continue the uninstallation process
                    disable(pluginid, conn);
                }
                catch
                {
                    conn.Query_Execute("UPDATE plugins SET state='" + (int)UberCMS.Plugins.Base.State.Disabled + "' WHERE pluginid='" + Utils.Escape(pluginid) + "'");
                }
            // Attempt to invoke the uninstall method
            try
            {
                string error = (string)invokeMethodReturn(info[0]["classpath"], "uninstall", new object[] { pluginid, conn });
                if (error != null) return error;
            }
            catch (Exception ex)
            {
                return "Failed to uninstall plugin - " + ex.Message + " - " + ex.GetBaseException().Message + "!";
            }
            // Remove folder
            if (deletePath)
            {
                try
                {
                    Directory.Delete(Core.basePath + "\\App_Code\\Plugins\\" + info[0]["directory"], true);
                }
                catch (Exception ex)
                {
                    return "Critical failure - failed to delete directory - " + ex.Message + "!";
                }
            }
            // Remove database entry
            conn.Query_Execute("DELETE FROM plugins WHERE pluginid='" + Utils.Escape(pluginid) + "'");
            return null;
        }
        public static string enable(string pluginid, Connector conn)
        {
            // Grab classpath
            Result info = conn.Query_Read("SELECT classpath FROM plugins WHERE pluginid='" + Utils.Escape(pluginid) + "'");
            if (info.Rows.Count != 1)
                return "Plugin does not exist!";
            // Invoke enable method
            try
            {
                string result = (string)invokeMethodReturn(info[0]["classpath"], "enable", new object[] { pluginid, conn });
                if (result != null) return result;
            }
            catch (Exception ex)
            {
                return "Failed to enable plugin - " + ex.Message + " - " + ex.GetBaseException().Message + "!";
            }
            // Update status
            conn.Query_Execute("UPDATE plugins SET state='" + (int)UberCMS.Plugins.Base.State.Enabled + "' WHERE pluginid='" + Utils.Escape(pluginid) + "'");
            // Restart the core
            Core.cmsStop();
            Core.cmsStart();
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            // Grab classpath
            Result info = conn.Query_Read("SELECT classpath FROM plugins WHERE pluginid='" + Utils.Escape(pluginid) + "'");
            if (info.Rows.Count != 1)
                return "Plugin does not exist!";
            // Invoke disable method
            try
            {
                string result = (string)invokeMethodReturn(info[0]["classpath"], "disable", new object[] { pluginid, conn });
                if (result != null) return result;
            }
            catch (Exception ex)
            {
                return "Failed to disable plugin - " + ex.Message + " - " + ex.GetBaseException().Message + "!";
            }
            // Update status
            conn.Query_Execute("UPDATE plugins SET state='" + (int)UberCMS.Plugins.Base.State.Disabled + "' WHERE pluginid='" + Utils.Escape(pluginid) + "'");
            return null;
        }
        /// <summary>
        /// Returns how long ago the specified date occurred; this is to make dates
        /// easier to read.
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static string getTimeString(DateTime date)
        {
            TimeSpan t = DateTime.Now.Subtract(date);
            if (t.TotalSeconds < 60)
                return t.TotalSeconds < 2 ? "1 second ago" : Math.Round(t.TotalSeconds, 0) + " seconds ago";
            else if (t.TotalMinutes < 60)
                return t.TotalSeconds < 2 ? "1 minute ago" : Math.Round(t.TotalMinutes, 0) + " minutes ago";
            else if (t.TotalHours < 24)
                return t.TotalHours < 2 ? "1 hour ago" : Math.Round(t.TotalHours, 0) + " hours ago";
            else if (t.TotalDays < 365)
                return t.TotalDays < 2 ? "1 day ago" : Math.Round(t.TotalDays, 0) + " days ago";
            else
                return date.ToString("dd/MM/yyyy HH:mm:ss");
        }
        /// <summary>
        /// Adds a CSS file to the header.
        /// </summary>
        /// <param name="path"></param>
        public static void addHeaderCSS(string path, ref PageElements pageElements)
        {
            if (!pageElements.containsElementKey("HEADER")) pageElements["HEADER"] = string.Empty;
            pageElements["HEADER"] += "<link href=\"" + pageElements["URL"] + path + "\" type=\"text/css\" rel=\"Stylesheet\" />";
        }
        /// <summary>
        /// Adds a JS file to the header.
        /// </summary>
        /// <param name="path"></param>
        public static void addHeaderJS(string path, ref PageElements pageElements)
        {
            if (!pageElements.containsElementKey("HEADER")) pageElements["HEADER"] = string.Empty;
            pageElements["HEADER"] += "<script src=\"" + pageElements["URL"] + path + "\"></script>";
        }
    }
}