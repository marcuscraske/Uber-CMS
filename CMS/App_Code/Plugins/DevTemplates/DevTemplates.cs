using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;

namespace UberCMS.Plugins
{
    public static class DevTemplates
    {
        public static string enable(string pluginid, Connector conn)
        {
            Misc.Plugins.reserveURLs(pluginid, null, new string[] { "devdump", "devupload" }, conn);
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            Misc.Plugins.unreserveURLs(pluginid, conn);
            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
#if DEBUG
            switch (request.QueryString["page"])
            {
                case "devdump":
                    pageDevDump(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "devupload":
                    pageDevUpload(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
            }
#endif
        }
        public static void pageDevDump(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            string dumpPath;
            string parent = request.QueryString["2"];
            if (request.QueryString["1"] != null)
            {
                dumpPath = Misc.Plugins.getPluginBasePath(request.QueryString["1"], conn);
                if (dumpPath == null) return;
                dumpPath += "/Templates";
                if (!System.IO.Directory.Exists(dumpPath))
                    System.IO.Directory.CreateDirectory(dumpPath);
            }
            else
                dumpPath = Core.basePath + "\\Installer\\Templates";
            if (dumpPath != null)
            {
                Core.templates.dump(conn, dumpPath, parent);
                pageElements["TITLE"] = "Developers - Dump Templates";
                pageElements["CONTENT"] = "<p>Dumped templates to <i>'" + dumpPath + "'</i>...</p>";
            }
        }
        public static void pageDevUpload(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            string targetPluginid = request.QueryString["1"];
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
            else
            {
                pageElements["CONTENT"] = "You must specify a pluginid: /devupload/<pluginid>";
            }
            pageElements["TITLE"] = "Developers - Upload Templates";
        }
    }
}
