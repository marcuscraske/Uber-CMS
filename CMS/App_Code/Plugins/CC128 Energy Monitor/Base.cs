using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;

namespace UberCMS.Plugins
{
    public static class CC128
    {
        public static string enable(string pluginid, Connector conn)
        {
            // Reserve URLs

            // Install templates
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            return null;
        }
        public static string cmsStart(string pluginid, Connector conn)
        {
            return "Base plugin cannot be started";
        }
        public static string cmsStop(string pluginid, Connector conn)
        {
            return "Base plugin cannot be stopped...nananananna BATMAN!";
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
        }
        public static void requestStart(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
        }
        public static void requestEnd(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
        }
        public static void handleError(string pluginid, Exception ex)
        {
        }
        public static void handleCycle(string pluginid, Connector conn)
        {
        }
    }
}
