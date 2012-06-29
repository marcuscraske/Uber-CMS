﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS
 * File:                    /App_Code/Misc/HtmlTemplates.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Responsible for handling HTML/templates between the database, CMS and filesystem.
 */
using System;
using System.Collections.Generic;
using System.Web;
using UberLib.Connector;
using System.Xml;
using System.IO;

namespace UberCMS.Misc
{
    /// <summary>
    /// Used to load and dump HTML from the database and cache it during runtime. HTML are referred to as
    /// "templates", with templates able to have parents - allowing theme-based HTML. Templates without a parent
    /// can simply have their pkey in the database left blank.
    /// 
    /// Notes:
    /// - The hkey "base" is used as the base-page for a template.
    /// </summary>
    public class HtmlTemplates
    {
        #region "Constants"
        /// <summary>
        /// Default templates will start with the specified value in the templates array.
        /// </summary>
        public const string defaultKey = "default$";
        #endregion

        #region "Variables"
        private Dictionary<string, string> templates = null;
        #endregion

        #region "Methods - Constructors & Destructors"
        /// <summary>
        /// Creates a new empty HtmlTemplates object.
        /// </summary>
        public HtmlTemplates()
        {
            templates = new Dictionary<string, string>();
        }
        /// <summary>
        /// Creates a new HtmlTemplates object, loaded with templates from the database.
        /// </summary>
        /// <param name="conn"></param>
        public HtmlTemplates(Connector conn)
        {
            templates = new Dictionary<string, string>();
            loadDb(conn);
        }
        public void dispose()
        {
            templates.Clear();
            templates = null;
        }
        #endregion

        #region "Methods - Accessors/mutators"
        /// <summary>
        /// Used to fetch a template; specify the parent key or null for default.
        /// </summary>
        /// <param name="hkey"></param>
        /// <returns></returns>
        public HtmlTemplatesFetch this[string pkey]
        {
            get
            {
                return new HtmlTemplatesFetch(this, pkey);
            }
        }
        public class HtmlTemplatesFetch
        {
            private string pkey;
            private HtmlTemplates parent;
            public HtmlTemplatesFetch(HtmlTemplates parent, string pkey)
            {
                this.parent = parent;
                this.pkey = pkey;
            }
            public string this[string hkey]
            {
                get
                {
                    if (pkey == null)
                        return parent.templates.ContainsKey(defaultKey + hkey) ? parent.templates[defaultKey + hkey] : null;
                    else
                        return parent.templates.ContainsKey(pkey + "$" + hkey) ? parent.templates[pkey + "$" + hkey] : null;
                }
            }
        }
        /// <summary>
        /// Fetches a template, returns null if not found.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="key"></param>
        /// <param name="useDefaultIfNotFound">If the template is not found for the specified parent and this is true, the template with a default parent will be returned.</param>
        /// <returns></returns>
        public string get(string parent, string key, bool useDefaultIfNotFound)
        {
            return templates.ContainsKey(parent + "$" + key) ? templates[parent + "$" + key] : useDefaultIfNotFound ? templates.ContainsKey(defaultKey + key) ? templates[defaultKey + key] : null : null;
        }
        #endregion

        #region "Methods - Loading & Dumping"
        /// <summary>
        /// Loads the templates stored in the database.
        /// </summary>
        /// <param name="conn"></param>
        public void loadDb(Connector conn)
        {
            lock (templates)
            {
                templates.Clear();
                foreach (ResultRow template in conn.Query_Read("SELECT pkey, hkey, html FROM html_templates ORDER BY hkey ASC"))
                    templates.Add((template["pkey"].Length > 0 ? template["pkey"] + "$" : defaultKey) + template["hkey"], template["html"]);
            }
        }
        /// <summary>
        /// Reads the templates from a directory into the database.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="directory"></param>
        public void readDumpToDb(Connector conn, string directory)
        {
            // Read each file
            XmlDocument doc;
            foreach (string file in Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories))
            {
                doc = new XmlDocument();
                doc.LoadXml(File.ReadAllText(file));
                conn.Query_Execute("INSERT INTO html_templates (pkey, hkey, description, html) VALUES('" + Utils.Escape(doc["template"]["pkey"].InnerText) + "', '" + Utils.Escape(doc["template"]["hkey"].InnerText) + "', '" + Utils.Escape(doc["template"]["description"].InnerText) + "', '" + Utils.Escape(doc["template"]["html"].InnerText) + "')");
            }
        }
        /// <summary>
        /// Dumps the templates stored in the database to a directory.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="directory"></param>
        public void dump(Connector conn, string directory)
        {
            // Delete existing templates
            foreach (string file in Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories))
                File.Delete(file);
            foreach (string subdir in Directory.GetDirectories(directory, "*", SearchOption.AllDirectories))
                try
                { Directory.Delete(subdir, false); }
                catch { }
            // Write new templates
            XmlDocument doc;
            XmlWriter writer;
            foreach (ResultRow template in conn.Query_Read("SELECT * FROM html_templates ORDER BY hkey ASC"))
            {
                doc = new XmlDocument();
                // Check the directory exists
                string dir = directory + "\\" + (template["pkey"].Length > 0 ? template["pkey"] + "\\" : string.Empty);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                // Begin writing the template
                writer = XmlWriter.Create(dir + template["hkey"] + ".xml");
                writer.WriteStartDocument();
                writer.WriteStartElement("template");

                writer.WriteStartElement("pkey");
                writer.WriteCData(template["pkey"]);
                writer.WriteEndElement();

                writer.WriteStartElement("hkey");
                writer.WriteCData(template["hkey"]);
                writer.WriteEndElement();

                writer.WriteStartElement("description");
                writer.WriteCData(template["description"]);
                writer.WriteEndElement();

                writer.WriteStartElement("html");
                writer.WriteCData(template["html"]);
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
                writer.Flush();
                writer.Close();
            }
        }
        #endregion
    }
}