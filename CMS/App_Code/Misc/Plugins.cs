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
        #region "Classes - Request Handlers"
        /// <summary>
        /// Used for communicating request handler data between Default.aspx.cs and the methods within this class.
        /// </summary>
        public class Request
        {
            public class RequestHandler
            {
                private string pluginid, classPath;
                public RequestHandler(string pluginid, string classPath)
                {
                    this.pluginid = pluginid; this.classPath = classPath;
                }
                public string Pluginid
                {
                    get { return pluginid; }
                }
                public string ClassPath
                {
                    get { return classPath; }
                }
            }
            public class RequestHandlers
            {
                private List<RequestHandler> handlers;
                public RequestHandlers()
                {
                    handlers = new List<RequestHandler>();
                }
                public System.Collections.IEnumerator GetEnumerator()
                {
                    return handlers.GetEnumerator();
                }
                public RequestHandler this[int index]
                {
                    get
                    {
                        return handlers[index];
                    }
                }
                public void add(RequestHandler handler)
                {
                    handlers.Add(handler);
                }
                public int count()
                {
                    return handlers.Count;
                }
            }
            #region "Methods"
            /// <summary>
            /// Returns the classpath of the plugin responsible for the passed request.
            /// 
            /// If a 404 occurs, this method will return a method able to handle the error;
            /// if no plugin is able to handle a 404, the returned collection will be empty.
            /// </summary>
            /// <param name="request"></param>
            /// <returns></returns>
            public static RequestHandlers getRequestPlugin(Connector conn, HttpRequest request)
            {
                // If no page is provided, we'll check for a default page in the setting, else we'll just use home
                string page = request.QueryString["page"] ?? (Core.settings.contains("core", "default_page") ? Core.settings["core"]["default_page"] : "home");
                RequestHandlers handlers = new RequestHandlers();
                foreach (ResultRow handler in conn.Query_Read("SELECT p.pluginid, p.classpath FROM urlrewriting AS u LEFT OUTER JOIN plugins AS p ON p.pluginid=u.pluginid WHERE u.parent IS NULL AND u.title LIKE '" + Utils.Escape(page.Replace("%", "")) + "' AND p.state='" + (int)UberCMS.Plugins.Base.State.Enabled + "' ORDER BY p.invoke_order ASC LIMIT 1"))
                    handlers.add(new RequestHandler(handler["pluginid"], handler["classpath"]));
                return handlers;
            }
            /// <summary>
            /// Returns the class-path for the 404 pages in their invoke order ascending.
            /// </summary>
            /// <param name="conn"></param>
            /// <returns></returns>
            public static RequestHandlers getRequest404s(Connector conn)
            {
                RequestHandlers handlers = new RequestHandlers();
                foreach (ResultRow handler in conn.Query_Read("SELECT pluginid, classpath FROM plugins WHERE state='" + (int)UberCMS.Plugins.Base.State.Enabled + "' AND handles_404='1' ORDER BY invoke_order ASC"))
                    handlers.add(new RequestHandler(handler["pluginid"], handler["classpath"]));
                return handlers;
            }
            #endregion
        }
        #endregion

        #region "Methods - Reflection"
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
        #endregion

        #region "Methods - Compression"
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
        #endregion

        #region "Methods - SQL"
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
        #endregion

        #region "Methods - Plugins"
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
        #endregion

        #region "Methods - Content"
        /// <summary>
        /// Copies files from one directory to another.
        /// </summary>
        /// <param name="pathOrigin">The origin path of the content.</param>
        /// <param name="pathDest">The destination path of the content.</param>
        /// <returns></returns>
        public static string contentInstall(string pathOrigin, string pathDest)
        {
            try
            {
                string destPath;
                string destDirectory;
                foreach (string file in Directory.GetFiles(pathOrigin, "*", SearchOption.AllDirectories))
                {
                    destPath = pathDest + file.Substring(pathOrigin.Length);
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
        /// Removes files from the destination path that exist in the origin path.
        /// </summary>
        /// <param name="pathOrigin">The origin path of the content.</param>
        /// <param name="pathDest">The destination path of the content.</param>
        /// <returns></returns>
        public static string contentUninstall(string pathOrigin, string pathDest)
        {
            try
            {
                string destPath;
                string destDirectory;
                foreach (string file in Directory.GetFiles(pathOrigin, "*", SearchOption.AllDirectories))
                {
                    destPath = pathDest + file.Substring(pathOrigin.Length);
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
                    catch { }
                }
            }
            catch (Exception ex)
            {
                return "Failed to uninstall content - " + ex.Message + " - " + ex.GetBaseException().Message + "!";
            }
            return null;
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
            return contentInstall(path, Core.basePath + "\\Content");
        }
        /// <summary>
        /// Uninstalls a plugin content directory from the main /Content directory.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string contentUninstall(string path)
        {
            return contentUninstall(path, Core.basePath + "\\Content");
        }
        #endregion

        #region "Methods - Templates"
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
        #endregion

        #region "Methods - URL rewriting"
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
        /// Unreserves an array of titles.
        /// </summary>
        /// <param name="urls"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static string unreserveURLs(string[] urls, Connector conn)
        {
            try
            {
                StringBuilder query = new StringBuilder("DELETE FROM urlrewriting WHERE ");
                foreach (string url in urls)
                    query.Append("title LIKE '" + Utils.Escape(url) + "' OR ");
                query.Remove(query.Length - 3, 3);
            }
            catch { }
            return null;
        }
        #endregion

        #region "Methods - Pre-processor Directives"
        public static string preprocessorDirective_Add(string symbol)
        {
            return preprocessorDirective_Modify(symbol, true);
        }
        public static string preprocessorDirective_Remove(string symbol)
        {
            return preprocessorDirective_Modify(symbol, false);
        }
        private static string preprocessorDirective_Modify(string symbol, bool addingSymbol)
        {
            try
            {
                string configPath = getWebConfigPath();
                XmlDocument webConfig = new XmlDocument();
                webConfig.Load(configPath);
                XmlNode compiler;
                if ((compiler = webConfig.SelectSingleNode("configuration/system.codedom/compilers/compiler")) == null)
                    return "The web.config is missing the compiler section and hence directives cannot be added! Please modify your web.config...";
                else
                {
                    if (addingSymbol)
                    {
                        string symbols = compiler.Attributes["compilerOptions"].Value;
                        if (symbols.Length == 0)
                            symbols = "/d:" + symbol;
                        else if (symbols.Contains("/d:" + symbol + ",") || symbols.Contains("," + symbol + ",") || symbols.EndsWith("," + symbol))
                            return null; // Contains pre-processor already
                        else
                            symbols += "," + symbol;
                        compiler.Attributes["compilerOptions"].Value = symbols;
                    }
                    else
                    {
                        string symbols = compiler.Attributes["compilerOptions"].Value;
                        if (symbols.Length == 0)
                            return null; // No values to remove, just return
                        else if (symbols.Length == 3 + symbol.Length)
                            symbols = string.Empty; // The symbol string must be /d:<symbol> - hence we'll leave it empty
                        else if (symbols.EndsWith("," + symbol))
                            symbols = symbols.Remove(symbols.Length - (symbol.Length + 1), symbol.Length + 1);
                        else
                        {
                            // Remove the symbol, which could be like /d:<symbol>, *or* ,<symbol>,
                            symbols = symbols.Replace("/d:" + symbol + ",", "/d:").Replace("," + symbol + ",", ",");
                            // Remove ending ,<symbol>
                            if (symbols.EndsWith("," + symbol)) symbols = symbols.Remove(symbols.Length - (symbol.Length + 1), symbol.Length + 1);
                        }
                        // -- Update the modified flags
                        compiler.Attributes["compilerOptions"].Value = symbols;
                    }
                    webConfig.Save(configPath);
                }
            }
            catch (Exception ex)
            {
                return "Failed to " + (addingSymbol ? "add" : "remove") + " pre-processor directive symbol '" + symbol + "' - " + ex.Message + "!";
            }
            return null;
        }
        private static string getWebConfigPath()
        {
            if (File.Exists(Core.basePath + "\\web.config"))
                return Core.basePath + "\\web.config";
            else if(File.Exists(Core.basePath + "\\Web.config"))
                return Core.basePath + "\\Web.config";
            else
                throw new Exception("Could not find web.config file!");
        }
        #endregion

        #region "Methods - Plugin Installation"
        /// <summary>
        /// Installs a plugin from either a zip-file or from a given path; if the install is from a zip, the zip file
        /// will not be deleted by this process (therefore you'll need to delete it after invoking this method).
        /// </summary>
        /// <param name="basePath">The base path of where the plugin is located.</param>
        /// <param name="pathIsZipFile">Specifies if the basePath parameter is a path of a directory (if false) or a zip file (if true).</param>
        /// <param name="conn"></param>
        /// <param name="pluginid">Specify this parameter as null if the plugin is new; if this is specified, the existing plugin entry will be updated.</param>
        /// <returns></returns>
        public static string install(string pluginid, ref string finalPluginid, string basePath, bool pathIsZipFile, Connector conn)
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
            // Read config values if pluginid is null
            string directory;
            string title;
            string classpath;
            string cycleInterval;
            string invokeOrder;
            bool handles404;
            bool handlesRequestStart;
            bool handlesRequestEnd;
            if (pluginid == null)
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
                catch (Exception ex)
                {
                    if (pathIsZipFile)
                        try { Directory.Delete(tempPath, true); }
                        catch { }
                    return "Could not read configuration, it's most likely a piece of data is missing; this could be a plugin designed for a different version of Uber CMS - " + ex.Message + "!";
                }
            else
            {
                directory = title = classpath = cycleInterval = invokeOrder = null;
                handles404 = handlesRequestStart = handlesRequestEnd = false;
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
            // Update the database
            try
            {
                if (pluginid == null) // Insert if the pluginid is null, else we'll just update the status
                    finalPluginid = conn.Query_Scalar("INSERT INTO plugins (title, directory, classpath, cycle_interval, invoke_order, state, handles_404, handles_request_start, handles_request_end) VALUES('" + Utils.Escape(title) + "', '" + Utils.Escape(directory) + "', '" + Utils.Escape(classpath) + "', '" + Utils.Escape(cycleInterval) + "', '" + Utils.Escape(invokeOrder) + "', '" + (int)(UberCMS.Plugins.Base.State.Disabled) + "', '" + (handles404 ? "1" : "0") + "', '" + (handlesRequestStart ? "1" : "0") + "', '" + (handlesRequestEnd ? "1" : "0") + "'); SELECT LAST_INSERT_ID();").ToString();
                else
                {
                    conn.Query_Execute("UPDATE plugins SET state='" + (int)UberCMS.Plugins.Base.State.Disabled + "' WHERE pluginid='" + Utils.Escape(pluginid) + "'");
                    finalPluginid = pluginid;
                }
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
        /// <param name="deletePath">If true, the physical files for the plugin will be removed; if false, the plugin's database entry will remain for later installation with the physical files.</param>
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
            if (deletePath)
            {
                // Remove folder and database entry
                try
                {
                    Directory.Delete(Core.basePath + "\\App_Code\\Plugins\\" + info[0]["directory"], true);
                }
                catch (Exception ex)
                {
                    return "Critical failure - failed to delete directory - " + ex.Message + "!";
                }
                conn.Query_Execute("DELETE FROM plugins WHERE pluginid='" + Utils.Escape(pluginid) + "'");
            }
            else
            {
                // Update the plugin to uninstalled
                conn.Query_Execute("UPDATE plugins SET state='" + (int)UberCMS.Plugins.Base.State.Uninstalled + "' WHERE pluginid='" + Utils.Escape(pluginid) + "'");
            }
            return null;
        }
        /// <summary>
        /// Enables a plugin.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
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
                return "Failed to enable plugin - " + ex.Message + " - " + ex.GetBaseException().Message + " - " + ex.GetBaseException().StackTrace + "!";
            }
            // Update status
            conn.Query_Execute("UPDATE plugins SET state='" + (int)UberCMS.Plugins.Base.State.Enabled + "' WHERE pluginid='" + Utils.Escape(pluginid) + "'");
            // Restart the core
            Core.cmsStop();
            Core.cmsStart();
            return null;
        }
        /// <summary>
        /// Disables a plugin.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
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
                return "Failed to disable plugin - " + ex.Message + " - " + ex.GetBaseException().Message + " - " + ex.GetBaseException().StackTrace + "!";
            }
            // Update status
            conn.Query_Execute("UPDATE plugins SET state='" + (int)UberCMS.Plugins.Base.State.Disabled + "' WHERE pluginid='" + Utils.Escape(pluginid) + "'");
            return null;
        }
        #endregion

        #region "Methods - CSS & JS"
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
            pageElements["HEADER"] += "<script type=\"text/javascript\" src=\"" + pageElements["URL"] + path + "\"></script>";
        }

        /// <summary>
        /// Adds a CSS file to the header, but only once.
        /// </summary>
        /// <param name="path"></param>
        public static void addHeaderCssOnce(string path, ref PageElements pageElements)
        {
            bool notDefined = false;
            if ((notDefined = !pageElements.containsElementKey("HEADER"))) pageElements["HEADER"] = string.Empty;
            string data = "<link href=\"" + pageElements["URL"] + path + "\" type=\"text/css\" rel=\"Stylesheet\" />";
            if (notDefined || !pageElements["HEADER"].Contains(data))
                pageElements["HEADER"] += data;
        }
        /// <summary>
        /// Adds a JS file to the header, but only once.
        /// </summary>
        /// <param name="path"></param>
        public static void addHeaderJsOnce(string path, ref PageElements pageElements)
        {
            bool notDefined = false;
            if ((notDefined = !pageElements.containsElementKey("HEADER"))) pageElements["HEADER"] = string.Empty;
            string data = "<script src=\"" + pageElements["URL"] + path + "\"></script>";
            if (notDefined || !pageElements["HEADER"].Contains(data))
                pageElements["HEADER"] += data;
        }
        #endregion

        #region "Methods - Misc"
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
                return t.TotalMinutes < 2 ?  "1 minute ago" : Math.Round(t.TotalMinutes, 0) + " minutes ago";
            else if (t.TotalHours < 24)
                return t.TotalHours < 2 ? "1 hour ago" : Math.Round(t.TotalHours, 0) + " hours ago";
            else if (t.TotalDays < 365)
                return t.TotalDays < 2 ? "1 day ago" : Math.Round(t.TotalDays, 0) + " days ago";
            else
                return date.ToString("dd/MM/yyyy HH:mm:ss");
        }
        /// <summary>
        /// Validates if a string consists of numeric characters (0 to 9); can accept null.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool isNumeric(string text)
        {
            if (text == null) return false;
            foreach (char c in text)
                if (c < 48 || c > 57) return false;
            return true;
        }
        /// <summary>
        /// Returns a string with 2 d.p which converts bytes into e.g. megabytes or gigabytes.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string getBytesString(float bytes)
        {
            const float kiolobyte = 1024;
            const float megabyte = 1048576;
            const float gigabyte = 1073741824;
            const float terrabyte = 1099511627776;
            const float petabyte = 1125899906842624;

            if (bytes < kiolobyte)
                return bytes + " B";
            else if (bytes < megabyte)
                return (bytes / kiolobyte).ToString("0.##") + " KB";
            else if (bytes < gigabyte)
                return (bytes / megabyte).ToString("0.##") + " MB";
            else if (bytes < terrabyte)
                return (bytes /gigabyte).ToString("0.##") + " GB";
            else if (bytes < petabyte)
                return (bytes / terrabyte).ToString("0.##") + "TB";
            else
                return (bytes / petabyte).ToString("0.##") + " PB";
        }
        #endregion
    }
}