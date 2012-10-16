 ﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS
 * File:                    /App_Code/Plugins/MathJax/Base.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Provides Maths markup for formatting using i.e. Tex.
 */
using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;

namespace UberCMS.Plugins
{
    public static class MathJax
    {
        public static string enable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Install files
            if((error = Misc.Plugins.contentInstall(basePath + "\\Content")) != null)
                return error;
            // Enable format provider
            if (!Common.formatProvider_add(conn, pluginid, "", "", "UberCMS.Plugins.MathJax", "formatIncludes"))
                return "Failed to enable format provider!";

            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Remove files
            if ((error = Misc.Plugins.contentUninstall(basePath + "\\Content")) != null)
                return error;
            // Disable format provider
            Common.formatProvider_remove(conn, pluginid);

            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            return null;
        }
        public static void formatIncludes(HttpRequest request, HttpResponse response, Connector connector, ref Misc.PageElements pageElements, bool formattingText, bool formattingObjects)
        {
            Misc.Plugins.addHeaderJS(pageElements["URL"] + "/Content/MathJax/MathJax/MathJax.js?config=default", ref pageElements);
        }
    }
}
