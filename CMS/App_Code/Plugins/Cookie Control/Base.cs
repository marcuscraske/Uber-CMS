 ﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS
 * File:                    /App_Code/Plugins/Cookie Control/Base.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * A simple plugin to enable/disable cookies on the serverside.
 */
using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;

namespace UberCMS.Plugins
{
    public static class CookieControl
    {
        public static string enable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Install content
            if ((error = Misc.Plugins.contentInstall(basePath + "\\Content")) != null)
                return error;
            // Install templates
            if ((error = Misc.Plugins.templatesInstall(basePath + "\\Templates", conn)) != null)
                return error;
            // Reserve pages
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "cookiecontrol" }, conn)) != null)
                return error;
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Unreserve pages
            if ((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;
            // Uninstall content
            if ((error = Misc.Plugins.contentUninstall(basePath + "\\Content")) != null)
                return error;
            // Uninstall templates
            if ((error = Misc.Plugins.templatesUninstall(basePath + "\\Templates", conn)) != null)
                return error;
            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Toggle cookie-control
            HttpCookie cookie = request.Cookies["cookie-control"];
            if (cookie != null)
            {
                cookie.Expires = DateTime.Now.AddDays(-1);
                response.Cookies.Add(cookie);
            }
            else
                response.Cookies.Add(new HttpCookie("cookie-control", "1"));
            // Redirect to the origin or homepage
            if (request.UrlReferrer != null)
                response.Redirect(request.UrlReferrer.AbsoluteUri);
            else
                response.Redirect(pageElements["URL"]);
        }
        public static void requestEnd(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {

            bool cookiesEnabled = request.Cookies["cookie-control"] != null;
            // Add styling and toggle button
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/CookieControl.css", ref pageElements);
            Misc.Plugins.addHeaderJS(pageElements["URL"] + "/Content/JS/CookieControl.js", ref pageElements);
            // Add toggle button
            pageElements.appendToKey("BODY_FOOTER", Core.templates["cookiecontrol"]["toggle"]);
            // Add warning banner
            if (!cookiesEnabled)
                pageElements.appendToKey("BODY_HEADER", Core.templates["cookiecontrol"]["banner"]);
            else
            {
                // Check if cookies have been enabled, if so return - no need to remove cookies
                pageElements.setFlag("COOKIES_ON");
                return;
            }
            // Clear all the response cookies - these may have been added programmatically
            response.Cookies.Clear();
            // Add each cookie, sent in the request, in the response - to expire
            HttpCookie cookie;
            for (int i = 0; i < request.Cookies.Count; i++)
            {
                cookie = request.Cookies[i];
                if (cookie.Name != "ASP.NET_SessionId")
                {
                    cookie.Expires = DateTime.Now.AddDays(-2);
                    response.Cookies.Add(cookie);
                }
            }
        }
    }
}
