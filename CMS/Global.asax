<%@ Application Language="C#" %>
<script runat="server">
    void Application_Start(object sender, EventArgs e) 
    {
        UberCMS.Core.cmsStart();
    }
    void Application_End(object sender, EventArgs e) 
    {
        UberCMS.Core.cmsStop();
    }
    void Application_Error(object sender, EventArgs e) 
    {
        UberCMS.Core.cmsError(Server.GetLastError());
    }
    void Application_BeginRequest(object sender, EventArgs e)
    {
        // This code rewrites the URL
        string path = System.Web.HttpContext.Current.Request.Path;
        string pathL = path.ToLower();
        if (pathL.StartsWith("/content") || path.Equals("/favicon.ico")) return;
#if !INSTALLED
        else if (pathL.StartsWith("/install/content/"))
            System.Web.HttpContext.Current.RewritePath(Request.ApplicationPath + "/Installer/Content/" + path.Substring(17));
        else if (pathL.StartsWith("/install"))
            rewriteNewPath(Request.ApplicationPath + "/Installer/Installer.aspx", Request.ApplicationPath + "/Installer/Installer.aspx?page=home");
#endif
        else if (pathL.StartsWith("/archive")) System.Web.HttpContext.Current.RewritePath(Request.ApplicationPath + "/Default.aspx?page=404");
        else rewriteNewPath(Request.ApplicationPath + "Default.aspx", Request.ApplicationPath + "Default.aspx?page=home");
    }
    void rewriteNewPath(string newpath, string newpath_default)
    {
        string[] items = System.Web.HttpContext.Current.Request.Path.Split('/');
        if (items.Length < 1 || (items.Length > 1 && items[0].Length == 0 && items[1].Length == 0) || System.Web.HttpContext.Current.Request.Path.ToLower().Equals("/default.aspx"))
            System.Web.HttpContext.Current.RewritePath(newpath_default, true);
        else
        {
            // Add each dir as a query-string
            int ai = 0;
            for (int i = 0; i < items.LongLength; i++)
            {
                if (items[i] != "")
                {
                    if (ai == 0) newpath += "?page=" + items[i] + "&";
                    else newpath += ai.ToString() + "=" + items[i] + "&";
                    ai++;
                }
            }
            // Add each pre-existing query-string
            if (Request.QueryString.Count != 0)
            {
                for (int q = 0; q < Request.QueryString.Count; q++)
                    newpath += Request.QueryString.Keys[q] + "=" + Request.QueryString[q] + "&";
            }
            // Remove newpath '&' char
            newpath = newpath.Remove(newpath.Length - 1, 1);
            // Rewrite URL to new one
            System.Web.HttpContext.Current.RewritePath(newpath, true);
        }
    }
</script>
