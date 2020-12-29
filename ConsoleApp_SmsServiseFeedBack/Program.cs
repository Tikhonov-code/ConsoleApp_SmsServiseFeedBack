using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp_SmsServiseFeedBack
{
    class Program
    {
        static void Main(string[] args)
        {
            int SMSNumber = 0;
            LogWriter lw = new LogWriter();
            Log record = new Log();
            record.page = Properties.Settings.Default.LogPage;
            record.user_id = Properties.Settings.Default.LogUser_id;
            record.function_query = Properties.Settings.Default.LogFunction_query;
            record.note = "Start";
            lw.WriteRecord(record);

            //1. request Alert list--------------------------------------
            SMS_Manager manager = new SMS_Manager();

            List<SmsLog> al = new List<SmsLog>();
            al = manager.GetZ_AlertLogs();

            if (al.Count > 0)
            {
                //2. Remove Alerts by rules
                //List<SmsLog> alerts_ruled = manager.GetSMSarray_AlertByRules(al);

                //3. Save Alerts in database
                if (al.Count > 0)
                {
                    al = SaveSMSinDatabase(al);

                    //4. Convert alert list to sms list
                    List<smsMsg> sms_list = new List<smsMsg>();
                    sms_list = manager.ConvertAlertToSmsList(al);

                    if (sms_list.Count > 0)
                    {
                        //5. Sending sms by FeedBack service
                        FeedBackResponse fbr = new FeedBackResponse();
                        fbr = manager.SendSMS(sms_list);

                        SMSNumber = sms_list.Count;

                        //6. Update records into  [DB_A4A060_cs].[dbo].[SmsLogs]
                        manager.FB_ResponseHandler(fbr);
                    }
                }
            }

            //5. Check and update sms status 
            manager.UpdateSMSstatus();

            //6. Log record
            record.note = "Finished: "+ SMSNumber+" sms were sent.";
            lw.WriteRecord(record);
            lw = null;
            ;
        }

        private static List<SmsLog> SaveSMSinDatabase(List<SmsLog> alerts_ruled)
        {
            List<SmsLog> sl = new List<SmsLog>();
            foreach (var item in alerts_ruled)
            {
                SmsLog slitem = new SmsLog();
                slitem.@event = item.@event;
                slitem.message = item.message;
                slitem.phonenumber = item.phonenumber;
                slitem.status = "prepared to sending";
                slitem.datemark = item.datemark;
                slitem.smscId = "0";
                slitem.AspNetUser_Id = item.AspNetUser_Id;
                sl.Add(slitem);
                slitem = null;
            }
            //update database
            try
            {
                using (DB_A4A060_csEntities context = new DB_A4A060_csEntities())
                {
                    context.SmsLogs.AddRange(sl);
                    context.SaveChanges();
                    int rowsnumber = sl.Count;
                    sl.Clear();
                    sl = context.SmsLogs.OrderByDescending(x => x.id).Where(x => x.status == "prepared to sending" && x.smscId == "0").Take(rowsnumber).ToList();
                }
            }
            catch (Exception ex)
            {
                var x = ex.Message;
                sl = null;
            }
            return sl;
        }
    }
}
