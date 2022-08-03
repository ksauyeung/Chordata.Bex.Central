using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using Chordata.Bex.Central.Common;
using Chordata.Bex.Api.Interface;
using System.Threading.Tasks;
using System.Data.Entity;
using Chordata.Bex.Central.Data;

namespace Chordata.Bex.Central
{    
    public partial class Main : Form
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private WindowManager wm = WindowManager.Instance;
        private ApiManager am = ApiManager.Instance;       
        
        public Main()
        { 
            InitializeComponent();
        }

        internal void SetStatusStripLabel(string labelName, string text)
        {
            if (statusStrip.Items.ContainsKey(labelName))
            {
                if (statusStrip.InvokeRequired)
                {
                    statusStrip.Invoke(new Action(() => { statusStrip.Items[labelName].Text = text; }));
                }
                else
                {
                    statusStrip.Items[labelName].Text = text;
                }
            }
        }

        private void ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem itm = (ToolStripMenuItem)sender;

            if (itm == polo2ToolStripMenuItem)
            {
                FrmMarket frm = new FrmMarket(am.GetApi<Api.Poloniex.PoloniexApi>());
                wm.OpenOneTitle(frm, this);
            }
            else if (itm == bitfinexToolStripMenuItem)
            {
                FrmMarket frm = new FrmMarket(am.GetApi<Api.Bitfinex.BitfinexApi>());
                wm.OpenOneTitle(frm, this);
            }
            else if (itm == bitMEX2ToolStripMenuItem)
            {
                FrmMarket frm = new FrmMarket(am.GetApi<Api.BitMEX.BitMEXApi>());
                wm.OpenOneTitle(frm, this);
            }
            else if (itm == oKCoinToolStripMenuItem)
            {
                FrmMarket frm = new FrmMarket(am.GetApi<Api.OKCoin.OKCoinApi>());
                wm.OpenOneTitle(frm, this);
            }
            else if (itm == oKEXFutureToolStripMenuItem)
            {
                FrmMarket frm = new FrmMarket(am.GetApi<Api.OKEXFuture.OKEXFutureApi>());
                wm.OpenOneTitle(frm, this);
            }
            else if (itm == messagesToolStripMenuItem)
            {
                FrmMessage frmMessage = new FrmMessage();
                wm.OpenOneTitle(frmMessage, this);

            }
            else if (itm == exitToolStripMenuItem)
            {
                Application.Exit();
            }
            else if (itm == openToolStripMenuItem)
            {
                var rootAppender = ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository())
                                         .Root.Appenders.OfType<log4net.Appender.FileAppender>()
                                         .FirstOrDefault();
                string filename = rootAppender != null ? rootAppender.File : string.Empty;
                System.Diagnostics.Process.Start(filename);

            }
            else if (itm == enabledToolStripMenuItem
                || itm == disabledToolStripMenuItem)
            {
                enabledToolStripMenuItem.Checked =
                    disabledToolStripMenuItem.Checked = false;
                itm.Checked = true;

                Properties.Settings.Default["TradeEnabled"] = enabledToolStripMenuItem.Checked;

            }
            else if (itm == level1ErrorsToolStripMenuItem
                || itm == level2WarningsToolStripMenuItem
                || itm == level3InfoToolStripMenuItem
                || itm == level4DebugToolStripMenuItem
                || itm == level5EverythingToolStripMenuItem
                || itm == offToolStripMenuItem)

            {
                level1ErrorsToolStripMenuItem.Checked =
                level2WarningsToolStripMenuItem.Checked =
                level3InfoToolStripMenuItem.Checked =
                level4DebugToolStripMenuItem.Checked =
                level5EverythingToolStripMenuItem.Checked =
                offToolStripMenuItem.Checked = false;
                itm.Checked = true;
                ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level = log4net.LogManager.GetRepository().LevelMap[(string)itm.Tag];
                ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).RaiseConfigurationChanged(EventArgs.Empty);
            }
            else if (itm == uTCToolStripMenuItem
                || itm == localToolStripMenuItem)
            {
                uTCToolStripMenuItem.Checked = localToolStripMenuItem.Checked = false;
                itm.Checked = true;
            }
            else if (itm == tileToolStripMenuItem)
            {
                this.LayoutMdi(MdiLayout.TileHorizontal);
            }
            else if (itm == tileVerticalToolStripMenuItem)
            {
                this.LayoutMdi(MdiLayout.TileVertical);
            }
            else if (itm == cascadeToolStripMenuItem)
            {
                this.LayoutMdi(MdiLayout.Cascade);
            }
            else if (itm == strategyToolStripMenuItem)
            {
                FrmStrategy frm = new FrmStrategy();
                wm.OpenOneTitle(frm, this);
            }
            else if (itm == dataToolStripMenuItem)
            {
                FrmData frm = new FrmData();
                wm.OpenOneTitle(frm, this);
            }
            else if (itm == exchangeToolStripMenuItem)
            {
                //FrmExchanges frm = new FrmExchanges();
                //wm.OpenOneTitle(frm, this);
            }
          
        }

        private void Main_Load(object sender, EventArgs e)
        {
            FrmMessage frmMessage = new FrmMessage();
            frmMessage.MdiParent = this;
            frmMessage.StartPosition = FormStartPosition.Manual;
            frmMessage.Width = this.ClientSize.Width - 20;

            int leftStart = (SystemInformation.Border3DSize.Width);
            int topStart = this.ClientSize.Height - statusStrip.Height - 30 - (frmMessage.Height + (SystemInformation.Border3DSize.Height * 2));
            frmMessage.Location = new Point(leftStart, topStart);
            wm.OpenOneTitle(frmMessage, this);

            //RunRoutines();
        }

        private void RunRoutines()
        {
            MarketDataGrab grab = new MarketDataGrab();
            grab.Start();
        }

        private async void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();

            while (OrderManager.Instance.OrderInProgress)
            {
                await System.Threading.Tasks.Task.Delay(1000);
            }

            //clean up current orders
            int market = OrderManager.Instance.OpenMarketCount;
            int order = OrderManager.Instance.OpenOrderCount;
            if(order > 0)
            {
                DialogResult r = MessageBox.Show(this,
                    "There are " + order.ToString() + " orders in " + market.ToString() + " markets.\n\nDo you want to cancel all orders before exiting?",
                    "Opne Orders",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button3
                    );
                
                if (r == DialogResult.Cancel)
                    e.Cancel = true;

                if (r == DialogResult.Yes)
                {
                    e.Cancel = true;
                    await OrderManager.Instance.CancelAllOrders();
                    Application.Exit();
                }
            }

            //disconnect all gracefully
            ApiManager.Instance.DisconnectAll();
        }

       
        private void unsubscribeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Api.Interface.IApi a = ApiManager.Instance.GetApi("Bitfinex");
            if (a.IsConnected)
                a.UnsubscribeAll();

            a = ApiManager.Instance.GetApi("OKEXFuture");
            if (a.IsConnected)
                a.UnsubscribeAll();

            a = ApiManager.Instance.GetApi("OKCoin");
            if (a.IsConnected)
                a.UnsubscribeAll();

            a = ApiManager.Instance.GetApi("BitMEX");
            if (a.IsConnected)
                a.UnsubscribeAll();

            a = ApiManager.Instance.GetApi("Poloniex");
            if (a.IsConnected)
                a.UnsubscribeAll();
        }

     

        #region Test
        FloatingPriceOrder5 fpx = null;
        private async void testGetOrderToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void Init()
        {
            Chordata.Bex.Api.BitMEX.BitMEXApi bitmex = ApiManager.Instance.GetApi<Chordata.Bex.Api.BitMEX.BitMEXApi>();
            bitmex.OnOrderChanged += Bitmex_OnOrderChanged;
        }

        private void Bitmex_OnOrderChanged(object sender, Api.OrderChangedEventArgs e)
        {
            IOrder changedOrder = e.Order;
            
            if (changedOrder.OrderStatus == Api.OrderStatus.New)
            {
                //order placed
            }
            else if (changedOrder.OrderStatus == Api.OrderStatus.PartiallyFilled)
            {
                //partial fill
                decimal cumulativeFill = changedOrder.FilledQty.GetValueOrDefault(); //its the Cumulative fill 
            }
            else if (changedOrder.OrderStatus == Api.OrderStatus.Filled)
            {
                //full fill
                decimal cumulativeFill = changedOrder.FilledQty.GetValueOrDefault(); //its the Cumulative fill 
            }
            else if (changedOrder.OrderStatus == Api.OrderStatus.Canceled)
            {
            }
        }

        private async void testSubscribeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (fpx != null)
            {
                IResult res = await fpx.CancelOrder("test");
                if (res == null)
                {
                }
                else if (res.Success)
                {
                }
                else
                {
                }
            }
        }

        private void testOrderBookToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (fpx != null)
            {
                fpx.Stop("user");

            }

        }

        private async void testAmendOrderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string orderID = "142106875047";
            decimal px = 0.0032M;
            Api.Interface.IApi a = ApiManager.Instance.GetApi("Poloniex");
            IResult res = await OrderManager.Instance.AmendOrderAsync(a, "BTC_ETC", orderID, 1, px);
        }

        private void testSFXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Common.Helper.PlaySfx(Sfx.Alert);

        }

        private void testDisconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Api.BitMEX.BitMEXApi api = (Api.BitMEX.BitMEXApi)ApiManager.Instance.GetApi("BitMEX");
            if (api.IsConnected)
                api.Disconnect();
        }

        private void testDisconnectPoloToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Api.Poloniex.PoloniexApi api = (Api.Poloniex.PoloniexApi)ApiManager.Instance.GetApi("Poloniex");
            if (api.WebSocketConnector != null && api.WebSocketConnector.ReadyState == WebSocketSharp.WebSocketState.Open)
                api.WebSocketConnector.Close();
        }

        private void testPoloToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Data.Tuna context = new Data.Tuna();
                log.Debug(context.Database.Connection.ConnectionString);
                log.Debug(context.Database.Connection.DataSource);
                context.Runs.LoadAsync();
                log.Debug(context.Database.Connection.ServerVersion);               
            }
            catch (Exception ex)
            {
                log.Debug(ex.Message);
            }
        }       

        private async void orderbookTestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            decimal px = 0.0045M;
            px = 0.0M;
            decimal amt = 0.03M;
            decimal hurdle = 0.025M;
            decimal makerFee = 0.0015M;
            decimal takerFee = 0.00075M;
            IApi poloniex = ApiManager.Instance.GetApi("Poloniex");
            IApi bitmex = ApiManager.Instance.GetApi("BitMEX");

            //Chordata.Bex.Api.Interface.IResult res = await OrderManager.Instance.PostBuyOrderAsync(a, "BTC_ETC", Api.OrderType.Limit, 1, px, Api.OrderOptions.PostOnly);
            poloniex.Connect(false);
            bitmex.Connect(false);
            poloniex.SubscribeToOrderBookAsync("BTC_ETC");
            bitmex.SubscribeToOrderBookAsync("ETC7D");

            while (bitmex.GetOrderBook("ETC7D") == null || poloniex.GetOrderBook("BTC_ETC") == null)
                await Task.Delay(1000);

            poloniex.SubscribeToOrdersAsync();

            while ((!bitmex.IsOrderBookSubscribed("ETC7D")) || !(poloniex.IsOrderBookSubscribed("BTC_ETC")))
            {
                await Task.Delay(1000);
            }


        }
        
        private async void testPoloToolStripMenuItem1_Click(object sender, EventArgs e)
        {

            try
            {
                IApi a = ApiManager.Instance.GetApi("poloniex");

                a.GetInstrumentAsync("BTC_ETC");



                //using (Tuna db = new Tuna())
                //{
                //    Order ord = new Order()
                //    {
                //        Amount = 1.0M,
                //        Exchange = a.ToString(),
                //        OrderId = o.Order
                //        OrderType = "Limit",
                //        Price = 1.1M,
                //        Side = "Sell",
                //        Status = "Cancelled",
                //        Symbol = "BTC_ETC",
                //        CreateDate = DateTime.UtcNow
                //    };
                //    db.Orders.Add(ord);
                //    Task<int> t = db.SaveChangesAsync();
                //    DBUtil.SaveDbAsync(db);
                //    Console.WriteLine("adsfasdfasdfasdfba");

                //}
            }
            catch (Exception ex)
            {
            }
        }
        #endregion

        private void testFormToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmTest t = new FrmTest();
            t.Show();
        }
    }
}
