namespace Elmah
{
    using System;
    using System.Collections;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Mail;
    using System.Text;
    using System.Threading;

    [CLSCompliant(false)]
    public class ErrorMail
    {
        public ErrorMail(
            string authPassword,
            string authUserName,
            ErrorTextFormatter formatter,
            string mailCopyRecipient,
            MailPriority mailPriority,
            string mailRecipient,
            string mailSender,
            string mailSubjectFormat,
            bool reportAsynchronously,
            bool sendYsod,
            ushort smtpPort,
            string smtpServer,
            bool useSsl)
        {
            this.AuthPassword = authPassword ?? string.Empty;
            this.AuthUserName = authUserName ?? string.Empty;
            this.Formatter = formatter ?? new ErrorMailHtmlFormatter();
            this.MailCopyRecipient = mailCopyRecipient ?? string.Empty;
            this.MailPriority = mailPriority;
            this.MailRecipient = mailRecipient ?? string.Empty;
            this.MailSender = mailSender ?? string.Empty;
            this.MailSubjectFormat = mailSubjectFormat ?? string.Empty;
            this.ReportAsynchronously = reportAsynchronously;
            this.SendYsod = sendYsod;
            this.SmtpPort = smtpPort;
            this.SmtpServer = smtpServer ?? string.Empty;
            this.UseSsl = useSsl;
        }
        
        public string AuthPassword { get; private set; }

        public string AuthUserName { get; private set; }

        public ErrorTextFormatter Formatter { get; private set; }

        public string MailCopyRecipient { get; private set; }

        public MailPriority MailPriority { get; private set; }

        public string MailRecipient { get; private set; }

        public string MailSender { get; private set; }

        public string MailSubjectFormat { get; private set; }

        public bool ReportAsynchronously { get; private set; }

        public bool SendYsod { get; private set; }

        public ushort SmtpPort { get; private set; }

        public string SmtpServer { get; private set; }

        public bool UseSsl { get; private set; }

        public static ErrorMail Create()
        {
            ErrorMail result = null;
            IDictionary config = ErrorMail.GetConfig();

            if (config != null)
            {
                string authPassword = ErrorMail.GetSetting(config, "password", string.Empty);
                string authUserName = ErrorMail.GetSetting(config, "userName", string.Empty);
                string mailCopyRecipient = ErrorMail.GetSetting(config, "cc", string.Empty);
                MailPriority mailPriority = (MailPriority)Enum.Parse(typeof(MailPriority), ErrorMail.GetSetting(config, "priority", MailPriority.Normal.ToString()), true);
                string mailRecipient = ErrorMail.GetSetting(config, "to");
                string mailSender = ErrorMail.GetSetting(config, "from", mailRecipient);
                string mailSubjectFormat = ErrorMail.GetSetting(config, "subject", string.Empty);
                bool reportAsynchronously = Convert.ToBoolean(ErrorMail.GetSetting(config, "async", bool.TrueString));
                bool sendYsod = Convert.ToBoolean(ErrorMail.GetSetting(config, "noYsod", bool.FalseString));
                ushort smtpPort = Convert.ToUInt16(ErrorMail.GetSetting(config, "smtpPort", "0"), CultureInfo.InvariantCulture);
                string smtpServer = ErrorMail.GetSetting(config, "smtpServer", string.Empty);
                bool useSsl = Convert.ToBoolean(ErrorMail.GetSetting(config, "useSsl", bool.FalseString));

                result = new ErrorMail(
                    authPassword,
                    authUserName,
                    new ErrorMailHtmlFormatter(),
                    mailCopyRecipient,
                    mailPriority,
                    mailRecipient,
                    mailSender,
                    mailSubjectFormat,
                    reportAsynchronously,
                    sendYsod,
                    smtpPort,
                    smtpServer,
                    useSsl);
            }

            return result;
        }

        public virtual void Send(Error error)
        {
            if (error == null)
            {
                throw new ArgumentNullException("error", "error cannot be null.");
            }

            if (this.ReportAsynchronously)
            {
                this.SendImplAsync(error);
            }
            else
            {
                this.SendImpl(error);   
            }
        }

        internal virtual MailMessage CreateMessage(
            Error error,
            ErrorTextFormatter formatter,
            string mailCopyRecipient,
            MailPriority mailPriority,
            string mailRecipient,
            string mailSender,
            string mailSubjectFormat)
        {
            if (error == null)
            {
                throw new ArgumentNullException("error", "error cannot be null.");
            }

            if (formatter == null)
            {
                throw new ArgumentNullException("formatter", "formatter cannot be null.");
            }

            MailMessage result = null;

            try
            {
                if (!string.IsNullOrEmpty(mailRecipient) && !string.IsNullOrEmpty(mailSender))
                {
                    result = new MailMessage();
                    ErrorMail.InitializeMessage(result, error, mailCopyRecipient, mailPriority, mailRecipient, mailSender, mailSubjectFormat);
                    ErrorMail.FormatBody(result, error, formatter);
                    ErrorMail.CreateAttachments(result, error, this.SendYsod);
                }
            }
            catch
            {
                if (result != null)
                {
                    result.Dispose();
                }

                throw;
            }

            return result;
        }

        internal virtual void SendMessage(MailMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message", "message cannot be null.");
            }

            SmtpClient client = new SmtpClient();
            string server = this.SmtpServer;
            ushort port = this.SmtpPort;
            string userName = this.AuthUserName;
            string password = this.AuthPassword;

            if (!string.IsNullOrEmpty(server))
            {
                client.Host = server;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
            }

            if (port > 0)
            {
                client.Port = port;
            }

            if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
            {
                client.Credentials = new NetworkCredential(userName, password);
            }

            client.EnableSsl = this.UseSsl;
            client.Send(message);
        }

        private static void CreateAttachments(MailMessage message, Error error, bool sendYsod)
        {
            if (sendYsod && !string.IsNullOrEmpty(error.WebHostHtmlMessage))
            {
                Attachment attachment = null;

                try
                {
                    attachment = Attachment.CreateAttachmentFromString(
                        error.WebHostHtmlMessage,
                        "YSOD.html",
                        Encoding.UTF8,
                        "text/html");

                    message.Attachments.Add(attachment);
                    attachment = null;
                }
                finally
                {
                    if (attachment != null)
                    {
                        attachment.Dispose();
                    }
                }
            }
        }

        private static void FormatBody(
            MailMessage message,
            Error error,
            ErrorTextFormatter formatter)
        {
            using (StringWriter writer = new StringWriter())
            {
                formatter.Format(writer, error);
                message.Body = writer.ToString();
            }

            switch ((formatter.MimeType).ToUpperInvariant())
            {
                case "TEXT/HTML":
                    message.IsBodyHtml = true;
                    break;
                case "TEXT/PLAIN":
                    message.IsBodyHtml = false;
                    break;
                default:
                    throw new ApplicationException(
                        string.Format(
                            "The error mail module does not know how to handle the {1} media type that is created by the {0} formatter.",
                            formatter.GetType().FullName,
                            formatter.MimeType));
            }
        }

        private static IDictionary GetConfig()
        {
            return Configuration.GetSubsection("errorMail") as IDictionary;
        }

        private static string GetSetting(IDictionary config, string name)
        {
            return GetSetting(config, name, null);
        }

        private static string GetSetting(IDictionary config, string name, string defaultValue)
        {
            Debug.Assert(config != null);
            Debug.AssertStringNotEmpty(name);
            string value = (string)config[name] ?? string.Empty;

            if (string.IsNullOrEmpty(value))
            {
                if (defaultValue == null)
                {
                    throw new ApplicationException(string.Format("The required configuration setting '{0}' is missing for the error mailing module.", name));
                }

                value = defaultValue;
            }

            return value;
        }

        private static void InitializeMessage(
            MailMessage message,
            Error error,
            string copyRecipient,
            MailPriority priority,
            string recipient,
            string sender,
            string subjectFormat)
        {
            message.Priority = priority;
            message.From = new MailAddress(sender ?? string.Empty);
            message.To.Add(recipient);

            if (!string.IsNullOrEmpty(copyRecipient))
            {
                message.CC.Add(copyRecipient);
            }

            subjectFormat = Mask.EmptyString(subjectFormat, "Error ({1}): {0}");
            message.Subject = string.Format(subjectFormat, error.Message, error.Type).Replace('\r', ' ').Replace('\n', ' ');
        }

        private void SendImpl(object state)
        {
            try
            {
                this.SendImpl((Error)state);
            }
            catch (SmtpException e)
            {
                //
                // Catch and trace COM/SmtpException here because this
                // method will be called on a thread pool thread and
                // can either fail silently in 1.x or with a big band in
                // 2.0. For latter, see the following MS KB article for
                // details:
                //
                //     Unhandled exceptions cause ASP.NET-based applications 
                //     to unexpectedly quit in the .NET Framework 2.0
                //     http://support.microsoft.com/kb/911816
                //

                Trace.TraceError(e.ToString());
            }
        }

        private void SendImpl(Error error)
        {
            using (MailMessage message = this.CreateMessage(
                error,
                this.Formatter,
                this.MailCopyRecipient,
                this.MailPriority,
                this.MailRecipient,
                this.MailSender,
                this.MailSubjectFormat))
            {
                this.SendMessage(message);
            }
        }

        private void SendImplAsync(Error error)
        {
            ThreadPool.QueueUserWorkItem(this.SendImpl, error);
        }
    }
}
