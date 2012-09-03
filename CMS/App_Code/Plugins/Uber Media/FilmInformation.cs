/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber Media
 * File:                    /UberMedia/FilmInformation.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Responsible for retrieving information about media from third-party sources:
 * > IMDB - films
 * > RottenTomatoes - films
 */
using System;
using System.Collections.Generic;
using System.Web;
using UberLib.Connector;
using System.IO;
using System.Net;
using System.Threading;
using System.Text;
#if UBERMEDIA
using Jayrock.Json;
using Jayrock.Json.Conversion;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Core;
#endif

namespace UberCMS.Plugins.UberMedia
{
    /// <summary>
    /// Used, currently, to retrieve a film synopsis by using multiple source providers.
    /// </summary>
    public static class FilmInformation
    {
        #region "Enums"
        public enum State
        {
            Unstarted,
            Starting,
            Started,
            Error
        }
        #endregion

        #region "Constants"
        private const string PROVIDER_IDENTIFIER_IMDB = "IMDB";
        private const string PROVIDER_IDENTIFIER_RT = "RottenTomatoes";
        private const string _IMDB_DownloadDatabaseFile_URL = @"http://ftp.sunet.se/pub/tv+movies/imdb/plot.list.gz";
        private const int _IMDB_DownloadDatabaseFile_Buffer = 4096;
        #endregion

        #region "Variables"
        /// <summary>
        /// Used for checking if we need to keep the application-pool alive for this service, when true.
        /// </summary>
        public static bool serviceIsActive = false;
        public static State state = State.Unstarted;
        public static string status = "Waiting to be started...";
        private static Thread cacheThread = null;
        #endregion

        #region "Methods"
        /// <summary>
        /// Executed every-time the core starts; this will e.g. check an IMDB movies file has been downloaded
        /// and the film-information service is ready.
        /// </summary>
        public static void cacheStart()
        {
            cacheThread = new Thread(cacheStart_Thread);
            cacheThread.Start();
        }
        public static void cacheStop()
        {
            try
            {
                if (cacheThread != null)
                    cacheThread.Abort();
            }
            catch { }
            finally
            {
                cacheThread = null;
            }
        }
        public static void cacheStart_Thread()
        {
            serviceIsActive = true;
            state = State.Starting;
            status = "Checking IMDB provider...";
            // Check if to rebuild the IMDB provider
            Result res = Core.globalConnector.Query_Read("SELECT cache_updated FROM um_film_information_providers WHERE title='" + Utils.Escape(PROVIDER_IDENTIFIER_IMDB) + "'");
            if (res.Rows.Count == 0 || res[0]["cache_updated"].Length == 0)
                // Rebuild the cache if the provider does not exist or the last_updated field is empty (most likely  due to failing)
                cacheBuild_IMDB();
            // Cache built, we're ready to serve requests...if no error occurred
            if (state != State.Error)
            {
                status = "Finished building cache.";
                state = State.Started;
            }
            serviceIsActive = false;
            cacheThread = null;
        }
        public static string getFilmSynopsis(string title, Connector conn)
        {
            // IMDB
            string imdb = getFilmSynopsis__Database(title, conn);
            if (imdb != null)
                return imdb;

            // RottenTomatoes
            string rottenTomatoes = getFilmSynopsis__RottenTomatoes(title, conn);
            if (rottenTomatoes != null)
                return rottenTomatoes;

            // TheMovieDB
            string theMovieDB = null;
            if (theMovieDB != null)
                return theMovieDB;

            return null;
        }
        #endregion

        #region "Methods - Synopsis Providers"
        public static string getFilmSynopsis__Database(string title, Connector conn)
        {
            try
            {
                string newTitle = StripName(title, stripCharsAlphaNumericSpaceApostrophe).Replace(" ", "%") + "%"; // Stripped with only alpha-numeric chars (and also space and ')
                // Grab every possible match and find the closest match using levenshtein algorithm
                Result matches = conn.Query_Read("SELECT title, description FROM um_film_information WHERE title LIKE '" + Utils.Escape(newTitle) + "'");
                if (matches.Rows.Count == 0)
                {
                    newTitle = StripName(title, stripCharsAlphaSpaceApostrophe).Replace(" ", "%") + "%"; // Stripped with the same as the above but with no numeric characters
                    matches = conn.Query_Read("SELECT title, description FROM um_film_information WHERE title LIKE '" + Utils.Escape(newTitle) + "'");
                }
                if (matches.Rows.Count == 0) return null;
                else
                {
                    ResultRow match;
                    int lowestValue = int.MaxValue;
                    int lowestRow = -1;
                    int currValue;
                    for (int i = 0; i < matches.Rows.Count; i++)
                    {
                        match = matches[i];
                        currValue = editDistance(title, match["title"]);
                        // Check if the current row is the closest
                        if (currValue < lowestValue)
                        {
                            lowestValue = currValue;
                            lowestRow = i;
                        }
                    }
                    // Return the synopsis of the lowest
                    if (lowestRow != -1)
                        return matches[lowestRow]["title"] + " - " + matches[lowestRow]["description"];
                    else
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }
        public static string getFilmSynopsis__RottenTomatoes(string title, Connector conn)
        {
#if UBERMEDIA
            if (Core.settings[Base.SETTINGS_KEY][Base.SETTINGS_ROTTEN_TOMATOES_API_KEY].Length == 0) return null;
            try
            {
                System.Threading.Thread.Sleep(100); // To ensure we dont surpass the API calls per a second allowed
                string url = "http://api.rottentomatoes.com/api/public/v1.0/movies.json?q=" + HttpUtility.UrlEncode(title) + "&page_limit=3&page=1&apikey=" + Core.settings[Base.SETTINGS_KEY][Base.SETTINGS_ROTTEN_TOMATOES_API_KEY];
                title = StripName(title, stripCharsAlphaNumericSpaceApostrophe);
                Base.logExternalRequest(conn, "Film data - Rotten Tomatoes", url);
                HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(url);
                req.UserAgent = "Uber Media";
                WebResponse resp = req.GetResponse();
                StreamReader stream = new StreamReader(resp.GetResponseStream());
                string data = stream.ReadToEnd();
                stream.Close();
                resp.Close();
                JsonArray arr = (JsonArray)((JsonObject)JsonConvert.Import(data))["movies"];
                int highest_index = -1;
                bool valid;
                string t, t2;
                for (int i = 0; i < arr.Count; i++)
                {
                    t = StripName(((JsonObject)arr[i])["title"].ToString(), stripCharsAlphaNumericSpaceApostrophe);
                    if (t == title) highest_index = i;
                    else
                    {
                        valid = true;
                        if (highest_index == -1) t2 = title;
                        else t2 = ((JsonObject)arr[highest_index])["title"].ToString();
                        foreach (string word in t2.Split(' ')) if (word.Length > 1 && !t2.Contains(t)) valid = false;
                        if (valid) highest_index = i;
                    }
                }
                if (highest_index == -1) return null;
                else
                {


                    string syn = ((JsonObject)arr[highest_index])["synopsis"].ToString();
                    string returnData = syn.Length > 0 ? syn : ((JsonObject)arr[highest_index])["critics_consensus"].ToString();
                    // Cache the data in the database
                    // -- Attempt to get the provider, else create it
                    int provid;
                    object provRaw = conn.Query_Scalar("SELECT provid FROM um_film_information_providers WHERE title='" + Utils.Escape(PROVIDER_IDENTIFIER_RT) + "'");
                    if (provRaw == null)
                        provid = int.Parse(conn.Query_Scalar("INSERT INTO um_film_information_providers (title) VALUES('" + Utils.Escape(PROVIDER_IDENTIFIER_RT) + "'); SELECT LAST_INSERT_ROW();").ToString());
                    else
                        provid = int.Parse(provRaw.ToString());
                    // -- Insert the data
                    conn.Query_Execute("INSERT INTO um_film_information (title, description, provid) VALUES('" + Utils.Escape(title) + "', '" + Utils.Escape(returnData) + "', '" + provid + "')");
                    return returnData;
                }
            }
            catch
            {
                return null;
            }
#else
            return null;
#endif
        }
        #endregion

        #region "Methods - Synopsis Cache Builders"
        public static void cacheBuild_IMDB()
        {
#if UBERMEDIA
            Connector imdbConn = Core.connectorCreate(true);
            string PATH_CACHE = Core.basePath + "\\Cache";
            string PATH_UNCOMPRESSED = PATH_CACHE + "/imdb.plot.list";
            string PATH_COMPRESSED = PATH_CACHE + "/imdb.plot.list.gz";

            status = "Retrieving provider identifier...";
            int provid;
            try
            {
                // Create provider and return identifier
                object data = imdbConn.Query_Scalar("SELECT provid FROM um_film_information_providers WHERE title='" + Utils.Escape(PROVIDER_IDENTIFIER_IMDB) + "'");
                if (data == null || !int.TryParse(data.ToString(), out provid))
                    // Create provider
                    provid = int.Parse(imdbConn.Query_Scalar("INSERT INTO um_film_information_providers (title) VALUES('" + Utils.Escape(PROVIDER_IDENTIFIER_IMDB) + "'); SELECT LAST_INSERT_ID();").ToString());
                else
                {
                    // Drop all data by provider
                    status = "Dropping all previous IMDB cache data...";
                    imdbConn.Query_Execute("DELETE FROM um_film_information WHERE provid='" + provid + "'");
                }
            }
            catch (Exception ex)
            {
                status = "Error occurred retrieving IMDB provider identifier - " + ex.Message + " - " + ex.GetBaseException().Message + " - " + ex.StackTrace;
                state = State.Error;
                return;
            }
            // Log the download
            Base.logExternalRequest(imdbConn, "Film data - IMDB", _IMDB_DownloadDatabaseFile_URL);
            try
            {
                // Check the cache directory exists
                if (!Directory.Exists(PATH_CACHE))
                    Directory.CreateDirectory(PATH_CACHE);
                // Download the database file from IMDB
                WebRequest req = WebRequest.Create(_IMDB_DownloadDatabaseFile_URL);
                WebResponse resp = req.GetResponse();
                Stream resp_stream = resp.GetResponseStream();
                if (File.Exists(PATH_COMPRESSED))
                    File.Delete(PATH_COMPRESSED);
                FileStream file = File.OpenWrite(PATH_COMPRESSED);
                // Write the data from the download stream to file
                byte[] buffer = new byte[_IMDB_DownloadDatabaseFile_Buffer];
                int bytes;
                double totalBytes = 0;
                do
                {
                    bytes = resp_stream.Read(buffer, 0, _IMDB_DownloadDatabaseFile_Buffer);
                    file.Write(buffer, 0, bytes);
                    file.Flush();
                    totalBytes += bytes;
                    status = "Downloading IMDB cache data... " + (Math.Round(totalBytes / (double)resp.ContentLength, 4) * 100) + "% - " + totalBytes + " of " + resp.ContentLength + " bytes";
                }
                while (bytes > 0);
                // Close the file
                file.Close();
                file.Dispose();
                file = null;
            }
            catch (Exception ex)
            {
                status = "Error occurred downloading IMDB cache data - " + ex.Message;
                state = State.Error;
                return;
            }
            // Reopen the file stream for reading
            status = "Extracting IMDB cache data...";
            try
            {
                FileStream file = new FileStream(PATH_COMPRESSED, FileMode.Open);
                // Open a decompression stream
                GZipInputStream gis = new GZipInputStream(file);
                FileStream file_decom = new FileStream(PATH_UNCOMPRESSED, FileMode.Create);
                byte[] buffer = new byte[_IMDB_DownloadDatabaseFile_Buffer];
                StreamUtils.Copy(gis, file_decom, buffer);
                file_decom.Flush();
                file_decom.Close();
                file_decom.Dispose();
                gis.Close();
                gis.Dispose();
                // Delete gz file
                File.Delete(PATH_COMPRESSED);
            }
            catch (Exception ex)
            {
                status = "Error occurred extracting IMDB cache data - " + ex.Message;
                state = State.Error;
                return;
            }
            // Begin reading through each record and insert it into the database
            try
            {
                StreamReader reader = new StreamReader(PATH_UNCOMPRESSED);                   // Provides an efficient buffered method of reading the text file
                // Get all of the possibilities and their film descriptions - we'll use Levenshtein Distance algorithm to calculate the best match (not perfect due to the format of film titles on IMDB)
                // -- this could be improved using a regex to find the specific titles ignoring the film year etc - although e.g. sunshine is the film title of about ten films...so this would still fail
                //    unless we compared the cost/difference amount and returned the lowest or none if all are equal
                string line;
                string title;
                StringBuilder bufferSynopsis = new StringBuilder();
                const string STATEMENT_START = "INSERT INTO um_film_information (title, description, provid, last_updated) VALUES";
                int statementCount = 0;
                StringBuilder statements = new StringBuilder();
                statements.Append(STATEMENT_START);
                while (reader.Peek() != -1)
                {
                    line = reader.ReadLine(); // Store the current line
                    // Process the line in a new thread
                    if (line.Length > 4 && line[0] == 'M' && line[1] == 'V' && line[2] == ':' && line[3] == ' ') // Movie title?
                    {
                        // Set the title
                        title = line.Substring(4);
                        // Get the film description - always a blank line after a movie title header
                        reader.ReadLine();
                        // Read the synopsis data
                        while (reader.Peek() != -1)
                        {
                            line = reader.ReadLine();
                            if (line.Length > 4 && line[0] == 'P' && line[1] == 'L' && line[2] == ':' && line[3] == ' ')
                                // Append the current line to the buffer
                                bufferSynopsis.Append(line.Substring(4)).Append(" ");
                            else break;
                        }
                        // Remove tailing space
                        if (bufferSynopsis.Length > 0) bufferSynopsis.Remove(bufferSynopsis.Length - 1, 1);
                        // Append the SQL statement
                        statements.Append("('").Append(Utils.Escape(title)).Append("', '").Append(Utils.Escape(bufferSynopsis.ToString())).Append("', '").Append(provid).Append("',  NOW()),");
                        // Clear synopsis buffer
                        bufferSynopsis.Remove(0, bufferSynopsis.Length);
                        // Increment statement count
                        statementCount++;
                        // Check if to execute the statements
                        if (statementCount >= 600)
                        {
                            // Reset counter
                            statementCount = 0;
                            // Remove tailing comma
                            statements.Remove(statements.Length - 1, 1);
                            // Execute the query
                            imdbConn.Query_Execute(statements.ToString());
                            // Clear statement buffer
                            statements.Remove(STATEMENT_START.Length, statements.Length - STATEMENT_START.Length);
                        }
                        // Add the title and synopsis to the database in another thread - decreases iteration time
                        //Core.GlobalConnector.Query_Execute("INSERT INTO film_information (title, description, provid, last_updated) VALUES('" + Utils.Escape(title) + "', '" + Utils.Escape(bufferSynopsis.ToString()) + "', '" + provid + "', NOW())");
                    }
                    System.Diagnostics.Debug.WriteLine(Math.Round((((double)reader.BaseStream.Position / (double)reader.BaseStream.Length) * 100), 2) + "%");
                    status = "Reading IMDB cache data... " + Math.Round((((double)reader.BaseStream.Position / (double)reader.BaseStream.Length) * 100), 2) + "% - " + reader.BaseStream.Position + "/" + reader.BaseStream.Length;
                }
            }
            catch (Exception ex)
            {
                status = "Error occurred reading IMDB cache data - " + ex.Message;
                state = State.Error;
                return;
            }
            // Update the provider as complete
            status = "Finished building IMDB cache...";
            imdbConn.Query_Execute("UPDATE um_film_information_providers SET cache_updated=NOW() WHERE provid='" + provid + "'");
#else
            return;
#endif
        }
        #endregion

        #region "Methods - Misc (also contains a const)"
        /// <summary>
        /// The allowed characters for use with the Rotten Tomatoes API queries - security and match reasons
        /// </summary>
        private const string stripCharsAlphaNumericSpaceApostrophe = "1234567890abcdefghijklmnopqrstuvwxyzàâäæçéèêëîïôœùûü '";
        private const string stripCharsAlphaSpaceApostrophe = "abcdefghijklmnopqrstuvwxyzàâäæçéèêëîïôœùûü '";
        /// <summary>
        /// Strips down a file name for simularity comparison.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        static string StripName(string text, string allowedChars)
        {
            text = text.ToLower(); // Make it lower-case
            string t = "";
            for (int i = 0; i < text.Length; i++)
                if (allowedChars.Contains(text[i].ToString())) t += text[i];
            return t;
        }
        /// <summary>
        /// Levenshtein Distance algorithm
        /// By Stephen Toub (Microsoft):
        /// http://blogs.msdn.com/b/toub/archive/2006/05/05/590814.aspx
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static int editDistance<T>(IEnumerable<T> x, IEnumerable<T> y)
            where T : IEquatable<T>
        {
            // Validate parameters
            if (x == null) throw new ArgumentNullException("x");
            if (y == null) throw new ArgumentNullException("y");

            // Convert the parameters into IList instances
            // in order to obtain indexing capabilities
            IList<T> first = x as IList<T> ?? new List<T>(x);
            IList<T> second = y as IList<T> ?? new List<T>(y);

            // Get the length of both.  If either is 0, return
            // the length of the other, since that number of insertions
            // would be required.
            int n = first.Count, m = second.Count;
            if (n == 0) return m;
            if (m == 0) return n;

            // Rather than maintain an entire matrix (which would require O(n*m) space),
            // just store the current row and the next row, each of which has a length m+1,
            // so just O(m) space. Initialize the current row.
            int curRow = 0, nextRow = 1;
            int[][] rows = new int[][] { new int[m + 1], new int[m + 1] };
            for (int j = 0; j <= m; ++j) rows[curRow][j] = j;

            // For each virtual row (since we only have physical storage for two)
            for (int i = 1; i <= n; ++i)
            {
                // Fill in the values in the row
                rows[nextRow][0] = i;
                for (int j = 1; j <= m; ++j)
                {
                    int dist1 = rows[curRow][j] + 1;
                    int dist2 = rows[nextRow][j - 1] + 1;
                    int dist3 = rows[curRow][j - 1] +
                        (first[i - 1].Equals(second[j - 1]) ? 0 : 1);

                    rows[nextRow][j] = Math.Min(dist1, Math.Min(dist2, dist3));
                }
                // Swap the current and next rows
                if (curRow == 0)
                {
                    curRow = 1;
                    nextRow = 0;
                }
                else
                {
                    curRow = 0;
                    nextRow = 1;
                }
            }
            // Return the computed edit distance
            return rows[curRow][m];
        }
        #endregion
    }
}