using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;

namespace UberCMS.Plugins
{
    public static class DevDump
    {
        public static string enable(string pluginid, Connector conn)
        {
            Misc.Plugins.reserveURLs(pluginid, null, new string[] { "devdump" }, conn);
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
#endif
        }
    }
}
