using System;
using System.Collections.Generic;
using System.Web;
using UberLib.Connector;

namespace UberCMS.Misc
{
    public class SettingsCategory
    {
        #region "Variables"
        public Dictionary<string, string> settings;
        #endregion

        #region "Methods - Constructors"
        public SettingsCategory()
        {
            settings = new Dictionary<string, string>();
        }
        #endregion

        #region "Methods - Properties/Accessors"
        /// <summary>
        /// Returns a value for the specified key.
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="KeyNotFoundException">Thrown if the key is not found.</exception>
        /// <returns></returns>
        public string this[string key]
        {
            get
            {
                if (!settings.ContainsKey(key)) throw new KeyNotFoundException("Settings key '" + key + "' does not exist!");
                return settings[key];
            }
        }
        /// <summary>
        /// Returns a boolean for a key. The value of the key is not validated e.g. the value "test" would evaluate as false
        /// and no exception would be thrown; the value can also be 1 (true) or 0 (false).
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="KeyNotFoundException">Thrown if the key is not found.</exception>
        /// <returns></returns>
        public bool getBool(string key)
        {
            if (!settings.ContainsKey(key)) throw new KeyNotFoundException("Settings key '" + key + "' does not exist!");
            return settings[key] == "1" || settings[key].ToLower() == "true";
        }
        /// <summary>
        /// Returns an integer for a specified key; if the value is not numeric or cannot be parsed, FormatException is thrown.
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="KeyNotFoundException">Thrown if the key is not found.</exception>
        /// <returns></returns>
        public int getInt(string key)
        {
            if (!settings.ContainsKey(key)) throw new KeyNotFoundException("Settings key '" + key + "' does not exist!");
            return int.Parse(settings[key]);
        }
        /// <summary>
        /// Returns a boolean stating if the collection contains a key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool contains(string key)
        {
            return settings.ContainsKey(key);
        }
        #endregion
    }
    public class Settings
    {
        #region "Variables"
        private Dictionary<string, SettingsCategory> categories;
        #endregion

        #region "Methods - Constructors"
        public Settings()
        {
            categories = new Dictionary<string, SettingsCategory>();
        }
        #endregion

        #region "Methods - Properties"
        /// <summary>
        /// Returns a category of settings.
        /// </summary>
        /// <param name="category"></param>
        /// <exception cref="KeyNotFoundException">Thrown if the category is not found.</exception>
        /// <returns></returns>
        public SettingsCategory this[string category]
        {
            get
            {
                if (categories.ContainsKey(category))
                    return categories[category];
                else
                    throw new KeyNotFoundException("Settings category '" + category + "' not found!");
            }
        }
        #endregion

        #region "Methods"
        /// <summary>
        /// Reloads the settings from the database.
        /// </summary>
        /// <param name="conn"></param>
        public void reload(Connector conn)
        {
            lock (categories)
            {
                categories.Clear();
                foreach (ResultRow setting in conn.Query_Read("SELECT category, keyname, value FROM settings ORDER BY category ASC, keyname ASC"))
                {
                    // Check the category exists
                    if (!categories.ContainsKey(setting["category"]))
                        categories.Add(setting["category"], new SettingsCategory());
                    // Set the value
                    categories[setting["category"]].settings.Add(setting["keyname"], setting["value"]);
                }
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
        public void updateSetting(Connector conn, string pluginid, string category, string key, string value, string description, bool updateIfExists)
        {
            lock (categories)
            {
                if (categories.ContainsKey(category) && categories[category].contains(key))
                    conn.Query_Execute("INSERT INTO settings (category, keyname, pluginid, value" + (description != null ? ", description" : string.Empty) + ") VALUES('" + Utils.Escape(category) + "', '" + Utils.Escape(key) + "', '" + Utils.Escape(pluginid) + "', '" + Utils.Escape(value) + "'" + (description != null ? ", '" + Utils.Escape(description) + "'" : string.Empty) + ")");
                else if(updateIfExists)
                    conn.Query_Execute("UPDATE settings SET value='" + Utils.Escape(value) + "'" + (description != null ? ", description='" + Utils.Escape(description) + "'" : string.Empty) + " WHERE category='" + Utils.Escape(category) + "' AND keyname='" + Utils.Escape(key) + "'");
            }
        }
        /// <summary>
        /// Removes all of the settings associated with a pluginid.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="pluginid"></param>
        public void removeCategory(Connector conn, string category)
        {
            lock (categories)
            {
                conn.Query_Execute("DELETE FROM settings WHERE category='" + Utils.Escape(category) + "'");
                reload(conn);
            }
        }
        public void dispose()
        {
            categories.Clear();
            categories = null;
        }
        #endregion
    }
}