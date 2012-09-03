 ﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS
 * File:                    /App_Code/InstallScript.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Responsible for the initial installation process of the CMS, such as installing
 * base templates and default plugins.
 */
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
            Misc.Plugins.executeSQL(basePath + "\\Installer\\Install.sql", conn);

            // Install default templates
            Misc.HtmlTemplates templates = new Misc.HtmlTemplates();
            templates.readDumpToDb(conn, basePath + "\\Installer\\Templates\\default");

            // Start the core of the CMS
            Core.cmsStart();

            // NOTE: Plugins from a zip should NOT be installed from this script because the code required for install would require the web-app to be changed;
            // instead simply unextract the zips to the App_Code\Plugins directory under a folder with the same name as the "directory" tag in the plugins'
            // Config.xml file.
            if (Core.criticalFailureError != null) throw new Exception(Core.criticalFailureError.GetBaseException().StackTrace);
            if (Core.settings == null) throw new Exception("fuck");

            // Install common
            string commonPluginid = null;
            if ((error = Misc.Plugins.install(null, ref commonPluginid, basePath + "\\App_Code\\Plugins\\Common", false, conn)) != null)
                throw new Exception("Failed to install common plugin: " + error);
            if ((error = Misc.Plugins.enable(commonPluginid, conn)) != null)
                throw new Exception("Failed to enable common plugin: " + error);

            // Install admin panel
            string adminPluginid = null;
            if((error = Misc.Plugins.install(null, ref adminPluginid, basePath + "\\App_Code\\Plugins\\Admin Panel", false, conn)) != null)
                throw new Exception("Failed to install admin panel plugin: " + error);
            if((error = Misc.Plugins.enable(adminPluginid, conn)) != null)
                throw new Exception("Failed to enable admin panel plugin: " + error);

            // Install basic site auth
            string bsaPluginid = null;
            if((error = Misc.Plugins.install(null, ref bsaPluginid, basePath + "\\App_Code\\Plugins\\BasicSiteAuth", false, conn)) != null)
                throw new Exception("Failed to install basic site auth plugin: " + error);
            if((error = Misc.Plugins.enable(bsaPluginid, conn)) != null)
                throw new Exception("Failed to enable basic site auth plugin: " + error);
        }
    }
}