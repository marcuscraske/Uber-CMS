 ﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS
 * File:                    /App_Code/Plugins/Base.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * The base class for a plugin; this is used for enum referencing and to display the
 * methods available for developers.
 */
using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Drawing;
using UberLib.Connector;
using System.Xml;

namespace UberCMS.Plugins.UberMedia
{
    public static class Base
    {
        #region "Constants - Settings"
        public const string SETTINGS_KEY = "ubermedia";
        public const string SETTINGS_ROTTEN_TOMATOES_API_KEY = "rotten_tomatoes_api_key";
        public const string SETTINGS_TERMINALS_AUTOMATIC_REGISTER = "terminals_automatic_register";
        public const string SETTINGS_THUMBNAIL_WIDTH = "thumbnail_width";
        public const string SETTINGS_THUMBNAIL_HEIGHT = "thumbnail_height";
        public const string SETTINGS_THUMBNAIL_SCREENSHOT_TIME = "thumbnail_screenshot_time";
        public const string SETTINGS_THUMBNAIL_THREADS = "thumbnail_threads";
        public const string SETTINGS_THUMBNAIL_THREAD_TTL = "thumbnail_thread_ttl";
        public const string SETTINGS_CONVERSION_THREADS = "conversion_threads";
        public const string SETTINGS_CONVERSION_TIMEOUT = "conversion_timeout";
        public const string SETTINGS_TEMPLATE_GLOBAL = "template_global";
        #endregion

        #region "Constants"
        public const int PHYSICAL_FOLDER_TITLE_MIN = 1;
        public const int PHYSICAL_FOLDER_TITLE_MAX = 50;
        public const int PHYSICAL_FOLDER_PATH_MIN = 3;
        public const int PHYSICAL_FOLDER_PATH_MAX = 248;
        public const int TAG_TITLE_MIN = 1;
        public const int TAG_TITLE_MAX = 35;
        public const int TERMINAL_TITLE_MIN = 1;
        public const int TERMINAL_TITLE_MAX = 28;
        public const int PAGE_REQUESTS_ITEMSPERPAGE = 5;
        public const string TEMPLATE = "ubermedia";
        #endregion

        #region "Enums"
        public enum States
        {
            Playing = 1,
            Paused = 3,
            Stopped = 5,
            Error = 7,
            Loading = 9,
            WaitingForInput = 11,
            Idle = 13,
        }
        #endregion

        #region "Methods - Plugin Handlers"
        public static string enable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Install SQL
            if((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Install.sql", conn)) != null)
                return error;
            // Install bin files
            if ((error = Misc.Plugins.contentInstall(basePath + "\\Bin", Core.basePath + "\\Bin")) != null)
                return error;
            // Install pre-processor directives
            if ((error = Misc.Plugins.preprocessorDirective_Add("UBER_MEDIA")) != null)
                return error;
            // Install content
            if ((error = Misc.Plugins.contentInstall(basePath + "\\Content")) != null)
                return error;
            // Install templates
            if ((error = Misc.Plugins.templatesInstall(basePath + "\\Templates", conn)) != null)
                return error;
            // Install settings
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_ROTTEN_TOMATOES_API_KEY, string.Empty, "Your API key for Rotten Tomatoes to retrieve third-party media information.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_TERMINALS_AUTOMATIC_REGISTER, "1", "Specifies if terminals can self-register themselves to your media library; this allows for easier installation of terminals/media-computers.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_THUMBNAIL_WIDTH, "120", "The width of generated thumbnails for media items.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_THUMBNAIL_HEIGHT, "90", "The height of generated thumbnails for media items.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_THUMBNAIL_SCREENSHOT_TIME, "90", "The number of seconds from which a thumbnail snapshot should derive from within a media item.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_THUMBNAIL_THREADS, "4", "The number of threads simultaneously generating thumbnails for media items.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_THUMBNAIL_THREAD_TTL, "40000", "The maximum amount of milliseconds for a thumbnail to generate an image; if exceeded, the thumbnail generation is terminated.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_CONVERSION_THREADS, "2", "The number of conversions to take place simultaneously.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_CONVERSION_TIMEOUT, "14400", "The maximum seconds for a conversion to successfully complete.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_TEMPLATE_GLOBAL, "0", "Boolean value; if true, the Uber Media template is applied globally to every single page. This is not recommended for CMS's with other non-Uber Media plugins.", false);
            // Reserve URLs
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "home", "ubermedia", "terminal" }, conn)) != null)
                return error;

            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Unreserve URLs
            if ((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;
            // Uninstall bin files
            if ((error = Misc.Plugins.contentUninstall(basePath + "\\Bin", Core.basePath + "\\Bin")) != null)
                return error;
            // Uninstall pre-processor directives
            if ((error = Misc.Plugins.preprocessorDirective_Remove("UBER_MEDIA")) != null)
                return error;
            // Uninstall content
            if ((error = Misc.Plugins.contentUninstall(basePath + "\\Content")) != null)
                return error;
            // Uninstall templates
            if ((error = Misc.Plugins.templatesUninstall(TEMPLATE, conn)) != null)
                return error;

            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Uninstall SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Uninstall.sql", conn)) != null)
                return error;
            // Remove settings
            Core.settings.removeCategory(conn, SETTINGS_KEY);

            return null;
        }
        public static string cmsStart(string pluginid, Connector conn)
        {
            // Start the thumbnail service
            ThumbnailGeneratorService.serviceStart();
            // Start the film information service
            FilmInformation.cacheStart();
            // Start indexing each drive
            Indexer.indexAllDrives();
            // Start conversion service
            ConversionService.startService();
            return null;
        }
        public static string cmsStop(string pluginid, Connector conn)
        {
            // Terminate thread pool of indexer
            Indexer.terminateThreadPool();
            // Terminate thumbnail service
            ThumbnailGeneratorService.serviceStop();
            // Terminate film information service
            FilmInformation.cacheStop();
            // Terminate conversion service
            ConversionService.stopService();
            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            StringBuilder contentLeft = new StringBuilder();
            StringBuilder contentRight = new StringBuilder();
            // Delegate the request to the responsible page
            switch (request.QueryString["page"])
            {
                case "home":
                    pageHome(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "ubermedia":
                    switch (request.QueryString["1"])
                    {
                        case "browse":
                            pageBrowse(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "youtube":
                            pageYouTube(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "newest":
                            pageNewest(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "popular":
                            pagePopular(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "item":
                            pageItem(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "convert":
                            pageConvert(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "search":
                            pageSearch(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "change":
                            pageChange(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "shutdown":
                            pageShutdown(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "restart":
                            pageRestart(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "credits":
                            pageCredits(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "status":
                            pageStatus(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "thumbnail":
                            pageThumbnail(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "admin":
                            pageAdmin(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                        case "control":
                            pageControl(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                            break;
                    }
                    break;
                case "terminal":
                    pageTerminal(request, response, conn);
                    break;
            }
            if (contentRight.Length != 0) pageElements["CONTENT"] = string.Empty; // To stop 404
            else return; // 404 - no content
            // Change the template to ubermedia
            pageElements["TEMPLATE"] = "ubermedia";
            // Build media computer dropdown
            StringBuilder mediaComputers = new StringBuilder();
            foreach (ResultRow mediacomputer in conn.Query_Read("SELECT title, terminalid FROM um_terminals ORDER BY title ASC"))
            {
                if (HttpContext.Current.Session["mediacomputer"] == null)
                    HttpContext.Current.Session["mediacomputer"] = mediacomputer["terminalid"];
                mediaComputers.Append("<option value=\"").Append(mediacomputer["terminalid"]).Append("\"").Append((HttpContext.Current.Session["mediacomputer"] != null && (string)HttpContext.Current.Session["mediacomputer"] == mediacomputer["terminalid"] ? @" selected=""selected""" : "")).Append(">").Append(HttpUtility.HtmlEncode(mediacomputer["title"])).Append("</option>");
            }
            pageElements["MEDIACOMPUTERS"] = mediaComputers.ToString();
            pageElements["MEDIACOMPUTER"] = (string)(HttpContext.Current.Session["mediacomputer"] ?? "");
            // Set the content areas
            pageElements["CONTENT_LEFT"] = contentLeft.ToString();
            pageElements["CONTENT_RIGHT"] = contentRight.ToString();
        }
        public static void requestStart(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if(Core.settings[SETTINGS_KEY].getBool(SETTINGS_TEMPLATE_GLOBAL))
                pageElements["TEMPLATE"] = "ubermedia";
        }
        #endregion

        #region "Methods - Pages"
        public static void pageHome(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Build list of folders/drives for the sidebar
            string folders = "";
            foreach (ResultRow folder in conn.Query_Read("SELECT title, pfolderid FROM um_physical_folders ORDER BY title ASC"))
                folders += "<a href=\"<!--URL-->/ubermedia/browse/" + folder["pfolderid"] + "\"><img src=\"<!--URL-->/Content/Images/ubermedia/folder.png\" alt=\"Folder\" />" + folder["title"] + "</a>";
            // Build list of new items
            StringBuilder itemsNew = new StringBuilder();
            foreach (ResultRow item in conn.Query_Read("SELECT vi.*, it.thumbnail FROM um_virtual_items AS vi LEFT OUTER JOIN um_item_types AS it ON it.uid=vi.type_uid WHERE vi.type_uid != '100' ORDER BY vi.date_added DESC LIMIT 6"))
                itemsNew.Append(
                                    Core.templates[TEMPLATE][item["type_uid"].Equals("100") ? "browse_folder" : "browse_item"]
                                    .Replace("%TITLE%", HttpUtility.HtmlEncode(item["title"]))
                                    .Replace("%TITLE_S%", HttpUtility.HtmlEncode(titleSplit(item["title"], 15)))
                                    .Replace("%VITEMID%", item["vitemid"])
                                    .Replace("%PFOLDERID%", item["pfolderid"])
                                );
            // Build list of random items
            StringBuilder itemsRandom = new StringBuilder();
            foreach (ResultRow item in conn.Query_Read("SELECT vi.*, it.thumbnail FROM um_virtual_items AS vi LEFT OUTER JOIN um_item_types AS it ON it.uid=vi.type_uid WHERE vi.type_uid != '100' ORDER BY RAND() DESC LIMIT 6"))
                itemsRandom.Append(
                                    Core.templates[TEMPLATE][item["type_uid"].Equals("100") ? "browse_folder" : "browse_item"]
                                    .Replace("%TITLE%", HttpUtility.HtmlEncode(item["title"]))
                                    .Replace("%TITLE_S%", HttpUtility.HtmlEncode(titleSplit(item["title"], 15)))
                                    .Replace("%VITEMID%", item["vitemid"])
                                    .Replace("%PFOLDERID%", item["pfolderid"])
                                );
            contentLeft.Append(
                Core.templates[TEMPLATE]["home_sidebar"].Replace("%FOLDERS%", folders.Length == 0 ? "None." : folders)
                );
            contentRight.Append(
                Core.templates[TEMPLATE]["home"].Replace("%NEWEST%", itemsNew.ToString()).Replace("%RANDOM%", itemsRandom.ToString())
                );
            selectNavItem(ref pageElements, "HOME");
        }
        public static void pageBrowse(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // URL struct:
            // /browse/<null> - all files
            // /browse/<drive> - specific drive
            // /browse/<drive>/<folder id> - specific folder
            // Ordering struct:
            // title, rating, views, date_added

            // Prep to list items by gathering any possible filtering/sorting attribs
            int page = request.QueryString["p"] != null && isNumeric(request.QueryString["p"]) ? int.Parse(request.QueryString["p"]) : 1;
            int items_per_page = 15;
            string sort = request.QueryString["sort"] != null && request.QueryString["sort"].Length > 0 ? request.QueryString["sort"] == "title" ? "vi.title" : request.QueryString["sort"] == "ratings" ? "vi.cache_rating" : request.QueryString["sort"] == "views" ? "vi.views" : request.QueryString["sort"] == "date_added" ? "vi.date_added" : "vi.title" : "vi.title";
            bool sort_asc = request.QueryString["sd"] == null || !request.QueryString["sd"].Equals("desc");
            string qtag = request.QueryString["tag"] != null && isNumeric(request.QueryString["tag"]) ? request.QueryString["tag"] : "";
            // Build current URL
            string current_url = pageElements["URL"] + "/ubermedia/browse/" + (request.QueryString["2"] != null ? request.QueryString["2"] + (request.QueryString["3"] != null ? "/" + request.QueryString["3"] : "") : "") + "?";
            string current_params = "&amp;sort=" + (request.QueryString["sort"] ?? "") + "&amp;sd=" + (sort_asc ? "asc" : "desc");
            string current_tag = "&amp;tag=" + qtag;
            // Get the requested action
            switch (request.QueryString["action"])
            {
                case "queue_all":
                    // Queue all the files in this folder
                    if (HttpContext.Current.Session["mediacomputer"] != null) // Ensure a media computer is selected
                    {
                        string terminalid = Utils.Escape(HttpContext.Current.Session["mediacomputer"].ToString()); ;
                        // Grab all of the items
                        Result items = conn.Query_Read("SELECT vi.vitemid FROM um_virtual_items AS vi " + (qtag.Length != 0 ? "LEFT OUTER JOIN um_tag_items AS ti ON ti.vitemid=vi.vitemid " : "") + "LEFT OUTER JOIN um_item_types AS it ON it.uid=vi.type_uid WHERE vi.type_uid != '100'" + (qtag.Length != 0 ? " AND ti.tagid='" + Utils.Escape(qtag) + "'" : "") + (request.QueryString["2"] != null ? " AND vi.pfolderid='" + Utils.Escape(request.QueryString["2"]) + "'" + (request.QueryString["3"] != null ? " AND vi.parent='" + Utils.Escape(request.QueryString["3"]) + "'" : " AND vi.parent='0'") : "") + " ORDER BY " + sort + " " + (sort_asc ? "ASC" : "DESC"));
                        if (items.Rows.Count != 0)
                        {
                            // Generate huge insert statement
                            StringBuilder statement = new StringBuilder();
                            statement.Append("INSERT INTO um_terminal_buffer (command, terminalid, arguments, queue) VALUES ");
                            foreach (ResultRow item in items)
                                // Append each item
                                statement.Append("('media', '" + terminalid + "', '" + Utils.Escape(item["vitemid"]) + "', '1'),");
                            // Remove trailing comma
                            statement.Remove(statement.Length - 1, 1);
                            // Execute the query
                            conn.Query_Execute(statement.ToString());
                        }
                    }
                    // Redirect the user back
                    response.Redirect(pageElements["URL"] + (current_url + current_params + current_tag).Replace("&amp;", "&"));
                    break;
                case "add_youtube":
                    if (request.QueryString["2"] == null) // Check we're in a drive - else we cannot add YouTubes
                        contentRight.Append(Core.templates[Base.TEMPLATE]["browse_youtube_na"]);
                    else
                    {
                        // Add a YouTube item
                        string error = null;
                        string youtubeURL = request.Form["youtube_url"];
                        string title = request.Form["youtube_title"];
                        // Check if any data has been posted, if so handle the data
                        if (youtubeURL != null && title != null)
                        {
                            string youtubeVID;
                            // Validate
                            if (title.Length < PHYSICAL_FOLDER_TITLE_MIN || title.Length > PHYSICAL_FOLDER_TITLE_MAX) // Check the title length
                                error = "Title must be " + PHYSICAL_FOLDER_TITLE_MIN + " to " + PHYSICAL_FOLDER_TITLE_MAX + " characters in length!";
                            else if ((youtubeVID = parseYouTubeURL(youtubeURL)) == null) // Parse the ID and check it has been found
                                error = "Could not parse YouTube URL/URI!";
                            else if (conn.Query_Count("SELECT COUNT('') FROM um_virtual_items WHERE pfolderid='" + Utils.Escape(request.QueryString["2"]) + "' AND parent='" + Utils.Escape(request.QueryString["3"] ?? "0") + "' AND title LIKE '%" + Utils.Escape(title) + "%'") != 0) // Check no virtual item with the same title exists within the same folder - since the extension will be the same and hence the physical file will be the same
                                error = "An item with the same name already exists within the same folder!";
                            else
                            {
                                string phyPath = conn.Query_Scalar("SELECT physicalpath FROM um_physical_folders WHERE pfolderid='" + Utils.Escape(request.QueryString["2"]) + "'").ToString();
                                string subPath;
                                if (request.QueryString["3"] != null) // Check if we're in a sub-folder - if so, grab the sub-folder's relative path
                                    subPath = conn.Query_Scalar("SELECT phy_path FROM um_virtual_items WHERE vitemid='" + Utils.Escape(request.QueryString["3"]) + "'").ToString();
                                else
                                    subPath = string.Empty;
                                // Create the physical file in-case the database is lost
                                File.WriteAllText(phyPath + subPath + "\\" + title + ".yt", youtubeVID);
                                // Create the virtual item to represent the file
                                string vitemid = conn.Query_Scalar("INSERT INTO um_virtual_items (pfolderid, parent, type_uid, title, phy_path, date_added) VALUES('" + Utils.Escape(request.QueryString["2"]) + "', " + (request.QueryString["3"] != null ? "'" + Utils.Escape(request.QueryString["3"]) + "'" : "NULL") + ", '1300', '" + Utils.Escape(title) + "', '" + Utils.Escape(subPath + "\\" + title + ".yt") + "', NOW()); SELECT LAST_INSERT_ID();").ToString();
                                // Add to the thumbnail generator
                                UberMedia.ThumbnailGeneratorService.addItem(phyPath + subPath + "\\" + title + ".yt", vitemid, "youtube");
                                // Redirect back
                                response.Redirect(pageElements["URL"] + current_url + current_params);
                            }
                        }
                        contentRight.Append(Core.templates[Base.TEMPLATE]["browse_youtube"]
                            .Replace("%IURL%", current_url + current_params + "&amp;action=" + request.QueryString["action"])
                            .Replace("%YOUTUBE_URL%", youtubeURL ?? string.Empty)
                            .Replace("%YOUTUBE_TITLE%", title ?? string.Empty))
                            .Replace("%ERROR_STYLE%", error != null ? "display: block; visibility: visible;" : string.Empty)
                            .Replace("%ERROR_MESSAGE%", HttpUtility.HtmlEncode(error) ?? string.Empty);
                    }
                    break;
                case "add_folder":
                    if (request.QueryString["2"] == null)
                        return;
                    else
                    {
                        string errorMSG = null;
                        string folderTitle = request.Form["folder_title"];
                        // Check for postback
                        if (folderTitle != null)
                        {
                            if (folderTitle.Length < PHYSICAL_FOLDER_TITLE_MIN || folderTitle.Length > PHYSICAL_FOLDER_TITLE_MAX)
                                errorMSG = "Folder title must be " + PHYSICAL_FOLDER_TITLE_MIN + " to " + PHYSICAL_FOLDER_TITLE_MAX + " chars in length!";
                            else if (!isAlphaNumericSpace(folderTitle))
                                errorMSG = "Folder title must only consist of the following characters: 'A-z 0-9 * - _'!";
                            else
                            {
                                // Build the new physical path
                                string phyPath = conn.Query_Scalar("SELECT physicalpath FROM um_physical_folders WHERE pfolderid='" + Utils.Escape(request.QueryString["2"]) + "'").ToString();
                                string subPath;
                                if (request.QueryString["3"] != null) // Check if we're in a sub-folder - if so, grab the sub-folder's relative path
                                    subPath = conn.Query_Scalar("SELECT phy_path FROM um_virtual_items WHERE vitemid='" + Utils.Escape(request.QueryString["3"]) + "'").ToString();
                                else
                                    subPath = string.Empty;
                                // Check it doesn't already exist
                                if (Directory.Exists(phyPath + subPath + "\\" + folderTitle))
                                    errorMSG = "A folder with the specified title '" + HttpUtility.HtmlEncode(folderTitle) + "' already exists!";
                                else
                                {
                                    string physicalPath = subPath + "\\" + folderTitle;
                                    // Create the folder and virtual item
                                    string folderid = conn.Query_Scalar("INSERT INTO um_virtual_items (pfolderid, parent, type_uid, title, phy_path) VALUES('" + Utils.Escape(request.QueryString["2"]) + "', " + (request.QueryString["3"] != null ? "'" + Utils.Escape(request.QueryString["3"]) + "'" : "NULL") + ", '100', '" + Utils.Escape(folderTitle) + "', '" + Utils.Escape(physicalPath) + "'); SELECT LAST_INSERT_ID();").ToString();
                                    Directory.CreateDirectory(phyPath + subPath + "\\" + folderTitle);
                                    // Redirect to the new folder
                                    response.Redirect(pageElements["URL"] + "/ubermedia/browse/" + request.QueryString["2"] + "/" + folderid);
                                }
                            }
                        }
                        contentRight.Append(Core.templates[Base.TEMPLATE]["browse_add_folder"]
                            .Replace("%IURL%", current_url + current_params + "&amp;action=" + request.QueryString["action"])
                            .Replace("%FOLDER_TITLE%", request.Form["folder_title"] ?? string.Empty)
                            .Replace("%ERROR_STYLE%", errorMSG != null ? "visibility: visible; display: block;" : string.Empty)
                            .Replace("%ERROR_MESSAGE%", errorMSG ?? string.Empty)
                            );
                    }
                    break;
                case "delete_folder":
                    if (request.QueryString["3"] == null)
                        return;
                    else
                    {
                        Result folderInfo = conn.Query_Read("SELECT vi.*, CONCAT(pf.physicalpath, vi.phy_path) AS path FROM um_virtual_items AS vi LEFT OUTER JOIN um_physical_folders AS pf ON pf.pfolderid=vi.pfolderid WHERE vi.vitemid='" + Utils.Escape(request.QueryString["3"]) + "'");
                        if (folderInfo.Rows.Count != 1)
                            return;
                        else
                        {
                            if (request.Form["confirm"] != null)
                            {
                                // Delete the physical path
                                Directory.Delete(folderInfo[0]["path"], true);
                                // Delete the node
                                conn.Query_Execute("DELETE FROM um_virtual_items WHERE vitemid='" + Utils.Escape(folderInfo[0]["vitemid"]) + "'");
                                // Redirect to the parent directory
                                response.Redirect(pageElements["URL"] + "/ubermedia/browse/" + folderInfo[0]["pfolderid"] + "/" + folderInfo[0]["parent"]);
                            }
                            contentRight.Append(
                                Core.templates[Base.TEMPLATE]["confirm"]
                                .Replace("%ACTION_TITLE%", "Delete Folder")
                                .Replace("%ACTION_URL%", current_url + current_params + "&amp;action=" + request.QueryString["action"])
                                .Replace("%ACTION_BACK%", current_url + current_params)
                                .Replace("%ACTION_DESC%", "Are you sure you want to delete the following directory '" + HttpUtility.HtmlEncode(folderInfo[0]["title"]) + "'? This will also delete the following physical path and any files within it:<br /><br /><i>" + folderInfo[0]["path"] + "</i><br /><br />You should ensure no media is being played from this folder, else you may cause damage to your media library!")
                                );
                        }
                    }
                    break;
                default:
                    // Build tags section
                    contentRight.Append("<h2>Tags</h2>");
                    Result tags = conn.Query_Read("SELECT t.tagid, t.title FROM um_tags AS t, um_tag_items AS ti, um_virtual_items AS vi WHERE ti.tagid=t.tagid" + (request.QueryString["2"] != null ? " AND ti.vitemid=vi.vitemid AND vi.pfolderid='" + Utils.Escape(request.QueryString["2"]) + "'" + (request.QueryString["3"] != null ? " AND (vi.vitemid='" + Utils.Escape(request.QueryString["3"]) + "' OR vi.parent='" + Utils.Escape(request.QueryString["3"]) + "')" : " AND vi.parent='0' ") : "") + " GROUP BY ti.tagid");
                    if (tags.Rows.Count == 0)
                        contentRight.Append("No items are tagged.");
                    else
                    {
                        foreach (ResultRow tag in tags)
                            contentRight.Append("<a href=\"" + current_url + current_params + "&tag=" + tag["tagid"] + "\" class=\"TAG\">" + tag["title"] + "</a>");
                    }
                    // If viewing an actual folder, grab the desc etc - also checks the folder exists
                    if (request.QueryString["2"] != null || request.QueryString["3"] != null)
                    {
                        contentRight.Append("<h2>Info</h2>");
                        if (request.QueryString["3"] != null) // Display sub-folder information - may contain synopsis if e.g. a TV show
                        {
                            Result data = conn.Query_Read("SELECT vi.description FROM um_virtual_items AS vi WHERE vi.vitemid='" + Utils.Escape(request.QueryString["3"]) + "'");
                            if (data.Rows.Count == 0)
                                return;
                            contentRight.Append(data[0]["description"].Length > 0 ? data[0]["description"] : "(no description)");
                        }
                        else // Build info pane due to being a physical folder
                        {
                            Result data = conn.Query_Read("SELECT pfolderid, (SELECT COUNT('') FROM um_virtual_items WHERE pfolderid='" + Utils.Escape(request.QueryString["2"]) + "' AND type_uid='100') AS c_folders, (SELECT COUNT('') FROM um_virtual_items WHERE pfolderid='" + Utils.Escape(request.QueryString["2"]) + "' AND type_uid != '100') AS c_files FROM um_physical_folders WHERE pfolderid='" + Utils.Escape(request.QueryString["2"]) + "';");
                            if (data.Rows.Count == 0)
                                return;
                            contentRight.Append("This main folder has " + data[0]["c_files"] + " files and " + data[0]["c_folders"] + " folders indexed.");
                        }
                    }
                    // List files
                    Result files = conn.Query_Read("SELECT vi.*, it.thumbnail FROM um_virtual_items AS vi " + (qtag.Length != 0 ? "LEFT OUTER JOIN um_tag_items AS ti ON ti.vitemid=vi.vitemid " : "") + "LEFT OUTER JOIN um_item_types AS it ON it.uid=vi.type_uid WHERE vi.type_uid != '100'" + (qtag.Length != 0 ? " AND ti.tagid='" + Utils.Escape(qtag) + "'" : "") + (request.QueryString["2"] != null ? " AND vi.pfolderid='" + Utils.Escape(request.QueryString["2"]) + "'" + (request.QueryString["3"] != null ? " AND vi.parent='" + Utils.Escape(request.QueryString["3"]) + "'" : " AND vi.parent IS NULL") : "") + " ORDER BY " + sort + " " + (sort_asc ? "ASC" : "DESC") + " LIMIT " + ((items_per_page * page) - items_per_page) + ", " + items_per_page.ToString());
                    contentRight.Append("<h2>Files " + browseCreateNavigationBar(conn, pageElements, request.QueryString["2"] ?? "", request.QueryString["3"] ?? "") + "</h2>");
                    if (files.Rows.Count != 0)
                        foreach (ResultRow item in files)
                            contentRight.Append(
                                Core.templates[Base.TEMPLATE][item["type_uid"].Equals("100") ? "browse_folder" : "browse_item"]
                                .Replace("%TITLE%", HttpUtility.HtmlEncode(item["title"]))
                                .Replace("%TITLE_S%", HttpUtility.HtmlEncode(titleSplit(item["title"], 15)))
                                .Replace("%VITEMID%", item["vitemid"])
                                .Replace("%PFOLDERID%", item["pfolderid"])
                            );
                    else
                        contentRight.Append("<p>No items - check the sidebar for sub-folders on the left!</p>");
                    // Attach footer
                    contentRight.Append(Core.templates[Base.TEMPLATE]["browse_footer"]
                        .Replace("%PAGE%", page.ToString())
                        .Replace("%BUTTONS%",
                                                (page > 1 ? Core.templates[Base.TEMPLATE]["browse_footer_previous"].Replace("%URL%", current_url + current_params + current_tag + "&amp;p=" + (page - 1)) : "") +
                                                (page <= int.MaxValue ? Core.templates[Base.TEMPLATE]["browse_footer_next"].Replace("%URL%", current_url + current_params + current_tag + "&amp;p=" + (page + 1)) : "")
                                ));
                    break;
            }
            // Query files and folders
            Result folders = conn.Query_Read("SELECT vi.* FROM um_virtual_items AS vi " + (qtag.Length != 0 ? "LEFT OUTER JOIN um_tag_items AS ti ON ti.vitemid=vi.vitemid " : "") + "WHERE vi.type_uid = '100'" + (qtag.Length != 0 ? " AND ti.tagid='" + Utils.Escape(qtag) + "'" : "") + (request.QueryString["2"] != null ? " AND vi.pfolderid='" + Utils.Escape(request.QueryString["2"]) + "'" : string.Empty) + (request.QueryString["3"] != null ? " AND vi.parent='" + Utils.Escape(request.QueryString["3"]) + "'" : " AND vi.parent IS NULL") + " ORDER BY " + sort + " " + (sort_asc ? "ASC" : "DESC"));
            // Build sidebar
            contentLeft.Append("<h2>Main Folders</h2>");
            // -- Build a list of all the main drives
            contentLeft.Append(Core.templates[Base.TEMPLATE]["browse_side_folder"].Replace("%CLASS%", request.QueryString["2"] == null ? "selected" : string.Empty).Replace("%IURL%", pageElements["URL"] + "/ubermedia/browse").Replace("%TITLE%", "All").Replace("%ICON%", pageElements["URL"] + "/Content/Images/ubermedia/folders.png"));
            foreach (ResultRow drive in conn.Query_Read("SELECT pfolderid, title FROM um_physical_folders ORDER BY title ASC"))
                contentLeft.Append(Core.templates[Base.TEMPLATE]["browse_side_folder"]
                    .Replace("%IURL%", pageElements["URL"] + "/ubermedia/browse/" + drive["pfolderid"] + "?" + current_params)
                    .Replace("%CLASS%", drive["pfolderid"].Equals(request.QueryString["2"]) ? "selected" : "")
                    .Replace("%TITLE%", drive["title"])
                    .Replace("%ICON%", pageElements["URL"] + "/Content/Images/ubermedia/folder.png"));
            // -- Build a list of options for the current items
            contentLeft.Append("<h2>Options</h2>");
            contentLeft.Append("<a href=\"" + current_url + "\"><img src=\"<!--URL-->/Content/Images/ubermedia/view.png\" alt=\"View Items\" title=\"View Items\" />View Items</a>");
            contentLeft.Append("<a href=\"" + current_url + "&amp;action=queue_all\"><img src=\"<!--URL-->/Content/Images/ubermedia/play_queue.png\" alt=\"Queue All Items\" title=\"Queue All Items\" />Queue All Items</a>");
            if (request.QueryString["2"] != null)
            { // For folder-specific options only
                contentLeft.Append("<a href=\"" + current_url + "&amp;action=add_folder\"><img src=\"<!--URL-->/Content/Images/ubermedia/add_folder.png\" alt=\"Add Folder\" title=\"Add Folder\" />Add Folder</a>");
                contentLeft.Append("<a href=\"" + current_url + "&amp;action=add_youtube\"><img src=\"<!--URL-->/Content/Images/ubermedia/youtube.png\" alt=\"Add YouTube\" title=\"Add YouTube\" />Add YouTube</a>");
                contentLeft.Append("<a href=\"" + pageElements["URL"] + "/ubermedia/convert/" + (request.QueryString["3"] != null ? request.QueryString["3"] : "folder/" + request.QueryString["2"]) + "\"><img src=\"<!--URL-->/Content/Images/ubermedia/convert.png\" alt=\"Convert\" title=\"Convert\" />Convert</a>");
            }
            if (request.QueryString["3"] != null)
            { // For sub-folder specific options only
                contentLeft.Append("<a href=\"" + current_url + "&amp;action=delete_folder\"><img src=\"<!--URL-->/Content/Images/ubermedia/delete_folder.png\" alt=\"Delete Folder\" title=\"Delete Folder\" />Delete Folder</a>");
            }
            // -- Display the sub-folders for this folder
            contentLeft.Append("<h2>Sub-folders</h2>");
            if (folders.Rows.Count == 0) contentLeft.Append("None.");
            else
                foreach (ResultRow folder in folders)
                    contentLeft.Append(Core.templates[Base.TEMPLATE]["browse_side_folder"]
                        .Replace("%CLASS%", "")
                        .Replace("%IURL%", pageElements["URL"] + "/ubermedia/browse/" + folder["pfolderid"] + "/" + folder["vitemid"] + "?" + current_params)
                        .Replace("%TITLE%", folder["title"].Length > 20 ? folder["title"].Substring(0, 20) + "..." : folder["title"])
                        .Replace("%ICON%", pageElements["URL"] + "/Content/Images/ubermedia/folder.png"));
            // Set the navigation
            if (!sort_asc && sort == "vi.date_added") selectNavItem(ref pageElements, "NEWEST");
            else if (!sort_asc && sort == "vi.views") selectNavItem(ref pageElements, "POPULAR");
            else selectNavItem(ref pageElements, "BROWSE");
        }
        public static void pageYouTube(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.QueryString["2"] == null)
                return;
            else
            {
                // The HTML below is also used by Ubermedia Server - they're the same with a different video player URL
                response.Write(@"
<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN"">
<html>
<!--    Some of the code in this is unused due to originating from another project of mine, feel welcome
        to clean it up - limpygnome
-->
	<head>
         <style type=""text/css"">
        body
        {
            margin: 0em;
            background: #000;
            overflow: hidden;
        }
        </style>
        <script type=""text/javascript"" src=""" + pageElements["URL"] + @"/Content/JS/ubermedia/swfobject.js""></script>
		<title>IDLE</title>
		<script type=""text/javascript"" language=""javascript"">
		    var ply;
		    function onYouTubePlayerReady()
		    {
		        ply = document.getElementById('ytplayer');
		        ply.addEventListener(""onStateChange"", ""onytplayerStateChange"");
		        ply.addEventListener(""onError"", ""onPlayerError"");
		    }
		    function onPlayerError(errorCode)
		    {
		        document.title = 'ERROR' + errorCode;
		    }
		    function onytplayerStateChange(newState)
		    {
		        switch (newState)
		        {
		            case -1:
		                document.title = ""LOADING"";
		                break;
		            case 0:
		                document.title = ""FINISHED"";
		                break;
		            case 1:
		                document.title = ""PLAYING"";
		                break;
		            case 2:
		                document.title = ""PAUSED"";
		                break;
		        }
		    }
		    // Core
		    function YT_Load(url)
		    {
		        if (ply) ply.loadVideoById(url, 0);
		    }
		    function YT_Unload()
		    {
		        ply.stopVideo();
		    }
		    // Actions
		    function YT_Action_Unstop()
		    {
		        ply.playVideo();
		    }
		    function YT_Action_ToggleStop()
		    {
		        if (ply)
		            if (ply.getPlayerState == 1)
		                ply.stopVideo();
		            else
		                ply.playVideo();
		    }
		    function YT_Action_TogglePause()
		    {
		        if (ply)
		            if (ply.getPlayerState() == 1)
		                ply.pauseVideo();
		            else
		                ply.playVideo();
		    }
		    function YT_Action_SetVolume(rate)
		    {
		        if (ply && rate >= 0 && rate <= 100)
		            ply.setVolume(rate);
		    }
		    function YT_Action_Mute()
		    {
		        if (ply)
		            ply.mute();
		    }
		    function YT_Action_Unmute()
		    {
		        if (ply)
		            ply.unMute();
		    }
		    function YT_Action_ToggleMute()
		    {
		        if (ply)
		            if (ply.isMuted())
		                ply.unMute();
		            else
		                ply.mute();
		    }
		    function YT_Action_Play()
		    {
		        if (ply)
		            ply.playVideo();
		    }
		    function YT_Action_Pause()
		    {
		        if (ply)
		            ply.pauseVideo();
		    }
		    function YT_Action_Position(seconds)
		    {
		        if (ply)
		            ply.seekTo(seconds, true);
		    }
		    function YT_Action_Stop()
		    {
		        if (ply)
		            ply.stopVideo();
		    }
		    // Queries
		    function YT_Query_Volume()
            {
		        if (ply)
		            return ply.getVolume();
		        return 0;
		    }
		    function YT_Query_IsMuted()
		    {
		        if (ply)
		            return ply.isMuted();
		        return false;
		    }
		    function YT_Query_Duration()
		    {
		        if (ply)
		            return ply.getDuration();
		        return 0.0;
		    }
		    function YT_Query_CurrentPosition()
		    {
		        if (ply)
		            return ply.getCurrentTime();
                else
		            return 0.0;
		    }
		    function YT_Query_State()
		    {
		        if (ply)
		            return ply.getPlayerState();
		        else
		            return 0;
		    }
		</script>
	</head>
<body>
	<div id=""ytplayer"">
        <p>You need to install Adobe Flash Player to view YouTube videos!</p>
    </div>
    <script type=""text/javascript"">
        var params = { allowScriptAccess: ""always"" };

        swfobject.embedSWF(

		    ""http://www.youtube.com/v/" + request.QueryString["2"] + @"?version=3&enablejsapi=1&autohide=1&autoplay=1&rel=0&showinfo=0&showsearch=0"", ""ytplayer"", ""100%"", ""100%"", ""8"", null, null, params);

    </script>
</body>
</html>
");
                conn.Disconnect();
                response.End();
            }
        }
        public static void pageNewest(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            response.Redirect(pageElements["URL"] + "/ubermedia/browse?sort=date_added&sd=desc");
        }
        public static void pagePopular(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            response.Redirect(pageElements["URL"] + "/ubermedia/browse?sort=views&sd=desc");
        }
        public static void pageItem(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string vitemid = request.QueryString["2"] ?? "";
            if (vitemid.Length == 0)
                return;
            Result data = conn.Query_Read("SELECT vi.*, CONCAT(pf.physicalpath, vi.phy_path) AS path, it.thumbnail, it.title AS type FROM (um_virtual_items AS vi, um_physical_folders AS pf) LEFT OUTER JOIN um_item_types AS it ON it.uid=vi.type_uid WHERE vi.vitemid='" + Utils.Escape(vitemid) + "' AND vi.type_uid != '100' AND pf.pfolderid=vi.pfolderid;");
            if (data.Rows.Count == 0)
                return;
            contentRight.Append("<h2>Viewing " + HttpUtility.HtmlEncode(data[0]["title"]) + " - ").Append(browseCreateNavigationBar(conn, pageElements, data[0]["pfolderid"], data[0]["parent"])).Append("</h2>");
            switch (request.QueryString["3"])
            {
                case "modify":
                    if (request.Form["title"] != null && request.Form["description"] != null && request.Form["type_uid"] != null)
                    {
                        string title = request.Form["title"];
                        string description = request.Form["description"];
                        string type_uid = request.Form["type_uid"];
                        // Validate
                        if (title.Length < 1 || title.Length > 30)
                            throwError(ref pageElements, "Title must be 1 to 30 characters in length!");
                        else if (description.Length > 4000)
                            throwError(ref pageElements, "Description cannot exceed 4000 characters in length!");
                        else if (!isNumeric(type_uid) || conn.Query_Count("SELECT COUNT('') FROM um_item_types WHERE uid='" + Utils.Escape(type_uid) + "'") != 1)
                            throwError(ref pageElements, "Type UID does not exist!");
                        else
                        {
                            // Update
                            conn.Query_Execute("UPDATE um_virtual_items SET title='" + Utils.Escape(title) + "', description='" + Utils.Escape(description) + "', type_uid='" + Utils.Escape(type_uid) + "' WHERE vitemid='" + Utils.Escape(data[0]["vitemid"]) + "';");
                            response.Redirect(pageElements["URL"] + "/ubermedia/item/" + data[0]["vitemid"]);
                        }
                    }
                    // Build list of types for the select element
                    string types = "";
                    foreach (ResultRow type in conn.Query_Read("SELECT title, uid FROM um_item_types WHERE system='0' ORDER BY title ASC"))
                        types += "<option value=\"" + HttpUtility.HtmlEncode(type["uid"]) + "\"" + ((request.Form["type_uid"] != null && type["uid"].Equals(request.Form["type_uid"]) || (type["uid"].Equals(data[0]["type_uid"]))) ? " selected=\"selected\"" : "") + ">" + HttpUtility.HtmlEncode(type["title"]) + "</option>";
                    // Output content
                    contentRight.Append(Core.templates[Base.TEMPLATE]["item_modify"]
                        .Replace("%VITEMID%", data[0]["vitemid"])
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(request.Form["title"] ?? data[0]["title"]))
                        .Replace("%TYPES%", types)
                        .Replace("%DESCRIPTION%", HttpUtility.HtmlEncode(request.Form["description"] ?? data[0]["description"]))
                    );
                    break;
                case "rebuild":
                    if (request.Form["confirm"] != null)
                    {
                        UberMedia.ThumbnailGeneratorService.addItem(data[0]["path"], data[0]["vitemid"], data[0]["thumbnail"]);
                        response.Redirect(pageElements["URL"] + "/ubermedia/item/" + vitemid);
                    }
                    else
                        contentRight.Append(Core.templates[Base.TEMPLATE]["confirm"]
                            .Replace("%ACTION_TITLE%", "Thumbnail Rebuild")
                            .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/item/" + data[0]["vitemid"] + "/rebuild")
                            .Replace("%ACTION_DESC%", "Are you sure you want to rebuild the thumbnail of this item?")
                            .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/item/" + data[0]["vitemid"])
                            );
                    break;
                case "delete":
                    if (request.Form["confirm"] != null)
                    {
                        try
                        {
                            // Delete file
                            File.Delete(data[0]["path"]);
                            // Delete from virtual file-system aand buffer
                            conn.Query_Execute("DELETE FROM um_virtual_items WHERE vitemid='" + Utils.Escape(data[0]["vitemid"]) + "'; DELETE FROM um_terminal_buffer WHERE command='media' AND arguments='" + Utils.Escape(data[0]["vitemid"]) + "';");
                            response.Redirect(pageElements["URL"] + "/browse");
                        }
                        catch (Exception ex)
                        {
                            contentRight.Append("<h2>Deletion Error</h2><p>Could not delete file, the following error occurred:</p><p>").Append(HttpUtility.HtmlEncode(ex.Message)).Append("</p>");
                        }
                    }
                    else
                        contentRight.Append(Core.templates[Base.TEMPLATE]["confirm"]
                            .Replace("%ACTION_TITLE%", "Deletion")
                            .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/item/" + data[0]["vitemid"] + "/delete")
                            .Replace("%ACTION_DESC%", "Are you sure you want to delete this item?")
                            .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/item/" + data[0]["vitemid"])
                            );
                    break;
                case "reset_views":
                    if (request.Form["confirm"] != null)
                    {
                        conn.Query_Execute("UPDATE um_virtual_items SET views='0' WHERE vitemid='" + Utils.Escape(data[0]["vitemid"]) + "'");
                        response.Redirect(pageElements["URL"] + "/item/" + data[0]["vitemid"]);
                    }
                    else
                        contentRight.Append(Core.templates[Base.TEMPLATE]["confirm"]
                            .Replace("%ACTION_TITLE%", "Reset Views")
                            .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/item/" + data[0]["vitemid"] + "/reset_views")
                            .Replace("%ACTION_DESC%", "Are you sure you want to reset the views of this item?")
                            .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/item/" + data[0]["vitemid"])
                            );
                    break;
                case "play_now":
                    if (HttpContext.Current.Session["mediacomputer"] != null) terminalBufferEntry(conn, "media", (string)HttpContext.Current.Session["mediacomputer"], data[0]["vitemid"], false, true);
                    if (request.UrlReferrer != null)
                        response.Redirect(request.UrlReferrer.AbsoluteUri);
                    else
                        response.Redirect(pageElements["URL"] + "/ubermedia/item/" + data[0]["vitemid"]);
                    break;
                case "add_to_queue":
                    if (HttpContext.Current.Session["mediacomputer"] != null) terminalBufferEntry(conn, "media", (string)HttpContext.Current.Session["mediacomputer"], data[0]["vitemid"], true, false);
                    if (request.UrlReferrer != null)
                        response.Redirect(request.UrlReferrer.AbsoluteUri);
                    else
                        response.Redirect(pageElements["URL"] + "/ubermedia/item/" + data[0]["vitemid"]);
                    break;
                case "remove_tag":
                    string t = request.QueryString["t"];
                    if (t != null && isNumeric(t))
                        conn.Query_Execute("DELETE FROM um_tag_items WHERE tagid='" + Utils.Escape(t) + "' AND vitemid='" + Utils.Escape(data[0]["vitemid"]) + "';");
                    response.Redirect(pageElements["URL"] + "/ubermedia/item/" + data[0]["vitemid"]);
                    break;
                case "stream.mp4":
                case "stream.avi":
                case "stream":
                    try
                    {
                        // Majorly useful resource in writing this code:
                        // http://forums.asp.net/t/1218116.aspx

                        // Disable buffer and clear the response so far
                        response.Clear();
                        response.Buffer = false;
                        // Set the content type
                        string extension = Path.GetExtension(data[0]["path"]).ToLower();
                        switch (extension)
                        {
                            case ".mp4": response.ContentType = "video/mp4"; break;
                            case ".avi": response.ContentType = "video/avi"; break;
                            case ".ogg": response.ContentType = "video/ogg"; break;
                            case ".mkv": response.ContentType = "video/x-matroska"; break;
                            case ".m2ts": response.ContentType = "video/MP2T"; break;
                            case ".m4v": response.ContentType = "video/x-m4v"; break;
                            case ".mpg": response.ContentType = "video/mpeg"; break;
                            case ".3gp": response.ContentType = "video/3gpp"; break;
                            default: response.ContentType = "application/octet-stream"; break;
                        }
                        FileStream fs = new FileStream(data[0]["path"], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        BinaryReader bin = new BinaryReader(fs);
                        long startRead = 0;
                        // Read the range of bytes requested
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
                        System.Diagnostics.Debug.WriteLine((request.Headers["Range"] ?? "no range") + " - " + (request.Headers["Range"] != null ? "bytes " + startRead + "-" + (fs.Length - 1) + "/" + fs.Length : "...") + " - " + startRead);
                        // Specify the number of bytes being sent
                        response.AddHeader("Content-Length", (fs.Length - startRead).ToString());
                        // Specify other headers
                        string lastModified = File.GetLastWriteTime(data[0]["path"]).ToString("r");
                        response.AddHeader("Connection", "Keep-Alive");
                        response.AddHeader("Last-Modified", lastModified);
                        response.AddHeader("ETag", HttpUtility.UrlEncode(data[0]["path"], System.Text.Encoding.UTF8) + lastModified);
                        response.AddHeader("Accept-Ranges", "bytes");
                        response.StatusCode = 206;
                        // Start the stream at the offset
                        bin.BaseStream.Seek(startRead, SeekOrigin.Begin);
                        // Write bytes whilst the user is connected in chunks of 1024 bytes
                        int maxChunks = (int)Math.Ceiling((double)(fs.Length - startRead) / 10240);
                        for (int i = 0; i < maxChunks && response.IsClientConnected; i++)
                        {
                            response.BinaryWrite(bin.ReadBytes(10240));
                            response.Flush();
                        }

                        bin.Close();
                        fs.Close();
                        conn.Disconnect();
                        response.End();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: " + ex.Message + "\n" + ex.StackTrace);
                    }
                    break;
                case "watch":
                    // Check if the user has requested to force vlc usage
                    bool forceVLC = request.Cookies["force_vlc"] != null && request.Cookies["force_vlc"].Value.Equals("1");
                    if (request.QueryString["force_vlc"] != null)
                    {
                        if (request.QueryString["force_vlc"].Equals("1"))
                        {
                            HttpCookie cookie = new HttpCookie("force_vlc");
                            cookie.Value = "1";
                            cookie.Expires = DateTime.MaxValue;
                            response.Cookies.Add(cookie); // OM NOM NOM NOM
                            forceVLC = true;
                        }
                        else if (forceVLC)
                        {
                            response.Cookies["force_vlc"].Expires = DateTime.MinValue;
                            response.Cookies["force_vlc"].Value = "0";
                            response.Cookies.Remove("force_vlc");
                            forceVLC = false;
                        }
                    }
                    // Grab the HTML responsible for displaying/playing/interacting the media with the end-user
                    switch (int.Parse(data[0]["type_uid"]))
                    {
                        case 1000: // Video
                            // Use VLC or HTML5 video
                            if (forceVLC || (request.QueryString["4"] != null && request.QueryString["4"].Equals("vlc")))
                                contentRight.Append(Core.templates[Base.TEMPLATE]["item_player_vlc"]);
                            else
                                contentRight.Append(Core.templates[Base.TEMPLATE]["item_player_html5_video"]);
                            break;
                        case 1200: // Audio
                            // Use VLC or HTML5 audio
                            if (forceVLC || (request.QueryString["4"] != null && request.QueryString["4"].Equals("vlc")))
                                contentRight.Append(Core.templates[Base.TEMPLATE]["item_player_vlc"]);
                            else
                                contentRight.Append(Core.templates[Base.TEMPLATE]["item_player_html5_audio"]);
                            break;
                        case 1300: // YouTube
                            // Use YouTube embedded player
                            contentRight.Append(Core.templates[Base.TEMPLATE]["item_player_youtube"]);
                            pageElements["ITEM_YOUTUBE"] = File.ReadAllText(data[0]["path"]);
                            break;
                        case 1400: // Web-link
                            // Display a hyper-link
                            contentRight.Append(Core.templates[Base.TEMPLATE]["item_player_web"]);
                            pageElements["ITEM_URL"] = File.ReadAllText(data[0]["path"]);
                            break;
                        case 1500: // Image
                            contentRight.Append(Core.templates[Base.TEMPLATE]["item_player_image"]);
                            break;
                        default: // Unknown
                            contentRight.Append(Core.templates[Base.TEMPLATE]["item_player_unknown"]);
                            break;
                    }
                    pageElements["ITEM_VITEMID"] = data[0]["vitemid"];
                    pageElements["ITEM_TITLE"] = HttpUtility.HtmlEncode(data[0]["title"]);
                    break;
                default:
                    // Check if the user has tried to attach a tag
                    if (request.Form["tag"] != null)
                    {
                        string tag = request.Form["tag"];
                        if (tag.Length > 0 && isNumeric(tag) && conn.Query_Count("SELECT COUNT('') FROM um_tags AS t WHERE NOT EXISTS (SELECT tagid FROM um_tag_items WHERE tagid=t.tagid AND vitemid='" + Utils.Escape(data[0]["vitemid"]) + "') AND t.tagid='" + Utils.Escape(request.Form["tag"]) + "'") == 1)
                            conn.Query_Execute("INSERT INTO um_tag_items (tagid, vitemid) VALUES('" + Utils.Escape(request.Form["tag"]) + "', '" + Utils.Escape(data[0]["vitemid"]) + "')");
                    }
                    // Build page content
                    contentRight.Append(Core.templates[Base.TEMPLATE]["item_page"]
                        .Replace("%VITEMID%", data[0]["vitemid"])
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(data[0]["title"]))
                        .Replace("%DESCRIPTION%", data[0]["description"].Length > 0 ? HttpUtility.HtmlEncode(data[0]["description"]) : "(no description)")
                        .Replace("%VIEWS%", data[0]["views"])
                        .Replace("%DATE_ADDED%", data[0]["date_added"])
                        .Replace("%RATING%", data[0]["cache_rating"])
                        .Replace("%TYPE%", HttpUtility.HtmlEncode(data[0]["type"]) + " (" + HttpUtility.HtmlEncode(Path.GetExtension(data[0]["path"])) + ")")
                        .Replace("%PATH%", HttpUtility.HtmlEncode(data[0]["path"]))
                        .Replace("%VITEMID%", data[0]["vitemid"]));
                    // Add tags
                    string cTags = "";
                    string optionsTags = "";
                    // Buikd the current tags assigned to the item
                    Result tags = conn.Query_Read("SELECT t.title, t.tagid FROM um_tags AS t, um_tag_items AS ti WHERE ti.vitemid='" + Utils.Escape(vitemid) + "' AND t.tagid=ti.tagid");
                    if (tags.Rows.Count == 0) cTags = "None.";
                    else
                        foreach (ResultRow tag in tags)
                            cTags += Core.templates[Base.TEMPLATE]["item_tag"]
                                .Replace("%TAGID%", tag["tagid"])
                                .Replace("%VITEMID%", data[0]["vitemid"])
                                .Replace("%TITLE%", HttpUtility.HtmlEncode(tag["title"]));
                    // Build a list of all the tags
                    foreach (ResultRow tag in conn.Query_Read("SELECT * FROM um_tags ORDER BY title ASC"))
                        optionsTags += "<option value=\"" + tag["tagid"] + "\">" + HttpUtility.HtmlEncode(tag["title"]) + "</option>";
                    contentRight = contentRight.Replace("%TAGS%", cTags).Replace("%OPTIONS_TAGS%", optionsTags);
                    break;
            }
            selectNavItem(ref pageElements, "BROWSE");
            // Build options area
            contentLeft.Append(
                Core.templates[Base.TEMPLATE]["item_sidebar"].Replace("%VITEMID%", vitemid)
            );
        }
        public static void pageConvert(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Get the files
            bool isOriginFolder = false;
            string[] videoFormats = new string[] { "mp4", "ogv", "avi", "mkv", "wma", "mpg" };
            int filterT = -1;
            string filter = request.QueryString["filter"] != null && int.TryParse(request.QueryString["filter"], out filterT) && filterT >= 0 && filterT < videoFormats.Length ? videoFormats[filterT] : null;
            Result files = null;
            if (request.QueryString["2"] == null)
                return;
            else if (request.QueryString["2"].Equals("folder") && request.QueryString["3"] != null && isNumeric(request.QueryString["3"]) && int.Parse(request.QueryString["3"]) >= 0)
                files = conn.Query_Read("SELECT CONCAT(pf.physicalpath, vi.phy_path) AS path, pf.physicalpath, pf.allow_web_synopsis, vi.title, vi.pfolderid FROM um_virtual_items AS vi LEFT OUTER JOIN um_physical_folders AS pf ON pf.pfolderid=vi.pfolderid WHERE vi.pfolderid='" + Utils.Escape(request.QueryString["3"]) + "' AND vi.parent is NULL AND vi.type_uid != '100'" + (filter != null ? " AND vi.phy_path LIKE '%." + Utils.Escape(filter) + "'" : string.Empty) + ";");
            else if (isNumeric(request.QueryString["2"]) && int.Parse(request.QueryString["2"]) >= 0)
            // Check if the vitemid is a folder or an actual item
            {
                Result vitemidCheck = conn.Query_Read("SELECT type_uid FROM um_virtual_items WHERE vitemid='" + Utils.Escape(request.QueryString["2"]) + "'");
                if (vitemidCheck.Rows.Count == 1)
                {
                    if (vitemidCheck[0]["type_uid"] == "100")
                    {
                        // Folder
                        isOriginFolder = true;
                        files = conn.Query_Read("SELECT CONCAT(pf.physicalpath, vi.phy_path) AS path, pf.physicalpath, pf.allow_web_synopsis, vi.title, vi.pfolderid FROM um_virtual_items AS vi LEFT OUTER JOIN um_physical_folders AS pf ON pf.pfolderid=vi.pfolderid WHERE vi.parent='" + Utils.Escape(request.QueryString["2"]) + "' AND vi.type_uid != '100'" + (filter != null ? " AND vi.phy_path LIKE '%." + Utils.Escape(filter) + "'" : string.Empty) + ";");
                    }
                    else
                        // Media item
                        files = conn.Query_Read("SELECT CONCAT(pf.physicalpath, vi.phy_path) AS path, pf.physicalpath, pf.allow_web_synopsis, vi.title, vi.pfolderid FROM um_virtual_items AS vi LEFT OUTER JOIN um_physical_folders AS pf ON pf.pfolderid=vi.pfolderid WHERE vi.vitemid='" + Utils.Escape(request.QueryString["2"]) + "'" + (filter != null ? " AND vi.phy_path LIKE '%." + Utils.Escape(filter) + "'" : string.Empty) + ";");
                }
            }
            // Prepare to build the page
            Result folders = conn.Query_Read("SELECT pfolderid, title, physicalpath FROM um_physical_folders ORDER BY title ASC");
            // Check we have files to convert
            if (files == null || files.Rows.Count == 0)
            {
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["convert_nofiles"]
                    );
            }
            else
            {
                string error = null;
                // Get posted data
                string convertAction = request.Form["convert_action"];
                string convertActionMove = request.Form["convert_action_move"];
                string convertFormat = request.Form["convert_format"];
                string convertVideoBitrate = request.Form["convert_video_bitrate"];
                string convertAudioBitrate = request.Form["convert_audio_bitrate"];
                string convertAudioSamplerate = request.Form["convert_audio_samplerate"];
                string convertVideoWidth = request.Form["convert_video_width"];
                string convertVideoHeight = request.Form["convert_video_height"];
                // Check for postback
                if (convertFormat != null && convertVideoBitrate != null && convertAudioBitrate != null && convertAudioSamplerate != null &&
                    convertVideoWidth != null && convertVideoHeight != null)
                {
                    UberMedia.ConversionAction action = UberMedia.ConversionAction.Nothing;
                    int actionTemp = -1;
                    string actionMove = null;
                    int videoBitrate = -1;
                    int videoWidth = -1;
                    int videoHeight = -1;
                    int audioBitrate = -1;
                    int audioSamplerate = -1;
                    int format = -1;
                    // Validate
                    if (files.Rows.Count == 0)
                        error = "No files to be converted, cannot continue!";
                    if (convertVideoBitrate.Length > 0 && (!int.TryParse(convertVideoBitrate, out videoBitrate) || videoBitrate < 1))
                        error = "Invalid video bitrate!";
                    else if (convertVideoWidth.Length > 0 && (!int.TryParse(convertVideoWidth, out videoWidth) || videoWidth < 1))
                        error = "Invalid video width!";
                    else if (convertVideoHeight.Length > 0 && (!int.TryParse(convertVideoHeight, out videoHeight) || videoHeight < 1))
                        error = "Invalid video height!";
                    else if (convertAudioBitrate.Length > 0 && (!int.TryParse(convertAudioBitrate, out audioBitrate) || audioBitrate < 1))
                        error = "Invalid audio bitrate!";
                    else if (convertAudioSamplerate.Length > 0 && (!int.TryParse(convertAudioSamplerate, out audioSamplerate) || audioSamplerate < 1))
                        error = "Invalid audio samplerate!";
                    else if (convertFormat == null || !int.TryParse(convertFormat, out format) || format < 0 || format >= videoFormats.Length)
                        error = "Invalid format!";
                    else if (convertAction == null || !int.TryParse(convertAction, out actionTemp) || !Enum.IsDefined(typeof(UberMedia.ConversionAction), actionTemp))
                        error = "Invalid action!";
                    try
                    {
                        action = (UberMedia.ConversionAction)actionTemp;
                    }
                    catch
                    {
                        error = "Invalid action!";
                    }
                    if (error == null && action == UberMedia.ConversionAction.Move)
                    {
                        bool found = false;
                        foreach (ResultRow folder in folders)
                            if (folder["pfolderid"].Equals(convertActionMove))
                            {
                                actionMove = folder["physicalpath"];
                                found = true;
                                break;
                            }
                        if (!found) error = "Invalid action move folder!";
                    }
                    if (error == null)
                    {
                        // Queue for conversion
                        UberMedia.ConversionInfo ci;
                        string basePath;
                        string filename;
                        string newFormat = videoFormats[format];
                        foreach (ResultRow file in files)
                        { // We'll skip any files with the same extension
                            if (Path.GetExtension(file["path"]) != "." + newFormat)
                            {
                                basePath = Path.GetDirectoryName(file["path"]);
                                filename = Path.GetFileNameWithoutExtension(file["path"]);
                                ci = new UberMedia.ConversionInfo();
                                ci.phy_pfolderid = file["pfolderid"];
                                ci.phy_path = file["physicalpath"];
                                ci.phy_allowsynopsis = file["allow_web_synopsis"].Equals("1");
                                ci.pathSource = file["path"];
                                ci.pathOutput = basePath + "\\" + filename + "." + newFormat;
                                ci.actionOriginal = action;
                                ci.actionOriginalArgs = actionMove;
                                ci.audioBitrate = audioBitrate;
                                ci.audioSampleRate = audioSamplerate;
                                ci.videoBitrate = videoBitrate;
                                ci.videoResolution = new Size(videoWidth, videoHeight);
                                // Add item to queue to be converted
                                UberMedia.ConversionService.queue.Add(ci);
                            }
                        }
                        // Redirect to the origin
                        if (request.QueryString["2"].Equals("folder"))
                            // Folder - parent-level
                            response.Redirect(pageElements["URL"] + "/ubermedia/browse/" + request.QueryString["2"]);
                        else if (isOriginFolder)
                        {
                            // Folder - Get the parent folder ID
                            string pfolderid = conn.Query_Scalar("SELECT pfolderid FROM um_virtual_items WHERE vitemid='" + Utils.Escape(request.QueryString["1"]) + "'").ToString();
                            response.Redirect(pageElements["URL"] + "/ubermedia/browse/" + pfolderid + "/" + request.QueryString["1"]);
                        }
                        else
                            // Item
                            response.Redirect(pageElements["URL"] + "/ubermedia/item/" + request.QueryString["1"]);
                    }
                }
                // Build formats list
                StringBuilder formats = new StringBuilder();
                string f;
                for (int i = 0; i < videoFormats.Length; i++)
                {
                    f = videoFormats[i];
                    formats.Append("<option value=\"" + i + "\"" + (f.Equals(convertFormat) ? " selected=\"selected\"" : string.Empty) + ">" + HttpUtility.HtmlEncode(f) + "</option>");
                }
                // Build actions list
                StringBuilder actions = new StringBuilder();
                string[] actionsText = Enum.GetNames(typeof(UberMedia.ConversionAction));
                int val;
                for (int i = 0; i < actionsText.Length; i++)
                {
                    val = (int)Enum.Parse(typeof(UberMedia.ConversionAction), actionsText[i]);
                    actions.Append("<option value=\"" + val + "\"" + (val.ToString().Equals(convertAction) ? " selected=\"selected\"" : string.Empty) + ">" + HttpUtility.HtmlEncode(actionsText[i].Replace("_", " ")) + "</option>");
                }
                // Build actions move list
                StringBuilder actionsMove = new StringBuilder();
                foreach (ResultRow folder in folders)
                    actionsMove.Append("<option value=\"" + folder["pfolderid"] + "\"" + (folder["pfolderid"].Equals(convertActionMove) ? " selected=\"selected\"" : string.Empty) + ">" + HttpUtility.HtmlEncode(folder["title"]) + "</option>");
                // Build files list
                StringBuilder filesList = new StringBuilder();
                foreach (ResultRow file in files)
                    filesList.Append("<li>").Append(HttpUtility.HtmlEncode(file["title"])).Append("<br /><i>").Append(HttpUtility.HtmlEncode(file["path"])).Append("</i></li>");
                // Build filter list
                StringBuilder filterList = new StringBuilder();
                filterList.Append("<option>None</option>");
                for (int i = 0; i < videoFormats.Length; i++)
                    filterList.Append("<option value=\"" + i + "\"" + (i == filterT ? " selected=\"selected\"" : string.Empty) + ">" + HttpUtility.HtmlEncode(videoFormats[i]) + "</option>");
                // Set content
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["convert"]
                    .Replace("%FORMATS%", formats.ToString())
                    .Replace("%ACTIONS%", actions.ToString())
                    .Replace("%ACTIONS_MOVE%", actionsMove.ToString())
                    .Replace("%VIDEO_BITRATE%", HttpUtility.HtmlEncode(convertVideoBitrate ?? string.Empty))
                    .Replace("%AUDIO_BITRATE%", HttpUtility.HtmlEncode(convertAudioBitrate ?? string.Empty))
                    .Replace("%AUDIO_SAMPLERATE%", HttpUtility.HtmlEncode(convertAudioSamplerate ?? string.Empty))
                    .Replace("%VIDEO_WIDTH%", HttpUtility.HtmlEncode(convertVideoWidth ?? string.Empty))
                    .Replace("%VIDEO_HEIGHT%", HttpUtility.HtmlEncode(convertVideoHeight ?? string.Empty))
                    .Replace("%FILTER%", filterList.ToString())
                    .Replace("%FILES%", filesList.ToString())
                    .Replace("%ERROR_STYLE%", error != null ? "display: block; visibility: visible;" : string.Empty)
                    .Replace("%ERROR_MSG%", error ?? string.Empty)
                    .Replace("%PARAMS_NOFILTER%", (request.QueryString["1"] != null ? "/" + request.QueryString["1"] : string.Empty) + (request.QueryString["2"] != null ? "/" + request.QueryString["2"] : string.Empty))
                    .Replace("%PARAMS%", (request.QueryString["1"] != null ? "/" + request.QueryString["1"] : string.Empty) + (request.QueryString["2"] != null ? "/" + request.QueryString["2"] : string.Empty) + (filterT != -1 ? "?filter=" + filterT : string.Empty))
                    );
            }
            contentLeft.Append(
                Core.templates[Base.TEMPLATE]["convert_sidebar"]
                );
        }
        public static void pageSearch(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string query = request.QueryString["q"] != null ? Utils.Escape(request.QueryString["q"].Replace("%", "")) : "";
            if (query.Length == null)
            {
                pageElements["CONTENT_LEFT"] = Core.templates[Base.TEMPLATE]["search_sidebar"];
                pageElements["CONTENT_RIGHT"] = Core.templates[Base.TEMPLATE]["search"];
            }
            else
            {
                if (query.Length == 0)
                {
                    response.Write("<h2>Search</h2>");
                    response.Write("<p>Enter your query in the top-right box!</p>");
                }
                else
                {
                    response.Write("Results for '" + query + "':<div class=\"clear\"></div>");
                    Result results = conn.Query_Read("SELECT vi.*, it.thumbnail FROM um_virtual_items AS vi LEFT OUTER JOIN um_item_types AS it ON it.uid=vi.type_uid WHERE vi.title LIKE '%" + query + "%' ORDER BY FIELD(vi.type_uid, '100') DESC, vi.title ASC LIMIT 100");
                    if (results.Rows.Count == 0) response.Write("<p>No items were found matching your criteria...</p>");
                    else
                        foreach (ResultRow item in results)
                            response.Write(
                                Core.templates[Base.TEMPLATE][item["type_uid"].Equals("100") ? "browse_folder" : "browse_item"]
                                .Replace("%TITLE%", HttpUtility.HtmlEncode(item["title"]))
                                .Replace("%TITLE_S%", HttpUtility.HtmlEncode(titleSplit(item["title"], 15)))
                                .Replace("%VITEMID%", item["vitemid"])
                                .Replace("%PFOLDERID%", item["pfolderid"])
                                .Replace("<!--URL-->", pageElements["URL"])
                                );
                }
                conn.Disconnect();
                response.End();
            }
        }
        public static void pageChange(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Validate the input
            if (request.QueryString["c"] == null || !isNumeric(request.QueryString["c"]) || conn.Query_Count("SELECT COUNT('') FROM um_terminals WHERE terminalid='" + Utils.Escape(request.QueryString["c"]) + "'") != 1)
                response.Write("Invalid media-computer!");
            else
                // Store the setting in a session variable
                HttpContext.Current.Session["mediacomputer"] = request.QueryString["c"];
            conn.Disconnect();
            response.End();
        }
        public static void pageShutdown(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Ensure a media computer is selected
            string tid = request.QueryString["2"] ?? (string)HttpContext.Current.Session["mediacomputer"];
            if (tid == null)
                return;
            // Ask the user to confirm their action
            if (request.Form["confirm"] != null)
            {
                terminalBufferEntry(conn, "shutdown", tid, string.Empty, false, true);
                response.Redirect(pageElements["URL"] + "/ubermedia/control");
            }
            else
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["confirm"]
                    .Replace("%ACTION_TITLE%", "Shutdown Terminal")
                    .Replace("%ACTION_URL%", pageElements["URL"] + "/ubermedia/shutdown/" + tid)
                    .Replace("%ACTION_DESC%", "Are you sure you want to shutdown the current terminal?")
                    .Replace("%ACTION_BACK%", pageElements["URL"] + "/ubermedia/control")
                    );
        }
        public static void pageRestart(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Ensure a media computer is selected
            string tid = request.QueryString["2"] ?? (string)HttpContext.Current.Session["mediacomputer"];
            if (tid == null)
                return;
            // Ask the user to confirm their action
            if (request.Form["confirm"] != null)
            {
                terminalBufferEntry(conn, "restart", tid, string.Empty, false, true);
                response.Redirect(pageElements["URL"] + "/ubermedia/control");
            }
            else
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["confirm"]
                    .Replace("%ACTION_TITLE%", "Restart Terminal")
                    .Replace("%ACTION_URL%", pageElements["URL"] + "/ubermedia/restart/" + tid)
                    .Replace("%ACTION_DESC%", "Are you sure you want to restart the current terminal?")
                    .Replace("%ACTION_BACK%", pageElements["URL"] + "/ubermedia/control")
                    );
        }
        public static void pageCredits(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            contentLeft.Append(
                Core.templates[Base.TEMPLATE]["credits_sidebar"]
                );
            contentRight.Append(
                Core.templates[Base.TEMPLATE]["credits"]
                );
        }
        public static void pageStatus(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            switch (request.QueryString["2"])
            {
                default:
                    // Status of the terminals
                    contentRight.Append(Core.templates[Base.TEMPLATE]["status_header"]);
                    bool parsedDate = false;
                    DateTime updated;
                    TimeSpan length;
                    foreach (ResultRow terminal in conn.Query_Read("SELECT * FROM um_terminals ORDER BY title ASC"))
                    {
                        parsedDate = DateTime.TryParse(terminal["status_updated"], out updated);
                        if (parsedDate)
                            length = DateTime.Now.Subtract(updated);
                        else
                            length = new TimeSpan(); // Cassini seems to have an issue unless this is set...prolly a compilation bug in ASP.NET 2
                        contentRight.Append(
                            Core.templates[Base.TEMPLATE]["status_terminal"]
                            .Replace("%TERMINALID%", terminal["terminalid"])
                            .Replace("%TITLE%", HttpUtility.HtmlEncode(terminal["title"]))
                            .Replace("%UPDATED%", HttpUtility.HtmlEncode(terminal["status_updated"]))
                            .Replace("%STATUS%", !parsedDate ? "Never online" : length.TotalSeconds < 8 ? "Online" : length.TotalSeconds < 15 ? "Communication lost" : "Offline")
                            );
                    }
                    break;
                case "shutdown":
                case "restart":
                    bool shutdown = request.QueryString["1"].Equals("shutdown");
                    if (request.Form["confirm"] != null)
                    {
                        // Shutdown every terminal
                        string statement = buildCommandStatementAllTerminals(conn, shutdown ? "shutdown" : "restart", null, false);
                        if (statement != null)
                            conn.Query_Execute(statement);
                        response.Redirect(pageElements["URL"] + "/ubermedia/status");
                    }
                    else
                        contentRight.Append(
                            Core.templates[Base.TEMPLATE]["confirm"]
                            .Replace("%ACTION_TITLE%", (shutdown ? "Shutdown" : "Restart") + " All Terminals")
                            .Replace("%ACTION_DESC%", "Are you sure you want to " + (shutdown ? "shutdown" : "restart") + " every terminal?")
                            .Replace("%ACTION_URL%", shutdown ? pageElements["URL"] + "/ubermedia/status/shutdown" : pageElements["URL"] + "/ubermedia/status/restart")
                            .Replace("%ACTION_BACK%", pageElements["URL"] + "/status")
                            );
                    break;
            }
            contentLeft.Append(
                 Core.templates[Base.TEMPLATE]["status_left"]
            );
            selectNavItem(ref pageElements, "CONTROL");
        }
        public static byte[] thumbnailNotFound = null;
        public static void pageThumbnail(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Get the thumbnail data
            byte[] data = null;
            string rawVitemid = request.QueryString["2"];
            int vitemid;
            if (rawVitemid != null && int.TryParse(rawVitemid, out vitemid) && vitemid > 0)
            { // Attempt to get the data from the database
                Result result = conn.Query_Read("SELECT thumbnail_data FROM um_virtual_items WHERE vitemid='" + Utils.Escape(request.QueryString["2"]) + "'");
                if (result.Rows.Count == 1)
                    data = result[0].GetByteArray("thumbnail_data");
            }
            if (data == null)
            { // Display the not-found image from file or cache
                if (thumbnailNotFound == null)
                {
                    MemoryStream ms = new MemoryStream();
                    using (FileStream f = new FileStream(Core.basePath + "/Content/Images/ubermedia/thumbnail.png", FileMode.Open, FileAccess.Read))
                    {
                        using (Image img = Image.FromStream(f))
                        {
                            img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                    }
                    // Set the local byte array and dispose the stream
                    data = ms.ToArray();
                    ms.Dispose();
                    thumbnailNotFound = data;
                }
                else data = thumbnailNotFound;
            }
            // Set the content type to jpeg
            response.ContentType = "image/jpeg";
            // Write to the response stream
            response.AddHeader("Content-Length", data.Length.ToString());
            response.BinaryWrite(data);
            conn.Disconnect();
            response.End(); // End the response - nothing more to send
        }
        #endregion

        #region "Methods - Pages - Terminal"
        /// <summary>
        /// Handles terminal communication.
        /// </summary>
        public static void pageTerminal(HttpRequest request, HttpResponse response, Connector conn)
        {
            switch (request.QueryString["1"])
            {
                case "register":
                    terminalRegister(conn, request, response);
                    break;
                case "update":
                    terminalStatus(request, response, conn);
                    terminalGetcmd(request, response, conn);
                    break;
                case "getcmd":
                    terminalGetcmd(request, response, conn);
                    break;
                case "status":
                    terminalStatus(request, response, conn);
                    break;
                case "media":
                    terminalMedia(request, response, conn);
                    break;
                default:
                    response.Write("ERROR:Unknown command specified!");
                    conn.Disconnect();
                    response.End();
                    break;
            }
        }
        /// <summary>
        /// Used to register a new media terminal.
        /// </summary>
        private static void terminalRegister(Connector conn, HttpRequest request, HttpResponse response)
        {
            // Check we have been sent at least a title
            string title = request.QueryString["title"];
            if (title != null)
            {
                // Check the title is valid
                if (title.Length < TERMINAL_TITLE_MIN || title.Length > TERMINAL_TITLE_MAX)
                    response.Write("ERROR:Title must be " + TERMINAL_TITLE_MIN + " to " + TERMINAL_TITLE_MAX + " chars in length!");
                else
                    // Insert the title and return the key for the new terminal
                    response.Write("SUCCESS:" + int.Parse(conn.Query_Scalar("INSERT INTO um_terminals (title) VALUES('" + Utils.Escape(title) + "'); SELECT LAST_INSERT_ID();").ToString()));
            }
            else
                response.Write("ERROR:No title provided!");
            // Stop any other HTML from being printed by ending the response
            conn.Disconnect();
            response.End();
        }
        /// <summary>
        /// Used to grab the next command for the terminal; this command is then immediately deleted.
        /// </summary>
        private static void terminalGetcmd(HttpRequest request, HttpResponse response, Connector conn)
        {
            string terminalid = request.QueryString["tid"];
            string queue = request.QueryString["q"];
            if (terminalid == null)
                response.Write("ERROR:No terminal identifier specified!");
            else if (queue == null)
                response.Write("ERROR:No queue parameter provided!");
            else if (queue != "0" && queue != "1")
                response.Write("ERROR:Queue parameter must be 1 or 0!");
            else
            {
                Result res = conn.Query_Read("SELECT * FROM um_terminal_buffer WHERE terminalid='" + Utils.Escape(terminalid) + "'" + (queue.Equals("0") ? " AND queue='0'" : string.Empty) + " ORDER BY cid ASC LIMIT 1");
                if (res.Rows.Count == 1)
                {
                    response.Write(res[0]["command"] + ":" + res[0]["arguments"]);
                    conn.Query_Execute("DELETE FROM um_terminal_buffer WHERE cid='" + Utils.Escape(res[0]["cid"]) + "'");
                }
            }
            conn.Disconnect();
            response.End();
        }
        /// <summary>
        /// Used to update the status of the terminal.
        /// </summary>
        private static void terminalStatus(HttpRequest request, HttpResponse response, Connector conn)
        {
            string terminalid = request.QueryString["tid"];
            string state = request.QueryString["state"];
            string volume = request.QueryString["volume"];
            string muted = request.QueryString["muted"];
            string vitemid = request.QueryString["vitemid"];
            string pos = request.QueryString["pos"];
            string dur = request.QueryString["dur"];
            conn.Query_Execute("UPDATE um_terminals SET status_state='" + Utils.Escape(state) + "', status_volume='" + Utils.Escape(volume) + "', status_volume_muted='" + Utils.Escape(muted) + "', status_vitemid='" + Utils.Escape(vitemid) + "', status_position='" + Utils.Escape(pos) + "', status_duration='" + Utils.Escape(dur) + "', status_updated=NOW() WHERE terminalid='" + Utils.Escape(terminalid) + "'");
        }
        /// <summary>
        /// Used to increment the ratings for a piece of media and retrieve the data required to play something.
        /// </summary>
        private static void terminalMedia(HttpRequest request, HttpResponse response, Connector conn)
        {
            string vitemid = request.QueryString["vitemid"];
            if (vitemid == null)
                response.Write("ERROR:No vitemid specified!");
            else
            {
                Result item = conn.Query_Read("SELECT it.interface, (CONCAT(pf.physicalpath, vi.phy_path)) AS path, vi.title FROM (um_virtual_items AS vi, um_physical_folders AS pf) LEFT OUTER JOIN um_item_types AS it ON it.uid=vi.type_uid WHERE pf.pfolderid=vi.pfolderid AND vi.vitemid='" + Utils.Escape(vitemid) + "';");
                if (item.Rows.Count == 1)
                {
                    response.Write("SUCCESS:" + item[0]["interface"] + ":" + item[0]["path"] + ":" + item[0]["title"]);
                    conn.Query_Execute("UPDATE um_virtual_items SET views = views + 1 WHERE vitemid='" + Utils.Escape(vitemid) + "'");
                }
                else
                    response.Write("ERROR:Virtual item not found!");
            }
            conn.Disconnect();
            response.End();
        }
        #endregion

        #region "Methods - Pages - Control"
        /// <summary>
        /// Used to control the current selected media computer.
        /// </summary>
        public static void pageControl(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.QueryString["cmd"] != null && request.QueryString["mc"] != null)
            {
                string mc = request.QueryString["mc"]; // Media computer/terminalid
                switch (request.QueryString["cmd"])
                {
                    case "status":
                        // For retrieving the status of the terminal (general information)
                        response.ContentType = "text/xml";
                        /*
                         * <d>
                         *      <p>secs</p>     Position in seconds
                         *      <d>dur</d>      Duration in seconds
                         *      <v>vol</v>      Volume from 0.0 to 1.0
                         *      <m>mute</m>     Indicates if muted - 1 or 0
                         *      <s>state</s>    State
                         *      <pv>val</pv>    Unique value to indicate changes (count of items:last cid)
                         * </d>
                         * 
                         * 
                         * 
                         */
                        Result data = conn.Query_Read("SELECT t.*, CONCAT(CONCAT((SELECT COUNT('') FROM um_terminal_buffer WHERE terminalid=t.terminalid AND command='media' AND queue='1'), ':'),(SELECT cid FROM um_terminal_buffer WHERE terminalid=t.terminalid AND command='media' AND queue='1' ORDER BY cid DESC LIMIT 1)) AS val FROM um_terminals AS t WHERE t.terminalid='" + Utils.Escape(mc) + "' AND t.status_updated >= DATE_SUB(NOW(), INTERVAL 1 MINUTE);");
                        // This "hash" system could be much improved, however this works well for the current scenario
                        string playlistHash = conn.Query_Scalar("SELECT SUM(arguments)*COUNT(arguments) FROM um_terminal_buffer WHERE terminalid='" + Utils.Escape(mc) + "' AND command='media' AND queue='1';").ToString();
                        if (data.Rows.Count == 1)
                        {
                            ResultRow s = data[0];
                            response.Write("<d>");
                            response.Write("    <p>" + s["status_position"] + "</p>");
                            response.Write("    <d>" + s["status_duration"] + "</d>");
                            response.Write("    <v>" + s["status_volume"] + "</v>");
                            response.Write("    <m>" + s["status_volume_muted"] + "</m>");
                            response.Write("    <s>" + s["status_state"] + "</s>");
                            response.Write("    <pv>" + s["status_vitemid"] + "</pv>");
                            response.Write("    <ph>" + playlistHash + "</ph>");
                            response.Write("</d>");
                        }
                        else
                        {
                            response.Write("<d>");
                            response.Write("    <p>0</p>");
                            response.Write("    <d>0</d>");
                            response.Write("    <v>0</v>");
                            response.Write("    <m>1</m>");
                            response.Write("    <s>0</s>");
                            response.Write("    <pv></pv>");
                            response.Write("    <ph>" + playlistHash + "</ph>");
                            response.Write("</d>");
                        }
                        break;
                    case "playlist":
                        // For retrieving the playlist of items (current item & upcoming items)
                        /*
                         * <d>
                         *      <c>                 Current item playing:
                         *          <v>vitemid</v>  Vitemid.
                         *          <t>title</t>    Title.
                         *      </c>
                         *      <u>                 List of upcoming items:
                         *          <i>
                         *              <c>cid</c>      cid - terminal buffer command identifier
                         *              <v>vitemid</v>  Vitemid
                         *              <t>title</t>    Title
                         *          </i>
                         *          ...                 Multiple items using i-tag
                         *      </u>
                         * </d>
                         */
                        response.ContentType = "text/xml";
                        Result currItem = conn.Query_Read("SELECT vi.vitemid, vi.title FROM (um_virtual_items AS vi, um_terminals AS t) WHERE vi.vitemid=t.status_vitemid AND t.terminalid='" + Utils.Escape(mc) + "' AND t.status_updated >= DATE_SUB(NOW(), INTERVAL 1 MINUTE)");
                        Result upcomingItems = conn.Query_Read("SELECT tb.cid, vi.vitemid, vi.title FROM (um_virtual_items AS vi, um_terminal_buffer AS tb) WHERE vi.vitemid=tb.arguments AND tb.command='media' AND tb.queue='1' ORDER BY tb.cid ASC;");
                        response.Write("<d>");
                        response.Write("    <c>");
                        if (currItem.Rows.Count == 1)
                        {
                            response.Write("        <v>" + currItem[0]["vitemid"] + "</v>");
                            response.Write("        <t>" + HttpUtility.HtmlEncode(currItem[0]["title"]) + "</t>");
                        }
                        else
                        {
                            response.Write("        <v></v>");
                            response.Write("        <t></t>");
                        }
                        response.Write("    </c>");
                        response.Write("    <u>");
                        foreach (ResultRow item in upcomingItems)
                        {
                            response.Write("        <i>");
                            response.Write("            <c>" + item["cid"] + "</c>");
                            response.Write("            <v>" + item["vitemid"] + "</v>");
                            response.Write("            <t>" + HttpUtility.HtmlEncode(item["title"]) + "</t>");
                            response.Write("        </i>");
                        }
                        response.Write("    </u>");
                        response.Write("</d>");
                        break;
                    case "remove":
                        string cid = request.QueryString["cid"];
                        // Removes an item from the playlist - if cid is empty, use skip command
                        if (cid != null && cid.Length > 0)
                            conn.Query_Execute("DELETE FROM um_terminal_buffer WHERE command='media' AND cid='" + Utils.Escape(request.QueryString["cid"]) + "'");
                        else
                            terminalBufferEntry(conn, "next", mc, string.Empty, false, true);
                        break;
                    case "play_now":
                        string cid2 = request.QueryString["cid"];
                        // Plays an item now by setting the queue column/flag to zero - causing the command to be immediately executed on the terminal
                        if (cid2 != null && cid2.Length > 0)
                            conn.Query_Execute("UPDATE um_terminal_buffer SET queue='0' WHERE cid='" + Utils.Escape(request.QueryString["cid"]) + "'");
                        break;
                    case "c_pi":
                        // Previous item
                        terminalBufferEntry(conn, "previous", mc, string.Empty, false, true);
                        break;
                    case "c_sb":
                        // Skip backwards
                        terminalBufferEntry(conn, "skip_backward", mc, string.Empty, false, true);
                        break;
                    case "c_s":
                        // Stop
                        terminalBufferEntry(conn, "stop", mc, string.Empty, false, true);
                        break;
                    case "c_p":
                        // Toggles play
                        terminalBufferEntry(conn, "play_toggle", mc, string.Empty, false, true);
                        break;
                    case "c_sf":
                        // Skip forward
                        terminalBufferEntry(conn, "skip_forward", mc, string.Empty, false, true);
                        break;
                    case "c_ni":
                        // Next item
                        terminalBufferEntry(conn, "next", mc, string.Empty, false, true);
                        break;
                    case "c_vol":
                        // Changes the value of the volume; check for v querystring
                        if (request.QueryString["v"] == null) return;
                        double v2 = -1;
                        if (!double.TryParse(request.QueryString["v"], out v2) || !(v2 >= 0 && v2 <= 1))
                            return;
                        terminalBufferEntry(conn, "volume", mc, request.QueryString["v"], false, true);
                        break;
                    case "c_mute":
                        // Toggles muting the volume
                        terminalBufferEntry(conn, "mute_toggle", mc, "", false, true);
                        break;
                    case "c_pos":
                        // Changes the position of the current media
                        if (request.QueryString["v"] == null) return;
                        double v = -1;
                        if (!double.TryParse(request.QueryString["v"], out v) || v < 0)
                            return;
                        terminalBufferEntry(conn, "position", mc, request.QueryString["v"], false, true);
                        break;
                    case "remove_all":
                        conn.Query_Execute("DELETE FROM um_terminal_buffer WHERE terminalid='" + Utils.Escape(mc) + "' AND command='media' AND queue='1';");
                        break;
                }
                conn.Disconnect();
                if (request.QueryString["manual"] != null)
                    response.Redirect(pageElements["URL"] + "/ubermedia/control");
                else
                    response.End();
            }
            else if (HttpContext.Current.Session["mediacomputer"] == null)
            {
                // Display a message prompting the user to install a terminal
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["controls_error"]
                    );
            }
            else
            {
                // Create controls page
                string mediacomputer = (string)HttpContext.Current.Session["mediacomputer"];
                pageElements["PLAYING"] = controlsPrintCurrentItem(conn, mediacomputer);
                pageElements["UPCOMING"] = controlsPrintUpcoming(conn, mediacomputer);
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["controls_content"]
                    );
                contentLeft.Append(
                    Core.templates[Base.TEMPLATE]["controls_left"]
                    );
                pageElements["ONLOAD"] = "controlsInit('" + HttpUtility.HtmlEncode(mediacomputer) + "')";
            }
            selectNavItem(ref pageElements, "CONTROL");
        }
        public static string controlsPrintCurrentItem(Connector conn, string terminalid)
        {
            Result data = conn.Query_Read("SELECT vi.vitemid, vi.title FROM um_virtual_items AS vi, um_terminals AS t WHERE vi.vitemid=t.status_vitemid AND t.terminalid='" + Utils.Escape(terminalid) + "' AND t.status_updated >= DATE_SUB(NOW(), INTERVAL 1 MINUTE)");
            return data.Rows.Count == 0 ? "None." :
                Core.templates[Base.TEMPLATE]["controls_item"]
                    .Replace("%VITEMID%", data[0]["vitemid"])
                    .Replace("%CID%", "")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(data[0]["title"]));
            ;
        }
        public static string controlsPrintUpcoming(Connector conn, string terminalid)
        {
            string data = "";
            foreach (ResultRow item in conn.Query_Read("SELECT tb.cid, vi.title, vi.vitemid FROM um_terminal_buffer AS tb, um_virtual_items AS vi WHERE tb.terminalid='" + Utils.Escape(terminalid) + "' AND tb.command='media' AND tb.queue='1' AND vi.vitemid=tb.arguments ORDER BY tb.cid ASC"))
                data += Core.templates[Base.TEMPLATE]["controls_item"]
                    .Replace("%VITEMID%", item["vitemid"])
                    .Replace("%CID%", item["cid"])
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(item["title"]));
            return data.Length == 0 ? "None." : data;
        }
        #endregion

        #region "Methods - Pages - Admin"
        /// <summary>
        /// Admin management system; responsible for managing settings and core features of the media library.
        /// </summary>
        public static void pageAdmin(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string subpg = request.QueryString["2"] != null ? request.QueryString["2"] : "home";
            switch (subpg)
            {
                case "startup":
                    adminStartup(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "home":
                    adminHome(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "rebuild_all":
                    adminRebuildAll(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "rebuild_thumbnails":
                    adminRebuildThumbnails(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "run_indexer":
                    adminRunIndexer(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "stop_indexer":
                    adminStopIndexer(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "start_thumbnails":
                    adminStartThumbnails(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "stop_thumbnails":
                    adminStopThumbnails(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "folders":
                    adminFolders(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "folder":
                    adminFolder(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "requests":
                    adminRequests(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "settings":
                    adminSettings(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "terminals":
                    adminTerminals(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "tags":
                    adminTags(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "rebuild_film_cache":
                    adminRebuildFilmCache(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "conversion_start":
                    adminConversionStart(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "conversion_stop":
                    adminConversionStop(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "conversion_clear":
                    adminConversionClear(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
                case "uninstall":
                    adminUninstall(pluginid, conn, ref contentLeft, ref contentRight, ref pageElements, request, response);
                    break;
            }
            contentLeft.Append(
                Core.templates[Base.TEMPLATE]["admin_sidebar"]
                );
            selectNavItem(ref pageElements, "CONTROL");
        }
        private static void adminStartup(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            contentRight.Append(Core.templates[Base.TEMPLATE]["admin_startup"]);
        }
        private static void adminHome(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            System.Threading.Thread th;
            int t;
            // Indexing service
            StringBuilder indexing = new StringBuilder();
            if (UberMedia.Indexer.threadPool.Count > 0)
            {
                Result drives = conn.Query_Read("SELECT pfolderid, title FROM um_physical_folders ORDER BY pfolderid ASC");
                int i = 0;
                foreach (KeyValuePair<string, System.Threading.Thread> thread in UberMedia.Indexer.threadPool)
                {
                    i++;
                    indexing.Append("<div>Thread " + i.ToString() + " [Folder " + thread.Key + " - " + driveTitle(drives, thread.Key) + "]: " + thread.Value.ThreadState.ToString() + " - " + UberMedia.Indexer.getStatus(thread.Key) + "</div>");
                }
            }
            else indexing.Append("No threads are actively indexing any folders.");
            indexing.Append("<br />Active: ").Append(UberMedia.Indexer.serviceIsActive);
            // Thumbnail service
            StringBuilder thumbnail = new StringBuilder();
            if (UberMedia.ThumbnailGeneratorService.threads.Count > 0)
            {
                for (t = 0; t < UberMedia.ThumbnailGeneratorService.threads.Count; t++)
                {
                    th = UberMedia.ThumbnailGeneratorService.threads[t];
                    thumbnail.Append("<div>Thread ").Append(t).Append(" - ").Append(th.ThreadState.ToString()).Append("</div>");
                }
            }
            else thumbnail.Append("No threads have been pooled by the thumbnail service.");
            thumbnail
                .Append("<br /><br />Items queued for processing: ").Append(UberMedia.ThumbnailGeneratorService.queue.Count)
                .Append("<br />Next item: " + (UberMedia.ThumbnailGeneratorService.queue.Count > 0 ? UberMedia.ThumbnailGeneratorService.queue[0][0] : "none."))
                .Append("<br />Delegator status: ").Append(UberMedia.ThumbnailGeneratorService.status)
                .Append("<br />Active: ").Append(UberMedia.ThumbnailGeneratorService.serviceIsActive);
            // Film information service
            StringBuilder filmInformation = new StringBuilder();
            filmInformation.Append(UberMedia.FilmInformation.state.ToString()).Append(" - ").Append(UberMedia.FilmInformation.status)
                .Append("<br />Active: ").Append(UberMedia.FilmInformation.serviceIsActive);
            // Conversion service
            StringBuilder conversion = new StringBuilder();
            for (t = 0; t < UberMedia.ConversionService.threads.Count; t++)
            {
                th = UberMedia.ConversionService.threads[t];
                conversion.Append("<div>Thread ").Append(t).Append(" - ").Append(th.ThreadState.ToString()).Append(" - ").Append(UberMedia.ConversionService.threadStatus != null && t < UberMedia.ConversionService.threadStatus.Length ? UberMedia.ConversionService.threadStatus[t] ?? "Idle." : "Unknown.").Append("</div>");
            }
            conversion.Append("<br />Items queued for conversion: ").Append(UberMedia.ConversionService.queue.Count)
            .Append("<br />Delegator status: ").Append(UberMedia.ConversionService.status)
            .Append("<br />Active: ").Append(UberMedia.ConversionService.serviceIsActive);
            // Check if this is an ajax request
            if (request.QueryString["ajax"] != null)
            {
                response.ContentType = "application/xml";

                XmlWriter xml = XmlWriter.Create(response.OutputStream);
                xml.WriteStartDocument();
                xml.WriteStartElement("admin");

                xml.WriteStartElement("indexing");
                xml.WriteCData(indexing.ToString());
                xml.WriteEndElement();

                xml.WriteStartElement("thumbnails");
                xml.WriteCData(thumbnail.ToString());
                xml.WriteEndElement();

                xml.WriteStartElement("filminformation");
                xml.WriteCData(filmInformation.ToString());
                xml.WriteEndElement();

                xml.WriteStartElement("conversion");
                xml.WriteCData(conversion.ToString());
                xml.WriteEndElement();

                xml.WriteEndElement();
                xml.WriteEndDocument();

                xml.Flush();
                conn.Disconnect();
                response.End();
            }
            else
            {
                // Set the template
                contentRight.Append(Core.templates[Base.TEMPLATE]["admin_home"]);
                // Set on-load
                pageElements["ONLOAD"] = "adminStatus();";
                // Build
                contentRight
                    .Replace("%INDEXING%", indexing.ToString())
                    .Replace("%THUMBNAIL%", thumbnail.ToString())
                    .Replace("%FILMINFORMATION%", filmInformation.ToString())
                    .Replace("%CONVERSION%", conversion.ToString());
            }
        }
        private static void adminRebuildAll(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                checkNotDoublePost(ref pageElements, response);
                // Shutdown indexers
                UberMedia.Indexer.terminateThreadPool();
                // Shutdown thumbnail service
                UberMedia.ThumbnailGeneratorService.serviceStop();
                // Clear queue
                UberMedia.ThumbnailGeneratorService.queue.Clear();
                // Delete database data
                conn.Query_Execute("DELETE FROM um_virtual_items; ALTER TABLE um_virtual_items AUTO_INCREMENT = 0;");
                // Start thumbnail service
                UberMedia.ThumbnailGeneratorService.serviceStart();
                // Start indexers
                runIndexers(conn);
                response.Redirect(pageElements["URL"] + "/ubermedia/admin");
            }
            else contentRight.Append(
                Core.templates[Base.TEMPLATE]["confirm"]
                .Replace("%ACTION_TITLE%", "Clear &amp; Rebuild Library")
                .Replace("%ACTION_DESC%", "All data, tagging, virtual-changes and other related items will be wiped and reindexed.")
                .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/rebuild_all")
                .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin")
                );
        }
        private static void adminRebuildThumbnails(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                checkNotDoublePost(ref pageElements, response);
                // Shutdown indexing
                UberMedia.Indexer.terminateThreadPool();
                // Shutdown thumbnail service
                UberMedia.ThumbnailGeneratorService.serviceStop();
                // Wipe queue
                lock (UberMedia.ThumbnailGeneratorService.queue)
                    UberMedia.ThumbnailGeneratorService.queue.Clear();
                // Reset all thumbnails
                conn.Query_Execute("UPDATE um_virtual_items SET thumbnail_data=NULL;");
                // Start thumbnail service
                UberMedia.ThumbnailGeneratorService.serviceStart();
                // Build map of physical folder id's to paths for requeueing
                Dictionary<string, string> DrivePaths = new Dictionary<string, string>();
                foreach (ResultRow drive in conn.Query_Read("SELECT pfolderid, physicalpath FROM um_physical_folders ORDER BY pfolderid ASC"))
                    DrivePaths.Add(drive["pfolderid"], drive["physicalpath"]);
                // Queue items
                foreach (ResultRow item in conn.Query_Read("SELECT vi.vitemid, vi.pfolderid, vi.phy_path, it.thumbnail FROM um_virtual_items AS vi LEFT OUTER JOIN um_item_types AS it ON it.uid=vi.type_uid WHERE vi.type_uid != '100' AND it.thumbnail != '' ORDER BY vitemid ASC"))
                    UberMedia.ThumbnailGeneratorService.addItem(DrivePaths[item["pfolderid"]] + item["phy_path"], item["vitemid"], item["thumbnail"]);
                // Redirect user
                response.Redirect(pageElements["URL"] + "/ubermedia/admin");
            }
            else
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["confirm"]
                .Replace("%ACTION_TITLE%", "Rebuild All Thumbnails")
                .Replace("%ACTION_DESC%", "All thumbnails will be deleted and rescheduled for processing.")
                .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/rebuild_thumbnails")
                .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin")
                );
        }
        private static void adminStopIndexer(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                checkNotDoublePost(ref pageElements, response);
                UberMedia.Indexer.terminateThreadPool();
                response.Redirect(pageElements["URL"] + "/ubermedia/admin");
            }
            else
                contentRight.Append(Core.templates[Base.TEMPLATE]["confirm"]
                .Replace("%ACTION_TITLE%", "Stop Indexer")
                .Replace("%ACTION_DESC%", "This will force the indexer offline (if any threads of the indexer are executing).")
                .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/stop_indexer")
                .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin"));
        }
        private static void adminRunIndexer(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                checkNotDoublePost(ref pageElements, response);
                runIndexers(conn);
                response.Redirect(pageElements["URL"] + "/ubermedia/admin");
            }
            else
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["confirm"]
                 .Replace("%ACTION_TITLE%", "Run Indexer")
                 .Replace("%ACTION_DESC%", "This will run the indexers manually to find new content and delete missing media; drives already being indexed (right now) will be unaffected.")
                 .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/run_indexer")
                 .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin")
                 );
        }
        private static void adminStartThumbnails(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                checkNotDoublePost(ref pageElements, response);
                UberMedia.ThumbnailGeneratorService.serviceStart();
                response.Redirect(pageElements["URL"] + "/ubermedia/admin");
            }
            else
                contentRight.Append(Core.templates[Base.TEMPLATE]["confirm"]
                .Replace("%ACTION_TITLE%", "Start Thumbnail Service")
                .Replace("%ACTION_DESC%", "This will enable the thumbnail service to continue processing items in the thumbnail queue.")
                .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/start_thumbnails")
                .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin"));
        }
        private static void adminStopThumbnails(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                checkNotDoublePost(ref pageElements, response);
                UberMedia.ThumbnailGeneratorService.serviceStop();
                response.Redirect(pageElements["URL"] + "/ubermedia/admin");
            }
            else
                contentRight.Append(Core.templates[Base.TEMPLATE]["confirm"]
                .Replace("%ACTION_TITLE%", "Stop Thumbnail Service")
                .Replace("%ACTION_DESC%", "This will disable the thumbnail service from processing items in the thumbnail queue.")
                .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/stop_thumbnails")
                .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin"));
        }
        private static void adminFolders(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Check if the user has requested to add a new folder
            if (request.Form["title"] != null && request.Form["path"] != null)
            {
                string title = request.Form["title"];
                string path = request.Form["path"];
                bool synopsis = request.Form["synopsis"] != null;
                // Validate
                if (title.Length < PHYSICAL_FOLDER_TITLE_MIN || title.Length > PHYSICAL_FOLDER_TITLE_MAX)
                    throwError(ref pageElements, "Title must be " + PHYSICAL_FOLDER_TITLE_MIN + " to " + PHYSICAL_FOLDER_TITLE_MAX + " characters in length!");
                else if (path.Length < PHYSICAL_FOLDER_PATH_MIN || path.Length > PHYSICAL_FOLDER_PATH_MAX)
                    throwError(ref pageElements, "Invalid path!");
                else if (!Directory.Exists(path))
                    throwError(ref pageElements, "Path '" + path + "' does not exist or it is currently inaccessible!");
                else
                {
                    // Add folder to db
                    string pfolderid = conn.Query_Scalar("INSERT INTO um_physical_folders (title, physicalpath, allow_web_synopsis) VALUES('" + Utils.Escape(title) + "', '" + Utils.Escape(path) + "', '" + (synopsis ? "1" : "0") + "'); SELECT LAST_INSERT_ID();").ToString();
                    response.Redirect(pageElements["URL"] + "/ubermedia/admin/folder/" + pfolderid);
                }
            }
            // Build list of folders
            string currentFolders = "";
            foreach (ResultRow folder in conn.Query_Read("SELECT * FROM um_physical_folders ORDER BY title ASC"))
                currentFolders += Core.templates[Base.TEMPLATE]["admin_folders_item"]
                    .Replace("%PFOLDERID%", folder["pfolderid"])
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(folder["title"]))
                    .Replace("%PATH%", HttpUtility.HtmlEncode(folder["physicalpath"]))
                    .Replace("%SYNOPSIS%", folder["allow_web_synopsis"]);
            // Build page content
            contentRight.Append(
                Core.templates[Base.TEMPLATE]["admin_folders"]
                .Replace("%CURRENT_FOLDERS%", currentFolders)
                .Replace("%TITLE%", HttpUtility.HtmlEncode(request.Form["title"]) ?? "")             // Add new folder form
                .Replace("%PATH%", HttpUtility.HtmlEncode(request.Form["path"]) ?? "")
                .Replace("%SYNOPSIS%", request.Form["synopsis"] != null ? " checked" : "")
                );
        }
        private static void adminFolder(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Grab the folder identifier and validate it
            string folderid = request.QueryString["3"];
            if (folderid == null || !isNumeric(folderid))
                return;
            // Grab the folders info
            Result data = conn.Query_Read("SELECT (SELECT COUNT('') FROM um_virtual_items WHERE pfolderid=p.pfolderid AND type_uid='100') AS total_folders, IFNULL(SUM(vi.views), 0) AS total_views, COUNT(vi.vitemid) AS total_items, p.* FROM um_physical_folders AS p LEFT OUTER JOIN um_virtual_items AS vi ON (vi.pfolderid=p.pfolderid AND vi.type_uid != '100') WHERE p.pfolderid='" + Utils.Escape(folderid) + "'");

            // Redirect if no data is returned
            if (data.Rows.Count != 1)
                return;
            // Build the content
            switch (request.QueryString["4"])
            {
                case "rebuild_thumbnails":
                    // Reset any pre-existing thumbnails
                    conn.Query_Execute("UPDATE um_virtual_items SET thumbnail_data=NULL WHERE pfolderid='" + Utils.Escape(data[0]["pfolderid"]) + "'");
                    // Get all of the items for this folder
                    Result items = conn.Query_Read("SELECT CONCAT(pf.physicalpath, vi.phy_path) AS path, vi.vitemid, it.thumbnail FROM (um_virtual_items AS vi, um_physical_folders AS pf, um_item_types AS it) WHERE it.uid=vi.type_uid AND it.system='0' AND vi.pfolderid=pf.pfolderid AND pf.pfolderid='" + Utils.Escape(data[0]["pfolderid"]) + "'");
                    // Requeue all the thumbnails
                    foreach (ResultRow item in items)
                        UberMedia.ThumbnailGeneratorService.addItem(item["path"], item["vitemid"], item["thumbnail"]);
                    // Back to folder main page
                    response.Redirect(pageElements["URL"] + "/ubermedia/admin/folder/" + data[0]["pfolderid"]);
                    break;
                case "index":
                    UberMedia.Indexer.indexDrive(data[0]["pfolderid"], data[0]["physicalpath"], data[0]["allow_web_synopsis"].Equals("1"));
                    response.Redirect(pageElements["URL"] + "/ubermedia/admin/folder/" + data[0]["pfolderid"]);
                    break;
                case "remove":
                    if (request.Form["confirm"] != null)
                    {
                        // Shutdown the indexer (if it exists)
                        UberMedia.Indexer.terminateIndexer(data[0]["pfolderid"]);
                        // Remove items and the folder its self
                        conn.Query_Execute("DELETE FROM um_virtual_items WHERE pfolderid='" + Utils.Escape(data[0]["pfolderid"]) + "'; DELETE FROM um_physical_folders WHERE pfolderid='" + Utils.Escape(data[0]["pfolderid"]) + "';");
                        response.Redirect(pageElements["URL"] + "/ubermedia/admin/folders");
                    }
                    else
                        contentRight.Append(
                            Core.templates[Base.TEMPLATE]["confirm"]
                            .Replace("%ACTION_TITLE%", "Remove Folder")
                            .Replace("%ACTION_DESC%", "Are you sure you want to remove this folder? Note: this will not delete the physical folder!")
                            .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/folder/" + data[0]["pfolderid"] + "/remove")
                            .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin/folder/" + data[0]["pfolderid"])
                            );
                    break;
                case "add_type":
                    string type = request.Form["type"];
                    if (type != null && isNumeric(type) && conn.Query_Count("SELECT ((SELECT COUNT('') FROM um_item_types WHERE system='0' AND typeid='" + Utils.Escape(type) + "') + (SELECT COUNT('') FROM um_physical_folder_types WHERE typeid='" + Utils.Escape(type) + "' AND pfolderid='" + Utils.Escape(data[0]["pfolderid"]) + "'))") == 1)
                        conn.Query_Execute("INSERT INTO um_physical_folder_types (pfolderid, typeid) VALUES('" + Utils.Escape(data[0]["pfolderid"]) + "', '" + Utils.Escape(type) + "');");
                    response.Redirect(pageElements["URL"] + "/ubermedia/admin/folder/" + data[0]["pfolderid"]);
                    break;
                case "remove_type":
                    string ty = request.QueryString["t"];
                    if (ty != null && isNumeric(ty))
                        conn.Query_Execute("DELETE FROM um_physical_folder_types WHERE typeid='" + Utils.Escape(ty) + "' AND pfolderid='" + Utils.Escape(data[0]["pfolderid"]) + "';");
                    response.Redirect(pageElements["URL"] + "/ubermedia/admin/folder/" + data[0]["pfolderid"]);
                    break;
                default:
                    // Check if the user has tried to modify the folder title or/and path
                    if (request.Form["title"] != null && request.Form["path"] != null)
                    {
                        string title = request.Form["title"];
                        string path = request.Form["path"];
                        bool web_synopsis = request.Form["synopsis"] != null;
                        if (title.Length < PHYSICAL_FOLDER_TITLE_MIN || title.Length > PHYSICAL_FOLDER_TITLE_MAX)
                            throwError(ref pageElements, "Title must be " + PHYSICAL_FOLDER_TITLE_MIN + " to " + PHYSICAL_FOLDER_TITLE_MAX + " characters in length!");
                        else if (path.Length < PHYSICAL_FOLDER_PATH_MIN || path.Length > PHYSICAL_FOLDER_PATH_MAX)
                            throwError(ref pageElements, "Invalid path!");
                        else if (!Directory.Exists(path))
                            throwError(ref pageElements, "Path does not exist!");
                        else
                        {
                            conn.Query_Execute("UPDATE um_physical_folders SET physicalpath='" + Utils.Escape(path) + "', title='" + Utils.Escape(title) + "', allow_web_synopsis='" + (web_synopsis ? "1" : "0") + "' WHERE pfolderid='" + Utils.Escape(data[0]["pfolderid"]) + "'");
                            response.Redirect(pageElements["URL"] + "/ubermedia/admin/folder/" + data[0]["pfolderid"]);
                        }
                    }
                    // Build list of item types available to add
                    string typesAdd = "";
                    foreach (ResultRow it in conn.Query_Read("SELECT typeid, title FROM um_item_types WHERE system='0' ORDER BY title ASC"))
                        typesAdd += "<option value=\"" + Utils.Escape(it["typeid"]) + "\">" + HttpUtility.HtmlEncode(it["title"]) + "</option>";
                    // Build list of pre-existing types assigned to the folder
                    string typesRemove = "";
                    foreach (ResultRow it in conn.Query_Read("SELECT pft.typeid, it.title FROM um_physical_folder_types AS pft LEFT OUTER JOIN um_item_types AS it ON it.typeid=pft.typeid WHERE pft.pfolderid='" + Utils.Escape(data[0]["pfolderid"]) + "' ORDER BY it.title ASC;"))
                        typesRemove += Core.templates[Base.TEMPLATE]["admin_folder_type"]
                            .Replace("%TYPEID%", it["typeid"])
                            .Replace("%TITLE%", HttpUtility.HtmlEncode(it["title"]));
                    if (typesRemove.Length == 0) typesRemove = "None.";
                    // Build content
                    contentRight.Append(
                        Core.templates[Base.TEMPLATE]["admin_folder"]
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(data[0]["title"]))                               // Modify
                        .Replace("%PATH%", HttpUtility.HtmlEncode(data[0]["physicalpath"]))
                        .Replace("%SYNOPSIS%", data[0]["allow_web_synopsis"].Equals("1") ? "checked" : "")
                        .Replace("%TOTAL_ITEMS%", data[0]["total_items"])                                           // Stats
                        .Replace("%TOTAL_FOLDERS%", data[0]["total_folders"])
                        .Replace("%TOTAL_VIEWS%", data[0]["total_views"])
                        .Replace("%TYPES%", typesAdd)                                                               // Types
                        .Replace("%TYPES_REMOVE%", typesRemove)
                        .Replace("%PFOLDERID%", data[0]["pfolderid"])                                               // Misc
                        );
                    break;
            }
        }
        private static void adminSettings(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Check if the user has requested to update a setting
            if (request.Form.Count != 0)
            {
                string queryUpdate = "";
                foreach (string key in request.Form.AllKeys)
                    if (key.StartsWith("setting_"))
                        queryUpdate += "UPDATE settings SET value='" + Utils.Escape(request.Form[key]) + "' WHERE category='" + Utils.Escape(SETTINGS_KEY) + "' AND keyname='" + Utils.Escape(key.Remove(0, 8)) + "'; ";
                if (queryUpdate.Length > 0)
                    conn.Query_Execute(queryUpdate);
            }
            // Build content
            contentRight.Append(Core.templates[Base.TEMPLATE]["admin_settings_header"]);
            foreach (ResultRow setting in conn.Query_Read("SELECT keyname, value, description FROM settings WHERE category='" + Utils.Escape(SETTINGS_KEY) + "' ORDER BY keyname ASC"))
            {
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["admin_settings_item"]
                    .Replace("%KEYNAME%", HttpUtility.HtmlEncode(setting["keyname"]))
                    .Replace("%VALUE%", HttpUtility.HtmlEncode(setting["value"]))
                    .Replace("%DESCRIPTION%", HttpUtility.HtmlEncode(setting["description"]))
                    );
            }
            contentRight.Append(Core.templates[Base.TEMPLATE]["admin_settings_footer"]);
        }
        private static void adminRequests(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Check if the user has requested to delete all of the external request log entries
            if (request.QueryString["clear"] != null)
            {
                conn.Query_Execute("DELETE FROM um_external_requests;");
                response.Redirect(pageElements["URL"] + "/ubermedia/admin/requests");
            }
            // Build content
            int page = request.QueryString["p"] != null && isNumeric(request.QueryString["p"]) ? int.Parse(request.QueryString["p"]) : 1;
            if (page < 1) page = 1;

            string reqs = "";
            foreach (ResultRow req in conn.Query_Read("SELECT * FROM um_external_requests ORDER BY datetime DESC LIMIT " + ((PAGE_REQUESTS_ITEMSPERPAGE * page) - PAGE_REQUESTS_ITEMSPERPAGE) + ", " + PAGE_REQUESTS_ITEMSPERPAGE))
                reqs += Core.templates[Base.TEMPLATE]["admin_requests_item"]
                    .Replace("%REASON%", HttpUtility.HtmlEncode(req["reason"]))
                    .Replace("%DATETIME%", req["datetime"])
                    .Replace("%URL%", HttpUtility.HtmlEncode(req["url"]));
            contentRight.Append(
                Core.templates[Base.TEMPLATE]["admin_requests"]
                .Replace("%ITEMS%", reqs.Length > 0 ? reqs : "None.")
                .Replace("%P_N%", (page < int.MaxValue ? page + 1 : page).ToString())
                .Replace("%P_P%", (page > 1 ? page - 1 : page).ToString())
                .Replace("%PAGE%", page.ToString())
                );
        }
        private static void adminTerminals(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            switch (request.QueryString["3"])
            {
                case null:
                    // Build list of terminals for removal and config generation
                    string terminals = "";
                    string genTerminals = "";
                    foreach (ResultRow terminal in conn.Query_Read("SELECT * FROM um_terminals ORDER BY title ASC"))
                    {
                        terminals += Core.templates[Base.TEMPLATE]["admin_terminals_item"]
                            .Replace("%TERMINALID%", terminal["terminalid"])
                            .Replace("%TITLE%", HttpUtility.HtmlEncode(terminal["title"]))
                            .Replace("%UPDATED%", terminal["status_updated"].Length > 0 ? terminal["status_updated"] : "(never)");
                        genTerminals = "<option value=\"" + HttpUtility.HtmlEncode(terminal["terminalid"]) + "\">" + HttpUtility.HtmlEncode(terminal["title"]) + "</option>";
                    }
                    // Build content
                    contentRight.Append(
                        Core.templates[Base.TEMPLATE]["admin_terminals"]
                        .Replace("%TERMINALS%", terminals)
                        );
                    break;
                case "remove":
                    string ty = request.QueryString["t"];
                    if (ty == null || !isNumeric(ty))
                        return;
                    // Grab the info of the key
                    Result tdata = conn.Query_Read("SELECT terminalid, title FROM um_terminals WHERE terminalid='" + Utils.Escape(ty) + "'");
                    // Check if the user has confirmed the deletion, else prompt them
                    if (request.Form["confirm"] != null)
                    {
                        conn.Query_Execute("DELETE FROM um_terminals WHERE terminalid='" + Utils.Escape(tdata[0]["terminalid"]) + "'");
                        conn.Disconnect();
                        response.Redirect(pageElements["URL"] + "/ubermedia/admin/terminals");
                    }
                    else
                        contentRight.Append(
                            Core.templates[Base.TEMPLATE]["confirm"]
                            .Replace("%ACTION_TITLE%", "Deletion of Terminal")
                            .Replace("%ACTION_DESC%", "Are you sure you want to delete the terminal '" + tdata[0]["title"] + "' (TID: " + tdata[0]["terminalid"] + ")?")
                            .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/terminals/remove?t=" + tdata[0]["terminalid"])
                            .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin/terminals")
                            );
                    break;
            }
        }
        private static void adminTags(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Check what the user has requested
            switch (request.QueryString["3"])
            {
                case null:
                    // Check if the user has requested to add a tag
                    if (request.Form["title"] != null)
                    {
                        string title = request.Form["title"];
                        if (title.Length < TAG_TITLE_MIN || title.Length > TAG_TITLE_MAX)
                            throwError(ref pageElements, "Title must be " + TAG_TITLE_MIN + " to " + TAG_TITLE_MAX + " characters in length!");
                        else if (conn.Query_Count("SELECT COUNT('') FROM um_tags WHERE title LIKE '%" + Utils.Escape(title) + "%'") != 0)
                            throwError(ref pageElements, "A tag with the same title already exists!");
                        else
                            conn.Query_Execute("INSERT INTO um_tags (title) VALUES('" + Utils.Escape(title) + "');");
                    }
                    // Build tags
                    string tags = "";
                    foreach (ResultRow tag in conn.Query_Read("SELECT * FROM um_tags ORDER BY title ASC"))
                        tags += Core.templates[Base.TEMPLATE]["admin_tags_item"]
                            .Replace("%TAGID%", tag["tagid"])
                            .Replace("%TITLE%", HttpUtility.HtmlEncode(tag["title"]));
                    // Build content
                    contentRight.Append(Core.templates[Base.TEMPLATE]["admin_tags"]
                        .Replace("%TAGS%", tags));
                    break;
                case "remove":
                    string tagid = request.QueryString["t"];
                    if (tagid != null && isNumeric(tagid))
                    {
                        Result ty = conn.Query_Read("SELECT tagid, title FROM um_tags WHERE tagid='" + Utils.Escape(tagid) + "'");
                        if (ty.Rows.Count != 1)
                            return;
                        if (request.Form["confirm"] != null)
                        {
                            conn.Query_Execute("DELETE FROM um_tags WHERE tagid='" + Utils.Escape(tagid) + "'");
                            conn.Disconnect();
                            response.Redirect(pageElements["URL"] + "/ubermedia/admin/tags");
                        }
                        else
                            contentRight.Append(
                                Core.templates[Base.TEMPLATE]["confirm"]
                                .Replace("%ACTION_TITLE%", "Confirm Tag Deletion")
                                .Replace("%ACTION_DESC%", "Are you sure you want to remvoe tag '" + ty[0]["title"] + "'?")
                                .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/tags/remove?t=" + ty[0]["tagid"])
                                .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin/tags")
                                );
                    }
                    break;
            }
        }
        private static void adminRebuildFilmCache(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                UberMedia.FilmInformation.state = UberMedia.FilmInformation.State.Starting;
                conn.Query_Execute("UPDATE um_film_information_providers SET cache_updated=NULL");
                UberMedia.FilmInformation.cacheStart();
                response.Redirect(pageElements["URL"] + "/ubermedia/admin");
            }
            else
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["confirm"]
                    .Replace("%ACTION_TITLE%", "Rebuild Film Information Cache")
                    .Replace("%ACTION_DESC%", "Are you sure you want to rebuild the film information cache?")
                    .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/rebuild_film_cache")
                    .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin")
                    );
        }
        private static void adminConversionStart(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                UberMedia.ConversionService.startService();
                conn.Disconnect();
                response.Redirect(pageElements["URL"] + "/ubermedia/admin");
            }
            else
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["confirm"]
                    .Replace("%ACTION_TITLE%", "Start Conversion Service")
                    .Replace("%ACTION_DESC%", "Are you sure you want to start the conversion service?")
                    .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/conversion_start")
                    .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin")
                    );
        }
        private static void adminConversionStop(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                UberMedia.ConversionService.stopService();
                response.Redirect(pageElements["URL"] + "/ubermedia/admin");
            }
            else
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["confirm"]
                    .Replace("%ACTION_TITLE%", "Stop Conversion Service")
                    .Replace("%ACTION_DESC%", "Are you sure you want to stop the conversion service?")
                    .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/conversion_stop")
                    .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin")
                    );
        }
        private static void adminConversionClear(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                UberMedia.ConversionService.queue.Clear();
                conn.Disconnect();
                response.Redirect(pageElements["URL"] + "/admin");
            }
            else
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["confirm"]
                    .Replace("%ACTION_TITLE%", "Clear Conversion Service")
                    .Replace("%ACTION_DESC%", "Are you sure you want to clear the conversion service's queue?")
                    .Replace("%ACTION_URL%", "<!--URL-->/ubermedia/admin/conversion_clear")
                    .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin")
                    );
        }
        private static void adminUninstall(string pluginid, Connector conn, ref StringBuilder contentLeft, ref StringBuilder contentRight, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null)
            {
                File.Delete(Core.basePath + "/CMS.config"); // Delete CMS config
                Core.cmsStop(); // Restart the core
                Core.cmsStart();
                conn.Disconnect();
                response.Redirect(pageElements["URL"] + "/");
            }
            else
                contentRight.Append(
                    Core.templates[Base.TEMPLATE]["confirm"]
                    .Replace("%ACTION_TITLE%", "Uninstall Core")
                    .Replace("%ACTION_DESC%", "Are you sure you want to uninstall the core? This will not delete the database data, but only the Config.xml file...")
                    .Replace("%ACTION_URL%", "<!--URL-->/ubermeida/admin/uninstall")
                    .Replace("%ACTION_BACK%", "<!--URL-->/ubermedia/admin")
                    );
        }
        #endregion

        #region "Methods - Misc"
        public static void logExternalRequest(Connector conn, string reason, string url)
        {
            conn.Query_Execute("INSERT INTO um_external_requests (reason, url, datetime) VALUES('" + Utils.Escape(reason) + "', '" + Utils.Escape(url) + "', NOW());");
        }
        public enum NavItems
        {
            Home, Browse, Newest, Popular
        }
        public static void selectNavItem(ref Misc.PageElements elements, string name)
        {
            elements["NAVI_" + name] = "selected";
        }
        public static bool isNumeric(string data)
        {
            try
            {
                int.Parse(data); return true;
            }
            catch { return false; }
        }
        /// <summary>
        /// Breaks a line up by ensuring its seperated every so often.
        /// </summary>
        /// <returns></returns>
        public static string titleSplit(string text, int chars)
        {
            string[] words = text.Split(' ');
            string n = "";
            foreach (string s in words)
                if (s.Length > chars) n += s.Substring(0, chars) + "\n" + s.Substring(chars) + " ";
                else n += s + " ";
            return n.Remove(n.Length - 1, 1);
        }
        /// <summary>
        /// Creates a navigation bar for an item based on its pfolderid and vitemid.
        /// </summary>
        /// <param name="pfolderid"></param>
        /// <param name="vitemid"></param>
        public static string browseCreateNavigationBar(Connector conn, Misc.PageElements elements, string pfolderid, string vitemid_parent)
        {
            string html = "<a href=\"" + elements["URL"] + "/browse\">All</a>";
            if (pfolderid.Length == 0) return html;
            else
            {
                html += " &gt; <a href=\"" + elements["URL"] + "/browse/" + pfolderid + "\">" + (conn.Query_Scalar("SELECT title FROM um_physical_folders WHERE pfolderid='" + Utils.Escape(pfolderid) + "'") ?? "Failed to retrieve folder name.").ToString() + "</a>";
                if (vitemid_parent.Length == 0) return html;
            }
            int iterations = 0;
            string parent = vitemid_parent;
            Result t;
            string html2 = "";
            while (iterations < 20 || parent != "0")
            {
                t = conn.Query_Read("SELECT parent, title, vitemid FROM um_virtual_items WHERE vitemid='" + Utils.Escape(parent) + "'");
                if (t.Rows.Count != 1) break;
                parent = t[0]["parent"];
                html2 = " &gt; <a href=\"" + elements["URL"] + "/browse/" + pfolderid + "/" + t[0]["vitemid"] + "\">" + t[0]["title"] + "</a>" + html2;
                iterations++;
            }
            return html + html2;
        }
        public static void runIndexers(Connector conn)
        {
            foreach (ResultRow drive in conn.Query_Read("SELECT pfolderid, physicalpath, allow_web_synopsis FROM um_physical_folders ORDER BY pfolderid ASC"))
                if (!UberMedia.Indexer.threadPool.ContainsKey(drive["pfolderid"])) UberMedia.Indexer.indexDrive(drive["pfolderid"], drive["physicalpath"], drive["allow_web_synopsis"].Equals("1"));
        }
        public static string driveTitle(Result drives, string pfolderid)
        {
            foreach (ResultRow r in drives) if (r["pfolderid"] == pfolderid) return r["title"];
            return "Unknown";
        }
        public static void checkNotDoublePost(ref Misc.PageElements elements, HttpResponse response)
        {
            if (HttpContext.Current.Session["post_protect"] != null && DateTime.Now.Subtract((DateTime)HttpContext.Current.Session["post_protect"]).TotalSeconds < 2)
                response.Redirect(elements["URL"] + "/admin", true); // We specify true in-case of any overrides
            else if (HttpContext.Current.Session["post_protect"] != null) HttpContext.Current.Session["post_protect"] = DateTime.Now;
            else HttpContext.Current.Session.Add("post_protect", DateTime.Now);
        }
        /// <summary>
        /// Enters a command into the terminal buffer to control a media computer/terminal.
        /// </summary>
        /// <param name="command">The command to be executed e.g. play.</param>
        /// <param name="terminalid">The terminalid identifier of the media computer.</param>
        /// <param name="arguments">Any required arguments.</param>
        /// <param name="queue">True = command is queued, false = command is executed immediately.</param>
        /// <param name="Connector"></param>
        public static bool terminalBufferEntry(Connector conn, string command, string terminalid, string arguments, bool queue, bool onlineProtection)
        {
            // Ensure the mc/terminal is valid and responded at least a minute ago
            if (!onlineProtection || conn.Query_Count("SELECT COUNT('') FROM um_terminals WHERE status_updated >= DATE_SUB(NOW(), INTERVAL 1 MINUTE) AND terminalid='" + Utils.Escape(terminalid) + "'") == 1)
            {
                checkAutoIncrementSafety(conn);
                conn.Query_Execute("INSERT INTO um_terminal_buffer (command, terminalid, arguments, queue) VALUES('" + Utils.Escape(command) + "', '" + Utils.Escape(terminalid) + "', '" + Utils.Escape(arguments) + "', '" + (queue ? "1" : "0") + "')");
                return true;
            }
            else return false;
        }
        /// <summary>
        /// Returns the query/statement used to insert the proposed terminal buffer entry; this does not provide any online protection etc!
        /// </summary>
        /// <param name="command"></param>
        /// <param name="terminalid"></param>
        /// <param name="arguments"></param>
        /// <param name="queue"></param>
        /// <param name="onlineProtection"></param>
        /// <returns></returns>
        public static string terminalBufferEntry(Connector conn, string command, string terminalid, string arguments, bool queue)
        {
            checkAutoIncrementSafety(conn);
            return "INSERT INTO um_terminal_buffer (command, terminalid, arguments, queue) VALUES('" + Utils.Escape(command) + "', '" + Utils.Escape(terminalid) + "', '" + Utils.Escape(arguments) + "', '" + (queue ? "1" : "0") + "');";
        }
        private const long TERMINAL_BUFFER_AUTOINCREMENT_LIMIT = long.MaxValue - 100000;
        /// <summary>
        /// Checks the auto-increment for the terminal buffer table has not been exceeded, else the value is reset the table is emptied.
        /// </summary>
        public static void checkAutoIncrementSafety(Connector conn)
        {
            if (long.Parse(conn.Query_Scalar("SELECT Auto_increment FROM information_schema.tables WHERE table_name='um_terminal_buffer' AND table_schema = DATABASE();").ToString()) >= TERMINAL_BUFFER_AUTOINCREMENT_LIMIT)
                conn.Query_Execute("DELETE FROM um_terminal_buffer; ALTER TABLE um_terminal_buffer AUTO_INCREMENT=1;");
        }
        /// <summary>
        /// Throws a simple error message by setting the display style to block (so the element is visible to the user) and a message.
        /// </summary>
        /// <param name="message">The message to be displayed to the user.</param>
        public static void throwError(ref Misc.PageElements elements, string message)
        {
            elements["ERR_STYLE"] = "display: block !important; visibility: visible !important;";
            elements["ERR_MSG"] = HttpUtility.HtmlEncode(message);
        }
        /// <summary>
        /// Parses the provided URL and returns the extracted video ID. If no ID can be extracted, null is returned.
        /// </summary>
        /// <returns></returns>
        public static string parseYouTubeURL(string url)
        {
            // -- Credit: http://stackoverflow.com/questions/2597080/regex-to-parse-youtube-yid
            Match m = Regex.Match(url, "(?<=v=)[a-zA-Z0-9-_]+(?=&)|(?<=[0-9]/)[^&\n]+|(?<=v=)[^&\n]+");
            if (m.Success)
                return m.Groups[0].Value;
            else return null;
        }
        /// <summary>
        /// Validates the text is a valid folder title by checking each char is either:
        /// > alphabetical
        /// > numeric
        /// > space
        /// > * asterisk
        /// > - hyphen
        /// > _ under-scroll
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool isAlphaNumericSpace(string input)
        {
            foreach (char c in input)
                if (c != 32 && c != 45 && !(c >= 48 && c <= 57) && !(c >= 65 && c <= 90) && c != 95 && !(c >= 97 && c <= 122))
                    return false;
            return true;
        }
        /// <summary>
        /// Builds an insert-statement to insert a command for every terminal.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        /// <param name="queue"></param>
        /// <returns>A string if successful, else null if no terminals.</returns>
        public static string buildCommandStatementAllTerminals(Connector conn, string command, string args, bool queue)
        {
            Result terminals = conn.Query_Read("SELECT terminalid FROM um_terminals");
            if (terminals.Rows.Count > 0)
            {
                // Build the right-hand each row
                string query = ", '" + Utils.Escape(command) + "', " + (args != null && args.Length > 0 ? "'" + Utils.Escape(args) + "'" : "NULL") + ", '" + (queue ? "1" : "0") + "'),";
                // Build the actual query
                StringBuilder statement = new StringBuilder("INSERT INTO um_terminal_buffer (terminalid, command, arguments, queue) VALUES");
                // Add each terminal
                foreach (ResultRow terminal in terminals)
                    statement.Append("('").Append(Utils.Escape(terminal["terminalid"])).Append("'").Append(query);
                // Remove tailing comma
                statement.Remove(statement.Length - 1, 1);
                // Return statement
                return statement.ToString();
            }
            else
                return null;
        }
        #endregion
    }
}
