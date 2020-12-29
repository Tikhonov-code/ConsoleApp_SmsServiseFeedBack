using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp_SmsServiseFeedBack
{
    public class SMS_Manager
    {
        private string login = Properties.Settings.Default.login;
        private string password = Properties.Settings.Default.password;
        private string url = Properties.Settings.Default.url_FeedBack;
        private string[] Sending_sms_status ={ "accepted","invalid mobile phone", "error authorization","text is empty","text must be string","sender address invalid",
                                                "wapurl invalid",  "invalid schedule time format", "invalid status queue name", "not enough balance"};

        private string[] Sent_sms_status = { "queued", "delivered", "delivery error", "smsc submit", "smsc reject", "incorrect id" };

        // Read Alerts log Table
        public List<SmsLog> GetZ_AlertLogs()
        {
            List<SMS_GetZ_AlertLogsByDate_Result> result = new List<SMS_GetZ_AlertLogsByDate_Result>();
            List<SmsLog> sl = new List<SmsLog>();

            DateTime? dt = GetTorontoLocalDateTime();
            //DateTime? dt = DateTime.Parse("2020-08-5");

            try
            {
                using (DB_A4A060_csEntities context = new DB_A4A060_csEntities())
                {
                    result = context.SMS_GetZ_AlertLogsByDate(dt.Value).ToList();
                }
                //remove record with Gaps > 5%
                if (result.Count > 0)
                {
                    result = RemoveBigGapsData(result, Properties.Settings.Default.GapsLimit);
                }
                else
                {
                    return sl;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            //Write records to Smslog table database

            foreach (var item in result)
            {
                SmsLog slitem = new SmsLog();
                slitem.@event = item.@event;
                slitem.message = item.message;
                slitem.phonenumber = item.Phone;
                slitem.status = "prepared to sending";
                slitem.datemark = item.date_emailsent;
                slitem.smscId = "0";
                slitem.AspNetUser_Id = item.AspNetUser_ID;
                sl.Add(slitem);
                slitem = null;
            }

            //check if records were saved today
            var rdonetoday = RemoveItemsSaved(sl);

            return rdonetoday;
        }

        private List<SmsLog> RemoveItemsSaved(List<SmsLog> sl)
        {
            List<SmsLog> sl_result = new List<SmsLog>();

            DateTime? dt = GetTorontoLocalDateTime().Date;//DateTime.Parse("2020-04-02"); //
            //DateTime? dt = DateTime.Parse("2020-08-05"); //

            DateTime? dt1 = dt.Value.Date.AddHours(24);

            List<SmsLog> rowex = new List<SmsLog>();
            try
            {
                using (DB_A4A060_csEntities context = new DB_A4A060_csEntities())
                {
                    rowex = context.SmsLogs.Where(x => x.datemark >= dt.Value && x.datemark <= dt1.Value && x.status.Contains("delivered")).ToList();
                }
            }
            catch (Exception ex)
            {
                var x = ex.Message;
                throw;
            }
            //filter sl - remove old records
            //sl.RemoveAll(x => rowex.Any(r => r.AspNetUser_Id.Contains(x.AspNetUser_Id)
            //                                && x.@event == r.@event
            //                                && x.message == r.message
            //                                && x.phonenumber.Trim() == r.phonenumber.Trim()
            //                               ));
            //Harrcroft Acres. Warning Temperature stays over 40.5C for 6 hours. Cow 724 Dec 29 2020 12:28AM
            foreach (var item in sl)
            {
                string msgCow = item.message.Substring(0, item.message.Length - 19);
                var xel = rowex.Find(x => x.AspNetUser_Id.Contains(item.AspNetUser_Id)
                                && x.@event == item.@event
                                && x.phonenumber.Trim() == item.phonenumber.Trim()
                                && x.message.Contains(msgCow)
                                && x.status.Contains("delivered")
                );
                if (xel == null)
                {
                    sl_result.Add(item);
                }
            }
            return sl_result;
        }

        // Write SMS log records
        public void WriteSmsLogs(List<SmsLog> smsLogs)
        {
            try
            {
                using (DB_A4A060_csEntities context = new DB_A4A060_csEntities())
                {
                    context.SmsLogs.AddRange(smsLogs);
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        // Send sms with Feedback service
        public FeedBackResponse SendSMS(List<smsMsg> sms_list)
        {
            FeedBackResponse result = new FeedBackResponse();
            //1. create SMS Package -----------------------------------
            smsPackage pak = new smsPackage(sms_list);

            //2. Post request           
            result = HttpPostRequest(this.url, pak);

            return result;
        }

        public List<SMS_GetZ_AlertLogsByDate_Result> RemoveBigGapsData(List<SMS_GetZ_AlertLogsByDate_Result> rec_list, float gapsLimit)
        {
            //Stonecreek Farms: Animal_id=3933; Bolus_id=117 [Saturday, April 4, 2020 -- Friday, April 10, 2020] Average Intakes=61.96, litres;  
            //Day Intakes at 4/11/2020 dropped down to 43.98, litres Gaps= 1.3
            //smsMsg sms = new smsMsg();
            //sms.text = "Stonecreek Farms: Animal_id=4301; Bolus_id=118 [Sunday, April 5, 2020 -- Saturday, April 11, 2020] Average Intakes=67.28, litres;  Day Intakes at 4/12/2020 dropped down to 32.94, litres Gaps= 9.9";
            //sms_list.Add(sms);

            List<SMS_GetZ_AlertLogsByDate_Result> sms_listResult = new List<SMS_GetZ_AlertLogsByDate_Result>();
            sms_listResult.AddRange(rec_list);

            foreach (var item in rec_list)
            {
                if (item.@event == "WI20")
                {
                    int ind0 = item.message.IndexOf("Gaps=");
                    if (ind0 == -1)
                    {
                        sms_listResult.Remove(item);
                        continue;
                    }
                    ind0 += 5;
                    int ind1 = item.message.Length;
                    string gaps_val = item.message.Substring(ind0, ind1 - ind0).Trim();
                    float gaps_flt = Convert.ToSingle(gaps_val);
                    if (gaps_flt > gapsLimit)
                    {
                        sms_listResult.Remove(item);
                    }
                }
            }
            return sms_listResult;
        }

        private FeedBackResponse HttpPostRequest(string url, smsPackage pak)
        {
            string responseText = string.Empty;
            try
            {

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = JsonConvert.SerializeObject(pak);
                    streamWriter.Write(json);
                    streamWriter.Flush();
                }
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    responseText = streamReader.ReadToEnd();

                }
            }
            catch (WebException ex)
            {
                responseText = "WebException: " + ex.Message;
            }
            FeedBackResponse fdr = new FeedBackResponse();
            fdr = JsonConvert.DeserializeObject<FeedBackResponse>(responseText);
            return fdr;
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

        //database managment-----------------------------------------------------
        private List<SMS_GetZ_AlertLogsByDate_Result> AlertsList()
        {
            List<SMS_GetZ_AlertLogsByDate_Result> results = new List<SMS_GetZ_AlertLogsByDate_Result>();
            DateTime? dt = GetTorontoLocalDateTime();
            try
            {
                using (DB_A4A060_csEntities context = new DB_A4A060_csEntities())
                {
                    results = context.SMS_GetZ_AlertLogsByDate(dt.Value).ToList();
                }
            }
            catch (Exception)
            {

                throw;
            }
            return results;
        }

        public List<smsMsg> ConvertAlertToSmsList(List<SmsLog> alerts)
        {
            //1. Check SMS service rules
            // List<SmsLog> alerts_ruled = GetSMSarray_AlertByRules(alerts);
            List<smsMsg> result = new List<smsMsg>();

            if (alerts.Count > 0)
            {
                //1. get phones distinct list
                string[] phones_list = alerts.Select(x => x.phonenumber).Distinct().ToArray();

                //2. compress messages by 1 phone

                foreach (var item in phones_list)
                {
                    smsMsg sms = new smsMsg();
                    sms.clientId = alerts.Where(x => x.phonenumber == item).Max(x => x.id).ToString();
                    sms.phone = item;

                    var msg = alerts.Where(x => x.phonenumber == item).Select(x => x.message).ToList();
                    sms.text = string.Join(";\r\n", msg);

                    sms.sender = Properties.Settings.Default.FeedBackSender;

                    result.Add(sms);
                }
            }

            return result;
        }

        //FeedBack response after sending------------------------------------------
        public void FB_ResponseHandler(FeedBackResponse fbr)
        {
            //1. get messages list from FeedBack Responce
            List<messages> sms_fbr = new List<messages>();
            sms_fbr = fbr.messages;
            //sms_fbr.AddRange(fbr.messages);

            //2. Update records in database
            List<SmsLog> slxx = new List<SmsLog>();
            try
            {
                using (DB_A4A060_csEntities context = new DB_A4A060_csEntities())
                {

                    slxx = context.SmsLogs.Where(x => x.status == "prepared to sending" && x.smscId == "0").ToList();
                    //Update status and smscid from FeedBack Response;
                    foreach (var item in slxx)
                    {
                        int client_Id = (sms_fbr.Count > 1) ? sms_fbr.Where(x => x.clientId == item.id).SingleOrDefault().clientId : sms_fbr[0].clientId;
                        if (item.id == client_Id)
                        {
                            item.status = (sms_fbr.Count > 1) ? sms_fbr.Where(x => x.clientId == item.id).SingleOrDefault().status : sms_fbr[0].status;
                            item.smscId = (sms_fbr.Count > 1) ? sms_fbr.Where(x => x.clientId == item.id).SingleOrDefault().smscId : sms_fbr[0].smscId;
                        }
                    }
                    foreach (var item in slxx)
                    {
                        if (item.smscId.Trim() == "0")
                        {
                            item.status = slxx.Where(x => x.phonenumber == item.phonenumber && x.smscId.Trim() != "0").SingleOrDefault().status;
                            item.smscId = slxx.Where(x => x.phonenumber == item.phonenumber && x.smscId.Trim() != "0").SingleOrDefault().smscId.ToString();
                        }
                    }
                    //context.SmsLogs.AddRange(sl);
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                string x = ex.Message;
            }
        }

        //Update sms status by FeedBack request
        internal void UpdateSMSstatus()
        {
            string param = string.Empty;
            //1. Request FeedBack status
            using (DB_A4A060_csEntities context = new DB_A4A060_csEntities())
            {
                var smslist = context.SmsLogs.Where(z => z.status == "accepted" || z.status == "queued" || z.status == "prepared to sending").Select(x => new
                {
                    clientId = x.id,
                    smscId = x.smscId.Trim()
                }).ToList();

                param = (smslist.Count > 0) ? JsonConvert.SerializeObject(smslist) : "0";
            }
            if (param != "0")
            {
                string req_pass = "{\"login\": \"" + Properties.Settings.Default.login +
                                    "\",\"password\": \"" + Properties.Settings.Default.password +
                                    "\", \"messages\": " + param + "}";

                FeedBackResponse fbr = HttpPostRequestStatus(req_pass);
                FB_StatusHandler(fbr);
            }
        }

        private FeedBackResponse HttpPostRequestStatus(string req_pass)
        {
            string responseText = string.Empty;
            string urlstatus = "http://api.smsfeedback.ru/messages/v2/status.json";
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(urlstatus);
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    //string json = JsonConvert.SerializeObject(req_pass);
                    //streamWriter.Write(json);
                    streamWriter.Write(req_pass);
                    streamWriter.Flush();
                }
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    responseText = streamReader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                responseText = "WebException: " + ex.Message;
            }
            FeedBackResponse fdr = new FeedBackResponse();
            fdr = JsonConvert.DeserializeObject<FeedBackResponse>(responseText);
            return fdr;
            //return responseText;
        }

        private void FB_StatusHandler(FeedBackResponse fbr)
        {
            //1. get messages list from FeedBack Responce
            List<messages> sms_fbr = new List<messages>();
            sms_fbr.AddRange(fbr.messages);
            int[] clientIdarr = sms_fbr.Select(x => x.clientId).ToArray();

            //2. Update records in database
            List<SmsLog> slxx = new List<SmsLog>();
            try
            {
                using (DB_A4A060_csEntities context = new DB_A4A060_csEntities())
                {
                    slxx = context.SmsLogs.Where(x => clientIdarr.Contains(x.id)).ToList();
                    //Update status and smscid from FeedBack Response;
                    foreach (var item in slxx)
                    {
                        int client_Id = sms_fbr.Find(x => x.clientId == item.id).clientId;
                        if (item.id == client_Id)
                        {
                            item.status = sms_fbr.Find(x => x.clientId == item.id).status;
                            item.smscId = sms_fbr.Find(x => x.clientId == item.id).smscId;
                        }
                    }
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                string x = ex.Message;
            }
        }

        public List<SmsLog> GetSMSarray_AlertByRules(List<SmsLog> alerts)
        {
            List<SmsLog> result = new List<SmsLog>();
            var user_id_list = alerts.Select(x => x.AspNetUser_Id).Distinct().ToArray();

            //string[] user_id_list = new string[2] { "20595462-e9cd-40a7-81e9-08fc7fdbaa4c","cc5e12ee-54bd-47ef-88ce-7e8590432218" };

            string useridpar = "\'" + string.Join("\',\'", user_id_list) + "\'";

            using (DB_A4A060_csEntities context = new DB_A4A060_csEntities())
            {
                var phonesList = context.SMS_Get_PhonesListByUserID(useridpar).ToList();

                if (phonesList.Count > 0)
                {
                    foreach (var item in alerts)
                    {
                        var r = phonesList.Find(x => (x.AspNetUser_ID == item.AspNetUser_Id) && (x.Alert_Name == item.@event)
                                                    &&  x.phone.Trim() == item.phonenumber.Trim()
                        );
                        if (r != null)
                        {
                            item.phonenumber = r.phone;
                            result.Add(item);
                        }
                    }
                }
            }

            return result;
        }

    }
}
