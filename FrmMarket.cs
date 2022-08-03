using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using WebSocketSharp;
using Chordata.Bex.Central.Data;
using Chordata.Bex.Central.Common;
using Chordata.Bex.Api;
using Chordata.Bex.Api.Interface;

namespace Chordata.Bex.Central
{
    public partial class FrmMarket : Form
    {
        protected IApi api;
        object sync = new object();

        public FrmMarket(IApi api)
        {
            InitializeComponent();
            this.api = api;
            if (api != null)
            {
                api.OnConnectionEstablished += Api_OnConnectionEstablished;
                api.OnConnectionClosed += Api_OnConnectionClosed;
                api.OnConnectionError += Api_OnConnectionError;
                api.OnMessage += Api_OnMessage;
                api.OnTickerChanged += Api_OnTickerChanged;
                api.OnOrderBookChanged += Api_OnOrderBookChanged;

                string apiName = api.ToString();                
                using (Tuna db = new Data.Tuna())
                {
                    cbSymbol.DataSource  = (
                        from s in db.Symbols
                        where s.Enabled.Value && s.Exchange == apiName
                        select s.Name).ToArray(); ;
                }

                Text = "Market - " + apiName;
            }
        }

        #region Methods    

        private void PrepareGrid()
        {
            //set the grid to display orderbook
            dgvMessage.AutoGenerateColumns = true;
            int depth = 10;
            for (int i = 0; i < depth; i++)
                dgvMessage.Rows.Add();
        }

        protected virtual void ShowOrderBook(object orderbook, DataGridView dgv)
        {
            IOrderBook book = (IOrderBook)orderbook;
            KeyValuePair<decimal, decimal>[] buyEntries = book.Bids.ToArray();
            KeyValuePair<decimal, decimal>[] sellEntries = book.Asks.ToArray();

            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                decimal sellPrice = i < sellEntries.Length ? sellEntries[i].Key : 0.0M;
                decimal sellAmount = i < sellEntries.Length ? sellEntries[i].Value : 0.0M;
                decimal sellTotalAmt = i < sellEntries.Length ? sellEntries[i].Key * sellEntries[i].Value : 0.0M;

                decimal buyPrice = i < buyEntries.Length ? buyEntries[i].Key : 0.0M;
                decimal buyAmount = i < buyEntries.Length ? buyEntries[i].Value : 0.0M;
                decimal buyTotalAmt = i < buyEntries.Length ? buyEntries[i].Key * buyEntries[i].Value : 0.0M;

                dgv.Rows[i].Cells["colSellPrice"].Value = sellPrice;
                dgv.Rows[i].Cells["colSellAmount"].Value = sellAmount;
                dgv.Rows[i].Cells["colSellTotalAmount"].Value = sellTotalAmt;
                dgv.Rows[i].Cells["colBuyPrice"].Value = buyPrice;
                dgv.Rows[i].Cells["colBuyAmount"].Value = buyAmount;
                dgv.Rows[i].Cells["colBuyTotalAmount"].Value = buyTotalAmt;
            }
        }
        

        protected virtual void PrependText(string text, TextBox o)
        {
            o.PerformSafely(() => o.Text = "[" + DateTime.UtcNow.ToString() + "] " + text + Environment.NewLine + o.Text);
        }

        protected virtual void Unsubscribe()
        {
            if (api.IsConnected)
                api.UnsubscribeAll();

            tbCurrentSymbol.PerformSafely(() => tbCurrentSymbol.Text = string.Empty);
            btnSubscribe.PerformSafely(() => btnSubscribe.Text = "Start");
        }

        protected virtual void Subscribe(string symbol = "")
        {
            if (api.IsConnected)
            {
                api.SubscribeToTickerAsync(symbol);
                api.SubscribeToOrderBookAsync(symbol);
            }
        }

        protected virtual void Connect()
        {
            lblStatus.PerformSafely(() => lblStatus.Text = "Connecting...");
            btnConnect.PerformSafely(() => btnConnect.Enabled = false);
            api.Connect(true);
        }

        protected virtual void Disconnect()
        {
            lblStatus.PerformSafely(() => lblStatus.Text = "Disconnecting...");
            btnConnect.PerformSafely(() => btnConnect.Enabled = false);
            api.Disconnect();
        }

        protected virtual void Reset()
        {
            lblStatus.PerformSafely(() => lblStatus.Text = "Disconnected");
            btnConnect.PerformSafely(() => btnConnect.Text = "Connect");
            btnSubscribe.PerformSafely(() => btnSubscribe.Text = "Start");
            btnConnect.PerformSafely(() => btnConnect.Enabled = true);
            tbCurrentSymbol.PerformSafely(() => tbCurrentSymbol.Text = string.Empty);
            tbBuyQty.PerformSafely(() => tbBuyQty.Text = string.Empty);
            tbSellQty.PerformSafely(() => tbSellQty.Text = string.Empty);
        }

        protected virtual bool CheckConnection()
        {

            if (api.IsConnected)
            {
                lblStatus.PerformSafely(() => lblStatus.Text = "Connected");
                btnConnect.PerformSafely(() => btnConnect.Text = "Disconnect");
                btnConnect.PerformSafely(() => btnConnect.Enabled = true);
                return true;
            }
            else
            {
                lblStatus.PerformSafely(() => lblStatus.Text = "Disconnected");
                btnConnect.PerformSafely(() => btnConnect.Text = "Connect");
                btnConnect.PerformSafely(() => btnConnect.Enabled = true);
                return false;
            }
        }

        #endregion

        #region Events
        protected virtual void FrmMarket_Load(object sender, EventArgs e)
        {
            PrepareGrid();
            if (!CheckConnection())
                Connect();
        }

        protected virtual void btnConnect_Click(object sender, EventArgs e)
        {
            if (api.IsConnected)
                Disconnect();
            else
                Connect();
        }

        protected virtual void btnSubscribe_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(tbCurrentSymbol.Text))
            {
                tbCurrentSymbol.PerformSafely(() => tbCurrentSymbol.Text = string.Empty);
                btnSubscribe.PerformSafely(() => btnSubscribe.Text = "Start");
            }
            else if (api.IsConnected)
            {
                string symbol = cbSymbol.Text;
                if (!api.IsOrderBookSubscribed(symbol) || !api.IsTickerSubscribed(symbol))
                {
                    Subscribe(symbol);
                }
                tbCurrentSymbol.PerformSafely(() => tbCurrentSymbol.Text = cbSymbol.Text);
                btnSubscribe.PerformSafely(() => btnSubscribe.Text = "Stop");
            }
            else
            {
                MessageBox.Show("Not connected.");
            }
        }

        protected async virtual void btnOrders_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(tbCurrentSymbol.Text))
            {
                IOrder[] orders = await api.GetOrdersAsync(tbCurrentSymbol.Text, true);
                if (orders != null)
                {
                    foreach (IOrder o in orders)
                    {
                        PrependText(o.OrderID.ToString() + " [" + o.Price + "][" + o.Amount + "]", tbMessages);
                    }
                    PrependText(orders.Length.ToString() + " orders returned.", tbMessages);
                }
                else
                {
                    PrependText("Failed to retrieve orders.", tbMessages);
                }
            }
            else
            {
                MessageBox.Show("Subscribe to symbol first.");
            }
        }

        protected virtual async void btnBuy_Click(object sender, EventArgs e)
        {
            decimal size = Tools.ToSafeDecimal(tbBuyQty.Text);
            decimal price = Tools.ToSafeDecimal(tbBuyPrice.Text);
            if (size != 0.0M && price > 0.0M)
            {
                IResult result = await OrderManager.Instance.PostBuyOrderAsync(api, cbSymbol.Text, OrderType.Limit, size, price);
                if (result.Success)
                {
                    PrependText("Order placed. " + ((IOrder)result).OrderID, tbMessages);
                }
                else
                {
                    PrependText("Order failed. " + result.Message, tbMessages);
                }
            }
            else
            {
                MessageBox.Show("Enter valid buy Qty.");
            }
        }

        protected virtual async void btnSell_Click(object sender, EventArgs e)
        {
            decimal size = Tools.ToSafeDecimal(tbSellQty.Text);
            decimal price = Tools.ToSafeDecimal(tbSellPrice.Text);
            if (size != 0.0M && price > 0.0M)
            {
                IResult result = await OrderManager.Instance.PostSellOrderAsync(api, cbSymbol.Text, OrderType.Limit, size, price);
                if (result.Success)
                {
                    PrependText("Order placed. " + ((IOrder)result).OrderID, tbMessages);
                }
                else
                {
                    PrependText("Order failed. " + result.Message, tbMessages);
                }
            }
            else
            {
                MessageBox.Show("Enter valid sell Qty.");
            }
        }

        protected virtual async void btnCancel_Click(object sender, EventArgs e)
        {
            string orderId = tbCancelOrder.Text;
            IResult res = await OrderManager.Instance.CancelOrderAsync(api, orderId);
            if (res.Success)
            {
                PrependText("Order cancelled. " + orderId, tbMessages);
            }
            else
            {
                PrependText("Order cancel failed. " + orderId + " " + res.Message, tbMessages);
            }
        }

        protected virtual async void btnAmend_Click(object sender, EventArgs e)
        {
            string orderId = tbCancelOrder.Text;
            IResult r = null;
            decimal? qty = Tools.ToSafeDecimal(amendQty.Text);
            decimal? price = Tools.ToSafeDecimal(tbAmendPrice.Text);
            string symbol = cbSymbol.Text;
            if (price == 0.0M) price = null;
            if (qty == 0.0M) qty = null;

            if (!string.IsNullOrEmpty(symbol) && orderId != string.Empty && (qty.HasValue || price.HasValue))
            {
                r = await OrderManager.Instance.AmendOrderAsync(api, symbol, orderId, qty, price);
                if (r.Success)
                    PrependText("Order amended. " + ((IOrder)r).OrderID, tbMessages);
                else
                    PrependText("Order amend failed. " + orderId + " " + r.Message, tbMessages);
            }
            else
            {
                MessageBox.Show("Enter Symbol, Order ID and new Qty/Price");
            }
        }

        protected virtual void Api_OnTickerChanged(object sender, TickerChangedEventArgs e)
        {
            if (this.Disposing) return;
            if (e.Symbol.Equals(tbCurrentSymbol.Text, StringComparison.InvariantCultureIgnoreCase))
            {
                lblLastPrice.PerformSafely(() => lblLastPrice.Text = Tools.ToSafeDouble(e.MarketData.LastPrice).ToString());
            }
        }

        protected virtual void Api_OnOrderBookChanged(object sender, OrderBookChangedEventArgs e)
        {
            if (this.Disposing) return;
            if (e.OrderBook.Symbol == tbCurrentSymbol.Text)
                ShowOrderBook(e.OrderBook, dgvMessage);
        }

        protected virtual void Api_OnMessage(object sender, ApiMessageEventArgs e)
        {
            if (this.Disposing) return;
            if (!string.IsNullOrEmpty(e.ApiMessage))
            {
                PrependText(e.ApiMessage, tbMessages);
            }
        }

        protected virtual void Api_OnConnectionError(object sender, EventArgs e)
        {
            if (this.Disposing) return;
            WebSocketSharp.ErrorEventArgs evt = (WebSocketSharp.ErrorEventArgs)e;
            PrependText("Socker Error. " + evt.Message + Environment.NewLine +
                (evt.Exception == null? string.Empty : evt.Exception.Message) + Environment.NewLine, tbMessages);
        }

        protected virtual void Api_OnConnectionClosed(object sender, EventArgs e)
        {
            if (this.Disposing) return;
            CloseEventArgs evt = (CloseEventArgs)e;
            string msg = "Connection is closed. ";
            msg += " Code " + evt.Code.ToString();

            PrependText(msg, tbMessages);
            Reset();
        }

        protected virtual void Api_OnConnectionEstablished(object sender, EventArgs e)
        {
            if (this.Disposing) return;
            CheckConnection();
        }

        protected virtual void FrmMarket_FormClosing(object sender, FormClosingEventArgs e)
        {
            api.OnConnectionEstablished -= Api_OnConnectionEstablished;
            api.OnConnectionClosed -= Api_OnConnectionClosed;
            api.OnConnectionError -= Api_OnConnectionError;
            api.OnMessage -= Api_OnMessage;
            api.OnTickerChanged -= Api_OnTickerChanged;
            api.OnOrderBookChanged -= Api_OnOrderBookChanged;
        }

        private void dgvMessage_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                object val = dgvMessage.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                if (val != null)
                {

                    if (e.ColumnIndex == 2) //buy px col
                    {
                        tbSellPrice.Text = val.ToString();
                    }
                    else if (e.ColumnIndex == 3) //sell px col
                    {
                        tbBuyPrice.Text = val.ToString();
                    }


                    qCalc[0] = qCalc[1];
                    qCalc[1] = dgvMessage.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    UpdateCalc();
                }
            }
        }

        private static DataGridViewCell[] qCalc = { null, null };


        #endregion

        private void dgvMessage_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if (qCalc[0] == dgvMessage.Rows[e.RowIndex].Cells[e.ColumnIndex] ||
                  qCalc[1] == dgvMessage.Rows[e.RowIndex].Cells[e.ColumnIndex])
                {
                    UpdateCalc();
                }
            }
        }

        private void UpdateCalc()
        {            
            decimal d0, d1;
            if (qCalc[0] != null && decimal.TryParse(qCalc[0].Value.ToString(), out d0) &&
                qCalc[1] != null && decimal.TryParse(qCalc[1].Value.ToString(), out d1))
            {
                decimal diff = Math.Abs(d1 - d0);
                if (d1 != 0.0M || d0 != 0.0M)
                {
                    decimal dd = d1 > d0 ? d0 : d1;
                    Main m = (Main)MdiParent;
                    if (dd != 0.0M)
                    {
                        diff = diff / (d1 > d0 ? d0 : d1) * 100;
                        m.SetStatusStripLabel("statusLabelCalc", "Spread(%): " + diff.ToString());
                    }
                    else
                    {
                        m.SetStatusStripLabel("statusLabelCalc", "Spread(%): --");
                    }

                }
            }
        }

    }
}
