using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Chordata.Bex.Central
{
    public partial class FrmTest : Form
    {
        public FrmTest()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string[] t = textBox1.Text.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder("Timestamp,OrderID,Symbol,Order Amount,Order Price,Executed Amount,Average Price,Fees,Side");
            for (int i = 0; i < t.Length; i++)
            {
                try
                {
                    if (t[i].Trim().Length > 0 && t[i][37] == '[')
                    {
                        string json = t[i].Substring(37);
                        JArray a = (JArray)JsonConvert.DeserializeObject(json);
                        for (int j = 0; j < a.Count; j++)
                        {
                            try
                            {
                                JObject o = (JObject)a[j];
                                o = (JObject)o.GetValue("data");
                                int status = (int)o.GetValue("status");
                                if (status > 0)
                                {


                                    string sTime = t[i].Substring(0, 23);
                                    DateTime dt = DateTime.ParseExact(sTime, "yyyy-MM-dd HH:mm:ss,fff", System.Globalization.CultureInfo.InvariantCulture);
                                    //long tms = (long)(dt.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;


                                    long tms = ((DateTimeOffset)dt.ToLocalTime()).ToUnixTimeMilliseconds();


                                    double amount = (double)o.GetValue("amount"); //order amount
                                    long orderid = (long)o.GetValue("orderid");
                                    double fee = (double)o.GetValue("fee");
                                    string contractname = (string)o.GetValue("contract_name");
                                    double priceAvg = (double)o.GetValue("price_avg");
                                    int type = (int)o.GetValue("type");
                                    double deal_amount = (double)o.GetValue("deal_amount");
                                    double price = (double)o.GetValue("price");
                                    sb.Append(Environment.NewLine);
                                    sb.Append(tms);
                                    sb.Append(",");
                                    sb.Append(orderid);
                                    sb.Append(",");
                                    sb.Append(contractname);
                                    sb.Append(",");
                                    sb.Append(amount.ToString("0.##########"));
                                    sb.Append(",");
                                    sb.Append(price.ToString("0.##########"));
                                    sb.Append(",");
                                    sb.Append(deal_amount.ToString("0.##########"));
                                    sb.Append(",");
                                    sb.Append(priceAvg.ToString("0.##########"));
                                    sb.Append(",");
                                    sb.Append(fee.ToString("0.##########"));
                                    sb.Append(",");
                                    if (type == 1 || type == 4)
                                        sb.Append("BUY");
                                    else
                                        sb.Append("SELL");

                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }

                    }
                }
                catch (Exception eex)
                {
                    Console.WriteLine(eex.Message);

                }
            }
            textBox2.Text = sb.ToString();

        }

        private void button2_Click(object sender, EventArgs e)
        {
            string[] t = textBox1.Text.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder("Timestamp,TradeID,Symbol,Order Price,Executed Amount,Executed Price,Fees,Fee Curr,Side");
            for (int i = 0; i < t.Length; i++)
            {
                try
                {
                    if (t[i].Trim().Length > 37 && t[i][35] == 't' && t[i][36] == 'u')
                    {
                        string json = t[i].Substring(31);

                        JArray a = (JArray)JsonConvert.DeserializeObject(json);
                        a = (JArray)a[2];
                        double rawAmt = (double)a[5];

                        string tradeId = a[1].ToString();
                        string pair = a[2].ToString();
                        string tms = a[3].ToString();
                        string orderId = a[4].ToString();
                        string amount = (Math.Abs(rawAmt)).ToString();
                        string side = rawAmt < 0.0 ? "SELL" : "BUY";
                        string price = a[6].ToString();
                        string type = a[7].ToString();
                        string orderPrice = a[8].ToString();
                        string fee = Math.Abs((double)a[9]).ToString();
                        string feeCurr = a[10].ToString();
                        sb.Append(Environment.NewLine);
                        sb.Append(tms);
                        sb.Append(",");
                        sb.Append(tradeId);
                        sb.Append(",");
                        sb.Append(pair);
                        sb.Append(",");
                        sb.Append(orderPrice);
                        sb.Append(",");
                        sb.Append(amount);
                        sb.Append(",");
                        sb.Append(price);
                        sb.Append(",");
                        sb.Append(fee);
                        sb.Append(",");
                        sb.Append(feeCurr);
                        sb.Append(",");
                        sb.Append(side);
                        sb.Append(",");
                        sb.Append(type);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            textBox2.Text = sb.ToString();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string[] t = textBox1.Text.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder("Timestamp,Orderid,Symbol,tradeUnitPrice,tradeAmount,completedTradeAmount,averagePrice,tradePrice,side");
            for (int i = 0; i < t.Length; i++)
            {
                try
                {
                    if (t[i].Trim().Length > 0 && t[i][31] == '[')
                    {
                        string json = t[i].Substring(31);
                        JArray a = (JArray)JsonConvert.DeserializeObject(json);
                        for (int j = 0; j < a.Count; j++)
                        {
                            JObject o = (JObject)a[j];
                            o = (JObject)o.GetValue("data");
                            int status = (int)o.GetValue("status");
                            if (status > 0)
                            {
                                string sTime = t[i].Substring(0, 23);
                                DateTime dt = DateTime.ParseExact(sTime, "yyyy-MM-dd HH:mm:ss,fff", System.Globalization.CultureInfo.InvariantCulture);
                                long tms = ((DateTimeOffset)dt.ToLocalTime()).ToUnixTimeMilliseconds();
                                string symbol = (string)o.GetValue("symbol");
                                long orderid = (long)o.GetValue("orderId");
                                double tradeUnitPrice = (double)o.GetValue("tradeUnitPrice");                                 
                                double tradeAmount = (double)o.GetValue("tradeAmount");
                                double completedTradeAmount = (double)o.GetValue("completedTradeAmount");
                                double averagePrice = (double)o.GetValue("averagePrice");
                                double tradePrice = (double)o.GetValue("tradePrice");
                                string tradeType = (string)o.GetValue("tradeType");

                                sb.Append(Environment.NewLine);
                                sb.Append(tms);
                                sb.Append(",");
                                sb.Append(orderid);
                                sb.Append(",");
                                sb.Append(symbol);
                                sb.Append(",");
                                sb.Append(tradeUnitPrice);
                                sb.Append(",");
                                sb.Append(tradeAmount);
                                sb.Append(",");
                                sb.Append(completedTradeAmount);
                                sb.Append(",");
                                sb.Append(averagePrice);
                                sb.Append(",");
                                sb.Append(tradePrice);
                                sb.Append(",");
                                sb.Append(tradeType);                                
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            textBox2.Text = sb.ToString();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string[] t = textBox1.Text.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder("OrderId,OrderType,SystemType,Symbol,Side,Price,Size,OrderStatus,FilledSize,AveragePrice,TotalAmount,Created,Fees,ContractValue");
            for (int i = 0; i < t.Length; i++)
            {
                try
                {
                    var settings = new JsonSerializerSettings { DateFormatString = "yyyy-MM-ddTH:mm:ss.fffZ" };

                    string json = t[i];
                    JObject job = (JObject)JsonConvert.DeserializeObject(json, settings);                    
                    job = (JObject)job.GetValue("data");
                    JArray jar = (JArray)job.GetValue("orders");
                    for (int j = 0; j < jar.Count; j++)
                    {
                        job = (JObject)jar[j];
                        string id = (string)job.GetValue("id");
                        DateTime createTime = (DateTime)job.GetValue("createTime");                        
                        
                        double dealValue = (double)job.GetValue("dealValue");
                        double fee = (double)job.GetValue("fee");
                        double dealAmount = (double)job.GetValue("dealAmount");
                        double priceAvg = (double)job.GetValue("priceAvg");
                        double price = (double)job.GetValue("price");
                        int side = (int)job.GetValue("side");
                        double amount = (double)job.GetValue("amount");
                        int status = (int)job.GetValue("status");
                        string contract = (string)job.GetValue("contract");
                        int systemType = (int)job.GetValue("systemType"); //2 = liquidation
                        double unitAmount = (double)job.GetValue("unitAmount");
                        
                        sb.Append(Environment.NewLine);
                        sb.Append(id);
                        sb.Append(",");
                        sb.Append("");
                        sb.Append(",");
                        sb.Append(getSystemType(systemType));
                        sb.Append(",");
                        sb.Append(contract);
                        sb.Append(",");
                        sb.Append(getSide(systemType, side));
                        sb.Append(",");
                        sb.Append(price);
                        sb.Append(",");
                        sb.Append(amount);
                        sb.Append(",");
                        sb.Append(getStatus(status));
                        sb.Append(",");
                        sb.Append(dealAmount);
                        sb.Append(",");
                        sb.Append(priceAvg);
                        sb.Append(",");
                        sb.Append(dealValue);
                        sb.Append(",");
                        sb.Append(((DateTimeOffset)createTime.ToLocalTime()).ToUnixTimeMilliseconds());
                        sb.Append(",");
                        sb.Append(fee);
                        sb.Append(",");
                        sb.Append(unitAmount);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                textBox2.Text = sb.ToString();

            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string[] t = textBox1.Text.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder("OrderId,OrderType,SystemType,Symbol,Side,Price,Size,OrderStatus,FilledSize,AveragePrice,TotalAmount,Created,Modified");
            for (int i = 0; i < t.Length; i++)
            {
                try
                {
                    String json = t[i];
                    JObject job = (JObject)JsonConvert.DeserializeObject(json);
                    job = (JObject)job.GetValue("data");
                    JArray jar = (JArray)job.GetValue("orders");
                    for (int j = 0; j < jar.Count; j++)
                    {

                        job = (JObject)jar[j];
                        long id = (long)job.GetValue("id");
                        long createTime = (long)job.GetValue("createTime");
                        double executedValue = (double)job.GetValue("executedValue");
                        double filledSize = (double)job.GetValue("filledSize");
                        double averagePrice = (double)job.GetValue("averagePrice");
                        long modifyTime = (long)job.GetValue("modifyTime");
                        int orderType = (int)job.GetValue("orderType");
                        double price = (double)job.GetValue("price");
                        string quoteSize = (string)job.GetValue("quoteSize");
                        int side = (int)job.GetValue("side");
                        double size = (double)job.GetValue("size");
                        int status = (int)job.GetValue("status");
                        string symbol = (string)job.GetValue("symbol");
                        int systemType = (int)job.GetValue("systemType");


                        sb.Append(Environment.NewLine);
                        sb.Append(id);
                        sb.Append(",");
                        sb.Append(getOrderType(orderType));
                        sb.Append(",");
                        sb.Append(getSystemType(systemType));
                        sb.Append(",");
                        sb.Append(symbol);
                        sb.Append(",");
                        sb.Append(side == 1 ? "BUY" : "SELL");
                        sb.Append(",");
                        sb.Append(price);
                        sb.Append(",");
                        sb.Append(size);
                        sb.Append(",");
                        sb.Append(getStatus(status));
                        sb.Append(",");
                        sb.Append(filledSize);
                        sb.Append(",");
                        sb.Append(averagePrice);
                        sb.Append(",");
                        sb.Append(executedValue);
                        sb.Append(",");
                        sb.Append(createTime);
                        sb.Append(",");
                        sb.Append(modifyTime);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            textBox2.Text = sb.ToString();
        }

        private string getSide(int sysType, int side)
        {
            string rv = "";
            if (sysType == 0)
            {
                rv = side == 1 ? "BUY" : "SELL";
            }
            else
            {
                rv = side == 1 ? "SELL" : "BUY";
            }
            return rv;
        }

        private string getSystemType(int t)
        {
            string type = "";
            switch (t)
            {
                case 0:
                    type = "FUTURE";
                    break;
                case 1:
                    type = "SPOT";
                    break;
                case 2:
                    type = "MARGIN";
                    break;
                case 3:
                    type = "SWAP";
                    break;
                default:
                    type = "";
                    break;
            }
            return type;
        }

        private string getOrderType(int t)
        {
            string type = "";
            switch (t)
            {
                case 0:
                    type = "LIMIT";
                    break;
                case 1:
                    type = "MARKET";
                    break;
                default:
                    type = "";
                    break;
            }
            return type;
        }

        private string getStatus(int s) {
            string status = "";
            switch (s)
            {
                case 2:
                    status ="FILLED";
                    break;
                case 1:
                    status = "PARTIALFILLED";
                    break;
                case 0:
                    status = "PENDING";
                    break;
                case -1:
                    status = "CANCELLED";
                    break;
                case -2:
                    status = "CANCELLED";
                    break;
                default:
                    status = "";
                    break;

            }
            return status;
        }

       
    }
}
