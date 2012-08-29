using System;
using System.Collections.Generic;
using System.Web;
using UberLib.Connector;
using UberCMS;

namespace UberCMS.Installer
{
    public class InstallScript
    {
        public static void install(Connector conn)
        {
            string error = null;
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            // Install SQL
            Misc.Plugins.executeSQL(basePath + "\\Installer\Install.sql", conn);

            // Install default templates
            Misc.HtmlTemplates templates = new Misc.HtmlTemplates();
            templates.readDumpToDb(conn, basePath + "\\Installer\\Templates\\default");

            // NOTE: Plugins from a zip should NOT be installed from this script because the code required for install would require the web-app to be changed;
            // instead simply unextract the zips to the App_Code\Plugins directory under a folder with the same name as the "directory" tag in the plugins'
            // Config.xml file.

            // Install admin panel
            string adminPluginid = null;
            if((error = Misc.Plugins.install(null, ref adminPluginid, basePath + "\\App_Code\\Plugins\\Admin Panel", false, conn)) != null)
                throw new Exception("Failed to install admin panel: " + error);
            if((error = Misc.Plugins.enable(adminPluginid, conn)) != null)
                throw new Exception("Failed to enable admin panel: " + error);

            // Install basic site auth
            string bsaPluginid = null;
            if((error = Misc.Plugins.install(null, ref bsaPluginid, basePath + "\\App_Code\\Plugins\\BasicSiteAuth", false, conn)) != null)
                throw new Exception("Failed to install basic site auth: " + error);
            if((error = Misc.Plugins.enable(bsaPluginid, conn)) != null)
                throw new Exception("Failed to enable basic site auth: " + error);
        }
    }
}