using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp_SmsServiseFeedBack
{
    public class LogWriter
    {
        public bool WriteRecord(Log item)
        {
            bool result = false;
            try
            {
                using (DB_A4A060_csEntities context = new DB_A4A060_csEntities())
                {
                    item.datestamp = GetTorontoLocalDateTime();// DateTime.Now;
                    context.Logs.Add(item);
                    // executes the commands to implement the changes to the database
                    context.SaveChanges();
                    result = true;
                }
            }
            catch (Exception ex)
            {
                SendEmail("post@gmail.com", "1960tix@gmail.com", ex.Message + ex.InnerException.Message, "LogWriter");
            }
            return result;
        }

        public DateTime GetTorontoLocalDateTime()
        {
            var timeUtc = DateTime.UtcNow;
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime easternTime = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, easternZone);
            //----------------------------------------------------------------------------
            //return tdTor;
            return easternTime;

        }

        public string SendEmail(string strFrom, string strTo, string MailMsg, string strSubject)
        {
            string result = string.Empty;
            MailMessage m = new MailMessage();

            SmtpClient sc = new SmtpClient(Properties.Settings.Default.EmailHost);
            m.From = new MailAddress(Properties.Settings.Default.EmailUserName);
            m.To.Add(strTo);
            //m.CC.Add(strTo);
            m.Subject = strSubject;
            m.Body = MailMsg;
            string str1 = "gmail.com";
            string str2 = strFrom.ToLower();

            if (str2.Contains(str1))
            {
                try
                {
                    sc.Port = 587;
                    sc.Credentials = new System.Net.NetworkCredential(Properties.Settings.Default.EmailUserName, Properties.Settings.Default.EmailPassword);
                    sc.EnableSsl = true;
                    sc.Send(m);
                    result = "Email Send successfully";
                }
                catch (Exception)
                {
                    throw;
                }
            }
            else
            {
                try
                {
                    sc.Port = 465;
                    sc.Credentials = new System.Net.NetworkCredential(Properties.Settings.Default.EmailUserName, Properties.Settings.Default.EmailPassword);
                    sc.EnableSsl = false;
                    sc.Send(m);
                    result += "Email Send successfully";
                }
                catch (Exception)
                {
                    throw;
                }
            }
            return result;
        }


    }
}
