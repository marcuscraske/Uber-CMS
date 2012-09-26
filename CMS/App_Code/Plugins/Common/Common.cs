 ﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS / Plugins / Common
 * File:                    /App_Code/Plugins/Common/Common.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * A plugin with various common functions for other plugins, such as captcha verficiation,
 * validation, BB Code rendering and much more.
 */
#define COMMON

using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using System.Text.RegularExpressions;
using System.Drawing;

namespace UberCMS.Plugins
{
    public static class Common
    {
        public const string SETTINGS_KEY = "common";
        public const string SETTINGS_KEY_CAPTCHA_ENABLED = "captcha_enabled";

        #region "Methods - Plugin Event Handlers"
        public static string enable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Install SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Install.sql", conn)) != null)
                return error;
            // Install content
            if ((error = Misc.Plugins.contentInstall(basePath + "\\Content")) != null)
                return error;
            // Install settings
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_KEY_CAPTCHA_ENABLED, "1", "Specifies if the captcha is enabled.", false);
            // Install pages
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "captcha" }, conn)) != null)
                return error;

            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Uninstall content
            if ((error = Misc.Plugins.contentInstall(basePath + "\\Content")) != null)
                return error;
            // Uninstall pages
            if ((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;

            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Uninstall SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Uninstall.sql", conn)) != null)
                return error;
            // Uninstall settings
            Core.settings.removeCategory(conn, SETTINGS_KEY);

            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            switch (request.QueryString["page"])
            {
                case "captcha":
                    if (!Core.settings[SETTINGS_KEY].getBool(SETTINGS_KEY_CAPTCHA_ENABLED)) return;
                    pageCaptcha(pluginid, conn, ref pageElements, request, response);
                    break;
            }
        }
        #endregion

        #region "Methods - Pages"
        /// <summary>
        /// Used to verify the user is human.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageCaptcha(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Set the content-type to an image
            response.ContentType = "image/png";
            // Generate random string and store as a session variable
            string text = Common.Utils.randomText(6);
            // Draw text on a random banner
            int width = 160;
            int height = 70;
            int strikesA = 20;
            int strikesB = 10;
            Random R = new Random(DateTime.Now.Millisecond);
            Bitmap temp = new Bitmap(width, height);
            Graphics gi = Graphics.FromImage(temp);
            pageCaptchaStrikeThrough(ref R, strikesA, gi, width, height, 1, 2);
            string[] fonts = new string[] { "Arial", "Verdana", "Times New Roman", "Tahoma" };
            Font f = new Font(fonts[R.Next(0, fonts.Length)], (float)R.Next(20, 24), FontStyle.Regular, GraphicsUnit.Pixel);
            int midY = (height / 2) - (int)(gi.MeasureString(text, f).Height / 2);
            int offset = R.Next(0, 20);
            string charr;
            for (int i = 0; i < text.Length; i++)
            {
                charr = text.Substring(i, 1);
                gi.DrawString(charr, f, new SolidBrush(Color.FromArgb(R.Next(0, 180), R.Next(0, 180), R.Next(0, 180))), new Point(R.Next(0, 5) + offset, midY + R.Next(-10, 10)));
                offset += (int)gi.MeasureString(charr, f).Width;
            }
            pageCaptchaStrikeThrough(ref R, strikesB, gi, width, height, 1, 1);
            int w2 = width / 2;
            int h2 = height / 2;
            gi.FillRectangle(new SolidBrush(Color.FromArgb(R.Next(20, 70), R.Next(0, 255), R.Next(0, 255), R.Next(0, 255))), 0, 0, w2, h2);
            gi.FillRectangle(new SolidBrush(Color.FromArgb(R.Next(20, 70), R.Next(0, 255), R.Next(0, 255), R.Next(0, 255))), w2, h2, w2, h2);
            gi.FillRectangle(new SolidBrush(Color.FromArgb(R.Next(20, 70), R.Next(0, 255), R.Next(0, 255), R.Next(0, 255))), w2, 0, w2, h2);
            gi.FillRectangle(new SolidBrush(Color.FromArgb(R.Next(20, 70), R.Next(0, 255), R.Next(0, 255), R.Next(0, 255))), 0, h2, w2, h2);
            int w8 = width / 8;
            int h8 = height / 8;
            int w4 = width / 4;
            int h4 = height / 4;
            for (int i = 0; i < 5; i++)
                gi.FillRectangle(new SolidBrush(Color.FromArgb(R.Next(10, 40), R.Next(0, 255), R.Next(0, 255), R.Next(0, 255))), R.Next(w8, w2), R.Next(h8, h2), R.Next(w4, width), R.Next(h4, height));
            gi.Dispose();
            // Write image to stream
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                temp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.WriteTo(response.OutputStream);
            }
            temp.Dispose();
            // Set to session variable
            HttpContext.Current.Session["captcha"] = text;
            // End the response
            response.End();
        }
        private static void pageCaptchaStrikeThrough(ref Random rand, int strikes, Graphics gi, int width, int height, int minLineWidth, int maxLineWidth)
        {
            for (int i = 0; i < strikes; i++)
                gi.DrawLine(new Pen(Color.FromArgb(rand.Next(0, 255), rand.Next(0, 255), rand.Next(0, 255)), rand.Next(minLineWidth, maxLineWidth)), new Point(rand.Next(0, width), rand.Next(0, height)), new Point(rand.Next(0, width), rand.Next(0, height)));
        }
        #endregion

        #region "Class - Country"
        /// <summary>
        /// Used for handling country-codes and titles, useful for location information for e.g. profiles and shipping data; uses the following specification:
        /// http://en.wikipedia.org/wiki/ISO_3166-1_alpha-2#AD
        /// </summary>
        public class Country
        {
            public string countryCode;
            public string countryTitle;
            /// <summary>
            /// Fetches the title of a country based on its alpha-two-char code; refer to the following specification for more information:
            /// http://en.wikipedia.org/wiki/ISO_3166-1_alpha-2
            /// </summary>
            /// <param name="countryCode"></param>
            /// <param name="conn"></param>
            /// <returns></returns>
            public static string getCountryTitle(string countryCode, Connector conn)
            {
                Result title = conn.Query_Read("SELECT country_title FROM markup_engine_countrycodes WHERE country_code='" + UberLib.Connector.Utils.Escape(countryCode) + "'");
                if (title.Rows.Count != 1)
                    return null;
                else
                    return title[0]["country_title"];
            }
            /// <summary>
            /// Fetches an array of available country titles and their associated codes.
            /// </summary>
            /// <param name="conn"></param>
            /// <returns></returns>
            public static Country[] getCountries(Connector conn)
            {
                List<Country> countries = new List<Country>();
                Country tCountry;
                foreach (ResultRow country in conn.Query_Read("SELECT country_code, country_title FROM markup_engine_countrycodes ORDER BY country_title ASC"))
                {
                    tCountry = new Country();
                    tCountry.countryCode = country["country_code"];
                    tCountry.countryTitle = country["country_title"];
                    countries.Add(tCountry);
                }
                return countries.ToArray();
            }
        }
        #endregion

        #region "Class - Validation"
        public static class Validation
        {
            /// <summary>
            /// Validates a URL.
            /// Credit - Michael Krutwig - http://regexlib.com/REDetails.aspx?regexp_id=153
            /// </summary>
            /// <param name="url"></param>
            /// <returns></returns>
            public static bool validUrl(string url)
            {
                if (url.Length > 2000) return false;
                return Regex.Match(url, @"^(http|https|ftp)\://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,3}(:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*[^\.\,\)\(\s]$").Success;
            }
            /// <summary>
            /// Validates the string is correct hex; # is optional at the start.
            /// </summary>
            /// <param name="hex"></param>
            /// <returns></returns>
            public static bool validHex(string hex)
            {
                return Regex.Match(hex, @"^([a-fA-F0-9]{6}$)").Success;
            }
            /// <summary>
            /// Validates an e-mail address; credit for the regex pattern goes to the following article:
            /// http://www.codeproject.com/Articles/22777/Email-Address-Validation-Using-Regular-Expression
            /// </summary>
            /// <param name="text"></param>
            /// <returns></returns>
            public static bool validEmail(string text)
            {
                const string regexPattern =
                    @"^(([\w-]+\.)+[\w-]+|([a-zA-Z]{1}|[\w-]{2,}))@"
         + @"((([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\.([0-1]?
				[0-9]{1,2}|25[0-5]|2[0-4][0-9])\."
         + @"([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\.([0-1]?
				[0-9]{1,2}|25[0-5]|2[0-4][0-9])){1}|"
         + @"([a-zA-Z]+[\w-]+\.)+[a-zA-Z]{2,8})$";
                return Regex.IsMatch(text, regexPattern);
            }
            /// <summary>
            /// Checks if a captcha code is correct.
            /// </summary>
            /// <param name="captcha"></param>
            /// <returns></returns>
            public static bool validCaptcha(string captcha)
            {
                if (captcha != null && captcha == (string)HttpContext.Current.Session["captcha"])
                {
                    HttpContext.Current.Session.Remove("captcha");
                    return true;
                }
                else
                    return false;
            }
        }
        #endregion

        #region "Class - Utils"
        public static class Utils
        {
            private static Random rand = null;
            /// <summary>
            /// Generates a string with random alpha-numeric characters of a specified length.
            /// </summary>
            /// <param name="length"></param>
            /// <returns></returns>
            public static string randomText(int length)
            {
                if (rand == null)
                    rand = new Random((DateTime.Now.Day * DateTime.Now.Month) + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond);
                string chars = "abcdefghijklmnopqrstuvwxyz01234567890";
                StringBuilder text = new StringBuilder();
                for (int i = 0; i < length; i++)
                    text.Append(chars[rand.Next(0, chars.Length - 1)].ToString());
                return text.ToString();
            }
        }
        #endregion

        #region "Class - BBCode"
        /// <summary>
        /// This is required to insert the required dependencies for formatted pages; this is only needed for e.g. cached or preview documents.
        /// </summary>
        public static void formatInsertDependencies(ref Misc.PageElements pageElements)
        {
            Misc.Plugins.addHeaderCssOnce(pageElements["URL"] + "/Content/CSS/Common.css", ref pageElements);
            // Code syntax highlighter: base
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shCore.js", ref pageElements);
            Misc.Plugins.addHeaderCssOnce(pageElements["URL"] + "/Content/CSS/Common/shCore.css", ref pageElements);
            Misc.Plugins.addHeaderCssOnce(pageElements["URL"] + "/Content/CSS/Common/shThemeDefault.css", ref pageElements);
            // Code syntax highlighter: languages - admittedly this is heavy...
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushAS3.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushBash.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushColdFusion.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushCpp.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushCSharp.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushCss.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushDelphi.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushJava.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushJScript.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushPerl.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushPhp.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushPowerShell.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushPython.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushRuby.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushScala.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushSql.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushVb.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushXml.js", ref pageElements);
            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushPlain.js", ref pageElements);
            pageElements.appendToKey("BODY_FOOTER", "<script type=\"text/javascript\">SyntaxHighlighter.all()</script>");
        }
        /// <summary>
        /// Formats BB Code within text (http://en.wikipedia.org/wiki/BBCode); supported tags:
        /// 
        /// [noformat]text[/noformat] - Doesn't format the specified text.
        /// [font=Arial]text[/font] - Formats text to a font.
        /// [
        /// </summary>
        /// <param name="originalText"></param>
        /// <param name="textFormatting"></param>
        /// <param name="objectFormatting"></param>
        public static void format(Connector conn, ref StringBuilder originalText, ref Misc.PageElements pageElements, bool textFormatting, bool objectFormatting)
        {
            // Attach styling file if at least one bool is true
            if (textFormatting || objectFormatting)
                Misc.Plugins.addHeaderCssOnce(pageElements["URL"] + "/Content/CSS/Common.css", ref pageElements);
            // Begin formatting
            if (textFormatting)
            {
                // New lines
                BBCode.formatNewline(ref originalText);
                // No BB code
                BBCode.formatNoBBCode(ref originalText);
                // Font face
                BBCode.formatFontFace(ref originalText);
                // Font size
                BBCode.formatFontSize(ref originalText);
                // Highlighting
                BBCode.formatHighlighting(ref originalText);
                // Text colour
                BBCode.formatTextColour(ref originalText);
                // Bold
                BBCode.formatTextBold(ref originalText);
                // Italics
                BBCode.formatTextItalics(ref originalText);
                // Underlined
                BBCode.formatTextUnderlined(ref originalText);
                // Strike-through
                BBCode.formatTextStrikeThrough(ref originalText);
                // Shadows
                BBCode.formatTextShadow(ref originalText);
                // Text align left
                BBCode.formatTextAlignLeft(ref originalText);
                // Text align center
                BBCode.formatTextAlignCenter(ref originalText);
                // Text align right
                BBCode.formatTextAlignRight(ref originalText);
                // URL
                BBCode.formatUrl(ref originalText);
                // E-mail
                BBCode.formatEmail(ref originalText);
                // Bullet points
                BBCode.formatBulletPoints(ref originalText);
            }
            if (objectFormatting)
            {
                // Float left
                BBCode.formatFloatLeft(ref originalText);
                // Float right
                BBCode.formatFloatRight(ref originalText);
                // Clear
                BBCode.formatClear(ref originalText);
                // Padding
                BBCode.formatPadding(ref originalText);
                // Block quote
                BBCode.formatBlockQuote(ref originalText);
                // Image
                BBCode.formatImage(ref originalText);
                // YouTube
                BBCode.formatYouTube(ref originalText);
                // Table
                BBCode.formatTable(ref originalText);
                // HTML5 Audio
                BBCode.formatHtml5Audio(ref originalText);
                // HTML5 Video
                BBCode.formatHtml5Video(ref originalText);
                // Pastebin
                BBCode.formatPastebin(ref originalText);
                // Code
                BBCode.formatCode(ref originalText, ref pageElements);
            }
        }
        public static class BBCode
        {
            /// <summary>
            /// Replaces new-lines with <![CDATA[<br />]]>.
            /// </summary>
            /// <param name="text"></param>
            public static void formatNewline(ref StringBuilder text)
            {
                // Removes carriage return (not needed) and replaces >\n ]\n for HTML and BBCode blocks
                text.Replace("\r", string.Empty).Replace(">\n", ">").Replace("]\n", "]").Replace("\n", "<br />");
            }
            /// <summary>
            /// Replaces a section of symbols within text to disallow bbcode formatting.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [noformat]<text>[/noformat]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatNoBBCode(ref StringBuilder text)
            {
                StringBuilder formatter;
                foreach (Match m in Regex.Matches(text.ToString(), @"\[noformat\](.*?)\[\/noformat\]", RegexOptions.Multiline))
                {
                    formatter = new StringBuilder(m.Groups[1].Value);
                    // Turn [ and ] to HTML entities
                    formatter.Replace("[", "&#91;").Replace("]", "&#93;").Replace("{", "&#123;").Replace("}", "&#125;");
                    text.Replace(m.Value, formatter.ToString());
                }
            }
            /// <summary>
            /// Replaces the font of a section of text.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [font=<font face>]<text>[/font]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatFontFace(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[font=([a-zA-Z\s]+)\](.*?)\[\/font\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<span style=\"font-family: " + m.Groups[1].Value + "\">" + m.Groups[2].Value + "</span>");
            }
            /// <summary>
            /// Changes the size of text.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [size=<value>]<text>[/size]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatFontSize(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[size=([1-9]{1}|1[0-9]{1}|2[0-9]{1}|30{1}|[1-9]{1}.[1-9]{1}|1[0-9]{1}.[1-9]{1}|2[0-9]{1}.[1-9]{1})(em|pt)\](.*?)\[\/size\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<span style=\"font-size: " + m.Groups[1].Value + m.Groups[2].Value + "\">" + m.Groups[3].Value + "</span>");
            }
            /// <summary>
            /// Highlights text.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [highlight=#<3 or 6 char hex value>]<text>[/highlight]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatHighlighting(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[highlight=#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})\](.*?)\[\/highlight\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<span style=\"background: #" + m.Groups[1].Value + "\">" + m.Groups[2].Value + "</span>");
            }
            /// <summary>
            /// Changes the colour of text.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [colour=#<3 or 6 char hex value>]<text>[/colour]
            /// [color=#<3 or 6 char hex value>]<text>[/color]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatTextColour(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[(?:colour|color)=#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})\](.*?)\[\/(?:colour|color)\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<span style=\"color: #" + m.Groups[1].Value + "\">" + m.Groups[2].Value + "</span>");
            }
            /// <summary>
            /// Renders bold text.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [b]<text>[/b]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatTextBold(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[b\](.*?)\[\/b\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<span class=\"COMMON_B\">" + m.Groups[1].Value + "</span>");
            }
            /// <summary>
            /// Renders italic text.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [i]<text>[/i]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatTextItalics(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[i\](.*?)\[\/i\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<span class=\"COMMON_I\">" + m.Groups[1].Value + "</span>");
            }
            /// <summary>
            /// Renders underlined text.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [u]<text>[/u]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatTextUnderlined(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[u\](.*?)\[\/u\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<span class=\"COMMON_U\">" + m.Groups[1].Value + "</span>");
            }
            /// <summary>
            /// Renders a strike-through on text.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [s]<text>[/s]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatTextStrikeThrough(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[s\](.*?)\[\/s\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<span class=\"COMMON_LT\">" + m.Groups[1].Value + "</span>");
            }
            /// <summary>
            /// Gives text a shadow.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [shadow=<offset x>,<offset y>,<blur>,#<colour in 3 or 6 hex>]<text>[/shadow]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatTextShadow(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[shadow=([0-9]{1}|[0-9]{1}.[0-9]{1}|[0-9]{2}|[0-9]{2}.[0-9]{1}),([0-9]{1}|[0-9]{1}.[0-9]{1}|[0-9]{2}|[0-9]{2}.[0-9]{1}),([0-9]{1}|[0-9]{1}.[0-9]{1}|[0-9]{2}|[0-9]{2}.[0-9]{1}),#([a-fA-F0-9]{3}|[a-fA-F0-9]{6})\](.*?)\[\/shadow\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<span style=\"text-shadow: " + m.Groups[1].Value + "em " + m.Groups[2].Value + "em " + m.Groups[3].Value + "em #" + m.Groups[4].Value + ";\">" + m.Groups[5].Value + "</span>");
            }
            /// <summary>
            /// Aligns text to the left within a container.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [left]<text>[/left]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatTextAlignLeft(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[left\](.*?)\[\/left\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<div class=\"COMMON_TAL\">" + m.Groups[1].Value + "</div>");
            }
            /// <summary>
            /// Aligns text to the center within a container.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [center]<text>[/center]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatTextAlignCenter(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[center\](.*?)\[\/center\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<div class=\"COMMON_TAC\">" + m.Groups[1].Value + "</div>");
            }
            /// <summary>
            /// Aligns text to the right within a container.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [right]<text>[/right]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatTextAlignRight(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[right\](.*?)\[\/right\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<div class=\"COMMON_TAR\">" + m.Groups[1].Value + "</div>");
            }
            /// <summary>
            /// Used to create a hyper-link for a URL.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [url=<link>]<text>[/url]
            /// [url]<link>[/url]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatUrl(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[url=([a-zA-Z0-9]+)\:\/\/([a-zA-Z0-9\/\._\-\=\?]+)\](.*?)\[\/url\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<a href=\"" + m.Groups[1].Value + "://" + m.Groups[2].Value + "\">" + m.Groups[3].Value + "</a>");
                foreach (Match m in Regex.Matches(text.ToString(), @"\[url\]([a-zA-Z0-9]+)\:\/\/([a-zA-Z0-9\/\._\-\=\?]+)\[\/url\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<a href=\"" + m.Groups[1].Value + "://" + m.Groups[2].Value + "\">" + m.Groups[1].Value + "://" + m.Groups[2].Value + "</a>");
            }
            /// <summary>
            /// Used to hyper-link an e-mail:
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [email]<email address>[/email]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatEmail(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[email\]([a-zA-Z0-9\.@_\-]+)\[\/email\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<a href=\"mailto://" + m.Groups[1].Value + "\">" + m.Groups[1].Value + "</a>");
            }
            /// <summary>
            /// Used to create a list of bullet points.
            /// 
            /// Markup:
            /// <![CDATA[
            /// [list]
            ///  [*] item 1
            ///  [*] item 2
            /// [/list]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatBulletPoints(ref StringBuilder text)
            {
                StringBuilder list;
                foreach (Match m in Regex.Matches(text.ToString(), @"\[list\](.*?)\[\/list\]", RegexOptions.Multiline))
                {
                    list = new StringBuilder(m.Groups[1].Value);
                    foreach (Match m2 in Regex.Matches(list.ToString(), @"\[\*\](.*?)<br />", RegexOptions.Multiline))
                        list.Replace(m2.Value, "<li>" + m2.Groups[1].Value + "</li>");
                    list.Replace("<br />", ""); // Remove any line breaks
                    text.Replace(m.Value, "<ul class=\"COMMON_BP\">" + list.ToString() + "</ul>");
                }
            }
            /// <summary>
            /// Floats text to the left of a page.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [fl]<text>[/fl]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatFloatLeft(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[fl\](.*?)\[\/fl\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<div class=\"COMMON_FL\">" + m.Groups[1].Value + "</div>");
            }
            /// <summary>
            /// Floats text to the right of a page.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [fr]<text>[/fr]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatFloatRight(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[fr\](.*?)\[\/fr\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<div class=\"COMMON_FR\">" + m.Groups[1].Value + "</div>");
            }
            /// <summary>
            /// Clears space after floating objects (clear: both - CSS).
            /// </summary>
            /// <param name="text"></param>
            public static void formatClear(ref StringBuilder text)
            {
                text.Replace("[clear]", "<div class=\"COMMON_CLEAR\"></div>");
            }
            /// <summary>
            /// Creates padding around text.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [padding=<value>]<text>[/padding]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatPadding(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[padding=([0-9]{1}|[0-9]{2}|[0-9]{1}.[0-9]{1}|[0-9]{2}.[0-9]{1})\](.*?)\[\/padding]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<div style=\"padding: " + m.Groups[1].Value + "em\">" + m.Groups[2].Value + "</div>");
            }
            /// <summary>
            /// Creates a quotation object.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [blockquote=<source of quote>]<quote text>[/blockquote]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatBlockQuote(ref StringBuilder text)
            {
                string stext;
                foreach (Match m in Regex.Matches(text.ToString(), @"\[blockquote=(.*?)\](.*?)\[\/blockquote\]", RegexOptions.Multiline))
                {
                    stext = m.Groups[2].Value;
                    if (stext.StartsWith("<br />") && stext.Length > 6) stext = stext.Substring(6); // Remove starting <br />
                    if (stext.EndsWith("<br />") && stext.Length > 6) stext = stext.Remove(stext.Length - 6, 6); // Remove ending <br />
                    text.Replace(m.Value, "<blockquote class=\"COMMON_BQ\"><div class=\"COMMON_QS\">“</div>" + stext + "<div class=\"COMMON_QE\">”</div><div class=\"COMMON_QC\">- " + m.Groups[1].Value + "</div></blockquote>");
                }
            }
            /// <summary>
            /// Creates an image.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [img]<url>[/img]
            /// [img=<width>px]<url>[/img]
            /// [img=<width>em]<url>[/img]
            /// [img=<width>px,<height>px]<url>[/img]
            /// [img=<width>em,<height>em]<url>[/img]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatImage(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[img\]([a-zA-Z0-9]+)\:\/\/([a-zA-Z0-9\/\._\-\+]+)\[\/img\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<a title=\"Click to open the image...\" href=\"" + m.Groups[1].Value + "://" + m.Groups[2].Value + "\" class=\"COMMON_IMG\"><img src=\"" + m.Groups[1].Value + "://" + m.Groups[2].Value + "\" /></a>");
                foreach (Match m in Regex.Matches(text.ToString(), @"\[img=([0-9]{4}px|[0-9]{3}px|[0-9]{2}px|[0-9]{1}px|[0-9]{4}em|[0-9]{3}em|[0-9]{2}em|[0-9]{1}em)\]([a-zA-Z0-9]+)\:\/\/([a-zA-Z0-9\/\._\-\+]+)\[\/img\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<a title=\"Click to open the image...\" href=\"" + m.Groups[3].Value + "://" + m.Groups[4].Value + "\" class=\"COMMON_IMG\"><img style=\"width: " + m.Groups[1].Value + "; height: " + m.Groups[2].Value + ";\" src=\"" + m.Groups[3].Value + "://" + m.Groups[4].Value + "\" /></a>");
                foreach (Match m in Regex.Matches(text.ToString(), @"\[img=([0-9]{4}px|[0-9]{3}px|[0-9]{2}px|[0-9]{1}px|[0-9]{4}em|[0-9]{3}em|[0-9]{2}em|[0-9]{1}em),([0-9]{4}px|[0-9]{3}px|[0-9]{2}px|[0-9]{1}px|[0-9]{4}em|[0-9]{3}em|[0-9]{2}em|[0-9]{1}em)\]([a-zA-Z0-9]+)\:\/\/([a-zA-Z0-9\/\._\-\+]+)\[\/img\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<a title=\"Click to open the image...\" href=\"" + m.Groups[3].Value + "://" + m.Groups[4].Value + "\" class=\"COMMON_IMG\"><img style=\"width: " + m.Groups[1].Value + "; height: " + m.Groups[2].Value + ";\" src=\"" + m.Groups[3].Value + "://" + m.Groups[4].Value + "\" /></a>");
            }
            /// <summary>
            /// Used to create a table.
            /// 
            /// Markup:
            /// <![CDATA[
            /// [table]
            ///   [tr]
            ///      [tc]row 1 cell 1[/tc]
            ///      [tc]row 1 cell 2[/tc]
            ///      [tc]row 1 cell 3[/tc]
            ///   [/tr]
            ///   [tr]
            ///     [tc]row 2 cell 1[/tc]
            ///     [tc]row 2 cell 2[/tc]
            ///     [tc]row 2 cell 3[/tc]
            ///   [/tr]
            /// [/table]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatTable(ref StringBuilder text)
            {
                StringBuilder tableRows;
                StringBuilder tableColumns;
                string columnFormatted;
                foreach (Match m in Regex.Matches(text.ToString(), @"\[table\](.*?)\[\/table\]", RegexOptions.Multiline))
                {
                    tableRows = new StringBuilder();
                    foreach (Match m2 in Regex.Matches(m.Groups[1].Value, @"\[tr\](.*?)\[\/tr\]", RegexOptions.Multiline))
                    {
                        tableColumns = new StringBuilder();
                        foreach (Match m3 in Regex.Matches(m2.Groups[1].Value, @"\[tc\](.*?)\[\/tc\]", RegexOptions.Multiline))
                        {
                            columnFormatted = m3.Groups[1].Value.Trim();
                            if (columnFormatted.StartsWith("<br />"))
                                columnFormatted = columnFormatted.Substring(6);
                            if (columnFormatted.EndsWith("<br />"))
                                columnFormatted = columnFormatted.Remove(columnFormatted.Length - 6, 6);
                            columnFormatted = columnFormatted.Trim();
                            tableColumns.Append("<td>").Append(columnFormatted).Append("</td>");
                        }
                        tableRows.Append("<tr>").Append(tableColumns.ToString()).Append("</tr>");
                    }
                    text.Replace(m.Value, "<table>" + tableRows.ToString() + "</table>");
                }

            }
            /// <summary>
            /// Inserts a YouTube video.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [youtube]<youtube url>[/youtube]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatYouTube(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[youtube\]http:\/\/(?:it.|www.)?youtube\.(?:com|.co.uk|.com.br|.fr|.jp|.nl|.pl|.es|.ie)\/watch\?(?:[A-Za-z0-9\&\=_\-]+)?(v=([A-Za-z0-9_\-]+))(?:&[A-Za-z0-9\&\=_\-]+)?\[\/youtube\]", RegexOptions.Multiline))
                    //text.Replace(m.Value, "captured - " + m.Groups[1] + "," + m.Groups[2] + "," + m.Value);
                    text.Replace(m.Value, @"<object width=""480"" height=""360""><param name=""movie"" value=""http://www.youtube.com/v/" + m.Groups[2].Value + @"?version=3&amp;hl=en_GB""></param><param name=""allowFullScreen"" value=""true""></param><param name=""allowscriptaccess"" value=""always""></param><embed src=""http://www.youtube.com/v/" + m.Groups[2].Value + @"?version=3&amp;hl=en_GB"" type=""application/x-shockwave-flash"" width=""480"" height=""360"" allowscriptaccess=""always"" allowfullscreen=""true""></embed></object>");
            }
            /// <summary>
            /// Inserts a HTML5 audio player.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [audio]<url>[/audio]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatHtml5Audio(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[audio\]([a-zA-Z0-9]+)\:\/\/([a-zA-Z0-9\/\._\-]+)\[\/audio\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<audio class=\"COMMON_AUDIO\" controls=\"controls\"><source src=\"" + m.Groups[1].Value + "://" + m.Groups[2].Value + "\" >Your browser does not support HTML5 Audio - consider using Google Chrome or Mozilla Firefox!</audio>");
            }
            /// <summary>
            /// Inserts a HTML5 video player.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [video]<url>[/video]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatHtml5Video(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[video\]([a-zA-Z0-9]+)\:\/\/([a-zA-Z0-9\/\._\-]+)\[\/video\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<video class=\"COMMON_VIDEO\" controls=\"controls\"><source src=\"" + m.Groups[1].Value + "://" + m.Groups[2].Value + "\" >Your browser does not support HTML5 Video - consider using Google Chrome or Mozilla Firefox!</video>");
            }
            /// <summary>
            /// Inserts a pastebin snippet.
            /// 
            /// Tag(s):
            /// <![CDATA[
            /// [pastebin]<pastebin URL>[/pastebin]
            /// ]]>
            /// </summary>
            /// <param name="text"></param>
            public static void formatPastebin(ref StringBuilder text)
            {
                foreach (Match m in Regex.Matches(text.ToString(), @"\[pastebin\]http://(?:www.)?pastebin.com/([a-zA-Z0-9]+)\[\/pastebin\]", RegexOptions.Multiline))
                    text.Replace(m.Value, "<script src=\"http://pastebin.com/embed_js.php?i=" + m.Groups[1].Value + "\"></script>");
            }
            public static void formatCode(ref StringBuilder text, ref Misc.PageElements pageElements)
            {
                StringBuilder code;
                foreach (Match m in Regex.Matches(text.ToString(), @"\[code=(as3|bash|coldfusion|cpp|csharp|c#|css|delphi|java|jscript|perl|php|plain|powershell|python|ruby|scala|sql|vb|xml)\](.*?)\[\/code\]", RegexOptions.Multiline))
                {
                    code = new StringBuilder(m.Groups[2].Value);
                    // Replace tag symbols
                    code.Replace("<br />", "\n").Replace("<", "&lt;").Replace(">", "&gt;").Append("</pre>");
                    // Include language core
                    switch (m.Groups[1].Value)
                    {
                        case "as3":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushAS3.js", ref pageElements);
                            break;
                        case "bash":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushBash.js", ref pageElements);
                            break;
                        case "coldfusion":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushColdFusion.js", ref pageElements);
                            break;
                        case "cpp":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushCpp.js", ref pageElements);
                            break;
                        case "csharp":
                        case "c#":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushCSharp.js", ref pageElements);
                            break;
                        case "css":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushCss.js", ref pageElements);
                            break;
                        case "delphi":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushDelphi.js", ref pageElements);
                            break;
                        case "java":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushJava.js", ref pageElements);
                            break;
                        case "jscript":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushJScript.js", ref pageElements);
                            break;
                        case "perl":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushPerl.js", ref pageElements);
                            break;
                        case "php":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushPhp.js", ref pageElements);
                            break;
                        case "powershell":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushPowerShell.js", ref pageElements);
                            break;
                        case "python":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushPython.js", ref pageElements);
                            break;
                        case "ruby":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushRuby.js", ref pageElements);
                            break;
                        case "scala":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushScala.js", ref pageElements);
                            break;
                        case "sql":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushSql.js", ref pageElements);
                            break;
                        case "vb":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushVb.js", ref pageElements);
                            break;
                        case "xml":
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushXml.js", ref pageElements);
                            break;
                        case "plain":
                        default:
                            Misc.Plugins.addHeaderJsOnce(pageElements["URL"] + "/Content/JS/Common/shBrushPlain.js", ref pageElements);
                            break;
                    }
                    // Replace text
                    text.Replace(m.Value, "<pre class=\"brush: " + m.Groups[1].Value + "\">" + code.ToString());//.Append("<script type=\"text/javascript\">SyntaxHighlighter.all()</script>");
                }
            }
        }
        #endregion

        #region "Class - Anti-CSRF Protection"
        public static class AntiCSRF
        {
            private const string ANTI_CSRF_KEY = "COMMON_CSRF_TOKEN";
            /// <summary>
            /// Use this method to fetch a token you can embed in a form; then when the form is submitted,
            /// use the isValidToken method within this class to validate the submitted token is the
            /// same as the one embedded. This is almost the same as a captcha, except less hassle and can
            /// be utilized much more heavily around the CMS without annoying users.
            /// </summary>
            /// <returns></returns>
            public static string getFormToken()
            {
                string token = Utils.randomText(32);
                HttpContext.Current.Session[ANTI_CSRF_KEY] = token;
                return token;
            }
            /// <summary>
            /// Sets a cookie on the client with a token. You can then use the isValidToken method to
            /// validate the users request as genuine; the cookie will expire in ten minutes.
            /// </summary>
            /// <param name="response"></param>
            public static void setCookieToken(HttpResponse response)
            {
                string token = Utils.randomText(32);
                HttpContext.Current.Session[ANTI_CSRF_KEY] = token;
                HttpCookie cookie = new HttpCookie(ANTI_CSRF_KEY, token);
                cookie.Expires = DateTime.Now.AddMinutes(10);
                response.Cookies.Add(cookie);
            }
            /// <summary>
            /// Validates the specified token.
            /// </summary>
            /// <param name="value"></param>
            /// <returns></returns>
            public static bool isValidTokenForm(string token)
            {
                if (HttpContext.Current.Session[ANTI_CSRF_KEY] == null)
                    return false;
                string storedToken = (string)HttpContext.Current.Session[ANTI_CSRF_KEY];
                HttpContext.Current.Session[ANTI_CSRF_KEY] = null;
                return storedToken == token;
            }
            /// <summary>
            /// Validates the request has a valid anti-CSRF cookie.
            /// </summary>
            /// <param name="request"></param>
            /// <returns></returns>
            public static bool isValidTokenCookie(HttpRequest request, HttpResponse response)
            {
                HttpCookie cookie;
                if (HttpContext.Current.Session[ANTI_CSRF_KEY] == null || (cookie = request.Cookies[ANTI_CSRF_KEY]) == null)
                    return false;
                else
                {
                    cookie.Expires = DateTime.Now.AddDays(-1); // Expires the cookie, no longer needed
                    response.Cookies.Add(cookie);
                    return (string)HttpContext.Current.Session[ANTI_CSRF_KEY] == cookie.Value;
                }
            }
        }
        #endregion
    }
}