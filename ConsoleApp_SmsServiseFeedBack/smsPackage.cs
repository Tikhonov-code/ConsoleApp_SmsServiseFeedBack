using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp_SmsServiseFeedBack
{
    public class smsPackage
    {

        public smsPackage(List<smsMsg> sms_list)
        {
            this.messages = new List<smsMsg>();
            this.messages.AddRange(sms_list);
            this.login = Properties.Settings.Default.login;
            this.password = Properties.Settings.Default.password;
            this.statusQueueName = "Qtix";
            this.showBillingDetails = false;
            this.scheduleTime = DateTime.Now.ToShortDateString();//SMS_Manager.GetTorontoLocalDateTime();

        }

        public string login { get; set; }
        public string password { get; set; }
        public string statusQueueName { get; set; }
        public bool showBillingDetails { get; set; }
        public string scheduleTime { get; set; }
        public List<smsMsg> messages { get; set; }

    }
    public class smsMsg
    {
        public string clientId { get; set; }
        public string phone { get; set; }
        public string text { get; set; }
        public string sender { get; set; }
    }

    public class FeedBackResponse
    {
        //"{\"status\":\"ok\",\"messages\":[{\"clientId\":\"12\",\"smscId\":5433172353,\"status\":\"accepted\"},{\"clientId\":\"11\",\"smscId\":5433172355,\"status\":\"accepted\"},{\"clientId\":\"10\",\"smscId\":5433172358,\"status\":\"accepted\"}]}"
        //{"status":"ok","messages":[{"clientId":"2","smscId":5432543830,"status":"accepted"},{"clientId":"2","smscId":5432543832,"status":"accepted"}]}
        public string status { get; set; }
        public List<messages> messages { get; set; }

    }

    public class messages
    {
        public int clientId { get; set; }
        public string smscId { get; set; }
        public string status { get; set; }
    }

    // SMS Status feedBack response
    //public class FeedBackSmsStatus
    //{
    //    public string login { get; set; }
    //}
    public class smsIDs
    {
        public int clientId { get; set; }
        public int smscId { get; set; }
    }
}
