using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using System.Web.Security;
using System.Security.Cryptography;
using System.IO;
using System.Xml;
using System.Drawing;
using System.Text.RegularExpressions;

namespace UberCMS.Plugins
{
    public static class BasicSiteAuth
    {
        #region "Enums"
        public enum LogEvents
        {
            Login_Incorrect = 1,
            Login_Authenticated = 2,
            Registered = 15,
            AccountRecovered_Email = 20,
            AccountRecovered_SQA = 21
        }
        #endregion

        #region "Constants"
        private const int USERNAME_MIN = 3;
        private const int USERNAME_MAX = 18;
        private const int PASSWORD_MIN = 3;
        private const int PASSWORD_MAX = 40;
        private const int SECRET_QUESTION_MIN = 0;
        private const int SECRET_QUESTION_MAX = 40;
        private const int SECRET_ANSWER_MIN = 0;
        private const int SECRET_ANSWER_MAX = 40;
        #endregion

        #region "Constants - Setting Keys"
        public const string SETTINGS_MAX_LOGIN_ATTEMPTS = "bsa_max_login_attempts";
        public const string SETTINGS_MAX_LOGIN_PERIOD = "bsa_max_login_period";
        public const string SETTINGS_USER_GROUP_DEFAULT = "bsa_user_group_default";
        public const string SETTINGS_USER_GROUP_USER = "bsa_user_group_user";
        public const string SETTINGS_USER_GROUP_BANNED = "bsa_user_group_banned";
        public const string SETTINGS_SITE_NAME = "bsa_site_name";
        public const string SETTINGS_USERNAME_STRICT = "bsa_username_strict";
        public const string SETTINGS_USERNAME_STRICT_CHARS = "bsa_username_strict_chars";
        public const string SETTINGS_RECOVERY_MAX_EMAILS = "bsa_max_recovery_emails";
        public const string SETTINGS_RECOVERY_SQA_ATTEMPTS_MAX = "bsa_sqa_attempts_max";
        public const string SETTINGS_RECOVERY_SQA_ATTEMPTS_INTERVAL = "bsa_sqa_attempts_interval";
        #endregion

        #region "Variables"
        private static string salt1, salt2;
        #endregion

        #region "Methods - Plugin Event Handlers"
        public static string enable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Install SQL
            error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Enable.sql", conn);
            if (error != null) return error;
            // -- Check if any groups exist, else install base groups
            if (conn.Query_Count("SELECT COUNT('') FROM bsa_user_groups") == 0)
            {
                error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Enable_DefaultGroups.sql", conn);
                if (error != null) return error;
                // Check if to create a default user
                if (conn.Query_Count("SELECT COUNT('') FROM bsa_users") == 0)
                {
                    try
                    {
                        conn.Query_Execute("INSERT INTO bsa_users (groupid, username, password, email) VALUES('4', 'root', '"  + Utils.Escape(generateHash("password", salt1, salt2)) + "', 'changeme@cms');");
                    }
                    catch (Exception ex)
                    {
                        return "Failed to create a default user - " + ex.Message + "!";
                    }
                }
            }
            // Install settings
            Core.settings.updateSetting(conn, pluginid, SETTINGS_USER_GROUP_DEFAULT, "1", "The default groupid assigned to new registered users.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_USER_GROUP_USER, "2", "The groupid for basic users.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_USER_GROUP_BANNED, "5", "The groupid of banned users.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_MAX_LOGIN_ATTEMPTS, "5", "The maximum login attempts during a max-login period.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_MAX_LOGIN_PERIOD, "20", "The period during which a maximum amount of login attempts can occur.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_SITE_NAME, "Unnamed CMS", "The name of the site, as displayed in e-mails.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_USERNAME_STRICT, "1", "If enabled, strict characters will only be allowed for usernames.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_USERNAME_STRICT_CHARS, "abcdefghijklmnopqrstuvwxyz._àèòáéóñ", "The strict characters allowed for usernames if strict-mode is enabled.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_RECOVERY_MAX_EMAILS, "3", "The maximum recovery e-mails to be dispatched from an IP per a day.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_RECOVERY_SQA_ATTEMPTS_MAX, "3", "The maximum attempts of answering a secret question during a specified interval.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_RECOVERY_SQA_ATTEMPTS_INTERVAL, "15", "The interval/number of minutes for maximum amounts of answering secret questions.", false);
            // Install templates
            Misc.Plugins.templatesInstall(basePath + "\\Templates", conn);
            if (error != null) return error;
            // Install content
            error = Misc.Plugins.contentInstall(basePath + "\\Content");
            if (error != null) return error;
            // Reserve URLs
            Misc.Plugins.reserveURLs(pluginid, null, new string[] { "login", "logout", "register", "recover", "my_account", "captcha" }, conn);
            if (error != null) return error;
            // No error occurred, return null
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Remove templates
            error = Misc.Plugins.templatesUninstall("bsa_site_auth", conn);
            if (error != null) return error;
            // Remove content
            error = Misc.Plugins.contentUninstall(basePath + "\\Content");
            if (error != null) return error;
            // Remove reserved URLs
            error = Misc.Plugins.unreserveURLs(pluginid, conn);
            if (error != null) return error;
            // No error occurred, return null
            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            string error = null;
            // Remove tables
            error = Misc.Plugins.executeSQL(Misc.Plugins.getPluginBasePath(pluginid, conn) + "\\SQL\\Uninstall.sql", conn);
            if (error != null) return error;
            // Remove settings
            Core.settings.removeSettings(conn, pluginid);
            // No error occurred, return null
            return null;
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
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            switch (request.QueryString["page"])
            {
                case "login":
                    if (HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageLogin(pluginid, conn, ref pageElements, request, response);
                    break;
                case "logout":
                    if (!HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageLogout(pluginid, conn, ref pageElements, request, response);
                    break;
                case "register":
                    if (HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageRegister(pluginid, conn, ref pageElements, request, response);
                    break;
                case "recover":
                    if (HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageRecover(pluginid, conn, ref pageElements, request, response);
                    break;
                case "my_account":
                    if (!HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageMyAccount(pluginid, conn, ref pageElements, request, response);
                    break;
                case "captcha":
                    pageCaptcha(pluginid, conn, ref pageElements, request, response);
                    break;
            }
        }
        public static void requestEnd(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            if (HttpContext.Current.User.Identity.IsAuthenticated)
            {
                // Select username and check for bans
                Result data = conn.Query_Read("SELECT u.username, COUNT(b.banid) AS active_bans FROM bsa_users AS u LEFT OUTER JOIN bsa_user_bans AS b ON (b.userid=u.userid AND ((b.unban_date IS NULL) OR (b.unban_date > NOW()) )) WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
                if (data.Rows.Count != 1 || int.Parse(data[0]["active_bans"]) > 0)
                {
                    // Dispose the current session - now invalid
                    FormsAuthentication.SignOut();
                    HttpContext.Current.Session.Abandon();
                    // Redirect to logout page to inform the user -- this will cause a 404 but also ensure the session has been disposed because it's invalid
                    response.Redirect(pageElements["URL"] + "/logout/banned", true);
                }
                else
                    pageElements["USERNAME"] = data[0]["username"];
            }
            if (HttpContext.Current.User.Identity.IsAuthenticated) pageElements.setFlag("AUTHENTICATED");
        }
        #endregion

        #region "Methods - Pages"
        /// <summary>
        /// Used to authenticate existing users.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageLogin(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            const string incorrectUserPassword = "Incorrect username or password!";
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
                    int maxLoginPeriod = int.Parse(Core.settings[SETTINGS_MAX_LOGIN_PERIOD]);
                    int maxLoginAttempts = int.Parse(Core.settings[SETTINGS_MAX_LOGIN_ATTEMPTS]);
                    // Check the IP has not tried to authenticate in the past
                    if (conn.Query_Count("SELECT COUNT('') FROM bsa_failed_logins WHERE ip='" + Utils.Escape(request.UserHostAddress) + "' AND datetime >= '" + Utils.Escape(DateTime.Now.AddMinutes(-maxLoginPeriod).ToString("yyyy-MM-dd HH:mm:ss")) + "'") >= maxLoginAttempts)
                        error = "You've exceeded the maximum login-attempts, try again in " + maxLoginPeriod + " minutes...";
                    else
                    {
                        // Authenticate
                        Result res = conn.Query_Read("SELECT u.userid, u.password, g.access_login, COUNT(b.banid) AS active_bans FROM bsa_users AS u LEFT OUTER JOIN bsa_user_groups AS g ON g.groupid=u.groupid LEFT OUTER JOIN bsa_user_bans AS b ON (b.userid=u.userid AND ((b.unban_date IS NULL) OR (b.unban_date > NOW()) )) WHERE u.username='" + Utils.Escape(username) + "'");
                        if (res.Rows.Count != 1 || res[0]["password"] != generateHash(password, salt1, salt2))
                        {
                            // Incorrect login - log as an attempt
                            // -- Check if the user exists, if so we'll log it into the user_log table
                            res = conn.Query_Read("SELECT userid FROM bsa_users WHERE username LIKE '" + username.Replace("%", "") + "'");
                            conn.Query_Execute((res.Rows.Count == 1 ? "INSERT INTO bsa_user_log (userid, event_type, date, additional_info) VALUES('" + Utils.Escape(res[0]["userid"]) + "', '" + (int)LogEvents.Login_Incorrect + "', NOW(), '" + Utils.Escape(request.UserHostAddress) + "'); " : string.Empty) + "INSERT INTO bsa_failed_logins (ip, attempted_username, datetime) VALUES('" + Utils.Escape(request.UserHostAddress) + "', '" + Utils.Escape(username) + "', NOW());");
                            error = incorrectUserPassword;
                        }
                        else if (!res[0]["access_login"].Equals("1"))
                            error = "Your account is not allowed to login; your account is either awaiting activation or you've been banned.";
                        else if (int.Parse(res[0]["active_bans"]) > 0)
                        {
                            Result currentBan = conn.Query_Read("SELECT reason, unban_date FROM bsa_user_bans WHERE userid='" + Utils.Escape(res[0]["userid"]) + "' ORDER BY unban_date DESC");
                            if (currentBan.Rows.Count == 0)
                                error = "You are currently banned.";
                            else
                                error = "Your account is currently banned until '" + (currentBan[0]["unban_date"].Length > 0 ? HttpUtility.HtmlEncode(currentBan[0]["unban_date"]) : "the end of time (permanent)")  + "' for the reason '" + HttpUtility.HtmlEncode(currentBan[0]["reason"]) + "'!";
                        }
                        else
                        {
                            // Authenticate the user
                            FormsAuthentication.SetAuthCookie(res[0]["userid"], persist);
                            // Check if a ref-url exists, if so redirect to it
                            conn.Disconnect();
                            if (request.UrlReferrer != null && !request.Url.AbsolutePath.EndsWith("login"))
                                response.Redirect(request.UrlReferrer.AbsoluteUri);
                            else
                                response.Redirect(pageElements["URL"]);
                        }
                    }
                }
            }
            // Display page
            pageElements["TITLE"] = "Login";
            pageElements["CONTENT"] = Core.templates["basic_site_auth"]["login"]
                .Replace("%USERNAME%", request.Form["username"] ?? string.Empty)
                .Replace("%PERSIST%", request.Form["persist"] != null ? "checked" : string.Empty)
                .Replace("%ERROR%", error != null ? Core.templates[null]["error"].Replace("%ERROR%", error) : string.Empty);
        }
        /// <summary>
        /// Used to sign-out the user/dispose the session.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageLogout(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Dispose the current session
            FormsAuthentication.SignOut();
            HttpContext.Current.Session.Abandon();
            // Inform the user
            pageElements["TITLE"] = "Logged-out";
            pageElements["CONTENT"] = Core.templates["basic_site_auth"]["logout"];
        }
        /// <summary>
        /// Used to register new accounts; this supports activation as well.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageRegister(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            switch (request.QueryString["1"])
            {
                case "success":
                    // Check which template to display - welcome or activation-required
                    bool activationNeeded = conn.Query_Scalar("SELECT access_login FROM bsa_user_groups WHERE groupid='" + Utils.Escape(Core.settings[SETTINGS_USER_GROUP_DEFAULT]) + "'").ToString().Equals("0");
                    if (activationNeeded)
                    {
                        pageElements["TITLE"] = "Register - Success - Verification Required";
                        pageElements["CONTENT"] = Core.templates["basic_site_auth"]["register_success_activate"];
                    }
                    else
                    {
                        pageElements["TITLE"] = "Register - Success";
                        pageElements["CONTENT"] = Core.templates["basic_site_auth"]["register_success"];
                    }
                    break;
                case "activate":
                case "deactivate":
                    bool activate = request.QueryString["1"].Equals("activate");
                    string dkey = request.QueryString["key"];
                    if (dkey != null)
                    {
                        // Locate the user-group associated with the key
                        Result res = conn.Query_Read("SELECT a.keyid, a.userid, u.groupid, u.username FROM bsa_activations AS a LEFT OUTER JOIN bsa_users AS u ON u.userid=a.userid");
                        // Ensure the condition is valid
                        if (res.Rows.Count == 1 && res[0]["groupid"] == Core.settings[SETTINGS_USER_GROUP_DEFAULT])
                        {
                            // Ensure the user wants to activate/deactivate their account
                            if (request.Form["confirm"] == null)
                            {
                                if (activate)
                                {
                                    pageElements["TITLE"] = "Register - Activate Account";
                                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["activate"]
                                        .Replace("%KEY%", request.QueryString["key"])
                                        .Replace("%USERNAME%", HttpUtility.HtmlEncode(res[0]["username"]));
                                }
                                else
                                {
                                    pageElements["TITLE"] = "Register - Deactivate Account";
                                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["deactivate"]
                                        .Replace("%KEY%", request.QueryString["key"])
                                        .Replace("%USERNAME%", HttpUtility.HtmlEncode(res[0]["username"]));
                                }
                            }
                            else
                            {
                                if (activate)
                                {
                                    // Remove the activation key and change the groupid
                                    conn.Query_Execute("DELETE FROM bsa_activations WHERE keyid='" + Utils.Escape(res[0]["keyid"]) + "'; UPDATE bsa_users SET groupid='" + Utils.Escape(Core.settings[SETTINGS_USER_GROUP_USER]) + "' WHERE userid='" + Utils.Escape(res[0]["userid"]) + "';");
                                    // Display confirmation
                                    pageElements["TITLE"] = "Account Activated";
                                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["activate_success"];
                                }
                                else
                                {
                                    // Delete the account
                                    conn.Query_Execute("DELETE FROM bsa_users WHERE userid='" + Utils.Escape(res[0]["userid"]) + "';");
                                    // Display confirmation
                                    pageElements["TITLE"] = "Account Deactivated";
                                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["deactivate_success"];
                                }
                            }
                        }
                    }
                    break;
                case null:
                    string error = null;
                    string username = request.Form["username"];
                    string password = request.Form["password"];
                    string confirmPassword = request.Form["confirm_password"];
                    string email = request.Form["email"];
                    string secretQuestion = request.Form["secret_question"];
                    string secretAnswer = request.Form["secret_answer"];
                    string captcha = request.Form["captcha"];
                    if (username != null && password != null && confirmPassword != null && email != null && secretQuestion != null && secretAnswer != null)
                    {
                        // Validate
                        if (!validCaptcha(captcha))
                            error = "Incorrect captcha code!";
                        else if (username.Length < USERNAME_MIN || username.Length > USERNAME_MAX)
                            error = "Username must be " + USERNAME_MIN + " to " + USERNAME_MAX + " characters in length!";
                        else if ((error = validUsernameChars(username)) != null)
                            ;
                        else if (password.Length < PASSWORD_MIN || password.Length > PASSWORD_MAX)
                            error = "Password must be " + PASSWORD_MIN + " to " + PASSWORD_MAX + " characters in length!";
                        else if (!validEmail(email))
                            error = "Invalid e-mail address!";
                        else if (secretQuestion.Length < SECRET_QUESTION_MIN || secretQuestion.Length > SECRET_QUESTION_MAX)
                            error = "Secret question must be " + SECRET_QUESTION_MIN + " to " + SECRET_QUESTION_MAX + " characters in length!";
                        else if (secretAnswer.Length < SECRET_ANSWER_MIN || secretAnswer.Length > SECRET_ANSWER_MAX)
                            error = "Secret answer must be " + SECRET_ANSWER_MIN + " to " + SECRET_ANSWER_MAX + " characters in length!";
                        else
                        {
                            // Attempt to insert the user
                            Dictionary<string, string> inputParams = new Dictionary<string, string>();
                            inputParams.Add("groupid", Core.settings[SETTINGS_USER_GROUP_DEFAULT]);
                            inputParams.Add("username", username);
                            inputParams.Add("password", generateHash(password, salt1, salt2));
                            inputParams.Add("email", email);
                            inputParams.Add("secret_question", secretQuestion);
                            inputParams.Add("secret_answer", secretAnswer);
                            Dictionary<string, Connector.DataType> outputParams = new Dictionary<string, Connector.DataType>();
                            outputParams.Add("userid", Connector.DataType.Text);
                            Result registerResult = conn.Query_Read_StoredProcedure("bsa_register", inputParams, outputParams);
                            if (registerResult.Rows.Count == 0) error = "Failed to register your account, please try again later or contact us!";
                            else
                            {
                                int registerCode = int.Parse(registerResult[0]["userid"]);
                                if (registerCode == -100)
                                    error = "E-mail already in-use by another user!";
                                else if (registerCode == -200)
                                    error = "Username already in-use!";
                                else if (registerCode < 0)
                                    error = "Unknown error occurred (" + registerCode + ")!";
                                else
                                {
                                    // Send a welcome or activation e-mail
                                    bool activation = conn.Query_Scalar("SELECT access_login FROM bsa_user_groups WHERE groupid='" + Utils.Escape(Core.settings[SETTINGS_USER_GROUP_DEFAULT]) + "'").ToString().Equals("0");
                                    StringBuilder emailMessage;
                                    if (activation)
                                    {
                                        // Generate activation key
                                        string activationKey = randomText(16);
                                        conn.Query_Execute("INSERT INTO bsa_activations (userid, code) VALUES('" + registerCode + "', '" + Utils.Escape(activationKey) + "');");
                                        // Generate message
                                        string baseURL = "http://" + request.Url.Host + (request.Url.Port != 80 ? ":" + request.Url.Port : string.Empty);
                                        emailMessage = new StringBuilder(Core.templates["basic_site_auth"]["email_register_activate"]);
                                        emailMessage
                                            .Replace("%USERNAME%", username)
                                            .Replace("%URL_ACTIVATE%", baseURL + "/register/activate/?key=" + activationKey)
                                            .Replace("%URL_DELETE%", baseURL + "/register/deactivate/?key=" + activationKey)
                                            .Replace("%IP_ADDRESS%", request.UserHostAddress)
                                            .Replace("%BROWSER%", request.UserAgent)
                                            .Replace("%DATE_TIME%", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                                    }
                                    else
                                    {
                                        emailMessage = new StringBuilder(Core.templates["basic_site_auth"]["email_register_welcome"]);
                                        emailMessage
                                            .Replace("%USERNAME%", username)
                                            .Replace("%IP_ADDRESS%", request.UserHostAddress)
                                            .Replace("%BROWSER%", request.UserAgent)
                                            .Replace("%DATE_TIME%", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                                    }
                                    // Add e-mail to queue
                                    Core.emailQueue.add(email, Core.settings[SETTINGS_SITE_NAME] + " - Registration", emailMessage.ToString(), true);
                                    // Show registration success page
                                    conn.Disconnect();
                                    response.Redirect(pageElements["URL"] + "/register/success", true);
                                }
                            }
                        }
                    }
                    pageElements["TITLE"] = "Register";
                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["register"]
                        .Replace("%USERNAME%", request.Form["username"] ?? string.Empty)
                        .Replace("%EMAIL%", request.Form["email"] ?? string.Empty)
                        .Replace("%SECRET_QUESTION%", request.Form["secret_question"] ?? string.Empty)
                        .Replace("%SECRET_ANSWER%", request.Form["secret_answer"] ?? string.Empty)
                        .Replace("%ERROR%", error != null ? Core.templates[null]["error"].Replace("%ERROR%", error) : string.Empty);
                    break;
            }

        }
        /// <summary>
        /// Used to recover an account; due to much code, this method pretty much pushes the request onto another method (to keep the code clean).
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageRecover(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Check which function the user wants
            switch (request.QueryString["1"])
            {
                case "email":
                    pageRecover_Email(pluginid, conn, ref pageElements, request, response);
                    break;
                case "secret_qa":
                    pageRecover_SecretQA(pluginid, conn, ref pageElements, request, response);
                    break;
                case null:
                    pageElements["TITLE"] = "Account Recovery";
                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["recovery_menu"];
                    break;
            }
        }
        /// <summary>
        /// Used to allow the user to recover their account using the secret question and answer mechanism.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageRecover_SecretQA(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.QueryString["2"] != null && request.QueryString["2"] == "success")
            {
                // Display success page
                pageElements["TITLE"] = "Account Recovery - Secret Question - Success";
                pageElements["CONTENT"] = Core.templates["basic_site_auth"]["recovery_qa_success"];
            }
            else
            {
                string error = null;
                string username = request.Form["username"];
                string captcha = request.Form["captcha"];
                string userid = null;
                // Check if the user is looking up a user for the first time - we'll allow the user to answer if the captcha is valid - this is security against brute-force to test if users exist
                if (username != null && captcha != null)
                {
                    // Verify captcha
                    if (!validCaptcha(captcha))
                        error = "Incorrect captcha code!";
                    else
                        HttpContext.Current.Session["recover_sqa"] = username;
                }
                // Check if the user exists
                if (username != null)
                {
                    string rawUserid = (conn.Query_Scalar("SELECT userid FROM bsa_users WHERE username LIKE '" + Utils.Escape(username.Replace("%", "")) + "'") ?? string.Empty).ToString();
                    if (rawUserid.Length > 0)
                        userid = rawUserid;
                    else
                        error = "User does not exist!";
                }
                // Check the user has not exceeded the maximum secret answering attempts
                if (conn.Query_Count("SELECT COUNT('') FROM bsa_recovery_sqa_attempts WHERE ip='" + Utils.Escape(request.UserHostAddress) + "' AND datetime >= DATE_SUB(NOW(), INTERVAL " + Core.settings.getInt(SETTINGS_RECOVERY_SQA_ATTEMPTS_INTERVAL) + " MINUTE)") >= Core.settings.getInt(SETTINGS_RECOVERY_SQA_ATTEMPTS_MAX))
                    error = "You have exceeded the maximum attempts at answering a secret-question from this IP, come back in " + Core.settings.getInt(SETTINGS_RECOVERY_SQA_ATTEMPTS_INTERVAL) + " minutes!";
                // Check if the user wants the form for answering a secret question - but only if a username has been posted, exists and captcha is valid
                if (error == null && userid != null && HttpContext.Current.Session["recover_sqa"] != null && username == (string)HttpContext.Current.Session["recover_sqa"])
                {
                    // Fetch the secret question & password
                    ResultRow sqa = conn.Query_Read("SELECT secret_question, secret_answer FROM bsa_users WHERE userid='" + Utils.Escape(userid) + "'")[0];
                    if (sqa["secret_question"].Length == 0 || sqa["secret_answer"].Length == 0)
                        error = "Secret question recovery for this account has been disabled!";
                    else
                    {
                        // Check for postback
                        string secretAnswer = request.Form["secret_answer"];
                        string newPassword = request.Form["newpassword"];
                        string newPasswordConfirm = request.Form["newpassword_confirm"];
                        if (username != null && secretAnswer != null)
                        {
                            const string incorrectAnswer = "Incorrect secret answer!";
                            // Validate
                            if (secretAnswer.Length < SECRET_ANSWER_MIN || secretAnswer.Length > SECRET_ANSWER_MAX)
                                error = incorrectAnswer;
                            else if (newPassword != newPasswordConfirm)
                                error = "Your new password and the confirm password are different, retype your password!";
                            else if (newPassword.Length < PASSWORD_MIN || newPassword.Length > PASSWORD_MAX)
                                error = "Password must be " + PASSWORD_MIN + " to " + PASSWORD_MAX + " characters in length!";
                            else if (sqa["secret_answer"] != secretAnswer)
                            {
                                // Insert the attempt, as well as inform the user
                                conn.Query_Execute("INSERT INTO bsa_recovery_sqa_attempts (ip, datetime) VALUES('" + Utils.Escape(request.UserHostAddress) + "', NOW())");
                                error = "Incorrect secret answer!";
                            }
                            else
                            {
                                // Change the password
                                conn.Query_Execute("UPDATE bsa_users SET password='" + Utils.Escape(generateHash(newPassword, salt1, salt2)) + "' WHERE userid='" + Utils.Escape(userid) + "'");
                                // Redirect to success page
                                response.Redirect(pageElements["URL"] + "/recover/secret_qa/success");
                            }
                        }
                        // Display form
                        pageElements["TITLE"] = "Account Recovery - Secret Question";
                        pageElements["CONTENT"] = Core.templates["basic_site_auth"]["recovery_qa_question"]
                            .Replace("%USERNAME%", HttpUtility.HtmlEncode(username ?? string.Empty))
                            .Replace("%SECRET_QUESTION%", HttpUtility.HtmlEncode(sqa["secret_question"]))
                            .Replace("%SECRET_ANSWER%", HttpUtility.HtmlEncode(secretAnswer ?? string.Empty))
                            .Replace("%ERROR%", error != null ? Core.templates[null]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty);
                    }
                }
                if(pageElements["CONTENT"] == null || pageElements["CONTENT"].Length == 0)
                {
                    // Ask user for which user
                    pageElements["TITLE"] = "Account Recovery - Secret Question";
                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["recovery_qa_user"]
                        .Replace("%USERNAME%", HttpUtility.HtmlEncode(username ?? string.Empty))
                        .Replace("%ERROR%", error != null ? Core.templates[null]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty);
                }
            }
        }
        /// <summary>
        /// Used to recover
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageRecover_Email(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string error = null;
            if (request.QueryString["2"] != null)
            {
                // User has opened a recovery link
                string code = request.QueryString["2"];
                // Check the code is valid and retrieve the account
                Result rec = conn.Query_Read("SELECT recoveryid, userid, datetime_dispatched FROM bsa_recovery_email WHERE code='" + Utils.Escape(code) + "'");
                if (rec.Rows.Count == 1)
                {
                    // Code exists, display change password form
                    string newPassword = request.Form["new_password"];
                    bool passwordChanged = false;
                    if (newPassword != null)
                    {
                        // User has specified new password
                        if (newPassword.Length < PASSWORD_MIN || newPassword.Length > PASSWORD_MAX)
                            error = "Password must be " + PASSWORD_MIN + " to " + PASSWORD_MAX + " characters in length!";
                        else
                        {
                            // Update the password and delete the recovery row
                            conn.Query_Execute("DELETE FROM bsa_recovery_email WHERE recoveryid='" + Utils.Escape(rec[0]["recoveryid"]) + "'; UPDATE bsa_users SET password='" + Utils.Escape(generateHash(newPassword, salt1, salt2)) + "' WHERE userid='" + Utils.Escape(rec[0]["userid"]) + "';");
                            passwordChanged = true;
                        }
                    }
                    // Display form
                    if (passwordChanged)
                    {
                        pageElements["TITLE"] = "Account Recovery - Password Changed";
                        pageElements["CONTENT"] = Core.templates["basic_site_auth"]["recovery_email_changed"];
                    }
                    else
                    {
                        pageElements["TITLE"] = "Account Recovery - New Password";
                        pageElements["CONTENT"] = Core.templates["basic_site_auth"]["recovery_email_newpass"]
                            .Replace("%CODE%", HttpUtility.UrlEncode(code))
                            .Replace("%ERROR%", error != null ? Core.templates[null]["error"].Replace("%ERROR%", error) : string.Empty);
                    }
                }
            }
            else
            {
                // Ask for username, if postback..validate and dispatch recovery e-mail
                bool emailDispatched = false;
                string captcha = request.Form["captcha"];
                string username = request.Form["username"];
                if (username != null && captcha != null)
                {
                    if (!validCaptcha(captcha))
                        error = "Incorrect captcha code!";
                    else
                    {
                        // Validate the user exists and check the current IP hasn't surpassed the number of e-mails deployed for the day
                        Result info = conn.Query_Read("SELECT u.username, u.userid, u.email, COUNT(re.recoveryid) AS dispatches FROM bsa_users AS u LEFT OUTER JOIN bsa_recovery_email AS re ON (re.userid=u.userid AND re.ip='" + Utils.Escape(request.UserHostAddress) + "' AND re.datetime_dispatched >= DATE_SUB(NOW(), INTERVAL 1 DAY)) WHERE u.username LIKE '" + Utils.Escape(username.Replace("%", "")) + "'");
                        if (info.Rows.Count != 1)
                            error = "User does not exist!";
                        else if (int.Parse(info[0]["dispatches"]) >= int.Parse(Core.settings[SETTINGS_RECOVERY_MAX_EMAILS]))
                            error = "You've already sent the maximum amount of recovery e-mails allowed within the last 24 hours!";
                        else
                        {
                            string baseURL = "http://" + request.Url.Host + (request.Url.Port != 80 ? ":" + request.Url.Port : string.Empty);
                            // Create recovery record
                            string code = null;
                            // Ensure the code doesn't already exist - this is a major security concern because the code is essentially a password to an account
                            int attempts = 0;
                            while (attempts < 5)
                            {
                                code = randomText(16);
                                if (conn.Query_Count("SELECT COUNT('') FROM bsa_recovery_email WHERE code LIKE '" + Utils.Escape(code) + "'") == 0)
                                    break;
                                else
                                    code = null;
                                attempts++;
                            }
                            if (code == null)
                                error = "Unable to generate recovery code, try again - apologies!";
                            else
                            {
                                conn.Query_Execute("INSERT INTO bsa_recovery_email (userid, code, datetime_dispatched, ip) VALUES('" + Utils.Escape(info[0]["userid"]) + "', '" + Utils.Escape(code) + "', NOW(), '" + Utils.Escape(request.UserHostAddress) + "')");
                                // Build e-mail message
                                StringBuilder message = new StringBuilder(Core.templates["basic_site_auth"]["recovery_email"]);
                                message
                                    .Replace("%USERNAME%", info[0]["username"])
                                    .Replace("%URL%", baseURL + "/recover/email/" + code)
                                    .Replace("%IP_ADDRESS%", request.UserHostAddress)
                                    .Replace("%BROWSER%", request.UserAgent)
                                    .Replace("%DATE_TIME%", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                                // Add e-mail to queue
                                Core.emailQueue.add(info[0]["email"], Core.settings[SETTINGS_SITE_NAME] + " - Account Recovery", message.ToString(), true);
                                // Set dispatched flag to true to show success message
                                emailDispatched = true;
                            }
                        }
                    }
                }
                // Display form
                if (emailDispatched)
                {
                    pageElements["TITLE"] = "Account Recovery - Email - Successful";
                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["recovery_email_success"];
                }
                else
                {
                    pageElements["TITLE"] = "Account Recovery - E-mail";
                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["recovery_email_form"]
                        .Replace("%ERROR%", error != null ? Core.templates[null]["error"].Replace("%ERROR%", error) : string.Empty);
                }
            }
        }
        /// <summary>
        /// Used by the user to update account details such as their password, e-mail and secret question+answer.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageMyAccount(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string error = null;
            string currentPassword = request.Form["currentpassword"];
            string newPassword = request.Form["newpassword"];
            string newPasswordConfirm = request.Form["newpassword_confirm"];
            string email = request.Form["email"];
            string secretQuestion = request.Form["secret_question"];
            string secretAnswer = request.Form["secret_answer"];
            bool updatedSettings = false;
            if (currentPassword != null && newPassword != null && newPasswordConfirm != null && email != null && secretQuestion != null && secretAnswer != null)
            {
                if (currentPassword.Length < PASSWORD_MIN || currentPassword.Length > PASSWORD_MAX || generateHash(currentPassword, salt1, salt2) != conn.Query_Scalar("SELECT password FROM bsa_users WHERE userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'").ToString())
                    error = "Incorrect current password!";
                else if (newPassword.Length != 0 && newPassword.Length < PASSWORD_MIN || newPassword.Length > PASSWORD_MAX)
                    error = "Your new password must be between " + PASSWORD_MIN + " to " + PASSWORD_MAX + " characters in length!";
                else if (newPassword.Length != 0 && newPassword != newPasswordConfirm)
                    error = "Your new password does not match, please retype it!";
                else if (email.Length != 0 && !validEmail(email))
                    error = "Invalid e-mail address!";
                else if (secretQuestion.Length < SECRET_QUESTION_MIN || secretQuestion.Length > SECRET_QUESTION_MAX)
                    error = "Secret question must be " + SECRET_QUESTION_MIN + " to " + SECRET_QUESTION_MAX + " characters in length!";
                else if (secretAnswer.Length < SECRET_ANSWER_MIN || secretAnswer.Length > SECRET_ANSWER_MAX)
                    error = "Secret answer must be " + SECRET_ANSWER_MIN + " to " + SECRET_ANSWER_MAX + " characters in length!";
                else
                {
                    // Update main account details
                    StringBuilder query = new StringBuilder("UPDATE bsa_users SET secret_question='" + Utils.Escape(secretQuestion) + "', secret_answer='" + Utils.Escape(secretAnswer) + "',");
                    if (newPassword.Length > 0)
                        query.Append("password='" + Utils.Escape(generateHash(newPassword, salt1, salt2)) + "',");
                    if (email.Length > 0)
                        query.Append("email='" + Utils.Escape(email) + "',");
                    conn.Query_Execute(query.Remove(query.Length - 1, 1).Append(" WHERE userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'").ToString());
                    // Attempt to update the e-mail
                    Dictionary<string, string> inputs = new Dictionary<string, string>();
                    inputs.Add("userid", HttpContext.Current.User.Identity.Name);
                    inputs.Add("email", email != null && email.Length > 0 ? email : string.Empty);
                    Dictionary<string, Connector.DataType> outputs = new Dictionary<string, Connector.DataType>();
                    outputs.Add("errorcode", Connector.DataType.Int32);
                    // Store the result and parse it
                    Result res = conn.Query_Read_StoredProcedure("bsa_change_email", inputs, outputs);
                    if (res.Rows.Count != 1)
                        error = "Unknown error occurred!";
                    else if (res[0]["errorcode"] != "1")
                        error = "E-mail already in-use by another user!";
                    else
                        updatedSettings = true;
                }
            }
            // Grab account info
            ResultRow userInfo = conn.Query_Read("SELECT email, secret_question, secret_answer FROM bsa_users WHERE userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'")[0];
            // Display form
            pageElements["TITLE"] = "My Account";
            pageElements["CONTENT"] = Core.templates["basic_site_auth"]["my_account"]
                .Replace("%EMAIL%", HttpUtility.HtmlEncode(email ?? userInfo["email"]))
                .Replace("%SECRET_QUESTION%", HttpUtility.HtmlEncode(secretQuestion ?? userInfo["secret_question"]))
                .Replace("%SECRET_ANSWER%", HttpUtility.HtmlEncode(secretAnswer ?? userInfo["secret_answer"]))
                .Replace("%ERROR%", error != null ? Core.templates[null]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : updatedSettings ? Core.templates[null]["success"].Replace("%SUCCESS%", "Account settings successfully updated!") : string.Empty);
        }
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
            string text = randomText(6);
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
            for(int i = 0; i < text.Length; i++)
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
            for(int i = 0; i < 5; i++)
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

        #region "Methods"
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
        /// <summary>
        /// Generates a hash for a string, i.e. a password, for securely storing data within a database.
        /// 
        /// This uses SHA512, two salts and a custom shifting algorithm.
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
                        buffer = salt1.Length + rawData[i] * (rawSalt1[s1] + salt2.Length) * rawSalt2[s2] + rawData.Length;
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
        private static string randomText(int length)
        {
            string chars = "abcdefghijklmnopqrstuvwxyz01234567890";
            StringBuilder text = new StringBuilder();
            Random ran = new Random(DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond);
            for (int i = 0; i < length; i++)
                text.Append(chars[ran.Next(0, chars.Length - 1)].ToString());
            return text.ToString();
        }
        /// <summary>
        /// Validates an e-mail address; credit for the regex pattern goes to the following article:
        /// http://www.codeproject.com/Articles/22777/Email-Address-Validation-Using-Regular-Expression
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static bool validEmail(string text)
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
        /// Validates the characters of a username are allowed.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static string validUsernameChars(string text)
        {
            string strictChars = Core.settings["bsa_username_strict_chars"];
            // Check if strict mode is enabled, else we'll allow any char
            if (Core.settings.getBool("bsa_username_strict"))
            {
                string c;
                foreach (char rc in text)
                {
                    c = rc.ToString();
                    if (!strictChars.Contains(c))
                        return "Username cannot contain the character '" + c + "'!";
                }
            }
            else if (text.StartsWith(" "))
                return "Username cannot start with a space!";
            else if (text.EndsWith(" "))
                return "Username cannot end with a space!";
            return null;
        }
        #endregion
    }
}