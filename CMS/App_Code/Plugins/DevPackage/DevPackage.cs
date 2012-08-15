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
            Misc.Plugins.reserveURLs(pluginid, null, new string[] { "devpackage" }, conn);
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            Misc.Plugins.unreserveURLs(pluginid, conn);
            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["TITLE"] = "Developers - Package Automator";

            string targetPluginid = request.QueryString["1"];
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
                List<string[]> files = new List<string[]>();
                List<string> filesMissing = new List<string>();
                string[] file;
                foreach (string rawFile in File.ReadAllText(basePath + "\\files.list").Replace("\r", string.Empty).Split('\n'))
                {
                    file = rawFile.Trim().Split(',');
                    if ((file.Length == 2 || (file.Length == 1 && file[0].StartsWith("-"))) && !file[0].StartsWith("//"))
                    {
                        // Format the file
                        file[0] = file[0].Replace("%LOCAL%", basePath).Replace("%GLOBAL%", cmsBasePath);
                        if(file.Length == 2) file[1] = file[1].Replace("%LOCAL%", basePath).Replace("%GLOBAL%", cmsBasePath);
                        if(file[0].StartsWith("-") && file[0].Length > 1)
                            filesExcluded.Add(file[0].Substring(1));
                        else if (file[0].EndsWith("\\*"))
                        {
                            file[0] = file[0].Remove(file[0].Length - 2, 2);
                            // A path has been specified, not a file
                            if (!Directory.Exists(file[0]))
                                filesMissing.Add(file[0] + " (DIRECTORY)");
                            else
                                foreach (string f in Directory.GetFiles(file[0], "*", SearchOption.AllDirectories))
                                    if(!f.EndsWith("Thumbs.db")) // Protection against adding Windows thumbnail indexer files
                                        files.Add(new string[]{f, file[1] + Path.GetDirectoryName(f).Remove(0, file[0].Length - 1) });
                        }
                        else
                        {
                            // Check the file exists
                            if (!File.Exists(file[0]))
                                filesMissing.Add(file[0]);
                            else
                                files.Add(new string[]{file[0], file[1]});
                        }
                    }
                }
                // Remove the excluded filees from the files array
                for (int i = 0; i < files.Count; i++)
                    if (filesExcluded.Contains(files[i][0]))
                        files.RemoveAt(i);
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
                using (ZipFile zip = new ZipFile(zipPath))
                {
                    foreach (string[] f in files)
                        zip.AddFile(f[0], f[1]);
                    zip.Save();
                }
                // Inform the dev of the success
                StringBuilder output = new StringBuilder("Successfully packaged zip:<br />");
                output.Append(zipPath).Append("<br /><br />Files excluded:");
                foreach (string f in filesExcluded)
                    output.Append("<br />").Append(f);
                output.Append("<br /><br />Files included:");
                foreach (string[] f in files)
                    output.Append("<br />").Append(f[0]);
                pageElements["CONTENT"] =  output.ToString();
            }
            else
                pageElements["CONTENT"] = "No pluginid specified - call this page (GET) with /devpackage/<pluginid>; the zip file will be outputted to the root directory of the CMS.";
        }
    }
}
