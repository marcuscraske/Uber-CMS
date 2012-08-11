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
        #region "Methods - CMS Methods"
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
            Core.settings.updateSetting(conn, pluginid, "common", "captcha_enabled", "1", "Specifies if the captcha is enabled", false);
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
            Core.settings.removeCategory(conn, "common");

            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            switch (request.QueryString["page"])
            {
                case "captcha":
                    pageCaptcha(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
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
        private static void pageCaptcha(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
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
            /// <summary>
            /// Generates a string with random alpha-numeric characters of a specified length.
            /// </summary>
            /// <param name="length"></param>
            /// <returns></returns>
            public static string randomText(int length)
            {
                string chars = "abcdefghijklmnopqrstuvwxyz01234567890";
                StringBuilder text = new StringBuilder();
                Random ran = new Random(DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond);
                for (int i = 0; i < length; i++)
                    text.Append(chars[ran.Next(0, chars.Length - 1)].ToString());
                return text.ToString();
            }
        }
        #endregion

        #region "Class - BBCode"
        public static class BBCode
        {
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
            public static void format(ref StringBuilder originalText, bool textFormatting, bool objectFormatting)
            {
                if (textFormatting)
                {
                    // No BB code
                    //Providers.formatNoBBCode(ref originalText);
                    // Font face
                    Providers.formatFontFace(ref originalText);
                    // Font size

                    // Highlighting

                    // Text colour

                    // Bold

                    // Italics

                    // Underlined

                    // Block quote

                    // Bullet points

                    // Shadows

                    // Text align left

                    // Text align right

                    // Text align center

                    // URL

                    // E-mail

                    // Strike-through

                }
                if (objectFormatting)
                {
                    // Image

                    // YouTube

                    // Vimeo

                    // Table

                    // HTML5 Audio

                    // HTML5 Video

                    // Pastebin
                }
            }
            public static class Providers
            {
                public static void formatNoBBCode(ref StringBuilder text)
                {
                    StringBuilder formatter;
                    foreach (Match m in Regex.Matches(text.ToString(), @"[noformat](.*?)[/noformat]", RegexOptions.Multiline))
                    {
                        formatter = new StringBuilder(m.Groups[1].Value);
                        // Turn [ and ] to HTML entities
                        formatter.Replace("[", "&#91;").Replace("]", "&#93;");
                        text.Replace(m.Value, formatter.ToString());
                    }
                }
                public static void formatFontFace(ref StringBuilder text)
                {
                    foreach (Match m in Regex.Matches(text.ToString(), @"\[font=([a-zA-Z\s]+)\](.*?)\[\/font\]", RegexOptions.Multiline))
                        text.Replace(m.Value, "<span style=\"font-family: " + m.Groups[1].Value + "\">" + m.Groups[2].Value + "</span>");
                }
            }
        }
        #endregion

        #region "Class - Smileys"
        
        #endregion
    }
}
