using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;

namespace UberCMS.Plugins
{
    public static class DevDump
    {
        /// <summary>
        /// Invoked to enable the plugin.
        /// </summary>
        /// <returns></returns>
        public static string enable(string pluginid, Connector conn)
        {
            conn.Query_Execute("INSERT INTO urlrewriting (pluginid, title) VALUES('" + Utils.Escape(pluginid) + "', 'devdump')");
            return null;
        }
        /// <summary>
        /// Invoked when a request is mapped to this plugin.
        /// </summary>
        /// <param name="pageElements"></param>
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
#if DEBUG
            string dumpPath = Core.basePath + "\\Installer\\Templates";
            Core.templates.dump(conn, dumpPath);
            pageElements["TITLE"] = "Developers - Dump Templates";
            pageElements["CONTENT"] = "<p>Dumped templates to <i>'" + dumpPath + "'</i>...</p>";
#endif
        }
    }
}
