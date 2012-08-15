﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS
 * File:                    /Default.aspx.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Responsible for handling, URL-rewritten, requests.
 */
using System;
using System.Collections.Generic;
using System.Web;
using UberLib.Connector;
using System.Text;
using UberCMS;
using UberCMS.Misc;
using System.Diagnostics;

public partial class _Default : System.Web.UI.Page
{
    #region "Variables"
    private Connector conn;
    private PageElements elements;
    #endregion

    #region "Methods - Events"
    protected void Page_Load(object sender, EventArgs e)
    {
        // Begin recording the time for the request to be processed
        Stopwatch timer = new Stopwatch();
        timer.Start();
        // Check the core is operational
        switch (Core.state)
        {
            case Core.State.NotInstalled:
                Response.Redirect("/installer", true);
                break;
            case Core.State.CriticalFailure:
                Response.Write("<html><head><title>Uber CMS - Critical Failure</title></head><body>");
                Response.Write("<h1>Core Failure - Critical Error</h1>");
                Response.Write("<h2>Error</h2><p>" + Core.criticalFailureError.Message + "</p><h2>Stack-trace</h2><p>" + Core.criticalFailureError.StackTrace + "</p>");
                Response.Write("<h2>Base Error</h2><p>" + Core.criticalFailureError.GetBaseException().Message + "</p><h2>Base Stack-trace</h2><p>" + Core.criticalFailureError.GetBaseException().StackTrace + "</p>");
                Response.Write("</body></html>");
                Response.End();
                break;
            case Core.State.Stopped:
                Core.cmsStart();
                break;
        }
        // Setup the request
        conn = Core.connectorCreate(true);
        elements = new PageElements();
        elements["URL"] = ResolveUrl("");
        string baseTemplateParent = "default";
        // If debugging, reload cached templates
#if DEBUG
        Core.templates.reloadDb(conn);
#endif
        // Invoke the pre-handler methods
        Result plugins = conn.Query_Read("SELECT pluginid, classpath FROM plugins WHERE state='" + (int)UberCMS.Plugins.Base.State.Enabled + "' AND handles_request_start='1' ORDER BY invoke_order ASC");
        foreach (ResultRow plugin in plugins)
            Plugins.invokeMethod(plugin["classpath"], "requestStart", new object[] { plugin["pluginid"], conn, elements, Request, Response, baseTemplateParent });
        
        // Invoke the plugin responsible for handling the request
        int i;
        Plugins.Request.RequestHandler handler;
        Plugins.Request.RequestHandlers handlers = Plugins.Request.getRequestPlugin(conn, Request);
        for (i = 0; i < handlers.count() && elements["CONTENT"] == null; i++)
        {
            handler = handlers[i];
            Plugins.invokeMethod(handler.ClassPath, "handleRequest", new object[] { handler.Pluginid, conn, elements, Request, Response, baseTemplateParent });
        }

        // If no content has been set, a 404 has occurred - hence find a plugin to handle this issue
        handlers = Plugins.Request.getRequest404s(conn);
        for (i = 0; i < handlers.count() && elements["CONTENT"] == null; i++)
        {
            handler = handlers[i];
            Plugins.invokeMethod(handler.ClassPath, "handleRequestNotFound", new object[] { handler.Pluginid, conn, elements, Request, Response, baseTemplateParent });
        }

        // Check if any content has still not been set - if so we'll display a manual error to inform the user/developer/administrator/operator
        if(elements["CONTENT"] == null)
        {
            // 404 not found...
            elements["TITLE"] = "Page Not Found";
            elements["CONTENT"] = "<p>The requested page could not be found and a 404 page could not be served...</p>";
        }

        // Invoke the post-handler methods
        plugins = conn.Query_Read("SELECT pluginid, classpath FROM plugins WHERE state='" + (int)UberCMS.Plugins.Base.State.Enabled + "' AND handles_request_end='1' ORDER BY invoke_order ASC");
        foreach (ResultRow plugin in plugins)
            Plugins.invokeMethod(plugin["classpath"], "requestEnd", new object[] { plugin["pluginid"], conn, elements, Request, Response, baseTemplateParent });
        // Format the site template
        StringBuilder content = new StringBuilder(Core.templates.get(baseTemplateParent, "base", null) ?? string.Empty);
        // Stop the timer and set element
        timer.Stop();
        elements["BENCH_MARK"] = "Generated in " + timer.ElapsedMilliseconds + " m/s (" + ((float)timer.ElapsedMilliseconds / 1000) + " secs)";
        // Replace elements within template
        elements.replaceElements(ref content, 0, 3);
        // Output the built page to the user
        Response.Write(content.ToString());
        // Dispose connector
        conn.Disconnect();
    }
    #endregion
}