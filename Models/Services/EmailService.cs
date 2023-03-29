using System;
using System.Net;
using System.Net.Mail;

namespace MediaCred.Models.Services
{
    public class EmailService
    {
        private readonly string _fromEmail;
        private readonly string _fromEmailPassword;
        private readonly string _smtpHost;
        private readonly int _smtpPort;

        public EmailService()
        {
            _fromEmail = "mmca.alex.nico@gmail.com";
            _fromEmailPassword = "rnxrpdsikypaxjha";
            _smtpHost = "smtp.gmail.com";
            _smtpPort = 587;
        }

        public void Send(string toEmail, string subject, string body)
        {
            var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_fromEmail, _fromEmailPassword),
                EnableSsl = true
            };

            var message = new MailMessage(_fromEmail, toEmail, subject, body);
            message.IsBodyHtml = true;

            client.Send(message);
        }
    }
}
