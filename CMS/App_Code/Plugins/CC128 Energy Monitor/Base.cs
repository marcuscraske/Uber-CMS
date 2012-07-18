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
            if (request.QueryString["1"] != null && request.QueryString["1"] == "history")
                pageHistory(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
            else
                pagePower(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
        }
        public static void pagePower(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
        }
        public static void pageHistory(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
        }
        public static void requestEnd(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["CC128_WATTS"] = lastReadingWatts.ToString();
            pageElements["CC128_TEMP"] = lastReadingTemperature.ToString();
        }
        public static int lastReadingWatts = 0;
        public static float lastReadingTemperature = 0;
        public static Thread monitorThread = null;
        public static EnergyMonitor monitor = null;
        public static string cmsStart(string pluginid, Connector conn)
        {
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
            }
        }
    }
}