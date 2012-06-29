using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using System.Web.Security;
using System.Security.Cryptography;
using System.IO;
using System.Xml;

namespace UberCMS.Plugins
{
    public static class BasicSiteAuth
    {
        public enum LogEvents
        {
            Login_Incorrect = 1,
            Login_Authenticated = 2,

            Registered = 15,
        }

        public const int MAX_LOGIN_ATTEMPTS = 5;
        public const int MAX_LOGIN_PERIOD = 20;
        private const int USERNAME_MIN = 3;
        private const int USERNAME_MAX = 18;
        private const int PASSWORD_MIN = 3;
        private const int PASSWORD_MAX = 40;

        private static string salt1, salt2;

        public static string enable(string pluginid, Connector conn)
        {
            string error = null;
            // Install SQL
            error = Misc.Plugins.executeSQL(Core.basePath + "\\App_Code\\Plugins\\BasicSiteAuth\\Enable.sql", conn);
            if (error != null) return error;
            // -- Check if any groups exist, else install base groups
            if (conn.Query_Count("SELECT COUNT('') FROM bsa_user_groups") == 0)
            {
                error = Misc.Plugins.executeSQL(Core.basePath + "\\App_Code\\Plugins\\BasicSiteAuth\\Enable_DefaultGroups.sql", conn);
                if (error != null) return error;
            }
            // Install content
            error = Misc.Plugins.contentInstall(Misc.Plugins.getPluginBasePath(pluginid, conn) + "\\Content");
            // Reserve URLs
            Misc.Plugins.reserveURLs(pluginid, null, new string[] { "login", "logout", "register", "recover", "my_account", "captcha" }, conn);
            return error;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string error = null;
            // Remove content
            error = Misc.Plugins.contentUninstall(Misc.Plugins.getPluginBasePath(pluginid, conn) + "\\Content");
            // Remove reserved URLs
            Misc.Plugins.unreserveURLs(pluginid, conn);
            return error;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            string error = null;
            // Remove tables
            error = Misc.Plugins.executeSQL(Core.basePath + "\\App_Code\\Plugins\\BasicSiteAuth\\Uninstall.sql", conn);
            return error;
        }
        public static string cmsStart(string pluginid, Connector conn)
        {
            string salts = Misc.Plugins.getPluginBasePath(pluginid, conn) + "\\Salts.xml";
            // Check if existing salts exist, if so...we'll load them
            if (File.Exists(salts))
            {
                // Salts exist - load them
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(File.ReadAllText(salts));
                salt1 = doc["salts"]["salt1"].InnerText;
                salt2 = doc["salts"]["salt2"].InnerText;
            }
            else
            {
                // Salts do not exist - create them
                salt1 = randomText(16);
                salt2 = randomText(16);
                StringBuilder saltsConfig = new StringBuilder();
                XmlWriter writer = XmlWriter.Create(saltsConfig);
                writer.WriteStartDocument();
                writer.WriteStartElement("salts");

                writer.WriteStartElement("salt1");
                writer.WriteCData(salt1);
                writer.WriteEndElement();

                writer.WriteStartElement("salt2");
                writer.WriteCData(salt2);
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
                writer.Flush();
                writer.Close();

                File.WriteAllText(salts, saltsConfig.ToString());
            }
            return null;
        }
        /// <summary>
        /// Invoked when a request is mapped to this plugin.
        /// </summary>
        /// <param name="pageElements"></param>
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            switch (request.QueryString["page"])
            {
                case "login":
                    pageLogin(pluginid, conn, ref pageElements, request, response);
                    break;
                case "logout":
                    pageLogout(pluginid, conn, ref pageElements, request, response);
                    break;
                case "register":
                    if (HttpContext.Current.User.Identity.IsAuthenticated) return;
                    break;
                case "recover":
                    if (HttpContext.Current.User.Identity.IsAuthenticated) return;
                    break;
                case "my_account":
                    if (!HttpContext.Current.User.Identity.IsAuthenticated) return;
                    break;
                case "captcha":
                    break;
            }
        }
        private static void pageLogin(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            const string incorrectUserPassword = "Incorrect username or password!";
            if (HttpContext.Current.User.Identity.IsAuthenticated) return;
            string error = null;
            // Check for login
            if (request.Form["username"] != null && request.Form["password"] != null)
            {
                bool persist = request.Form["persist"] != null;
                string username = request.Form["username"];
                string password = request.Form["password"];
                // Validate
                if (!validCaptcha(request.Form["captcha"]))
                    error = "Invalid captcha code!";
                else if (username.Length < USERNAME_MIN || username.Length > USERNAME_MAX)
                    error = incorrectUserPassword;
                else if (password.Length < PASSWORD_MIN || password.Length > PASSWORD_MAX)
                    error = incorrectUserPassword;
                else
                {
                    // Check the IP has not tried to authenticate in the past
                    if (conn.Query_Count("SELECT COUNT('') FROM bsa_failed_logins WHERE ip='" + Utils.Escape(request.UserHostAddress) + "' AND datetime >= '" + Utils.Escape(DateTime.Now.AddMinutes(-MAX_LOGIN_PERIOD).ToString("yyyy-MM-dd HH:mm:ss")) + "'") > MAX_LOGIN_ATTEMPTS)
                        error = "You've exceeded the maximum login-attempts, try again in " + MAX_LOGIN_PERIOD + " minutes...";
                    else
                    {
                        // Authenticate
                        Result res = conn.Query_Read("SELECT password FROM bsa_users WHERE username='" + Utils.Escape(username) + "'");
                        if (res.Rows.Count != 1 || res[0]["password"] != generateHash(password, "", ""))
                        {
                            // Incorrect login - log as an attempt
                            // -- Check if the user exists, if so we'll log it into the user_log table
                            string userid = (string)conn.Query_Scalar("SELECT userid FROM bsa_users WHERE username LIKE '" + username.Replace("%", "") + "'");
                            conn.Query_Execute((userid != null ? "INSERT INTO bsa_user_log (userid, event_type, date, additional_info) VALUES('" + Utils.Escape(userid) + "', '" + (int)LogEvents.Login_Incorrect + "', NOW(), '" + Utils.Escape(request.UserHostAddress) + "'); " : string.Empty) + "INSERT INTO bsa_failed_logins (ip, attempted_username, datetime) VALUES('" + Utils.Escape(request.UserHostAddress) + "', '" + Utils.Escape(username) + "', NOW());");
                        }
                        else
                        {
                            // Authenticate the user
                            FormsAuthentication.SetAuthCookie(username, persist);
                            // Check if a ref-url exists, if so redirect to it
                            if (request.UrlReferrer != null)
                                response.Redirect(request.UrlReferrer.AbsoluteUri);
                            else
                                response.Redirect("/");
                        }
                    }
                }
            }
            // Display page
            pageElements["TITLE"] = "Login";
            pageElements["CONTENT"] = Core.templates["basic_site_auth"]["login"]
                .Replace("%USERNAME%", request.Form["username"] ?? string.Empty)
                .Replace("%PERSIST%", request.Form["persist"] != null ? "checked" : string.Empty)
                .Replace("%ERROR%", error != null ? Core.templates["basic_site_auth"]["error"] : string.Empty);
        }

        private static void pageLogout(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (!HttpContext.Current.User.Identity.IsAuthenticated) return;
            // Dispose the current session
            FormsAuthentication.SignOut();
            // Inform the user
            pageElements["TITLE"] = "Logged-out";
            pageElements["CONTENT"] = Core.templates["basic_site_auth"]["logout"];
        }
        private static void pageRegister(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
        }
        private static void pageRecover(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
        }
        private static void pageMyAccount(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
        }
        private static void pageCaptcha(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
        }

        public static void requestEnd(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["USERNAME"] = HttpContext.Current.User.Identity.Name;
        }
        public static bool validCaptcha(string captcha)
        {
            if (captcha != null && captcha == (string)HttpContext.Current.Session["captcha"])
                return true;
            else
                return false;
        }
        /// <summary>
        /// Generates a hash for a string, i.e. a password, for securely storing data within a database.
        /// </summary>
        /// <param name="data"></param>
        private static string generateHash(string data, string salt1, string salt2)
        {
            byte[] rawData = Encoding.UTF8.GetBytes(data);
            byte[] rawSalt1 = Encoding.UTF8.GetBytes(salt1);
            byte[] rawSalt2 = Encoding.UTF8.GetBytes(salt2);
            // Apply salt
            int s1, s2;
            int buffer;
            for (int i = 0; i < rawData.Length; i++)
            {
                buffer = 0;
                // Change the value of the current byte
                for (s1 = 0; s1 < rawSalt1.Length; s1++)
                    for (s2 = 0; s2 < rawSalt2.Length; s2++)
                        buffer = (rawSalt1[s1] + salt2.Length) * rawSalt2[s2] + rawData.Length;
                // Round it down within numeric range of byte
                while (buffer > byte.MaxValue)
                    buffer -= byte.MaxValue;
                // Check the value is not below 0
                if (buffer < 0) buffer = 0;
                // Reset the byte value
                rawData[i] = (byte)buffer;
            }
            // Hash the byte-array
            HashAlgorithm hasher = new SHA512Managed();
            Byte[] computedHash = hasher.ComputeHash(rawData);
            // Convert to base64 and return
            return Convert.ToBase64String(computedHash);
        }
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
}