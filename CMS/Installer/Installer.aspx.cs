using System;
using System.Text;
using System.Collections.Generic;
using System.Web;
using UberLib.Connector;
using UberLib.Connector.Connectors;
using System.Reflection;
using System.Net.Mail;
using System.Xml;
using System.IO;

public partial class Installer_Installer : System.Web.UI.Page
{
    public const string TEMPLATES_KEY = "installer";
    public static UberCMS.Misc.HtmlTemplates templates = null;
    public static DbSettings dbSettings = null;

    protected void Page_Load(object sender, EventArgs e)
    {
        // Check the templates are loaded, else load them
#if !DEBUG
        if (templates == null)
        {
            templates = new UberCMS.Misc.HtmlTemplates();
            templates.reloadFromDisk(AppDomain.CurrentDomain.BaseDirectory + "\\Installer\\Templates");
        }
#else
        if (templates == null) templates = new UberCMS.Misc.HtmlTemplates();
        templates.reloadFromDisk(AppDomain.CurrentDomain.BaseDirectory + "\\Installer\\Templates");
#endif
        // Check if any DB settings have been loaded, else load them
        if (dbSettings == null && File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\CMS.config"))
        {
            XmlDocument settings = new XmlDocument();
            settings.LoadXml(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "\\CMS.config"));
            dbSettings = new DbSettings(
                settings["settings"]["db"]["host"].InnerText,
                settings["settings"]["db"]["database"].InnerText,
                settings["settings"]["db"]["username"].InnerText,
                settings["settings"]["db"]["password"].InnerText,
                int.Parse(settings["settings"]["db"]["port"].InnerText)
                );
            settings = null;
        }
        // Invoke the correct page to handle the request
        UberCMS.Misc.PageElements pageElements = new UberCMS.Misc.PageElements();
        pageElements["URL"] = ResolveUrl("/install");
        StringBuilder content = new StringBuilder();
#if !INSTALLED
        switch (Request.QueryString["1"])
        {
            case "home":
            case null:
                pageHome(ref pageElements, Request, Response, ref content);
                break;
            case "setup":
                pageSetup(ref pageElements, Request, Response, ref content);
                break;
            case "install":
                pageInstall(ref pageElements, Request, Response, ref content);
                break;
        }
#else
        pageFinish(ref pageElements, Request, Response, ref content);
#endif
        // Build and display the final output
        pageElements["CONTENT"] = content.ToString();
        StringBuilder template = new StringBuilder(templates[TEMPLATES_KEY]["template"]);
        pageElements.replaceElements(ref template, 0, 5);
        Response.Write(template.ToString());
    }
    public static void pageHome(ref UberCMS.Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref StringBuilder content)
    {
        content.Append(
            templates[TEMPLATES_KEY]["home"]
            );
        pageElements["TITLE"] = "Welcome!";
    }
    /// <summary>
    /// Used to retrieve and validate the database settings to be used by the installer process; this section is skipped
    /// if the database settings are specified in the web.config already.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <param name="content"></param>
    public static void pageSetup(ref UberCMS.Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref StringBuilder content)
    {
#if INSTALLED
        response.Redirect(baseURL + "/finish");
#else
        // Check the database is not already installed
        if (dbSettings != null)
            // -- Skip to the installer page
            response.Redirect(pageElements["URL"] + "/installer");
        string error = null;
        // Check for postback
        string dbHost = request.Form["db_host"];
        string dbDatabase = request.Form["db_database"];
        string dbUsername = request.Form["db_username"];
        string dbPassword = request.Form["db_password"];
        string dbPort = request.Form["db_port"];
        string mailHost = request.Form["mail_host"];
        string mailPort = request.Form["mail_port"];
        string mailUsername = request.Form["mail_username"];
        string mailPassword = request.Form["mail_password"];
        string mailAddress = request.Form["mail_address"];
        string privacyIndexing = request.Form["privacy_indexing"];
        if (dbHost != null && dbDatabase != null && dbUsername != null && dbPassword != null && dbPort != null &&
            mailHost != null && mailPort != null && mailUsername != null && mailPassword != null && mailAddress != null)
        {
            // Validate database settings
            try
            {
                MySQL conn = new MySQL();
                conn.Settings_Host = dbHost;
                conn.Settings_Port = int.Parse(dbPort);
                conn.Settings_User = dbUsername;
                conn.Settings_Pass = dbPassword;
                conn.Connect();
                // Create the database if it doesn't exist
                conn.Query_Execute("CREATE DATABASE IF NOT EXISTS `" + Utils.Escape(dbDatabase) + "`");
            }
            catch (Exception ex)
            {
                error = "MySQL settings failed - check for typo's - raw error: `" + ex.Message + "`, `" + ex.GetBaseException().Message + "`!";
            }
            if (error == null)
            {
                // Test the e-mail settings if non-empty
                if (mailHost.Length != 0 && mailPort.Length != 0 && mailUsername.Length != 0 && mailAddress.Length != 0)
                {
                    // Validate e-mail settings
                    int mailPortParsed;
                    if (mailHost.Length == 0) error = "E-mail host cannot be empty!";
                    else if (mailPort.Length == 0) error = "E-mail port cannot be empty!";
                    else if (!int.TryParse(mailPort, out mailPortParsed) || mailPortParsed < 1 || mailPortParsed > UInt16.MaxValue) error = "Invalid mail port!";
                    else if (mailUsername.Length == 0) error = "E-mail username cannot be empty!";
                    else if (mailAddress.Length == 0) error = "E-mail address cannot be empty!";
                    else
                    {
                        try
                        {
                            SmtpClient client = new SmtpClient();
                            client.Host = mailHost;
                            client.Port = mailPortParsed;
                            client.Credentials = new System.Net.NetworkCredential(mailUsername, mailPassword);
                            client.Send("test@test.com", mailAddress, "Test message", "Test message to test CMS settings.");
                        }
                        catch (Exception ex)
                        {
                            error = "E-mail settings failed - check for typo's - raw error: `" + ex.Message + "`, `" + ex.GetBaseException().Message + "`!";
                        }
                    }
                }
                if (error == null)
                {
                    error = "success";
                    try
                    {
                        // Write the settings - they work!
                        // -- CMS.config (db & mail)
                        try
                        {
                            StringBuilder cmsConfig = new StringBuilder();
                            XmlWriter writer = XmlWriter.Create(cmsConfig);
                            // Start document
                            writer.WriteStartDocument();
                            writer.WriteStartElement("settings");
                            // Database
                            writer.WriteStartElement("db");
                            writer.WriteStartElement("host");
                            writer.WriteCData(dbHost);
                            writer.WriteEndElement();
                            writer.WriteStartElement("port");
                            writer.WriteCData(dbPort);
                            writer.WriteEndElement();
                            writer.WriteStartElement("database");
                            writer.WriteCData(dbDatabase);
                            writer.WriteEndElement();
                            writer.WriteStartElement("username");
                            writer.WriteCData(dbUsername);
                            writer.WriteEndElement();
                            writer.WriteStartElement("password");
                            writer.WriteCData(dbHost);
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                            // E-mail
                            writer.WriteStartElement("mail");
                            writer.WriteStartElement("host");
                            writer.WriteCData(mailHost);
                            writer.WriteEndElement();
                            writer.WriteStartElement("port");
                            writer.WriteCData(mailPort);
                            writer.WriteEndElement();
                            writer.WriteStartElement("username");
                            writer.WriteCData(mailUsername);
                            writer.WriteEndElement();
                            writer.WriteStartElement("password");
                            writer.WriteCData(mailPassword);
                            writer.WriteEndElement();
                            writer.WriteStartElement("address");
                            writer.WriteCData(mailAddress);
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                            // End document
                            writer.WriteEndElement();
                            writer.WriteEndDocument();
                            // Flush and write to file
                            writer.Flush();
                            writer.Close();
                            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "\\CMS.config", writer.ToString());
                        }
                        catch(Exception ex2)
                        {
                            throw new Exception("Failed to create CMS.config - " + ex2.Message + "!");
                        }
                        // -- Robots/search engine indexing
                        try
                        {
                            if (privacyIndexing != null)
                                File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "\\robots.txt", "Disallow: /");
                        }
                        catch
                        {
                            throw new Exception("Failed to create robots file for privacy protection against search engine indexing.");
                        }
                        // Redirect to the installer page
                        response.Redirect(pageElements["URL"] + "/install");
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }
            }
        }
        // Output page
        content.Append(
            templates[TEMPLATES_KEY]["general_settings"]
            .Replace("%ERROR%", error != null ? templates[TEMPLATES_KEY]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty)
            .Replace("%DB_HOST%", HttpUtility.HtmlEncode(dbHost))
            .Replace("%DB_PORT%", HttpUtility.HtmlEncode(dbPort))
            .Replace("%DB_USERNAME%", HttpUtility.HtmlEncode(dbUsername))
            .Replace("%DB_PASSWORD%", HttpUtility.HtmlEncode(dbPassword))
            .Replace("%DB_DATABASE%", HttpUtility.HtmlEncode(dbDatabase))
            .Replace("%MAIL_HOST%", HttpUtility.HtmlEncode(mailHost))
            .Replace("%MAIL_PORT%", HttpUtility.HtmlEncode(mailPort))
            .Replace("%MAIL_USERNAME%", HttpUtility.HtmlEncode(mailUsername))
            .Replace("%MAIL_PASSWORD%", HttpUtility.HtmlEncode(mailPassword))
            .Replace("%MAIL_ADDRESS%", HttpUtility.HtmlEncode(mailAddress))
            .Replace("%PRIVACY_INDEXING%", privacyIndexing != null ? " checked" : string.Empty)
        );
        pageElements["TITLE"] = "General Settings";
#endif
    }
    /// <summary>
    /// Runs the installation script, responsible for initially setting up the CMS and any base plugins.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <param name="content"></param>
    public static void pageInstall(ref UberCMS.Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref StringBuilder content)
    {
#if INSTALLED
        response.Redirect(baseURL + "/finish");
#else
        // Create connector object
        Connector conn = dbSettings.create();
        try
        {
            // Run installer Scripts
            string s = "";
            MethodInfo m;
            foreach (Type clas in Assembly.GetAssembly(typeof(UberCMS.Installer.InstallScript)).GetTypes())
                if (clas.Namespace == "UberCMS.Installer")
                {
                    m = clas.GetMethod("install");
                    m.Invoke(null, new object[] { conn });
                }
                else s += clas.FullName + "<br />";
            throw new Exception("failed. - " + s);
            // Successful - write success to web.config
            UberCMS.Misc.Plugins.preprocessorDirective_Add("INSTALLED");
            // Redirect to finish page
            response.Redirect(pageElements["URL"] + "/finish");
        }
        catch (Exception ex)
        {
            content.Append(
                templates[TEMPLATES_KEY]["install_error"]
                .Replace("%MESSAGE_PRIMARY%", HttpUtility.HtmlEncode(ex.Message))
                .Replace("%STACK_TRACE_PRIMARY%", HttpUtility.HtmlEncode(ex.StackTrace))
                .Replace("%MESSAGE_BASE%", HttpUtility.HtmlEncode(ex.GetBaseException().Message))
                .Replace("%STACK_TRACE_BASE%", HttpUtility.HtmlEncode(ex.GetBaseException().StackTrace))
                );
            pageElements["TITLE"] = "Installation Failed";
        }
        // Dispose connector
        conn.Disconnect();
#endif
    }
    /// <summary>
    /// Displays a confirmation to the user that the CMS has been installed successfully.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <param name="content"></param>
    public static void pageFinish(ref UberCMS.Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref StringBuilder content)
    {
        content.Append(
            templates[TEMPLATES_KEY]["finish"]
        );
    }
}
public class DbSettings
{
    public string host, database, username, password;
    public int port;

    public DbSettings(string host, string database, string username, string password, int port)
    {
        this.host = host;
        this.database = database;
        this.username = username;
        this.password = password;
        this.port = port;
    }
    public Connector create()
    {
        MySQL conn = new MySQL();
        conn.Settings_Database = database;
        conn.Settings_Host = host;
        conn.Settings_Port = port;
        conn.Settings_User = username;
        conn.Settings_Pass = password;
        conn.Connect();
        return conn;
    }
}