using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using UberLib.Connector;
using System.Drawing;
using System.IO;

namespace UberCMS.Plugins
{
    public static class Articles
    {
        public const int TITLE_MAX = 45;
        public const int TITLE_MIN = 1;
        public const int BODY_MIN = 1;
        public const int BODY_MAX = 8000; // Consider making this a setting
        public const int RELATIVE_URL_CHUNK_MIN = 1;
        public const int RELATIVE_URL_CHUNK_MAX = 32;
        public const int RELATIVE_URL_MAXCHUNKS = 8;
        public const int TAGS_TITLE_MIN = 1;
        /// <summary>
        /// Remember to update the SQL; keywords are varchars set to this max char length.
        /// </summary>
        public const int TAGS_TITLE_MAX = 30;
        public const int TAGS_MAX = 20;
        public const int THUMBNAIL_MAXSIZE = 2097152; // Two megabytes
        public const int THUMBNAIL_MAXWIDTH = 240;
        public const int THUMBNAIL_MAXHEIGHT = 180;
        public const int COMMENTS_LENGTH_MIN = 2;
        public const int COMMENTS_LENGTH_MAX = 512;
        public const int COMMENTS_MAX_PER_HOUR = 8;
        public const int COMMENTS_PER_PAGE = 5;
        public const int HISTORY_PER_PAGE = 10;
        public const int PENDING_PER_PAGE = 10;
        public const int ARTICLES_EDIT_PER_HOUR = 4;
        public const int ARTICLES_EDIT_PER_DAY = 10;

        public const string SETTINGS_KEY = "articles";
        public const string SETTINGS_KEY_HANDLES_404 = "handles_404";

        /// <summary>
        /// When an article is modified or deleted, some of the tags may not be in-use by any other articles - therefore we can remove them with the below cleanup query.
        /// </summary>
        public const string QUERY_TAGS_CLEANUP = "DELETE FROM articles_tags WHERE NOT EXISTS (SELECT DISTINCT tagid FROM articles_tags_article WHERE tagid=articles_tags.tagid);";
        /// <summary>
        /// When an article (thread) is edited/deleted, the following query is used to cleanup any thumbnails no longer in-use; this structure is far more efficient for storage.
        /// </summary>
        public const string QUERY_THUMBNAIL_CLEANUP = "DELETE FROM articles_thumbnails WHERE NOT EXISTS (SELECT DISTINCT thumbnailid FROM articles WHERE thumbnailid=articles_thumbnails.thumbnailid);";

        #region "Methods - Plugin Event Handlers"
        public static string enable(string pluginid, Connector conn)
        {
            string basePath = Misc.Plugins.getPluginBasePath(pluginid, conn);
            string error = null;
            // Reserve URLS
            if ((error = Misc.Plugins.reserveURLs(pluginid, null, new string[] { "article", "articles" }, conn)) != null)
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
            // Unreserve all URLs
            if((error = Misc.Plugins.unreserveURLs(pluginid, conn)) != null)
                return error;
            // Uninstall SQL
            if ((error = Misc.Plugins.executeSQL(basePath + "\\SQL\\Uninstall.sql", conn)) != null)
                return error;
            // Remove settings
            Core.settings.removeCategory(conn, SETTINGS_KEY);

            return null;
        }
        public static void handleRequest(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            switch (request.QueryString["page"])
            {
                case "article":
                    pageArticle(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "articles":
                    pageArticles(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                default:
                    pageArticle_Editor(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
            }
        }
        public static void handleRequestNotFound(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            if(Core.settings[SETTINGS_KEY].getBool(SETTINGS_KEY_HANDLES_404))
                pageArticle_View(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
        }
        #endregion

        #region "Methods - Page Handlers"
        public static void pageArticles(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            switch (request.QueryString["1"])
            {
                case null:
                    pageArticles_Browse(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "delete":
                    pageArticles_Delete(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "pending":
                    pageArticles_Pending(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
            }
        }
        public static void pageArticle(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            switch (request.QueryString["1"])
            {
                default:
                    pageArticle_View(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "create":
                case "editor":
                    pageArticle_Editor(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
                case "thumbnail":
                    pageArticle_Thumbnail(pluginid, conn, ref pageElements, request, response, ref baseTemplateParent);
                    break;
            }
        }
        #endregion

        #region "Methods - Pages"
        public static void pageArticles_Browse(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
        }
        public static void pageArticles_Pending(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Check the user has publishing permissions
            if (!HttpContext.Current.User.Identity.IsAuthenticated || !conn.Query_Scalar("SELECT ug.access_media_publish FROM bsa_users AS u LEFT OUTER JOIN bsa_user_groups AS ug ON ug.groupid=u.groupid WHERE u.userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "'").ToString().Equals("1"))
                return;
            // Get the current page
            int page;
            if(!int.TryParse(request.QueryString["pg"], out page) || page < 1) page = 1;
            // Build a list of pending articles
            StringBuilder articlesPending = new StringBuilder();
            Result pending = conn.Query_Read("SELECT a.articleid, a.title, u.username, a.userid, a.datetime, a.allow_html FROM articles AS a LEFT OUTER JOIN bsa_users AS u ON u.userid=a.userid WHERE a.published='0' ORDER BY a.datetime ASC LIMIT " + ((page * PENDING_PER_PAGE) - PENDING_PER_PAGE) + "," + PENDING_PER_PAGE);
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
            if (page < int.MaxValue && pending.Rows.Count == PENDING_PER_PAGE) pageElements.setFlag("ARTICLE_PAGE_NEXT");
            // Output the page
            Misc.Plugins.addHeaderCSS(pageElements["URL"] + "/Content/CSS/Article.css", ref pageElements);
            pageElements["CONTENT"] = Core.templates["articles"]["articles_pending"]
                .Replace("%PENDING%", articlesPending.ToString())
                ;
            pageElements["TITLE"] = "Articles - Pending";
        }
        /// <summary>
        /// Used to create/modify an article.
        /// </summary>
        /// <param name="pluginid"></param>
        /// <param name="conn"></param>
        /// <param name="pageElements"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="baseTemplateParent"></param>
        public static void pageArticle_Editor(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
        {
            // Check the user is logged-in, else redirect to the login page
            if (!HttpContext.Current.User.Identity.IsAuthenticated)
                response.Redirect(pageElements["URL"] + "/login", true);

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
                if (title.Length < TITLE_MIN || title.Length > TITLE_MAX)
                    error = "Title must be " + TITLE_MIN + " to " + TITLE_MAX + " characters in length!";
                else if (body.Length < BODY_MIN || body.Length > BODY_MAX)
                    error = "Body must be " + BODY_MIN + " to " + BODY_MAX + " characters in length!";
                else if (thumbnail != null && thumbnail.ContentLength > THUMBNAIL_MAXSIZE)
                    error = "Thumbnail cannot exceed " + THUMBNAIL_MAXSIZE + " bytes (" + Misc.Plugins.getBytesString(THUMBNAIL_MAXSIZE) + ")!";
                else if (thumbnail != null && thumbnail.ContentLength > 0 && thumbnail.ContentType != "image/gif" && thumbnail.ContentType != "image/jpeg" && thumbnail.ContentType != "image/png" && thumbnail.ContentType != "image/jpg")
                    error = "Invalid thumbnail image format!";
                else if ((error = validRelativeUrl(relativeUrl)) != null)
                    ;
                else
                {
                    // Verify tags
                    ArticleTags parsedTags = getTags(tags);
                    if (parsedTags.error != null) error = parsedTags.error;
                    else
                    {
                        // Check if we're inserting, else perhaps inheriting, a thumbnail
                        string thumbnailid = null;
                        if (thumbnail != null && thumbnail.ContentLength > 0)
                        {
                            try
                            {
                                // Compress the image
                                Image image = Image.FromStream(thumbnail.InputStream);
                                int newWidth;
                                int newHeight;
                                int maxWidth = THUMBNAIL_MAXWIDTH;
                                int maxHeight = THUMBNAIL_MAXHEIGHT;
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
                                Bitmap compressedImage = new Bitmap(newWidth, newHeight);
                                Graphics g = Graphics.FromImage(compressedImage);
                                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                g.DrawImage(image, 0, 0, newWidth, newHeight);
                                g.Dispose();
                                g = null;
                                image.Dispose();
                                image = null;
                                MemoryStream ms = new MemoryStream();
                                compressedImage.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                                compressedImage.Dispose();
                                compressedImage = null;
                                byte[] thumbRawData = ms.ToArray();
                                ms.Dispose();
                                ms = null;
                                // Insert thumbnail and get thumbnailid
                                Dictionary<string, object> thumbParams = new Dictionary<string, object>();
                                thumbParams.Add("thumb", thumbRawData);
                                thumbnailid = conn.Query_Scalar_Parameters("INSERT INTO articles_thumbnails (data) VALUES(@thumb); SELECT LAST_INSERT_ID();", thumbParams).ToString();
                            }
                            catch
                            {
                                error = "Failed to compress thumbnail image, please try again or report this to the site administrator!";
                            }
                        }
                        else if (inheritThumbnail && preDataRow != null && preDataRow["thumbnailid"].Length != 0)
                        {
                            // Grab pre-existing thumbnailid
                            thumbnailid = preDataRow["thumbnailid"];
                        }
                        // Ensure no thumbnail processing errors occur, else do not continue
                        if (error == null)
                        {
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
                                    .Append("', allow_comments='").Append(allowComments ? "1" : "0")
                                    .Append("', allow_html='").Append(allowHTML ? "1" : "0")
                                    .Append("', show_pane='").Append(showPane).Append("' WHERE articleid='").Append(articleid).Append("';");
                                // Delete the previous tags
                                query.Append("DELETE FROM articles_tags_article WHERE articleid='" + Utils.Escape(articleid) + "';");
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
                                    .Append("INSERT INTO articles (threadid, title, userid, body, moderator_userid, published, allow_comments, allow_html, show_pane, thumbnailid, datetime) VALUES('")
                                    .Append(Utils.Escape(threadid))
                                    .Append("', '").Append(Utils.Escape(title))
                                    .Append("', '").Append(Utils.Escape(HttpContext.Current.User.Identity.Name))
                                    .Append("', '").Append(Utils.Escape(body))
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
                            // -- This will delete any tags in the main table no longer used in the articles tags table
                            finalQuery.Append(QUERY_TAGS_CLEANUP);
                            // -- This will delete any unused thumbnail images
                            finalQuery.Append(QUERY_THUMBNAIL_CLEANUP);
                            // -- Execute final query
                            conn.Query_Execute(finalQuery.ToString());
                            // Redirect to the new article
                            conn.Disconnect();
                            response.Redirect(pageElements["URL"] + "/article/" + articleid, true);
                        }
                    }
                }
            }
            // Display form
            pageElements["CONTENT"] = Core.templates["articles"]["editor"]
                .Replace("%ERROR%", error != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error)) : string.Empty)
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
            pageElements["TITLE"] = "Articles - Editor";
        }
        public static void pageArticles_Delete(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
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
                    conn.Query_Execute("DELETE FROM articles_thread WHERE threadid='" + Utils.Escape(threadid) + "'; " + QUERY_TAGS_CLEANUP + QUERY_THUMBNAIL_CLEANUP);
                    // Redirect to articles home
                    conn.Disconnect();
                    response.Redirect(pageElements["URL"] + "/articles");
                }
            }
            // Display confirmation/security-verification form
            pageElements["CONTENT"] = Core.templates["articles"]["thread_delete"]
                .Replace("%THREADID%", HttpUtility.HtmlEncode(threadData[0]["threadid"]))
                .Replace("%CSRF%", HttpUtility.HtmlEncode(Common.AntiCSRF.getFormToken()))
                .Replace("%TITLE%", HttpUtility.HtmlEncode(threadData[0]["title"]))
                .Replace("%ARTICLE_COUNT%", HttpUtility.HtmlEncode(threadData[0]["article_count"]))
                .Replace("%RELATIVE_URL%", HttpUtility.HtmlEncode(threadData[0]["relative_url"]))
                ;
            pageElements["TITLE"] = "Articles - Delete Thread";
        }
        public static byte[] pageArticle_Thumbnail_Unknown = null;
        public static void pageArticle_Thumbnail(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
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
        /// <param name="baseTemplateParent"></param>
        public static void pageArticle_View(string pluginid, Connector conn, ref Misc.PageElements pageElements, HttpRequest request, HttpResponse response, ref string baseTemplateParent)
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
                for (int i = 1; i <= RELATIVE_URL_MAXCHUNKS; i++)
                {
                    chunk = request.QueryString[i.ToString()];
                    if (chunk != null)
                    {
                        if (chunk.Length > RELATIVE_URL_CHUNK_MAX)
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
            Result articleRaw = conn.Query_Read("SELECT (SELECT COUNT('') FROM articles WHERE threadid=a.threadid AND articleid <= a.articleid ORDER BY articleid ASC) AS revision, (SELECT ac.allow_comments FROM articles_thread AS act LEFT OUTER JOIN articles AS ac ON ac.articleid=act.articleid_current WHERE act.threadid=at.threadid) AS allow_comments_thread, a.articleid, a.threadid, a.title, a.userid, a.body, a.moderator_userid, a.published, a.allow_comments, a.allow_html, a.show_pane, a.datetime, at.relative_url, at.articleid_current, u.username FROM (articles AS a, articles_thread AS at) LEFT OUTER JOIN bsa_users AS u ON u.userid=a.userid WHERE a.articleid='" + Utils.Escape(articleid) + "' AND at.threadid=a.threadid");
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
                        pageArticle_View_Publish(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref baseTemplateParent, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    case "delete":
                        // An owner of an unpublished article can delete it
                        if (!permDelete && !(owner && !published)) return; // Check the user has sufficient permission
                        pageArticle_View_Delete(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref baseTemplateParent, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    case "history":
                        pageArticle_View_History(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref baseTemplateParent, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    case "comments":
                        pageArticle_View_Comments(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref baseTemplateParent, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    case "set":
                        if (!permPublish || !published) return;
                        pageArticle_View_Set(ref pluginid, ref conn, ref pageElements, ref request, ref response, ref baseTemplateParent, ref permCreate, ref permDelete, ref permPublish, ref owner, ref subpageContent, ref article);
                        break;
                    default:
                        return; // 404 - unknown sub-page
                }
                content.Replace("%BODY%", subpageContent.ToString());
            }
            else
            {
                subpageContent.Append(article["body"]);
                // Render the article with bbcode
                Common.BBCode.format(ref subpageContent, ref pageElements, true, true);
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
                    .Append(Core.templates["articles"]["article_tags"].Replace("%TAGS%", tags.ToString()));
            }

            // Add pane
            content
                .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(article["articleid"]))
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
        }
        public static void pageArticle_View_Comments(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref string baseTemplateParent, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
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
                else if (commentBody.Length < COMMENTS_LENGTH_MIN || commentBody.Length > COMMENTS_LENGTH_MAX)
                    commentError = "Your comment must be " + COMMENTS_LENGTH_MIN + " to  " + COMMENTS_LENGTH_MAX + " in length!";
                else if (conn.Query_Count("SELECT COUNT('') FROM articles_thread_comments WHERE userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "' AND datetime >= DATE_SUB(NOW(), INTERVAL 1 HOUR)") >= COMMENTS_MAX_PER_HOUR)
                    commentError = "You've already posted the maximum of " + COMMENTS_MAX_PER_HOUR + " comments per an hour - try again later!";
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
            Result commentsData = conn.Query_Read("SELECT atc.*, u.username FROM articles_thread_comments AS atc LEFT OUTER JOIN bsa_users AS u ON u.userid=atc.userid WHERE threadid='" + Utils.Escape(article["threadid"]) + "' ORDER BY datetime DESC LIMIT " + ((COMMENTS_PER_PAGE * commentsPage) - COMMENTS_PER_PAGE) + "," + COMMENTS_PER_PAGE);
            // -- Build the data
            if (commentsData.Rows.Count == 0)
                content.Append(Core.templates["articles"]["comments_empty"]);
            else
                foreach (ResultRow comment in commentsData)
                {
                    content.Append(
                        (HttpContext.Current.User.Identity.IsAuthenticated && (permDelete || HttpContext.Current.User.Identity.Name == comment["userid"]) ? Core.templates["articles"]["comment_delete"] : Core.templates["articles"]["comment"])
                        .Replace("%USERID%", comment["userid"])
                        .Replace("%ARTICLEID%", article["articleid"])
                        .Replace("%COMMENTID%", comment["commentid"])
                        .Replace("%USERNAME%", HttpUtility.HtmlEncode(comment["username"]))
                        .Replace("%DATETIME%", HttpUtility.HtmlEncode(comment["datetime"]))
                        .Replace("%BODY%", HttpUtility.HtmlEncode(comment["message"]))
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
            if (commentsData.Rows.Count == COMMENTS_PER_PAGE)
                pageElements.setFlag("ARTICLE_PAGE_NEXT");
            // Set the postbox
            if (HttpContext.Current.User.Identity.IsAuthenticated && allowComments)
                content.Append(
                        Core.templates["articles"]["comments_postbox"]
                    .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(article["articleid"]))
                    .Replace("%SUBPAGE%", "history")
                    .Replace("%ERROR%", commentError != null ? Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", commentError) : string.Empty)
                    .Replace("%COMMENT_BODY%", HttpUtility.HtmlEncode(commentBody))
                    );
        }
        public static void pageArticle_View_Delete(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref string baseTemplateParent, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
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
                    conn.Query_Execute("DELETE FROM articles WHERE articleid='" + Utils.Escape(article["articleid"]) + "'");
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
                    Core.templates[baseTemplateParent]["error"].Replace("%ERROR%", HttpUtility.HtmlEncode(error))
                    );
            content.Append(
                Core.templates["articles"]["article_delete"]
                .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(article["articleid"]))
            );
        }
        public static void pageArticle_View_History(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref string baseTemplateParent, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
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
            Result articles = conn.Query_Read("SELECT a.*, u.username, u2.username AS author FROM articles AS a LEFT OUTER JOIN bsa_users AS u ON u.userid=a.moderator_userid LEFT OUTER JOIN bsa_users AS u2 ON u2.userid=a.userid WHERE a.threadid='" + Utils.Escape(article["threadid"]) + "' ORDER BY a.articleid DESC LIMIT " + ((HISTORY_PER_PAGE * page) - HISTORY_PER_PAGE) + "," + HISTORY_PER_PAGE);
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
            if (page < int.MaxValue && articles.Rows.Count == HISTORY_PER_PAGE)
                pageElements.setFlag("ARTICLE_PAGE_NEXT");
        }
        public static void pageArticle_View_Publish(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref string baseTemplateParent, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
        {
            if (request.Form["confirm"] != null)
            {
                conn.Query_Execute("UPDATE articles SET published='1', moderator_userid='" + Utils.Escape(HttpContext.Current.User.Identity.Name) + "' WHERE articleid='" + Utils.Escape(article["articleid"]) + "'; UPDATE articles_thread SET articleid_current='" + Utils.Escape(article["articleid"]) + "' WHERE threadid='" + Utils.Escape(article["threadid"]) + "';");
                conn.Disconnect();
                response.Redirect(pageElements["URL"] + "/article/" + article["articleid"]);
            }
            content.Append(
                Core.templates["articles"]["article_publish"]
                .Replace("%ARTICLEID%", HttpUtility.HtmlEncode(article["articleid"]))
                );
        }
        public static void pageArticle_View_Set(ref string pluginid, ref Connector conn, ref Misc.PageElements pageElements, ref HttpRequest request, ref HttpResponse response, ref string baseTemplateParent, ref bool permCreate, ref bool permDelete, ref bool permPublish, ref bool owner, ref StringBuilder content, ref ResultRow article)
        {
            conn.Query_Execute("UPDATE articles_thread SET articleid_current='" + Utils.Escape(article["articleid"]) + "' WHERE threadid='" + Utils.Escape(article["threadid"]) + "'");
            conn.Disconnect();
            response.Redirect(pageElements["URL"] + "/article/" + article["articleid"], true);
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
        public static string validRelativeUrl(string text)
        {
            if (text == null) return "Invalid relative path!";
            else if (text.Length == 0) return "No relative URL specified!";
            else if (text.StartsWith("/")) return "Relative path cannot start with '/'...";
            else if (text.EndsWith("/")) return "Relative path cannot end with '/'...";
            string[] chunks = text.Split('/');
            if (chunks.Length > RELATIVE_URL_MAXCHUNKS) return "Max top-directories in relative-path exceeded!";
            foreach (string s in chunks)
            {
                if (s.Length < RELATIVE_URL_CHUNK_MIN || s.Length > RELATIVE_URL_CHUNK_MAX)
                    return "Relative URL folder '" + s + "' must be " + RELATIVE_URL_CHUNK_MIN + " to " + RELATIVE_URL_CHUNK_MAX + " characters in size!";
                else
                    foreach (char c in s)
                    {
                        if ((c < 48 && c > 57) && (c < 65 && c > 90) && (c < 97 && c > 122) && c != 95 && c != 46)
                            return "Invalid character '" + c + "'!";
                    }
            }
            return null;
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
        public static ArticleTags getTags(string tags)
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
                    if (tag.Length < TAGS_TITLE_MIN || tag.Length > TAGS_TITLE_MAX)
                    {
                        tagCollection.error = "Invalid tag '" + tag + "' - must be between " + TAGS_TITLE_MIN + " to " + TAGS_TITLE_MAX + " characters!";
                        break;
                    }
                    else if (tagCollection.tags.Count + 1 > TAGS_MAX)
                        tagCollection.error = "Maximum tags of " + TAGS_MAX + " exceeded!";
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
    }
}