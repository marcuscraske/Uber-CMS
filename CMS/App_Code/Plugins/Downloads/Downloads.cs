 ﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS
 * File:                    /App_Code/Plugins/Downloads/Downloads.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * The class responsible for providing the Downloads plugin.
 * 
 * Notes:
   * - All paths should use / for directory separation, and hence never \.
   * - All relative paths should not end with or start with a slash.
 */
using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using System.IO;
using UberLib.Connector;
using System.Threading;
using System.Drawing;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace UberCMS.Plugins
{
    public static class Downloads
    {
        #region "Constants - Settings"
        public const string SETTINGS_KEY = "downloads";
        public const string SETTINGS_THREAD_INTERVAL = "thread_interval";
        public const string SETTINGS_FILEWATCHER_ENABLED = "filewatcher_enabled";
        public const string SETTINGS_ITEMS_PER_PAGE = "items_per_page";
        #endregion

        #region "Constants - Queries"
        public const string QUERY_ICONCACHE_CLEANUP = "DELETE FROM downloads_files_icons WHERE NOT EXISTS (SELECT DISTINCT iconid FROM downloads_files WHERE iconid=downloads_files_icons.iconid);";
        #endregion

        #region "Static Variables"
        public static string downloadsPath = null;
        #endregion

        #region "Methods - Plugin Handler Events"
        public static string enable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Install SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Install.sql", conn)) != null)
                return error;
            // Install templates
            if ((error = Misc.Plugins.templatesInstall(basePath + "\\Templates", conn)) != null)
                return error;
            // Install content
            if ((error = Misc.Plugins.contentInstall(basePath + "\\Content")) != null)
                return error;
            // Install settings
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_THREAD_INTERVAL, "1800000", "The interval between each check of changed files in the downloads directory; time is in milliseconds. Must be greater than zero, else disabled.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_FILEWATCHER_ENABLED, "1", "Indicates if file-watching should be enabled; else you'll need to run the indexer for every physical file change.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_ITEMS_PER_PAGE, "15", "The number of files or folders displayed per a page.", false);
            // Hook bbcode formatter
            Common.formatProvider_add(conn, pluginid, "UberCMS.Plugins.Downloads", "format", "", "");
            // Reserve URLs
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "download", "downloads" }, conn)) != null)
                return error;
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Stop filewatcher and indexer
            watcherDisable();
            if(th != null)
                try
                {
                    th.Abort();
                    th = null;
                }
                catch { }
            // Unreserve URLs
            if ((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;
            // Uninstall templates
            if ((error = Misc.Plugins.templatesUninstall(pluginid, conn)) != null)
                return error;
            // Uninstall content
            if ((error = Misc.Plugins.contentUninstall(basePath + "\\Content")) != null)
                return error;
            // Uninstall format provider
            Common.formatProvider_remove(conn, pluginid);

            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Uninstall SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Uninstall.sql", conn)) != null)
                return error;
            // Uninstall settings
            Core.settings.removeCategory(conn, SETTINGS_KEY);

            return null;
        }
        public static string cmsStart(string pluginid, Connector conn)
        {
            // The directory where all of our files will be stored
            downloadsPath = Core.basePath + "/_downloads";
            // Check the local directory exists for downloads
            if (!Directory.Exists(downloadsPath))
                Directory.CreateDirectory(downloadsPath);
            else
                // Begin indexing pre-existing files for changes
                indexMainFolder();
            // Initialize file-watcher
            watcherEnable();
            // Check all the icons in the cache exist physically - else delete the db version
            List<string> iconsFound = new List<string>();
            StringBuilder deleteBuffer = new StringBuilder("DELETE FROM downloads_ext_icons WHERE ");
            int dbLength = deleteBuffer.Length;
            foreach (ResultRow icon in conn.Query_Read("SELECT extension FROM downloads_ext_icons"))
            {
                if (!File.Exists(Core.basePath + "\\Content\\Images\\downloads\\icons"))
                    deleteBuffer.Append("extension='" + Utils.Escape(icon["extension"]) + "' OR ");
                else
                    iconsFound.Add(icon["extension"]);
            }
            if (deleteBuffer.Length != dbLength)
                conn.Query_Execute(deleteBuffer.Remove(deleteBuffer.Length - 4, 4).Append(";").ToString());
            // Add any new physical icons to the database
            string ext;
            Dictionary<string, object> attribs;
            foreach (string file in Directory.GetFiles(Core.basePath + "\\Content\\Images\\downloads\\icons", "*.png", SearchOption.AllDirectories))
                try
                {
                    if (!iconsFound.Contains((ext = Path.GetFileNameWithoutExtension(file))))
                    {
                        attribs = new Dictionary<string, object>();
                        attribs.Add("extension", ext);
                        attribs.Add("icon", File.ReadAllBytes(file));
                        conn.Query_Execute_Parameters("INSERT INTO downloads_ext_icons (extension, icon) VALUES(@extension, @icon)", attribs);
                    }
                }
                catch { }
            return null;
        }
        public static string cmsStop(string pluginid, Connector conn)
        {
            try
            {
                watcherDisable();
            }
            catch { }
            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Check if the user is admin
            bool admin = false;
#if BASIC_SITE_AUTH
            if (HttpContext.Current.User.Identity.IsAuthenticated)
            {
                Result userPerm = conn.Query_Read("SELECT access_admin FROM bsa_user_groups AS ug LEFT OUTER JOIN bsa_users AS u ON u.groupid=ug.groupid WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
                if (userPerm.Rows.Count == 1 && userPerm[0]["access_admin"] == "1")
                    admin = true;
            }
#endif
            switch (request.QueryString["page"])
            {
                case "download":
                    pageDownload(pluginid, conn, ref pageElements, request, response, admin);
                    break;
                case "downloads":
                    pageDownloads(pluginid, conn, ref pageElements, request, response, admin);
                    break;
            }
        }
        #endregion

        #region "Methods - Pages"
        public static void pageDownload(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin)
        {
            int downloadid;
            string subpg = request.QueryString["2"];
            string pgid = request.QueryString["1"];
            if (pgid == null || pgid.Length == 0) return;
            // Check if this is a direct link
            int pgidmp;
            if ((pgidmp = pgid.LastIndexOf('.')) != -1 && pgidmp < pgid.Length)
            {
                pgid = pgid.Substring(0, pgidmp);
                subpg = "get";
            }
            // Check the actual ID parses as an integer
            if(!int.TryParse(pgid, out downloadid)) return; // Invalid item id
            // Fetch details about the download
            Result fileRaw = conn.Query_Read("SELECT * FROM downloads_files WHERE downloadid='" + Utils.Escape(pgid) + "'");
            if (fileRaw.Rows.Count != 1) return; // Download item not found
            ResultRow file = fileRaw[0];
            // Process the action
            switch (subpg)
            {
                default:
                    // Display download information
                    pageDownload_View(pluginid, conn, ref pageElements, request, response, admin, file);
                    break;
                case "get":
                    pageDownload_Get(pluginid, conn, ref pageElements, request, response, admin, file);
                    break;
                case "delete":
                    pageDownload_Delete(pluginid, conn, ref pageElements, request, response, admin, file);
                    break;
                case "move":
                    pageDownload_Move(pluginid, conn, ref pageElements, request, response, admin, file);
                    break;
                case "reset_downloads":
                    pageDownload_ResetDownloads(pluginid, conn, ref pageElements, request, response, admin, file);
                    break;
                case "edit":
                    pageDownload_Edit(pluginid, conn, ref pageElements, request, response, admin, file);
                    break;
                case "embed":
                    pageDownload_Thumb(pluginid, conn, ref pageElements, request, response, admin, file);
                    break;
            }
        }
        public static void pageDownload_View(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin, ResultRow file)
        {
            // Get downloads
            ResultRow downloads = conn.Query_Read("SELECT (SELECT COUNT('') FROM downloads WHERE downloadid='" + Utils.Escape(file["downloadid"]) + "') AS downloads_total, (SELECT COUNT('') FROM (SELECT ip_addr FROM downloads WHERE downloadid='" + Utils.Escape(file["downloadid"]) + "' GROUP BY ip_addr) AS a) AS downloads_unique")[0];
            // Render page
            pageElements["CONTENT"] = Core.templates["downloads"]["download_get"]
                .Replace("%DOWNLOADID%", file["downloadid"])
                .Replace("%NAV%", getNavBar(file["physical_path"].LastIndexOf('/') == -1 ? string.Empty : file["physical_path"].Substring(0, file["physical_path"].LastIndexOf('/'))))
                .Replace("%EXTENSION%", HttpUtility.HtmlEncode(file["extension"]))
                .Replace("%FILESIZE%", HttpUtility.HtmlEncode(file["file_size"].Length > 0 ? Misc.Plugins.getBytesString(float.Parse(file["file_size"])) : "unknown bytes"))
                .Replace("%DESCRIPTION%", file["description"].Length > 0 ? HttpUtility.HtmlEncode(file["description"]) : "(no description)")
                .Replace("%ICONID%", HttpUtility.HtmlEncode(file["iconid"]))
                .Replace("%DOWNLOADS_TOTAL%", downloads["downloads_total"])
                .Replace("%DOWNLOADS_UNIQUE%", downloads["downloads_unique"])
                .Replace("%DIRECT_LINK%", "http://" + request.Url.Host + (request.Url.Port != 80 ? ":" + request.Url.Port : string.Empty) + "/download/" + file["downloadid"] + "." + file["extension"])
                ;
            pageElements["TITLE"] = "Download - " + HttpUtility.HtmlEncode(file["title"]);
            // Admin flag
            if (admin) pageElements.setFlag("DOWNLOADS_ADMIN");
            // Add CSS
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/Downloads.css", ref pageElements);
        }
        public static void pageDownload_Get(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin, ResultRow file)
        {
            // This download method borrows code from Uber Media; much credit to http://forums.asp.net/t/1218116.aspx
            string path = downloadsPath + "/" + file["physical_path"];
            if (!File.Exists(path))
            {
                conn.Disconnect();
                response.StatusCode = 500;
                response.Write("Failed to physically locate the file, please try your request again or contact us immediately!");
                response.End();
            }
            try
            {
                // Clear the response so far, as well as disable the buffer
                response.Clear();
                response.Buffer = false;
                // Based on RFC 2046 - page 4 - media types - point 5 - http://www.rfc-editor.org/rfc/rfc2046.txt - http://stackoverflow.com/questions/1176022/unknown-file-type-mime
                response.ContentType = "application/octet-stream";
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                BinaryReader bin = new BinaryReader(fs);
                long startRead = 0;
                // Read the range of bytes requested - allows downloads to be continued
                if (request.Headers["Range"] != null)
                {
                    string[] range = request.Headers["Range"].Split(new char[] { '=', '-' }); // RFC 2616 - section 14.35
                    // Ensure there are at least two parts
                    if (range.Length >= 2)
                    {
                        // Attempt to parse the requested bytes
                        long.TryParse(range[1], out startRead);
                        // Ensure its inclusive of 0 to size of file, else reset it to zero
                        if (startRead < 0 || startRead >= fs.Length) startRead = 0;
                        else
                            // Write the range of bytes being sent - RFC 2616 - section 14.16
                            response.AddHeader("Content-Range", string.Format(" bytes {0}-{1}/{2}", startRead, fs.Length - 1, fs.Length));
                    }
                }
                // Specify the number of bytes being sent
                response.AddHeader("Content-Length", (fs.Length - startRead).ToString());
                // Specify other headers
                string lastModified = file["datetime"];
                response.AddHeader("Connection", "Keep-Alive");
                response.AddHeader("Last-Modified", lastModified);
                response.AddHeader("ETag", HttpUtility.UrlEncode(file["physical_path"], System.Text.Encoding.UTF8) + lastModified); // Unique entity identifier
                response.AddHeader("Accept-Ranges", "bytes");
                response.AddHeader("Content-Disposition", "attachment;filename=" + file["title"]);
                response.StatusCode = 206;
                // Start the stream at the offset
                bin.BaseStream.Seek(startRead, SeekOrigin.Begin);
                const int chunkSize = 16384; // 16kb will be transferred at a time
                // Write bytes whilst the user is connected in chunks of 1024 bytes
                int maxChunks = (int)Math.Ceiling((double)(fs.Length - startRead) / chunkSize);
                int i;
                for (i = 0; i < maxChunks && response.IsClientConnected; i++)
                {
                    response.BinaryWrite(bin.ReadBytes(chunkSize));
                    response.Flush();
                }
                if (i >= maxChunks)
                {
                    // Download was successful - log it
                    conn.Query_Execute("INSERT INTO downloads (downloadid, ip_addr, datetime) VALUES('" + Utils.Escape(file["downloadid"]) + "', '" + Utils.Escape(request.UserHostAddress) + "', NOW());");
                }
                bin.Close();
                fs.Close();
            }
            catch
            {
                response.StatusCode = 500;
                response.Write("Failed to get file, please try your request again or contact us immediately!");
            }
            conn.Disconnect();
            response.End();
        }
        public static void pageDownload_Delete(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin, ResultRow file)
        {
            if (!admin) return;
            if (request.Form["confirm"] != null)
            {
                // Delete file
                File.Delete(downloadsPath.Replace("\\", "/") + "/" + file["physical_path"]);
                // Delete from database
                conn.Query_Execute("DELETE FROM downloads_files WHERE downloadid='" + Utils.Escape(file["downloadid"]) + "';" + QUERY_ICONCACHE_CLEANUP);
                // End response
                conn.Disconnect();
                response.Redirect(pageElements["URL"] + "/downloads/" + (file["physical_path"].LastIndexOf('/') != -1 ? file["physical_path"].Substring(0, file["physical_path"].LastIndexOf('/')) : string.Empty));
            }
            pageElements["CONTENT"] = Core.templates["downloads"]["download_delete"]
                .Replace("%DOWNLOADID%", file["downloadid"])
                .Replace("%PHYSICAL_PATH%", HttpUtility.HtmlEncode(downloadsPath.Replace("\\", "/") + "/" + file["physical_path"]))
                .Replace("%PATH%", HttpUtility.HtmlEncode(file["physical_path"]))
                ;
            pageElements["TITLE"] = "Download - " + HttpUtility.HtmlEncode(file["title"]) + " - Confirm Deletion";
        }
        public static void pageDownload_Move(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin, ResultRow file)
        {
            if (!admin) return;
            string error = null;
            string folderid = request.Form["folderid"];
            if (folderid != null)
            {
                // Validate the new folder
                if (folderid.Length == 0)
                    error = "Invalid folder!";
                else if (folderid == file["folderid"])
                    error = "The newly selected Folder is the same as the current folder!";
                else
                {
                    // Grab the folder's details to validate it exists
                    Result ff = conn.Query_Read("SELECT folderid, path, title FROM downloads_folders WHERE folderid='" + Utils.Escape(folderid) + "'");
                    if (ff.Rows.Count != 1)
                        error = "Folder does not exist!";
                    else
                    {
                        // Attempt to move the file, else we'll roll-back our actions and inform the user
                        try
                        {
                            string dest = ff[0]["path"] + (ff[0]["path"].Length > 0 ? "/" : string.Empty) + ff[0]["title"] + "/" + file["title"]; // Destination path of the file, including the filename
                            File.Move(downloadsPath.Replace("\\", "/") + "/" + file["physical_path"], downloadsPath.Replace("\\", "/") + "/" + dest);
                            conn.Query_Execute("UPDATE downloads_files SET folderid='" + Utils.Escape(ff[0]["folderid"]) + "', physical_path='" + Utils.Escape(dest) + "' WHERE downloadid='" + Utils.Escape(file["downloadid"]) + "'");
                            conn.Disconnect();
                            response.Redirect(pageElements["URL"] + "/download/" + file["downloadid"]);
                        }
                        catch(Exception ex)
                        {
                            error = "Failed to move file - " + ex.Message + " (" + ex.GetBaseException().Message + ") - " + ex.StackTrace + "!";
                        }
                    }
                }
            }
            StringBuilder folders = new StringBuilder();
            foreach (ResultRow folder in conn.Query_Read("SELECT folderid, CONCAT(CONCAT(path, '/'), title) AS path FROM downloads_folders ORDER BY path ASC"))
                if(folder["folderid"] != file["folderid"])
                    folders.Append("<option value=\"").Append(folder["folderid"]).Append("\">").Append(HttpUtility.HtmlEncode(folder["path"])).Append("</option>");
            // Render page
            pageElements["CONTENT"] = Core.templates["downloads"]["download_move"]
                .Replace("%ERROR%", error != null ? Core.templates[pageElements["TEMPLATE"]]["error"].Replace("<ERROR>", HttpUtility.HtmlEncode(error)) : string.Empty)
                .Replace("%DOWNLOADID%", file["downloadid"])
                .Replace("%FOLDERS%", folders.ToString());
            pageElements["TITLE"] = "Download - " + HttpUtility.HtmlEncode(file["title"]) + " - Move";
        }
        public static void pageDownload_ResetDownloads(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin, ResultRow file)
        {
            if (request.Form["confirm"] != null)
            {
                conn.Query_Execute("DELETE FROM downloads WHERE downloadid='" + Utils.Escape(file["downloadid"]) + "'");
                conn.Disconnect();
                response.Redirect(pageElements["URL"] + "/download/" + file["downloadid"]);
            }
            pageElements["CONTENT"] = Core.templates["downloads"]["download_reset"]
                .Replace("%DOWNLOADID%", file["downloadid"]);
            pageElements["TITLE"] = "Download - " + HttpUtility.HtmlEncode(file["title"]) + " - Reset Downloads";
        }
        public static void pageDownload_Edit(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin, ResultRow file)
        {
            if (!admin) return;
            string error = null;
            string title = request.Form["title"];
            string description = request.Form["description"];
            if (title != null && description != null)
            {
                try
                {
                    if (title.Contains("*") || title.Contains("%") || title.Contains("|") || title.Contains("\\") || title.Contains("/") || title.Contains(":") || title.Contains("\"") || title.Contains("<") || title.Contains(">") || title.Contains("?"))
                        error = "Title has invalid character(s) - the following are disallowed: * | \\ / : \" < > ? %";
                    else
                    {
                        if (title != file["title"])
                        {
                            // Move the file to rename it
                            File.Move(downloadsPath.Replace("\\", "/") + "/" + file["physical_path"], downloadsPath.Replace("\\", "/") + "/" + file["physical_path"].Substring(0, file["physical_path"].Length - file["title"].Length) + title);
                        }
                        conn.Query_Execute("UPDATE downloads_files SET title='" + Utils.Escape(title) + "', description='" + Utils.Escape(description) + "' WHERE downloadid='" + Utils.Escape(file["downloadid"]) + "'");
                        conn.Disconnect();
                        response.Redirect(pageElements["URL"] + "/download/" + file["downloadid"]);
                    }
                }
                catch (Exception ex)
                {
                    error = "Failed to update file - " + ex.Message + " (" + ex.GetBaseException().Message + ") - " + ex.StackTrace + "!";
                }
            }
            pageElements["CONTENT"] = Core.templates["downloads"]["download_edit"]
                .Replace("%DOWNLOADID%", file["downloadid"])
                .Replace("%ERROR%", error != null ? Core.templates[pageElements["TEMPLATE"]]["error"].Replace("<ERROR>", HttpUtility.HtmlEncode(error)) : string.Empty)
                .Replace("%TITLE%", HttpUtility.HtmlEncode(title ?? file["title"]))
                .Replace("%DESCRIPTION%", HttpUtility.HtmlEncode(description ?? file["description"]));
            pageElements["TITLE"] = "Download - " + HttpUtility.HtmlEncode(file["title"]) + " - Edit Description";
        }
        static Bitmap pageDownload_Thumb_bbcode = null;
        public static void pageDownload_Thumb(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin, ResultRow file)
        {
            // Check the background has been loaded/cached
            if (pageDownload_Thumb_bbcode == null)
                pageDownload_Thumb_bbcode = (Bitmap)Image.FromFile(Core.basePath + "\\Content\\Images\\downloads\\bbcode.png");
            // Construct the image
            Bitmap output = (Bitmap)pageDownload_Thumb_bbcode.Clone();
            Graphics g = Graphics.FromImage(output);
            // Grab the associated icon
            byte[] rawIcon = null;
            if (file["iconid"].Length > 0 && file["iconid"] != "0")
            {
                // Grab the icon generated of the file
                Result data = conn.Query_Read("SELECT data FROM downloads_files_icons WHERE iconid='" + Utils.Escape(file["iconid"]) + "'");
                if (data.Rows.Count == 1)
                    rawIcon = data[0].GetByteArray("data");
            }
            if (rawIcon == null && file["extension"].Length > 0)
            {
                // Grab the icon associated with the extension
                Result data = conn.Query_Read("SELECT icon FROM downloads_ext_icons WHERE extension='" + Utils.Escape(file["extension"]) + "'");
                if (data.Rows.Count == 1)
                    rawIcon = data[0].GetByteArray("icon");
            }
            if (rawIcon == null)
            {
                // Associate unknown extension with this file
                if (pageDownloads_Icon_Unknown == null)
                    loadUknownIcon();
                rawIcon = pageDownloads_Icon_Unknown;
            }
            // Draw icon
            MemoryStream ms = new MemoryStream(rawIcon);
            Bitmap icon = (Bitmap)Image.FromStream(ms);
            // Apply fake rounded edges
            icon.SetPixel(0, 0, Color.Transparent); // Top-left
            icon.SetPixel(1, 0, Color.Transparent);
            icon.SetPixel(0, 1, Color.Transparent);

            icon.SetPixel(31, 0, Color.Transparent); // Top-right
            icon.SetPixel(30, 0, Color.Transparent);
            icon.SetPixel(31, 1, Color.Transparent);

            icon.SetPixel(0, 31, Color.Transparent); // Bottom-left
            icon.SetPixel(0, 30, Color.Transparent);
            icon.SetPixel(1, 31, Color.Transparent);

            icon.SetPixel(31, 31, Color.Transparent); // Bottom-right
            icon.SetPixel(31, 30, Color.Transparent);
            icon.SetPixel(30, 31, Color.Transparent);
            g.DrawImage(icon, 5, 8, 32, 32);
            icon.Dispose();
            ms.Dispose();
            // Draw title
            g.DrawString(file["title"], new Font("Arial", 12.0f, FontStyle.Regular, GraphicsUnit.Pixel), new SolidBrush(Color.White), 40, 8);
            // Draw downloads
            g.DrawString(conn.Query_Scalar("SELECT COUNT('') FROM (SELECT ip_addr FROM downloads WHERE downloadid='" + Utils.Escape(file["downloadid"]) + "' GROUP BY ip_addr) AS a").ToString() + " downloads", new Font("Arial", 12.0f, FontStyle.Regular, GraphicsUnit.Pixel), new SolidBrush(Color.LightGray), 40, 24);
            // Output it to the user
            ms = new MemoryStream();
            output.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            output.Dispose();
            response.ContentType = "image/png";
            response.BinaryWrite(ms.ToArray());
            ms.Dispose();
            conn.Disconnect();
            response.End();
        }
        public static void pageDownloads(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin)
        {
            switch (request.QueryString["1"])
            {
                default:
                case "view":
                    pageDownloads_View(pluginid, conn, ref pageElements, request, response, admin);
                    break;
                case "icon":
                    pageDownloads_Icon(pluginid, conn, ref pageElements, request, response);
                    break;
                case "run_indexer":
                    pageDownloads_RunIndexer(pluginid, conn, ref pageElements, request, response, admin);
                    break;
                case "upload":
                    pageDownloads_Upload(pluginid, conn, ref pageElements, request, response, admin);
                    break;
                case "delete_folder":
                    pageDownloads_DeleteFolder(pluginid, conn, ref pageElements, request, response, admin);
                    break;
            }
        }
        public static void pageDownloads_View(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin)
        {
            // Build the path being viewed
            string fullpath = string.Empty;
            string path = string.Empty;
            string title = string.Empty;
            string iS;
            for (int i = 2; i < 30; i++)
            {
                iS = i.ToString();
                if (request.QueryString[iS] != null)
                    fullpath += request.QueryString[iS] + "/";
                else
                    break;
            }
            if (fullpath.Length > 0)
            {
                fullpath = fullpath.Remove(fullpath.Length - 1, 1); // Remove tailing slash
                int endpoint = fullpath.LastIndexOf('/');
                if (endpoint == -1)
                {
                    title = fullpath;
                    path = string.Empty;
                }
                else
                {
                    title = fullpath.Substring(endpoint + 1);
                    path = fullpath.Substring(0, endpoint);
                }
            }
            // Get the current folderid
            Result baseFolder = conn.Query_Read("SELECT folderid, title FROM downloads_folders WHERE path='" + Utils.Escape(path) + "' AND title='" + Utils.Escape(title) + "'");
            if (baseFolder.Rows.Count != 1) return; // Path doesn't exist...
            // Get the current page
            int page;
            if (request.QueryString["pg"] == null || !int.TryParse(request.QueryString["pg"], out page) || page < 1) page = 1;
            int maxItems = Core.settings[SETTINGS_KEY].getInt(SETTINGS_ITEMS_PER_PAGE);
            StringBuilder items = new StringBuilder();
            // Display folders
            Result folders = conn.Query_Read("SELECT * FROM downloads_folders WHERE path='" + Utils.Escape(path.Length > 0 ? path + "/" + title : title) + "' AND title != '' ORDER BY title ASC LIMIT " + ((page * maxItems) - maxItems) + "," + maxItems);
            foreach (ResultRow folder in folders)
                items.Append(
                    Core.templates["downloads"]["view_item_folder"]
                    .Replace("%FOLDERID%", folder["folderid"])
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(folder["title"]))
                    .Replace("%PATH%", HttpUtility.HtmlEncode(folder["path"].Length > 0 ? folder["path"] + "/" + folder["title"] : folder["title"]))
                    .Replace("%DATETIME%", HttpUtility.HtmlEncode(folder["datetime"].Length > 0 ? folder["datetime"] : "no changes"))
                    .Replace("%DATETIME_SHORT%", folder["datetime"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(folder["datetime"])) : "no changes")
                    );
            // Display files
            Result files = conn.Query_Read("SELECT * FROM downloads_files WHERE folderid='" + Utils.Escape(baseFolder[0]["folderid"]) + "' ORDER BY title ASC LIMIT " + ((page * maxItems) - maxItems) + "," + maxItems);
            foreach (ResultRow file in files)
                items.Append(
                    Core.templates["downloads"]["view_item_file"]
                    .Replace("%DOWNLOADID%", file["downloadid"])
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(file["title"]))
                    .Replace("%TITLE_URL%", HttpUtility.UrlEncode(file["title"]))
                    .Replace("%EXTENSION%", HttpUtility.HtmlEncode(file["extension"]))
                    .Replace("%FILE_SIZE%", file["file_size"].Length == 0 ? "unknown" : HttpUtility.HtmlEncode(Misc.Plugins.getBytesString(float.Parse(file["file_size"]))))
                    .Replace("%ICONID%", file["iconid"].Length > 0 ? file["iconid"] : "0")
                    .Replace("%DATETIME%", HttpUtility.HtmlEncode(file["datetime"].Length > 0 ? file["datetime"] : "no changes"))
                    .Replace("%DATETIME_SHORT%", file["datetime"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(file["datetime"])) : "no changes")
                    );
            // Render page
            pageElements["CONTENT"] = Core.templates["downloads"]["view"]
                .Replace("%FOLDERID%", baseFolder[0]["folderid"])
                .Replace("%NAV%", getNavBar(fullpath))
                .Replace("%PATH%", HttpUtility.UrlEncode(fullpath).Replace("%2f", "/"))
                .Replace("%PAGE%", page.ToString())
                .Replace("%PAGE_PREVIOUS%", (page > 1 ? page - 1 : 1).ToString())
                .Replace("%PAGE_NEXT%", (page == int.MaxValue ? int.MaxValue : page + 1).ToString())
                .Replace("%ITEMS%", items.ToString());
            pageElements["TITLE"] = "Downloads - Viewing /" + HttpUtility.HtmlEncode(fullpath);
            // Set flags for page buttons
            if(page > 1) pageElements.setFlag("DOWNLOADS_PAGE_PREV");
            if(page != int.MaxValue && (files.Rows.Count == maxItems || folders.Rows.Count == maxItems)) pageElements.setFlag("DOWNLOADS_PAGE_NEXT");
            // Set flag for admin
            if(admin)
                pageElements.setFlag("DOWNLOADS_ADMIN");
            // Add CSS
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/Downloads.css", ref pageElements);
        }
        public static byte[] pageDownloads_Icon_Unknown = null;
        public static void pageDownloads_Icon(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string iconid = request.QueryString["2"];
            if (iconid == null || iconid.Length == 0) return;
            byte[] data = null;
            if (iconid != "0")
            {
                // Attempt to fetch the icon from the database
                Result icon = conn.Query_Read("SELECT data FROM downloads_files_icons WHERE iconid='" + Utils.Escape(iconid) + "'");
                if (icon.Rows.Count == 1)
                    data = icon[0].GetByteArray("data");
            }
            if (data == null)
            {
                // No icon has been set; check if an extension was specified, we'll use that - else the unknown file-type
                string ext = request.QueryString["3"];
                if (ext != null && ext.Length > 0 && ext.Length < 10)
                {
                    Result icon = conn.Query_Read("SELECT icon FROM downloads_ext_icons WHERE extension='" + Utils.Escape(ext) + "'");
                    if (icon.Rows.Count == 1)
                        data = icon[0].GetByteArray("icon");
                }
            }
            if (data == null)
            {
                // We'll pass back the unknown file-type - this should be cached as a static byte array, else we'll cache it now
                if (pageDownloads_Icon_Unknown == null) loadUknownIcon();
                data = pageDownloads_Icon_Unknown;
            }
            // Set the content-type, write the image data and end the response
            conn.Disconnect();
            response.ContentType = "image/png";
            response.BinaryWrite(data);
            response.End();
        }
        private static void loadUknownIcon()
        {
            Bitmap braw = (Bitmap)Image.FromFile(Core.basePath + "\\Content\\Images\\downloads\\unknown_file.png");
            MemoryStream ms = new MemoryStream();
            braw.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            braw.Dispose();
            pageDownloads_Icon_Unknown = ms.ToArray();
            ms.Dispose();
        }
        public static void pageDownloads_RunIndexer(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin)
        {
            if (!admin) return;
            indexMainFolder();
            pageElements["TITLE"] = "Downloads - Run Indexer - Confirmaton";
            pageElements["CONTENT"] = Core.templates["downloads"]["run_indexer"];
        }
        public static void pageDownloads_Upload(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin)
        {
            // Check the user is an admin
            if (!admin) return;
            string folderid = request.QueryString["2"];
            if (folderid == null || folderid.Length == 0) return; // Invalid folderid
            // Fetch folder details
            Result folderRaw = conn.Query_Read("SELECT folderid, title, path FROM downloads_folders WHERE folderid='" + Utils.Escape(folderid) + "'");
            if (folderRaw.Rows.Count != 1) return; // No folder found
            ResultRow folder = folderRaw[0];
            string path = folder["path"] + (folder["title"].Length > 0 ? (folder["path"].Length > 0 ? "/" : string.Empty) + folder["title"] : string.Empty);
            string physicalPath = downloadsPath.Replace("\\", "/") + "/" + path;
            string error = null;
            HttpPostedFile file = request.Files["file_upload"];
            if (file != null)
            {
                string dest = physicalPath + "/" + file.FileName;
                // Check the file doesn't already exist
                if (file.ContentLength == 0)
                    error = "No file uploaded!";
                else if (File.Exists(dest))
                    error = "Cannot save file - already exists at '" + dest + "'!";
                else
                {
                    string downloadid = null;
                    try
                    {
                        string title = file.FileName;
                        string extension = file.FileName.LastIndexOf('.') != -1 ? file.FileName.Substring(file.FileName.LastIndexOf('.') + 1) : string.Empty;
                        // Create an entry for the file
                        downloadid = conn.Query_Scalar("INSERT INTO downloads_files (folderid, title, extension, physical_path, datetime) VALUES('" + Utils.Escape(folder["folderid"]) + "', '" + Utils.Escape(title) + "', '" + Utils.Escape(extension) + "', '" + Utils.Escape(physicalPath + "/" + title) + "', NOW()); SELECT LAST_INSERT_ID();").ToString();
                        // Save the file
                        file.SaveAs(dest);
                        // Update the file-size and iconid
                        string iconid = null;
                        processFileIcon(ref iconid, dest, extension);
                        FileInfo fi = new FileInfo(dest);
                        conn.Query_Execute("UPDATE downloads_files SET file_size='" + Utils.Escape(file.ContentLength.ToString()) + "', iconid=" + (iconid != null ? "'" + Utils.Escape(iconid) + "'" : "NULL") + " WHERE downloadid='" + Utils.Escape(downloadid) + "'");
                        // End the response
                        conn.Disconnect();
                        response.Redirect(pageElements["URL"] + "/download/" + downloadid);
                    }
                    catch (Exception ex)
                    {
                        if (downloadid != null)
                            // Remove the download from the database - an error occurred
                            conn.Query_Execute("DELETE FROM downloads_files WHERE downloadid='" + Utils.Escape(downloadid) + "'");
                        // The error will be pretty detailed, since the user is an admin and therefore most likely the site administrator
                        error = "Failed to handle uploaded-file: " + ex.Message + "(" + ex.GetBaseException().Message + ") - " + ex.StackTrace;
                    }
                }
            }
            pageElements["CONTENT"] = Core.templates["downloads"]["upload"]
                .Replace("%ERROR%", error != null ? Core.templates[pageElements["TEMPLATE"]]["error"].Replace("<ERROR>", HttpUtility.HtmlEncode(error)) : string.Empty)
                .Replace("%FOLDERID%", folderid)
                .Replace("%PATH%", HttpUtility.HtmlEncode(path))
                .Replace("%PHYSICAL_PATH%", HttpUtility.HtmlEncode(physicalPath))
                ;
            pageElements["TITLE"] = "Downloads - Upload";
        }
        public static void pageDownloads_DeleteFolder(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool admin)
        {
            // Check the user is an admin
            if (!admin) return;
            // Ensure the folderid is somewhat valid
            if (request.QueryString["2"] == null || request.QueryString["2"].Length == 0) return;
            // Retrieve details about the folder
            Result folderData = conn.Query_Read("SELECT folderid, path, title FROM downloads_folders WHERE folderid='" + Utils.Escape(request.QueryString["2"]) + "'");
            if (folderData.Rows.Count != 1) return; // Folder was not found
            ResultRow folder = folderData[0];
            // Check the user has confirmed to delete it
            string path = folder["path"] + (folder["title"].Length > 0 ? (folder["path"].Length > 0 ? "/" : string.Empty) + folder["title"] : string.Empty);
            string physicalPath = downloadsPath.Replace("\\", "/") + "/" + path;
            if (request.Form["confirm"] != null)
            {
                // Delete file structure
                try
                {
                    Directory.Delete(physicalPath, true);
                }
                catch { }
                // Delete database structure
                conn.Query_Execute("DELETE FROM downloads_folders WHERE folderid='" + Utils.Escape(folder["folderid"]) + "' OR path='" + Utils.Escape(path) + "' OR path LIKE '" + Utils.Escape(path + "/") + "%';" + QUERY_ICONCACHE_CLEANUP);
                // End the response
                conn.Disconnect();
                response.Redirect(pageElements["URL"] + "/downloads");
            }
            else
            {
                // Build a list of files and folders that will be deleted due to this action
                StringBuilder items = new StringBuilder();
                Result folders = conn.Query_Read("SELECT path, title FROM downloads_folders WHERE folderid='" + Utils.Escape(folder["folderid"]) + "' OR path='" + Utils.Escape(path) + "' OR path LIKE '" + Utils.Escape(path + "/") + "%'");
                items.Append("Folders:<br />");
                if (folders.Rows.Count > 0)
                    foreach (ResultRow f in folders)
                        items.Append("- ").Append(f["path"] + (f["path"].Length > 0 ? "/" : string.Empty) + f["title"]).Append("<br />");
                else
                    items.Append("- none -<br />");
                Result files = conn.Query_Read("SELECT physical_path FROM downloads_files WHERE folderid IN (SELECT folderid FROM downloads_folders WHERE folderid='" + Utils.Escape(folder["folderid"]) + "' OR path='" + Utils.Escape(path) + "' OR path LIKE '" + Utils.Escape(path + "/") + "%')");
                items.Append("Files:<br />");
                if (files.Rows.Count > 0)
                    foreach (ResultRow file in files)
                        items.Append("- ").Append(HttpUtility.HtmlEncode(file["physical_path"])).Append("<br />");
                else
                    items.Append("- none -<br />");
                // Render page
                pageElements["CONTENT"] = Core.templates["downloads"]["delete_folder"]
                    .Replace("%FOLDERID%", folder["folderid"])
                    .Replace("%PATH%", HttpUtility.HtmlEncode(path))
                    .Replace("%PHYSICAL_PATH%", HttpUtility.HtmlEncode(physicalPath))
                    .Replace("%ITEMS%", items.ToString())
                    ;
                pageElements["TITLE"] = "Downloads - Delete Folder - Confirm Action";
            }
        }
        #endregion

        #region "Methods - Watcher"
        private static FileSystemWatcher fsw = null;
        public static void watcherEnable()
        {
            if (!Core.settings[SETTINGS_KEY].getBool(SETTINGS_FILEWATCHER_ENABLED))
                return;
            fsw = new FileSystemWatcher(downloadsPath, "*");
            fsw.IncludeSubdirectories = true;
            fsw.EnableRaisingEvents = true;
            fsw.Changed += new FileSystemEventHandler(fsw_Changed);
            fsw.Created += new FileSystemEventHandler(fsw_Created);
            fsw.Deleted += new FileSystemEventHandler(fsw_Deleted);
            fsw.Renamed += new RenamedEventHandler(fsw_Renamed);
        }
        public static void watcherDisable()
        {
            lock (fsw)
            {
                fsw.Changed -= new FileSystemEventHandler(fsw_Changed);
                fsw.Created -= new FileSystemEventHandler(fsw_Created);
                fsw.Deleted -= new FileSystemEventHandler(fsw_Deleted);
                fsw.Renamed -= new RenamedEventHandler(fsw_Renamed);
            }
            fsw.Dispose();
            fsw = null;
        }
        static void fsw_Renamed(object sender, RenamedEventArgs e)
        {
            try
            {
                StringBuilder queryBuffer = new StringBuilder();
                // Determine if a directory or file was renamed
                if (Directory.Exists(e.FullPath))
                {
                    string oldRelPath, oldTitle, newRelPath, title;
                    // Update the folder that has been changed
                    newRelPath = e.FullPath.Replace("\\", "/").Substring(downloadsPath.Length + 1);
                    oldRelPath = e.OldFullPath.Replace("\\", "/").Substring(downloadsPath.Length + 1);
                    int nrpli = newRelPath.LastIndexOf('/');
                    int orpli = oldRelPath.LastIndexOf('/');
                    title = newRelPath.Substring(nrpli + 1);
                    oldTitle = oldRelPath.Substring(orpli + 1);
                    newRelPath = nrpli == -1 ? string.Empty : newRelPath.Substring(0, nrpli);
                    oldRelPath = orpli == -1 ? string.Empty : oldRelPath.Substring(0, orpli);
                    Core.globalConnector.Query_Execute("UPDATE downloads_folders SET title='" + Utils.Escape(title) + "', path='" + Utils.Escape(newRelPath) + "' WHERE path='" + Utils.Escape(oldRelPath) + "' AND title='" + Utils.Escape(oldTitle) + "'");
                    // Process directories
                    string directory;
                    string[] directories = Directory.GetDirectories(e.FullPath, "*", SearchOption.AllDirectories);
                    for (int i = 0; i < directories.Length; i++)
                    {
                        directory = directories[i].Replace("\\", "/");
                        newRelPath = directory.Substring(downloadsPath.Length + 1);
                        oldRelPath = e.OldFullPath.Replace("\\", "/").Substring(downloadsPath.Length + 1) + "/" + directory.Substring(e.FullPath.Length + 1);
                        nrpli = newRelPath.LastIndexOf('/');
                        orpli = oldRelPath.LastIndexOf('/');
                        title = newRelPath.Substring(nrpli + 1);
                        oldTitle = oldRelPath.Substring(orpli + 1);
                        newRelPath = nrpli == -1 ? string.Empty : newRelPath.Substring(0, nrpli);
                        oldRelPath = orpli == -1 ? string.Empty : oldRelPath.Substring(0, orpli);
                        queryBuffer.Append("UPDATE downloads_folders SET path='" + Utils.Escape(newRelPath) + "' WHERE path='" + Utils.Escape(oldRelPath) + "' AND title='" + Utils.Escape(oldTitle) + "';");
                    }
                    if (queryBuffer.Length > 0)
                    {
                        Core.globalConnector.Query_Execute(queryBuffer.ToString());
                        queryBuffer.Remove(0, queryBuffer.Length);
                    }
                    // Process files
                    directories = Directory.GetFiles(e.FullPath, "*", SearchOption.AllDirectories);
                    string file;
                    for (int i = 0; i < directories.Length; i++)
                    {
                        file = directories[i];
                        newRelPath = file; // Ignore variable names, these are the new and old FULL PATHS!!!! - just efficient to reuse
                        oldRelPath = e.OldFullPath + "/" + file.Substring(e.FullPath.Length + 1);
                        fswFileRenamed(oldRelPath, newRelPath, ref queryBuffer);
                    }
                }
                else
                    fswFileRenamed(e.OldFullPath, e.FullPath, ref queryBuffer);
                if (queryBuffer.Length > 0)
                    Core.globalConnector.Query_Execute(queryBuffer.ToString());
            }
            catch { }
        }
        static void fswFileRenamed(string oldPath, string newPath, ref StringBuilder queryBuffer)
        {
            string relativePath = newPath == downloadsPath ? string.Empty : newPath.Replace("\\", "/").Substring(downloadsPath.Length + 1);
            string title = relativePath.Length > 0 ? relativePath.Substring(relativePath.LastIndexOf('/') + 1) : string.Empty;
            string folderPath = relativePath.Substring(0, relativePath.Length - (title.Length + 1));
            string folderTitle = folderPath.Substring(folderPath.LastIndexOf('/') + 1);
            string folderid = getFolderId(folderPath, folderTitle);
            queryBuffer.Append("UPDATE downloads_files SET physical_path='" + Utils.Escape(relativePath) + "', title='" + Utils.Escape(title) + "', folderid='" + Utils.Escape(folderid) + "' WHERE physical_path='" + Utils.Escape(oldPath.Replace("\\", "/").Substring(downloadsPath.Length + 1)) + "'; UPDATE downloads_folders SET datetime=NOW() WHERE folderid='" + Utils.Escape(folderid) + "';");
        }
        static void fsw_Deleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                string relPath = e.FullPath.Substring(downloadsPath.Length + 1).Replace("\\", "/");
                int rpli = relPath.LastIndexOf('/');
                string path = rpli != -1 ? relPath.Substring(0, rpli) : string.Empty;
                string title = rpli != -1 ? relPath.Substring(rpli + 1) : relPath;
                Core.globalConnector.Query_Execute("DELETE FROM downloads_files WHERE physical_path='" + Utils.Escape(relPath) + "'; DELETE FROM downloads_folders WHERE path='" + Utils.Escape(path) + "' AND title='" + Utils.Escape(title) + "';" + QUERY_ICONCACHE_CLEANUP);
            }
            catch { }
        }
        static void fsw_Created(object sender, FileSystemEventArgs e)
        {
            try
            {
                Thread.Sleep(500); // We sleep for 500 m/s in-case this event was caused by the website and hence we wait for database changes
                string relPath = e.FullPath.Substring(downloadsPath.Length + 1).Replace("\\", "/");
                string title = relPath.Substring(relPath.LastIndexOf('/') + 1);
                if (title == "Thumbs.db") return;
                if (Directory.Exists(e.FullPath))
                    // We're adding a directory
                    getFolderId(relPath, title);
                else
                {
                    // We're adding a file
                    if (Core.globalConnector.Query_Count("SELECT COUNT('') FROM downloads_files WHERE physical_path='" + Utils.Escape(relPath) + "'") == 0)
                    {
                        // File doesn't exist, add it
                        string extension = title.Contains(".") && !title.EndsWith(".") ? title.Substring(title.LastIndexOf('.') + 1) : string.Empty;
                        string folderpath = relPath.Length == title.Length ? string.Empty : relPath.Substring(0, relPath.Length - (title.Length + 1));
                        string foldertitle = folderpath.Length > 0 ? folderpath.Substring(folderpath.LastIndexOf('/') + 1) : string.Empty;
                        string folderid = getFolderId(folderpath, foldertitle);
                        FileInfo fi = new FileInfo(e.FullPath);
                        string iconid = null;
                        processFileIcon(ref iconid, e.FullPath, extension);
                        Core.globalConnector.Query_Execute("INSERT INTO downloads_files (folderid, title, extension, physical_path, file_size, iconid, datetime) VALUES('" + Utils.Escape(folderid) + "', '" + Utils.Escape(title) + "', '" + Utils.Escape(extension) + "', '" + Utils.Escape(relPath) + "', '" + Utils.Escape(fi.Length.ToString()) + "', " + (iconid == null ? "NULL" : "'" + Utils.Escape(iconid) + "'") + ", NOW()); UPDATE downloads_folders SET datetime=NOW() WHERE folderid='" + Utils.Escape(folderid) + "';");
                    }
                }
            }
            catch { }
        }
        static void fsw_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (!Directory.Exists(e.FullPath))
                {
                    if (e.FullPath.Replace("\\", "/").EndsWith("/Thumbs.db")) return;
                    // Get the file that has changed
                    Result fileChanged = Core.globalConnector.Query_Read("SELECT downloadid FROM downloads_files WHERE physical_path='" + Utils.Escape(e.FullPath.Substring(downloadsPath.Length + 1).Replace("\\", "/")) + "'");
                    if (fileChanged.Rows.Count == 1)
                    {
                        // Rebuild its icon
                        string iconid = null;
                        processFileIcon(ref iconid, e.FullPath, e.FullPath.LastIndexOf('.') == -1 ? string.Empty : e.FullPath.Substring(e.FullPath.LastIndexOf('.') + 1));
                        // Update the icon and file-size
                        FileInfo f = new FileInfo(e.FullPath);
                        Core.globalConnector.Query_Execute("UPDATE downloads_files SET file_size='" + Utils.Escape(f.Length.ToString()) + "', iconid=" + (iconid == null ? "NULL" : "'" + Utils.Escape(iconid) + "'") + ", datetime=NOW() WHERE downloadid='" + Utils.Escape(fileChanged[0]["downloadid"]) + "';" + QUERY_ICONCACHE_CLEANUP);
                    }
                }
            }
            catch { }
        }
        #endregion

        #region "Methods - Indexer"
        public static Thread th = null;
        public static void indexMainFolder()
        {
            if (th != null && (th.ThreadState == ThreadState.WaitSleepJoin || th.ThreadState == ThreadState.Running)) return;
            th = new Thread(threadIndexer);
            th.Priority = ThreadPriority.Lowest;
            th.Start();
        }
        private static void threadIndexer()
        {
            try
            {
                int interval = Core.settings[SETTINGS_KEY].getInt(SETTINGS_THREAD_INTERVAL);
                StringBuilder fileInsertBuffer;
                const string fileInsertBufferQuery = "INSERT INTO downloads_files (folderid, title, extension, physical_path, file_size, iconid, datetime) VALUES";
                StringBuilder deletionBufferFolder;
                const string deletionBufferFolderQuery = "DELETE FROM downloads_folders WHERE ";
                StringBuilder deletionBufferFile;
                const string deletionBufferFileQuery = "DELETE FROM downloads_files WHERE ";
                while (true)
                {

                    // Check each folder in the database still exists
                    deletionBufferFolder = new StringBuilder(deletionBufferFolderQuery);
                    foreach (ResultRow dFolder in Core.globalConnector.Query_Read("SELECT folderid, path FROM downloads_folders"))
                        if (!Directory.Exists(downloadsPath + (dFolder["path"].Length == 0 ? string.Empty : "/" + dFolder["path"])))
                            deletionBufferFolder.Append("folderid='").Append(Utils.Escape(dFolder["folderid"])).Append("' OR ");
                    if (deletionBufferFolder.Length > deletionBufferFolderQuery.Length)
                        Core.globalConnector.Query_Execute(deletionBufferFolder.Remove(deletionBufferFolder.Length - 4, 4).ToString());

                    // Check every file in the database still exists
                    deletionBufferFile = new StringBuilder(deletionBufferFileQuery);
                    foreach (ResultRow dFile in Core.globalConnector.Query_Read("SELECT downloadid, physical_path FROM downloads_files"))
                        if (!File.Exists(downloadsPath + "/" + dFile["physical_path"]))
                            deletionBufferFile.Append("downloadid='").Append(Utils.Escape(dFile["downloadid"])).Append("' OR ");
                    if (deletionBufferFile.Length > deletionBufferFileQuery.Length)
                        Core.globalConnector.Query_Execute(deletionBufferFile.Remove(deletionBufferFile.Length - 4, 4).ToString());

                    // Begin indexing each file
                    fileInsertBuffer = new StringBuilder(fileInsertBufferQuery);
                    indexDirectory(downloadsPath, ref fileInsertBuffer);
                    if (fileInsertBuffer.Length > fileInsertBufferQuery.Length)
                        Core.globalConnector.Query_Execute(fileInsertBuffer.Remove(fileInsertBuffer.Length - 1, 1).Append(";").Append(QUERY_ICONCACHE_CLEANUP).ToString());

                    // Sleep
                    Thread.Sleep(interval);
                }
            }
            catch { }
        }
        public static void indexDirectory(string path, ref StringBuilder fileInsertBuffer)
        {
            // Generate the relative path and title for the current directory
            string relativePath = path == downloadsPath ? string.Empty : path.Replace("\\", "/").Substring(downloadsPath.Length + 1);
            string title = relativePath.Length == 0 ? string.Empty : relativePath.Substring(relativePath.LastIndexOf('/') + 1);
            // Ensure an entry exists for the directory by getting the folderid
            string folderid = getFolderId(relativePath, title);
            // Get all the actual files
            string[] files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
            string file, fTitle, fExtension, fRelPath, iconid;
            int fExtensionIndex;
            for(int i = 0; i < files.Length; i++)
            {
                file = files[i].Replace("\\", "/");
                fRelPath = file.Substring(downloadsPath.Length + 1);
                if (!file.EndsWith("/Thumbs.db") && Core.globalConnector.Query_Count("SELECT COUNT('') FROM downloads_files WHERE physical_path='" + Utils.Escape(fRelPath) + "'") == 0)
                {
                    try
                    {
                        fTitle = file.Substring(file.LastIndexOf('/') + 1);
                        fExtension = (fExtensionIndex = fTitle.LastIndexOf('.')) != -1 && fExtensionIndex + 1 < fTitle.Length ? fTitle.Substring(fExtensionIndex + 1) : string.Empty;
                        FileInfo fi = new FileInfo(file);
                        // Check if file can have an icon; this process can fail
                        iconid = null;
                        processFileIcon(ref iconid, file, fExtension);
                        // Append data for appending
                        fileInsertBuffer.Append("('" + Utils.Escape(folderid) + "', '" + Utils.Escape(fTitle) + "', '" + Utils.Escape(fExtension) + "', '" + Utils.Escape(fRelPath) + "', '" + Utils.Escape(fi.Length.ToString()) + "', " + (iconid == null ? "NULL" : "'" + Utils.Escape(iconid) + "'") + ", NOW()),");
                    }
                    catch { }
                }
            }
            // Index the sub-directories
            foreach (string directory in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
                indexDirectory(directory, ref fileInsertBuffer);
        }
        /// <summary>
        /// Handles the file-icon for a file; this method will set the iconid parameter with
        /// the icon for this image; this value can be null.
        /// </summary>
        /// <param name="iconid">Referenced variable for returning the ID.</param>
        /// <param name="file">Full path of the file.</param>
        /// <param name="fExtension">Extension without period.</param>
        private static void processFileIcon(ref string iconid, string file, string fExtension)
        {
            try
            {
                Bitmap img = null;
                switch (fExtension)
                {
                    case "jpg":
                    case "jpeg":
                    case "gif":
                    case "bmp":
                    case "png":
                        img = getThumb((Bitmap)Image.FromFile(file));
                        break;
                    case "exe":
                    case "dll":
                        img = getThumb(Icon.ExtractAssociatedIcon(file).ToBitmap());
                        break;
                }
                // If image is not null, generate hash and check if it exists in the image-store; if so we'll use its iconid, else we'll add it
                if (img != null)
                {
                    // Convert image to byte array
                    MemoryStream ms = new MemoryStream();
                    img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    img.Dispose();
                    img = null;
                    byte[] data = ms.ToArray();
                    ms.Dispose();
                    ms = null;
                    // Generate SHA1 hash
                    SHA1CryptoServiceProvider sha = new SHA1CryptoServiceProvider();
                    string hash = Convert.ToBase64String(sha.ComputeHash(data));
                    // Check the database to see if the hash already exists, if so we'll use the provided id - else we'll make a new entry
                    iconid = (Core.globalConnector.Query_Scalar("SELECT iconid FROM downloads_files_icons WHERE hash='" + Utils.Escape(hash) + "'") ?? string.Empty).ToString();
                    if (iconid.Length == 0)
                    {
                        // Create new entry
                        Dictionary<string, object> qData = new Dictionary<string, object>();
                        qData.Add("hash", hash);
                        qData.Add("image", data);
                        iconid = Core.globalConnector.Query_Scalar_Parameters("INSERT INTO downloads_files_icons (hash, data) VALUES(@hash, @image); SELECT LAST_INSERT_ID();", qData).ToString();
                    }
                }
            }
            catch { }
        }
        /// <summary>
        /// Returns the 32x32 thumbnail of the provided image, with scaling.
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        private static Bitmap getThumb(Bitmap src)
        {
            int newWidth;
            int newHeight;
            if (src.Width > src.Height)
            {
                newWidth = 32;
                newHeight = (int)((float)src.Height / ((float)src.Width / 32.0f));
            }
            else
            {
                newHeight = 32;
                newWidth = (int)((float)src.Width / ((float)src.Height / 32.0f));
            }
            Bitmap img = new Bitmap(32, 32);
            Graphics g = Graphics.FromImage(img);
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            g.DrawImage(src, 16 - (newWidth / 2), 16 - (newHeight / 2), newWidth, newHeight);
            g.Dispose();
            src.Dispose();
            return img;
        }
        #endregion

        #region "Methods - Shared"
        /// <summary>
        /// Gets the folderid of a relative path; if a folderid does not exist, an entry is created and the folderid returned.
        /// </summary>
        /// <param name="relativePath">The relative path of the folder, including the title.</param>
        /// <param name="title">The folder name/title.</param>
        /// <returns></returns>
        public static string getFolderId(string relativePath, string title)
        {
            string folderid;
            string basePath = relativePath.IndexOf('/') != -1 ? relativePath.Substring(0,  relativePath.LastIndexOf('/')) : string.Empty;
            // The directory has changed; check if a folder already exists, else we'll fetch a new one
            if ((folderid = (Core.globalConnector.Query_Scalar("SELECT folderid FROM downloads_folders WHERE path='" + Utils.Escape(basePath) + "' AND title='" + Utils.Escape(title) + "'") ?? string.Empty).ToString()).Length == 0)
                folderid = Core.globalConnector.Query_Scalar("INSERT INTO downloads_folders (title, path) VALUES('" + Utils.Escape(title) + "', '" + Utils.Escape(basePath) + "'); SELECT LAST_INSERT_ID();").ToString();
            return folderid;
        }
        /// <summary>
        /// Builds a navigation bar from a path, for better user navigation.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string getNavBar(string path)
        {
            StringBuilder navBar = new StringBuilder();
            navBar.Append(Core.templates["downloads"]["nav_item"].Replace("%PATH%", "").Replace("%TEXT%", "/"));
            string pathBuffer = string.Empty;
            foreach (string node in path.Split('/'))
            {
                if (node.Length > 0)
                {
                    if (pathBuffer.Length > 0)
                        navBar.Append("/"); // Append separator
                    navBar.Append(
                        Core.templates["downloads"]["nav_item"].Replace("%PATH%", pathBuffer.Length > 0 ? pathBuffer + "/" + node : node).Replace("%TEXT%", node)
                        );
                    pathBuffer += node;
                }
            }
            return navBar.ToString();
        }
        #endregion

        #region "Methods - BBCode"
        public static void format(ref StringBuilder text, HttpRequest request, HttpResponse response, Connector conn, Misc.PageElements pageElements, bool formattingText, bool formattingObjects, int currentTree)
        {
            if (formattingObjects)
            {
                // Download button
                foreach (Match m in Regex.Matches(text.ToString(), @"\[download=([0-9]+)\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<a href=\"" + pageElements["URL"] + "/download/" + m.Groups[1].Value + "\"><img src=\"" + pageElements["URL"] + "/download/" + m.Groups[1].Value + "/embed\" alt=\"Download file\" /></a>");
            }
        }
        #endregion
    }
}