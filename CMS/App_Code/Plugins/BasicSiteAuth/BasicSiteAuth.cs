#define BASIC_SITE_AUTH
#define BASIC_SITE_AUTH_ADMIN // Remove this preprocessor directive to disable the admin panel

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
        #region "Enums - Logging"
        public enum LogEvents
        {
            // Registration
            Registered = 0,
            Registration_Activated = 1,
            // Authentication - login
            Login_Incorrect = 10,
            Login_Authenticated = 11,
            // Authentication - recovery
            AccountRecovery_SQA_Incorrect = 21,
            AccountRecovered_Email = 30,
            AccountRecovered_SQA = 31,
            // Account
            MyAccountUpdated = 40,
            // Admin panel
            AdminPanel_Accessed = 100,
        }
        #endregion

        #region "Constants"
        public const int USERNAME_MIN = 3;
        public const int USERNAME_MAX = 18;
        public const int PASSWORD_MIN = 3;
        public const int PASSWORD_MAX = 40;
        public const int SECRET_QUESTION_MIN = 0;
        public const int SECRET_QUESTION_MAX = 40;
        public const int SECRET_ANSWER_MIN = 0;
        public const int SECRET_ANSWER_MAX = 40;
        public const int USER_GROUP_TITLE_MIN = 1;
        public const int USER_GROUP_TITLE_MAX = 25;
        #endregion

        #region "Constants - Setting Keys"
        public const string SETTINGS_CATEGORY = "basic_site_auth";
        public const string SETTINGS_MAX_LOGIN_ATTEMPTS = "max_login_attempts";
        public const string SETTINGS_MAX_LOGIN_PERIOD = "max_login_period";
        public const string SETTINGS_USER_GROUP_DEFAULT = "user_group_default";
        public const string SETTINGS_USER_GROUP_USER = "user_group_user";
        public const string SETTINGS_USER_GROUP_BANNED = "user_group_banned";
        public const string SETTINGS_SITE_NAME = "site_name";
        public const string SETTINGS_USERNAME_STRICT = "username_strict";
        public const string SETTINGS_USERNAME_STRICT_CHARS = "username_strict_chars";
        public const string SETTINGS_RECOVERY_MAX_EMAILS = "max_recovery_emails";
        public const string SETTINGS_RECOVERY_SQA_ATTEMPTS_MAX = "sqa_attempts_max";
        public const string SETTINGS_RECOVERY_SQA_ATTEMPTS_INTERVAL = "sqa_attempts_interval";
        #endregion

        #region "Variables"
        public static string salt1, salt2;
        #endregion

        #region "Methods - Plugin Event Handlers"
        public static string enable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Install SQL
            error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Enable.sql", conn);
            if (error != null) return error;
            // Check authentication salts exist - else create them
            initSalts(pluginid, conn);
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
                        conn.Query_Execute("INSERT INTO bsa_users (groupid, username, password, email) VALUES('4', 'root', '" + Utils.Escape(generateHash("password", salt1, salt2)) + "', 'changeme@cms');");
                    }
                    catch (Exception ex)
                    {
                        return "Failed to create a default user - " + ex.Message + "!";
                    }
                }
            }
            // Install settings
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_USER_GROUP_DEFAULT, "1", "The default groupid assigned to new registered users.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_USER_GROUP_USER, "2", "The groupid for basic users.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_USER_GROUP_BANNED, "5", "The groupid of banned users.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_MAX_LOGIN_ATTEMPTS, "5", "The maximum login attempts during a max-login period.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_MAX_LOGIN_PERIOD, "20", "The period during which a maximum amount of login attempts can occur.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_SITE_NAME, "Unnamed CMS", "The name of the site, as displayed in e-mails.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_USERNAME_STRICT, "1", "If enabled, strict characters will only be allowed for usernames.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_USERNAME_STRICT_CHARS, "abcdefghijklmnopqrstuvwxyz._àèòáéóñ0123456789", "The strict characters allowed for usernames if strict-mode is enabled.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_RECOVERY_MAX_EMAILS, "3", "The maximum recovery e-mails to be dispatched from an IP per a day.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_RECOVERY_SQA_ATTEMPTS_MAX, "3", "The maximum attempts of answering a secret question during a specified interval.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_CATEGORY, SETTINGS_RECOVERY_SQA_ATTEMPTS_INTERVAL, "15", "The interval/number of minutes for maximum amounts of answering secret questions.", false);
            // Install templates
            Misc.Plugins.templatesInstall(basePath + "\\Templates\\basic_site_auth", conn);
            if (error != null) return error;
            // Install content
            error = Misc.Plugins.contentInstall(basePath + "\\Content");
            if (error != null) return error;
            // Reserve URLs
            Misc.Plugins.reserveURLs(pluginid, null, new string[] { "login", "logout", "register", "recover", "my_account", "captcha", "log" }, conn);
            if (error != null) return error;
            // Reserve URL for admin system
#if BASIC_SITE_AUTH_ADMIN
            Misc.Plugins.reserveURLs(pluginid, null, new string[] { "admin" }, conn);
#endif
            // Install admin pages
            adminPage_Install("UberCMS.BasicSiteAuth.Admin", "pageUsers", "Users", "Authentication", "Content/Images/bsa_admin/admin_users.png", conn);
            adminPage_Install("UberCMS.BasicSiteAuth.Admin", "pageUserGroups", "User Groups", "Authentication", "Content/Images/bsa_admin/admin_user_groups.png", conn);
            adminPage_Install("UberCMS.BasicSiteAuth.Admin", "pageUserLogs", "User Logs", "Authentication", "Content/Images/bsa_admin/admin_user_logs.png", conn);
            adminPage_Install("UberCMS.BasicSiteAuth.Admin", "pageEmailQueue", "E-mail Queue", "Core", "Content/Images/bsa_admin/admin_email_queue.png", conn);
            adminPage_Install("UberCMS.BasicSiteAuth.Admin", "pagePlugins", "Plugins", "Core", "Content/Images/bsa_admin/admin_plugins.png", conn);
            adminPage_Install("UberCMS.BasicSiteAuth.Admin", "pageSettings", "Settings", "Core", "Content/Images/bsa_admin/admin_settings.png", conn);
            // No error occurred, return null
            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string error = null;
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            // Remove templates
            error = Misc.Plugins.templatesUninstall("basic_site_auth", conn);
            if (error != null) return error;
            // Remove content
            error = Misc.Plugins.contentUninstall(basePath + "\\Content");
            if (error != null) return error;
            // Remove reserved URLs
            error = Misc.Plugins.unreserveURLs(pluginid, conn);
            if (error != null) return error;
            // Remove admin pages
            adminPage_Uninstall("UberCMS.Plugins.BSA_Admin", "pageUsers", conn);
            adminPage_Uninstall("UberCMS.Plugins.BSA_Admin", "pageUserGroups", conn);
            adminPage_Uninstall("UberCMS.Plugins.BSA_Admin", "pageUserLogs", conn);
            adminPage_Uninstall("UberCMS.Plugins.BSA_Admin", "pageEmailQueue", conn);
            adminPage_Uninstall("UberCMS.Plugins.BSA_Admin", "pagePlugins", conn);
            adminPage_Uninstall("UberCMS.Plugins.BSA_Admin", "pageSettings", conn);
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
            Core.settings.removeCategory(conn, SETTINGS_CATEGORY);
            // No error occurred, return null
            return null;
        }
        public static string cmsStart(string pluginid, Connector conn)
        {
            initSalts(pluginid, conn);
            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            switch (request.QueryString["page"])
            {
                case "login":
                    if (HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageLogin(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "logout":
                    if (!HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageLogout(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "register":
                    if (HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageRegister(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "recover":
                    if (HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageRecover(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "my_account":
                    if (!HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageMyAccount(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "captcha":
                    pageCaptcha(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "log":
                    if (!HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageLog(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
#if BASIC_SITE_AUTH_ADMIN
                case "admin":
                    if (!HttpContext.Current.User.Identity.IsAuthenticated) return;
                    pageAdmin(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
#endif
            }
        }
        public static void requestEnd(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            if (HttpContext.Current.User.Identity.IsAuthenticated)
            {
                // Set base flag(s)
                pageElements.setFlag("AUTHENTICATED");
                // Select username and check for bans
                Result data = conn.Query_Read("SELECT u.username, COUNT(b.banid) AS active_bans, g.title, g.access_login FROM bsa_users AS u LEFT OUTER JOIN bsa_user_bans AS b ON (b.userid=u.userid AND ((b.unban_date IS NULL) OR (b.unban_date > NOW()) )) LEFT OUTER JOIN bsa_user_groups AS g ON g.groupid=u.groupid WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
                if (data.Rows.Count != 1 || int.Parse(data[0]["active_bans"]) > 0 || !data[0]["access_login"].Equals("1"))
                {
                    // Dispose the current session - now invalid
                    FormsAuthentication.SignOut();
                    HttpContext.Current.Session.Abandon();
                    // Redirect to logout page to inform the user -- this will cause a 404 but also ensure the session has been disposed because it's invalid
                    response.Redirect(pageElements["URL"] + "/logout/banned", true);
                }
                else
                    pageElements["USERNAME"] = data[0]["username"];
                // Set group flag
                pageElements.setFlag("GROUP_" + data[0]["title"]);
            }
            // Add CSS file
            Misc.Plugins.addHeaderCSS("/Content/CSS/BasicSiteAuth.css", ref pageElements);
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
        private static void pageLogin(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
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
                    int maxLoginPeriod = int.Parse(Core.settings[SETTINGS_CATEGORY][SETTINGS_MAX_LOGIN_PERIOD]);
                    int maxLoginAttempts = int.Parse(Core.settings[SETTINGS_CATEGORY][SETTINGS_MAX_LOGIN_ATTEMPTS]);
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
                            conn.Query_Execute("INSERT INTO bsa_failed_logins (ip, attempted_username, datetime) VALUES('" + Utils.Escape(request.UserHostAddress) + "', '" + Utils.Escape(username) + "', NOW());");
                            // Log event
                            logEvent(res[0]["userid"], LogEvents.Login_Incorrect, request.UserHostAddress + " - " + request.UserAgent, conn);
                            // Inform the user
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
                                error = "Your account is currently banned until '" + (currentBan[0]["unban_date"].Length > 0 ? HttpUtility.HtmlEncode(currentBan[0]["unban_date"]) : "the end of time (permanent)") + "' for the reason '" + HttpUtility.HtmlEncode(currentBan[0]["reason"]) + "'!";
                        }
                        else
                        {
                            // Authenticate the user
                            FormsAuthentication.SetAuthCookie(res[0]["userid"], persist);
                            // Log the event
                            logEvent(res[0]["userid"], LogEvents.Login_Authenticated, request.UserHostAddress + " - " + request.UserAgent, conn);
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
                .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", error) : string.Empty);
        }
        /// <summary>
        /// Used to sign-out the user/dispose the session.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageLogout(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
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
        private static void pageRegister(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            switch (request.QueryString["1"])
            {
                case "success":
                    // Check which template to display - welcome or activation-required
                    bool activationNeeded = conn.Query_Scalar("SELECT access_login FROM bsa_user_groups WHERE groupid='" + Utils.Escape(Core.settings[SETTINGS_CATEGORY][SETTINGS_USER_GROUP_DEFAULT]) + "'").ToString().Equals("0");
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
                        if (res.Rows.Count == 1 && res[0]["groupid"] == Core.settings[SETTINGS_CATEGORY][SETTINGS_USER_GROUP_DEFAULT])
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
                                    conn.Query_Execute("DELETE FROM bsa_activations WHERE keyid='" + Utils.Escape(res[0]["keyid"]) + "'; UPDATE bsa_users SET groupid='" + Utils.Escape(Core.settings[SETTINGS_CATEGORY][SETTINGS_USER_GROUP_USER]) + "' WHERE userid='" + Utils.Escape(res[0]["userid"]) + "';");
                                    // Log the event
                                    logEvent(res[0]["userid"], LogEvents.Registration_Activated, request.UserHostAddress, conn);
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
                            try
                            {
                                int userid = conn.Query_Count("INSERT INTO bsa_users (groupid, username, password, email, secret_question, secret_answer, registered) VALUES('" + Utils.Escape(Core.settings[SETTINGS_CATEGORY][SETTINGS_USER_GROUP_DEFAULT]) + "', '" + Utils.Escape(username) + "', '" + Utils.Escape(generateHash(password, salt1, salt2)) + "', '" + Utils.Escape(email) + "', '" + Utils.Escape(secretQuestion) + "', '" + Utils.Escape(secretAnswer) + "', NOW()); SELECT LAST_INSERT_ID();");
                                // Log registration
                                logEvent(userid.ToString(), LogEvents.Registered, null, conn);
                                // Send a welcome or activation e-mail
                                bool activation = conn.Query_Scalar("SELECT access_login FROM bsa_user_groups WHERE groupid='" + Utils.Escape(Core.settings[SETTINGS_CATEGORY][SETTINGS_USER_GROUP_DEFAULT]) + "'").ToString().Equals("0");
                                StringBuilder emailMessage;
                                if (activation)
                                {
                                    // Generate activation key
                                    string activationKey = randomText(16);
                                    conn.Query_Execute("INSERT INTO bsa_activations (userid, code) VALUES('" + userid + "', '" + Utils.Escape(activationKey) + "');");
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
                            catch (DuplicateEntryException ex)
                            {
                                switch (ex.Column)
                                {
                                    case "username":
                                        error = "Username already in-use!";
                                        break;
                                    case "email":
                                        error = "E-mail already in-use!";
                                        break;
                                    default:
                                        error = "Unknown error occurred, apologies - try again!";
                                        break;
                                }
                            }
                            catch (Exception)
                            {
                                error = "Unknown error occurred, apologies - try again!";
                            }
                        }
                    }
                    pageElements["TITLE"] = "Register";
                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["register"]
                        .Replace("%USERNAME%", request.Form["username"] ?? string.Empty)
                        .Replace("%EMAIL%", request.Form["email"] ?? string.Empty)
                        .Replace("%SECRET_QUESTION%", request.Form["secret_question"] ?? string.Empty)
                        .Replace("%SECRET_ANSWER%", request.Form["secret_answer"] ?? string.Empty)
                        .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", error) : string.Empty);
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
        private static void pageRecover(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Check which function the user wants
            switch (request.QueryString["1"])
            {
                case "email":
                    pageRecover_Email(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "secret_qa":
                    pageRecover_SecretQA(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
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
        private static void pageRecover_SecretQA(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
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
                if (conn.Query_Count("SELECT COUNT('') FROM bsa_recovery_sqa_attempts WHERE ip='" + Utils.Escape(request.UserHostAddress) + "' AND datetime >= DATE_SUB(NOW(), INTERVAL " + Core.settings[SETTINGS_CATEGORY].getInt(SETTINGS_RECOVERY_SQA_ATTEMPTS_INTERVAL) + " MINUTE)") >= Core.settings[SETTINGS_CATEGORY].getInt(SETTINGS_RECOVERY_SQA_ATTEMPTS_MAX))
                    error = "You have exceeded the maximum attempts at answering a secret-question from this IP, come back in " + Core.settings[SETTINGS_CATEGORY].getInt(SETTINGS_RECOVERY_SQA_ATTEMPTS_INTERVAL) + " minutes!";
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
                                // Insert the attempt
                                conn.Query_Execute("INSERT INTO bsa_recovery_sqa_attempts (ip, datetime) VALUES('" + Utils.Escape(request.UserHostAddress) + "', NOW())");
                                // Log the event
                                logEvent(userid, LogEvents.AccountRecovery_SQA_Incorrect, request.UserHostAddress + " - " + request.UserAgent, conn);
                                // Inform the user
                                error = "Incorrect secret answer!";
                            }
                            else
                            {
                                // Log the event
                                logEvent(userid, LogEvents.AccountRecovered_SQA, request.UserHostAddress + " - " + request.UserAgent, conn);
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
                            .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty);
                    }
                }
                if (pageElements["CONTENT"] == null || pageElements["CONTENT"].Length == 0)
                {
                    // Ask user for which user
                    pageElements["TITLE"] = "Account Recovery - Secret Question";
                    pageElements["CONTENT"] = Core.templates["basic_site_auth"]["recovery_qa_user"]
                        .Replace("%USERNAME%", HttpUtility.HtmlEncode(username ?? string.Empty))
                        .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty);
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
        private static void pageRecover_Email(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
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
                            // Log the event
                            logEvent(rec[0]["userid"], LogEvents.AccountRecovered_Email, request.UserHostAddress + " - " + request.UserAgent, conn);
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
                            .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", error) : string.Empty);
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
                        else if (int.Parse(info[0]["dispatches"]) >= int.Parse(Core.settings[SETTINGS_CATEGORY][SETTINGS_RECOVERY_MAX_EMAILS]))
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
                        .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", error) : string.Empty);
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
        private static void pageMyAccount(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
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
                    try
                    {
                        conn.Query_Execute(query.Remove(query.Length - 1, 1).Append(" WHERE userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'").ToString());
                        updatedSettings = true;
                        // Log event
                        logEvent(HttpContext.Current.User.Identity.Name, LogEvents.MyAccountUpdated, request.UserHostAddress + " - " + request.UserAgent, conn);
                    }
                    catch (DuplicateEntryException)
                    {
                        error = "E-mail already in-use!";
                    }
                    catch (Exception ex)
                    {
                        error = "Unknown error occurred whilst updating your account settings!" + ex.Message;
                    }
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
                .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : updatedSettings ? Core.templates[baseTemplateParent]["success"].Replace("%SUCCESS%", "Account settings successfully updated!") : string.Empty);
        }
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
        /// <summary>
        /// Displays logged events of the users actions; this can also be accessed by administrators for all users if the preprocessor
        /// BASIC_SITE_AUTH_ADMIN is defined.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageLog(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            string userid = null;
            // Check what account we'll be displaying
#if BASIC_SITE_AUTH_ADMIN
            if (request.QueryString["1"] != null)
            {
                // Check the current user is admin and the userid exists
                Result currUser = conn.Query_Read("SELECT g.access_admin FROM bsa_users AS u LEFT OUTER JOIN bsa_user_groups AS g ON g.groupid=u.groupid WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
                if (currUser.Rows.Count != 1 || !currUser[0]["access_admin"].Equals("1") || conn.Query_Count("SELECT COUNT('') FROM bsa_users WHERE userid='" + Utils.Escape(request.QueryString["1"]) + "'") != 1)
                    return;
                else
                    // User is admin - allow them to view another user's log records
                    userid = request.QueryString["1"];
            }
#endif
            // Check a userid has been set, else return
            if (userid == null)
                userid = HttpContext.Current.User.Identity.Name;
            // Get request parameters
            int page;
            if (request.QueryString["pg"] == null || !int.TryParse(request.QueryString["pg"], out page) || page < 1)
                page = 1;
            bool sortDateAsc = request.QueryString["sd"] != null && request.QueryString["sd"].Equals("a");
            bool sortDateDesc = request.QueryString["sd"] != null && request.QueryString["sd"].Equals("d");
            bool sortEventTypesAsc = request.QueryString["se"] != null && request.QueryString["se"].Equals("a");
            bool sortEventTypesDesc = request.QueryString["se"] != null && request.QueryString["se"].Equals("d");
            // Begin building the event log items
            StringBuilder eventItems = new StringBuilder();
            StringBuilder item;
            const int eventItemsPerPage = 8;
            foreach (ResultRow logEvent in conn.Query_Read("SELECT * FROM bsa_user_log WHERE userid='" + Utils.Escape(userid) + "' ORDER BY " + (sortEventTypesAsc ? "event_type ASC" : sortEventTypesDesc ? "event_type DESC" : sortDateAsc ? "date ASC" : "date DESC") + " LIMIT " + ((eventItemsPerPage * page) - eventItemsPerPage) + "," + eventItemsPerPage))
            {
                item = new StringBuilder(Core.templates["basic_site_auth"]["log_event"]);
                item
                    .Replace("%DATE%", HttpUtility.HtmlEncode(logEvent["date"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(logEvent["date"])) : "Unknown"))
                    .Replace("%DATE_RAW%", HttpUtility.HtmlEncode(logEvent["date"]))
                    .Replace("%ADDITIONAL_INFO%", HttpUtility.HtmlEncode(logEvent["additional_info"]));
                switch ((LogEvents)int.Parse(logEvent["event_type"]))
                {
                    case LogEvents.Registered:
                        item.Replace("%EVENT_TITLE%", "Registration")
                            .Replace("%EVENT_ICON%", "Content/Images/Log/Registered.png");
                        break;
                    case LogEvents.Login_Incorrect:
                        item.Replace("%EVENT_TITLE%", "Login: Incorrect")
                            .Replace("%EVENT_ICON%", "Content/Images/Log/Login_Incorrect.png");
                        break;
                    case LogEvents.Login_Authenticated:
                        item.Replace("%EVENT_TITLE%", "Login: Authenticated")
                            .Replace("%EVENT_ICON%", "Content/Images/Log/Login_Success.png");
                        break;
                    case LogEvents.AccountRecovery_SQA_Incorrect:
                        item.Replace("%EVENT_TITLE%", "Recovery: Secret Answer Incorrect")
                            .Replace("%EVENT_ICON%", "Content/Images/Log/Recovery_Secret_Incorrect.png");
                        break;
                    case LogEvents.AccountRecovered_Email:
                        item.Replace("%EVENT_TITLE%", "Recovery: Successful via E-mail")
                            .Replace("%EVENT_ICON%", "Content/Images/Log/Recovery_Email.png");
                        break;
                    case LogEvents.AccountRecovered_SQA:
                        item.Replace("%EVENT_TITLE%", "Recovery: Successful via Secret Answer")
                            .Replace("%EVENT_ICON%", "Content/Images/Log/Recovery_Secret.png");
                        break;
                    case LogEvents.MyAccountUpdated:
                        item.Replace("%EVENT_TITLE%", "Account Details Updated")
                            .Replace("%EVENT_ICON%", "Content/Images/Log/Account_Details.png");
                        break;
                }
                eventItems.Append(item.ToString());
                item = null;
            }
            // Check if no log events occurred - if so, inform the user
            if (eventItems.Length == 0)
                eventItems.Append(Core.templates["basic_site_auth"]["log_no_events"]);
            // Set content
            pageElements["TITLE"] = "Account Log";
            pageElements["CONTENT"] = Core.templates["basic_site_auth"]["log"]
                .Replace("%EVENTS%", eventItems.ToString())
                .Replace("%PAGE%", page.ToString())
                .Replace("%URL_PREVIOUS%", pageElements["URL"] + "/log/" + userid + "?sd=" + request.QueryString["sd"] + "&se=" + request.QueryString["se"] + "&pg=" + (page == 1 ? 1 : page - 1))
                .Replace("%URL_NEXT%", pageElements["URL"] + "/log/" + userid + "?sd=" + request.QueryString["sd"] + "&se=" + request.QueryString["se"] + "&pg=" + (int.MaxValue - 1 == page ? 1 : page + 1))
                .Replace("%SORT_DATE%", pageElements["URL"] + "/log/" + userid + "?sd=" + (sortDateAsc ? "d" : "a"))
                .Replace("%SORT_EVENT%", pageElements["URL"] + "/log/" + userid + "?se=" + (sortEventTypesAsc ? "d" : "a"))
                .Replace("%USERID%", userid != HttpContext.Current.User.Identity.Name ? userid : string.Empty);
        }
#if BASIC_SITE_AUTH_ADMIN
        /// <summary>
        /// Administration page, used for managing various core functions of the CMS as well as plugins.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private static void pageAdmin(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Check user has admin access
            Result authCheck = conn.Query_Read("SELECT g.access_admin FROM bsa_users AS u LEFT OUTER JOIN bsa_user_groups AS g ON g.groupid=u.groupid WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
            if (authCheck.Rows.Count != 1 || !authCheck[0]["access_admin"].Equals("1"))
                return;
            // Handle the request and build the content based on the selected page
            string pageid = request.QueryString["1"];
            if (pageid == null)
            {
                // No page requested, display welcome message
                pageElements["ADMIN_CONTENT"] = Core.templates["basic_site_auth"]["admin_welcome"];
                pageElements["ADMIN_TITLE"] = "Welcome!";
            }
            else
            {
                // Grab the classpath
                Result page = conn.Query_Read("SELECT classpath, method FROM bsa_admin_pages WHERE pageid='" + Utils.Escape(pageid) + "'");
                if (page.Rows.Count != 1)
                    return;
                // Set the admin URL
                pageElements["ADMIN_URL"] = pageElements["URL"] + "/admin/" + pageid;
                // Invoke the page handler
                if (!Misc.Plugins.invokeMethod(page[0]["classpath"], page[0]["method"], new object[] { conn, pageElements, request, response, baseTemplateParent }))
                    return;
                else if (pageElements["ADMIN_CONTENT"] == null || pageElements["ADMIN_CONTENT"].Length == 0)
                    return;
            }
            // Build menu
            StringBuilder menu = new StringBuilder();
            string currentHeader = null;
            foreach (ResultRow item in conn.Query_Read("SELECT pageid, title, category, menu_icon FROM bsa_admin_pages ORDER BY category ASC, title ASC"))
            {
                if(item["category"] != currentHeader)
                {
                    currentHeader = item["category"];
                    menu.Append(
                        Core.templates["basic_site_auth"]["admin_menu_header"]
                        .Replace("%TEXT%", HttpUtility.HtmlEncode(currentHeader))
                        );
                }
                menu.Append(
                    Core.templates["basic_site_auth"]["admin_menu_item"]
                    .Replace("%URL%", pageElements["URL"] + "/admin/" + item["pageid"])
                    .Replace("%ICON%", HttpUtility.UrlEncode(item["menu_icon"]))
                    .Replace("%TEXT%", HttpUtility.HtmlEncode(item["title"]))
                    );
            }
            // Set page
            pageElements["TITLE"] = "Admin";
            pageElements["CONTENT"] = Core.templates["basic_site_auth"]["admin"]
                .Replace("%MENU%", menu.ToString());
        }
#endif
        #endregion

        #region "Methods - Logging"
        /// <summary>
        /// Logs an event for an account.
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="eventType"></param>
        /// <param name="additionalInfo"></param>
        public static void logEvent(string userid, LogEvents eventType, string additionalInfo, Connector conn)
        {
            conn.Query_Execute("INSERT INTO bsa_user_log (userid, event_type, date, additional_info) VALUES('" + Utils.Escape(userid) + "', '" + (int)eventType + "', NOW(), " + (additionalInfo != null ? "'" + Utils.Escape(additionalInfo) + "'" : "NULL") + ")");
        }
        #endregion

        #region "Methods - Admin System"
#if BASIC_SITE_AUTH_ADMIN
        public static void adminPage_Install(string classpath, string method, string title, string category, string menuIcon, Connector conn)
        {
            conn.Query_Execute("INSERT INTO bsa_admin_pages (classpath, method, title, category, menu_icon) VALUES('" + Utils.Escape(classpath) + "', '" + Utils.Escape(method) + "', '" + Utils.Escape(title) + "', '" + Utils.Escape(category) + "', '" + Utils.Escape(menuIcon) + "')");
        }
        public static void adminPage_Uninstall(string classpath, string method, Connector conn)
        {
            conn.Query_Execute("DELETE FROM bsa_admin_pages WHERE classpath='" + Utils.Escape(classpath) + "' AND method='" + Utils.Escape(method) + "'");
        }
#endif
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
        public static string generateHash(string data, string salt1, string salt2)
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
        public static string randomText(int length)
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
        /// Validates the characters of a username are allowed.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string validUsernameChars(string text)
        {
            string strictChars = Core.settings[SETTINGS_CATEGORY][SETTINGS_USERNAME_STRICT_CHARS];
            // Check if strict mode is enabled, else we'll allow any char
            if (Core.settings[SETTINGS_CATEGORY].getBool(SETTINGS_USERNAME_STRICT))
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
        /// <summary>
        /// Loads the two salts required for the hasher; if the salts do not exist, they're generated
        /// and stored inside of Salts.xml within the base of the plugins' directory.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        private static void initSalts(string pluginid, Connector conn)
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
        }
        /// <summary>
        /// Checks each char in the specified string is alpha-numeric or an underscroll.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool validateAlphaNumericUnderscroll(string text)
        {
            foreach (char c in text.ToCharArray())
            {
                if (!(c >= 48 && c <= 57) && !(c >= 65 && c <= 90) && !(c >= 97 && c <= 122) && c != 95)
                    return false;
            }
            return true;
        }
        #endregion
    }
}
        /* This below gives a good example of how to make admin pages for the BasicSiteAuth panel; you should use preprocessor directives
 * in-case BasicSiteAuth is not installed or the panel is disabled, this will cause ASP.NET to not compile the contained code to avoid runtime errors. This
 * means your plugin could support multiple authentication or admin systems and be independent of other plugins.
 */
#if BASIC_SITE_AUTH_ADMIN
namespace UberCMS.BasicSiteAuth
{
    public static class Admin
    {
        public static void pageUsers(Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            if (request.QueryString["2"] != null)
            {
                // Editing a user
                string error = null;
                bool updatedAccount = false;
                // Grab the user's info, bans and available user groups
                Result user = conn.Query_Read("SELECT * FROM bsa_users WHERE userid='" + Utils.Escape(request.QueryString["2"]) + "'");
                if (user.Rows.Count != 1) return;
                Result bans = conn.Query_Read("SELECT b.*, u.username FROM bsa_user_bans AS b LEFT OUTER JOIN bsa_users AS u ON u.userid=b.banner_userid ORDER BY datetime DESC");
                Result userGroups = conn.Query_Read("SELECT groupid, title FROM bsa_user_groups ORDER BY access_login ASC, access_changeaccount ASC, access_media_create ASC, access_media_edit ASC, access_media_delete ASC, access_media_publish ASC, access_admin ASC, title ASC");
                string dban = request.QueryString["dban"];
                // Check for deleting a ban
                if (dban != null)
                {
                    conn.Query_Execute("DELETE FROM bsa_user_bans WHERE banid='" + Utils.Escape(dban) + "'");
                    conn.Disconnect();
                    response.Redirect(pageElements["ADMIN_URL"] + "/" + user[0]["userid"], true);
                }
                // Check for postback of banning the user
                string ban = request.QueryString["ban"];
                string banCustom = request.QueryString["ban_custom"];
                string banReason = request.QueryString["ban_reason"];
                if (ban != null || banCustom != null)
                {
                    int banAmount = 0;
                    if (ban != null)
                    {
                        if (ban.Equals("Permanent"))
                            banAmount = 0;
                        else if (ban.Equals("1 Month"))
                            banAmount = 2628000;
                        else if (ban.Equals("1 Week"))
                            banAmount = 604800;
                        else if (ban.Equals("3 Days"))
                            banAmount = 259200;
                        else if (ban.Equals("1 Day"))
                            banAmount = 86400;
                        else
                            error = "Invalid ban period!";
                    }
                    else
                    {
                        if (banCustom != null && !int.TryParse(banCustom, out banAmount))
                            error = "Invalid ban period, not numeric!";
                        else if (banAmount < 0)
                            error = "Ban period cannot be less than zero!";
                    }
                    if(error == null)
                    {
                        // Get the time at which the user will be unbanned
                        DateTime dt = DateTime.Now.AddSeconds(-banAmount);
                        // Insert the record
                        conn.Query_Execute("INSERT INTO bsa_user_bans (userid, reason, unban_date, datetime, banner_userid) VALUES('" + Utils.Escape(user[0]["userid"]) + "', '" + Utils.Escape(banReason) + "', " + (banAmount == 0 ? "NULL" : "'" + Utils.Escape(dt.ToString("yyyy-MM-dd HH:mm:ss")) + "'") + ", NOW(), '" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "')");
                        // Refresh the page
                        conn.Disconnect();
                        response.Redirect(pageElements["ADMIN_URL"] + "/" + user[0]["userid"], true);
                    }
                }
                // Check for postback of editing the user
                string username = request.Form["username"];
                string password = request.Form["password"];
                string email = request.Form["email"];
                string secretQuestion = request.Form["secret_question"];
                string secretAnswer = request.Form["secret_answer"];
                string groupid = request.Form["groupid"];
                if (username != null && password != null && email != null && secretQuestion != null && secretAnswer != null && groupid != null)
                {
                    if (username.Length < Plugins.BasicSiteAuth.USERNAME_MIN || username.Length > Plugins.BasicSiteAuth.USERNAME_MAX)
                        error = "Username must be " + Plugins.BasicSiteAuth.USERNAME_MIN + " to " + Plugins.BasicSiteAuth.USERNAME_MAX + " characters in length!";
                    else if ((error = Plugins.BasicSiteAuth.validUsernameChars(username)) != null)
                        ;
                    else if (!Plugins.BasicSiteAuth.validEmail(email))
                        error = "Invalid e-mail!";
                    else if (password.Length != 0 && (password.Length < Plugins.BasicSiteAuth.PASSWORD_MIN || password.Length > Plugins.BasicSiteAuth.PASSWORD_MAX))
                        error = "Password must be " + Plugins.BasicSiteAuth.PASSWORD_MIN + " to " + Plugins.BasicSiteAuth.PASSWORD_MAX + " characters in length!";
                    else if (secretQuestion.Length < Plugins.BasicSiteAuth.SECRET_QUESTION_MIN || secretQuestion.Length > Plugins.BasicSiteAuth.SECRET_QUESTION_MAX)
                        error = "Secret question must be " + Plugins.BasicSiteAuth.SECRET_QUESTION_MIN + " to " + Plugins.BasicSiteAuth.SECRET_QUESTION_MAX + " characters in length!";
                    else if (secretAnswer.Length < Plugins.BasicSiteAuth.SECRET_ANSWER_MIN || secretAnswer.Length > Plugins.BasicSiteAuth.SECRET_ANSWER_MAX)
                        error = "Secret answer must be " + Plugins.BasicSiteAuth.SECRET_ANSWER_MIN + " to " + Plugins.BasicSiteAuth.SECRET_ANSWER_MAX + " characters in length!";
                    else
                    {
                        // Ensure the groupid is valid
                        bool groupFound = false;
                        foreach (ResultRow group in userGroups) if (group["groupid"] == groupid) groupFound = true;
                        if (!groupFound)
                            error = "Invalid group!";
                        else
                        {
                            // Attempt to update the user's details
                            try
                            {
                                conn.Query_Execute("UPDATE bsa_users SET username='" + Utils.Escape(username) + "', email='" + Utils.Escape(email) + "', " + (password.Length > 0 ? "password='" + Utils.Escape(Plugins.BasicSiteAuth.generateHash(password, Plugins.BasicSiteAuth.salt1, Plugins.BasicSiteAuth.salt2)) + "', " : string.Empty) + "secret_question='" + Utils.Escape(secretQuestion) + "', secret_answer='" + Utils.Escape(secretAnswer) + "', groupid='" + Utils.Escape(groupid) + "' WHERE userid='" + Utils.Escape(user[0]["userid"]) + "'");
                                updatedAccount = true;
                            }
                            catch (DuplicateEntryException ex)
                            {
                                if (ex.Column == "email")
                                    error = "E-mail already in-use by another user!";
                                else if (ex.Column == "username")
                                    error = "Username already in-use by another user!";
                                else
                                    error = "Value for " + ex.Column + " is already in-use by another user!";
                            }
                            catch (Exception ex)
                            {
                                error = "Failed to update user settings - " + ex.Message;
                            }
                        }
                    }
                }
                // Build user groups
                StringBuilder groups = new StringBuilder();
                foreach (ResultRow group in userGroups)
                    groups.Append("<option value=\"").Append(group["groupid"]).Append("\"").Append(group["groupid"] == groupid || groupid == null && group["groupid"] == user[0]["groupid"] ? " selected=\"selected\"" : string.Empty).Append(">" + HttpUtility.HtmlEncode(group["title"]) +"</option>");
                // Build bans
                StringBuilder bansHistory = new StringBuilder();
                if (bans.Rows.Count == 0)
                    bansHistory.Append("<p>No bans.</p>");
                else
                    foreach (ResultRow banItem in bans)
                        bansHistory.Append(
                            Core.templates["basic_site_auth"]["admin_users_edit_ban"]
                            .Replace("%BANID%", HttpUtility.HtmlEncode(banItem["banid"]))
                            .Replace("%DATETIME%", HttpUtility.HtmlEncode(Misc.Plugins.getTimeString(DateTime.Parse(banItem["datetime"]))))
                            .Replace("%UNBAN_DATE%", HttpUtility.HtmlEncode(banItem["unban_date"].Length > 0 ? banItem["unban_date"] : "never"))
                            .Replace("%USERNAME%", HttpUtility.HtmlEncode(banItem["username"]))
                            .Replace("%REASON%", HttpUtility.HtmlEncode(banItem["reason"]))
                            );
                // Display edit form
                pageElements["ADMIN_CONTENT"] = Core.templates["basic_site_auth"]["admin_users_edit"]
                    .Replace("%USERNAME%", HttpUtility.HtmlEncode(username ?? user[0]["username"]))
                    .Replace("%EMAIL%", HttpUtility.HtmlEncode(email ?? user[0]["email"]))
                    .Replace("%SECRET_QUESTION%", HttpUtility.HtmlEncode(secretQuestion ?? user[0]["secret_question"]))
                    .Replace("%SECRET_ANSWER%", HttpUtility.HtmlEncode(secretAnswer ?? user[0]["secret_answer"]))
                    .Replace("%GROUPID%", groups.ToString())
                    .Replace("%BAN_REASON%", HttpUtility.HtmlEncode(banReason))
                    .Replace("%BAN_CUSTOM%", HttpUtility.HtmlEncode(ban))
                    .Replace("%BANS%", bansHistory.ToString())
                    .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : updatedAccount ? Core.templates[baseTemplateParent]["success"].Replace("%SUCCESS%", "Successfully updated account settings!") : string.Empty)
                    .Replace("%USERID%", user[0]["userid"])
                    ;
                pageElements["ADMIN_TITLE"] = "Authentication - Users - Editing '" + HttpUtility.HtmlEncode(user[0]["username"]) + "'";
            }
            else
            {
                string error = null;
                // Check for postback
                string username = request.Form["username"];
                if(username != null)
                {
                    if (username.Length < Plugins.BasicSiteAuth.USERNAME_MIN || username.Length > Plugins.BasicSiteAuth.USERNAME_MAX || Plugins.BasicSiteAuth.validUsernameChars(username) != null)
                        error = "Invalid username!";
                    else
                    {
                        // Fetch the userid
                        Result userid = conn.Query_Read("SELECT userid FROM bsa_users WHERE username LIKE '" + Utils.Escape(username.Replace("%", "")) + "'");
                        if (userid.Rows.Count != 1) error = "User not found!";
                        else
                        {
                            conn.Disconnect();
                            response.Redirect(pageElements["ADMIN_URL"] + "/" + userid[0]["userid"], true);
                        }
                    }
                }
                // Display user-search form
                pageElements["ADMIN_CONTENT"] = Core.templates["basic_site_auth"]["admin_users"]
                    .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty)
                    .Replace("%USERNAME%", HttpUtility.HtmlEncode(username));
                pageElements["ADMIN_TITLE"] = "Authentication - Users";
            }
        }
        public static void pageUserGroups(Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            string error = null;
            bool updatedSettings = false;
            // Check for transfer of users
            string transferGroupID = request.QueryString["transfer"];
            if (transferGroupID != null)
            {
                // -- Transfer users to another group
                // Grab the title of the origin group - this will also help to validate it exists too, else we'll 404
                Result groupOrigin = conn.Query_Read("SELECT title FROM bsa_user_groups WHERE groupid='" + Utils.Escape(transferGroupID) + "'");
                if (groupOrigin.Rows.Count != 1) return; // 404 - the group does not exist
                string newTransferGroupID = request.QueryString["transfer_b"]; // The destination group ID
                if (newTransferGroupID != null)
                {
                    // Validate the group exists
                    if (conn.Query_Count("SELECT COUNT('') FROM bsa_user_groups WHERE groupid='" + Utils.Escape(newTransferGroupID) + "'") != 1)
                        error = "Destination group does not exist!";
                    else
                    {
                        // Transfer all the users http://memegenerator.net/instance/23587059
                        conn.Query_Execute("UPDATE bsa_users SET groupid='" + Utils.Escape(newTransferGroupID) + "' WHERE groupid='" + Utils.Escape(transferGroupID) + "'");
                        conn.Disconnect();
                        response.Redirect(pageElements["ADMIN_URL"]);
                    }
                }
                // Build a list of the current groups
                StringBuilder currentGroups = new StringBuilder();
                foreach (ResultRow group in conn.Query_Read("SELECT groupid, title FROM bsa_user_groups WHERE groupid != '" + Utils.Escape(transferGroupID) + "' ORDER BY title ASC"))
                    currentGroups.Append("<option value=\"").Append(group["groupid"]).Append("\">").Append(group["title"]).Append("</option>");
                // Display form
                pageElements["ADMIN_CONTENT"] =
                    Core.templates["basic_site_auth"]["admin_user_groupstransfer"]
                    .Replace("%GROUPID%", HttpUtility.HtmlEncode(transferGroupID))
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(groupOrigin[0]["title"]))
                    .Replace("%GROUPS%", currentGroups.ToString())
                    .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty)
                    ;
            }
            else
            {
                // -- List all user groups
                // Check for postback - delete a group
                string delete = request.QueryString["delete"];
                if (delete != null)
                {
                    if (conn.Query_Count("SELECT COUNT('') FROM bsa_users WHERE groupid='" + Utils.Escape(delete) + "'") > 0)
                        error = "Cannot delete group - the group contains users, transfer them to another group first!";
                    else
                    {
                        conn.Query_Execute("DELETE FROM bsa_user_groups WHERE groupid='" + Utils.Escape(delete) + "'");
                        conn.Disconnect();
                        response.Redirect(pageElements["ADMIN_URL"], true);
                    }
                }
                // Check for postback - added group
                string groupAddTitle = request.Form["group_add_title"];
                if (groupAddTitle != null)
                {
                    if (groupAddTitle.Length < Plugins.BasicSiteAuth.USER_GROUP_TITLE_MIN || groupAddTitle.Length > Plugins.BasicSiteAuth.USER_GROUP_TITLE_MAX)
                        error = "Group title must be between " + Plugins.BasicSiteAuth.USER_GROUP_TITLE_MIN + " to " + Plugins.BasicSiteAuth.USER_GROUP_TITLE_MAX + " characters in length!";
                    else
                        conn.Query_Execute("INSERT INTO bsa_user_groups (title) VALUES('" + Utils.Escape(groupAddTitle) + "')");
                }
                // Grab the current permissions
                const string dbPermissionsQuery = "SELECT * FROM bsa_user_groups ORDER BY title ASC";
                Result dbPermissions = conn.Query_Read(dbPermissionsQuery);
                // Check for postback - permissions
                string groupid, column, value;
                string[] parts;
                Dictionary<string, Dictionary<string, string>> groupRowsUpdate = new Dictionary<string, Dictionary<string, string>>();
                for (int i = 0; i < request.Form.Count; i++)
                {
                    parts = request.Form.Keys[i].Split('$');
                    if (parts.Length == 2 && parts[0].StartsWith("group_"))
                    {
                        groupid = parts[0].Substring(6);
                        column = parts[1];
                        value = request.Form[i];
                        if (!groupRowsUpdate.ContainsKey(groupid))
                            groupRowsUpdate.Add(groupid, new Dictionary<string, string>());
                        groupRowsUpdate[groupid].Add(column, value);
                    }
                }
                if (groupRowsUpdate.Count > 0)
                {
                    // Postback made - generate query by going through each permissions row and checking for a state (or lack of state) change
                    StringBuilder queries = new StringBuilder();
                    StringBuilder query;
                    const string queryStart = "UPDATE bsa_user_groups SET ";
                    string currGroupId;
                    foreach (ResultRow dbPermissionsRow in dbPermissions)
                    {
                        currGroupId = dbPermissionsRow["groupid"];
                        // Check if this group has been updated at all
                        if (groupRowsUpdate.ContainsKey(currGroupId))
                        {
                            query = new StringBuilder(queryStart);
                            foreach (KeyValuePair<string, object> groupColumn in dbPermissionsRow.Columns)
                            {
                                if (groupColumn.Key == "title")
                                {
                                    // Check for change
                                    if (groupRowsUpdate[currGroupId].ContainsKey("title") && groupRowsUpdate[currGroupId]["title"] != groupColumn.Value.ToString())
                                        // Append the changed title
                                        query.Append("title='").Append(Utils.Escape(groupRowsUpdate[currGroupId]["title"])).Append("',");
                                }
                                else if (groupColumn.Key == "groupid")
                                    ; // We currently do nothing with the groupid
                                else if ((groupColumn.Value.ToString().Equals("1") && !groupRowsUpdate[currGroupId].ContainsKey(groupColumn.Key)) || (!groupColumn.Value.ToString().Equals("1") && groupRowsUpdate[currGroupId].ContainsKey(groupColumn.Key)))
                                    query.Append(groupColumn.Key).Append("='").Append(Utils.Escape(groupRowsUpdate[currGroupId].ContainsKey(groupColumn.Key) ? "1" : "0")).Append("',");
                            }
                            // Check if to append anything
                            if (query.Length != queryStart.Length)
                                queries.Append(query.Remove(query.Length - 1, 1).Append(" WHERE groupid='").Append(Utils.Escape(currGroupId)).Append("';").ToString());
                        }
                    }
                    if (queries.Length > 0)
                    {
                        // Attempt to execute the query
                        try
                        {
                            conn.Query_Execute(queries.ToString());
                            updatedSettings = true;
                            // Refetch the permissions - probably the most efficient way
                            dbPermissions = conn.Query_Read(dbPermissionsQuery);
                        }
                        catch (Exception ex)
                        {
                            error = "Failed to update group settings - " + ex.Message;
                        }
                    }
                    else
                        updatedSettings = true;
                }
                // Fetch labels for permissions
                Result permissionLabels = conn.Query_Read("SELECT column_title, title FROM bsa_user_groups_labels");
                // List each group
                StringBuilder groups = new StringBuilder();
                StringBuilder groupPermissions;
                foreach (ResultRow group in dbPermissions)
                {
                    // Add each permission category
                    groupPermissions = new StringBuilder();
                    foreach (KeyValuePair<string, object> permission in group.Columns)
                        if (permission.Key != "groupid" && permission.Key != "title")
                        {
                            groupPermissions.Append(
                                Core.templates["basic_site_auth"]["admin_user_group_perm"]
                                .Replace("%TITLE%", HttpUtility.HtmlEncode(pageUserGroupsGetPermissionTitle(permission.Key, permissionLabels, permission.Key)))
                                .Replace("%CHECKED%", permission.Value.ToString().Equals("1") ? "checked" : string.Empty)
                                );
                        }
                    // Add the group to the groups
                    groups.Append(
                        Core.templates["basic_site_auth"]["admin_user_group"]
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(group["title"]))
                        .Replace("%PERMISSIONS%", groupPermissions.ToString())
                        .Replace("%GROUPID%", HttpUtility.HtmlEncode(group["groupid"]))
                        );
                }
                pageElements["ADMIN_CONTENT"] = Core.templates["basic_site_auth"]["admin_user_groups"]
                    .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : updatedSettings ? Core.templates[baseTemplateParent]["success"].Replace("%SUCCESS%", "Successfully updated settings!") : string.Empty)
                    .Replace("%GROUP_ADD_TITLE%", HttpUtility.HtmlEncode(groupAddTitle))
                    .Replace("%ITEMS%", groups.ToString())
                    ;
            }
            pageElements["ADMIN_TITLE"] = "Authentication - User Groups";
        }
        /// <summary>
        /// Used to get the permission title of a column in bsa_user_groups.
        /// </summary>
        private static string pageUserGroupsGetPermissionTitle(string defaultTitle, Result labels, string columnName)
        {
            foreach (ResultRow label in labels)
                if (label["column_title"].Equals(columnName)) return label["title"];
            return defaultTitle;
        }
        public static void pageUserLogs(Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            string error = null;
            // Check if a username has been posted
            string username = request.Form["log_username"];
            if (username != null)
            {
                if (username.Length < Plugins.BasicSiteAuth.USERNAME_MIN || username.Length > Plugins.BasicSiteAuth.USERNAME_MAX || Plugins.BasicSiteAuth.validUsernameChars(username) != null)
                    error = "Invalid username!";
                else
                {
                    // Fetch the userid
                    Result userid = conn.Query_Read("SELECT userid FROM bsa_users WHERE username LIKE '" + Utils.Escape(username.Replace("%", "")) + "'");
                    if (userid.Rows.Count != 1) error = "User not found!";
                    else
                    {
                        conn.Disconnect();
                        response.Redirect(pageElements["URL"] + "/log/" + userid[0]["userid"], true);
                    }
                }
            }
            // Display form
            pageElements["ADMIN_TITLE"] = "Authentication - User Logs";
            pageElements["ADMIN_CONTENT"] = Core.templates["basic_site_auth"]["admin_user_logs"]
                .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty)
                .Replace("%USERNAME%", username ?? string.Empty);
        }
        public static void pageEmailQueue(Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Check for e-mail deletion
            string deleteEmailID = request.QueryString["delete"];
            if (deleteEmailID != null)
            {
                conn.Query_Execute("DELETE FROM email_queue WHERE emailid='" + Utils.Escape(deleteEmailID) + "'");
                conn.Disconnect();
                response.Redirect(pageElements["ADMIN_URL"], true);
            }
            // Grab statistics about the number of e-mails pending
            ResultRow queueStats = conn.Query_Read("SELECT (SELECT COUNT('') FROM email_queue) AS count, (SELECT COUNT(DISTINCT email) FROM email_queue) AS unique_count")[0];
            // Generate a list of pending e-mails at the top of the queue
            StringBuilder pending = new StringBuilder();
            foreach (ResultRow email in conn.Query_Read("SELECT * FROM email_queue ORDER BY emailid ASC LIMIT 10"))
                pending.Append(
                    Core.templates["basic_site_auth"]["admin_emailqueue_item"]
                    .Replace("%EMAILID%", HttpUtility.HtmlEncode(email["emailid"]))
                    .Replace("%EMAIL%", HttpUtility.HtmlEncode(email["email"]))
                    .Replace("%SUBJECT%", HttpUtility.HtmlEncode(email["subject"]))
                    );
            if (pending.Length == 0) pending.Append("No e-mails in the queue!");
            // Display page
            pageElements["ADMIN_CONTENT"] =
                Core.templates["basic_site_auth"]["admin_emailqueue"]
                .Replace("%COUNT%", HttpUtility.HtmlEncode(queueStats["count"]))
                .Replace("%UNIQUE_COUNT%", HttpUtility.HtmlEncode(queueStats["unique_count"]))
                .Replace("%ERRORS%", HttpUtility.HtmlEncode(Core.emailQueue.mailErrors.ToString()))
                .Replace("%THREAD_STATUS%", HttpUtility.HtmlEncode(Core.emailQueue.cyclerThread != null ? Core.emailQueue.cyclerThread.ThreadState.ToString() : "Not operational - critical failure or undefined mail settings."))
                .Replace("%EMAILS%", pending.ToString())
                ;
            pageElements["ADMIN_TITLE"] = "Core - E-mail Queue";
        }
        public static void pageSettings(Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            string error = null;
            bool successfullyUpdated = false;
            // Check if the user has requested to delete a setting
            string deleteCategory = request.QueryString["delete_category"];
            string deleteKey = request.QueryString["delete_key"];
            if (deleteCategory != null && deleteKey != null)
            {
                conn.Query_Execute("DELETE FROM settings WHERE category='" + Utils.Escape(deleteCategory) + "' AND keyname='" + Utils.Escape(deleteKey) + "'");
                response.Redirect(pageElements["ADMIN_URL"]);
            }
            // Check if the user has posted updated settings
            Dictionary<string[], string> updatedSettings = new Dictionary<string[], string>();
            string category, key, value;
            string[] rawKey;
            for (int i = 0; i < request.Form.Count; i++)
            {
                rawKey = request.Form.Keys[i].Split('$');
                value = request.Form[i];
                if (rawKey.Length == 2 && rawKey[0].StartsWith("setting_") && rawKey[0].Length > 10 && Plugins.BasicSiteAuth.validateAlphaNumericUnderscroll(key = rawKey[0].Substring(8)))
                    updatedSettings.Add(new string[]{key, rawKey[1]}, value);
            }
            if (updatedSettings.Count > 0)
            {
                StringBuilder updateQuery = new StringBuilder();
                foreach (KeyValuePair<string[], string> setting in updatedSettings)
                    updateQuery.Append("UPDATE settings SET value='" + Utils.Escape(setting.Value) + "' WHERE category='" + Utils.Escape(setting.Key[0]) + "' AND keyname='" + Utils.Escape(setting.Key[1]) + "';");
                try
                {
                    conn.Query_Execute(updateQuery.ToString());
                    successfullyUpdated = true;
                }
                catch (Exception ex)
                {
                    error = "Failed to update settings - " + ex.Message + "!";
                }
            }
            // Display the settings
            StringBuilder settingItems = new StringBuilder();
            category = null;
            foreach (ResultRow setting in conn.Query_Read("SELECT category, keyname, value, description FROM settings ORDER BY category ASC, keyname ASC"))
            {
                if (setting["category"] != category)
                {
                    category = setting["category"];
                    settingItems.Append(
                        Core.templates["basic_site_auth"]["admin_settings_category"]
                        .Replace("%CATEGORY%", HttpUtility.HtmlEncode(category))
                        );
                }
                settingItems.Append(
                    Core.templates["basic_site_auth"]["admin_settings_item"]
                    .Replace("%KEYNAME%", HttpUtility.HtmlEncode(setting["keyname"]))
                    .Replace("%KEYNAME_URL%", HttpUtility.UrlEncode(setting["keyname"]))
                    .Replace("%CATEGORY%", HttpUtility.HtmlEncode(setting["category"]))
                    .Replace("%CATEGORY_URL%", HttpUtility.UrlEncode(setting["category"]))
                    .Replace("%VALUE%", HttpUtility.HtmlEncode(request.Form["setting_" + setting["keyname"]] ?? setting["value"]))
                    .Replace("%DESCRIPTION%", HttpUtility.HtmlEncode(setting["description"]))
                    );
            }
            pageElements["ADMIN_CONTENT"] =
                Core.templates["basic_site_auth"]["admin_settings"]
                .Replace("%ITEMS%", settingItems.ToString())
                .Replace("%SUCCESS%", successfullyUpdated ? Core.templates[baseTemplateParent]["success"].Replace("%SUCCESS%", "Successfully saved settings!") : string.Empty)
                .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty);
            pageElements["ADMIN_TITLE"] = "Core - Settings";
        }
        public static void pagePlugins(Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            string error = null;
            // Check for action
            string pluginid = request.QueryString["pluginid"];
            string action = request.QueryString["action"];
            // Process action
            if (action != null)
            {
                // Check if a pluginid has been specified, if so load it
                Result pluginidData = null;
                if(pluginid != null)
                    pluginidData = conn.Query_Read("SELECT * FROM plugins WHERE pluginid='" + Utils.Escape(pluginid) + "'");
                if (pluginidData != null && pluginidData.Rows.Count != 1)
                    error = "Invalid pluginid specified!";
                else if (pluginidData != null && action == "enable")
                {
                    if ((Plugins.Base.State)Enum.Parse(typeof(Plugins.Base.State), pluginidData[0]["state"]) == Plugins.Base.State.Enabled)
                        error = "Plugin is already enabled.";
                    else
                    {
                        error = Misc.Plugins.enable(pluginidData[0]["pluginid"], conn);
                        if (error == null) // Operation successful
                        {
                            conn.Disconnect();
                            response.Redirect(pageElements["ADMIN_URL"], true);
                        }
                    }
                }
                else if (pluginidData != null  && action == "disable")
                {
                    if ((Plugins.Base.State)Enum.Parse(typeof(Plugins.Base.State), pluginidData[0]["state"]) == Plugins.Base.State.Disabled)
                        error = "Plugin is already disabled.";
                    else
                    {
                        error = Misc.Plugins.disable(pluginidData[0]["pluginid"], conn);
                        if (error == null) // Operation successful
                        {
                            conn.Disconnect();
                            response.Redirect(pageElements["ADMIN_URL"], true);
                        }
                    }
                }
                else if (pluginidData != null && action == "uninstall")
                {
                    // Get the user to confirm the uninstallation
                    string confirm = request.QueryString["confirm"];
                    if (confirm != null)
                    {
                        // Uninstall the plugin
                        error = Misc.Plugins.uninstall(pluginidData[0]["pluginid"], true, conn);
                        if (error == null) // Operation successful
                        {
                            conn.Disconnect();
                            response.Redirect(pageElements["ADMIN_URL"], true);
                        }
                    }
                    // Display confirmation form
                    pageElements["ADMIN_CONTENT"] = Core.templates["basic_site_auth"]["admin_plugin_uninstall"]
                        .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty)
                        .Replace("%PLUGINID%", HttpUtility.HtmlEncode(pluginidData[0]["pluginid"]))
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(pluginidData[0]["title"]));
                    pageElements["ADMIN_TITLE"] = "Core - Plugins - Uninstall";
                    return;
                }
                else if (action == "upload")
                {
                    HttpPostedFile upload = request.Files["plugin_upload"];
                    if (upload == null || upload.ContentLength == 0 || (upload.ContentType != "application/zip" && upload.ContentType != "application/x-zip" && upload.ContentType != "application/x-zip-compressed"))
                        error = "Invalid plugin zip archive specified!";
                    else
                    {
                        // Save the zip
                        string zipPath = Core.basePath + "\\Cache\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + new Random().Next(1, int.MaxValue) + ".zip";
                        upload.SaveAs(zipPath);
                        // Attempt to install it
                        error = Misc.Plugins.install(zipPath, true, conn);
                        if(error != null)
                            try
                            {
                                File.Delete(zipPath);
                            }
                            catch { }
                    }
                }
                else
                    error = "Unhandled action!";
            }
            // List the plugins
            StringBuilder pluginsList = new StringBuilder();
            string state;
            string buttons;
            foreach (ResultRow plugin in conn.Query_Read("SELECT * FROM plugins ORDER BY title ASC"))
            {
                switch((Plugins.Base.State)Enum.Parse(typeof(Plugins.Base.State), plugin["state"]))
                {
                    case Plugins.Base.State.Disabled:
                        state = "Disabled";
                        buttons = Core.templates["basic_site_auth"]["admin_plugin_buttdisabled"].Replace("%PLUGINID%", HttpUtility.HtmlEncode(plugin["pluginid"]));;
                        break;
                    case Plugins.Base.State.Enabled:
                        state = "Enabled";
                        buttons = Core.templates["basic_site_auth"]["admin_plugin_buttenabled"].Replace("%PLUGINID%", HttpUtility.HtmlEncode(plugin["pluginid"]));
                        break;
                    default:
                        state = "Unknown";
                        buttons = string.Empty;
                        break;
                }
                pluginsList.Append(
                    Core.templates["basic_site_auth"]["admin_plugins_item"]
                    .Replace("%PLUGINID%", HttpUtility.HtmlEncode(plugin["pluginid"]))
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(plugin["title"]))
                    .Replace("%STATE%", HttpUtility.HtmlEncode(state))
                    .Replace("%BUTTONS%", buttons)
                    .Replace("%DIRECTORY%", HttpUtility.HtmlEncode(plugin["directory"]))
                    .Replace("%PRIORITY%", HttpUtility.HtmlEncode(plugin["invoke_order"]))
                    );
            }
            // Display content
            pageElements["ADMIN_CONTENT"] = Core.templates["basic_site_auth"]["admin_plugins"]
                .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty)
                .Replace("%ITEMS%", pluginsList.ToString())
                ;
            pageElements["ADMIN_TITLE"] = "Core - Plugins";
        }
    }
}
#endif