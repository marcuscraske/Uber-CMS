﻿using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using System.IO;

namespace UberCMS.Plugins
{
    public static class AdminPanel
    {
        #region "Methods - Plugin Event Handlers"
        public static string enable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Install SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Install.sql", conn)) != null)
                return error;
            // Install templates
            if ((error = Misc.Plugins.templatesInstall(basePath + "\\Templates", conn)) != null)
                return error;
            // Install default admin pages
            adminPage_Install("UberCMS.Plugins.AdminPanel", "pageEmailQueue", "E-mail Queue", "Core", "Content/Images/admin_panel/admin_email_queue.png", conn);
            adminPage_Install("UberCMS.Plugins.AdminPanel", "pagePlugins", "Plugins", "Core", "Content/Images/admin_panel/admin_plugins.png", conn);
            adminPage_Install("UberCMS.Plugins.AdminPanel", "pageSettings", "Settings", "Core", "Content/Images/admin_panel/admin_settings.png", conn);
            // Install content
            if ((error = Misc.Plugins.contentInstall(basePath + "\\Content")) != null)
                return error;
            // Reserve URLs
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "admin" }, conn)) != null)
                return error;
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            return "You cannot disable the admin panel!";
        }
        public static string uninstall(string pluginid, Connector conn)
        {
#if DEBUG
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Unreserve URLs
            if ((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;
            // Uninstall default admin pages
            adminPage_Uninstall("UberCMS.Plugins.BSA_Admin", "pageEmailQueue", conn);
            adminPage_Uninstall("UberCMS.Plugins.BSA_Admin", "pagePlugins", conn);
            adminPage_Uninstall("UberCMS.Plugins.BSA_Admin", "pageSettings", conn);
            // Uninstall content
            if ((error = Misc.Plugins.contentUninstall(basePath + "\\Content")) != null)
                return error;
            // Uninstall templates
            if ((error = Misc.Plugins.templatesUninstall("admin_panel", conn)) != null)
                return error;
            // Uninstall SQL
            if((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Uninstall.sql", conn)) != null)
                return error;
            return null;
#else
            return "Cannot uninstall the admin panel - modify the web.config file to run the site in debug-mode! This is protection against accidental uninstallation...";
#endif
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            switch (request.QueryString["page"])
            {
                case "admin":
                    pageAdmin(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
            }
        }
        #endregion

        #region "Methods - Pages"
        /// <summary>
        /// Administration page, used for managing various core functions of the CMS as well as plugins.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageAdmin(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Attach CSS file
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/AdminPanel.css", ref pageElements);
            // Check user has admin access
            Result authCheck = conn.Query_Read("SELECT g.access_admin FROM bsa_users AS u LEFT OUTER JOIN bsa_user_groups AS g ON g.groupid=u.groupid WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
            if (authCheck.Rows.Count != 1 || !authCheck[0]["access_admin"].Equals("1"))
                return;
            // Handle the request and build the content based on the selected page
            string pageid = request.QueryString["1"];
            if (pageid == null)
            {
                // No page requested, display welcome message
#if ADMIN_PANEL
                pageElements["ADMIN_CONTENT"] = Core.templates["admin_panel"]["welcome"];
#else
                pageElements["ADMIN_CONTENT"] = Core.templates["admin_panel"]["welcome_warning"];
#endif
                pageElements["ADMIN_TITLE"] = "Welcome!";
            }
            else
            {
                // Grab the classpath
                Result page = conn.Query_Read("SELECT classpath, method FROM admin_panel_pages WHERE pageid='" + Utils.Escape(pageid) + "'");
                if (page.Rows.Count != 1)
                    return;
                // Set the admin URL
                pageElements["ADMIN_URL"] = pageElements["URL"] + "/admin/" + pageid;
                // Invoke the page handler
                if (!Misc.Plugins.invokeMethod(page[0]["classpath"], page[0]["method"], new object[] { conn, pageElements, request, response, baseTemplateParent }))
                    return;
                else if (pageElements["ADMIN_CONTENT"] == null || pageElements["ADMIN_CONTENT"].Length == 0)
                    return;
            }
            // Build menu
            StringBuilder menu = new StringBuilder();
            string currentHeader = null;
            foreach (ResultRow item in conn.Query_Read("SELECT pageid, title, category, menu_icon FROM admin_panel_pages ORDER BY category ASC, title ASC"))
            {
                if (item["category"] != currentHeader)
                {
                    currentHeader = item["category"];
                    menu.Append(
                        Core.templates["admin_panel"]["menu_header"]
                        .Replace("%TEXT%", HttpUtility.HtmlEncode(currentHeader))
                        );
                }
                menu.Append(
                    Core.templates["admin_panel"]["menu_item"]
                    .Replace("%URL%", pageElements["URL"] + "/admin/" + item["pageid"])
                    .Replace("%ICON%", HttpUtility.UrlEncode(item["menu_icon"]))
                    .Replace("%TEXT%", HttpUtility.HtmlEncode(item["title"]))
                    );
            }
            // Set page
            pageElements["TITLE"] = "Admin";
            pageElements["CONTENT"] = Core.templates["admin_panel"]["admin"]
                .Replace("%MENU%", menu.ToString());
        }
        #endregion

        #region "Methods - Admin Pages"
        public static void pageEmailQueue(Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Check for e-mail deletion
            string deleteEmailID = request.QueryString["delete"];
            if (deleteEmailID != null)
            {
                conn.Query_Execute("DELETE FROM email_queue WHERE emailid='" + Utils.Escape(deleteEmailID) + "'");
                conn.Disconnect();
                response.Redirect(pageElements["ADMIN_URL"], true);
            }
            // Grab statistics about the number of e-mails pending
            ResultRow queueStats = conn.Query_Read("SELECT (SELECT COUNT('') FROM email_queue) AS count, (SELECT COUNT(DISTINCT email) FROM email_queue) AS unique_count")[0];
            // Generate a list of pending e-mails at the top of the queue
            StringBuilder pending = new StringBuilder();
            foreach (ResultRow email in conn.Query_Read("SELECT * FROM email_queue ORDER BY emailid ASC LIMIT 10"))
                pending.Append(
                    Core.templates["admin_panel"]["emailqueue_item"]
                    .Replace("%EMAILID%", HttpUtility.HtmlEncode(email["emailid"]))
                    .Replace("%EMAIL%", HttpUtility.HtmlEncode(email["email"]))
                    .Replace("%SUBJECT%", HttpUtility.HtmlEncode(email["subject"]))
                    );
            if (pending.Length == 0) pending.Append("No e-mails in the queue!");
            // Display page
            pageElements["ADMIN_CONTENT"] =
                Core.templates["admin_panel"]["emailqueue"]
                .Replace("%COUNT%", HttpUtility.HtmlEncode(queueStats["count"]))
                .Replace("%UNIQUE_COUNT%", HttpUtility.HtmlEncode(queueStats["unique_count"]))
                .Replace("%ERRORS%", HttpUtility.HtmlEncode(Core.emailQueue.mailErrors.ToString()))
                .Replace("%THREAD_STATUS%", HttpUtility.HtmlEncode(Core.emailQueue.cyclerThread != null ? Core.emailQueue.cyclerThread.ThreadState.ToString() : "Not operational - critical failure or undefined mail settings."))
                .Replace("%EMAILS%", pending.ToString())
                ;
            pageElements["ADMIN_TITLE"] = "Core - E-mail Queue";
        }
        public static void pageSettings(Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            string error = null;
            bool successfullyUpdated = false;
            // Check if the user has requested to delete a setting
            string deleteCategory = request.QueryString["delete_category"];
            string deleteKey = request.QueryString["delete_key"];
            if (deleteCategory != null && deleteKey != null)
            {
                conn.Query_Execute("DELETE FROM settings WHERE category='" + Utils.Escape(deleteCategory) + "' AND keyname='" + Utils.Escape(deleteKey) + "'");
                response.Redirect(pageElements["ADMIN_URL"]);
            }
            // Check if the user has posted updated settings
            Dictionary<string[], string> updatedSettings = new Dictionary<string[], string>();
            string category, key, value;
            string[] rawKey;
            for (int i = 0; i < request.Form.Count; i++)
            {
                rawKey = request.Form.Keys[i].Split('$');
                value = request.Form[i];
                if (rawKey.Length == 2 && rawKey[0].StartsWith("setting_") && rawKey[0].Length > 10 && Plugins.BasicSiteAuth.validateAlphaNumericUnderscroll(key = rawKey[0].Substring(8)))
                    updatedSettings.Add(new string[] { key, rawKey[1] }, value);
            }
            if (updatedSettings.Count > 0)
            {
                StringBuilder updateQuery = new StringBuilder();
                foreach (KeyValuePair<string[], string> setting in updatedSettings)
                    updateQuery.Append("UPDATE settings SET value='" + Utils.Escape(setting.Value) + "' WHERE category='" + Utils.Escape(setting.Key[0]) + "' AND keyname='" + Utils.Escape(setting.Key[1]) + "';");
                try
                {
                    conn.Query_Execute(updateQuery.ToString());
                    successfullyUpdated = true;
                }
                catch (Exception ex)
                {
                    error = "Failed to update settings - " + ex.Message + "!";
                }
            }
            // Display the settings
            StringBuilder settingItems = new StringBuilder();
            category = null;
            foreach (ResultRow setting in conn.Query_Read("SELECT category, keyname, value, description FROM settings ORDER BY category ASC, keyname ASC"))
            {
                if (setting["category"] != category)
                {
                    category = setting["category"];
                    settingItems.Append(
                        Core.templates["admin_panel"]["settings_category"]
                        .Replace("%CATEGORY%", HttpUtility.HtmlEncode(category))
                        );
                }
                settingItems.Append(
                    Core.templates["admin_panel"]["settings_item"]
                    .Replace("%KEYNAME%", HttpUtility.HtmlEncode(setting["keyname"]))
                    .Replace("%KEYNAME_URL%", HttpUtility.UrlEncode(setting["keyname"]))
                    .Replace("%CATEGORY%", HttpUtility.HtmlEncode(setting["category"]))
                    .Replace("%CATEGORY_URL%", HttpUtility.UrlEncode(setting["category"]))
                    .Replace("%VALUE%", HttpUtility.HtmlEncode(request.Form["setting_" + setting["keyname"]] ?? setting["value"]))
                    .Replace("%DESCRIPTION%", HttpUtility.HtmlEncode(setting["description"]))
                    );
            }
            pageElements["ADMIN_CONTENT"] =
                Core.templates["admin_panel"]["settings"]
                .Replace("%ITEMS%", settingItems.ToString())
                .Replace("%SUCCESS%", successfullyUpdated ? Core.templates[baseTemplateParent]["success"].Replace("%SUCCESS%", "Successfully saved settings!") : string.Empty)
                .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty);
            pageElements["ADMIN_TITLE"] = "Core - Settings";
        }
        public static void pagePlugins(Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            string error = null;
            // Check for action
            string pluginid = request.QueryString["pluginid"];
            string action = request.QueryString["action"];
            // Process action
            if (action != null)
            {
                // Check if a pluginid has been specified, if so load it
                Result pluginidData = null;
                if (pluginid != null)
                    pluginidData = conn.Query_Read("SELECT * FROM plugins WHERE pluginid='" + Utils.Escape(pluginid) + "'");
                if (pluginidData != null && pluginidData.Rows.Count != 1)
                    error = "Invalid pluginid specified!";
                else if (pluginidData != null && action == "enable")
                {
                    if ((Plugins.Base.State)Enum.Parse(typeof(Plugins.Base.State), pluginidData[0]["state"]) == Plugins.Base.State.Enabled)
                        error = "Plugin is already enabled.";
                    else
                    {
                        error = Misc.Plugins.enable(pluginidData[0]["pluginid"], conn);
                        if (error == null) // Operation successful
                        {
                            conn.Disconnect();
                            response.Redirect(pageElements["ADMIN_URL"], true);
                        }
                    }
                }
                else if (pluginidData != null && action == "disable")
                {
                    if ((Plugins.Base.State)Enum.Parse(typeof(Plugins.Base.State), pluginidData[0]["state"]) == Plugins.Base.State.Disabled)
                        error = "Plugin is already disabled.";
                    else
                    {
                        error = Misc.Plugins.disable(pluginidData[0]["pluginid"], conn);
                        if (error == null) // Operation successful
                        {
                            conn.Disconnect();
                            response.Redirect(pageElements["ADMIN_URL"], true);
                        }
                    }
                }
                else if (pluginidData != null && action == "uninstall")
                {
                    // Get the user to confirm the uninstallation
                    string confirm = request.QueryString["confirm"];
                    if (confirm != null)
                    {
                        // Uninstall the plugin
                        error = Misc.Plugins.uninstall(pluginidData[0]["pluginid"], true, conn);
                        if (error == null) // Operation successful
                        {
                            conn.Disconnect();
                            response.Redirect(pageElements["ADMIN_URL"], true);
                        }
                    }
                    // Display confirmation form
                    pageElements["ADMIN_CONTENT"] = Core.templates["admin_panel"]["plugin_uninstall"]
                        .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty)
                        .Replace("%PLUGINID%", HttpUtility.HtmlEncode(pluginidData[0]["pluginid"]))
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(pluginidData[0]["title"]));
                    pageElements["ADMIN_TITLE"] = "Core - Plugins - Uninstall";
                    return;
                }
                else if (action == "upload")
                {
                    HttpPostedFile upload = request.Files["plugin_upload"];
                    if (upload == null || upload.ContentLength == 0 || (upload.ContentType != "application/zip" && upload.ContentType != "application/x-zip" && upload.ContentType != "application/x-zip-compressed"))
                        error = "Invalid plugin zip archive specified!";
                    else
                    {
                        // Save the zip
                        string zipPath = Core.basePath + "\\Cache\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + new Random().Next(1, int.MaxValue) + ".zip";
                        upload.SaveAs(zipPath);
                        // Attempt to install it
                        error = Misc.Plugins.install(zipPath, true, conn);
                        if (error != null)
                            try
                            {
                                File.Delete(zipPath);
                            }
                            catch { }
                    }
                }
                else
                    error = "Unhandled action!";
            }
            // List the plugins
            StringBuilder pluginsList = new StringBuilder();
            string state;
            string buttons;
            foreach (ResultRow plugin in conn.Query_Read("SELECT * FROM plugins ORDER BY title ASC"))
            {
                switch ((Plugins.Base.State)Enum.Parse(typeof(Plugins.Base.State), plugin["state"]))
                {
                    case Plugins.Base.State.Disabled:
                        state = "Disabled";
                        buttons = Core.templates["admin_panel"]["plugin_buttdisabled"].Replace("%PLUGINID%", HttpUtility.HtmlEncode(plugin["pluginid"])); ;
                        break;
                    case Plugins.Base.State.Enabled:
                        state = "Enabled";
                        buttons = Core.templates["admin_panel"]["plugin_buttenabled"].Replace("%PLUGINID%", HttpUtility.HtmlEncode(plugin["pluginid"]));
                        break;
                    default:
                        state = "Unknown";
                        buttons = string.Empty;
                        break;
                }
                pluginsList.Append(
                    Core.templates["admin_panel"]["plugins_item"]
                    .Replace("%PLUGINID%", HttpUtility.HtmlEncode(plugin["pluginid"]))
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(plugin["title"]))
                    .Replace("%STATE%", HttpUtility.HtmlEncode(state))
                    .Replace("%BUTTONS%", buttons)
                    .Replace("%DIRECTORY%", HttpUtility.HtmlEncode(plugin["directory"]))
                    .Replace("%PRIORITY%", HttpUtility.HtmlEncode(plugin["invoke_order"]))
                    );
            }
            // Display content
            pageElements["ADMIN_CONTENT"] = Core.templates["admin_panel"]["plugins"]
                .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty)
                .Replace("%ITEMS%", pluginsList.ToString())
                ;
            pageElements["ADMIN_TITLE"] = "Core - Plugins";
        }
        #endregion

        #region "Methods - Pages Installation"
        public static void adminPage_Install(string classpath, string method, string title, string category, string menuIcon, Connector conn)
        {
            conn.Query_Execute("INSERT INTO admin_panel_pages (classpath, method, title, category, menu_icon) VALUES('" + Utils.Escape(classpath) + "', '" + Utils.Escape(method) + "', '" + Utils.Escape(title) + "', '" + Utils.Escape(category) + "', '" + Utils.Escape(menuIcon) + "')");
        }
        public static void adminPage_Uninstall(string classpath, string method, Connector conn)
        {
            conn.Query_Execute("DELETE FROM admin_panel_pages WHERE classpath='" + Utils.Escape(classpath) + "' AND method='" + Utils.Escape(method) + "'");
        }
        #endregion
    }
}