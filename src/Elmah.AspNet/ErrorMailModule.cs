#region License, Terms and Author(s)
//
// ELMAH - Error Logging Modules and Handlers for ASP.NET
// Copyright (c) 2004-9 Atif Aziz. All rights reserved.
//
//  Author(s):
//
//      Atif Aziz, http://www.raboof.com
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

[assembly: Elmah.Scc("$Id: ErrorMailModule.cs 923 2011-12-23 22:02:10Z azizatif $")]

namespace Elmah
{
    #region Imports

    using System;
    using System.Diagnostics;
    using System.Web;
    using System.Net.Mail;
    using ThreadPool = System.Threading.ThreadPool;

    #endregion

    public sealed class ErrorMailEventArgs : EventArgs
    {
        private readonly Error _error;
        private readonly MailMessage _mail;

        public ErrorMailEventArgs(Error error, MailMessage mail)
        {
            if (error == null)
                throw new ArgumentNullException("error");

            if (mail == null)
                throw new ArgumentNullException("mail");

            _error = error;
            _mail = mail;
        }

        public Error Error
        {
            get { return _error; }
        }

        public MailMessage Mail
        {
            get { return _mail; }
        }
    }

    public delegate void ErrorMailEventHandler(object sender, ErrorMailEventArgs args);

    /// <summary>
    /// HTTP module that sends an e-mail whenever an unhandled exception
    /// occurs in an ASP.NET web application.
    /// </summary>

    public class ErrorMailModule : HttpModuleBase, IExceptionFiltering
    {
        private ErrorMail _mail;

        public event ExceptionFilterEventHandler Filtering;
        public event ErrorMailEventHandler Mailing;
        public event ErrorMailEventHandler Mailed;
        public event ErrorMailEventHandler DisposingMail;

        /// <summary>
        /// Initializes the module and prepares it to handle requests.
        /// </summary>
        
        protected override void OnInit(HttpApplication application)
        {
            if (application == null)
                throw new ArgumentNullException("application");

            //
            // Get the configuration section of this module.
            // If it's not there then there is nothing to initialize or do.
            // In this case, the module is as good as mute.
            //

            ErrorMail mail = ErrorMail.Create();

            if (mail == null)
            {
                return;
            }
            
            //
            // Hook into the Error event of the application.
            //

            application.Error += OnError;
            ErrorSignal.Get(application).Raised += OnErrorSignaled;

            //
            // Finally, commit the state of the module if we got this far.
            // Anything beyond this point should not cause an exception.
            //

            _mail = mail;
        }

        /// <summary>
        /// Determines whether the module will be registered for discovery
        /// in partial trust environments or not.
        /// </summary>

        protected override bool SupportDiscoverability
        {
            get { return true; }
        }

        /// <summary>
        /// Gets the e-mail address of the sender.
        /// </summary>
        
        protected virtual string MailSender
        {
            get { return _mail.MailSender; }
        }

        /// <summary>
        /// Gets the e-mail address of the recipient, or a 
        /// comma-/semicolon-delimited list of e-mail addresses in case of 
        /// multiple recipients.
        /// </summary>
        /// <remarks>
        /// When using System.Web.Mail components under .NET Framework 1.x, 
        /// multiple recipients must be semicolon-delimited.
        /// When using System.Net.Mail components under .NET Framework 2.0
        /// or later, multiple recipients must be comma-delimited.
        /// </remarks>

        protected virtual string MailRecipient
        {
            get { return _mail.MailRecipient; }
        }

        /// <summary>
        /// Gets the e-mail address of the recipient for mail carbon 
        /// copy (CC), or a comma-/semicolon-delimited list of e-mail 
        /// addresses in case of multiple recipients.
        /// </summary>
        /// <remarks>
        /// When using System.Web.Mail components under .NET Framework 1.x, 
        /// multiple recipients must be semicolon-delimited.
        /// When using System.Net.Mail components under .NET Framework 2.0
        /// or later, multiple recipients must be comma-delimited.
        /// </remarks>

        protected virtual string MailCopyRecipient
        {
            get { return _mail.MailCopyRecipient; }
        }

        /// <summary>
        /// Gets the text used to format the e-mail subject.
        /// </summary>
        /// <remarks>
        /// The subject text specification may include {0} where the
        /// error message (<see cref="Error.Message"/>) should be inserted 
        /// and {1} <see cref="Error.Type"/> where the error type should 
        /// be insert.
        /// </remarks>

        protected virtual string MailSubjectFormat
        {
            get { return _mail.MailSubjectFormat; }
        }

        /// <summary>
        /// Gets the priority of the e-mail. 
        /// </summary>
        
        protected virtual MailPriority MailPriority
        {
            get { return _mail.MailPriority; }
        }

        /// <summary>
        /// Gets the SMTP server host name used when sending the mail.
        /// </summary>

        protected string SmtpServer
        {
            get { return _mail.SmtpServer; }
        }

        /// <summary>
        /// Gets the SMTP port used when sending the mail.
        /// </summary>

        protected int SmtpPort
        {
            get { return _mail.SmtpPort; }
        }

        /// <summary>
        /// Gets the user name to use if the SMTP server requires authentication.
        /// </summary>

        protected string AuthUserName
        {
            get { return _mail.AuthUserName; }
        }

        /// <summary>
        /// Gets the clear-text password to use if the SMTP server requires 
        /// authentication.
        /// </summary>

        protected string AuthPassword
        {
            get { return _mail.AuthPassword; }
        }

        /// <summary>
        /// Indicates whether <a href="http://en.wikipedia.org/wiki/Screens_of_death#ASP.NET">YSOD</a> 
        /// is attached to the e-mail or not. If <c>true</c>, the YSOD is 
        /// not attached.
        /// </summary>
        
        protected bool NoYsod
        {
            get { return !_mail.SendYsod; }
        }

        /// <summary>
        /// Determines if SSL will be used to encrypt communication with the 
        /// mail server.
        /// </summary>

        protected bool UseSsl
        {
            get { return _mail.UseSsl; }
        }

        /// <summary>
        /// The handler called when an unhandled exception bubbles up to 
        /// the module.
        /// </summary>

        protected virtual void OnError(object sender, EventArgs e)
        {
            var context = new HttpContextWrapper(((HttpApplication) sender).Context);
            OnError(context.Server.GetLastError(), context);
        }

        /// <summary>
        /// The handler called when an exception is explicitly signaled.
        /// </summary>

        protected virtual void OnErrorSignaled(object sender, ErrorSignalEventArgs args)
        {
            using (args.Exception.TryScopeCallerInfo(args.CallerInfo))
                OnError(args.Exception, args.Context);
        }

        /// <summary>
        /// Reports the exception.
        /// </summary>

        protected virtual void OnError(Exception e, HttpContextBase context)
        {
            if (e == null) 
                throw new ArgumentNullException("e");

            //
            // Fire an event to check if listeners want to filter out
            // reporting of the uncaught exception.
            //

            var args = new ExceptionFilterEventArgs(e, context);
            OnFiltering(args);

            if (args.Dismissed)
                return;

            //
            // Get the last error and then report it synchronously or 
            // asynchronously based on the configuration.
            //

            var error = new Error(e, context);

            if (_mail.ReportAsynchronously)
                ReportErrorAsync(error);
            else
                ReportError(error);
        }

        /// <summary>
        /// Raises the <see cref="Filtering"/> event.
        /// </summary>

        protected virtual void OnFiltering(ExceptionFilterEventArgs args)
        {
            var handler = Filtering;
            
            if (handler != null)
                handler(this, args);
        }

        /// <summary>
        /// Schedules the error to be e-mailed asynchronously.
        /// </summary>
        /// <remarks>
        /// The default implementation uses the <see cref="ThreadPool"/>
        /// to queue the reporting.
        /// </remarks>

        protected virtual void ReportErrorAsync(Error error)
        {
            if (error == null)
                throw new ArgumentNullException("error");

            //
            // Schedule the reporting at a later time using a worker from 
            // the system thread pool. This makes the implementation
            // simpler, but it might have an impact on reducing the
            // number of workers available for processing ASP.NET
            // requests in the case where lots of errors being generated.
            //

            ThreadPool.QueueUserWorkItem(ReportError, error);
        }

        private void ReportError(object state)
        {
            try
            {
                ReportError((Error) state);
            }

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

            catch (SmtpException e)
            {
                Trace.TraceError(e.ToString());
            }
        }

        /// <summary>
        /// Schedules the error to be e-mailed synchronously.
        /// </summary>

        protected virtual void ReportError(Error error)
        {
            if (error == null)
            {
                throw new ArgumentNullException("error");
            }

            // We need to pass these values in explicitly because they
            // may have been overridden in an inheriting implementation.
            MailMessage message = _mail.CreateMessage(
                error,
                this.CreateErrorFormatter(),
                this.MailCopyRecipient,
                this.MailPriority,
                this.MailRecipient,
                this.MailSender,
                this.MailSubjectFormat);

            try
            {
                if (message != null)
                {
                    ErrorMailEventArgs args = new ErrorMailEventArgs(error, message);

                    try
                    {
                        this.OnMailing(args);
                        this.SendMail(message);
                        this.OnMailed(args);
                    }
                    finally
                    {
                        this.OnDisposingMail(args);
                    }
                }
            }
            finally
            {
                if (message != null)
                {
                    message.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates the <see cref="ErrorTextFormatter"/> implementation to 
        /// be used to format the body of the e-mail.
        /// </summary>

        protected virtual ErrorTextFormatter CreateErrorFormatter()
        {
            return new ErrorMailHtmlFormatter();
        }

        /// <summary>
        /// Sends the e-mail using SmtpMail or SmtpClient.
        /// </summary>

        protected virtual void SendMail(MailMessage mail)
        {
            if (mail == null)
                throw new ArgumentNullException("mail");

            _mail.SendMessage(mail);
        }

        /// <summary>
        /// Fires the <see cref="Mailing"/> event.
        /// </summary>

        protected virtual void OnMailing(ErrorMailEventArgs args)
        {
            if (args == null)
                throw new ArgumentNullException("args");

            var handler = Mailing;

            if (handler != null)
                handler(this, args);
        }

        /// <summary>
        /// Fires the <see cref="Mailed"/> event.
        /// </summary>

        protected virtual void OnMailed(ErrorMailEventArgs args)
        {
            if (args == null)
                throw new ArgumentNullException("args");

            var handler = Mailed;

            if (handler != null)
                handler(this, args);
        }

        /// <summary>
        /// Fires the <see cref="DisposingMail"/> event.
        /// </summary>
        
        protected virtual void OnDisposingMail(ErrorMailEventArgs args)
        {
            if (args == null)
                throw new ArgumentNullException("args");

            var handler = DisposingMail;

            if (handler != null)
                handler(this, args);
        }
    }
}
