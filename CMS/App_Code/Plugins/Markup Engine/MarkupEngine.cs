#define MARKUP_ENGINE

using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using System.Text.RegularExpressions;

namespace UberCMS.Plugins
{
    public static class MarkupEngine
    {
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

            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            // Uninstall SQL

            // Uninstall content

            return null;
        }
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
                Result title = conn.Query_Read("SELECT country_title FROM markup_engine_countrycodes WHERE country_code='" + Utils.Escape(countryCode) + "'");
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
        }
        public static class Smileys
        {
        }
        public static class EmbeddedObjects
        {

        }
    }
}
