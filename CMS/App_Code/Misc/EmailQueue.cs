using System;
using System.Collections.Generic;
using System.Web;
using System.Net.Mail;
using System.Threading;
using UberLib.Connector;

namespace UberCMS.Misc
{
    public class EmailQueue
    {
        #region "Variables"
        private object threadProtection;
        public Thread cyclerThread = null;
        private string mailHost, mailUsername, mailPassword, mailAddress;
        private int mailPort;
        public long mailErrors = -1;
        #endregion

        #region "Method - Constructors"
        /// <summary>
        /// Creates a new enabled e-mail queue.
        /// </summary>
        /// <param name="mailHost"></param>
        /// <param name="mailPort"></param>
        /// <param name="mailUsername"></param>
        /// <param name="mailPassword"></param>
        /// <param name="mailAddress"></param>
        public EmailQueue(string mailHost, int mailPort, string mailUsername, string mailPassword, string mailAddress)
        {
            threadProtection = new object();
            this.mailHost = mailHost;
            this.mailPort = mailPort;
            this.mailUsername = mailUsername;
            this.mailPassword = mailPassword;
            this.mailAddress = mailAddress;
            mailErrors = 0;
        }
        /// <summary>
        /// Creates a disabled e-mail queue.
        /// </summary>
        public EmailQueue() {}
        #endregion

        #region "Methods - Start/stop"
        /// <summary>
        /// Starts the e-mail queue service.
        /// </summary>
        public void start()
        {
            if (threadProtection == null) return;
            lock (threadProtection)
            {
                cyclerThread = new Thread(
                    delegate()
                    {
                        cycler();
                    });
                cyclerThread.Start();
            }
        }
        /// <summary>
        /// Stops the e-mail queue service.
        /// </summary>
        public void stop()
        {
            if (threadProtection == null) return;
            lock (threadProtection)
            {
                cyclerThread.Abort();
                cyclerThread = null;
            }
        }
        #endregion

        #region "Methods"
        /// <summary>
        /// Adds an e-mail to the queue.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        /// <param name="html"></param>
        public void add(string email, string subject, string body, bool html)
        {
            Core.globalConnector.Query_Execute("INSERT INTO email_queue (email, subject, body, html) VALUES('" + Utils.Escape(email) + "', '" + Utils.Escape(subject) + "', '" + Utils.Escape(body) + "', '" + (html ? "1" : "0") + "')");
        }
        /// <summary>
        /// Infinitely checks for e-mails to send.
        /// </summary>
        public void cycler()
        {
            // Ensure the e-mail queue is enabled
            if (threadProtection == null) return;
            // Validate the settings, else stop
            if (mailHost == null || mailHost.Length == 0 || mailUsername == null || mailUsername.Length == 0 || mailPassword == null || mailAddress == null || mailAddress.Length == 0)
                return;
            // Create object for sending the e-mails
            SmtpClient client = new SmtpClient();
            client.Host = mailHost;
            client.Port = mailPort;
            client.Credentials = new System.Net.NetworkCredential(mailUsername, mailPassword);
            Result res;
            ResultRow data;
            const string query = "SELECT * FROM email_queue ORDER BY emailid ASC LIMIT 1";
            MailMessage msg;
            string prependQuery = null;
            bool failed = false;
            // Begin cycling for e-mails to send
            while (true)
            {
                try
                {
                    res = Core.globalConnector.Query_Read((prependQuery ?? string.Empty) + query);
                    prependQuery = null;
                    if (res.Rows.Count == 1)
                    {
                        data = res[0];
                        try
                        {
                            msg = new MailMessage();
                            msg.To.Add(data["email"]);
                            msg.From = new MailAddress(mailAddress);
                            msg.Subject = data["subject"];
                            msg.Headers.Add("CMS", "Uber CMS");
                            msg.Body = data["body"];
                            msg.IsBodyHtml = data["html"].Equals("1");
                            client.Send(msg);
                        }
                        catch (SmtpException)
                        {
                            // We dont wat to cause an overflow, this would cause failure of the queue and possibly core
                            // -- Assuming the queue would be operational for long durations
                            if (mailErrors < long.MaxValue - 1)
                                mailErrors++;
                            else
                                mailErrors = 0;
                            failed = true;
                        }
                        catch { }
                        if (!failed) prependQuery = "DELETE FROM email_queue WHERE emailid='" + Utils.Escape(data["emailid"]) + "';";
                        else failed = false; // Reset failure flag
                        data = null; res = null;
                    }
                    else
                        // Sleep...
                        Thread.Sleep(100);
                }
                catch { }
            }
        }
        #endregion
    }
}