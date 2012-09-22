 ﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS / Plugins/ CC128 Energy Monitor
 * File:                    /App_Code/Plugins/CC128 Energy Monitor/Base.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * A plugin for logging and interacting with data from a CC128 Energy Monitor.
 */
using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using System.Threading;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace UberCMS.Plugins
{
    public static class CC128
    {
        #region "Constants"
        public const string SERVICE_NAME = "ubercms-CC128-bs";
        #endregion

        #region "Methods - Plugin Event Handlers"
        public static string enable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Install SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Install.sql", conn)) != null)
                return error;
            // Install config
            if (!File.Exists(basePath + "\\Service\\Config.xml"))
            {
                StringBuilder settings = new StringBuilder();
                XmlWriter writer = XmlWriter.Create(settings);
                writer.WriteStartDocument();
                writer.WriteStartElement("datasources");

                writer.WriteStartElement("source");

                writer.WriteStartElement("host");
                writer.WriteCData(Core.connHost);
                writer.WriteEndElement();

                writer.WriteStartElement("port");
                writer.WriteCData(Core.connPort.ToString());
                writer.WriteEndElement();

                writer.WriteStartElement("user");
                writer.WriteCData(Core.connUsername);
                writer.WriteEndElement();

                writer.WriteStartElement("pass");
                writer.WriteCData(Core.connPassword);
                writer.WriteEndElement();

                writer.WriteStartElement("database");
                writer.WriteCData(Core.connDatabase);
                writer.WriteEndElement();

                writer.WriteStartElement("query");
                writer.WriteCData("INSERT INTO cc128_readings (temperature, watts, datetime) VALUES('%TEMPERATURE%', '%WATTS%', NOW());");
                writer.WriteEndElement();

                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();

                writer.Flush();
                writer.Close();
                File.WriteAllText(basePath + "\\Service\\Config.xml", settings.ToString());
            }
            // Start service
            backgroundStart();
            // Install templates
            if((error = Misc.Plugins.templatesInstall(basePath + "\\Templates\\CC128", conn)) != null)
                return error;
            // Install content
            if ((error = Misc.Plugins.contentInstall(basePath + "\\Content")) != null)
                return error;
            // Reserve URLs
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "power" }, conn)) != null)
                return error;

            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Unreserve URLs
            if ((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;
            // Remove templates
            if ((error = Misc.Plugins.templatesUninstall("CC128", conn)) != null)
                return error;
            // Remove content
            if ((error = Misc.Plugins.contentUninstall(basePath + "\\Content")) != null)
                return error;
            // Stop service
            backgroundStop();

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
        #endregion

        #region "Methods - Requests"
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Add headers - CSS and JS
            if (pageElements["HEADER"] == null) pageElements["HEADER"] = string.Empty;
            pageElements["HEADER"] += "<link href=\"" + pageElements["URL"] + "/Content/CSS/CC128.css\" type=\"text/css\" rel=\"Stylesheet\" />";
            pageElements["HEADER"] += "<script src=\"" + pageElements["URL"] + "/Content/JS/CC128.js\"></script>";
            // Determine which page the user wants
            string subPage = request.QueryString["1"];
            if (subPage != null && subPage == "history")
                pageHistory(pluginid, conn, ref pageElements, request, response);
            else if (subPage == "ajax")
            {
                // Write the last watt reading
                ResultRow lastReading = conn.Query_Read("SELECT (SELECT watts FROM cc128_readings WHERE datetime >= DATE_SUB(NOW(), INTERVAL 24 HOUR) ORDER BY datetime DESC LIMIT 1) AS last_reading, (SELECT MAX(watts) FROM cc128_readings WHERE datetime >= DATE_SUB(NOW(), INTERVAL 24 HOUR)) AS max_watts")[0];
                response.ContentType = "text/xml";
                response.Write("<d><w>" + lastReading["last_reading"] + "</w><m>" + lastReading["max_watts"] + "</m></d>");
                conn.Disconnect();
                response.End();
            }
            else
                pagePower(pluginid, conn, ref pageElements, request, response);
            // Set the base content
            pageElements["TITLE"] = "CC128 Energy Monitor - <!--CC128_TITLE-->";
            pageElements["CONTENT"] = Core.templates["cc128"]["base"];
        }
        public static void pagePower(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Set JS onload event
            if (pageElements["BODY_ONLOAD"] == null) pageElements["BODY_ONLOAD"] = string.Empty;
            pageElements["BODY_ONLOAD"] += "cc128onLoad();";
            // Set meter content
            pageElements["CC128_CONTENT"] = Core.templates["cc128"]["power"];
            pageElements["CC128_TITLE"] = "Current Power Usage";
            pageElements.setFlag("CC128_CURR");
        }
        public static void pageHistory(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            switch (request.QueryString["2"])
            {
                default:
                    // Today
                    if (request.QueryString["image"] != null)
                    {
                        // Output a graph for todays, unless otherwise specified, data
                        int graphWidth = 800;
                        int graphHeight = 500;
                        if (request.QueryString["width"] != null && int.TryParse(request.QueryString["width"], out graphWidth) && graphWidth < 10)
                            graphWidth = 800;
                        if (request.QueryString["height"] != null && int.TryParse(request.QueryString["height"], out graphHeight) && graphHeight < 10)
                            graphHeight = 500;
                        int graphPaddingLeft = 45;
                        int graphPaddingBottom = 65;
                        int graphPaddingTop = 10;
                        int graphPaddingRight = 20;
                        Bitmap graph = new Bitmap(graphWidth, graphHeight);
                        Graphics g = Graphics.FromImage(graph);
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                        Pen penBlack = new Pen(new SolidBrush(Color.Black)); // Used for drawing gridlines
                        Pen penDataWatts = new Pen(new SolidBrush(Color.Red)); // Used for drawing the watts
                        Font fontGridlines = new Font("Times New Roman", 8.0f, FontStyle.Regular); // Used for drawing text for gridlines
                        Font fontLabels = new Font("Times New Roman", 12.0f, FontStyle.Regular);
                        SolidBrush brushGridlinesText = new SolidBrush(Color.Black);

                        // Draw border
                        g.DrawRectangle(new Pen(new SolidBrush(Color.Gray)), 0, 0, graphWidth -1, graphHeight - 1);

                        // Draw Y line
                        g.DrawLine(penBlack, graphPaddingLeft, graphPaddingTop, graphPaddingLeft, graphHeight - graphPaddingBottom);
                        // Draw X line
                        g.DrawLine(penBlack, graphPaddingLeft, graphHeight - graphPaddingBottom, graphWidth - graphPaddingRight, graphHeight - graphPaddingBottom);

                        // Get the max value
                        int year = -1, month = -1, day = -1;
                        string rawYear = request.QueryString["year"];
                        string rawMonth = request.QueryString["month"];
                        string rawDay = request.QueryString["day"];
                        if (rawYear != null && int.TryParse(rawYear, out year) && year != -1 && year < 2000)
                            year = -1;
                        if (rawMonth != null && int.TryParse(rawMonth, out month) && month != -1 && month < 1 && month > 12)
                            month = -1;
                        if (rawDay != null && int.TryParse(rawDay, out day) && day != -1 && day < 1 && day > 31)
                            day = -1;
                        Result maxVal = conn.Query_Read("SELECT MAX(watts) AS watts FROM cc128_readings WHERE DATE(datetime) = " + (year != -1 && month != -1 && day != -1 ? "'" + year + "-" + month + "-" + day + "'" : "CURDATE()"));
                        if (maxVal.Rows.Count != 1 || maxVal[0]["watts"].Length == 0)
                        {
                            g.FillRectangle(new SolidBrush(Color.Red), 0, 0, graphWidth, graphHeight);
                            g.DrawString("No data available...check the CC128 is operational!\r\n\r\nIs it on COM1?\r\nDid you unplug it?\r\nIs there an issue with the database or server?", new Font("Times New Roman", 20.0f, FontStyle.Regular), new SolidBrush(Color.White), 5, 5);
                        }
                        else
                        {
                            int maxValue = int.Parse(maxVal[0]["watts"]);
                            // Calculate the area for plotting
                            double plotWidth = graphWidth - (graphPaddingLeft + graphPaddingRight);
                            double plotHeight = graphHeight - (graphPaddingTop + graphPaddingBottom);
                            double numberOfYGridLines = 10;
                            // Calculate the gap between watts/time from 0 to maxvalue within plot area
                            double steppingY = plotHeight / numberOfYGridLines;//plotHeight / (double)maxValue * (maxValue * 0.05); // Watts
                            double steppingX = plotWidth / 24; // Time - pretty much 24 hours (CEIL: 23.999999->24)

                            // Shared variables
                            int txtX, txtY;
                            SizeF txtSize;
                            string txt;

                            // Draw watt label
                            txtSize = g.MeasureString("Watts", fontLabels);
                            txtX = -(int)(txtSize.Width / 2);
                            txtY = graphHeight / 2;
                            g.TranslateTransform(txtX + (txtSize.Width / 2), txtY + (txtSize.Height / 2));
                            g.RotateTransform(270);
                            g.DrawString("Watts", fontLabels, brushGridlinesText, 0, 0);
                            g.ResetTransform();

                            // Draw watt grid lines
                            for (double i = 0; i <= plotHeight; i += steppingY)
                            {

                                g.DrawLine(penBlack, graphPaddingLeft - 4, (graphPaddingTop + (int)plotHeight) - (int)i, graphWidth - graphPaddingRight, (graphPaddingTop + (int)plotHeight) - (int)i);


                                txt = Math.Round(i > 0 ? maxValue * (i / plotHeight) : 0, 0).ToString();
                                txtSize = g.MeasureString(txt.ToString(), fontGridlines);
                                txtX = (graphPaddingLeft - 4) - (int)txtSize.Width;
                                txtY = ((graphPaddingTop + (int)plotHeight) - (int)i) - (int)(txtSize.Height / 2);

                                g.DrawString(txt, fontGridlines, brushGridlinesText, txtX, txtY);
                            }

                            // Draw time label
                            txtSize = g.MeasureString("Time", fontLabels);
                            g.DrawString("Time", fontLabels, brushGridlinesText, (graphWidth / 2) - (txtSize.Width / 2), graphHeight - txtSize.Height);

                            // Draw time grid lines
                            for (double i = 0; i <= plotWidth; i += steppingX)
                            {
                                g.DrawLine(penBlack, graphPaddingLeft + (int)i, graphPaddingTop, graphPaddingLeft + (int)i, graphHeight - (graphPaddingBottom - 4));

                                txt = i == 24 ? "23:59" : Math.Round(i > 0 ? (i / plotWidth) * 24 : 0, 0).ToString("0#") + ":00";
                                txtSize = g.MeasureString(txt, fontGridlines);
                                txtX = graphPaddingLeft + (int)i;
                                txtY = graphHeight - (graphPaddingBottom - 4);

                                g.TranslateTransform(txtX + (txtSize.Width / 2), txtY + (txtSize.Height / 2));
                                g.RotateTransform(270);
                                g.DrawString(txt, fontGridlines, brushGridlinesText, -txtSize.Width, -txtSize.Height);
                                g.ResetTransform();
                            }

                            // Plot data
                            int lastX = 0;
                            int lasty = 0;
                            int newX, newY;
                            double seconds;
                            DateTime secondsStart = year != -1 && month != -1 && day != -1 ? DateTime.Parse(year + "-" + month + "-" + day) : DateTime.Today;
                            foreach (ResultRow reading in conn.Query_Read("SELECT watts, datetime FROM cc128_readings WHERE DATE(datetime) = " + (year != -1 && month != -1 && day != -1 ? "'" + year + "-" + month + "-" + day + "'" : "CURDATE()")))
                            {
                                seconds = DateTime.Parse(reading["datetime"]).Subtract(secondsStart).TotalSeconds; // 86400 seconds in a day
                                newX = (int)((seconds / 86400) * plotWidth);
                                newY = (int)(((double)int.Parse(reading["watts"]) / (double)maxValue) * plotHeight);
                                g.DrawLine(penDataWatts, graphPaddingLeft + (lastX != 0 ? lastX : newX - 1), (int)(graphPaddingTop + plotHeight) - (lasty != 0 ? lasty : newY), graphPaddingLeft + newX, (int)(graphPaddingTop + plotHeight) - newY);
                                lastX = newX;
                                lasty = newY;
                            }
                        }
                        g.Dispose();
                        response.ContentType = "image/png";
                        graph.Save(response.OutputStream, System.Drawing.Imaging.ImageFormat.Png);
                        response.End();
                    }
                    else
                    {
                        StringBuilder itemsDay = new StringBuilder();
                        for (int i = 1; i <= 32; i++)
                            itemsDay.Append("<option").Append(i == DateTime.Now.Day ? " selected=\"selected\">" : ">").Append(i).Append("</option>");
                        StringBuilder itemsMonth = new StringBuilder();
                        for (int i = 1; i <= 12; i++)
                            itemsMonth.Append("<option value=\"").Append(i).Append("\"").Append(i == DateTime.Now.Month ? " selected=\"selected\">" : ">").Append(DateTime.Parse("2000-" + i + "-01").ToString("MMMM")).Append("</option>");
                        StringBuilder itemsYear = new StringBuilder();
                        for (int i = DateTime.Now.AddYears(-5).Year; i <= DateTime.Now.Year; i++)
                            itemsYear.Append("<option").Append(i == DateTime.Now.Year ? " selected=\"selected\">" : ">").Append(i).Append("</option>");
                        // Output the content to display an image (above) of todays data
                        pageElements["CC128_CONTENT"] = Core.templates["cc128"]["history_today"]
                            .Replace("%ITEMS_DAY%", itemsDay.ToString())
                            .Replace("%ITEMS_MONTH%", itemsMonth.ToString())
                            .Replace("%ITEMS_YEAR%", itemsYear.ToString())
                            ;
                        pageElements["CC128_TITLE"] = "History - Today";
                        pageElements.setFlag("CC128_H_TODAY");
                    }
                    break;
                case "month":
                    // Month
                    string monthCurr = DateTime.Now.Year + "-" + DateTime.Now.Month + "-01";
                    // Get the max value for the month
                    Result monthMaxVal = conn.Query_Read("SELECT AVG(watts) AS watts FROM cc128_readings WHERE datetime >= '" + Utils.Escape(monthCurr) + "' ORDER BY watts DESC LIMIT 1");
                    if (monthMaxVal.Rows.Count != 1 || monthMaxVal[0]["watts"].Length == 0)
                        pageElements["CC128_CONTENT"] = "<p>No data available.</p>";
                    else
                    {
                        double maxValue = double.Parse(monthMaxVal[0]["watts"]);
                        // Process every day
                        StringBuilder monthBars = new StringBuilder();
                        double percent;
                        foreach (ResultRow day in conn.Query_Read("SELECT AVG(watts) AS watts, DAY(datetime) AS day FROM cc128_readings WHERE datetime >= '" + Utils.Escape(monthCurr) + "' GROUP BY DATE(datetime)"))
                        {
                            percent = Math.Floor(100 * (double.Parse(day["watts"]) / maxValue));
                            monthBars.Append(
                                Core.templates["cc128"]["history_bar"]
                                .Replace("%TITLE%", int.Parse(day["day"]).ToString("0#") + " - " + day["watts"] + " watts average")
                                .Replace("%PERCENT%", (percent > 100 ? 100 : percent).ToString())
                                );
                        }
                        pageElements["CC128_CONTENT"] = Core.templates["cc128"]["history_month"]
                        .Replace("%ITEMS%", monthBars.ToString())
                        ;
                    }
                    pageElements["CC128_TITLE"] = "History - This Month";
                    pageElements.setFlag("CC128_H_MONTH");
                    break;
                case "year":
                    // Year
                    // Get the max value for the month
                    Result yearMaxVal = conn.Query_Read("SELECT AVG(watts) AS watts FROM cc128_readings WHERE YEAR(datetime) = YEAR(NOW()) GROUP BY MONTH(datetime) ORDER BY watts DESC LIMIT 1");
                    if (yearMaxVal.Rows.Count != 1)
                        pageElements["CC128_CONTENT"] = "<p>No data available.</p>";
                    else
                    {
                        double maxValue = double.Parse(yearMaxVal[0]["watts"]);
                        // Process every day
                        StringBuilder yearBars = new StringBuilder();
                        double percent;
                        foreach (ResultRow day in conn.Query_Read("SELECT AVG(watts) AS watts, MONTH(DATETIME) AS month  FROM cc128_readings WHERE YEAR(datetime) = YEAR(NOW()) GROUP BY MONTH(datetime)"))
                        {
                            percent = Math.Floor(100 * (double.Parse(day["watts"]) / maxValue));
                            yearBars.Append(
                                Core.templates["cc128"]["history_bar"]
                                .Replace("%TITLE%", DateTime.Parse(DateTime.Now.Year + "-" + day["month"] + "-01").ToString("MMMM") + " - " + day["watts"] + " watts average")
                                .Replace("%PERCENT%", (percent > 100 ? 100 : percent).ToString())
                                );
                        }
                        pageElements["CC128_CONTENT"] = Core.templates["cc128"]["history_month"]
                        .Replace("%ITEMS%", yearBars.ToString())
                        ;
                    }
                    pageElements["CC128_TITLE"] = "History - This Year";
                    pageElements.setFlag("CC128_H_YEAR");
                    break;
                case "all":
                    // All
                    Result general = conn.Query_Read("SELECT COUNT('') AS total, AVG(watts) AS average FROM cc128_readings");
                    Result allMax = conn.Query_Read("SELECT MAX(watts) AS watts, datetime FROM cc128_readings GROUP BY datetime ORDER BY watts DESC LIMIT 1");
                    Result allMin = conn.Query_Read("SELECT MIN(NULLIF(watts, 0)) AS watts, datetime FROM cc128_readings GROUP BY datetime ORDER BY watts ASC LIMIT 1"); // Thank-you to http://stackoverflow.com/questions/2099720/mysql-find-min-but-not-zero for pro-tip <3
                    pageElements["CC128_CONTENT"] = Core.templates["cc128"]["history_all"]
                        .Replace("%TOTAL%", HttpUtility.HtmlEncode(general.Rows.Count == 1 ? general[0]["total"] : "Unavailable"))
                        .Replace("%MAX_WATTS%", HttpUtility.HtmlEncode(allMax.Rows.Count == 1 ? allMax[0]["watts"] : "Unavailable"))
                        .Replace("%MAX_DATE%", HttpUtility.HtmlEncode(allMax.Rows.Count == 1 ? allMax[0]["datetime"] : "Unavailable"))
                        .Replace("%MIN_WATTS%", HttpUtility.HtmlEncode(allMin.Rows.Count == 1 ? allMin[0]["watts"] : "Unavailable"))
                        .Replace("%MIN_DATE%", HttpUtility.HtmlEncode(allMin.Rows.Count == 1 ? allMin[0]["datetime"] : "Unavailable"))
                        .Replace("%AVERAGE%", HttpUtility.HtmlEncode(general.Rows.Count == 1 ? general[0]["average"] : "Unavailable"))
                        ;
                    pageElements["CC128_TITLE"] = "History - All";
                    break;
            }
        }
        #endregion

        #region "Methods - CMS - Data Mining"
        public static string cmsStart(string pluginid, Connector conn)
        {
            // This will ensure the background service is running; if it's already running, this won't affect it
            backgroundStart();
            return null;
        }
        #endregion

        #region "Methods - Background Service"
        public static void backgroundStart()
        {
            if (Process.GetProcessesByName("BackgroundService").Length != 0) return; // Service is already running
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = "BackgroundService.exe";
                p.StartInfo.WorkingDirectory = Core.basePath + "\\App_Code\\Plugins\\CC128 Energy Monitor\\Service";
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.Start();
            }
            catch {}
        }
        public static void backgroundStop()
        {
            foreach (Process p in Process.GetProcessesByName("BackgroundService"))
                p.Kill();
        }
        #endregion
    }
}