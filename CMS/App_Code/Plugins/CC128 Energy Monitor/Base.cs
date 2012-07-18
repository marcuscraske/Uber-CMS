using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using UberLib.CC128;
using System.Threading;

namespace UberCMS.Plugins
{
    public static class CC128
    {
        public static string enable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Install SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Install.sql", conn)) != null)
                return error;
            // Reserve URLs
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "power" }, conn)) != null)
                return error;
            // Install templates
            if((error = Misc.Plugins.templatesInstall(basePath + "\\CC128", conn)) != null)
                return error;

            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string error = null;
            // Unreserve URLs
            if ((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;
            // Remove templates
            if ((error = Misc.Plugins.templatesUninstall("cc128", conn)) != null)
                return error;

            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Remove SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Uninstall.sql", conn)) != null)
                return error;

            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Add headers - CSS and JS
            if (pageElements["HEADER"] == null) pageElements["HEADER"] = string.Empty;
            pageElements["HEADER"] += "<link href=\"" + pageElements["URL"] + "/Content/CSS/CC128.css\" type=\"text/css\" rel=\"Stylesheet\" />";
            pageElements["HEADER"] += "<script src=\"" + pageElements["URL"] + "/Content/JS/CC128.js\"></script>";
            // Set JS onload event
            if (pageElements["BODY_ONLOAD"] == null) pageElements["BODY_ONLOAD"] = string.Empty;
            pageElements["BODY_ONLOAD"] += "cc128onLoad();";
            // Determine which page the user wants
            string subPage = request.QueryString["1"];
            if (subPage != null && subPage == "history")
                pageHistory(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
            else if (subPage == "ajax")
            {
                // Write the last watt reading
                response.ContentType = "text/xml";
                response.Write("<d><w>" + lastReadingWatts + "</w><m>" + maxWatts + "</m></d>");
                response.End();
            }
            else
                pagePower(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
            // Set the base content
            pageElements["TITLE"] = "CC128 Energy Monitor - <!--CC128_TITLE-->";
            pageElements["CONTENT"] = Core.templates["cc128"]["base"];
        }
        public static void pagePower(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["CC128_CONTENT"] = Core.templates["cc128"]["power"];
            pageElements["CC128_TITLE"] = "Current Power Usage";
            pageElements.setFlag("CC128_CURR");
        }
        public static void pageHistory(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
        }
        public static void requestEnd(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["CC128_WATTS"] = lastReadingWatts.ToString();
            pageElements["CC128_TEMP"] = lastReadingTemperature.ToString();
        }
        public static int maxWatts = 0;
        public static int lastReadingWatts = 0;
        public static float lastReadingTemperature = 0;
        public static Thread monitorThread = null;
        public static EnergyMonitor monitor = null;
        public static string cmsStart(string pluginid, Connector conn)
        {
            // Start monitor thread
            monitorThread = new Thread(delegate()
                {
                    monitor = new EnergyMonitor();
                    monitor.eventNewSensorData += new EnergyMonitor._eventNewSensorData(monitor_eventNewSensorData);
                    monitor.start();
                    while (true)
                    {
                        // If the monitor has not started for some reason, or disconnected, keep attempting to start it
                        if(monitor.ReadState == EnergyMonitor.State.ErrorOccurredOnStart || monitor.ReadState == EnergyMonitor.State.DeviceDisconnected)
                            try
                            {
                                monitor.start();
                            }
                            catch { }
                        Thread.Sleep(1000);
                    }
                });
            monitorThread.Start();
            // Get the max watts in the last 30 days
            try
            {
                maxWatts = conn.Query_Count("SELECT MAX(watts) FROM cc128_readings WHERE datetime >= DATE_SUB(NOW(), INTERVAL 30 DAY)");
            }
            catch { }
            return null;
        }
        public static string cmsStop(string pluginid, Connector conn)
        {
            monitorThread.Abort();
            monitorThread = null;
            monitor.disposeSerialPort(false);
            monitor = null;
            return null;
        }
        static void monitor_eventNewSensorData(EnergyReading reading)
        {
            // Insert reading into table
            if (reading.Sensors.Length != 0)
            {
                Core.globalConnector.Query_Execute("INSERT INTO cc128_readings (temperature, watts, datetime) VALUES('" + reading.Temperature + "', '" + reading.Sensors[0] + "', NOW())");
                lastReadingTemperature = reading.Temperature;
                lastReadingWatts = reading.Sensors[0];
                if (lastReadingWatts > maxWatts) maxWatts = lastReadingWatts;
            }
        }
    }
}