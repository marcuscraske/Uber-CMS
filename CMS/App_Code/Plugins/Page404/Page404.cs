using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;

namespace UberCMS.Plugins
{
    public static class Page404
    {
        public static void handleRequestNotFound(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            response.StatusCode = 404;
            pageElements["TITLE"] = "Page Not Found";
            pageElements["CONTENT"] = "<p>The requested resource could not be located...</p>";
        }
    }
}
