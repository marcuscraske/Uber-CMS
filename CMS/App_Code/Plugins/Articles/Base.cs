 ﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS / Plugins / Articles
 * File:                    /App_Code/Plugins/Articles/Base.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * A plugin which provides the ability to make articles with a revisioning system; you
 * can also store images and tag articles.
 */
using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace UberCMS.Plugins
{
    public static class Articles
    {
        #region "Enums"
        public enum RecentChanges_EventType
        {
            Created = 1001,
            Edited = 1002,
            Published = 1003,
            Deleted = 1004,
            DeletedThread = 1005,
            SetAsSelected = 1006,
            RebuiltArticleCache = 1007
        }
        #endregion

        #region "Constants"
        public const string PREPROCESSOR_DIRECTIVE = "ARTICLES";
        #endregion

        #region "Constants - Settings"
        public const string SETTINGS_KEY = "articles";
        public const string SETTINGS_KEY_HANDLES_404 = "handles_404";
        public const string SETTINGS_TITLE_MAX = "title_max";
        public const string SETTINGS_TITLE_MIN = "title_min";
        public const string SETTINGS_BODY_MIN = "body_min";
        public const string SETTINGS_BODY_MAX = "body_max";
        public const string SETTINGS_RELATIVE_URL_CHUNK_MIN = "relative_url_chunk_min";
        public const string SETTINGS_RELATIVE_URL_CHUNK_MAX = "relative_url_chunk_max";
        public const string SETTINGS_RELATIVE_URL_MAXCHUNKS = "relative_url_maxchunks";
        public const string SETTINGS_TAGS_TITLE_MIN = "tags_title_min";
        public const string SETTINGS_TAGS_TITLE_MAX = "tags_title_max";
        public const string SETTINGS_TAGS_MAX = "tags_max";
        public const string SETTINGS_THUMBNAIL_MAXSIZE = "thumbnail_maxsize";
        public const string SETTINGS_THUMBNAIL_MAXWIDTH = "thumbnail_maxwidth";
        public const string SETTINGS_THUMBNAIL_MAXHEIGHT = "thumbnail_maxheight";
        public const string SETTINGS_COMMENTS_LENGTH_MIN = "comments_length_min";
        public const string SETTINGS_COMMENTS_LENGTH_MAX = "comments_length_max";
        public const string SETTINGS_COMMENTS_MAX_PER_HOUR = "comments_max_per_hour";
        public const string SETTINGS_COMMENTS_PER_PAGE = "comments_per_page";
        public const string SETTINGS_HISTORY_PER_PAGE = "history_per_page";
        public const string SETTINGS_PENDING_PER_PAGE = "pending_per_page";
        public const string SETTINGS_ARTICLES_EDIT_PER_HOUR = "articles_edit_per_hour";
        public const string SETTINGS_ARTICLES_EDIT_PER_DAY = "articles_edit_per_day";
        public const string SETTINGS_BROWSE_TAG_CATEGORIES = "browse_tag_categories";
        public const string SETTINGS_BROWSE_ARTICLES_SECTION = "browse_articles_section";
        public const string SETTINGS_BROWSE_ARTICLES_PAGE = "browse_articles_page";
        public const string SETTINGS_CHANGES_PER_PAGE = "changes_per_page";
        public const string SETTINGS_STATS_POLLING = "stats_polling";
        public const string SETTINGS_IMAGES_TITLE_MIN = "images_title_min";
        public const string SETTINGS_IMAGES_TITLE_MAX = "images_title_max";
        public const string SETTINGS_IMAGES_MAXSIZE = "images_maxsize";
        public const string SETTINGS_IMAGES_MAXWIDTH = "images_maxwidth";
        public const string SETTINGS_IMAGES_MAXHEIGHT = "images_maxheight";
        public const string SETTINGS_IMAGES_VIEW_REFERENCES = "images_view_references";
        public const string SETTINGS_IMAGES_PER_PAGE = "images_per_page";
        public const string SETTINGS_IMAGE_TYPES = "images_types";
        #endregion

        #region "Constants - Queries"
        /// <summary>
        /// When an article is modified or deleted, some of the tags may not be in-use by any other articles - therefore we can remove them with the below cleanup query.
        /// </summary>
        public const string QUERY_TAGS_CLEANUP = "DELETE FROM articles_tags WHERE NOT EXISTS (SELECT DISTINCT tagid FROM articles_tags_article WHERE tagid=articles_tags.tagid);";
        /// <summary>
        /// When an article (thread) is edited/deleted, the following query is used to cleanup any thumbnails no longer in-use; this structure is far more efficient for storage.
        /// </summary>
        public const string QUERY_THUMBNAIL_CLEANUP = "DELETE FROM articles_thumbnails WHERE NOT EXISTS (SELECT DISTINCT thumbnailid FROM articles WHERE thumbnailid=articles_thumbnails.thumbnailid);";
        #endregion

        #region "Constants - Regexes"
        public const string REGEX_IMAGE_STORE = @"\[img\]([0-9]+)\[\/img\]";
        public const string REGEX_IMAGE_STORE_CUSTOM_W = @"\[img=([0-9]{4}px|[0-9]{3}px|[0-9]{2}px|[0-9]{1}px|[0-9]{4}em|[0-9]{3}em|[0-9]{2}em|[0-9]{1}em)\]([0-9]+)\[\/img\]";
        public const string REGEX_IMAGE_STORE_CUSTOM_WH = @"\[img=([0-9]{4}px|[0-9]{3}px|[0-9]{2}px|[0-9]{1}px|[0-9]{4}em|[0-9]{3}em|[0-9]{2}em|[0-9]{1}em),([0-9]{4}px|[0-9]{3}px|[0-9]{2}px|[0-9]{1}px|[0-9]{4}em|[0-9]{3}em|[0-9]{2}em|[0-9]{1}em)\]([0-9]+)\[\/img\]";
        #endregion

        #region "Variables"
        public static Thread threadRebuildCache = null;
        #endregion

        #region "Methods - Plugin Event Handlers"
        public static string enable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Install pre-processor directive
            if ((error = Misc.Plugins.preprocessorDirective_Add(PREPROCESSOR_DIRECTIVE)) != null)
                return error;
            // Install content
            if((error = Misc.Plugins.contentInstall(basePath + "\\Content")) != null)
                return error;
            // Install templates
            if ((error = Misc.Plugins.templatesInstall(basePath + "\\Templates", conn)) != null)
                return error;
            // Install SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Install.sql", conn)) != null)
                return error;
            // Install settings
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_KEY_HANDLES_404, "1", "Any 404/unhandled pages will be handled by article create - like Wikis.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_TITLE_MAX, "45", "The maximum length of an article title.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_TITLE_MIN, "1", "The minimum length of an article title.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_BODY_MIN, "1", "The minimum length of an article's body.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_BODY_MAX, "8000", "The maximum length of an articles body.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_RELATIVE_URL_CHUNK_MIN, "1", "The minimum length of a URL chunk/directory.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_RELATIVE_URL_CHUNK_MAX, "32", "The maximum length of a URL chunk/directory.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_RELATIVE_URL_MAXCHUNKS, "8", "The maximum number of chunks/directories.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_TAGS_TITLE_MIN, "1", "The minimum length of a tag.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_TAGS_TITLE_MAX, "30", "The maximum length of a tag.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_TAGS_MAX, "20", "The maximum amount of tags per an article.", false); // Remember to update the SQL; keywords are varchars set to this max char length.
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_THUMBNAIL_MAXSIZE, "2097152", "The maximum size of an article thumbnail.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_THUMBNAIL_MAXWIDTH, "240", "The maximum width of an article thumbnail.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_THUMBNAIL_MAXHEIGHT, "180", "The maximum height of an article thumbnail.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_COMMENTS_LENGTH_MIN, "2", "The minimum length of a comment.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_COMMENTS_LENGTH_MAX, "512", "The maximum length of a comment.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_COMMENTS_MAX_PER_HOUR, "8", "The maximum amount of comments per an hour.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_COMMENTS_PER_PAGE, "5", "The number of comments displayed per a page on an article.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_HISTORY_PER_PAGE, "10", "The number of history entries displayed on an article.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_PENDING_PER_PAGE, "10", "The number of pending articles displayed on the pending page.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_ARTICLES_EDIT_PER_HOUR, "8", "The maximum amount of article creations/edits per an hour.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_ARTICLES_EDIT_PER_DAY, "30", "The maximum amount of article creations/edits per a day.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_BROWSE_TAG_CATEGORIES, "15", "The number of main tags/categories displayed on the articles homepage.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_BROWSE_ARTICLES_SECTION, "5", "The number of articles per an articles homepage section.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_BROWSE_ARTICLES_PAGE, "10", "The number of articles displayed per a page for tags and search.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_CHANGES_PER_PAGE, "15", "The number of recent changes per a page.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_STATS_POLLING, "900", "The number of seconds between polling for new stats data about this plugin; set to 0 or less to disable the stats page.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_IMAGES_TITLE_MIN, "2", "The minimum length of an image title.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_IMAGES_TITLE_MAX, "30", "The maximum length of an image title.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_IMAGES_MAXSIZE, "1572864", "The maximum size of an image file.", false); // 1.5 mb
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_IMAGES_MAXWIDTH, "1920", "The maximum width of an uploaded image.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_IMAGES_MAXHEIGHT, "1080", "The maximum height of an uploaded image.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_IMAGES_VIEW_REFERENCES, "10", "The number of references displayed per a page when viewing an image.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_IMAGE_TYPES, "image/gif,image/jpeg,image/png,image/jpg,image/bmp", "The image mime types allowed to be uploaded for images and article thumbnails.", false);
            Core.settings.updateSetting(conn, pluginid, SETTINGS_KEY, SETTINGS_IMAGES_PER_PAGE, "9", "The number of images displayed per a page in the image store.", false);
            // Install default provider(s)
            formattingAdd(conn, pluginid, "UberCMS.Plugins.Common", "format");
            // Reserve URLS
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "home", "article", "articles" }, conn)) != null)
                return error;

            return null;
        }
        public static string disable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Unreserve base URLs
            if ((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;
            // Uninstall content
            if ((error = Misc.Plugins.contentUninstall(basePath + "\\Content")) != null)
                return error;
            // Uninstall templates
            if ((error = Misc.Plugins.templatesUninstall("articles", conn)) != null)
                return error;

            return null;
        }
        public static string uninstall(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Uninstall pre-processor directive
            if ((error = Misc.Plugins.preprocessorDirective_Remove(PREPROCESSOR_DIRECTIVE)) != null)
                return error;
            // Uninstall SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Uninstall.sql", conn)) != null)
                return error;
            // Remove settings
            Core.settings.removeCategory(conn, SETTINGS_KEY);

            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Check the cache is not being rebuilt, else display a page informing the user the system is currently unavailable
            if (threadRebuildCache != null)
            {
                pageUnavailableCacheReconstruction(pluginid, conn, ref pageElements, request, response);
                return;
            }
            // Delegate request
            switch (request.QueryString["page"])
            {
                case "home":
                    pageHome(pluginid, conn, ref pageElements, request, response);
                    break;
                case "article":
                    pageArticle(pluginid, conn, ref pageElements, request, response);
                    break;
                case "articles":
                    pageArticles(pluginid, conn, ref pageElements, request, response);
                    break;
                default:
                    pageArticle_Editor(pluginid, conn, ref pageElements, request, response);
                    break;
            }
        }
        public static void handleRequestNotFound(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if(Core.settings[SETTINGS_KEY].getBool(SETTINGS_KEY_HANDLES_404))
                pageArticle_View(pluginid, conn, ref pageElements, request, response);
        }
        #endregion

        #region "Methods - Page Handlers"
        public static void pageArticles(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Grab permissions
            bool permPublish;
            bool permCreate;
            bool permDelete;
            bool adminAccess;
            if (HttpContext.Current.User.Identity.IsAuthenticated)
            {
                Result permissions = conn.Query_Read("SELECT access_media_create, access_media_publish, access_media_delete, access_admin FROM bsa_users AS u LEFT OUTER JOIN bsa_user_groups AS ug ON ug.groupid=u.groupid WHERE userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
                if (permissions.Rows.Count == 1)
                {
                    permPublish = permissions[0]["access_media_publish"].Equals("1");
                    permCreate = permissions[0]["access_media_create"].Equals("1");
                    permDelete = permissions[0]["access_media_delete"].Equals("1");
                    adminAccess = permissions[0]["access_admin"].Equals("1");
                }
                else
                    permPublish = permCreate = permDelete = adminAccess = false;
            }
            else
                permPublish = permCreate = permDelete = adminAccess = false;
            // Load settings
            int browseArticlesSection = Core.settings[SETTINGS_KEY].getInt(SETTINGS_BROWSE_ARTICLES_SECTION);
            int browseArticlesPage = Core.settings[SETTINGS_KEY].getInt(SETTINGS_BROWSE_ARTICLES_PAGE);
            // Begin building the content
            StringBuilder content = new StringBuilder();
            string subpg = request.QueryString["1"];
            string tag = request.QueryString["2"];
            string search = request.QueryString["keywords"];
            // Invoke the sub-page for content
            switch (subpg)
            {
                case null:
                    pageArticles_Browse(ref content, pluginid, conn, ref pageElements, request, response);
                    break;
                case "images":
                    pageArticles_Images(ref content, pluginid, conn, ref pageElements, request, response, permCreate, permDelete, permPublish);
                    break;
                case "recent_changes":
                    pageArticles_RecentChanges(ref content, pluginid, conn, ref pageElements, request, response, adminAccess);
                    break;
                case "search":
                    if(search == null || search.Length < 1 || search.Length > 40) return;
                    pageArticles_Search(ref content, pluginid, conn, ref pageElements, request, response);
                    break;
                case "tag":
                    if(tag.Length < Core.settings[SETTINGS_KEY].getInt(SETTINGS_TAGS_TITLE_MIN) || tag.Length > Core.settings[SETTINGS_KEY].getInt(SETTINGS_TAGS_TITLE_MAX))
                        return;
                    pageArticles_Tag(ref content, pluginid, conn, ref pageElements, request, response);
                    break;
                case "stats":
                    pageArticles_Stats(ref content, pluginid, conn, ref pageElements, request, response);
                    break;
                case "pending":
                    pageArticles_Pending(ref content, pluginid, conn, ref pageElements, request, response);
                    break;
                case "delete":
                    pageArticles_Delete(ref content, pluginid, conn, ref pageElements, request, response);
                    break;
                case "index":
                    pageArticles_Index(ref content, pluginid, conn, ref pageElements, request, response);
                    break;
                case "rebuild_cache":
                    pageArticles_RebuildCache(ref content, pluginid, conn, ref pageElements, request, response);
                    break;
                default:
                    return;
            }
            // Check content has been set, else 404
            if (content.Length == 0) return;
            // Build tag categories
            StringBuilder tagCategories = new StringBuilder();
            Result rawCategories = conn.Query_Read("SELECT DISTINCT at.keyword, COUNT(ata.tagid) AS articles FROM articles_thread AS ath LEFT OUTER JOIN articles_tags_article AS ata On ata.articleid=ath.articleid_current LEFT OUTER JOIN articles_tags AS at ON at.tagid=ata.tagid GROUP BY at.keyword ORDER BY COUNT(ata.tagid) DESC, at.keyword ASC LIMIT " + Core.settings[SETTINGS_KEY].getInt(SETTINGS_BROWSE_TAG_CATEGORIES));
            if (rawCategories.Rows.Count != 0)
                foreach (ResultRow tagCategory in rawCategories)
                    tagCategories.Append(
                        Core.templates["articles"]["browse_tag"]
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(tagCategory["keyword"]))
                        .Replace("%TITLE_URL%", HttpUtility.UrlEncode(tagCategory["keyword"]))
                        .Replace("%ARTICLES%", HttpUtility.HtmlEncode(tagCategory["articles"]))
                        );
            else
                tagCategories.Append("No categories/tags exist!");
            // Set flags
            if (permPublish)
                pageElements.setFlag("ARTICLES_PUBLISH");
            if(Core.settings[SETTINGS_KEY].getInt(SETTINGS_STATS_POLLING) > 0)
                pageElements.setFlag("ARTICLES_STATS");
            if (permCreate)
                pageElements.setFlag("ARTICLES_CREATE");
            if (adminAccess)
                pageElements.setFlag("ARTICLES_ADMIN");
            // Add CSS
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/Article.css", ref pageElements);
            // Output page
            pageElements["CONTENT"] = Core.templates["articles"]["browse"]
                .Replace("%CONTENT%", content.ToString())
                .Replace("%TAGS%", tagCategories.ToString())
                .Replace("%SEARCH%", HttpUtility.HtmlEncode(search))
                ;
        }
        public static void pageArticle(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            switch (request.QueryString["1"])
            {
                default:
                    pageArticle_View(pluginid, conn, ref pageElements, request, response);
                    break;
                case "create":
                case "editor":
                    pageArticle_Editor(pluginid, conn, ref pageElements, request, response);
                    break;
                case "thumbnail":
                    pageArticle_Thumbnail(pluginid, conn, ref pageElements, request, response);
                    break;
                case "preview":
                    pageArticle_Preview(pluginid, conn, ref pageElements, request, response);
                    break;
            }
        }
        #endregion

        #region "Methods - Pages"
        public static void pageHome(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            int page;
            if (request.QueryString["pg"] == null || !int.TryParse(request.QueryString["pg"], out page) || page < 1) page = 1;
            const int newsPostsPerPage = 3;
            // Build list of posts
            StringBuilder content = new StringBuilder();
            Result articles = conn.Query_Read("SELECT a.articleid, a.title, a.body_cached, at.relative_url, u.username, a.datetime, a.userid FROM articles AS a LEFT OUTER JOIN bsa_users AS u ON u.userid=a.userid LEFT OUTER JOIN articles_thread AS at ON at.threadid=a.threadid WHERE a.articleid IN (SELECT articleid_current FROM articles_thread) AND a.articleid IN (SELECT ata.articleid FROM articles_tags_article AS ata, articles_tags AS at WHERE ata.tagid=at.tagid AND at.keyword='homepage') ORDER BY a.datetime DESC LIMIT " + ((page * newsPostsPerPage) - newsPostsPerPage) + "," + newsPostsPerPage);
            if (articles.Rows.Count > 0)
                foreach (ResultRow article in articles)
                    content.Append(
                        Core.templates["articles"]["home_item"]
                        .Replace("<TITLE>", HttpUtility.HtmlEncode(article["title"]))
                        .Replace("<BODY>", article["body_cached"])
                        .Replace("<DATETIME>", article["datetime"].Length > 0 ? HttpUtility.HtmlEncode(Misc.Plugins.getTimeString(DateTime.Parse(article["datetime"]))) : "unknown")
                        .Replace("<URL>", HttpUtility.HtmlEncode(article["relative_url"].Length > 0 ? pageElements["URL"] + "/" + article["relative_url"] : pageElements["URL"] + "/article/" + article["articleid"]))
                        .Replace("<USERID>", HttpUtility.HtmlEncode(article["userid"]))
                        .Replace("<USERNAME>", HttpUtility.HtmlEncode(article["username"]))
                        );
            else
                content.Append("No more news articles available!");
            // Set page nav flags
            if (page > 1) pageElements.setFlag("PAGE_PREVIOUS");
            if (page < int.MaxValue && articles.Rows.Count == newsPostsPerPage) pageElements.setFlag("PAGE_NEXT");
            // Render page
            pageElements["CONTENT"] = Core.templates["articles"]["home"]
                .Replace("<PAGE>", page.ToString())
                .Replace("<PAGE_PREVIOUS>", (page > 1 ? page - 1 : 1).ToString())
                .Replace("<PAGE_NEXT>", (page < int.MaxValue ? page + 1 : int.MaxValue).ToString())
                .Replace("<ITEMS>", content.ToString())
                ;
            pageElements["TITLE"] = "News";
            // Add includes
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/Article.css", ref pageElements);
            Common.formatInsertDependencies(ref pageElements);
        }
        public static void pageUnavailableCacheReconstruction(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            pageElements["TITLE"] = "Articles - Unavailable";
            pageElements["CONTENT"] = "The articles system is currently unavailable because the cache is being rebuilt; please try your request again in a few minutes...";
        }
        #endregion

        #region "Methods - Article - Pages"
        /// <summary>
        /// Used to create/modify an article.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        public static void pageArticle_Editor(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Check the user is logged-in, else redirect to the login page
            if (!HttpContext.Current.User.Identity.IsAuthenticated)
                response.Redirect(pageElements["URL"] + "/login", true);

            // Load the users permissions and check they're able to create articles
            Result permisions = conn.Query_Read("SELECT access_media_create, access_admin FROM bsa_users AS u LEFT OUTER JOIN bsa_user_groups AS ug ON ug.groupid=u.groupid WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
            if (permisions.Rows.Count != 1 || !permisions[0]["access_media_create"].Equals("1")) return;

            string error = null;
            Result preData = null;
            ResultRow preDataRow = null;
            // Check if we're modifying an existing article, if so we'll load the data
            string articleid = request.QueryString["articleid"];
            if (articleid != null && Misc.Plugins.isNumeric(articleid))
            {
                // Attempt to load the pre-existing article's data
                preData = conn.Query_Read("SELECT a.*, at.relative_url, GROUP_CONCAT(at2.keyword SEPARATOR ',') AS tags FROM articles AS a LEFT OUTER JOIN articles_tags AS at2 ON (EXISTS (SELECT tagid FROM articles_tags_article WHERE tagid=at2.tagid AND articleid='" + Utils.Escape(articleid) + "')) LEFT OUTER JOIN articles_thread AS at ON at.threadid=a.threadid WHERE articleid='" + Utils.Escape(articleid) + "'");
                if (preData.Rows.Count != 1) preData = null;
                else
                    preDataRow = preData[0];
            }
            // Check for postback
            string title = request.Form["title"];
            string body = request.Form["body"];
            string relativeUrl = request.Form["relative_url"] ?? request.QueryString["relative_url"];
            string tags = request.Form["tags"];
            bool allowHTML = request.Form["allow_html"] != null;
            bool allowComments = request.Form["allow_comments"] != null;
            bool showPane = request.Form["show_pane"] != null;
            bool inheritThumbnail = request.Form["inherit_thumbnail"] != null;
            HttpPostedFile thumbnail = request.Files["thumbnail"];
            if (title != null && body != null && relativeUrl != null && tags != null)
            {
                // Validate
                if (title.Length < Core.settings[SETTINGS_KEY].getInt(SETTINGS_TITLE_MIN) || title.Length > Core.settings[SETTINGS_KEY].getInt(SETTINGS_TITLE_MAX))
                    error = "Title must be " + Core.settings[SETTINGS_KEY][SETTINGS_TITLE_MIN] + " to " + Core.settings[SETTINGS_KEY][SETTINGS_TITLE_MAX] + " characters in length!";
                else if (body.Length < Core.settings[SETTINGS_KEY].getInt(SETTINGS_BODY_MIN) || body.Length > Core.settings[SETTINGS_KEY].getInt(SETTINGS_BODY_MAX))
                    error = "Body must be " + Core.settings[SETTINGS_KEY][SETTINGS_BODY_MIN] + " to " + Core.settings[SETTINGS_KEY][SETTINGS_BODY_MAX] + " characters in length!";
                else if (body.Replace(" ", string.Empty).Length == 0)
                    error = "Body cannot be empty/contain just spaces!";
                else if (thumbnail != null && thumbnail.ContentLength > 0 && thumbnail.ContentLength > Core.settings[SETTINGS_KEY].getInt(SETTINGS_THUMBNAIL_MAXSIZE))
                    error = "Thumbnail cannot exceed " + Core.settings[SETTINGS_KEY][SETTINGS_THUMBNAIL_MAXSIZE] + " bytes (" + Misc.Plugins.getBytesString(Core.settings[SETTINGS_KEY].getInt(SETTINGS_THUMBNAIL_MAXSIZE)) + ")!";
                else if (thumbnail != null && thumbnail.ContentLength > 0 && !Core.settings[SETTINGS_KEY].getCommaArrayContains(SETTINGS_IMAGE_TYPES, thumbnail.ContentType))
                    error = "Invalid thumbnail image format - ensure you uploaded an image!";
                else if ((error = validRelativeUrl(relativeUrl, Core.settings[SETTINGS_KEY].getInt(SETTINGS_RELATIVE_URL_MAXCHUNKS), Core.settings[SETTINGS_KEY].getInt(SETTINGS_RELATIVE_URL_CHUNK_MIN), Core.settings[SETTINGS_KEY].getInt(SETTINGS_RELATIVE_URL_CHUNK_MAX))) != null)
                    ;
                else
                {
                    // Verify the user has not exceeded post limits for today - unless they're admin, we'll just skip the checks
                    ResultRow postLimits = permisions[0]["access_admin"].Equals("1") ? null : conn.Query_Read("SELECT (SELECT COUNT('') FROM articles WHERE userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "' AND datetime >= DATE_SUB(NOW(), INTERVAL 1 HOUR)) AS articles_hour, (SELECT COUNT('') FROM articles WHERE userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "' AND datetime >= DATE_SUB(NOW(), INTERVAL 1 DAY)) AS articles_day")[0];
                    if (postLimits != null && int.Parse(postLimits["articles_hour"]) >= Core.settings[SETTINGS_KEY].getInt(SETTINGS_ARTICLES_EDIT_PER_HOUR))
                        error = "You've already posted the maximum amount of articles allowed within an hour, please try again later!";
                    else if (postLimits != null && int.Parse(postLimits["articles_day"]) >= Core.settings[SETTINGS_KEY].getInt(SETTINGS_ARTICLES_EDIT_PER_DAY))
                        error = "You've already posted the maximum amount of articles allowed today, please try again later!";
                    else
                    {
                        // Verify tags
                        ArticleTags parsedTags = getTags(tags, Core.settings[SETTINGS_KEY].getInt(SETTINGS_TAGS_TITLE_MIN), Core.settings[SETTINGS_KEY].getInt(SETTINGS_TAGS_TITLE_MAX), Core.settings[SETTINGS_KEY].getInt(SETTINGS_TAGS_MAX));
                        if (parsedTags.error != null) error = parsedTags.error;
                        else
                        {
                            // Check if we're inserting, else perhaps inheriting, a thumbnail
                            string thumbnailid = null;
                            if (thumbnail != null && thumbnail.ContentLength > 0)
                            {
                                byte[] imageData = compressImageData(thumbnail.InputStream, Core.settings[SETTINGS_KEY].getInt(SETTINGS_THUMBNAIL_MAXWIDTH), Core.settings[SETTINGS_KEY].getInt(SETTINGS_THUMBNAIL_MAXHEIGHT));
                                if (imageData != null)
                                {
                                    // Success - insert thumbnail and get thumbnailid
                                    Dictionary<string, object> thumbParams = new Dictionary<string, object>();
                                    thumbParams.Add("thumb", imageData);
                                    thumbnailid = conn.Query_Scalar_Parameters("INSERT INTO articles_thumbnails (data) VALUES(@thumb); SELECT LAST_INSERT_ID();", thumbParams).ToString();
                                }
                                else
                                    error = "Failed to process thumbnail image, please try again or report this to the site administrator!";
                            }
                            else if (inheritThumbnail && preDataRow != null && preDataRow["thumbnailid"].Length != 0)
                            {
                                // Grab pre-existing thumbnailid
                                thumbnailid = preDataRow["thumbnailid"];
                            }
                            // Ensure no thumbnail processing errors occur, else do not continue
                            if (error == null)
                            {
                                // Format the body formatting for caching
                                StringBuilder cached = new StringBuilder(body);
                                articleViewRebuildCache(conn, ref cached, allowHTML, ref pageElements);

                                // Posted data is valid, check if the thread exists - else create it
                                bool updateArticle = false; // If the article is being modified and it has not been published and it's owned by the same user -> update it (user may make a small change)
                                string threadid;
                                Result threadCheck = conn.Query_Read("SELECT threadid FROM articles_thread WHERE relative_url='" + Utils.Escape(relativeUrl) + "'");
                                if (threadCheck.Rows.Count == 1)
                                {
                                    // -- Thread exists
                                    threadid = threadCheck[0]["threadid"];
                                    // -- Check if to update the article if the articleid has been specified
                                    if (articleid != null)
                                    {
                                        Result updateCheck = conn.Query_Read("SELECT userid, published FROM articles WHERE articleid='" + Utils.Escape(articleid) + "' AND threadid='" + Utils.Escape(threadid) + "'");
                                        if (updateCheck.Rows.Count == 1 && updateCheck[0]["userid"] == HttpContext.Current.User.Identity.Name && !updateCheck[0]["published"].Equals("1"))
                                            updateArticle = true;
                                    }
                                }
                                else
                                    // -- Create thread
                                    threadid = conn.Query_Scalar("INSERT INTO articles_thread (relative_url) VALUES('" + Utils.Escape(relativeUrl) + "'); SELECT LAST_INSERT_ID();").ToString();

                                // Check if to insert or update the article
                                if (updateArticle)
                                {
                                    StringBuilder query = new StringBuilder();
                                    // Update the article
                                    query
                                        .Append("UPDATE articles SET title='").Append(Utils.Escape(title))
                                        .Append("', thumbnailid=").Append(thumbnailid != null ? "'" + Utils.Escape(thumbnailid) + "'" : "NULL")
                                        .Append(", body='").Append(Utils.Escape(body))
                                        .Append("', body_cached='").Append(Utils.Escape(cached.ToString()))
                                        .Append("', allow_comments='").Append(allowComments ? "1" : "0")
                                        .Append("', allow_html='").Append(allowHTML ? "1" : "0")
                                        .Append("', show_pane='").Append(showPane).Append("' WHERE articleid='").Append(Utils.Escape(articleid)).Append("';");
                                    // Delete the previous tags
                                    query.Append("DELETE FROM articles_tags_article WHERE articleid='" + Utils.Escape(articleid) + "';");
                                    // Delete the previous images associated with the article
                                    query.Append("DELETE FROM articles_images_links WHERE articleid='" + Utils.Escape(articleid) + "'");
                                    // -- Execute query
                                    conn.Query_Execute(query.ToString());
                                }
                                else
                                {
                                    // Check if the user is able to publish articles, if so we'll just publish it automatically
                                    Result userPerm = conn.Query_Read("SELECT ug.access_media_publish FROM bsa_users AS u LEFT OUTER JOIN bsa_user_groups AS ug ON ug.groupid=u.groupid WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
                                    if (userPerm.Rows.Count != 1) return; // Something is critically wrong with basic-site-auth
                                    bool publishAuto = userPerm[0]["access_media_publish"].Equals("1"); // If true, this will also set the new article as the current article for the thread
                                    // Insert article and link to the thread
                                    StringBuilder query = new StringBuilder();
                                    query
                                        .Append("INSERT INTO articles (threadid, title, userid, body, body_cached, moderator_userid, published, allow_comments, allow_html, show_pane, thumbnailid, datetime) VALUES('")
                                        .Append(Utils.Escape(threadid))
                                        .Append("', '").Append(Utils.Escape(title))
                                        .Append("', '").Append(Utils.Escape(HttpContext.Current.User.Identity.Name))
                                        .Append("', '").Append(Utils.Escape(body))
                                        .Append("', '").Append(Utils.Escape(cached.ToString()))
                                        .Append("', ").Append(publishAuto ? "'" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'" : "NULL")
                                        .Append(", '").Append(publishAuto ? "1" : "0")
                                        .Append("', '").Append(allowComments ? "1" : "0")
                                        .Append("', '").Append(allowHTML ? "1" : "0")
                                        .Append("', '").Append(showPane ? "1" : "0")
                                        .Append("', ").Append(thumbnailid != null ? "'" + Utils.Escape(thumbnailid) + "'" : "NULL")
                                        .Append(", NOW()); SELECT LAST_INSERT_ID();");
                                    articleid = conn.Query_Scalar(query.ToString()).ToString();
                                    // If this was automatically published, set it as the current article for the thread
                                    if (publishAuto)
                                        conn.Query_Execute("UPDATE articles_thread SET articleid_current='" + Utils.Escape(articleid) + "' WHERE relative_url='" + Utils.Escape(relativeUrl) + "'");
                                }
                                // Add the new tags and delete any tags not used by any other articles, as well as cleanup unused thumbnails
                                StringBuilder finalQuery = new StringBuilder();
                                if (parsedTags.tags.Count > 0)
                                {
                                    StringBuilder tagsInsertQuery = new StringBuilder();
                                    StringBuilder tagsArticleQuery = new StringBuilder();
                                    foreach (string tag in parsedTags.tags)
                                    {
                                        // -- Attempt to insert the tags - if they exist, they wont be inserted
                                        tagsInsertQuery.Append("('" + Utils.Escape(tag) + "'),");
                                        tagsArticleQuery.Append("((SELECT tagid FROM articles_tags WHERE keyword='" + Utils.Escape(tag) + "'), '" + Utils.Escape(articleid) + "'),");
                                    }
                                    // -- Build final query
                                    finalQuery.Append("INSERT IGNORE INTO articles_tags (keyword) VALUES")
                                        .Append(tagsInsertQuery.Remove(tagsInsertQuery.Length - 1, 1).ToString())
                                        .Append("; INSERT IGNORE INTO articles_tags_article (tagid, articleid) VALUES")
                                        .Append(tagsArticleQuery.Remove(tagsArticleQuery.Length - 1, 1).ToString())
                                        .Append(";");
                                }
                                // Add any linked imagery
                                // -- Find the unique valid image IDs
                                List<string> images = new List<string>();
                                foreach (Match m in Regex.Matches(body, REGEX_IMAGE_STORE, RegexOptions.Multiline))
                                    if (!images.Contains(m.Groups[1].Value))
                                        images.Add(m.Groups[1].Value);
                                foreach (Match m in Regex.Matches(body, REGEX_IMAGE_STORE_CUSTOM_W, RegexOptions.Multiline))
                                    if (!images.Contains(m.Groups[3].Value))
                                        images.Add(m.Groups[3].Value);
                                foreach (Match m in Regex.Matches(body, REGEX_IMAGE_STORE_CUSTOM_WH, RegexOptions.Multiline))
                                    if (!images.Contains(m.Groups[3].Value))
                                        images.Add(m.Groups[3].Value);
                                if (images.Count != 0)
                                {
                                    // -- Insert all the valid IDs which exist in the actual articles_images table
                                    finalQuery.Append("INSERT IGNORE INTO articles_images_links (articleid, imageid) SELECT '" + Utils.Escape(articleid) + "' AS articleid, imageid FROM articles_images WHERE imageid IN (");
                                    foreach (string s in images)
                                        finalQuery.Append("'").Append(Utils.Escape(s)).Append("',");
                                    finalQuery.Remove(finalQuery.Length - 1, 1).Append(");");
                                }
                                // -- This will delete any tags in the main table no longer used in the articles tags table
                                finalQuery.Append(QUERY_TAGS_CLEANUP);
                                // -- This will delete any unused thumbnail images
                                finalQuery.Append(QUERY_THUMBNAIL_CLEANUP);
                                // -- This will log the event
                                finalQuery.Append(insertEvent(updateArticle ? RecentChanges_EventType.Edited : RecentChanges_EventType.Created, HttpContext.Current.User.Identity.Name, articleid, threadid));
                                // -- Execute final query
                                conn.Query_Execute(finalQuery.ToString());
                                // Redirect to the new article
                                conn.Disconnect();
                                response.Redirect(pageElements["URL"] + "/article/" + articleid, true);
                            }
                        }
                    }
                }
            }
            // Display form
            pageElements["CONTENT"] = Core.templates["articles"]["editor"]
                .Replace("%ERROR%", error != null ? Core.templates[pageElements["TEMPLATE"]]["error"].Replace("<ERROR>", HttpUtility.HtmlEncode(error)) : string.Empty)
                .Replace("%PARAMS%", preData != null ? "articleid=" + HttpUtility.UrlEncode(preData[0]["articleid"]) : string.Empty)
                .Replace("%TITLE%", HttpUtility.HtmlEncode(title ?? (preDataRow != null ? preDataRow["title"] : string.Empty)))
                .Replace("%RELATIVE_PATH%", HttpUtility.HtmlEncode(relativeUrl ?? (preDataRow != null ? preDataRow["relative_url"] : string.Empty)))
                .Replace("%TAGS%", HttpUtility.HtmlEncode(tags ?? (preDataRow != null ? preDataRow["tags"] : string.Empty)))
                .Replace("%ALLOW_HTML%", allowHTML || (title == null && preDataRow != null && preDataRow["allow_html"].Equals("1")) ? "checked" : string.Empty)
                .Replace("%ALLOW_COMMENTS%", allowComments || (title == null && preDataRow != null && preDataRow["allow_comments"].Equals("1")) ? "checked" : string.Empty)
                .Replace("%SHOW_PANE%", showPane || (title == null && preDataRow != null && preDataRow["show_pane"].Equals("1")) ? "checked" : string.Empty)
                .Replace("%INHERIT%", inheritThumbnail || (title == null && preDataRow != null && preDataRow["thumbnailid"].Length > 0) ? "checked" : string.Empty)
                .Replace("%BODY%", HttpUtility.HtmlEncode(body ?? (preDataRow != null ? preDataRow["body"] : string.Empty)))
                ;
            // Finalize page
            Misc.Plugins.addHeaderJS(pageElements["URL"] + "/Content/JS/Article.js", ref pageElements);
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/Article.css", ref pageElements);
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/Common.css", ref pageElements);
            pageElements["TITLE"] = "Articles - Editor";
        }
        public static byte[] pageArticle_Thumbnail_Unknown = null;
        public static void pageArticle_Thumbnail(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            response.ContentType = "image/jpeg";
            // Cache the unknown image - spam/bot or failed responses will be a lot easier on the web server in terms of I/O
            if (pageArticle_Thumbnail_Unknown == null)
            {
                Image unknownImage = Image.FromFile(Core.basePath + "\\Content\\Images\\articles\\unknown.jpg");
                MemoryStream ms = new MemoryStream();
                unknownImage.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                pageArticle_Thumbnail_Unknown = ms.ToArray();
                ms.Dispose();
                ms = null;
                unknownImage.Dispose();
                unknownImage = null;
            }
            // Check if an articleid was specified, if so we'll try to get the actual image and output it
            string articleid = request.QueryString["2"];
            if (articleid != null && articleid.Length > 0)
            {
                Result thumb = conn.Query_Read("SELECT at.data FROM articles AS a LEFT OUTER JOIN articles_thumbnails AS at ON at.thumbnailid=a.thumbnailid WHERE a.articleid='" + Utils.Escape(articleid) + "'");
                if (thumb.Rows.Count == 1 && thumb[0].ColumnsByteArray != null)
                {
                    try
                    {
                        response.BinaryWrite(thumb[0].GetByteArray("data"));
                        conn.Disconnect();
                        response.End();
                        return;
                    }
                    catch
                    {
                    }
                }
            }
            // The response has not ended, write the unknown image
            response.BinaryWrite(pageArticle_Thumbnail_Unknown);
            conn.Disconnect();
            response.End();
        }
        public static void pageArticle_Preview(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string data = request.Form["data"];
            bool allowHtml = request.Form["allow_html"] != null && request.Form["allow_html"].Equals("1");
            // Format the data
            if (data != null)
            {
                int bodyMin = Core.settings[SETTINGS_KEY].getInt(SETTINGS_BODY_MIN);
                int bodyMax = Core.settings[SETTINGS_KEY].getInt(SETTINGS_BODY_MAX);
                if (data.Length < bodyMin || data.Length > bodyMax)
                    response.Write("Text must be " + bodyMin + " to " + bodyMax + " characters in length!");
                else
                {
                    StringBuilder formattedData = new StringBuilder(allowHtml ? data : HttpUtility.HtmlEncode(data));
                    articleFormat(conn, ref formattedData, ref pageElements, true, true);
                    response.Write(formattedData.ToString());
                }
            }
            // End the response
            conn.Disconnect();
            response.End();
        }
        #endregion

        #region "Methods - Articles - Pages"
        public static void pageArticles_Browse(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            int browseArticlesSection = Core.settings[SETTINGS_KEY].getInt(SETTINGS_BROWSE_ARTICLES_SECTION);
            // Build recent articles
            content.Append(Core.templates["articles"]["browse_header"].Replace("%TITLE%", "Recently Published"));
            Result rawArticlesRecent = conn.Query_Read("SELECT a.title, a.articleid, at.relative_url, a.datetime FROM articles_thread AS at LEFT OUTER JOIN articles AS a ON a.articleid=at.articleid_current ORDER BY a.datetime DESC, a.title ASC LIMIT " + browseArticlesSection);
            if (rawArticlesRecent.Rows.Count != 0)
                foreach (ResultRow article in rawArticlesRecent)
                    content.Append(
                        Core.templates["articles"]["browse_article"]
                        .Replace("%RELATIVE_URL%", article["relative_url"])
                        .Replace("%ARTICLEID%", HttpUtility.UrlEncode(article["articleid"]))
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(article["title"]))
                        .Replace("%DATETIME%", HttpUtility.HtmlEncode(article["datetime"]))
                        .Replace("%DATETIME_SHORT%", HttpUtility.HtmlEncode(article["datetime"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(article["datetime"])) : "Unknown"))
                        );
            else
                content.Append("None.");

            // Build most discussed articles
            content.Append(Core.templates["articles"]["browse_header"].Replace("%TITLE%", "Most Discussed"));
            Result rawArticlesDiscussed = conn.Query_Read("SELECT a.title, a.articleid, at.relative_url, a.datetime FROM articles_thread AS at LEFT OUTER JOIN articles AS a ON a.articleid=at.articleid_current LEFT OUTER JOIN articles_thread_comments AS atc ON atc.threadid=at.threadid GROUP BY atc.threadid ORDER BY COUNT(atc.commentid) DESC, a.title ASC LIMIT " + browseArticlesSection);
            if (rawArticlesDiscussed.Rows.Count != 0)
                foreach (ResultRow article in rawArticlesDiscussed)
                    content.Append(
                        Core.templates["articles"]["browse_article"]
                        .Replace("%RELATIVE_URL%", article["relative_url"])
                        .Replace("%ARTICLEID%", HttpUtility.UrlEncode(article["articleid"]))
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(article["title"]))
                        .Replace("%DATETIME%", HttpUtility.HtmlEncode(article["datetime"]))
                        .Replace("%DATETIME_SHORT%", HttpUtility.HtmlEncode(article["datetime"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(article["datetime"])) : "Unknown"))
                        );
            else
                content.Append("None.");

            // Build random articles
            content.Append(Core.templates["articles"]["browse_header"].Replace("%TITLE%", "Random Articles"));
            Result rawArticlesRandom = conn.Query_Read("SELECT a.title, a.articleid, at.relative_url, a.datetime FROM articles_thread AS at LEFT OUTER JOIN articles AS a ON a.articleid=at.articleid_current ORDER BY RAND(), a.title ASC LIMIT " + browseArticlesSection);
            if (rawArticlesRecent.Rows.Count != 0)
                foreach (ResultRow article in rawArticlesRandom)
                    content.Append(
                        Core.templates["articles"]["browse_article"]
                        .Replace("%RELATIVE_URL%", article["relative_url"])
                        .Replace("%ARTICLEID%", HttpUtility.UrlEncode(article["articleid"]))
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(article["title"]))
                        .Replace("%DATETIME%", HttpUtility.HtmlEncode(article["datetime"]))
                        .Replace("%DATETIME_SHORT%", HttpUtility.HtmlEncode(article["datetime"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(article["datetime"])) : "Unknown"))
                        );
            else
                content.Append("None.");
            pageElements["TITLE"] = "Articles - Browse";
        }
        public static void pageArticles_Tag(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string tag = request.QueryString["2"];
            int browseArticlesPage = Core.settings[SETTINGS_KEY].getInt(SETTINGS_BROWSE_ARTICLES_PAGE);
            // Viewing articles by tag
            int page;
            if (request.QueryString["bpg"] == null || !int.TryParse(request.QueryString["bpg"], out page) || page < 1) page = 1;
            string sort = request.QueryString["sort"];
            // Security
            tag = tag.Replace("%", string.Empty);
            content.Append(Core.templates["articles"]["browse_header"].Replace("%TITLE%", "Tag `" + HttpUtility.HtmlEncode(tag) + "`"));
            // Add sorting
            content.Append(
                Core.templates["articles"]["browse_sorting"]
                .Replace("%URL%", "articles/tag/" + HttpUtility.HtmlEncode(tag) + "?bpg=" + page)
                );
            // Display all the articles belonging to a tag
            Result rawArticles = conn.Query_Read("SELECT ata.articleid, a.title, a.datetime, ath.relative_url FROM articles_tags_article AS ata, articles_tags AS at, articles AS a, articles_thread AS ath WHERE a.articleid=ath.articleid_current AND ata.articleid=a.articleid AND ata.tagid=at.tagid AND at.keyword LIKE '" + Utils.Escape(tag) + "' ORDER BY " + (sort == "t_a" ? "a.title ASC" : sort == "t_d" ? "a.title DESC" : sort == "d_a" ? "a.datetime ASC" : "a.datetime DESC") + " LIMIT " + ((browseArticlesPage * page) - browseArticlesPage) + "," + browseArticlesPage);
            if (rawArticles.Rows.Count != 0)
                foreach (ResultRow article in rawArticles)
                    content.Append(
                        Core.templates["articles"]["browse_article"]
                        .Replace("%RELATIVE_URL%", article["relative_url"])
                        .Replace("%ARTICLEID%", HttpUtility.UrlEncode(article["articleid"]))
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(article["title"]))
                        .Replace("%DATETIME%", HttpUtility.HtmlEncode(article["datetime"]))
                        .Replace("%DATETIME_SHORT%", HttpUtility.HtmlEncode(article["datetime"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(article["datetime"])) : "Unknown"))
                        );
            else
                content.Append("None.");
            // Add page navigation
            content.Append(
                Core.templates["articles"]["browse_nav"]
                .Replace("%TAG%", HttpUtility.UrlEncode(tag))
                .Replace("%URL%", "articles/tag/%TAG%?sort=" + HttpUtility.UrlEncode(sort))
                .Replace("%PAGE%", page.ToString())
                .Replace("%PAGE_PREVIOUS%", (page > 1 ? page - 1 : 1).ToString())
                .Replace("%PAGE_NEXT%", (page < int.MaxValue ? page + 1 : int.MaxValue).ToString())
                );
            // Set navigation flags
            if (page > 1) pageElements.setFlag("ARTICLES_PAGE_PREVIOUS");
            if (page < int.MaxValue && rawArticles.Rows.Count == browseArticlesPage) pageElements.setFlag("ARTICLES_PAGE_NEXT");
            pageElements["TITLE"] = "Articles - Tag - " + HttpUtility.HtmlEncode(tag);
        }
        public static void pageArticles_Search(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string search = request.QueryString["keywords"];
            int browseArticlesPage = Core.settings[SETTINGS_KEY].getInt(SETTINGS_BROWSE_ARTICLES_PAGE);
            int page;
            if (request.QueryString["bpg"] == null || !int.TryParse(request.QueryString["bpg"], out page) || page < 1) page = 1;
            // Viewing articles by search
            content.Append(Core.templates["articles"]["browse_header"].Replace("%TITLE%", "Search Results for `" + HttpUtility.HtmlEncode(search) + "`"));
            string escapedKeywords = Utils.Escape(search.Replace("%", string.Empty));
            Result results = conn.Query_Read("SELECT a.articleid, a.title, a.datetime, at.relative_url FROM articles_thread AS at LEFT OUTER JOIN articles AS a ON a.articleid=at.articleid_current WHERE at.relative_url LIKE '" + escapedKeywords + "' OR a.title LIKE '%" + escapedKeywords + "%' OR a.body LIKE '%" + escapedKeywords + "%' LIMIT " + ((browseArticlesPage * page) - browseArticlesPage) + "," + browseArticlesPage);
            if (results.Rows.Count != 0)
                foreach (ResultRow foundItem in results)
                    content.Append(
                        Core.templates["articles"]["browse_article"]
                        .Replace("%RELATIVE_URL%", foundItem["relative_url"])
                        .Replace("%ARTICLEID%", HttpUtility.UrlEncode(foundItem["articleid"]))
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(foundItem["title"]))
                        .Replace("%DATETIME%", HttpUtility.HtmlEncode(foundItem["datetime"]))
                        .Replace("%DATETIME_SHORT%", HttpUtility.HtmlEncode(foundItem["datetime"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(foundItem["datetime"])) : "Unknown"))
                        );
            else
                content.Append("None.");
            // Add page navigation
            content.Append(
                Core.templates["articles"]["browse_nav"]
                .Replace("%URL%", "articles/search?keywords=" + HttpUtility.HtmlEncode(search))
                .Replace("%PAGE%", page.ToString())
                .Replace("%PAGE_PREVIOUS%", (page > 1 ? page - 1 : 1).ToString())
                .Replace("%PAGE_NEXT%", (page < int.MaxValue ? page + 1 : int.MaxValue).ToString())
                );
            // Set navigation flags
            if (page > 1) pageElements.setFlag("ARTICLES_PAGE_PREVIOUS");
            if (page < int.MaxValue && results.Rows.Count == browseArticlesPage) pageElements.setFlag("ARTICLES_PAGE_NEXT");
            pageElements["TITLE"] = "Articles - Search";
        }
        public static void pageArticles_RecentChanges(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool accessAdmin)
        {
            // Grab the page
            int page;
            if (request.QueryString["bpg"] == null || !int.TryParse(request.QueryString["bpg"], out page) || page < 1) page = 1;
            if (accessAdmin)
            {
                // Check for any admin options
                if (request.QueryString["2"] == "wipe" && Common.AntiCSRF.isValidTokenCookie(request, response))
                {
                    // Wipe all log entries and reload the page
                    conn.Query_Execute("DELETE FROM articles_log_events");
                    conn.Disconnect();
                    response.Redirect(pageElements["URL"] + "/articles/recent_changes");
                }
                // Append options pane
                content.Append(
                    Core.templates["articles"]["change_options"]
                );
                // Set anti-csrf protection
                Common.AntiCSRF.setCookieToken(response);
            }
            // Begin building each log event
            int changesPerPage = Core.settings[SETTINGS_KEY].getInt(SETTINGS_CHANGES_PER_PAGE);
            RecentChanges_EventType type;
            DateTime eventDate;
            int year, month, day;
            year = month = day = 0;
            Result logData = conn.Query_Read("SELECT ale.*, at.relative_url, a.title, u.username FROM articles_log_events AS ale LEFT OUTER JOIN articles AS a ON a.articleid=ale.articleid LEFT OUTER JOIN articles_thread AS at ON at.threadid=ale.threadid LEFT OUTER JOIN bsa_users AS u ON u.userid=ale.userid ORDER BY datetime DESC LIMIT " + ((changesPerPage * page) - changesPerPage) + "," + changesPerPage);
            if (logData.Rows.Count != 0)
                foreach (ResultRow logEvent in logData)
                {
                    eventDate = DateTime.Parse(logEvent["datetime"]);
                    // Check if to change the datetime
                    if (eventDate.Day != day || eventDate.Month != month || eventDate.Year != year)
                    {
                        day = eventDate.Day;
                        month = eventDate.Month;
                        year = eventDate.Year;
                        // Output date header
                        content.Append(
                            Core.templates["articles"]["change_date"]
                            .Replace("%TITLE%", eventDate.ToString("dd MMMM yyyy, dddd"))
                            );
                    }
                    // Append item
                    type = (RecentChanges_EventType)Enum.Parse(typeof(RecentChanges_EventType), logEvent["event_type"]);
                    switch (type)
                    {
                        case RecentChanges_EventType.Created:
                            content.Append(
                                Core.templates["articles"]["change_created"]
                                .Replace("%RELATIVE_URL%", HttpUtility.UrlEncode(logEvent["relative_url"]))
                                .Replace("%TITLE%", logEvent["title"].Length > 0 ? HttpUtility.HtmlEncode(logEvent["title"]) : "(unknown)")
                                .Replace("%USERID%", HttpUtility.HtmlEncode(logEvent["userid"]))
                                .Replace("%USERNAME%", HttpUtility.HtmlEncode(logEvent["username"]))
                                .Replace("%DATETIME%", HttpUtility.HtmlEncode(logEvent["datetime"]))
                                .Replace("%TIME%", HttpUtility.HtmlEncode(Misc.Plugins.getTimeString(eventDate)))
                                );
                            break;
                        case RecentChanges_EventType.Deleted:
                            content.Append(
                                Core.templates["articles"]["change_deleted"]
                                .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(logEvent["articleid"]))
                                .Replace("%THREADID%", HttpUtility.HtmlEncode(logEvent["threadid"]))
                                .Replace("%RELATIVE_URL%", logEvent["relative_url"].Length > 0 ? HttpUtility.UrlEncode(logEvent["relative_url"]) : "(unknown)")
                                .Replace("%USERID%", HttpUtility.HtmlEncode(logEvent["userid"]))
                                .Replace("%USERNAME%", HttpUtility.HtmlEncode(logEvent["username"]))
                                .Replace("%DATETIME%", HttpUtility.HtmlEncode(logEvent["datetime"]))
                                .Replace("%TIME%", HttpUtility.HtmlEncode(Misc.Plugins.getTimeString(eventDate)))
                                );
                            break;
                        case RecentChanges_EventType.DeletedThread:
                            content.Append(
                                Core.templates["articles"]["change_deletedthread"]
                                .Replace("%THREADID%", HttpUtility.HtmlEncode(logEvent["threadid"]))
                                .Replace("%USERID%", HttpUtility.HtmlEncode(logEvent["userid"]))
                                .Replace("%USERNAME%", HttpUtility.HtmlEncode(logEvent["username"]))
                                .Replace("%DATETIME%", HttpUtility.HtmlEncode(logEvent["datetime"]))
                                .Replace("%TIME%", HttpUtility.HtmlEncode(Misc.Plugins.getTimeString(eventDate)))
                                );
                            break;
                        case RecentChanges_EventType.Edited:
                            content.Append(
                                Core.templates["articles"]["change_created"]
                                .Replace("%RELATIVE_URL%", HttpUtility.UrlEncode(logEvent["relative_url"]))
                                .Replace("%TITLE%", logEvent["title"].Length > 0 ? HttpUtility.HtmlEncode(logEvent["title"]) : "(unknown)")
                                .Replace("%USERID%", HttpUtility.HtmlEncode(logEvent["userid"]))
                                .Replace("%USERNAME%", HttpUtility.HtmlEncode(logEvent["username"]))
                                .Replace("%DATETIME%", HttpUtility.HtmlEncode(logEvent["datetime"]))
                                .Replace("%TIME%", HttpUtility.HtmlEncode(Misc.Plugins.getTimeString(eventDate)))
                                );
                            break;
                        case RecentChanges_EventType.Published:
                            content.Append(
                                Core.templates["articles"]["change_published"]
                                .Replace("%RELATIVE_URL%", HttpUtility.UrlEncode(logEvent["relative_url"]))
                                .Replace("%TITLE%", logEvent["title"].Length > 0 ? HttpUtility.HtmlEncode(logEvent["title"]) : "(unknown)")
                                .Replace("%USERID%", HttpUtility.HtmlEncode(logEvent["userid"]))
                                .Replace("%USERNAME%", HttpUtility.HtmlEncode(logEvent["username"]))
                                .Replace("%DATETIME%", HttpUtility.HtmlEncode(logEvent["datetime"]))
                                .Replace("%TIME%", HttpUtility.HtmlEncode(Misc.Plugins.getTimeString(eventDate)))
                                );
                            break;
                        case RecentChanges_EventType.SetAsSelected:
                            content.Append(
                                Core.templates["articles"]["change_selected"]
                                .Replace("%RELATIVE_URL%", HttpUtility.UrlEncode(logEvent["relative_url"]))
                                .Replace("%TITLE%", logEvent["title"].Length > 0 ? HttpUtility.HtmlEncode(logEvent["title"]) : "(unknown)")
                                .Replace("%USERID%", HttpUtility.HtmlEncode(logEvent["userid"]))
                                .Replace("%USERNAME%", HttpUtility.HtmlEncode(logEvent["username"]))
                                .Replace("%DATETIME%", HttpUtility.HtmlEncode(logEvent["datetime"]))
                                .Replace("%TIME%", HttpUtility.HtmlEncode(Misc.Plugins.getTimeString(eventDate)))
                                );
                            break;
                    }
                }
            else
                content.Append("No recent changes have occurred or the log has been wiped.");
            // Append navigation
            content.Append(
                Core.templates["articles"]["browse_nav"]
                .Replace("%URL%", "articles/recent_changes")
                .Replace("%PAGE%", page.ToString())
                .Replace("%PAGE_PREVIOUS%", (page > 1 ? page - 1 : 1).ToString())
                .Replace("%PAGE_NEXT%", (page < int.MaxValue ? page + 1 : int.MaxValue).ToString())
                );
            // Set navigation flags
            if (page > 1) pageElements.setFlag("ARTICLES_PAGE_PREVIOUS");
            if (page < int.MaxValue && logData.Rows.Count == changesPerPage) pageElements.setFlag("ARTICLES_PAGE_NEXT");
            // Output the page
            pageElements["TITLE"] = "Articles - Recent Changes";
        }
        public static void pageArticles_Index(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            int browseArticlesPage = Core.settings[SETTINGS_KEY].getInt(SETTINGS_BROWSE_ARTICLES_PAGE);
            int page;
            if (request.QueryString["bpg"] == null || !int.TryParse(request.QueryString["bpg"], out page) || page < 1) page = 1;
            string sort = request.QueryString["sort"];
            // Add sorting
            content.Append(
                Core.templates["articles"]["browse_sorting"]
                .Replace("%URL%", "articles/index?bpg=" + page)
                );
            // List each article
            Result results = conn.Query_Read("SELECT a.articleid, a.title, a.datetime, at.relative_url FROM articles_thread AS at LEFT OUTER JOIN articles AS a ON a.articleid=at.articleid_current ORDER BY " + (sort == "t_a" ? "a.title ASC" : sort == "t_d" ? "a.title DESC" : sort == "d_a" ? "a.datetime ASC" : "a.datetime DESC") + " LIMIT " + ((browseArticlesPage * page) - browseArticlesPage) + "," + browseArticlesPage);
            if (results.Rows.Count != 0)
                foreach (ResultRow foundItem in results)
                    content.Append(
                        Core.templates["articles"]["browse_article"]
                        .Replace("%RELATIVE_URL%", foundItem["relative_url"])
                        .Replace("%ARTICLEID%", HttpUtility.UrlEncode(foundItem["articleid"]))
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(foundItem["title"]))
                        .Replace("%DATETIME%", HttpUtility.HtmlEncode(foundItem["datetime"]))
                        .Replace("%DATETIME_SHORT%", HttpUtility.HtmlEncode(foundItem["datetime"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(foundItem["datetime"])) : "Unknown"))
                        );
            else
                content.Append("No articles available.");
            // Append navigation
            content.Append(
                Core.templates["articles"]["browse_nav"]
                .Replace("%URL%", "articles/index")
                .Replace("%PAGE%", page.ToString())
                .Replace("%PAGE_PREVIOUS%", (page > 1 ? page - 1 : 1).ToString())
                .Replace("%PAGE_NEXT%", (page < int.MaxValue ? page + 1 : int.MaxValue).ToString())
                );
            // Set navigation flags
            if (page > 1) pageElements.setFlag("ARTICLES_PAGE_PREVIOUS");
            if (page < int.MaxValue && results.Rows.Count == browseArticlesPage) pageElements.setFlag("ARTICLES_PAGE_NEXT");
            pageElements["TITLE"] = "Articles - Index";
        }
        public static void pageArticles_Delete(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string threadid = request.QueryString["2"];
            if (threadid == null || !HttpContext.Current.User.Identity.IsAuthenticated) return;
            // Attempt to retrieve information about the article thread, as well as the users permissions
            Result threadData = conn.Query_Read("SELECT at.*, COUNT(a.articleid) AS article_count, ug.access_media_delete AS perm_delete, a2.title FROM (articles_thread AS at, bsa_users AS u) LEFT OUTER JOIN articles AS a ON a.articleid=at.articleid_current LEFT OUTER JOIN articles AS a2 ON a2.articleid=at.articleid_current LEFT OUTER JOIN bsa_user_groups AS ug ON ug.groupid=u.groupid WHERE at.threadid='" + Utils.Escape(threadid) + "' AND u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
            if (threadData.Rows.Count != 1 || threadData[0]["threadid"] != threadid || !threadData[0]["perm_delete"].Equals("1")) return;
            // Check if the user has posted a confirmation to delete the thread
            string error = null;
            string csrf = request.Form["csrf"];
            string captcha = request.Form["captcha"];
            if (request.Form["confirm"] != null && csrf != null && captcha != null)
            {
                // Validate CSRF
                if (!Common.AntiCSRF.isValidTokenForm(csrf))
                    error = "Invalid security verification, please try your request again!";
                else if (!Common.Validation.validCaptcha(captcha))
                    error = "Incorrect captcha verification code!";
                else
                {
                    // Delete the thread, clear unused tags and clear unused thumbnail images
                    conn.Query_Execute("DELETE FROM articles_thread WHERE threadid='" + Utils.Escape(threadid) + "'; " + QUERY_TAGS_CLEANUP + QUERY_THUMBNAIL_CLEANUP + insertEvent(RecentChanges_EventType.DeletedThread, HttpContext.Current.User.Identity.Name, null, threadData[0]["threadid"]));
                    // Redirect to articles home
                    conn.Disconnect();
                    response.Redirect(pageElements["URL"] + "/articles");
                }
            }
            // Display confirmation/security-verification form
            content.Append(Core.templates["articles"]["thread_delete"]
                .Replace("%THREADID%", HttpUtility.HtmlEncode(threadData[0]["threadid"]))
                .Replace("%ERROR%", error != null ? Core.templates[pageElements["TEMPLATE"]]["error"].Replace("<ERROR>", HttpUtility.HtmlEncode(error)) : string.Empty)
                .Replace("%CSRF%", HttpUtility.HtmlEncode(Common.AntiCSRF.getFormToken()))
                .Replace("%TITLE%", HttpUtility.HtmlEncode(threadData[0]["title"]))
                .Replace("%ARTICLE_COUNT%", HttpUtility.HtmlEncode(threadData[0]["article_count"]))
                .Replace("%RELATIVE_URL%", HttpUtility.HtmlEncode(threadData[0]["relative_url"]))
                );
            pageElements["TITLE"] = "Articles - Delete Thread";
        }
        public static void pageArticles_Pending(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Check the user has publishing permissions
            if (!HttpContext.Current.User.Identity.IsAuthenticated || !conn.Query_Scalar("SELECT ug.access_media_publish FROM bsa_users AS u LEFT OUTER JOIN bsa_user_groups AS ug ON ug.groupid=u.groupid WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'").ToString().Equals("1"))
                return;
            // Get the current page
            int page;
            if (!int.TryParse(request.QueryString["pg"], out page) || page < 1) page = 1;
            // Build a list of pending articles
            StringBuilder articlesPending = new StringBuilder();
            int pendingPerPage = Core.settings[SETTINGS_KEY].getInt(SETTINGS_PENDING_PER_PAGE);
            Result pending = conn.Query_Read("SELECT a.articleid, a.title, u.username, a.userid, a.datetime, a.allow_html FROM articles AS a LEFT OUTER JOIN bsa_users AS u ON u.userid=a.userid WHERE a.published='0' ORDER BY a.datetime ASC LIMIT " + ((page * pendingPerPage) - pendingPerPage) + "," + pendingPerPage);
            if (pending.Rows.Count > 0)
                foreach (ResultRow article in pending)
                    articlesPending.Append(
                        Core.templates["articles"]["articles_pending_row"]
                        .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(article["articleid"]))
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(article["title"]))
                        .Replace("%USERNAME%", HttpUtility.HtmlEncode(article["username"]))
                        .Replace("%USERID%", HttpUtility.HtmlEncode(article["userid"]))
                        .Replace("%CREATED%", HttpUtility.HtmlEncode(article["datetime"]))
                        .Replace("%WARNINGS%", article["allow_html"].Equals("1") ? "HTML" : "&nbsp;")
                        );
            else
                articlesPending.Append("No pending articles.");
            // Append navigation
            articlesPending.Append(
                Core.templates["articles"]["pending_nav"]
                .Replace("%PAGE_PREVIOUS%", (page > 1 ? page - 1 : 1).ToString())
                .Replace("%PAGE%", page.ToString())
                .Replace("%PAGE_NEXT%", (page < int.MaxValue ? page + 1 : int.MaxValue).ToString())
                );
            // Set navigation flags
            if (page > 1) pageElements.setFlag("ARTICLE_PAGE_PREVIOUS");
            if (page < int.MaxValue && pending.Rows.Count == pendingPerPage) pageElements.setFlag("ARTICLE_PAGE_NEXT");
            // Output the page
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/Article.css", ref pageElements);
            content.Append(Core.templates["articles"]["articles_pending"]
                .Replace("%PENDING%", articlesPending.ToString())
                );
            pageElements["TITLE"] = "Articles - Pending";
        }
        #region "Stats"
        private static string statsCache_threadsTotal = string.Empty;
        private static string statsCache_threadsAverageRevisions = string.Empty;
        private static string statsCache_articlesTotal = string.Empty;
        private static string statsCache_articlesStorageText = string.Empty;
        private static string statsCache_articlesStorageThumbs = string.Empty;
        private static string statsCache_articlesTotalThumbnails = string.Empty;
        private static string statsCache_articlesAverageLength = string.Empty;
        private static string statsCache_tagsTotal = string.Empty;
        private static string statsCache_tagsUnique = string.Empty;
        private static string statsCache_tagsAverage = string.Empty;
        private static string statsCache_imagesTotal = string.Empty;
        private static string statsCache_imagesStorage = string.Empty;
        private static string statsCache_totalStorage = string.Empty;
        /// <summary>
        /// The query used to retrieve stats data.
        /// </summary>
        private const string STATS_QUERY =
@"SELECT
(SELECT COUNT('') FROM articles_thread) AS threads_total,
(SELECT AVG(a.revisions) FROM (SELECT COUNT('') As revisions FROM articles_thread AS at, articles AS a WHERE a.threadid=at.threadid GROUP BY at.threadid) AS a) AS threads_average_revisions,
(SELECT COUNT('') FROM articles) AS articles_total,
(SELECT SUM(OCTET_LENGTH(body)) FROM articles) AS articles_storage_text,
(SELECT SUM(OCTET_LENGTH(data)) FROM articles_thumbnails) AS articles_storage_thumbs,
(SELECT COUNT('') FROM articles_thumbnails) AS articles_thumbs_total,
(SELECT AVG(LENGTH(body)) FROM articles) AS articles_average_length,
(SELECT COUNT('') FROM articles_tags_article) AS tags_total,
(SELECT COUNT('') FROM articles_tags) AS tags_unique,
(SELECT AVG(a.count) FROM (SELECT COUNT('') AS count FROM articles_tags_article AS ata GROUP BY ata.articleid) AS a) AS tags_average,
(SELECT COUNT('') FROM articles_images) AS images_total,
(SELECT SUM(OCTET_LENGTH(data)) FROM articles_images) AS images_storage
;
";
        public static DateTime lastUpdatedStats = DateTime.MinValue;
        public static void pageArticles_Stats(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Check if to update the cached statistical data
            int polling = Core.settings[SETTINGS_KEY].getInt(SETTINGS_STATS_POLLING);
            if (polling < 1) return; // Stats have been disabled (or we've received an invalid polling value)
            else if (DateTime.Now.Subtract(lastUpdatedStats).TotalSeconds > polling)
            {
                lastUpdatedStats = DateTime.Now;
                // Update the cached values - we want to avoid doing this often, since it would put strain on the database
                // hence it's easier to simply update every x often when the page is requested and cache the values
                ResultRow data = conn.Query_Read(STATS_QUERY)[0];
                statsCache_threadsTotal = data["threads_total"].Length == 0 ? "0" : int.Parse(data["threads_total"]).ToString();
                statsCache_threadsAverageRevisions = data["threads_average_revisions"].Length == 0 ? "0" : float.Parse(data["threads_average_revisions"]).ToString("0");
                statsCache_articlesTotal = data["articles_total"].Length == 0 ? "0" : float.Parse(data["articles_total"]).ToString();
                statsCache_articlesStorageText = data["articles_storage_text"].Length == 0 ? "0 B" : Misc.Plugins.getBytesString(long.Parse(data["articles_storage_text"]));
                statsCache_articlesStorageThumbs = data["articles_storage_thumbs"].Length == 0 ? " 0 B" : Misc.Plugins.getBytesString(long.Parse(data["articles_storage_thumbs"]));
                statsCache_articlesAverageLength = data["articles_average_length"].Length == 0 ? "0" : float.Parse(data["articles_average_length"]).ToString("0") + " characters";
                statsCache_articlesTotalThumbnails = data["articles_thumbs_total"].Length == 0 ? "0" : float.Parse(data["articles_thumbs_total"]).ToString();
                statsCache_tagsTotal = data["tags_total"].Length == 0 ? "0" : float.Parse(data["tags_total"]).ToString();
                statsCache_tagsUnique = data["tags_unique"].Length == 0 ? "0" : float.Parse(data["tags_unique"]).ToString();
                statsCache_tagsAverage = data["tags_average"].Length == 0 ? "0" : float.Parse(data["tags_average"]).ToString("0");
                statsCache_imagesTotal = data["images_total"].Length == 0 ? "0" : float.Parse(data["images_total"]).ToString();
                statsCache_imagesStorage = data["images_storage"].Length == 0 ? "0 B" : Misc.Plugins.getBytesString(long.Parse(data["images_storage"]));
                statsCache_totalStorage = Misc.Plugins.getBytesString((data["images_storage"].Length == 0 ? 0 : long.Parse(data["images_storage"])) + (data["articles_storage_thumbs"].Length == 0 ? 0 : long.Parse(data["articles_storage_thumbs"])) + (data["articles_storage_text"].Length == 0 ? 0 : long.Parse(data["articles_storage_text"])));
            }
            // Output the page
            pageElements["TITLE"] = "Articles - Statistics";
            content = new StringBuilder(Core.templates["articles"]["stats"]);
            content
                .Replace("%THREADS_TOTAL%", statsCache_threadsTotal)
                .Replace("%THREADS_AVERAGE_REVISIONS%", statsCache_threadsAverageRevisions)
                .Replace("%ARTICLES_TOTAL%", statsCache_articlesTotal)
                .Replace("%ARTICLES_STORAGE_TEXT%", statsCache_articlesStorageText)
                .Replace("%ARTICLES_STORAGE_THUMBS%", statsCache_articlesStorageThumbs)
                .Replace("%ARTICLES_THUMBS_TOTAL%", statsCache_articlesTotalThumbnails)
                .Replace("%ARTICLES_AVERAGE_LENGTH%", statsCache_articlesAverageLength)
                .Replace("%TAGS_TOTAL%", statsCache_tagsTotal)
                .Replace("%TAGS_UNIQUE%", statsCache_tagsUnique)
                .Replace("%TAGS_AVERAGE%", statsCache_tagsAverage)
                .Replace("%IMAGES_TOTAL%", statsCache_imagesTotal)
                .Replace("%IMAGES_STORAGE%", statsCache_imagesStorage)
                .Replace("%TOTAL_STORAGE%", statsCache_totalStorage)
                .Replace("%LAST_UPDATED%", lastUpdatedStats.ToString("dd/MM/yyyy HH:mm:ss"))
                ;
        }
        #endregion
        #region "Images"
        public static void pageArticles_Images(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool permCreate, bool permDelete, bool permPublish)
        {
            switch (request.QueryString["2"])
            {
                case null:
                    // Display existing images
                    pageArticles_Images_Browse(ref content, pluginid, conn, ref pageElements, request, response, permCreate);
                    break;
                case "data":
                    pageArticles_Images_Data(ref content, pluginid, conn, ref pageElements, request, response);
                    break;
                case "upload":
                    pageArticles_Images_Upload(ref content, pluginid, conn, ref pageElements, request, response, permCreate);
                    break;
                case "view":
                    pageArticles_Images_View(ref content, pluginid, conn, ref pageElements, request, response, permDelete);
                    break;
            }
        }
        public static void pageArticles_Images_Browse(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool permCreate)
        {
            // Process request parameters
            int page;
            if (request.QueryString["bpg"] == null || !int.TryParse(request.QueryString["bpg"], out page) || page < 1) page = 1;
            string search = request.QueryString["search"];
            string alphabet = request.QueryString["a"];
            if (search != null && (search.Length < 1 || search.Length > 25)) search = null;
            if(alphabet != null && (alphabet.Length != 1 || !(alphabet[0] >= 97 && alphabet[0] <= 122))) alphabet = null;
            int imagesPerPage = 9;
            // Build list of images
            StringBuilder images = new StringBuilder();
            StringBuilder query = new StringBuilder("SELECT i.imageid, i.title FROM articles_images AS i");
            if(search != null || alphabet != null)
            {
                query.Append(" WHERE ");
                if(search != null)
                    query.Append("i.title LIKE '%").Append(Utils.Escape(search.Replace("%", string.Empty))).Append("%'");
                if(search != null && alphabet != null)
                    query.Append(" AND ");
                if(alphabet != null)
                    query.Append("i.title LIKE '").Append(Utils.Escape(alphabet)).Append("%'");
            }
            query.Append(" ORDER BY i.title ASC, i.datetime ASC LIMIT ").Append((imagesPerPage * page) - imagesPerPage).Append(", ").Append(imagesPerPage);
            Result data = conn.Query_Read(query.ToString());
            StringBuilder results = new StringBuilder();
            if (data.Rows.Count != 0)
                foreach (ResultRow image in data)
                    results.Append(
                        Core.templates["articles"]["image_search_item"]
                        .Replace("%IMAGEID%", HttpUtility.HtmlEncode(image["imageid"]))
                        .Replace("%TITLE%", HttpUtility.HtmlEncode(image["title"]))
                        );
            else
                results.Append("No images found.");
            // Output the page
            content.Append(
                Core.templates["articles"]["image_search"]
                .Replace("%RESULTS%", results.ToString())
                .Replace("%SEARCH%", HttpUtility.HtmlEncode(search))
                );
            pageElements["TITLE"] = "Image Store - Browse";
            // Set flags
            if (permCreate) pageElements.setFlag("ARTICLE_CREATE");
            // Append navigation
            content.Append(
                Core.templates["articles"]["browse_nav"]
                .Replace("%URL%", "articles/images")
                .Replace("%PAGE%", page.ToString())
                .Replace("%PAGE_PREVIOUS%", (page > 1 ? page - 1 : 1).ToString())
                .Replace("%PAGE_NEXT%", (page < int.MaxValue ? page + 1 : int.MaxValue).ToString())
                );
            // Append flags
            if (page > 1) pageElements.setFlag("ARTICLES_PAGE_PREVIOUS");
            if (page < int.MaxValue && data.Rows.Count == imagesPerPage) pageElements.setFlag("ARTICLES_PAGE_NEXT");
        }
        public static void pageArticles_Images_Data(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            string imageid = request.QueryString["3"];
            if (imageid == null && imageid.Length > 0) return;
            // Grab the image data from the database
            Result data = conn.Query_Read("SELECT data FROM articles_images WHERE imageid='" + Utils.Escape(imageid) + "'");
            if (data.Rows.Count != 1 || data[0].ColumnsByteArray == null) return;
            // Output the image
            response.ContentType = "image/png";
            response.BinaryWrite(data[0].GetByteArray("data"));
            conn.Disconnect();
            response.End();
        }
        public static void pageArticles_Images_View(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool permDelete)
        {
            string imageid = request.QueryString["3"];
            if (imageid == null || imageid.Length == 0) return;
            // Grab data about the image
            Result imageData = conn.Query_Read("SELECT i.imageid, i.title, u.userid, u.username, i.datetime FROM articles_images AS i LEFT OUTER JOIN bsa_users AS u ON u.userid=i.userid WHERE i.imageid='" + Utils.Escape(imageid) + "'");
            if (imageData.Rows.Count != 1) return;
            ResultRow image = imageData[0];
            // Set page flags and protection for deletion of photos
            if (HttpContext.Current.User.Identity.IsAuthenticated && (permDelete || image["userid"] == HttpContext.Current.User.Identity.Name))
            {
                // Check if the article has been requested to be deleted
                if (request.QueryString["4"] == "delete" && Common.AntiCSRF.isValidTokenCookie(request, response))
                {
                    // Delete the article and redirect to the image store
                    conn.Query_Execute("DELETE FROM articles_images WHERE imageid='" + Utils.Escape(image["imageid"]) + "'");
                    conn.Disconnect();
                    response.Redirect(pageElements["URL"] + "/articles/images");
                }
                pageElements.setFlag("IMAGE_DELETE");       // Set flag
                Common.AntiCSRF.setCookieToken(response);   // Set cookie for anti-csrf protection
            }
            // Build the list of articles using the image
            int page;
            if (request.QueryString["bpg"] == null || !int.TryParse(request.QueryString["bpg"], out page) || page < 1) page = 1;
            int referencesPerPage = Core.settings[SETTINGS_KEY].getInt(SETTINGS_IMAGES_VIEW_REFERENCES);
            StringBuilder references = new StringBuilder();
            Result referencesData = conn.Query_Read("SELECT a.articleid, a.title, a.datetime FROM articles_images_links AS ail LEFT OUTER JOIN articles AS a ON a.articleid=ail.articleid WHERE ail.imageid='" + Utils.Escape(image["imageid"]) + "' ORDER BY a.datetime DESC LIMIT " + ((referencesPerPage * page) - referencesPerPage) + "," + referencesPerPage);
            if(referencesData.Rows.Count != 0)
                foreach(ResultRow reference in referencesData)
                    references.Append(
                        Core.templates["articles"]["image_view_reference"]
                        .Replace("<ARTICLEID>", HttpUtility.HtmlEncode(reference["articleid"]))
                        .Replace("<TITLE>", HttpUtility.HtmlEncode(reference["title"]))
                        .Replace("<DATETIME_SHORT>", HttpUtility.HtmlEncode(Misc.Plugins.getTimeString(DateTime.Parse(reference["datetime"]))))
                        .Replace("<DATETIME>", HttpUtility.HtmlEncode(reference["datetime"]))
                        );
            else
                references.Append("No articles reference this image.");
            // Output the page
            content.Append(
                Core.templates["articles"]["image_view"]
                .Replace("<IMAGEID>", HttpUtility.HtmlEncode(image["imageid"]))
                .Replace("<USERID>", HttpUtility.HtmlEncode(image["userid"]))
                .Replace("<USERNAME>", HttpUtility.HtmlEncode(image["username"]))
                .Replace("<DATETIME>", HttpUtility.HtmlEncode(image["datetime"]))
                .Replace("<REFERENCES>", references.ToString())
                );
            pageElements["TITLE"] = "Articles - Image Store - " + HttpUtility.HtmlEncode(image["title"]);
            // Add JS file for copypasta of embedding bbcode
            Misc.Plugins.addHeaderJS(pageElements["URL"] + "/Content/JS/Article.js", ref pageElements);
            // Append navigation
            content.Append(
                Core.templates["articles"]["browse_nav"]
                .Replace("%URL%", "articles/images/view/" + image["imageid"])
                .Replace("%PAGE%", page.ToString())
                .Replace("%PAGE_PREVIOUS%", (page > 1 ? page - 1 : 1).ToString())
                .Replace("%PAGE_NEXT%", (page < int.MaxValue ? page + 1 : int.MaxValue).ToString())
                );
            // Set navigation flags
            if (page > 1) pageElements.setFlag("ARTICLES_PAGE_PREVIOUS");
            if (page < int.MaxValue && referencesData.Rows.Count == referencesPerPage) pageElements.setFlag("ARTICLES_PAGE_NEXT");
        }
        public static void pageArticles_Images_Upload(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, bool permCreate)
        {
            // Upload an image
            // -- Ensure the user has creation permissions, else we'll 404
            if (!permCreate) return;
            string error = null;
            HttpPostedFile image = request.Files["image"];
            string title = request.Form["title"];
            string captcha = request.Form["captcha"];
            // Check for postback
            if (title != null && captcha != null && image != null)
            {
                // Validate
                if (!Common.Validation.validCaptcha(captcha))
                    error = "Incorrect captcha verification code, please try again!";
                else if (title.Length < Core.settings[SETTINGS_KEY].getInt(SETTINGS_IMAGES_TITLE_MIN) || title.Length > Core.settings[SETTINGS_KEY].getInt(SETTINGS_TITLE_MAX))
                    error = "Title must be between " + Core.settings[SETTINGS_KEY][SETTINGS_TITLE_MIN] + " to " + Core.settings[SETTINGS_KEY][SETTINGS_IMAGES_TITLE_MAX] + " characters in length!";
                else if (image.ContentLength == 0)
                    error = "The uploaded image contains no data, please try again!";
                else if (image.ContentLength > Core.settings[SETTINGS_KEY].getInt(SETTINGS_IMAGES_MAXSIZE))
                    error = "The uploaded image is too large - maximum size allowed is " + Misc.Plugins.getBytesString(Core.settings[SETTINGS_KEY].getLong(SETTINGS_IMAGES_MAXSIZE)) + "!";
                else if (!Core.settings[SETTINGS_KEY].getCommaArrayContains(SETTINGS_IMAGE_TYPES, image.ContentType))
                    error = "Invalid image type - ensure you've uploaded an actual image!";
                else
                {
                    // Compress the image data for database storage
                    byte[] imageData = compressImageData(image.InputStream, Core.settings[SETTINGS_KEY].getInt(SETTINGS_IMAGES_MAXWIDTH), Core.settings[SETTINGS_KEY].getInt(SETTINGS_IMAGES_MAXHEIGHT));
                    if (imageData == null)
                        error = "Failed to process image - please try your request again or ensure the uploaded image is not corrupt!";
                    else
                    {
                        // Write the data to the database
                        Dictionary<string, object> imageParams = new Dictionary<string, object>();
                        imageParams.Add("title", title);
                        imageParams.Add("userid", HttpContext.Current.User.Identity.Name);
                        imageParams.Add("data", imageData);
                        string imageid = conn.Query_Scalar_Parameters("INSERT INTO articles_images (title, userid, data, datetime) VALUES(@title, @userid, @data, NOW()); SELECT LAST_INSERT_ID();", imageParams).ToString();
                        // Redirect the user to view the image
                        conn.Disconnect();
                        response.Redirect(pageElements["URL"] + "/articles/images/view/" + imageid);
                    }
                }
            }
            // Output form
            content.Append(
                Core.templates["articles"]["image_uploader"]
                .Replace("%ERROR%", error != null ? Core.templates[pageElements["TEMPLATE"]]["error"].Replace("<ERROR>", error) : string.Empty)
                .Replace("%TITLE%", HttpUtility.HtmlEncode(title))
                );
            pageElements["TITLE"] = "Articles - Image Store - Upload";
        }
        #endregion
        public static void pageArticles_RebuildCache(ref StringBuilder content, string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            if (request.Form["confirm"] != null && Common.AntiCSRF.isValidTokenForm(request.Form["csrf"]))
            {
                cacheRebuild();
                conn.Disconnect();
                response.Redirect(pageElements["URL"] + "/articles");
            }
            content.Append(Core.templates["articles"]["rebuild_cache"].Replace("%CSRF%", Common.AntiCSRF.getFormToken()));
            pageElements["TITLE"] = "Articles - Confirm Rebuilding Cache";
        }
        #endregion

        #region "Methods - Cache Rebuilding"
        public static void cacheRebuild()
        {
            threadRebuildCache = new Thread(cacheRebuilder);
            threadRebuildCache.Priority = ThreadPriority.AboveNormal;
            threadRebuildCache.Start();
        }
        private static void cacheRebuilder()
        {
            // Create an independent connector; we don't want issues with other plugins
            Connector conn = Core.connectorCreate(true);
            // Begin rebuilding each article's body
            int totalArticles = conn.Query_Count("SELECT COUNT('') FROM articles");
            StringBuilder text;
            Result articleData;
            Misc.PageElements pe = new Misc.PageElements(); // Used for referencing; has no purpose.
            StringBuilder articlesUpdateBuffer = new StringBuilder();
            int articlesUpdateBufferCount = 0;
            int totalArticlesInBuffer = 8;
            for (int i = 0; i < totalArticles; i++) // We do this in-case there are thousands of articles - in-which case, we'd run out of memory
            {
                articleData = conn.Query_Read("SELECT articleid, body FROM articles LIMIT " + i.ToString() + ",1");
                if (articleData.Rows.Count == 1)
                {
                    // Update the cached text
                    text = new StringBuilder(articleData[0]["body"]);
                    articleFormat(conn, ref text, ref pe, true, true);
                    // Add to buffer
                    articlesUpdateBuffer.Append("UPDATE articles SET body_cached='" + Utils.Escape(text.ToString()) + "' WHERE articleid='" + Utils.Escape(articleData[0]["articleid"]) + "';");
                    articlesUpdateBufferCount++;
                }
                // Check if the buffer is full or we've reached the end of the articles, and hence should be executed
                if (articlesUpdateBufferCount >= totalArticlesInBuffer || i + 1 == totalArticles)
                {
                    conn.Query_Execute(articlesUpdateBuffer.ToString());
                    articlesUpdateBuffer = new StringBuilder();
                    articlesUpdateBufferCount = 0;
                    // Sleep to avoid overkilling the database
                    Thread.Sleep(10);
                }
            }
            // End of reconstruction
            threadRebuildCache = null;
            conn.Disconnect();
            conn = null;
        }
        #endregion

        #region "Methods - Pages - View Article"
        /// <summary>
        /// Used to view an article.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        public static void pageArticle_View(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response)
        {
            // Retrieve the article ID
            string articleid;
            if (request.QueryString["page"] == "article")
                articleid = request.QueryString["1"];
            else
            {
                // Build the relative URL
                StringBuilder relativeUrl = new StringBuilder();
                relativeUrl.Append(request.QueryString["page"]).Append("/"); // The querystring "pg" should never be null, however no null exception will occur with stringbuilder anyhow
                string chunk;
                int relativeUrlMaxChunks = Core.settings[SETTINGS_KEY].getInt(SETTINGS_RELATIVE_URL_MAXCHUNKS);
                int relativeUrlChunkMax = Core.settings[SETTINGS_KEY].getInt(SETTINGS_RELATIVE_URL_CHUNK_MAX);
                int relativeUrlChunkMin = Core.settings[SETTINGS_KEY].getInt(SETTINGS_RELATIVE_URL_CHUNK_MIN);
                for (int i = 1; i <= relativeUrlMaxChunks; i++)
                {
                    chunk = request.QueryString[i.ToString()];
                    if (chunk != null)
                    {
                        if (chunk.Length < relativeUrlChunkMin || chunk.Length > relativeUrlChunkMax)
                            return; // Invalid request - hence 404...
                        else
                            relativeUrl.Append(chunk).Append("/");
                    }
                    else
                        break;
                }
                // Check if we've grabbed anything
                if (relativeUrl.Length == 0)
                    return; // No URL captured - 404...
                else
                    relativeUrl.Remove(relativeUrl.Length - 1, 1); // Remove tailing slash
                // Grab the article ID from the database
                articleid = (conn.Query_Scalar("SELECT articleid_current FROM articles_thread WHERE relative_url='" + Utils.Escape(relativeUrl.ToString()) + "'") ?? string.Empty).ToString();
            }
            // Check we have an articleid that is not null and greater than zero, else 404
            if (articleid == null || articleid.Length == 0) return;
            // Load the article's data
            Result articleRaw = conn.Query_Read("SELECT (SELECT COUNT('') FROM articles WHERE threadid=a.threadid AND articleid <= a.articleid ORDER BY articleid ASC) AS revision, (SELECT ac.allow_comments FROM articles_thread AS act LEFT OUTER JOIN articles AS ac ON ac.articleid=act.articleid_current WHERE act.threadid=at.threadid) AS allow_comments_thread, a.articleid, a.threadid, a.title, a.userid, a.body, a.body_cached, a.moderator_userid, a.published, a.allow_comments, a.allow_html, a.show_pane, a.datetime, at.relative_url, at.articleid_current, u.username FROM (articles AS a, articles_thread AS at) LEFT OUTER JOIN bsa_users AS u ON u.userid=a.userid WHERE a.articleid='" + Utils.Escape(articleid) + "' AND at.threadid=a.threadid");
            if (articleRaw.Rows.Count != 1)
                return; // 404 - no data found - the article is corrupt (thread and article not linked) or the article does not exist
            ResultRow article = articleRaw[0];
            // Load the users permissions
            bool published = article["published"].Equals("1");
            bool permCreate;
            bool permDelete;
            bool permPublish;
            bool owner;
            if (HttpContext.Current.User.Identity.IsAuthenticated)
            {
                Result permsRaw = conn.Query_Read("SELECT access_media_create, access_media_delete, access_media_publish FROM bsa_users AS u LEFT OUTER JOIN bsa_user_groups AS g ON g.groupid=u.groupid WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'");
                if(permsRaw.Rows.Count != 1) return; // Something has gone wrong
                ResultRow perms = permsRaw[0];
                permCreate = perms["access_media_create"].Equals("1");
                permDelete = perms["access_media_delete"].Equals("1");
                permPublish = perms["access_media_publish"].Equals("1");
                owner = article["userid"] == HttpContext.Current.User.Identity.Name;
            }
            else
            {
                permCreate = false;
                permDelete = false;
                permPublish = false;
                owner = false;
            }

            // Create stringbuilder for assembling the article
            StringBuilder content = new StringBuilder();
            // Check the article is published *or* the user is admin/owner of the article
            if (!published && (!HttpContext.Current.User.Identity.IsAuthenticated || (!owner && !permPublish)))
                return;
            // Append the main body of the article
            content.Append(Core.templates["articles"]["article"]);
            
            // Render the body based on the current page
            bool subpage = request.QueryString["page"] == "article" && request.QueryString["2"] != null;
            StringBuilder subpageContent = new StringBuilder();
            if (subpage)
            {
                // -- Sub-page
                switch (request.QueryString["2"])
                {
                    case "publish":
                        if (!permPublish) return; // Check the user has sufficient permission
                        pageArticle_View_Publish(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    case "delete":
                        // An owner of an unpublished article can delete it
                        if (!permDelete && !(owner && !published)) return; // Check the user has sufficient permission
                        pageArticle_View_Delete(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    case "history":
                        pageArticle_View_History(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    case "comments":
                        pageArticle_View_Comments(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    case "set":
                        if (!permPublish || !published) return;
                        pageArticle_View_Set(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    case "rebuild":
                        pageArticle_View_Rebuild(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    default:
                        return; // 404 - unknown sub-page
                }
                content.Replace("%BODY%", subpageContent.ToString());
            }
            else
            {
                if (!published && article["allow_html"].Equals("1"))
                {
                    // Wrap content in HTML protection container (against e.g. malicious uploads)
                    subpageContent.Append(
                        Core.templates["articles"]["article_html_protect"]
                        .Replace("%DATA%", article["body_cached"].Replace("<", "&lt;").Replace(">", "&gt;"))
                        );
                }
                else
                    subpageContent.Append(article["body_cached"]);
                // Insert article dependencies
                Common.formatInsertDependencies(ref pageElements);
                // Generate tags
                StringBuilder tags = new StringBuilder();
                StringBuilder metaTags = new StringBuilder("<meta name=\"keywords\" content=\"");
                foreach (ResultRow tag in conn.Query_Read("SELECT at.keyword FROM articles_tags_article AS ata LEFT OUTER JOIN articles_tags AS at ON at.tagid=ata.tagid WHERE ata.articleid='" + Utils.Escape(article["articleid"]) + "'"))
                {
                    // Append tag for the bottom of the article
                    tags.Append(
                        Core.templates["articles"]["article_tag"].Replace("%TITLE%", HttpUtility.HtmlEncode(tag["keyword"]))
                        );
                    // Append tag for meta
                    metaTags.Append(HttpUtility.HtmlEncode(tag["keyword"])).Append(",");
                }
                metaTags.Remove(metaTags.Length - 1, 1);
                // -- Append meta keywords
                pageElements["HEADER"] += metaTags.Append("\">").ToString();
                // -- Append meta author
                pageElements["HEADER"] += "<meta name=\"author\" content=\"" + article["username"] + "\" />";
                // Set the article's body
                content.Replace("%BODY%", subpageContent.ToString())
                    .Append(Core.templates["articles"]["article_footer"].Replace("%TAGS%", tags.Length == 0 ? "(none)" : tags.ToString()));
            }

            // Add pane
            content
                .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(article["articleid"]))
                .Replace("%THREADID%", HttpUtility.HtmlEncode(article["threadid"]))
                .Replace("%REVISION%", HttpUtility.HtmlEncode(article["revision"]))
                .Replace("%DATE%", article["datetime"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(article["datetime"])) : "unknown")
                .Replace("%COMMENTS%", conn.Query_Count("SELECT COUNT('') FROM articles_thread_comments WHERE threadid='" + Utils.Escape(article["threadid"]) + "'").ToString())
                ;
            // Set flag for showing pane - this can be overriden if a querystring force_pane is specified
            if (article["show_pane"].Equals("1") || !published || request.QueryString["force_pane"] != null || subpage)
                pageElements.setFlag("ARTICLE_SHOW_PANE");

            // Set published flag
            if (published)
                pageElements.setFlag("ARTICLE_PUBLISHED");

            //Set current article flag
            if (article["articleid_current"] == article["articleid"])
                pageElements.setFlag("ARTICLE_CURRENT");

            // Set permission flags
            if (permCreate) pageElements.setFlag("ARTICLE_PERM_CREATE");
            if (permDelete) pageElements.setFlag("ARTICLE_PERM_DELETE");
            if (permPublish) pageElements.setFlag("ARTICLE_PERM_PUBLISH");

            pageElements["TITLE"] = HttpUtility.HtmlEncode(article["title"]);
            pageElements["CONTENT"] = content.ToString();
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/Article.css", ref pageElements);
            Misc.Plugins.addHeaderJS(pageElements["URL"] + "/Content/JS/Article.js", ref pageElements);
        }
        public static void pageArticle_View_Comments(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
        {
            bool allowComments = article["allow_comments_thread"].Equals("1");
            if (!allowComments)
                content.Append(Core.templates["articles"]["comments_disabled"]);

            // -- Check for a new comment posted by the user
            string commentError = null;
            string commentBody = request.Form["comment_body"];
            string commentCaptcha = request.Form["comment_captcha"];
            if (commentBody != null && commentCaptcha != null)
            {
                if (!Common.Validation.validCaptcha(commentCaptcha))
                    commentError = "Incorrect captcha verification code!";
                else if (commentBody.Length < Core.settings[SETTINGS_KEY].getInt(SETTINGS_COMMENTS_LENGTH_MIN) || commentBody.Length > Core.settings[SETTINGS_KEY].getInt(SETTINGS_COMMENTS_LENGTH_MAX))
                    commentError = "Your comment must be " + Core.settings[SETTINGS_KEY][SETTINGS_COMMENTS_LENGTH_MIN] + " to  " + Core.settings[SETTINGS_KEY][SETTINGS_COMMENTS_LENGTH_MAX] + " in length!";
                else if (commentBody.Replace(" ", string.Empty).Length == 0)
                    commentError = "Comment cannot be empty/contain just spaces!";
                else if (conn.Query_Count("SELECT COUNT('') FROM articles_thread_comments WHERE userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "' AND datetime >= DATE_SUB(NOW(), INTERVAL 1 HOUR)") >= Core.settings[SETTINGS_KEY].getInt(SETTINGS_COMMENTS_MAX_PER_HOUR))
                    commentError = "You've already posted the maximum of " + Core.settings[SETTINGS_KEY][SETTINGS_COMMENTS_MAX_PER_HOUR] + " comments per an hour - try again later!";
                else
                {
                    // Insert the post
                    conn.Query_Execute("INSERT INTO articles_thread_comments (threadid, userid, message, datetime) VALUES('" + Utils.Escape(article["threadid"]) + "', '" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "', '" + Utils.Escape(commentBody) + "', NOW())");
                    // Reset comment body
                    commentBody = null;
                }
            }
            // -- Check if to delete a comment
            string dcom = request.QueryString["dcom"];
            if (dcom != null && HttpContext.Current.User.Identity.IsAuthenticated && Misc.Plugins.isNumeric(dcom))
            {
                bool canDelete = permDelete;
                if (!canDelete)
                {
                    // -- User cannot delete all comments, check if they're the owner
                    Result dcomData = conn.Query_Read("SELECT userid FROM articles_thread_comments WHERE commentid='" + Utils.Escape(dcom) + "'");
                    if (dcomData.Rows.Count == 1 && dcomData[0]["userid"] == HttpContext.Current.User.Identity.Name)
                        canDelete = true;
                }
                if (!canDelete) return;
                else
                    conn.Query_Execute("DELETE FROM articles_thread_comments WHERE commentid='" + Utils.Escape(dcom) + "'");
            }
            // Build comments body
            string commentsPageRaw = request.QueryString["apg"];
            // -- Get the page
            int commentsPage;
            if (!int.TryParse(commentsPageRaw, out commentsPage) || commentsPage < 1) commentsPage = 1;
            // -- Get the comments data associated with that page
            int commentsPerPage = Core.settings[SETTINGS_KEY].getInt(SETTINGS_COMMENTS_PER_PAGE);
            Result commentsData = conn.Query_Read("SELECT atc.*, u.username FROM articles_thread_comments AS atc LEFT OUTER JOIN bsa_users AS u ON u.userid=atc.userid WHERE threadid='" + Utils.Escape(article["threadid"]) + "' ORDER BY datetime DESC LIMIT " + ((commentsPerPage * commentsPage) - commentsPerPage) + "," + commentsPerPage);
            // -- Build the data
            if (commentsData.Rows.Count == 0)
                content.Append(Core.templates["articles"]["comments_empty"]);
            else
                foreach (ResultRow comment in commentsData)
                {
                    content.Append(
                        (HttpContext.Current.User.Identity.IsAuthenticated && (permDelete || HttpContext.Current.User.Identity.Name == comment["userid"]) ? Core.templates["articles"]["comment_delete"] : Core.templates["articles"]["comment"])
                        .Replace("<USERID>", comment["userid"])
                        .Replace("<ARTICLEID>", article["articleid"])
                        .Replace("<COMMENTID>", comment["commentid"])
                        .Replace("<USERNAME>", HttpUtility.HtmlEncode(comment["username"]))
                        .Replace("<DATETIME>", HttpUtility.HtmlEncode(comment["datetime"]))
                        .Replace("<BODY>", HttpUtility.HtmlEncode(comment["message"]))
                        );
                }
            // Set navigator
            content.Append(
                Core.templates["articles"]["page_nav"]
                .Replace("%SUBPAGE%", "comments")
                .Replace("%PAGE%", commentsPage.ToString())
                .Replace("%PAGE_PREVIOUS%", (commentsPage > 1 ? commentsPage - 1 : 1).ToString())
                .Replace("%PAGE_NEXT%", (commentsPage < int.MaxValue ? commentsPage + 1 : int.MaxValue).ToString())
                );
            // -- Set flags for the previous and next buttons - very simple solution but highly efficient
            if (commentsPage > 1)
                pageElements.setFlag("ARTICLE_PAGE_PREVIOUS");
            if (commentsData.Rows.Count == commentsPerPage)
                pageElements.setFlag("ARTICLE_PAGE_NEXT");
            // Set the postbox
            if (HttpContext.Current.User.Identity.IsAuthenticated && allowComments)
                content.Append(
                        Core.templates["articles"]["comments_postbox"]
                    .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(article["articleid"]))
                    .Replace("%SUBPAGE%", "history")
                    .Replace("%ERROR%", commentError != null ? Core.templates[pageElements["TEMPLATE"]]["error"].Replace("<ERROR>", commentError) : string.Empty)
                    .Replace("%COMMENT_BODY%", HttpUtility.HtmlEncode(commentBody))
                    );
        }
        public static void pageArticle_View_Delete(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
        {
            string error = null;
            string captcha = request.Form["captcha"];
            
            if (request.Form["confirm"] != null && captcha != null)
            {
                if (!Common.Validation.validCaptcha(captcha))
                    error = "Incorrect captcha verification code!";
                else
                {
                    // Delete the article
                    conn.Query_Execute("DELETE FROM articles WHERE articleid='" + Utils.Escape(article["articleid"]) + "';" + insertEvent(RecentChanges_EventType.Deleted, HttpContext.Current.User.Identity.Name, article["articleid"], article["threadid"]));
                    // Check if any more articles exist and if a current article is set
                    ResultRow thread = conn.Query_Read("SELECT (SELECT articleid_current FROM articles_thread WHERE threadid='" + Utils.Escape(article["threadid"]) + "') AS current_article, (SELECT COUNT('') FROM articles WHERE threadid='" + Utils.Escape(article["threadid"]) + "') AS articles_remaining")[0];
                    StringBuilder finalQuery = new StringBuilder();
                    if (thread["current_article"].Length == 0)
                    {
                        // Set a new article
                        if (int.Parse(thread["articles_remaining"]) == 0)
                            // Delete the thread
                            finalQuery.Append("DELETE FROM articles_thread WHERE threadid='" + Utils.Escape(article["threadid"]) + "';");
                        else
                            // Set a new article
                            finalQuery.Append("UPDATE articles_thread SET articleid_current=(SELECT articleid FROM articles WHERE published='1' AND threadid='" + Utils.Escape(article["threadid"]) + "' ORDER BY articleid DESC LIMIT 1) WHERE threadid='" + Utils.Escape(article["threadid"]) + "';");
                    }
                    // Append tags cleanup query
                    finalQuery.Append(QUERY_TAGS_CLEANUP);
                    // Finish up
                    conn.Query_Execute(finalQuery.ToString());
                    conn.Disconnect();
                    response.Redirect(pageElements["URL"] + "/articles", true);
                }
            }
            // Display form
            if (error != null)
                content.Append(
                    Core.templates[pageElements["TEMPLATE"]]["error"].Replace("<ERROR>", HttpUtility.HtmlEncode(error))
                    );
            content.Append(
                Core.templates["articles"]["article_delete"]
                .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(article["articleid"]))
            );
        }
        public static void pageArticle_View_History(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
        {
            // Setup the page being viewed
            int page;
            string rawPage = request.QueryString["apg"];
            if (rawPage == null || !int.TryParse(rawPage, out page) || page < 1) page = 1;
            // Append header
            content.Append(
                Core.templates["articles"]["history_header"]
                );
            // Grab the current selected article
            string currentArticleID = (conn.Query_Scalar("SELECT articleid_current FROM articles_thread WHERE threadid='" + Utils.Escape(article["threadid"]) + "'") ?? string.Empty).ToString();
            // Append each article revision
            int historyPerPage = Core.settings[SETTINGS_KEY].getInt(SETTINGS_HISTORY_PER_PAGE);
            Result articles = conn.Query_Read("SELECT a.*, u.username, u2.username AS author FROM articles AS a LEFT OUTER JOIN bsa_users AS u ON u.userid=a.moderator_userid LEFT OUTER JOIN bsa_users AS u2 ON u2.userid=a.userid WHERE a.threadid='" + Utils.Escape(article["threadid"]) + "' ORDER BY a.articleid DESC LIMIT " + ((historyPerPage * page) - historyPerPage) + "," + historyPerPage);
            foreach (ResultRow a in articles)
            {
                content.Append(
                    Core.templates["articles"]["history_row"]
                    .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(a["articleid"]))
                    .Replace("%SELECTED%", a["articleid"] == currentArticleID ? "SELECTED" : string.Empty)
                    .Replace("%TITLE%", HttpUtility.HtmlEncode(a["title"]))
                    .Replace("%PUBLISHED%", a["published"].Equals("1") ? "Published by " + HttpUtility.HtmlEncode(a["username"]) : "Pending publication.")
                    .Replace("%DATETIME%", a["datetime"].Length > 0 ? a["datetime"] : "Unknown")
                    .Replace("%DATETIME_SHORT%", a["datetime"].Length > 0 ? Misc.Plugins.getTimeString(DateTime.Parse(a["datetime"])) : "Unknown")
                    .Replace("%CREATOR_USERID%", HttpUtility.HtmlEncode(a["userid"]))
                    .Replace("%CREATOR%", HttpUtility.HtmlEncode(a["author"]))
                    );
            }
            // Append navigator
            content.Append(
                Core.templates["articles"]["page_nav"]
                .Replace("%ARTICLEID%", article["articleid"])
                .Replace("%SUBPAGE%", "history")
                .Replace("%PAGE%", page.ToString())
                .Replace("%PAGE_PREVIOUS%", (page > 1 ? page - 1 : 1).ToString())
                .Replace("%PAGE_NEXT%", (page < int.MaxValue ? page + 1 : int.MaxValue).ToString())
                );
            // Set navigator flags
            if (page > 1)
                pageElements.setFlag("ARTICLE_PAGE_PREVIOUS");
            if (page < int.MaxValue && articles.Rows.Count == historyPerPage)
                pageElements.setFlag("ARTICLE_PAGE_NEXT");
        }
        public static void pageArticle_View_Publish(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
        {
            if (request.Form["confirm"] != null)
            {
                StringBuilder publishQuery = new StringBuilder();
                publishQuery.Append("UPDATE articles SET published='1', moderator_userid='")
                .Append(Utils.Escape(HttpContext.Current.User.Identity.Name)).Append("' WHERE articleid='")
                .Append(Utils.Escape(article["articleid"])).Append("'; UPDATE articles_thread SET articleid_current='")
                .Append(Utils.Escape(article["articleid"])).Append("' WHERE threadid='")
                .Append(Utils.Escape(article["threadid"])).Append("';")
                .Append(insertEvent(RecentChanges_EventType.Published, HttpContext.Current.User.Identity.Name, article["articleid"], article["threadid"]));
                conn.Query_Execute(publishQuery.ToString());
                conn.Disconnect();
                response.Redirect(pageElements["URL"] + "/article/" + article["articleid"]);
            }
            content.Append(
                Core.templates["articles"]["article_publish"]
                .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(article["articleid"]))
                );
        }
        public static void pageArticle_View_Set(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
        {
            conn.Query_Execute("UPDATE articles_thread SET articleid_current='" + Utils.Escape(article["articleid"]) + "' WHERE threadid='" + Utils.Escape(article["threadid"]) + "';" + insertEvent(RecentChanges_EventType.SetAsSelected, HttpContext.Current.User.Identity.Name, article["articleid"], article["threadid"]));
            conn.Disconnect();
            response.Redirect(pageElements["URL"] + "/article/" + article["articleid"], true);
        }
        public static void pageArticle_View_Rebuild(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
        {
            if (!permPublish) return;
            StringBuilder cached = new StringBuilder(article["body"]);
            articleViewRebuildCache(conn, ref cached, article["allow_html"].Equals("1"), ref pageElements);
            conn.Query_Execute("UPDATE articles SET body_cached='" + Utils.Escape(cached.ToString()) + "' WHERE articleid='" + Utils.Escape(article["articleid"]) + "';" + insertEvent(RecentChanges_EventType.RebuiltArticleCache, HttpContext.Current.User.Identity.Name, article["articleid"], article["threadid"]));
            conn.Disconnect();
            response.Redirect(pageElements["URL"] + "/article/" + article["articleid"], true);
        }
        static void articleViewRebuildCache(Connector conn, ref StringBuilder text, bool allowHTML, ref Misc.PageElements pageElements)
        {
            if (!allowHTML)
                text.Replace("<", "&lt;").Replace(">", "&gt;");
            articleFormat(conn, ref text, ref pageElements, true, true);
        }
        #endregion

        #region "Methods - Misc"
        /// <summary>
        /// Validates a URL is relative; path should be in the format of e.g.:
        /// path
        /// path/path
        /// path/path/path
        /// 
        /// Allowed characters:
        /// - Alpha-numeric
        /// - Under-scroll
        /// - Dot/period
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string validRelativeUrl(string text, int relativeUrlMaxChunks, int relativeUrlChunkMin, int relativeUrlChunkMax)
        {
            if (text == null) return "Invalid relative path!";
            else if (text.Length == 0) return "No relative URL specified!";
            else if (text.StartsWith("/")) return "Relative path cannot start with '/'...";
            else if (text.EndsWith("/")) return "Relative path cannot end with '/'...";
            string[] chunks = text.Split('/');
            if (chunks.Length > relativeUrlMaxChunks) return "Max top-directories in relative-path exceeded!";
            foreach (string s in chunks)
            {
                if (s.Length < relativeUrlChunkMin || s.Length > relativeUrlChunkMax)
                    return "Relative URL folder '" + s + "' must be " + relativeUrlChunkMin + " to " + relativeUrlChunkMax + " characters in size!";
                else
                    foreach (char c in s)
                    {
                        if ((c < 48 && c > 57) && (c < 65 && c > 90) && (c < 97 && c > 122) && c != 95 && c != 46)
                            return "Invalid character '" + c + "'!";
                    }
            }
            return null;
        }
        /// <summary>
        /// Returns the SQL query for inserting a new event.
        /// </summary>
        /// <param name="eventType">The type of event.</param>
        /// <param name="userid">The user's identifier.</param>
        /// <param name="articleid">The ID of the article involved; can be null.</param>
        /// <param name="data">Relevant additional data; this field can be null.</param>
        /// <returns></returns>
        public static string insertEvent(RecentChanges_EventType eventType, string userid, string articleid, string threadid)
        {
            return "INSERT INTO articles_log_events (event_type, userid, datetime, articleid, threadid) VALUES('" + (int)eventType + "', '" + Utils.Escape(userid) + "', NOW(), " + (articleid == null ? "NULL" : "'" + Utils.Escape(articleid) + "'") + ", " + (threadid == null ? "NULL" : "'" + Utils.Escape(threadid) + "'") + ");";
        }
        /// <summary>
        /// Grabs image data from a stream, compresses the image and returns it as a byte array ready for storage.
        /// </summary>
        /// <param name="dataStream">The IO stream containing the image data.</param>
        /// <param name="maxWidth">The maximum width of the image allowed.</param>
        /// <param name="maxHeight">The maximum height of the image allowed.</param>
        /// <returns></returns>
        public static byte[] compressImageData(Stream dataStream, int maxWidth, int maxHeight)
        {
            try
            {
                // Resize the image within the constraints of the maximum size
                Image image = Image.FromStream(dataStream);
                int newWidth;
                int newHeight;
                if (image.Width < maxWidth && image.Height < maxHeight)
                {
                    // We won't bother with any transformations, we'll just draw a new compressed image instead
                    newWidth = image.Width;
                    newHeight = image.Height;
                }
                else
                {
                    // We'll need to transform the size to fit the maximum size bounds
                    if (image.Width > maxWidth)
                    {
                        newWidth = maxWidth;
                        newHeight = (int)((double)image.Height / ((double)image.Width / (double)maxWidth));
                    }
                    else
                    {
                        newHeight = maxHeight;
                        newWidth = (int)((double)image.Width / ((double)image.Height / (double)maxHeight));
                    }
                }
                // Draw the new image with high-quality compression
                Bitmap compressedImage = new Bitmap(newWidth, newHeight);
                Graphics g = Graphics.FromImage(compressedImage);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.DrawImage(image, 0, 0, newWidth, newHeight);
                g.Dispose();
                g = null;
                image.Dispose();
                image = null;
                // Write the data to a byte array
                MemoryStream ms = new MemoryStream();
                compressedImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                compressedImage.Dispose();
                compressedImage = null;
                byte[] thumbRawData = ms.ToArray();
                ms.Dispose();
                ms = null;
                // Return the compressed data
                return thumbRawData;
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region "Methods & Classes - Article Tags"

        /// <summary>
        /// Parses a string such as "web,cms,ubermeat" (without quotations) to extract tags; the return structure
        /// will contain an array of successfully parsed tags; however if an error occurs, the error variable
        /// is set and the process is aborted.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static ArticleTags getTags(string tags, int tagsTitleMin, int tagsTitleMax, int tagsMax)
        {
            // Initialize return struct
            ArticleTags tagCollection = new ArticleTags();
            tagCollection.error = null;
            tagCollection.tags = new List<string>();
            // Parse the tags and try to find an error
            string tag;
            foreach (string rawTag in tags.Split(','))
            {
                tag = rawTag.Trim();
                if (tag.Length != 0)
                {
                    if (tag.Length < tagsTitleMin || tag.Length > tagsTitleMax)
                    {
                        tagCollection.error = "Invalid tag '" + tag + "' - must be between " + tagsTitleMin + " to " + tagsTitleMax + " characters!";
                        break;
                    }
                    else if (tagCollection.tags.Count + 1 > tagsMax)
                        tagCollection.error = "Maximum tags of " + tagsMax + " exceeded!";
                    else if(!tagCollection.tags.Contains(tag))
                        tagCollection.tags.Add(tag);
                }
            }
            return tagCollection;
        }
        public struct ArticleTags
        {
            public List<string> tags;
            public string error;
        }
        #endregion

        #region "Methods - Formatting"
        /// <summary>
        /// Adds a formatting provider for rendering the article's text.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="pluginid"></param>
        /// <param name="classpath"></param>
        /// <param name="method"></param>
        public static void formattingAdd(Connector conn, string pluginid, string classpath, string method)
        {
            try
            {
                conn.Query_Execute("INSERT INTO articles_format_providers (classpath, method, pluginid) VALUES('" + Utils.Escape(classpath) + "', '" + Utils.Escape(method) + "', '" + Utils.Escape(pluginid) + "')");
            }
            catch { }
        }
        /// <summary>
        /// Removes a formatting provider based on classpath and method.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="classpath"></param>
        /// <param name="method"></param>
        public static void formattingRemove(Connector conn, string classpath, string method)
        {
            conn.Query_Execute("DELETE FROM articles_format_providers WHERE classpath='" + Utils.Escape(classpath) + "' AND method='" + Utils.Escape(method) + "'");
        }
        /// <summary>
        /// Removes a formatting provider based on pluginid.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="pluginid"></param>
        public static void formattingRemove(Connector conn, string pluginid)
        {
            conn.Query_Execute("DELETE FROM articles_format_providers WHERE pluginid='" + Utils.Escape(pluginid) + "'");
        }
        /// <summary>
        /// Used to format articles
        /// </summary>
        /// <param name="originalText"></param>
        /// <param name="pageElements"></param>
        /// <param name="textFormatting"></param>
        /// <param name="objectFormatting"></param>
        public static void articleFormat(Connector conn, ref StringBuilder originalText, ref Misc.PageElements pageElements, bool textFormatting, bool objectFormatting)
        {
            articleFormatProvider(conn, ref originalText, ref pageElements, textFormatting, objectFormatting, 0);
        }
        private static void articleFormatProvider(Connector conn, ref StringBuilder originalText, ref Misc.PageElements pageElements, bool textFormatting, bool objectFormatting, int currTree)
        {
            if ((++currTree) > 5) return; // We've reached the maximum of five recursions, time to return home
            // Invoke other providers
            foreach (ResultRow formatProvider in conn.Query_Read("SELECT classpath, method FROM articles_format_providers ORDER BY method ASC"))
                Misc.Plugins.invokeMethod(formatProvider["classpath"], formatProvider["method"], new object[] { conn, originalText, pageElements, true, true });
            // Invoke local providers
            formatImage(ref originalText, ref pageElements);
            formatTemplate(conn, ref originalText, ref pageElements, currTree);
        }
        public static void formatImage(ref StringBuilder text, ref Misc.PageElements pageElements)
        {
            foreach (Match m in Regex.Matches(text.ToString(), REGEX_IMAGE_STORE, RegexOptions.Multiline))
                text.Replace(m.Value, "<a title=\"Click to view the image...\" href=\"" + pageElements["URL"] + "/articles/images/view/" + m.Groups[1].Value + "\" class=\"COMMON_IMG\"><img src=\"" + pageElements["URL"] + "/articles/images/data/" + m.Groups[1].Value + "\" /></a>");
            foreach (Match m in Regex.Matches(text.ToString(), REGEX_IMAGE_STORE_CUSTOM_W, RegexOptions.Multiline))
                text.Replace(m.Value, "<a title=\"Click to open the image...\" href=\"" + pageElements["URL"] + "/articles/images/view/" + m.Groups[3].Value + "\" class=\"COMMON_IMG\"><img style=\"max-width: " + m.Groups[1].Value + ";\" src=\"" + pageElements["URL"] + "/articles/images/data/" + m.Groups[2].Value + "\" /></a>");
            foreach (Match m in Regex.Matches(text.ToString(), REGEX_IMAGE_STORE_CUSTOM_WH, RegexOptions.Multiline))
                text.Replace(m.Value, "<a title=\"Click to open the image...\" href=\"" + pageElements["URL"] + "/articles/images/view/" + m.Groups[3].Value + "\" class=\"COMMON_IMG\"><img style=\"width: " + m.Groups[1].Value + "; height: " + m.Groups[2].Value + ";\" src=\"" + pageElements["URL"] + "/articles/images/data/" + m.Groups[3].Value + "\" /></a>");
        }
        public static void formatTemplate(Connector conn, ref StringBuilder text, ref Misc.PageElements pageElements, int currTree)
        {
            Result data;
            StringBuilder body;
            string[] param;
            string[] innerParam;
            foreach (Match m in Regex.Matches(text.ToString(), @"\[\[([a-zA-Z0-9\/\._\-|= \(\)]+)\]\]", RegexOptions.Multiline))
            {
                param = m.Groups[1].Value.Split('|');
                data = conn.Query_Read("SELECT a.body FROM articles_thread AS at LEFT OUTER JOIN articles AS a ON a.articleid=at.articleid_current WHERE at.relative_url='" + Utils.Escape(param[0]) + "'");
                if (data.Rows.Count != 1)
                    // Template doesn't exist - replace the markup with an error message
                    text.Replace(m.Value, "<div class=\"COMMON_TEMPLATE_FAILURE\">Failed to embed template located at '" + Utils.Escape(m.Groups[1].Value) + "'.../div>");
                else
                {
                    body = new StringBuilder(data[0]["body"]);
                    // Replace params
                    if (param.Length > 1)
                    {
                        for (int i = 1; i < param.Length; i++)
                        {
                            innerParam = param[i].Split('=');
                            if (innerParam.Length == 1)
                                body.Replace("{{" + i + "}}", param[i]);
                            else if (innerParam.Length == 2)
                                body.Replace("{{" + innerParam[0] + "}}", innerParam[1]);
                        }
                    }
                    // Format template
                    articleFormatProvider(conn, ref body, ref pageElements, true, true, currTree);
                    text.Replace(m.Value, body.ToString());
                }
            }
        }
        #endregion
    }
}