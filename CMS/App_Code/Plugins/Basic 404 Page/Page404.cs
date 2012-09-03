 ﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS / Plugins / Basic 404 Page
 * File:                    /App_Code/Plugins/Basic 404 Page/Page404.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * A basic 404 handler page/plugin.
 */
using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;

namespace UberCMS.Plugins
{
    public static class Page404
    {
        public static void handleRequestNotFound(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            response.StatusCode = 404;
            pageElements["TITLE"] = "Page Not Found";
            pageElements["CONTENT"] = "<div class=\"TC\"><img src=\"" + pageElements["URL"] + "/Content/Images/Page404/cat.png\" alt=\"Page Not Found\" /></div><p>The requested resource could not be located...</p>";
        }
        public static string enable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Install content
            Misc.Plugins.contentInstall(basePath + "\\Content");
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Uninstall content
            Misc.Plugins.contentUninstall(basePath + "\\Content");
            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            return null;
        }
    }
}
