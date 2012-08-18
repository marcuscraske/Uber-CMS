using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using Ionic.Zip;
using System.IO;

namespace UberCMS.Plugins
{
    public static class DevPackage
    {
        public static string enable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Install templates
            if ((error = Misc.Plugins.templatesInstall(basePath + "\\Templates", conn)) != null)
                return error;
            // Reserve URLs
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "devpackage" }, conn)) != null)
                return error;
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Uninstall templates
            if((error = Misc.Plugins.templatesUninstall("devpackage", conn)) != null)
                return error;
            // Unreserve URLs
            if ((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;
            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            switch (request.QueryString["1"])
            {
                case null:
                case "home":
                    pageHome(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "sync":
                    pageSync(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "package":
                    pagePackage(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "dump":
                    pageDump(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "upload":
                    pageUpload(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
            }
        }
        public static void pageHome(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["TITLE"] = "Developers - Package Automator";
            StringBuilder plugins = new StringBuilder();
            foreach (ResultRow plugin in conn.Query_Read("SELECT title, pluginid FROM plugins ORDER BY title ASC"))
            {
                plugins.Append(
                    Core.templates["devpackage"]["home_plugin"]
                    .Replace("%PLUGINID%", HttpUtility.HtmlEncode(plugin["pluginid"]))
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(plugin["title"]))
                    );
            }
            pageElements["CONTENT"] = Core.templates["devpackage"]["home"].Replace("%PLUGINS%", plugins.ToString());
        }
        /// <summary>
        /// Sycnhronises files from the global directories of a files.list to the local directory of a plugin.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="baseTemplateParent"></param>
        public static void pageSync(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["TITLE"] = "Developers - Package Automator - Sync Global Files";
            string target = request.QueryString["2"];
            if (target != null)
            {
                // Grab the path
                string basePath = Misc.Plugins.getPluginBasePath(target, conn);
                if (basePath == null) return; // Plugin does not exist
                // Check files.list exists
                if (!File.Exists(basePath + "\\files.list"))
                    pageElements["CONTENT"] = "Cannot sync plugin '<i>" + target + "</i>' at '<i>" + basePath + "</i>' - files.list does not exist!";
                else
                {
                    StringBuilder output = new StringBuilder("Files synchronised:");
                    StringBuilder excluded = new StringBuilder("<br /><br />Files excluded:");
                    // Begin syncing files
                    string line;
                    string[] file;
                    string destination;
                    foreach (string rawline in File.ReadAllText(basePath + "\\files.list").Replace("\r", "").Split('\n'))
                    {
                        line = rawline.Trim();
                        if (line.Length != 0 && !line.StartsWith("//"))
                        {
                            file = line.Split(',');
                            // <file origin>,<directory destination in plugin folder>
                            if (file.Length == 2 && file[0].StartsWith("%GLOBAL%"))
                            {
                                file[0] = file[0].Replace("%GLOBAL%", Core.basePath);
                                file[1] = file[1].Replace("%LOCAL%", basePath);
                                // Check if the line is a directory or file
                                if (file[0].EndsWith("\\*"))
                                {
                                    // Iterate and sync each file
                                    output.Append("<br />").Append(file[0]);
                                    foreach (string f in Directory.GetFiles(file[0].Remove(file[0].Length - 1, 1), "*", SearchOption.AllDirectories))
                                    {
                                        destination = basePath + file[1] + "\\" + f.Remove(0, file[0].Length - 1);
                                        pageSync_file(f, destination);
                                        output.Append("<br />-> <i>").Append(destination).Append("</i>");
                                    }
                                }
                                else
                                {
                                    // Sync the file
                                    destination = basePath + "\\" + file[1] + "\\" + Path.GetFileName(file[0]);
                                    pageSync_file(file[0], destination);
                                    output.Append("<br />").Append(file[0]).Append("<br />-> <i>").Append(destination).Append("</i>");
                                }
                            }
                            else
                                excluded.Append("<br />").Append(file[0]);
                        }
                    }
                    // Inform the developer
                    pageElements["CONTENT"] = output.ToString() + excluded.ToString();
                }
            }
        }
        private static void pageSync_file(string origin, string destination)
        {
            // Check the parent directory of the destination exists, else create it
            string dir = Path.GetDirectoryName(destination);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            else if (File.Exists(destination))
                return; // File exists - abort
            // Copy the file
            File.Copy(origin, destination);
        }
        /// <summary>
        /// Creates a package/zip archive for a plugin based on the files.list file within the plugin directory, which states
        /// which files to include in the archive; this allows for easier development and deployment of packages.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="baseTemplateParent"></param>
        public static void pagePackage(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["TITLE"] = "Developers - Package Automator - Package";
            string targetPluginid = request.QueryString["2"];
            if (targetPluginid != null)
            {
                string cmsBasePath = Core.basePath;
                // Retrieve base path of plugin - we'll use the top-directory as the zip name
                string basePath = Misc.Plugins.getPluginBasePath(targetPluginid, conn);
                if (basePath == null) return;
                // Open the files list and add each file to the zip
                if (!File.Exists(basePath + "\\files.list"))
                {
                    pageElements["CONTENT"] = "No <i>files.list</i> exists (" + basePath + "\\files.list) in the base of the plugin; this is required to get a list of files to package.";
                    return;
                }
                // Iterate each file in the list and ensure it exists - else stop the process and warn the developer
                List<string> filesExcluded = new List<string>();
                Dictionary<string, string> files = new Dictionary<string, string>();
                List<string> filesMissing = new List<string>();
                string[] file;
                foreach (string rawFile in File.ReadAllText(basePath + "\\files.list").Replace("\r", string.Empty).Split('\n'))
                {
                    file = rawFile.Trim().Split(',');
                    if ((file.Length == 2 || (file.Length == 1 && file[0].StartsWith("-"))) && !file[0].StartsWith("//"))
                    {
                        // Format the file
                        file[0] = file[0].Replace("%LOCAL%", basePath).Replace("%GLOBAL%", cmsBasePath);
                        if (file.Length == 2) file[1] = file[1].Replace("%LOCAL%", basePath).Replace("%GLOBAL%", cmsBasePath);
                        if (file[0].StartsWith("-") && file[0].Length > 1)
                            filesExcluded.Add(file[0].Substring(1));
                        else if (file[0].EndsWith("\\*"))
                        {
                            file[0] = file[0].Remove(file[0].Length - 2, 2);
                            // A path has been specified, not a file
                            if (!Directory.Exists(file[0]))
                                filesMissing.Add(file[0] + " (DIRECTORY)");
                            else
                                foreach (string f in Directory.GetFiles(file[0], "*", SearchOption.AllDirectories))
                                    if (!f.EndsWith("\\Thumbs.db")) // Protection against adding Windows thumbnail indexer files
                                        pagePackage_addFile(f, file[1] + Path.GetDirectoryName(f).Remove(0, file[0].Length - 1), ref files, ref filesExcluded);
                        }
                        else
                        {
                            // Check the file exists
                            if (!File.Exists(file[0]))
                                filesMissing.Add(file[0]);
                            else
                                pagePackage_addFile(file[0], file[1], ref files, ref filesExcluded);
                        }
                    }
                }
                // Remove the excluded filees from the files array
                foreach(string f in filesExcluded)
                    files.Remove(f);
                // If missing files have been found, inform the dev and abort
                if (filesMissing.Count > 0)
                {
                    StringBuilder content = new StringBuilder("The following files are missing:");
                    foreach (string f in filesMissing)
                        content.Append("<br />'").Append(f).Append("'");
                    pageElements["CONTENT"] = content.ToString();
                    return;
                }
                // Create a zip in the base of the CMS - delete it if it exists
                string zipPath = cmsBasePath + Path.GetFileName(basePath) + ".zip";
                if (File.Exists(zipPath))
                {
                    try
                    {
                        File.Delete(zipPath);
                    }
                    catch (Exception ex)
                    {
                        pageElements["CONTENT"] = "Failed to delete the zip-path:<br />" + zipPath + "<br /><br />" + ex.Message + "<br />" + ex.StackTrace;
                        return;
                    }
                }
                // Begin building the output
                StringBuilder output = new StringBuilder("Successfully packaged zip:<br />");
                output.Append(zipPath).Append("<br /><br />Files excluded:");
                foreach (string f in filesExcluded)
                    output.Append("<br />").Append(f);
                output.Append("<br /><br />Files included:");
                // Begin zipping the plugin
                using (ZipFile zip = new ZipFile(zipPath))
                {
                    foreach (KeyValuePair<string, string> f in files)
                    {
                        zip.AddFile(f.Key, f.Value);
                        output.Append("<br />").Append(f.Key);
                    }
                    zip.Save();
                }
                // Inform the dev of the success
                pageElements["CONTENT"] = output.ToString();
            }
            else
                pageElements["CONTENT"] = "No pluginid specified - call this page (GET) with /devpackage/package/<pluginid>; the zip file will be outputted to the root directory of the CMS.";
        }
        /// <summary>
        /// Decides if to add a file to a zip package by ensuring it doesn't already exist.
        /// </summary>
        /// <param name="fileOrigin"></param>
        /// <param name="fileDestPath"></param>
        /// <param name="files"></param>
        /// <param name="filesExcluded"></param>
        private static void pagePackage_addFile(string fileOrigin, string fileDestPath, ref Dictionary<string, string> files, ref List<string> filesExcluded)
        {
            // Apply destination suffix of .file if ending is .js to avoid web-server compile issues
            if (fileDestPath.EndsWith(".js"))
                fileDestPath += ".file";
            // Validate the destination path does not exist already, if not add the file
            bool fileUnique = true;
            if (files.ContainsValue(fileDestPath))
            {
                // Check a duplicate file does not exist
                foreach (KeyValuePair<string, string> f in files)
                    if (f.Value == fileDestPath && f.Key.EndsWith("\\" + Path.GetFileName(fileOrigin)))
                    {
                        filesExcluded.Add(fileOrigin);
                        fileUnique = false;
                        break;
                    }
            }
            if (fileUnique)
                files.Add(fileOrigin, fileDestPath);
            // Reset the variables
            fileOrigin = fileDestPath = null;
        }
        /// <summary>
        /// Used to dump templates from the database.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="baseTemplateParent"></param>
        public static void pageDump(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["TITLE"] = "Developers - Package Automator - Dump Templates";
            string templatesPath = null;
            string target = request.QueryString["2"];
            string dumpPath = null;
            List<string> templates = new List<string>();

            switch (target)
            {
                case null:
                    return;
                default:
                    // Check the plugin exists; if it does, load the templates it wants dumped
                    dumpPath = Misc.Plugins.getPluginBasePath(target, conn);
                    if (dumpPath == null) return; // Plugin does not exist
                    // Load the templates to be dumped
                    if (!File.Exists(dumpPath + "\\templates.list"))
                    {
                        pageElements["CONTENT"] = "The plugin '<i>" + target + "</i>' at '<i>" + dumpPath + "</i>' is missing the templates.list file!";
                        return;
                    }
                    templatesPath = dumpPath + "\\templates.list";
                    dumpPath += "\\Templates";
                    break;
                case "all_default":
                    dumpPath = Core.basePath + "\\Installer\\Templates";
                    templates.Add(string.Empty);
                    if (File.Exists(Core.basePath + "\\Installer\\templates.list"))
                        templatesPath = Core.basePath + "\\Installer\\templates.list";
                    break;
                case "all":
                    // Dump all templates (unless a templates.list file exists) to the installation directory
                    dumpPath = Core.basePath + "\\Installer\\Templates";
                    if (File.Exists(Core.basePath + "\\Installer\\templates.list"))
                        templatesPath = Core.basePath + "\\Installer\\templates.list";
                    break;
            }
            StringBuilder output = new StringBuilder("Dumped templates:<br />");
            if (dumpPath != null)
            {
                if (templatesPath != null || templates.Count > 0)
                {
                    if (templatesPath != null)
                    {
                        // Load the templates to be dumped
                        string formatted;
                        foreach (string line in File.ReadAllText(templatesPath).Replace("\r", "").Split('\n'))
                        {
                            formatted = line.Trim();
                            if (!formatted.StartsWith("//") && formatted.Length != 0)
                                templates.Add(formatted == "default" ? string.Empty : formatted);
                        }
                    }
                    // Ensure the dump path exists
                    if (!Directory.Exists(dumpPath))
                        Directory.CreateDirectory(dumpPath);
                    // Dump each template
                    foreach (string template in templates)
                    {
                        output.Append("<br />'" + template + "'");
                        Core.templates.dump(conn, dumpPath, template);
                    }
                }
                else
                {
                    output.Append("-- all --");
                    // Dump all templates
                    Core.templates.dump(conn, dumpPath, null);
                }
            }
            else
                return;
            pageElements["CONTENT"] = output.ToString();
        }
        /// <summary>
        /// Uploads templates from a plugin at \Templates to the database.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="baseTemplateParent"></param>
        public static void pageUpload(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["TITLE"] = "Developers - Package Automator - Upload Templates";
            string targetPluginid = request.QueryString["2"];
            if (targetPluginid != null)
            {
                string path = Misc.Plugins.getPluginBasePath(targetPluginid, conn);
                if (path == null) return;
                path += "\\Templates";
                // Load them into the database
                Misc.Plugins.templatesInstall(path, conn);
                // Inform the user
                pageElements["CONTENT"] = "Successfully installed templates from '<i>" + path + "</i>'...";
            }
        }
    }
}