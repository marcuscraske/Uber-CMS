#define BASIC_SITE_AUTH_PROFILES

using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using System.Drawing;
using System.IO;

namespace UberCMS.Plugins
{
    /// <summary>
    /// Country codes use:
    /// http://en.wikipedia.org/wiki/ISO_3166-1_alpha-2
    /// </summary>
    public static class BSA_Profiles
    {
        public static string enable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Reserve URLs
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "profile", "members" }, conn)) != null)
                return error;
            // Install SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Install.sql", conn)) != null)
                return error;
            // Install templates
            if ((error = Misc.Plugins.templatesInstall(basePath + "\\Templates", conn)) != null)
                return error;
            // Install content
            if ((error = Misc.Plugins.contentInstall(basePath + "\\Content")) != null)
                return error;

            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Unreserve URLs
            if((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;
            // Uninstall profile pictures by users - any errors are not critical
            try
            {
                string ppBasePath = Core.basePath + "\\Content\\Profiles";
                if (Directory.Exists(ppBasePath))
                    Directory.Delete(ppBasePath, true);
            }
            catch { }
            // Uninstall content
            if ((error = Misc.Plugins.contentUninstall(basePath + "\\Content")) != null)
                return error;
            // Uninstall templates
            if ((error = Misc.Plugins.templatesUninstall("bsa_profiles", conn)) != null)
                return error;

            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Uninstall SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Uninstall.sql", conn)) != null)
                return error;

            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Add CSS
            Misc.Plugins.addHeaderCSS("/Content/CSS/BSA_Profiles.css", ref pageElements);
            if (request.QueryString["page"].Equals("profile"))
                pageProfile_Profile(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
            else if (request.QueryString["page"].Equals("members"))
                pageMembers(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
        }
        public static void pageProfile_Profile(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Decide which user to display
            string userid = null;
            if (request.QueryString["userid"] != null) // Load via userid
            {
                // Ensure the userid is valid
                if (conn.Query_Count("SELECT COUNT('') FROM bsa_users WHERE userid='" + Utils.Escape(request.QueryString["userid"]) + "'") != 1)
                    return;
                userid = request.QueryString["userid"];
            }
            else if (request.QueryString["username"] != null) // Load via username
            {
                // Fetch the userid, if not found we'll 404 the request by returning
                Result usernameToUserid = conn.Query_Read("SELECT userid FROM bsa_users WHERE username LIKE '" + Utils.Escape(request.QueryString["username"].Replace("%", "")) + "'");
                if (usernameToUserid.Rows.Count != 1) return;
                userid = usernameToUserid[0]["userid"];
            }
            else if (HttpContext.Current.User.Identity.IsAuthenticated) // Load the current logged-in user
                userid = HttpContext.Current.User.Identity.Name;
            else // No user specified, user is not authenticated - tell them to register
                response.Redirect(pageElements["URL"] + "/register", true);
            // By this point the userid should be valid and exist, hence we just need to grab the profile data
            string rawProfileDataQuery = "SELECT p.*, u.username, u.registered, g.title AS group_title, g.access_admin FROM bsa_profiles AS p LEFT OUTER JOIN bsa_users AS u ON u.userid=p.userid LEFT OUTER JOIN bsa_user_groups AS g ON g.groupid=u.groupid WHERE p.userid='" + Utils.Escape(userid) + "'";
            Result rawProfileData = conn.Query_Read(rawProfileDataQuery);
            if (rawProfileData.Rows.Count == 0) // Profile doesn't exist, create it
            {
                conn.Query_Execute("INSERT INTO bsa_profiles (userid) VALUES('" + Utils.Escape(userid) + "')");
                rawProfileData = conn.Query_Read(rawProfileDataQuery);
                if (rawProfileData.Rows.Count == 0) return; // Something is wrong...
            }
            ResultRow profileData = rawProfileData[0];
            // Check if admin or the owner of the profile - if so, we'll set the PROFILE_OWNER FLAG
            bool owner = false;
            if (HttpContext.Current.User.Identity.IsAuthenticated && (profileData["userid"] == HttpContext.Current.User.Identity.Name))
            {
                pageElements.setFlag("PROFILE_OWNER");
                owner = true;
            }
            // Check the user is allowed to access the profile - if it's disabled, only the owner or an admin can access it
            if (!owner && !profileData["disabled"].Equals("0"))
                return;
            // Check which page the user wants to access
            switch (request.QueryString["1"])
            {
                default:
                    // -- About page is default
                    pageProfile_About(pluginid, ref profileData, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "settings":
                    pageProfile_Settings(pluginid, ref rawProfileDataQuery, ref profileData, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "upload":
                    pageProfile_Upload(pluginid, ref profileData, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
            }
            if (pageElements["PROFILE_CONTENT"] == null) return; // No content set, 404..
            // Build frame
            DateTime registered = profileData["registered"].Length > 0 ? DateTime.Parse(profileData["registered"]) : DateTime.MinValue;
            pageElements["CONTENT"] =
                Core.templates["bsa_profiles"]["profile_frame"]
                .Replace("%USERID%", HttpUtility.HtmlEncode(profileData["userid"]))
                .Replace("%PANE_BG_COLOUR%", profileData["colour_background"])
                .Replace("%PANE_TEXT_COLOUR%", profileData["colour_text"])
                .Replace("%PROFILE_PICTURE%", profileData["profile_picture_url"].Length == 0 ? "Content/Images/bsa_profiles/unknown.png" : HttpUtility.HtmlEncode(profileData["profile_picture_url"]))
                .Replace("%BACKGROUND%", (profileData["background_url"].Length > 0 ? "url('" + HttpUtility.HtmlEncode(profileData["background_url"]) + "') " : string.Empty) + (profileData["background_colour"].Length > 0 ? "#" + profileData["background_colour"] : string.Empty))
                .Replace("%USERNAME%", HttpUtility.HtmlEncode(profileData["username"]))
                .Replace("%GROUP%", HttpUtility.HtmlEncode(profileData["group_title"]))
                .Replace("%REGISTERED%", HttpUtility.HtmlEncode(registered.ToString("dd MMMM yyyy")))
                .Replace("%REGISTERED_DAYS%", HttpUtility.HtmlEncode(Misc.Plugins.getTimeString(registered)))
                .Replace("%COUNTRY_FLAG%", profileData["country_code"].Length > 0 ? profileData["country_code"] : "unknown")
                .Replace("%COUNTRY_TITLE%", MarkupEngine.Country.getCountryTitle(profileData["country_code"], conn) ?? "Unknown")
                .Replace("%GENDER_CODE%", profileData["gender"])
                .Replace("%GENDER%", profileData["gender"] == "1" ? "Male" : profileData["gender"] == "2" ? "Female" : "Not specified.")
                .Replace("%OCCUPATION%", profileData["occupation"].Length > 0 ? HttpUtility.HtmlEncode(profileData["occupation"]) : "Not specified.");
            ;
            pageElements["TITLE"] = "Profile - " + HttpUtility.HtmlEncode(profileData["username"]);
        }
        public static void pageProfile_About(string pluginid, ref ResultRow profileData, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Build contact details
            StringBuilder contact = new StringBuilder();
            // -- Github
            if (profileData["contact_github"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "https://github.com/" + profileData["contact_github"])
                    .Replace("%IMAGE%", "https://github.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_github"])));
            // -- Website
            if (profileData["contact_website"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://" + profileData["contact_website"])
                    .Replace("%IMAGE%", pageElements["ADMIN_URL"] + "/Content/Images/bsa_profiles/website.png")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_website"])));
            // -- Email
            if (profileData["contact_email"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "mailto:" + profileData["contact_email"])
                    .Replace("%IMAGE%", "http://facebook.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_email"])));
            // -- Facebook
            if(profileData["contact_facebook"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://www.facebook.com/" + profileData["contact_facebook"])
                    .Replace("%IMAGE%", "http://facebook.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_facebook"])));
            // -- G+
            if (profileData["contact_googleplus"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "https://plus.google.com/" + profileData["contact_googleplus"])
                    .Replace("%IMAGE%", "http://plus.google.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_googleplus"])));
            // -- Steam
            if (profileData["contact_steam"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://steamcommunity.com/id/" + profileData["contact_steam"])
                    .Replace("%IMAGE%", "http://steamcommunity.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_steam"])));
            // -- WLM
            if (profileData["contact_wlm"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://spaces.live.com/profile.aspx?mem=" + profileData["contact_wlm"])
                    .Replace("%IMAGE%", "http://windows.microsoft.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_wlm"])));
            // -- Skype
            if (profileData["contact_skype"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://myskype.info/" + profileData["contact_skype"])
                    .Replace("%IMAGE%", "http://www.skypeassets.com/i/images/icons/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_skype"])));
            // -- YouTube
            if (profileData["contact_youtube"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://www.youtube.com/user/" + profileData["contact_youtube"])
                    .Replace("%IMAGE%", "http://www.youtube.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_youtube"])));
            // -- SoundCloud
            if (profileData["contact_soundcloud"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://soundcloud.com/" + profileData["contact_soundcloud"])
                    .Replace("%IMAGE%", "http://soundcloud.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_soundcloud"])));
            // -- Xbox
            if (profileData["contact_xbox"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://www.xboxlc.com/profile/" + profileData["contact_xbox"])
                    .Replace("%IMAGE%", "http://www.xbox.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_xbox"])));
            // -- PSN
            if (profileData["contact_psn"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://profiles.us.playstation.com/playstation/psn/visit/profiles/" + profileData["contact_psn"])
                    .Replace("%IMAGE%", "http://us.playstation.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_psn"])));
            // -- Flickr
            if (profileData["contact_flickr"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://www.flickr.com/photos/" + profileData["contact_flickr"])
                    .Replace("%IMAGE%", "http://l.yimg.com/g/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_flickr"])));
            // -- Twitter
            if (profileData["contact_twitter"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://twitter.com/" + profileData["contact_twitter"])
                    .Replace("%IMAGE%", "http://www.twitter.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_twitter"])));
            // -- Xfire
            if (profileData["contact_xfire"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://www.xfire.com/profile/" + profileData["contact_xfire"])
                    .Replace("%IMAGE%", "http://xfire.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_xfire"])));
            // -- Deviantart
            if (profileData["contact_deviantart"].Length > 0)
                contact.Append(Core.templates["bsa_profiles"]["profile_about_contact"]
                    .Replace("%URL%", "http://" + profileData["contact_deviantart"] + ".deviantart.com/")
                    .Replace("%IMAGE%", "http://deviantart.com/favicon.ico")
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(profileData["contact_deviantart"])));
            // Set content
            pageElements["PROFILE_CONTENT"] = Core.templates["bsa_profiles"]["profile_about"]
                .Replace("%NUTSHELL%", profileData["nutshell"].Length > 0 ? HttpUtility.HtmlEncode(profileData["nutshell"]) : "User has not specified a nutshell.")
                .Replace("%CONTACT%", contact.Length == 0 ? "User has not specified any contact details." : contact.ToString())
                ;
            pageElements.setFlag("PROFILE_ABOUT");
        }
        public static void pageProfile_Settings(string pluginid, ref string profileQuery, ref ResultRow profileData, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            bool updatedProfile = false;
            string error = null;
            // Add the JS colour-picker
            Misc.Plugins.addHeaderJS("/Content/JS/bsa_profiles/jscolor.js", ref pageElements);
            // Check for postback
            // -- Profile
            string profileEnabled = request.Form["profile_enabled"];
            string frameBgURL = request.Form["frame_bg_url"];
            string frameBgColour = request.Form["frame_bg_colour"];
            string paneBgColour = request.Form["pane_bg_colour"];
            string paneTextColour = request.Form["pane_text_colour"];
            // -- About You
            string nutshell = request.Form["nutshell"];
            string country = request.Form["country"];
            string gender = request.Form["gender"];
            string occupation = request.Form["occupation"];
            // -- Contact details
            string contactGithub = request.Form["contact_github"];
            string contactWebsite = request.Form["contact_website"];
            string contactEmail = request.Form["contact_email"];
            string contactFacebook = request.Form["contact_facebook"];
            string contactGooglePlus = request.Form["contact_googleplus"];
            string contactSteam = request.Form["contact_steam"];
            string contactWlm = request.Form["contact_wlm"];
            string contactSkype = request.Form["contact_skype"];
            string contactYouTube = request.Form["contact_youtube"];
            string contactSoundcloud = request.Form["contact_soundcloud"];
            string contactXbox = request.Form["contact_xbox"];
            string contactPsn = request.Form["contact_psn"];
            string contactFlickr = request.Form["contact_flickr"];
            string contactTwitter = request.Form["contact_twitter"];
            string contactXfire = request.Form["contact_xfire"];
            string contactDeviantArt = request.Form["contact_deviantart"];
            if (profileEnabled != null && frameBgURL != null && frameBgColour != null && paneBgColour != null && paneTextColour != null && nutshell != null &&
                country != null && gender != null && occupation != null && contactGithub != null && contactWebsite != null && contactEmail != null && contactFacebook != null &&
                contactGooglePlus != null && contactSteam != null && contactWlm != null && contactSkype != null && contactYouTube != null && contactSoundcloud != null &&
                contactXbox != null && contactPsn != null && contactFlickr != null && contactTwitter != null && contactXfire != null && contactDeviantArt != null)
            {
                // Validate form data
                const int maxNutshell = 800;
                const int maxOccupation = 35;
                const int maxContactItem = 40;
                // -- Profile
                if (profileEnabled != "0" && profileEnabled != "1") error = "Invalid profile-enabled value!";
                else if (frameBgURL.Length != 0 && !MarkupEngine.Validation.validUrl(frameBgURL))
                    error = "Invalid frame background URL!";
                else if (frameBgColour.Length != 0 && !MarkupEngine.Validation.validHex(frameBgColour))
                    error = "Invalid frame background colour!";
                else if (paneBgColour.Length != 0 && !MarkupEngine.Validation.validHex(paneBgColour))
                    error = "Invalid pane background colour!";
                else if (paneTextColour.Length != 0 && !MarkupEngine.Validation.validHex(paneTextColour))
                    error = "Invalid pane text colour!";
                // -- About You
                else if (nutshell.Length > maxNutshell)
                    error = "Nutshell cannot be greater than 800 characters in length!";
                else if ((country.Length != 0 && country.Length != 2) || MarkupEngine.Country.getCountryTitle(country, conn) == null)
                    error = "Invalid country!";
                else if (gender != "0" && gender != "1" && gender != "2")
                    error = "Invalid gender!";
                else if (occupation.Length > maxOccupation)
                    error = "Invalid occupation!";
                // -- Contact details - we'll only validate size
                else if (contactGithub.Length > maxContactItem)
                    error = "Contact github cannot exceed " + maxContactItem + " characters!";
                else if (contactWebsite.Length != 0 && !MarkupEngine.Validation.validUrl(contactWebsite))
                    error = "Invalid contact website!";
                else if (contactEmail.Length != 0 && !MarkupEngine.Validation.validEmail(contactEmail))
                    error = "Invalid contact e-mail!";
                else if (contactFacebook.Length > maxContactItem)
                    error = "Contact Facebook cannot exceed " + maxContactItem + " characters!";
                else if (contactGooglePlus.Length > maxContactItem)
                    error = "Contact Google Plus cannot exceed " + maxContactItem + " characters!";
                else if (contactSteam.Length > maxContactItem)
                    error = "Contact Steam cannot exceed " + maxContactItem + " characters!";
                else if (contactWlm.Length > maxContactItem)
                    error = "Contact WLM cannot exceed " + maxContactItem + " characters!";
                else if (contactSkype.Length > maxContactItem)
                    error = "Contact Skype cannot exceed " + maxContactItem + " characters!";
                else if (contactYouTube.Length > maxContactItem)
                    error = "Contact YouTube cannot exceed " + maxContactItem + " characters!";
                else if (contactSoundcloud.Length > maxContactItem)
                    error = "Contact SoundCloud cannot exceed " + maxContactItem + " characters!";
                else if (contactXbox.Length > maxContactItem)
                    error = "Contact Xbox Live cannot exceed " + maxContactItem + " characters!";
                else if (contactPsn.Length > maxContactItem)
                    error = "Contact PlayStation Network cannot exceed " + maxContactItem + " characters!";
                else if (contactFlickr.Length > maxContactItem)
                    error = "Contact Flickr cannot exceed " + maxContactItem + " characters!";
                else if (contactTwitter.Length > maxContactItem)
                    error = "Contact Twitter cannot exceed " + maxContactItem + " characters!";
                else if (contactXfire.Length > maxContactItem)
                    error = "Contact Xfire cannot exceed " + maxContactItem + " characters!";
                else if (contactDeviantArt.Length > maxContactItem)
                    error = "Contact DeviantArt cannot exceed " + maxContactItem + " characters!";
                else
                {
                    // Posted data is valid - update the database
                    try
                    {
                        StringBuilder query = new StringBuilder("UPDATE bsa_profiles SET ")
                        .Append("disabled='").Append(profileEnabled).Append("',")
                        .Append("background_url='").Append(frameBgURL).Append("',")
                        .Append("background_colour='").Append(frameBgColour).Append("',")
                        .Append("colour_background='").Append(paneBgColour).Append("',")
                        .Append("colour_text='").Append(paneTextColour).Append("',")
                        .Append("nutshell='").Append(nutshell).Append("',")
                        .Append("gender='").Append(gender).Append("',")
                        .Append("country_code='").Append(country).Append("',")
                        .Append("occupation='").Append(occupation).Append("',")
                        .Append("contact_github='").Append(contactGithub).Append("',")
                        .Append("contact_website='").Append(contactWebsite).Append("',")
                        .Append("contact_email='").Append(contactEmail).Append("',")
                        .Append("contact_facebook='").Append(contactFacebook).Append("',")
                        .Append("contact_googleplus='").Append(contactGooglePlus).Append("',")
                        .Append("contact_steam='").Append(contactSteam).Append("',")
                        .Append("contact_wlm='").Append(contactWlm).Append("',")
                        .Append("contact_skype='").Append(contactSkype).Append("',")
                        .Append("contact_youtube='").Append(contactYouTube).Append("',")
                        .Append("contact_soundcloud='").Append(contactSoundcloud).Append("',")
                        .Append("contact_xbox='").Append(contactXbox).Append("',")
                        .Append("contact_psn='").Append(contactPsn).Append("',")
                        .Append("contact_flickr='").Append(contactFlickr).Append("',")
                        .Append("contact_twitter='").Append(contactTwitter).Append("',")
                        .Append("contact_xfire='").Append(contactXfire).Append("',")
                        .Append("contact_deviantart='").Append(contactDeviantArt).Append("'")
                        .Append(" WHERE profileid='").Append(profileData["profileid"]).Append("'")
                        ;
                        conn.Query_Execute(query.ToString());
                        updatedProfile = true;
                        // Reload the profile settings
                        profileData = conn.Query_Read(profileQuery)[0];
                    }
                    catch
                    {
                        error = "Failed to update profile settings, try again!";
                    }
                }
            }
            // Build options
            StringBuilder profileEnabledItems = new StringBuilder();
            profileEnabledItems.Append("<option value=\"0\"").Append("0" == (profileEnabled ?? profileData["disabled"]) ? " selected=\"selected\"" : string.Empty).Append(">Enabled</option>");
            profileEnabledItems.Append("<option value=\"1\"").Append("1" == (profileEnabled ?? profileData["disabled"]) ? " selected=\"selected\"" : string.Empty).Append(">Disabled</option>");
            
            StringBuilder countryItems = new StringBuilder();
            foreach (MarkupEngine.Country c in MarkupEngine.Country.getCountries(conn))
                countryItems.Append("<option value=\"").Append(c.countryCode).Append("\"").Append(c.countryCode == (country ?? profileData["country_code"]) ? " selected=\"selected\"" : string.Empty).Append(">").Append(HttpUtility.HtmlEncode(c.countryTitle)).Append("</option>");
            
            StringBuilder genderItems = new StringBuilder();
            genderItems.Append("<option value=\"0\"").Append("0" == (gender ?? profileData["gender"]) ? " selected=\"selected\"" : string.Empty).Append(">Not Specified</option>");
            genderItems.Append("<option value=\"1\"").Append("1" == (gender ?? profileData["gender"]) ? " selected=\"selected\"" : string.Empty).Append(">Male</option>");
            genderItems.Append("<option value=\"2\"").Append("2" == (gender ?? profileData["gender"]) ? " selected=\"selected\"" : string.Empty).Append(">Female</option>");

            // Set the content
            pageElements["PROFILE_CONTENT"] = Core.templates["bsa_profiles"]["profile_settings"]
                .Replace("%USERID%", HttpUtility.HtmlEncode(profileData["userid"]))
                .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : updatedProfile ? Core.templates[baseTemplateParent]["success"].Replace("%SUCCESS%", "Successfully updated profile settings!") : string.Empty)
                .Replace("%ENABLED%", profileEnabledItems.ToString())
                .Replace("%FRAME_BG_URL%", HttpUtility.HtmlEncode(profileData["background_url"]))
                .Replace("%FRAME_BG_COLOUR%", HttpUtility.HtmlEncode(profileData["background_colour"]))
                .Replace("%PANE_BG_COLOUR%", HttpUtility.HtmlEncode(profileData["colour_background"]))
                .Replace("%PANE_TEXT_COLOUR%", HttpUtility.HtmlEncode(profileData["colour_text"]))
                .Replace("%NUTSHELL%", HttpUtility.HtmlEncode(profileData["nutshell"]))
                .Replace("%COUNTRY%", countryItems.ToString())
                .Replace("%GENDER%", genderItems.ToString())
                .Replace("%OCCUPATION%", HttpUtility.HtmlEncode(profileData["occupation"]))
                .Replace("%CONTACT_GITHUB%", HttpUtility.HtmlEncode(profileData["contact_github"]))
                .Replace("%CONTACT_WEBSITE%", HttpUtility.HtmlEncode(profileData["contact_website"]))
                .Replace("%CONTACT_EMAIL%", HttpUtility.HtmlEncode(profileData["contact_email"]))
                .Replace("%CONTACT_FACEBOOK%", HttpUtility.HtmlEncode(profileData["contact_facebook"]))
                .Replace("%CONTACT_GOOGLEPLUS%", HttpUtility.HtmlEncode(profileData["contact_googleplus"]))
                .Replace("%CONTACT_STEAM%", HttpUtility.HtmlEncode(profileData["contact_steam"]))
                .Replace("%CONTACT_WLM%", HttpUtility.HtmlEncode(profileData["contact_wlm"]))
                .Replace("%CONTACT_SKYPE%", HttpUtility.HtmlEncode(profileData["contact_skype"]))
                .Replace("%CONTACT_YOUTUBE%", HttpUtility.HtmlEncode(profileData["contact_youtube"]))
                .Replace("%CONTACT_SOUNDCLOUD%", HttpUtility.HtmlEncode(profileData["contact_soundcloud"]))
                .Replace("%CONTACT_XBOX%", HttpUtility.HtmlEncode(profileData["contact_xbox"]))
                .Replace("%CONTACT_PSN%", HttpUtility.HtmlEncode(profileData["contact_psn"]))
                .Replace("%CONTACT_FLICKR%", HttpUtility.HtmlEncode(profileData["contact_flickr"]))
                .Replace("%CONTACT_TWITTER%", HttpUtility.HtmlEncode(profileData["contact_twitter"]))
                .Replace("%CONTACT_XFIRE%", HttpUtility.HtmlEncode(profileData["contact_xfire"]))
                .Replace("%CONTACT_DEVIANTART%", HttpUtility.HtmlEncode(profileData["contact_deviantart"]))
                ;
            pageElements.setFlag("PROFILE_SETTINGS");
        }
        public static void pageProfile_Upload(string pluginid, ref ResultRow profileData, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            string error = null;
            HttpPostedFile image = request.Files["profile_picture"];
            if(image != null)
            {
                if (image.ContentLength > 1048576)
                    error = "Picture cannot exceed 1 megabyte!";
                else if (image.ContentType != "image/gif" && image.ContentType != "image/jpeg" && image.ContentType != "image/png" && image.ContentType != "image/jpg")
                    error = "Invalid file format!";
                else
                {
                    // Compress the image
                    double maxWidth = 144;
                    double maxHeight = 144;
                    Stream bStream = image.InputStream;
                    Image pp = Image.FromStream(bStream);
                    // Work-out the size of the new image
                    int width;
                    int height;
                    if (pp.Width > maxWidth)
                    {
                        width = (int)maxWidth;
                        height = (int)((maxWidth / (double)pp.Width) * pp.Height);
                    }
                    else
                    {
                        height = (int)maxHeight;
                        width = (int)((maxHeight / (double)pp.Height) * pp.Width);
                    }
                    Bitmap compressedImage = new Bitmap(width, height);
                    // Draw the uploaded image
                    Graphics g = Graphics.FromImage(compressedImage);
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.DrawImage(pp, 0, 0, width, height);
                    g.Dispose();
                    // Save the image
                    string basePath = Core.basePath + "\\Content\\Profiles";
                    // -- Check the directory exists
                    if(!Directory.Exists(basePath))
                        Directory.CreateDirectory(basePath);
                    // -- Save
                    compressedImage.Save(basePath + "\\" + profileData["profileid"] + ".png", System.Drawing.Imaging.ImageFormat.Png);
                    conn.Query_Execute("UPDATE bsa_profiles SET profile_picture_url='" + Utils.Escape("Content/Profiles/" + profileData["profileid"] + ".png") + "' WHERE profileid='" + Utils.Escape(profileData["profileid"]) + "'");
                    // Dispose image
                    pp.Dispose();
                    bStream = null;
                    // Redirect to about
                    conn.Disconnect();
                    response.Redirect(pageElements["URL"] + "/profile?userid=" + profileData["userid"], true);
                }
            }
            pageElements["PROFILE_CONTENT"] = Core.templates["bsa_profiles"]["profile_upload"]
                .Replace("%USERID%", HttpUtility.HtmlEncode(profileData["userid"]))
                .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty);
            pageElements.setFlag("PROFILE_UPLOAD");
        }
        public static void pageMembers(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            pageElements["CONTENT"] = Core.templates["bsa_profiles"]["members"];
            pageElements["TITLE"] = "Members";
        }
    }
}