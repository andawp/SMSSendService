using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Web;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections;
using System.Text.RegularExpressions;
using System.Xml;

namespace becksend
{
    public partial class Service1 : ServiceBase
    {
        private Thread MainThread;//定义一个线程 ;
        private System.Timers.Timer _timer = new System.Timers.Timer();
        string username = "";
        string password = "";
        string database = "";
        string uid = "";
        string pwd = "";
        string adminTel = "";//管理员手机号码，C6系统用户名

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // TODO: 在此处添加代码以启动服务。
            string sLine;
            string data;
            using (StreamReader sr = new StreamReader("sendSmsConfig.txt"))
            {
                sLine = sr.ReadToEnd();
                data = "";
                if (sLine != null && !sLine.Equals(""))
                {
                    data = sLine;
                }
                string[] aryReturn1 = Regex.Split(data, "-");
                this.username = aryReturn1[0];
                this.password = aryReturn1[1];
                this.database = aryReturn1[2];
                this.uid = aryReturn1[3];
                this.pwd = aryReturn1[4];
                this.adminTel = aryReturn1[5];
            }
            MainThread = new Thread(new ThreadStart(ThreadFunc));
            MainThread.Priority = ThreadPriority.Highest;
            MainThread.Start();

            Log("OnStart");
        }

        protected override void OnStop()
        {
            // TODO: 在此处添加代码以执行停止服务所需的关闭操作。20140522，更新在线程停止前发送短信给管理员，并记录日志
            Log("OnStop");
            int i = SendPaging(adminTel, "短信发送程序终止，请检查处理！");
            Log(i + "-寻呼发送状态");
            MainThread.Abort();
        }

        //线程执行的方法,20140526
        public void ThreadFunc()
        {
            while (true)
            {
                Thread.Sleep(10000);//每10秒轮询一次
                QueryAndSend();
            }

        }

        private void QueryAndSend()
        {
            //连接数据库，查询发送标记为0（未发送）的短信
            //datediff(day,subTime,getdate())=0,用来限制系统，只发送当天的短信。2011-11-10，by 王鹏
            SqlConnection.ClearAllPools();//重置或清空连接池，为解决程序运行一段时间就奔溃的bug，2012-03-23，by 王鹏
            SqlConnection con = new SqlConnection("server=JIADING-OASERVE;database=" + this.database + ";uid=" + this.uid + ";pwd=" + this.pwd + "");
            SqlDataAdapter da = new SqlDataAdapter("Select left(SmsToTel,20) as Tel,SmsContent,SmsUser From [Sms] LEFT OUTER JOIN Users ON Sms.SmsUser = Users.UserID Where SmsFlag = 3 and SmsState = 0 and datediff(day,subTime,getdate())=0", con);
            DataSet ds = new DataSet();
            try
            {
                ds.Clear();
                da.Fill(ds);
            }
            catch (Exception ex)
            {
                using (StreamWriter sw = File.AppendText("sendResponse.txt"))
                {
                    Log("查询数据库未发送短信失败！" + ex.ToString(), sw);
                }
            }

            if (ds.Tables[0].Rows.Count != 0)
            {
                for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                {
                    string sendsmsresponse = SendSms(ds.Tables[0].Rows[i]["Tel"].ToString(), ds.Tables[0].Rows[i]["SmsContent"].ToString());
                    string smsbalance = GetBalance(this.username, this.password);

                    XmlDocument xml = new XmlDocument();
                    xml.LoadXml(sendsmsresponse);
                    string Code = xml.ChildNodes[0].InnerText;
                    string Result = xml.ChildNodes[1].InnerText;

                    XmlDocument xml2 = new XmlDocument();
                    xml2.LoadXml(smsbalance);
                    string Code2 = xml2.ChildNodes[0].InnerText;
                    string Result2 = xml2.ChildNodes[1].InnerText;
             
                    using(StreamWriter sw = File.AppendText("sendResponse.txt"))
                    {
                        responseLog(Code,Result,Code2,Result2,sw);
                    }

                }
                SMSSendService.C6WebService.NetCall c6webservice = new SMSSendService.C6WebService.NetCall();

                SendPaging(adminTel,c6webservice.getUserName(ds.Tables[0].Rows[0]["SmsUser"].ToString()) + "刚刚发送了" + ds.Tables[0].Rows.Count + "条短信。");

                SqlCommand cmd = new SqlCommand("Update [Sms] Set SmsFlag=1 Where SmsFlag=3 and SmsState=0", con);

                try
                {
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    using (StreamWriter sw = File.AppendText("sendResponse.txt"))
                    {
                        Log("更新数据库短信已经发送标记失败！" + ex.ToString(), sw);
                    }
                }
                finally
                {
                    con.Close();
                }
            }
            ds.Dispose();
        }

        //追加返回代码到sendsmsresponse.txt
        public static void responseLog(String Code,String Result,String Code2,String Result2,StreamWriter sw)
        {
            sw.WriteLine("发送时间：" + DateTime.Now.ToString("yyyy-MM-dd hh:ff:mm"));
            sw.WriteLine("返回代码：" + Code);
            sw.WriteLine("结果说明：" + Result);
            sw.WriteLine("短信余额查询结果：");
            sw.WriteLine("返回代码：" + Code2);
            sw.WriteLine("结果说明：" + Result2);
            sw.WriteLine("-------------------------------------------------------------------------------------");
        }
        //追加日志到log.txt
        public static void Log(String logMessage, TextWriter w)
        {
            w.WriteLine("{0}{1}", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
            w.WriteLine("  :");
            w.WriteLine("  :{0}", logMessage);
            w.WriteLine("-------------------------------");
        }
        public static void Log(string logMessage)
        {
            using (StreamWriter sw = File.AppendText("sendResponse.txt"))
            {
                sw.WriteLine("{0}{1}", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
                sw.WriteLine("  :");
                sw.WriteLine("  :{0}", logMessage);
                sw.WriteLine("-------------------------------");                
            }
        }
        //发送短信
        public string sendsms(string username, string password, string objmobiles, string smstext, string t, string ytime)
        {
            Encoding encoding = Encoding.GetEncoding("utf-8");
            //string url = "http://sms.hn106.com/webapi/smsapi.asmx/";2012.11.14,修改短信接口API参数
            string strUrl = "http://sms.hn106.com/WebAPI/SmsAPI.asmx/SendSms";
            string postData = "user=" + username;
            postData += ("&pwd=" + password);
            postData += ("&mobiles=" + objmobiles);
            postData += ("&contents=" + smstext);
            postData += ("&t=" + t);
            postData += ("&ytime=" + ytime);
            byte[] data = encoding.GetBytes(postData);

            // 准备请求...
            HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(strUrl);
            myRequest.Method = "POST";
            myRequest.ContentType = "application/x-www-form-urlencoded";
            myRequest.ContentLength = data.Length;
            Stream newStream = myRequest.GetRequestStream();
            // 发送数据 
            newStream.Write(data, 0, data.Length);
            newStream.Close();


            // 得到 response
            HttpWebResponse myResponse = (HttpWebResponse)myRequest.GetResponse();
            StreamReader reader = new StreamReader(myResponse.GetResponseStream(),encoding);
            string content = reader.ReadToEnd();
            XmlDocument xd = new XmlDocument();
            xd.LoadXml(content);
            int code = int.Parse(xd.ChildNodes[0].InnerText);
            if(code == -6)
            {
                SendPaging(adminTel, "C6短信帐户余额不足！");
            }
            return content;
        }
        //重写短信发送方法，20140522王鹏
        public string SendSms(string tel, string txt)
        {
            Encoding encoding = Encoding.GetEncoding("utf-8");
            //string url = "http://sms.hn106.com/webapi/smsapi.asmx/";2012.11.14,修改短信接口API参数
            string strUrl = "http://sms.hn106.com/WebAPI/SmsAPI.asmx/SendSms";
            string postData = "user=" + username;
            postData += ("&pwd=" + password);
            postData += ("&mobiles=" + tel);
            postData += ("&contents=" + txt);
            postData += ("&t=A");
            postData += ("&ytime=" + "");
            byte[] data = encoding.GetBytes(postData);

            // 准备请求...
            HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(strUrl);
            myRequest.Method = "POST";
            myRequest.ContentType = "application/x-www-form-urlencoded";
            myRequest.ContentLength = data.Length;
            Stream newStream = myRequest.GetRequestStream();
            // 发送数据 
            newStream.Write(data, 0, data.Length);
            newStream.Close();


            // 得到 response
            HttpWebResponse myResponse = (HttpWebResponse)myRequest.GetResponse();
            StreamReader reader = new StreamReader(myResponse.GetResponseStream(), encoding);
            string content = reader.ReadToEnd();
            return content;            
        }
        //发送C6寻呼给系统管理员
        public int SendPaging(string receiver, string txt)
        {
            SMSSendService.C6WebService.NetCall c6webservice = new SMSSendService.C6WebService.NetCall();
            string type = "SmsSendService";
            int i = c6webservice.sendC6Call(receiver, txt,type);
            return i;
        }
        //查询短信余额
        public string GetBalance(string user,string pwd)
        {
            Encoding encoding = Encoding.GetEncoding("utf-8");
            string strUrl = "http://sms.hn106.com/WebAPI/SmsAPI.asmx/GetBalance";
            string postData = "user=" + user;
            postData += ("&pwd=" + pwd);
            byte[] data = encoding.GetBytes(postData);

            HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(strUrl);
            myRequest.Method = "POST";
            myRequest.ContentType = "application/x-www-form-urlencoded";
            myRequest.ContentLength = data.Length;
            Stream newStream = myRequest.GetRequestStream();
            newStream.Write(data,0,data.Length);
            newStream.Close();

            HttpWebResponse myResponse = (HttpWebResponse)myRequest.GetResponse();
            StreamReader reader = new StreamReader(myResponse.GetResponseStream(),encoding);
            string content = reader.ReadToEnd();
            return content;
        }  
    }
}
