using System;
using System.Collections.Generic;
using System.Web;
using UberLib.Connector;

namespace UberCMS.Misc
{
    public class Settings
    {
        #region "Variables"
        private Dictionary<string, string> raw;
        #endregion

        #region "Methods - Constructors"
        public Settings()
        {
            raw = new Dictionary<string, string>();
        }
        #endregion

        #region "Methods - Properties"
        public int getInt(string key)
        {
            if (!raw.ContainsKey(key)) throw new KeyNotFoundException("Settings key '" + key + "' does not exist!");
            return int.Parse(raw[key]);
        }
        public string this[string key]
        {
            get
            {
                if (!raw.ContainsKey(key)) throw new KeyNotFoundException("Settings key '" + key + "' does not exist!");
                return raw[key];
            }
        }
        public bool getBool(string key)
        {
            if (!raw.ContainsKey(key)) throw new KeyNotFoundException("Settings key '" + key + "' does not exist!");
            return raw[key] == "1" || raw[key].ToLower() == "true";
        }
        #endregion

        #region "Methods"
        /// <summary>
        /// Reloads the settings from the database.
        /// </summary>
        /// <param name="conn"></param>
        public void reload(Connector conn)
        {
            lock (raw)
            {
                raw.Clear();
                foreach (ResultRow setting in conn.Query_Read("SELECT keyname, value FROM settings ORDER BY keyname ASC"))
                    raw.Add(setting["keyname"], setting["value"]);
            }
        }
        /// <summary>
        /// Updates a pre-existing setting, else the key is inserted into both the collection and the database.
        /// 
        /// Set description as null to not update it.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="pluginid"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void updateSetting(Connector conn, string pluginid, string key, string value, string description, bool updateIfExists)
        {
            lock (raw)
            {
                if(!raw.ContainsKey(key))
                {
                    conn.Query_Execute("INSERT INTO settings (keyname, pluginid, value" + (description != null ? ", description" : string.Empty) + ") VALUES('" + Utils.Escape(key) + "', '" + Utils.Escape(pluginid) + "', '" + Utils.Escape(value) + "'" + (description != null ? ", '" + Utils.Escape(description) + "'" : string.Empty) + ")");
                    raw.Add(key, value);
                }
                else if (updateIfExists)
                {
                    conn.Query_Execute("UPDATE settings SET value='" + Utils.Escape(value) + "'" + (description != null ? ", description='" + Utils.Escape(description) + "'" : string.Empty) + " WHERE keyname='" + Utils.Escape(key) + "'");
                    raw[key] = value;
                }
            }
        }
        /// <summary>
        /// Removes all of the settings associated with a pluginid.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="pluginid"></param>
        public void removeSettings(Connector conn, string pluginid)
        {
            lock (raw)
            {
                conn.Query_Execute("DELETE FROM settings WHERE pluginid='" + Utils.Escape(pluginid) + "'");
                reload(conn);
            }
        }
        public void dispose()
        {
            raw.Clear();
            raw = null;
        }
        #endregion
    }
}