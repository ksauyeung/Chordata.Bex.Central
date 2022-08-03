using System;
using System.Linq;
using System.Collections.Generic;
using Chordata.Bex.Api.Interface;
using Chordata.Bex.Central.Data;
using System.Threading.Tasks;
using Chordata.Bex.Central.Common;
using System.Threading;

namespace Chordata.Bex.Central
{
    public class MakerTaker5 : Interface.IStrategy
    {
        /* OP field descriptions:
         * Name:    Strategy name
         * Text1:   Maker API
         * Text2:   Maker instrument
         * Text3:   Taker API
         * Text4:   Taker instrument
         * Text5:   Maker side
         * Num1:    Maker-Taker order qty
         * Num2:    Opening hurdle rate
         * Num3:    Closing hurdle rate
         * Num4:    Hours before settlement of early close cutoff time
         * Num5:    Minutes before settlement of closing of position                
         * Num6:    Taker Taker Closing hurdle rate
         */

        /* Run field descriptions:
         * ---------------Run parameters (copied from OP)--------------------v
         * Text1:   Maker API (The Left)
         * Text2:   Maker instrument
         * Text3:   Taker API (The Right)
         * Text4:   Taker instrument
         * Text5:   Maker side
         * Num1:    Maker-Taker order qty
         * Num2:    Opening hurdle rate
         * Num3:    Closing hurdle rate
         * Num4:    Hours before settlement of early close cutoff time
         * Num5:    Minutes before settlement of closing of position         
         * Num6:    Taker Taker Closing hurdle rate
         * v-----------------------Op parameters-----------------------------v
         * Num7:    Taker price bracket. The actual taker price is discounted for sell, added for buy:
         *  ActualBuyQuote = quote + abs(num7%) 
         *  ActualSellQuote = quote - abs(num7%)
         * v-----------------------Execution---------------------------------v
         * Num7:    Maker opening executed Qty
         * Num8:    Taker opening executed Qty
         * Num9:    Maker opening executed price
         * Num10:   Taker opening executed price    
         * Num11:   Maker closing executed qty
         * Num12:   Taker closing executed qty
         * Num13:   Maker closing executed price
         * Num14:   Taker closing executed price         
         * 
         * Num15:   Maker opening total price net fees
         * Num16:   Taker opening total price net fees
         * Num17:   Maker closing total price net fees
         * Num18:   Taker closing total price net fees
         * 
         * Date1:   Maker settlement datetime
         * Order1:  Opening maker order
         * Order2:  Opening taker order
         * Order3:  Closing maker order left
         * Order4:  Closing taker order executed for left maker order
         * Order5:  Closing maker order right
         * Order6:  Closing taker order executed for right maker order
         
         */

        /* Maker-Taker (short-long) (BitMEX-Poloniex)
         *
         * General Flow:
         * 
         * -Opening-
         * A Maker (sell) order is placed on the maker API (BitMEX)
         * Maker order price is constantly adjusted against the top taker (ask) price on Poloniex (+1.2% hurdle + 0.25% takerfee)
         * If maker order is hit, place a taker (buy) order on taker API (Poloniex)
         * 
         * Set a timer to fire 30 minutes before maker order closing/expiry 
         * 
         * -Closing-
         * A maker (buy) order is placed on the maker API (BitMEX)
         * Maker order is priced less 0.2% of the default hurdle rate against the taker API (Poloniex)
         * If maker order is hit, place a taker (sell) order of the oposite side on the taker API (Poloniex)
         * The position is closed.
         * 
         * If maker order is not hit and the timer expires...
         * Cancel the maker order
         * A taker market (buy) order is placed on BitMEX to close the BitMEX short position
         * A taker market (sell) order is placed on Poloniex to close the Poloniex long position.
         */

        #region Declarations         

        private bool isStarted;
        const ushort MAX_ERROR_COUNT = 3; //maximum error occurance before hard stop on the order
        const ushort MAX_MAKER_RETRY = 8;
        const ushort MAX_MAKER_RETRY_INTERVAL_SECONDS = 5;
        private List<IApi> subscribedApis = new List<IApi>();
        private IDictionary<int, MakerTakerSet> makerTakerSets = new Dictionary<int, MakerTakerSet>();
        public event EventHandler<string> OnMessage;
        private readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        const double HURDLE_BOOST_FACTOR = 0.05; //5 percent;
        const ushort HURDLE_BOOST_PERIOD_MINUTE = 5; //5 minutes;
        private const bool USE_PRICE_BRACKET = true;
        private SemaphoreSlim smfStartStop = new SemaphoreSlim(1);
        #endregion

        #region Methods

        public async void Start()
        {
            await smfStartStop.WaitAsync();
            try
            {
                if (isStarted) return; isStarted = true;
                Message("Started", 0);
                Run();
            }
            finally
            {
                smfStartStop.Release();
            }
        }

        public async Task Stop()
        {
            if (!isStarted) return;
            await smfStartStop.WaitAsync();
            try
            {
                Message("Stopping...", 0);
                IResult res = null;
                //using (var db = new Tuna())
                //{
                Run[] runs = new Run[makerTakerSets.Values.Count];
                int i = -1;
                foreach (MakerTakerSet set in makerTakerSets.Values)
                {
                        
                    if (set.TimerSettlement != null)
                        set.TimerSettlement.Stop();

                    if (set.MakerOpeningOrder != null)
                    {
                        set.MakerOpeningOrder.Stop("Maker-Taker stopped");
                        if (!set.MakerOpeningOrder.IsFilled)
                        {
                            res = await set.MakerOpeningOrder.CancelOrder("Maker-Taker stopped");
                            if(!res.Success)
                                Message("Could not cancel opening maker order " + res.Message, set.RunRecord.Id);
                        }
                    }
                    if (set.MakerClosingOrderLeft != null) set.MakerClosingOrderLeft.Stop("Maker-Taker stopped");
                    if (set.MakerClosingOrderRight != null) set.MakerClosingOrderRight.Stop("Maker-Taker stopped");

                    set.RunRecord.Status = (int)RunStatus.Stopped;

                    Run current = set.RunRecord;                        
                    runs[++i] = set.RunRecord;

                    set.Dispose();
                    //db.Entry(current).State = System.Data.Entity.EntityState.Modified;
                }
                DBUtil.WriteRunRecords(runs);
                    //DBUtil.SaveDbAsync(db); //db.SaveChangesAsync();
                //}
                makerTakerSets.Clear();
                isStarted = false;
                Message("Stopped", 0);
            }
            finally
            {
                smfStartStop.Release();
            }
        }

        /// <summary>
        /// Run an operation. Run all available if no ID is provide.
        /// </summary>
        private async void Run(int OpID = -1)
        {
            MakerTakerSet set;
            log.Debug("Run(" + OpID.ToString() + ")");
            using (var db = new Tuna())
            {
                //OPs left join Runs
                var queryOpRuns = from op in db.OPs.Where(o => o.Name == "Maker-Taker" && o.Enabled == true && (OpID == -1 || o.Id == OpID))
                                  join run in db.Runs.Where(o => o.Status <= (int)RunStatus.Running)
                on op.Id equals run.OpId into oprun
                                  from run in oprun.DefaultIfEmpty()
                                  select new { OP = op, Run = run };

                List<MakerTakerSet> tmpList = new List<MakerTakerSet>();
                int cnt = queryOpRuns.Count();
                foreach (var v in queryOpRuns)
                {
                    set = new MakerTakerSet();
                    set.OpRecord = v.OP;
                    set.RunRecord = v.Run;
                    set.OnMessage += Set_OnMessage;
                    if (set.RunRecord == null) //if no run, initialize new run
                    {
                        set.RunRecord = new Run()
                        {
                            OpId = set.OpRecord.Id,
                            Text1 = set.OpRecord.Text1,
                            Text2 = set.OpRecord.Text2,
                            Text3 = set.OpRecord.Text3,
                            Text4 = set.OpRecord.Text4,
                            Text5 = set.OpRecord.Text5,
                            Num1 = set.OpRecord.Num1,
                            Num2 = set.OpRecord.Num2,
                            Num3 = set.OpRecord.Num3,
                            Num4 = set.OpRecord.Num4,
                            Num5 = set.OpRecord.Num5,
                            Num6 = set.OpRecord.Num6,
                            Date1 = set.OpRecord.Date1,
                            Status = (int)RunStatus.Ready
                        };
                        db.Runs.Add(set.RunRecord);
                    }
                    log.Debug("Init set...");
                    set.Maker = ApiManager.Instance.GetApi(set.RunRecord.Text1);
                    set.Taker = ApiManager.Instance.GetApi(set.RunRecord.Text3);
                    set.OriginalOpeningHurdleRate = set.RunRecord.Num2.GetValueOrDefault();
                    set.OriginalClosingHurdleRate = set.RunRecord.Num3.GetValueOrDefault();

                    if (!subscribedApis.Contains(set.Maker))
                    {
                        subscribedApis.Add(set.Maker);
                        set.Maker.OnConnectionClosed += Exchange_OnConnectionClosed;
                    }
                    set.Maker.Connect(true);
                    if (!subscribedApis.Contains(set.Taker))
                    {
                        subscribedApis.Add(set.Taker);
                        set.Taker.OnConnectionClosed += Exchange_OnConnectionClosed;
                    }
                    set.Taker.Connect(true);

                    do
                    {
                        await Task.Delay(1000);
                    }
                    while (!set.Taker.IsConnected || !set.Maker.IsConnected);
                    log.Debug("Connected.");
                    //---Both APIs should be connected below this line---   

                    set.TakerTakerFee = (await set.Taker.GetInstrumentAsync(set.RunRecord.Text4)).TakerFee.GetValueOrDefault();
                    set.TakerMakerFee = (await set.Taker.GetInstrumentAsync(set.RunRecord.Text4)).MakerFee.GetValueOrDefault();
                    set.MakerTakerFee = (await set.Maker.GetInstrumentAsync(set.RunRecord.Text2)).TakerFee.GetValueOrDefault();
                    set.MakerMakerFee = (await set.Maker.GetInstrumentAsync(set.RunRecord.Text2)).MakerFee.GetValueOrDefault();

                    if (!set.Taker.IsOrderBookSubscribed(set.RunRecord.Text4))
                    {
                        Message("Subscribing to " + set.RunRecord.Text4 + ".", set.RunRecord.Id);
                        set.Taker.SubscribeToOrderBookAsync(set.RunRecord.Text4);
                    }
                    if (!set.Maker.IsOrderBookSubscribed(set.RunRecord.Text2))
                    {
                        Message("Subscribing to " + set.RunRecord.Text2 + ".", set.RunRecord.Id);
                        set.Maker.SubscribeToOrderBookAsync(set.RunRecord.Text2);
                    }

                    set.Maker.SubscribeToOrdersAsync(string.Empty);
                    set.Taker.SubscribeToOrdersAsync(string.Empty);

                    //if (set.Maker.GetOrderBook(set.RunRecord.Text2) == null || set.Taker.GetOrderBook(set.RunRecord.Text4) == null)
                    if (!set.Maker.IsOrderBookSubscribed(set.RunRecord.Text2) || !set.Taker.IsOrderBookSubscribed(set.RunRecord.Text4))
                    {
                        Message("Waiting for orderbooks...", set.RunRecord.Id);
                        while (set.Maker.GetOrderBook(set.RunRecord.Text2) == null || set.Taker.GetOrderBook(set.RunRecord.Text4) == null)
                            await Task.Delay(1000);
                        Message("Orderbooks ok.", set.RunRecord.Id);
                    }

                    log.Debug("Preparing orders...");
                    string message = string.Empty;
                    bool prepared = await PrepareOrders(set);
                    if (prepared)
                    {
                        //begin the settlement timer
                        int Minutes_Before_Settlement = set.RunRecord.Num5.HasValue ? (int)set.RunRecord.Num5.GetValueOrDefault() : 2;
                        if (set.RunRecord.Date1.HasValue && DateTime.UtcNow <= set.RunRecord.Date1.Value.AddMinutes(-Minutes_Before_Settlement - 1))
                        {
                            double interval = (set.RunRecord.Date1.Value.AddMinutes(-Minutes_Before_Settlement) - DateTime.UtcNow).TotalMilliseconds;
                            set.TimerSettlement = new Common.Timer(interval > Int32.MaxValue ? Int32.MaxValue : interval);
                            set.TimerSettlement.Tag = set;
                            set.TimerSettlement.AutoReset = false;
                            set.TimerSettlement.Elapsed += SettlementTimer_Elapsed;
                            set.TimerSettlement.Start();
                        }

                        if (!set.IsPositionOpened)
                        {
                            set.MakerOpeningOrder.OnOrderHit += MakerOpeningOrder_OnOrderHit;
                            set.MakerOpeningOrder.OnError += MakerOpeningOrder_OnError;
                            set.MakerOpeningOrder.OnOrderCancelled += MakerOpeningOrder_OnOrderCancelled;
                            set.MakerOpeningOrder.OnOrderAmended += MakerOrder_OnOrderAmended;
                            set.MakerOpeningOrder.Start();
                            set.RunRecord.Started = DateTime.UtcNow;
                        }
                        else
                        {
                            set.MakerClosingOrderLeft.OnOrderHit += MakerClosingOrderLeft_OnOrderHit;
                            set.MakerClosingOrderLeft.OnError += MakerClosingOrderLeft_OnError;
                            set.MakerClosingOrderLeft.OnOrderCancelled += MakerClosingOrderLeft_OnOrderCancelled;
                            set.MakerClosingOrderLeft.OnOrderAmended += MakerOrder_OnOrderAmended;
                            set.MakerClosingOrderRight.OnOrderHit += MakerClosingOrderRight_OnOrderHit;
                            set.MakerClosingOrderRight.OnError += MakerClosingOrderRight_OnError;
                            set.MakerClosingOrderRight.OnOrderCancelled += MakerClosingOrderRight_OnOrderCancelled;
                            set.MakerClosingOrderRight.OnOrderAmended += MakerOrder_OnOrderAmended;
                            set.MakerClosingOrderLeft.Start();
                            set.MakerClosingOrderRight.Start();
                        }
                        set.RunRecord.Status = (int)RunStatus.Running;

                        //Listen to orderbook for taker-taker close
                        if (set.IsPositionOpened && !set.IsPositionClosed)
                        {
                            set.OnPositionClosed += Set_OnPositionClosed;
                            set.StartClosingTakerTaker();
                        }
                    }
                    else
                    {
                        set.RunRecord.Message = set.FaultMessage;
                        set.RunRecord.Status = (int)RunStatus.Fault;
                    }
                    tmpList.Add(set);
                }
                await DBUtil.SaveDbAsync(db);

                foreach (MakerTakerSet s in tmpList)
                {
                    makerTakerSets.Add(s.RunRecord.Id, s);
                    log.Debug("Set " + s.RunRecord.Id.ToString() + " initialized.");
                }
                tmpList.Clear();
            }
        }

        private void Set_OnPositionClosed(object sender, EventArgs e)
        {
            AfterPositionClosed((MakerTakerSet)sender);
        }

        private void Set_OnMessage(object sender, string e)
        {
            Message(e, ((MakerTakerSet)sender).RunRecord.Id);
        }

        /// <summary>
        /// Rerun current set
        /// </summary>
        /// <param name="set"></param>
        private async void Rerun(MakerTakerSet set)
        {
            bool prepared = await PrepareOrders(set);
            if (prepared)
            {
                int Minutes_Before_Settlement = set.RunRecord.Num5.HasValue ? (int)set.RunRecord.Num5.GetValueOrDefault() : 2;
                if (set.RunRecord.Date1.HasValue && DateTime.UtcNow <= set.RunRecord.Date1.Value.AddMinutes(-Minutes_Before_Settlement - 1))
                {
                    //set the timer to trigger X minute before expiry
                    if (set.TimerSettlement != null)
                        set.TimerSettlement.Stop();

                    //set the timer to trigger X minute before expiry
                    double interval = (set.RunRecord.Date1.Value.AddMinutes(-Minutes_Before_Settlement) - DateTime.UtcNow).TotalMilliseconds;
                    set.TimerSettlement = new Common.Timer(interval > Int32.MaxValue ? Int32.MaxValue : interval);
                    set.TimerSettlement.Tag = set;
                    set.TimerSettlement.AutoReset = false;
                    set.TimerSettlement.Elapsed += SettlementTimer_Elapsed;
                    set.TimerSettlement.Start();
                }

                if (!set.IsPositionOpened)
                {
                    //run open order
                    set.MakerOpeningOrder.OnOrderHit += MakerOpeningOrder_OnOrderHit;
                    set.MakerOpeningOrder.OnError += MakerOpeningOrder_OnError;
                    set.MakerOpeningOrder.OnOrderCancelled += MakerOpeningOrder_OnOrderCancelled;
                    set.MakerOpeningOrder.OnOrderAmended += MakerOrder_OnOrderAmended;
                    set.RunRecord.Started = DateTime.UtcNow;
                    set.MakerOpeningOrder.Start();

                }
                else if (!set.IsPositionClosed)
                {
                    set.MakerClosingOrderLeft.OnOrderHit += MakerClosingOrderLeft_OnOrderHit;
                    set.MakerClosingOrderLeft.OnError += MakerClosingOrderLeft_OnError;
                    set.MakerClosingOrderLeft.OnOrderCancelled += MakerClosingOrderLeft_OnOrderCancelled;
                    set.MakerClosingOrderLeft.OnOrderAmended += MakerOrder_OnOrderAmended;
                    set.MakerClosingOrderRight.OnOrderHit += MakerClosingOrderRight_OnOrderHit;
                    set.MakerClosingOrderRight.OnError += MakerClosingOrderRight_OnError;
                    set.MakerClosingOrderRight.OnOrderCancelled += MakerClosingOrderRight_OnOrderCancelled;
                    set.MakerClosingOrderRight.OnOrderAmended += MakerOrder_OnOrderAmended;

                    set.MakerClosingOrderLeft.Start();
                    set.MakerClosingOrderRight.Start();
                }
                else //closed
                {
                    //reset
                }
                makerTakerSets.Remove(set.RunRecord.Id);
                makerTakerSets.Add(set.RunRecord.Id, set);
                log.Debug("Set " + set.RunRecord.Id.ToString() + " reinitialized.");
                //Listen to orderbook for taker-taker close
                if (set.IsPositionOpened && !set.IsPositionClosed)
                {
                    set.OnPositionClosed += Set_OnPositionClosed;
                    set.StartClosingTakerTaker();
                }
                using (var db = new Tuna())
                {
                    var run = db.Entry(set.RunRecord);
                    run.Entity.Status = (int)RunStatus.Running;
                    run.State = System.Data.Entity.EntityState.Modified;
                    await DBUtil.SaveDbAsync(db);
                }

            }
        }

        /// <summary>
        /// Close the position by executing taker orders
        /// </summary>
        /// <param name="set"></param>
        /// <returns></returns>
        private async Task<IResult> ClosePosition(MakerTakerSet set, string reason)
        {
            // Case 1: Position fully opened. Opening taker executed. Closing maker may or may not exist, but no closing taker, move the price to market, after that also place a closing taker. This is the most normal case.
            // Case 2: Position half opened, aka one opening maker leg only. 
            // Case 3: No position open. 

            // Do 1: Cancel the closing maker order and execute market order for both sides
            // Do 2: ??? Calculate the break even price (just the two taker fees). If better than market, execute a market order to close. ????
            // Do 3: Cancel everything and run away.

            Result result = null;
            string message = string.Empty;
            Run record = set.RunRecord;
            decimal topPrice = 0.0M, totalPrice = 0.0M, avgPrice = 0.0M;

            if (!string.IsNullOrEmpty(record.Order1) && !string.IsNullOrEmpty(record.Order2)) //case 1
            {
                if (!string.IsNullOrEmpty(record.Order3))
                {
                    IResult res = await set.MakerClosingOrderLeft.CancelOrder(reason);
                    if (!res.Success)
                    {
                        message += set.Maker.ToString() + " Maker " + record.Text2 + " order " + record.Order3 + " failed to cancel.\n";
                    }
                }
                if (!string.IsNullOrEmpty(record.Order5))
                {
                    IResult res = await set.MakerClosingOrderRight.CancelOrder(reason);
                    if (!res.Success)
                    {
                        message += set.Taker.ToString() + " Maker " + record.Text4 + " order " + record.Order5 + " failed to cancel.\n";
                    }
                }

                if (record.Num7.GetValueOrDefault() > 0 && record.Num8.GetValueOrDefault() > 0) //opening maker and taker already executed 
                {
                    IResult res;
                    if (record.Text5 == "Sell")
                    {
                        IOrderBook obLeft = set.Maker.GetOrderBook(record.Text2);
                        if (!Helper.GetTopPrice(obLeft, Api.Side.Sell, record.Num7.Value, out topPrice, out totalPrice, out avgPrice))
                            return new Result(false, "Not enough quantities on orderbook or quoting has failed " + set.Maker.ToString() + " " + record.Text2);

                        res = await set.ExecuteTakerOrder(set.Maker, record.Text2, Api.Side.Buy, record.Num7.Value, topPrice, set.OpRecord.Num7.GetValueOrDefault(), Api.OrderOptions.Close); //left taker order
                        if (res.Success)
                        {
                            record.Num11 = ((IOrder)res).NetAmount.GetValueOrDefault();
                            record.Num13 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                            record.Order6 = ((IOrder)res).OrderID;

                            IOrderBook obTaker = set.Taker.GetOrderBook(record.Text4);
                            if (!Helper.GetTopPrice(obTaker, Api.Side.Buy, record.Num8.Value, out topPrice, out totalPrice, out avgPrice))
                                return new Result(false, "Not enough quantities on orderbook or quoting has failed " + set.Taker.ToString() + " " + record.Text2);

                            res = await set.ExecuteTakerOrder(set.Taker, record.Text4, Api.Side.Sell, record.Num8.Value, topPrice, set.OpRecord.Num7.GetValueOrDefault(), Api.OrderOptions.Close); //right taker order

                            if (res.Success)
                            {
                                record.Num12 = ((IOrder)res).NetAmount.GetValueOrDefault();
                                record.Num14 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                                record.Order4 = ((IOrder)res).OrderID;

                                //I must have the confirmed amount here, if not, get it.
                                if (set.RunRecord.Num12 == 0.0M)
                                {
                                    IOrder[] takerExecuted = await set.Taker.GetOrdersAsync(set.RunRecord.Order4);
                                    if (takerExecuted.Length > 0)
                                    {
                                        set.RunRecord.Num12 = takerExecuted[0].NetAmount;
                                        set.RunRecord.Num14 = takerExecuted[0].AveragePrice;
                                    }
                                }

                                //I must have the confirmed amount here, if not, get it.
                                if (set.RunRecord.Num11 == 0.0M)
                                {
                                    IOrder[] takerExecuted = await set.Maker.GetOrdersAsync(set.RunRecord.Order6);
                                    if (takerExecuted.Length > 0)
                                    {
                                        set.RunRecord.Num11 = takerExecuted[0].NetAmount;
                                        set.RunRecord.Num13 = takerExecuted[0].AveragePrice;
                                    }
                                }

                            }
                        }
                        result = new Result(res.Success, message + res.Message);
                    }
                    else
                    {
                        IOrderBook obLeft = set.Maker.GetOrderBook(record.Text2);
                        if (!Helper.GetTopPrice(obLeft, Api.Side.Buy, record.Num7.Value, out topPrice, out totalPrice, out avgPrice))
                            return new Result(false, "Not enough quantities on orderbook or quoting has failed " + set.Maker.ToString() + " " + record.Text2);

                        res = await set.ExecuteTakerOrder(set.Maker, record.Text2, Api.Side.Sell, record.Num7.Value, topPrice, set.OpRecord.Num7.GetValueOrDefault(), Api.OrderOptions.Close); //left taker
                        if (res.Success)
                        {
                            record.Num11 = ((IOrder)res).NetAmount.GetValueOrDefault();
                            record.Num13 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                            record.Order6 = ((IOrder)res).OrderID;

                            IOrderBook obRight = set.Taker.GetOrderBook(record.Text4);
                            if (!Helper.GetTopPrice(obRight, Api.Side.Sell, record.Num8.Value, out topPrice, out totalPrice, out avgPrice))
                                return new Result(false, "Not enough quantities on orderbook or quoting has failed " + set.Taker.ToString() + " " + record.Text2);

                            res = await set.ExecuteTakerOrder(set.Taker, record.Text4, Api.Side.Buy, record.Num8.Value, topPrice, set.OpRecord.Num7.GetValueOrDefault(), Api.OrderOptions.Close); //right taker order
                            if (res.Success)
                            {
                                record.Num12 = ((IOrder)res).NetAmount.GetValueOrDefault();
                                record.Num14 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                                record.Order4 = ((IOrder)res).OrderID;

                                //I must have the confirmed amount here, if not, get it.
                                if (set.RunRecord.Num12 == 0.0M)
                                {
                                    IOrder[] takerExecuted = await set.Maker.GetOrdersAsync(set.RunRecord.Order4);
                                    if (takerExecuted.Length > 0)
                                    {
                                        set.RunRecord.Num12 = takerExecuted[0].NetAmount;
                                        set.RunRecord.Num14 = takerExecuted[0].AveragePrice;
                                    }
                                }

                                //I must have the confirmed amount here, if not, get it.
                                if (set.RunRecord.Num11 == 0.0M)
                                {
                                    IOrder[] takerExecuted = await set.Maker.GetOrdersAsync(set.RunRecord.Order6);
                                    if (takerExecuted.Length > 0)
                                    {
                                        set.RunRecord.Num11 = takerExecuted[0].NetAmount;
                                        set.RunRecord.Num13 = takerExecuted[0].AveragePrice;
                                    }
                                }

                            }
                        }
                        result = new Result(res.Success, message + res.Message);
                    }
                }

            }
            else if (!string.IsNullOrEmpty(record.Order1) && record.Num7.GetValueOrDefault() > 0.0M && string.IsNullOrEmpty(record.Order2)) //case 2
            {
                result = new Result(false, "Run " + record.Id.ToString() + " has an opening maker leg only, please manually resolve this and reset the run.");
            }
            else
            {
                if (!string.IsNullOrEmpty(record.Order1))
                {
                    IResult res = await set.MakerOpeningOrder.CancelOrder(reason);
                    if (!res.Success)
                        message += set.Maker.ToString() + " Maker " + record.Text2 + " order " + record.Order1 + " failed to cancel.\n";
                    else
                        message += "Opening maker cancelled due to settlement. ";
                    result = new Result(res.Success, message + res.Message);
                }
            }
            return result;
        }

        /// <summary>
        /// Runs after the position is closed. Writes status to database.
        /// </summary>
        /// <param name="set"></param>
        private void AfterPositionClosed(MakerTakerSet set)
        {
            int opId = set.OpRecord.Id;
            Message("Completed. Run ID " + set.RunRecord.Id.ToString(), set.RunRecord.Id);
            if (set.TimerSettlement != null)
                set.TimerSettlement.Stop();
            set.RunRecord.Completed = DateTime.UtcNow;
            set.RunRecord.Status = (int)RunStatus.Completed;
            set.RunRecord.Message = set.FaultMessage;
            using (var db = new Tuna())
            {
                var r = db.Entry(set.RunRecord);
                r.State = System.Data.Entity.EntityState.Modified;
                DBUtil.SaveDb(db);
            }
            
            makerTakerSets.Remove(set.RunRecord.Id);
            set.Dispose();

            //Run again before cutoff.
            bool doRestart = true;
            decimal hours = set.RunRecord.Num4.GetValueOrDefault();
            DateTime? settlement = set.RunRecord.Date1;
            if (settlement.HasValue && settlement.Value.AddHours(-(double)hours) <= DateTime.UtcNow)
                doRestart = false;

            if (doRestart)
                Run(set.OpRecord.Id);
        }

        /// <summary>
        /// Prepare the orders for the MakerTakerSet        /// 
        /// </summary>
        /// <param name="set"></param>
        /// <returns>True if successful, the set's orders will be populated. False otherwise.</returns>
        private async Task<bool> PrepareOrders(MakerTakerSet set)
        {
            set.MakerOpeningOrder = null;
            set.MakerClosingOrderLeft = null;
            set.MakerClosingOrderRight = null;
            set.IsPositionOpened = false;
            set.IsPositionClosed = false;
            string msg = string.Empty;
            bool success = false;


            /*------------------New Order---------------------*/
            if (string.IsNullOrEmpty(set.RunRecord.Order1)) //No opening orders, new order
            {
                success = await PlaceOpeningMakerOrder(set);
                if (success)
                    msg = "Opening maker placed: " + set.MakerOpeningOrder.ToString();
                else
                    msg = "Failed to place Opneing maker";

            }

            else if (string.IsNullOrEmpty(set.RunRecord.Order2)) //Opening maker order already placed, but no taker, retrieve
            {
                success = await RetrieveOpeningOrder(set);
                //for retrievals we need to consider the order status, as it maybe invalid.

                if (set.MakerOpeningOrder == null || set.MakerOpeningOrder.IsCancelled) //order retrieval failed
                {
                    success = await PlaceOpeningMakerOrder(set);

                    if (success)
                        msg = "Opening maker placed: " + set.MakerOpeningOrder.ToString();
                    else
                        msg = "Failed to place Opneing maker";
                }
                else if (set.RunRecord.Date1 < DateTime.UtcNow) //expired
                {
                    if (set.MakerOpeningOrder.OrderStatus == Api.OrderStatus.New) //carried over from last week which was not hit
                    {
                        set.RunRecord.Date1 = (await set.Maker.GetInstrumentAsync(set.RunRecord.Text2)).Settle; //refresh the expiry date
                    }
                    else if (set.MakerOpeningOrder.OrderStatus == Api.OrderStatus.PartiallyFilled) //for partially filled, cancel the outstanding qty and make a new order
                    {
                        IResult r = await OrderManager.Instance.CancelOrderAsync(set.Maker, set.MakerOpeningOrder.OrderID); //this will not raise the cancel event
                        if (r.Success)
                        {
                            success = await PlaceOpeningMakerOrder(set);
                            if (success)
                                msg = "Opening maker placed: " + set.MakerOpeningOrder.ToString();
                            else
                                msg = "Failed to place Opneing maker";

                        }
                    }
                    else //for all other status, a new order is placed
                    {
                        success = await PlaceOpeningMakerOrder(set);
                        if (success)
                            msg = "Opening maker placed: " + set.MakerOpeningOrder.ToString();
                        else
                            msg = "Failed to place Opneing maker";
                    }
                }
                else if (set.MakerOpeningOrder.OrderStatus == Api.OrderStatus.Canceled) //order was cancelled but not expired.
                {
                    success = await PlaceOpeningMakerOrder(set);
                    if (success)
                        msg = "Opening maker placed: " + set.MakerOpeningOrder.ToString();
                    else
                        msg = "Failed to place Opneing maker";

                }
                else if (set.MakerOpeningOrder.OrderStatus == Api.OrderStatus.New)
                {
                    msg = "Opening maker retrieved: " + set.MakerOpeningOrder.ToString();
                }
                else //the order is filled alaready, the run is no longer valid
                {
                    msg = "Opening order filled without opening taker.";
                }
            }

            /*----------------Opening Partial fill---or---Position fully opened--------------------*/
            else if (string.IsNullOrEmpty(set.RunRecord.Order4) && string.IsNullOrEmpty(set.RunRecord.Order6)) //position not closed
            {
                if (set.RunRecord.Num7.GetValueOrDefault() < set.RunRecord.Num1.GetValueOrDefault())
                {
                    //For partial filled opening maker, the order must still be alive.
                    //1. attempt to retrieve the maker order
                    //2. If retrieved and is alive then continue trying open the position fully
                    //3. If it is no longer alive, then try continue onto placing closing makers
                    success = await RetrieveOpeningOrder(set);                   
                    if (!success || set.MakerOpeningOrder == null || !set.MakerOpeningOrder.IsOpen) //order retrieval failed
                    {
                        success = false;
                        msg = "The opening order is no longer alive.";
                    }                   
                }
                if (success)
                {
                    msg = "Opening maker (partial) retrieved: " + set.MakerOpeningOrder.ToString();
                }
                else
                {
                    msg = "Closing Makers:";
                    bool leftOrderOk = false, rightOrderOk = false;
                    if (set.RunRecord.Num11.GetValueOrDefault() == 0.0M) //closing maker not executed
                    {
                        if (!string.IsNullOrEmpty(set.RunRecord.Order3))
                        {
                            leftOrderOk = await RetrieveLeftClosingOrders(set);
                            if (leftOrderOk) msg += "\nLeft retrieved: " + set.MakerClosingOrderLeft.ToString();
                        }
                        if (string.IsNullOrEmpty(set.RunRecord.Order3) || !leftOrderOk) //order was cancelled or cannot find
                        {  //place left closing maker                        
                            leftOrderOk = await PlaceLeftClosingMakerOrder(set);
                            if (leftOrderOk)
                                msg += "\nLeft placed: " + set.MakerClosingOrderLeft.ToString();
                            else
                                msg += "\nFailed to place right closing maker.";
                        }
                        if (!leftOrderOk) //if still no left order, cancel the existing right order and return
                        {
                            if (!string.IsNullOrEmpty(set.RunRecord.Order5))
                            {
                                IResult res = await OrderManager.Instance.CancelOrderAsync(set.Taker, set.RunRecord.Order5);
                                if (!res.Success)
                                    msg += "\nRight maker failed to cancel.";
                                set.RunRecord.Order5 = null;
                            }
                        }
                        else //left order ok
                        {
                            if (!string.IsNullOrEmpty(set.RunRecord.Order5))
                            {
                                rightOrderOk = await RetrieveRightClosingOrders(set);
                                if (rightOrderOk) msg += "\nRight retrieved: " + set.MakerClosingOrderRight.ToString();
                            }
                            if (string.IsNullOrEmpty(set.RunRecord.Order5) || !rightOrderOk) //order was cancelled or cannot find
                            { //place right closing maker
                                rightOrderOk = await PlaceRightClosingMakerOrder(set);
                                if (rightOrderOk)
                                    msg += "\nRight placed: " + set.MakerClosingOrderRight.ToString();
                                else
                                    msg += "\nFailed to place right closing maker.";
                            }
                            if (!rightOrderOk) //if still no left order, cancel the existing right order and return
                            {
                                if (!string.IsNullOrEmpty(set.RunRecord.Order3))
                                {
                                    IResult res = await OrderManager.Instance.CancelOrderAsync(set.Maker, set.RunRecord.Order3);
                                    if (!res.Success)
                                        msg += "\nLeft maker failed to cancel.";
                                    set.RunRecord.Order3 = null;
                                }
                            }
                        }
                        success = leftOrderOk && rightOrderOk;
                    }
                    else
                    {
                        msg += "Failed to place left closing maker. A closing maker already executed.";
                    }
                    set.IsPositionOpened = true;
                }
            }



            /*-------------------Position closed---------------------*/
            else
            {
                set.IsPositionOpened = true;
                set.IsPositionClosed = true;
            }

            if (success)
            {
                Message("Orders prepared:\n" + msg, set.RunRecord.Id);
            }
            else
            {
                set.FaultMessage = "Order preparation failed for run ID " + set.RunRecord.Id.ToString() + " . " + msg;
                Message(set.FaultMessage, set.RunRecord.Id);
            }
            return success;
        }

        /// <summary>
        /// Place the opening order
        /// </summary>
        /// <param name="set">Working set to place the opening order onto</param>
        /// <returns></returns>
        private async Task<bool> PlaceOpeningMakerOrder(MakerTakerSet set)
        {
            var makerApi = set.Maker;
            var makerInstrument = set.RunRecord.Text2;
            var makerSide = Api.Tools.ToSide(set.RunRecord.Text5);
            var makerOrderType = Api.OrderType.Limit;
            var makerSize = set.RunRecord.Num1.GetValueOrDefault();
            var makerInitPrice = 0.0M;
            var takerApi = set.Taker;
            var takerInstrument = set.RunRecord.Text4;
            var hurdleRate = set.RunRecord.Num2.GetValueOrDefault();

            PricingScheme ps = PricingScheme.None;
            if (makerSide == Api.Side.Sell)
                ps = PricingScheme.PercentageOfTarget;
            else if (makerSide == Api.Side.Buy)
                ps = PricingScheme.PercentageOfNewPrice;

            set.RunRecord.Num7 = null;
            set.RunRecord.Num8 = null;
            set.RunRecord.Num9 = null;
            set.RunRecord.Num10 = null;
            set.RunRecord.Num11 = null;
            set.RunRecord.Num12 = null;
            set.RunRecord.Num13 = null;
            set.RunRecord.Num14 = null;
            set.RunRecord.Date1 = null;
            set.RunRecord.Order1 = null;
            set.RunRecord.Order2 = null;
            set.RunRecord.Order3 = null;
            set.RunRecord.Order4 = null;
            set.RunRecord.Date1 = (await makerApi.GetInstrumentAsync(makerInstrument)).Settle;

            ushort retry = 0;
            while ((set.MakerOpeningOrder == null || set.MakerOpeningOrder.IsCancelled || set.MakerOpeningOrder.OrderStatus == Api.OrderStatus.None) && MAX_MAKER_RETRY >= retry)
            {
                hurdleRate = set.RunRecord.Num2.GetValueOrDefault();
                set.MakerOpeningOrder = await FloatingPriceOrder5.Create(makerApi,
                makerInstrument,
                makerSide,
                makerOrderType,
                makerSize,
                makerInitPrice,
                takerInstrument,
                takerApi,
                set.OriginalOpeningHurdleRate,
                hurdleRate,
                set.HurdleBoostEffectiveUntil,
                set.TakerTakerFee,
                set.MakerMakerFee,
                ps,
                set.RunRecord.OpId);
                                
                if (set.MakerOpeningOrder == null || set.MakerOpeningOrder.IsCancelled || set.MakerOpeningOrder.OrderStatus == Api.OrderStatus.None)
                {
                    retry++;
                    if (MAX_MAKER_RETRY >= retry)
                    {
                        if (set.MakerOpeningOrder != null && set.MakerOpeningOrder.IsCancelled && set.MakerOpeningOrder.CancelReason == Api.CancelReason.OrderIsPostOnly)
                        { //if its cancelled due to post_only then boost the hurdle rate
                            set.BoostOpeningHurdleRate(HURDLE_BOOST_FACTOR, HURDLE_BOOST_PERIOD_MINUTE);
                        }
                        else
                        { //wait for retry
                            await Task.Delay(retry * MAX_MAKER_RETRY_INTERVAL_SECONDS * 1000);
                        }
                    }
                    else
                    { //retry has exhausted
                        set.FaultMessage = "Failed to place left opening maker order. ";
                        if (set.MakerOpeningOrder != null)
                        {
                            if (set.MakerOpeningOrder.IsCancelled)
                                set.FaultMessage += "Order is canceled by exchange. ";
                            set.FaultMessage += set.MakerOpeningOrder.Message;
                        }
                        return false;
                    }
                }
            }
            set.RunRecord.Order1 = set.MakerOpeningOrder.OrderID;
            return true;
        }

        private async Task<bool> PlaceLeftClosingMakerOrder(MakerTakerSet set)
        {
            PricingScheme ps;
            IApi leftMakerApi = null, rightMakerApi = null;
            string leftMakerInstrument = string.Empty, rightMakerInstrument = string.Empty;
            Api.Side leftMakerSide;
            Api.OrderType leftMakerOrderType;
            decimal leftMakerSize = 0.0M;
            decimal hurdleRate = 0.0M;

            rightMakerInstrument = set.RunRecord.Text4;
            rightMakerApi = set.Taker;
            leftMakerApi = set.Maker;
            leftMakerInstrument = set.RunRecord.Text2;
            leftMakerSide = Api.Tools.ToSide(set.RunRecord.Text5);
            //closing side is the opposite
            if (leftMakerSide == Api.Side.Buy)
            {
                leftMakerSide = Api.Side.Sell;
                ps = PricingScheme.PercentageOfNewPrice;
            }
            else
            {
                leftMakerSide = Api.Side.Buy;
                ps = PricingScheme.PercentageOfTarget;
            }
            leftMakerOrderType = Api.OrderType.Limit;
            leftMakerSize = set.RunRecord.Num7.HasValue ? set.RunRecord.Num7.Value : set.RunRecord.Num1.GetValueOrDefault();
            leftMakerSize = leftMakerSize - set.RunRecord.Num11.GetValueOrDefault(); //subtract any partial executed qty;
            hurdleRate = set.RunRecord.Num3.GetValueOrDefault();   //closing hurdle rate

            //Make case for Poloniex, if closing maker buy, adjust qty to compensate at least for maker fee
            if (leftMakerSide == Api.Side.Buy && leftMakerApi.ToString() == "Poloniex")
            {
                if (set.MakerMakerFee < 1.0M)
                    leftMakerSize = leftMakerSize / (1.0M - set.MakerMakerFee);
            }
            //Make case end         

            ushort retry = 0;
            while ((set.MakerClosingOrderLeft == null || set.MakerClosingOrderLeft.IsCancelled || set.MakerClosingOrderLeft.OrderStatus == Api.OrderStatus.None) && MAX_MAKER_RETRY >= retry)
            {
                set.MakerClosingOrderLeft = await FloatingPriceOrder5.Create(leftMakerApi,
                leftMakerInstrument,
                leftMakerSide,
                leftMakerOrderType,
                leftMakerSize,
                0.0M,
                rightMakerInstrument,
                rightMakerApi,
                set.OriginalClosingHurdleRate,
                hurdleRate,
                set.HurdleBoostEffectiveUntil,
                0.0M,
                0.0M,
                ps,
                set.RunRecord.OpId);

                if (set.MakerClosingOrderLeft == null || set.MakerClosingOrderLeft.IsCancelled || set.MakerClosingOrderLeft.OrderStatus == Api.OrderStatus.None)
                {
                    retry++;
                    if (MAX_MAKER_RETRY >= retry)
                    {
                        if (set.MakerClosingOrderLeft != null && set.MakerClosingOrderLeft.IsCancelled && set.MakerClosingOrderLeft.CancelReason == Api.CancelReason.OrderIsPostOnly)
                        { //if its cancelled due to post_only then boost the hurdle rate
                            set.BoostClosingHurdleRate(-HURDLE_BOOST_FACTOR, HURDLE_BOOST_PERIOD_MINUTE);
                        }
                        else
                        { //wait for retry
                            await Task.Delay(retry * MAX_MAKER_RETRY_INTERVAL_SECONDS * 1000);
                        }
                    }
                    else
                    {   //retry has exhausted
                        set.FaultMessage = "Failed to place left closing maker order. ";
                        if (set.MakerClosingOrderLeft != null)
                        {
                            if (set.MakerClosingOrderLeft.IsCancelled)
                                set.FaultMessage += "Order is canceled by exchange. ";
                            set.FaultMessage += set.MakerClosingOrderLeft.Message;
                        }                        
                        return false;
                    }
                }
            }
            set.RunRecord.Order3 = set.MakerClosingOrderLeft.OrderID;
            return true;
        }

        private async Task<bool> PlaceRightClosingMakerOrder(MakerTakerSet set)
        {           
            PricingScheme ps;
            IApi leftMakerApi = null, rightMakerApi = null;
            string leftMakerInstrument = string.Empty, rightMakerInstrument = string.Empty;
            Api.Side rightMakerSide;
            Api.OrderType rightMakerOrderType;
            decimal rightMakerSize;
            decimal hurdleRate = 0.0M;

            leftMakerInstrument = set.RunRecord.Text2;
            leftMakerApi = set.Maker;            
            rightMakerApi = set.Taker;
            rightMakerInstrument = set.RunRecord.Text4;
            rightMakerSide = Api.Tools.ToSide(set.RunRecord.Text5);

            if (rightMakerSide == Api.Side.Sell)
            {
                ps = PricingScheme.PercentageOfNewPrice;
            }
            else
            {
                ps = PricingScheme.PercentageOfTarget;
            }
            rightMakerOrderType = Api.OrderType.Limit;
            rightMakerSize = set.RunRecord.Num8.HasValue ? set.RunRecord.Num8.Value : set.RunRecord.Num1.GetValueOrDefault();
            rightMakerSize = rightMakerSize - set.RunRecord.Num12.GetValueOrDefault();
            hurdleRate = set.RunRecord.Num3.GetValueOrDefault();   //closing hurdle rate

            //Make case for Poloniex. Adjust the qty to compensate for at least the maker fee.
            if (rightMakerSide == Api.Side.Buy && rightMakerApi.ToString() == "Poloniex")
            {
                if (set.TakerMakerFee < 1.0M)
                    rightMakerSize = rightMakerSize / (1.0M - set.TakerMakerFee);
            }
            //Make case end

            ushort retry = 0;
            while ((set.MakerClosingOrderRight == null || set.MakerClosingOrderRight.IsCancelled || set.MakerClosingOrderRight.OrderStatus == Api.OrderStatus.None) && MAX_MAKER_RETRY >= retry)
            {
                set.MakerClosingOrderRight = await FloatingPriceOrder5.Create(rightMakerApi,
                rightMakerInstrument,
                rightMakerSide,
                rightMakerOrderType,
                rightMakerSize,
                0.0M,
                leftMakerInstrument,
                leftMakerApi,
                set.OriginalClosingHurdleRate,
                hurdleRate,
                set.HurdleBoostEffectiveUntil,
                0.0M,
                0.0M,
                ps,
                set.RunRecord.OpId);

                if (set.MakerClosingOrderRight == null || set.MakerClosingOrderRight.IsCancelled || set.MakerClosingOrderRight.OrderStatus == Api.OrderStatus.None)
                {
                    retry++;
                    if (MAX_MAKER_RETRY >= retry)
                    {
                        if (set.MakerClosingOrderRight != null && set.MakerClosingOrderRight.IsCancelled && set.MakerClosingOrderRight.CancelReason == Api.CancelReason.OrderIsPostOnly)
                        { //if its cancelled due to post_only then boost the hurdle rate
                            set.BoostClosingHurdleRate(-HURDLE_BOOST_FACTOR, HURDLE_BOOST_PERIOD_MINUTE);
                        }
                        else
                        { //wait for retry
                            await Task.Delay(retry * MAX_MAKER_RETRY_INTERVAL_SECONDS * 1000);
                        }
                    }
                    else
                    {   // retry has exhausted
                        set.FaultMessage = "Failed to place left closing maker order. ";
                        if (set.MakerClosingOrderRight != null)
                        {
                            if (set.MakerClosingOrderRight.IsCancelled)
                                set.FaultMessage += "Order is canceled by exchange. ";
                            set.FaultMessage += set.MakerClosingOrderRight.Message;
                        }
                        return false;
                    }
                }
            }
            set.RunRecord.Order5 = set.MakerClosingOrderRight.OrderID;
            return true;
        }

        /// <summary>
        /// Get the existing opening order
        /// </summary>
        /// <param name="set">The working set to populate the opening order onto</param>
        /// <returns>True if successful, false otherwise</returns>
        private async Task<bool> RetrieveOpeningOrder(MakerTakerSet set)
        {
            var makerApi = set.Maker;
            var takerApi = set.Taker;
            var takerInstrument = set.RunRecord.Text4;
            var makerInstrument = set.RunRecord.Text2;
            var takerFee = (await takerApi.GetInstrumentAsync(takerInstrument)).TakerFee.GetValueOrDefault();
            var makerFee = (await makerApi.GetInstrumentAsync(makerInstrument)).MakerFee.GetValueOrDefault();
            var hurdleRate = set.RunRecord.Num2.GetValueOrDefault();
            PricingScheme ps;
            if (set.RunRecord.Text5.ToLower() == "sell")
            {
                ps = PricingScheme.PercentageOfTarget;
            }
            else
            {
                ps = PricingScheme.PercentageOfNewPrice;
            }

            set.MakerOpeningOrder = await FloatingPriceOrder5.Get(makerApi,
                        set.RunRecord.Order1,
                        takerInstrument,
                        takerApi,
                        hurdleRate,
                        takerFee,
                        makerFee,
                        ps,
                        set.RunRecord.OpId);

            return set.MakerOpeningOrder != null;
        }

        private async Task<bool> RetrieveLeftClosingOrders(MakerTakerSet set)
        {
            IApi makerApi = null, takerApi = null;
            string makerInstrument, takerInstrument;
            decimal makerFeeRate = 0.0M, takerFeeRate = 0.0M;
            decimal hurdleRate = 0.0M;
            PricingScheme ps;
            makerApi = set.Maker;
            takerApi = set.Taker;
            makerInstrument = set.RunRecord.Text2;
            takerInstrument = set.RunRecord.Text4;
            hurdleRate = set.RunRecord.Num3.GetValueOrDefault();
            if (set.RunRecord.Text5.ToLower() == "sell")
            {
                ps = PricingScheme.PercentageOfTarget;
            }
            else
            {
                ps = PricingScheme.PercentageOfNewPrice;
            }

            set.MakerClosingOrderLeft = await FloatingPriceOrder5.Get(makerApi,
                         set.RunRecord.Order3,
                         takerInstrument,
                         takerApi,
                         hurdleRate,
                         takerFeeRate,
                         makerFeeRate,
                         ps,
                         set.RunRecord.OpId);

            return set.MakerClosingOrderLeft != null && set.MakerClosingOrderLeft.OrderStatus != Api.OrderStatus.Canceled;
        }

        private async Task<bool> RetrieveRightClosingOrders(MakerTakerSet set)
        {
            IApi makerApi = null, takerApi = null;
            string makerInstrument, takerInstrument;
            decimal makerFeeRate = 0.0M, takerFeeRate = 0.0M;
            decimal hurdleRate = 0.0M;
            PricingScheme ps;
            makerApi = set.Taker;
            takerApi = set.Maker;
            makerInstrument = set.RunRecord.Text4;
            takerInstrument = set.RunRecord.Text2;
            hurdleRate = set.RunRecord.Num3.GetValueOrDefault();
            if (set.RunRecord.Text5.ToLower() == "sell")
            {
                ps = PricingScheme.PercentageOfNewPrice;
            }
            else
            {
                ps = PricingScheme.PercentageOfTarget;
            }
            set.MakerClosingOrderRight = await FloatingPriceOrder5.Get(makerApi,
                         set.RunRecord.Order5,
                         takerInstrument,
                         takerApi,
                         hurdleRate,
                         takerFeeRate,
                         makerFeeRate,
                         ps,
                         set.RunRecord.OpId);

            return set.MakerClosingOrderRight != null && set.MakerClosingOrderRight.OrderStatus != Api.OrderStatus.Canceled;
        }


        /// <summary>
        /// Run fault. Stops any order in the set and mark the run as fault so it cannot restart until user intervention
        /// </summary>
        /// <param name="set"></param>
        /// <param name="faultMessage"></param>
        private void RunFault(MakerTakerSet set, string faultMessage, bool doRerun = true)
        {
            if (set.MakerOpeningOrder != null) set.MakerOpeningOrder.Stop("Maker-Taker set " + set.RunRecord.Id.ToString() + " fault " + faultMessage);
            if (set.MakerClosingOrderLeft != null) set.MakerClosingOrderLeft.Stop("Maker-Taker set " + set.RunRecord.Id.ToString() + " fault " + faultMessage);
            if (set.MakerClosingOrderRight != null) set.MakerClosingOrderRight.Stop("Maker-Taker set " + set.RunRecord.Id.ToString() + " fault " + faultMessage);
            if (set.TimerSettlement != null) set.TimerSettlement.Stop();
            set.StopClosingTakerTaker();            
            set.RunRecord.Message = faultMessage;
            set.RunRecord.Status = (int)RunStatus.Fault;
            using (var db = new Tuna())
            {
                var o = db.Entry<Run>(set.RunRecord);
                o.State = System.Data.Entity.EntityState.Modified;
                DBUtil.SaveDb(db);
            }

            if (doRerun)
                Run(set.OpRecord.Id);
        }

        /// <summary>
        /// Retrieves the working set by order ID
        /// </summary>
        /// <param name="api">The order's owner</param>
        /// <param name="orderId">Order ID</param>
        /// <returns></returns>
        private MakerTakerSet GetWorkingSet(IApi api, string orderId)
        {
            foreach (var set in makerTakerSets)
            {
                if ((set.Value.MakerOpeningOrder != null && (set.Value.MakerOpeningOrder.OwnerApi == api && set.Value.MakerOpeningOrder.OrderID == orderId)) ||
                    (set.Value.MakerClosingOrderLeft != null && (set.Value.MakerClosingOrderLeft.OwnerApi == api && set.Value.MakerClosingOrderLeft.OrderID == orderId)) ||
                    (set.Value.MakerClosingOrderRight != null && (set.Value.MakerClosingOrderRight.OwnerApi == api && set.Value.MakerClosingOrderRight.OrderID == orderId)))
                {
                    return set.Value;
                }
            }
            return null;
        }

        private void Message(string message, int runId)
        {       
                 
            if (runId > 0)
            {
                message = "<MakerTaker><" + runId.ToString() + "> " + message;
            }
            else
            {
                message = "<MakerTaker>" + message;
            }

            OnMessage?.Invoke(this, message);
            log.Debug(message);
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the opening maker order is hit.
        /// </summary>
        /// <param name="sender">The FloatingPriceOrder object.</param>
        /// <param name="e"></param>
        /// 
        private async void MakerOpeningOrder_OnOrderHit(object sender, EventArgs e)
        {
            MakerTakerSet set;
            FloatingPriceOrder5 mtOrder = (FloatingPriceOrder5)sender;
            string msg = string.Empty;
            bool doRerun = false;
            decimal qtyFilled = mtOrder.NetAmount.GetValueOrDefault();
            decimal qtyNettedSinceLast = 0.0M;
            if (qtyFilled > 0)
            {
                set = GetWorkingSet(mtOrder.OwnerApi, mtOrder.OrderID);
                if (set == null)
                    return;

                if(set.MakerOpeningOrder.IsFullyFilled)
                    set.MakerOpeningOrder.ClearEventSubscribers();
                msg = "Opening maker hit: " + mtOrder.ToString(); Message(msg, set.RunRecord.Id); Helper.PlaySfx(Sfx.Alert);

                qtyNettedSinceLast = qtyFilled - set.RunRecord.Num7.GetValueOrDefault();
                set.RunRecord.Num7 = qtyFilled; //set maker executed qty    
                set.RunRecord.Num9 = mtOrder.AveragePrice.GetValueOrDefault() == 0.0M ? mtOrder.Price : mtOrder.AveragePrice.GetValueOrDefault();
                set.RunRecord.Message = msg;
                IResult rst;
                decimal qtyTakerQuote = qtyNettedSinceLast;
                if (mtOrder.Side == Api.Side.Sell) 
                {
                    //It was an opening sell. Place an opposite buy taker order on the right side                    
                    if (mtOrder.TargetAPI.ToString() == "Poloniex")
                    { //Make case for Poloniex, if a buy taker were to be placed. Adjust the taker qty to compensate for at least the maker fee.
                        if (set.TakerMakerFee < 1.0M)
                            qtyTakerQuote = qtyTakerQuote / (1.0M - set.TakerMakerFee);
                    } //Make case end
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Buy, qtyTakerQuote, mtOrder.TargetPrice, set.OpRecord.Num7.GetValueOrDefault(), Api.OrderOptions.None); //right taker
                }
                else 
                {
                    //It was an opening buy; place an opposite sell taker order on the right side
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Sell, qtyTakerQuote, mtOrder.TargetPrice, set.OpRecord.Num7.GetValueOrDefault(), Api.OrderOptions.None); //right taker
                }

                if (rst.Success)
                {
                    set.RunRecord.Order2 = ((IOrder)rst).OrderID;
                    decimal netAmt = ((IOrder)rst).NetAmount.GetValueOrDefault();

                    if (string.IsNullOrEmpty(set.RunRecord.Order2) || ((IOrder)rst).IsCancelled)
                    {
                        Message("set.RunRecord.Order2 is empty", set.RunRecord.Id);
                    }

                    if (netAmt == 0.0M)
                    {//I must have the correct amount here, get it from server if its missing.
                        IOrder[] takerExecuted = await mtOrder.TargetAPI.GetOrdersAsync(set.RunRecord.Order2);
                        if (takerExecuted.Length > 0)
                            netAmt = takerExecuted[0].NetAmount.GetValueOrDefault();
                    }
                    if (netAmt == 0.0M)
                    {
                        msg = "Opening taker placed but failed to fill: " + ((IOrder)rst).ToString();
                        set.RunRecord.Num8 = 0.0M;
                        set.RunRecord.Num10 = 0.0M;
                        Message(msg, set.RunRecord.Id);
                        RunFault(set, msg, false);
                    }
                    else
                    {
                        //calculate the run's opening taker new average price
                        decimal netTotalPrice = set.RunRecord.Num8.GetValueOrDefault() * set.RunRecord.Num10.GetValueOrDefault();
                        netTotalPrice += ((IOrder)rst).AveragePrice.GetValueOrDefault() * netAmt;
                        set.RunRecord.Num8 = set.RunRecord.Num8.GetValueOrDefault() + netAmt;
                        set.RunRecord.Num10 = netTotalPrice / set.RunRecord.Num8.GetValueOrDefault();

                        Message("Opening taker placed: " + ((IOrder)rst).ToString(), set.RunRecord.Id);
                        doRerun = set.MakerOpeningOrder!= null && set.MakerOpeningOrder.IsFullyFilled;
                    }
                    using (var db = new Tuna())
                    {
                        var o = db.Entry(set.RunRecord);
                        o.State = System.Data.Entity.EntityState.Modified;
                        await DBUtil.SaveDbAsync(db);
                    }
                }
                else
                {
                    msg = "Fail to place opening taker  " + mtOrder.TargetAPI.ToString() + " " + mtOrder.TargetSymbol + ". " + rst.Message;
                    Message(msg, set.RunRecord.Id);
                    RunFault(set, msg, false);
                }
                
                if (doRerun) //rerun for closing order placement
                    Rerun(set);
            }
        }

        /// <summary>
        /// Raised when the opening maker order is cancelled extenally; this automatically reruns the op.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MakerOpeningOrder_OnOrderCancelled(object sender, EventArgs e)
        {
            FloatingPriceOrder5 order = (FloatingPriceOrder5)sender;            
            order.Stop("Opening Maker cancelled");
            string msg;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set == null) return;

            msg = "Maker abruptly cancelled: " + order.ToString();

            if (order.CancelReason == Api.CancelReason.OrderIsPostOnly)
            {
                set.BoostOpeningHurdleRate(HURDLE_BOOST_FACTOR, HURDLE_BOOST_PERIOD_MINUTE);
                msg += "\nPost-Only new hurdle: " + set.RunRecord.Num2.GetValueOrDefault().ToString();
            }

            using (var db = new Tuna())
            {
                var r = db.Entry<Run>(set.RunRecord);
                if(!order.IsFilled)
                    r.Entity.Order1 = null;
                r.Entity.Message = msg;
                r.Entity.Status = (int)RunStatus.Stopped;
                r.State = System.Data.Entity.EntityState.Modified;
                DBUtil.SaveDb(db);
            }
            Message(msg, set.RunRecord.Id);
            Rerun(set);
        }

        /*
        private void MakerOpeningOrder_OnOrderCancelled0(object sender, EventArgs e)
        {
            FloatingPriceOrder5 order = (FloatingPriceOrder5)sender;
            order.Stop("Opening Maker cancelled");
            string msg;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set == null) return;

            msg = "Maker cancelled: " + order.ToString();
            using (var db = new Tuna())
            {
                var r = db.Entry<Run>(set.RunRecord);
                r.Entity.Order1 = null;
                r.Entity.Message = msg;
                r.Entity.Status = (int)RunStatus.Stopped;
                r.State = System.Data.Entity.EntityState.Modified;                
                db.SaveChanges();
            }
            Message(msg, set.RunRecord.Id);
            Rerun(set);
        }
        */

        private async void MakerOpeningOrder_OnError(object sender, string e)
        {
            string msg = "Opening maker order error: " + e;
            FloatingPriceOrder5 order = (FloatingPriceOrder5)sender;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            Message(msg, set.RunRecord.Id);
            if (order.ErrorCount > MAX_ERROR_COUNT)
            {
                order.Stop(msg);                
                Message("Order stopped. " + e + " " + order.OrderID,  set != null ? set.RunRecord.Id : 0);
                IResult res = await order.CancelOrder(msg);
                if (res.Success) 
                    Message("Opening order cancelled.", set.RunRecord.Id);
                else
                    Message("Could not cancel opening order. " + res.Message, set.RunRecord.Id);
            }
        }

        /// <summary>
        /// Raised when the closing maker order on the right market is hit. Cancels the left maker order. Executes a market order to close the left market position.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MakerClosingOrderRight_OnOrderHit(object sender, EventArgs e)
        {
            string reason = "Right closing order was hit.";
            MakerTakerSet set;
            FloatingPriceOrder5 mtOrder = (FloatingPriceOrder5)sender;
            decimal fixedTargetPrice = mtOrder.TargetPrice; //fix the target price
            decimal qtyFilled = mtOrder.NetAmount.GetValueOrDefault();
            decimal qtyNettedSinceLast = 0.0M;
            log.Debug("MakerClosingOrderRight_OnOrderHit qtyFilled " + qtyFilled.ToString());            
            string msg = string.Empty;
            if (qtyFilled > 0)
            {
                set = GetWorkingSet(mtOrder.OwnerApi, mtOrder.OrderID);
                if (set == null)
                    return;

                set.StopClosingTakerTaker();

                //stop any maker opening order
                if (set.MakerOpeningOrder != null && set.MakerOpeningOrder.IsOpen)
                {
                    set.MakerOpeningOrder.ClearEventSubscribers();
                    set.MakerOpeningOrder.Stop(reason);
                    set.MakerOpeningOrder.CancelOrder(reason);
                }

                //stop the left order
                set.MakerClosingOrderLeft.ClearEventSubscribers();
                set.MakerClosingOrderLeft.Stop(reason);

                //Initiaate maker order cancel on the left first                
                Task<IResult> cancelTask = null;
                if (set.MakerClosingOrderLeft.IsOpen)
                {
                    Message("Canceling left maker order", set.RunRecord.Id);
                    cancelTask = set.MakerClosingOrderLeft.CancelOrder(reason);
                }

                msg = "Closing maker (right) hit: " + mtOrder.ToString(); Message(msg, set.RunRecord.Id); Helper.PlaySfx(Sfx.Alert);

                //stop the right order
                if (set.MakerClosingOrderRight.IsFullyFilled)
                {
                    msg = "Fully hit: " + mtOrder.ToString(); Message(msg, set.RunRecord.Id); Helper.PlaySfx(Sfx.Alert);
                    set.MakerClosingOrderRight.ClearEventSubscribers();
                }
                
                decimal qtyLast = set.RunRecord.Num12.GetValueOrDefault();
                qtyNettedSinceLast = qtyFilled - set.RunRecord.Num12.GetValueOrDefault();
                set.RunRecord.Num12 = qtyFilled; //set closing maker executed qty      
                set.RunRecord.Num14 = mtOrder.AveragePrice.GetValueOrDefault() == 0.0M ? mtOrder.Price : mtOrder.AveragePrice.GetValueOrDefault();
                set.RunRecord.Message = msg;

                decimal qtyTakerQuote = qtyNettedSinceLast;
                if (set.RunRecord.Num12.GetValueOrDefault() > set.RunRecord.Num7.GetValueOrDefault())
                {
                    qtyTakerQuote = set.RunRecord.Num7.GetValueOrDefault() - qtyLast;
                }

                if (mtOrder.TargetAPI.ToString() == "BitMEX") //TODO: remove this fix
                    qtyTakerQuote = Math.Ceiling(qtyTakerQuote);

                //Wait for cancel, then submit taker order.                       
                //If its bitmex, do not wait.
                IResult rst, rstCancel;
                if (mtOrder.TargetAPI.ToString() != "BitMEX")
                {
                    if (cancelTask != null)
                    {
                        await cancelTask;                        
                    }
                }

                //Continue regardless of cancel status                
                if (mtOrder.Side == Api.Side.Sell) //was an closing sell
                {
                    //make case for Polo
                    if (mtOrder.TargetAPI.ToString() == "Poloniex")
                    {
                        if (set.MakerMakerFee < 1.0M)
                            qtyTakerQuote = qtyTakerQuote / (1 - set.MakerMakerFee);
                    }
                    //make case end
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Buy, qtyTakerQuote, mtOrder.TargetPrice < fixedTargetPrice ? mtOrder.TargetPrice : fixedTargetPrice, set.OpRecord.Num7.GetValueOrDefault(), Api.OrderOptions.None); //taker to the left for the right
                }
                else //was an closing buy
                {
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Sell, qtyTakerQuote, mtOrder.TargetPrice > fixedTargetPrice ? mtOrder.TargetPrice : fixedTargetPrice, set.OpRecord.Num7.GetValueOrDefault(), Api.OrderOptions.None); //taker to the left for the right
                }
                Message("Fixed target price was " + fixedTargetPrice.ToString() + "/FPX target was " + mtOrder.TargetPrice.ToString(), set.RunRecord.Id);

                if (cancelTask != null)
                {
                    rstCancel = await cancelTask;
                    if (!rstCancel.Success)
                    {
                        Message("Left closing maker order cancel failed. " + set.MakerClosingOrderLeft.OrderID + " " + rstCancel.Message, set.RunRecord.Id);
                        set.RunRecord.Message += rstCancel.Message;
                    }
                    else
                    {
                        Message("Left closing maker order was cancelled. " + rst.Message, set.RunRecord.Id);
                    }
                }

                if (rst.Success)
                {                    
                    set.RunRecord.Order4 = ((IOrder)rst).OrderID;
                    if (string.IsNullOrEmpty(set.RunRecord.Order4) || ((IOrder)rst).IsCancelled)
                    {
                        Message("set.RunRecord.Order4 is empty", set.RunRecord.Id);
                    }

                    decimal netAmt = ((IOrder)rst).NetAmount.GetValueOrDefault();
                    if (netAmt == 0.0M)
                    {
                        IOrder[] takerExecuted = await mtOrder.TargetAPI.GetOrdersAsync(set.RunRecord.Order4);
                        if (takerExecuted.Length > 0)
                            netAmt = takerExecuted[0].NetAmount.GetValueOrDefault();
                    }
                    if (netAmt == 0.0M)
                    {
                        msg = "Closing taker placed but failed to fill: " + ((IOrder)rst).ToString();
                        set.RunRecord.Num11 = 0.0M;
                        set.RunRecord.Num13 = 0.0M;
                        Message(msg, set.RunRecord.Id);
                        RunFault(set, msg, false);
                    }
                    else
                    {
                        //calculate the run's closing taker new average price
                        decimal netTotalPrice = set.RunRecord.Num11.GetValueOrDefault() * set.RunRecord.Num13.GetValueOrDefault();
                        netTotalPrice += ((IOrder)rst).AveragePrice.GetValueOrDefault() * netAmt;
                        set.RunRecord.Num11 = set.RunRecord.Num11.GetValueOrDefault() + netAmt;
                        set.RunRecord.Num13 = netTotalPrice / set.RunRecord.Num11.GetValueOrDefault();

                        msg = "Closing taker placed: " + set.RunRecord.Order4 + " Net:" + set.RunRecord.Num11.ToString() + "@" + set.RunRecord.Num13.ToString(); Message(msg, set.RunRecord.Id);
                    }
                    
                    set.RunRecord.Message += msg;

                    if (set.RunRecord.Num11.GetValueOrDefault() >= set.RunRecord.Num7.GetValueOrDefault()) //position is fully closed
                    {
                        AfterPositionClosed(set);
                    }
                    else
                    {
                        using (var db = new Tuna())
                        {
                            var o = db.Entry<Run>(set.RunRecord);
                            o.State = System.Data.Entity.EntityState.Modified;
                            await DBUtil.SaveDbAsync(db);
                        }
                    }
                }
                else
                {
                    msg = "Fail to place Closing taker " + mtOrder.TargetAPI.ToString() + " " + mtOrder.TargetSymbol + ". " + rst.Message; Message(msg, set.RunRecord.Id);
                    Message(msg, set.RunRecord.Id);
                    RunFault(set, msg, false);
                }
            }
        }

        private async void MakerClosingOrderRight_OnOrderCancelled(object sender, EventArgs e)
        {
            FloatingPriceOrder5 order = (FloatingPriceOrder5)sender;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
                        
            if (set == null || !order.isStarted)
                return; //if the set is gone or the order is stopped, do nothing

            order.Stop("Right closing maker order cancelled");
            string msg = "Right Closing Maker was cancelled: " + order.ToString(); Message(msg, set.RunRecord.Id);
            if (order.CancelReason == Api.CancelReason.OrderIsPostOnly)
            {
                set.BoostClosingHurdleRate(-HURDLE_BOOST_FACTOR, HURDLE_BOOST_PERIOD_MINUTE);
                msg += "\nPost-Only new hurdle: " + set.MakerClosingOrderRight.HurdleRate.ToString(); Message(msg, set.RunRecord.Id);
            }

            bool success = await PlaceRightClosingMakerOrder(set);
            using (var db = new Tuna())
            {
                var r = db.Entry(set.RunRecord);
                if (success)
                {
                    set.MakerClosingOrderRight.OnOrderHit += MakerClosingOrderRight_OnOrderHit;
                    set.MakerClosingOrderRight.OnError += MakerClosingOrderRight_OnError;
                    set.MakerClosingOrderRight.OnOrderCancelled += MakerClosingOrderRight_OnOrderCancelled;
                    set.MakerClosingOrderRight.OnOrderAmended += MakerOrder_OnOrderAmended;
                    set.MakerClosingOrderRight.Start();

                    msg = "Right Closing Maker placed: " + set.MakerClosingOrderRight.ToString(); Message(msg, set.RunRecord.Id);
                    r.Entity.Order5 = set.MakerClosingOrderRight.OrderID;
                }
                else
                {
                    //If reposting failed then also cancel the left maker                    
                    if (set.MakerClosingOrderLeft != null)
                    {
                        IResult res = await set.MakerClosingOrderLeft.CancelOrder("Right closing order was cancelled.");
                        if (res.Success)
                            set.RunRecord.Order3 = null;
                        else
                            msg = "\nLeft maker failed to cancel. " + set.MakerClosingOrderLeft.ToString(); Message(msg, set.RunRecord.Id);
                    }
                    r.Entity.Order5 = null;
                    r.Entity.Status = (int)RunStatus.Stopped;
                }
                r.State = System.Data.Entity.EntityState.Modified;
                r.Entity.Message = msg;
                await DBUtil.SaveDbAsync(db);
            }
        }

        private void MakerClosingOrderRight_OnError(object sender, string e)
        {
            string msg = "Right Maker Closing order error: " + e;
            FloatingPriceOrder5 order = (FloatingPriceOrder5)sender;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            order.ClearEventSubscribers();
            order.Stop(msg);
            order.CancelOrder(msg);
            if (set != null)
            {
                Message(msg, set.RunRecord.Id);
                set.MakerClosingOrderLeft.ClearEventSubscribers();
                set.MakerClosingOrderLeft.Stop(msg);
                set.MakerClosingOrderLeft.CancelOrder(msg);
                RunFault(set, e, false);
            }
            else
            {
                throw new NotImplementedException(e);
            }
        }

        /// <summary>
        /// Raised when closing maker order on the maker side is hit. The entire position should be closed afterwards.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MakerClosingOrderLeft_OnOrderHit(object sender, EventArgs e)
        {            
            string reason = "Left closing maker order hit.";
            MakerTakerSet set;
            FloatingPriceOrder5 mtOrder = (FloatingPriceOrder5)sender;
            decimal fixedTargetPrice = mtOrder.TargetPrice; //fix the target price
            decimal qtyFilled = mtOrder.NetAmount.GetValueOrDefault();
            decimal qtyNettedSinceLast = 0.0M;
            log.Debug("MakerClosingOrderLeft_OnOrderHit qtyFilled " + qtyFilled.ToString());
            string msg = string.Empty;
            if (qtyFilled > 0)
            {
                set = GetWorkingSet(mtOrder.OwnerApi, mtOrder.OrderID);
                if (set == null)
                    return;

                set.StopClosingTakerTaker();

                //stop any maker opening order
                if (set.MakerOpeningOrder != null && set.MakerOpeningOrder.IsOpen)
                {
                    set.MakerOpeningOrder.ClearEventSubscribers();
                    set.MakerOpeningOrder.Stop(reason);
                    set.MakerOpeningOrder.CancelOrder(reason);
                }

                //stop the right order
                set.MakerClosingOrderRight.ClearEventSubscribers();
                set.MakerClosingOrderRight.Stop(reason);

                //Initiate maker order Cancel the on the right first                
                Task<IResult> cancelTask = null;
                if (set.MakerClosingOrderRight.IsOpen)
                {
                    Message("Canceling right maker order", set.RunRecord.Id);
                    cancelTask = set.MakerClosingOrderRight.CancelOrder(reason); //submit and go.                
                }

                msg = "Closing maker (left) hit: " + mtOrder.ToString(); Message(msg, set.RunRecord.Id); Helper.PlaySfx(Sfx.Alert);

                //stop the left order
                if (set.MakerClosingOrderLeft.IsFullyFilled)
                {
                    msg = "Fully hit: " + mtOrder.ToString(); Message(msg, set.RunRecord.Id); Helper.PlaySfx(Sfx.Alert);
                    set.MakerClosingOrderLeft.ClearEventSubscribers();
                }

                decimal qtyLast = set.RunRecord.Num11.GetValueOrDefault();
                qtyNettedSinceLast = qtyFilled - set.RunRecord.Num11.GetValueOrDefault();
                set.RunRecord.Num11 = qtyFilled; //set closing maker executed qty    
                set.RunRecord.Num13 = mtOrder.AveragePrice.GetValueOrDefault() == 0.0M ? mtOrder.Price : mtOrder.AveragePrice.GetValueOrDefault();
                set.RunRecord.Message = msg;
                
                decimal qtyTakerQuote = qtyNettedSinceLast;
                if (set.RunRecord.Num11.GetValueOrDefault() > set.RunRecord.Num8.GetValueOrDefault())
                {
                    qtyTakerQuote = set.RunRecord.Num8.GetValueOrDefault() - qtyLast;
                }

                if (mtOrder.TargetAPI.ToString() == "BitMEX") //TODO: remove this fix
                    qtyTakerQuote = Math.Ceiling(qtyTakerQuote);

                //Wait for cancel, then submit taker order.        
                IResult rst, rstCancel;
                if (mtOrder.TargetAPI.ToString() != "BitMEX")
                {
                    if (cancelTask != null)
                    {
                        await cancelTask;
                    }
                }
               
                
                //Continue regardless of cancel status                
                if (mtOrder.Side == Api.Side.Sell) //was an closing sell
                {
                    //make case for Polo
                    if (mtOrder.TargetAPI.ToString() == "Poloniex")
                    {
                        if (set.TakerMakerFee < 1.0M)
                            qtyTakerQuote = qtyTakerQuote / (1 - set.TakerMakerFee);
                    }
                    //make case end
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Buy, qtyTakerQuote, mtOrder.TargetPrice < fixedTargetPrice ? mtOrder.TargetPrice : fixedTargetPrice, set.OpRecord.Num7.GetValueOrDefault(), Api.OrderOptions.None); //taker right
                }
                else //was an closing buy
                {
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Sell, qtyTakerQuote, mtOrder.TargetPrice > fixedTargetPrice ? mtOrder.TargetPrice : fixedTargetPrice, set.OpRecord.Num7.GetValueOrDefault(), Api.OrderOptions.None); //taker right
                }
                Message("Fixed target price was " + fixedTargetPrice.ToString() + "/FPX target was " + mtOrder.TargetPrice.ToString(), set.RunRecord.Id);

                if (cancelTask != null)
                {
                    rstCancel = await cancelTask;
                    if (!rstCancel.Success)
                    {
                        Message("Right closing maker order cancel failed. " + set.MakerClosingOrderRight.OrderID + " " + rst.Message, set.RunRecord.Id);
                        set.RunRecord.Message += rst.Message;
                    }
                    else
                    {
                        Message("Right closing maker order cancelled. " + set.MakerClosingOrderRight.OrderID, set.RunRecord.Id);
                    }
                }
          
                if (rst.Success)
                {
                    set.RunRecord.Order6 = ((IOrder)rst).OrderID;
                    decimal netAmt = ((IOrder)rst).NetAmount.GetValueOrDefault();

                    if (string.IsNullOrEmpty(set.RunRecord.Order6) || ((IOrder)rst).IsCancelled)
                    {
                        Message("set.RunRecord.Order6 is empty", set.RunRecord.Id);
                    }

                    if (netAmt == 0.0M)
                    {
                        IOrder[] takerExecuted = await mtOrder.TargetAPI.GetOrdersAsync(set.RunRecord.Order6);
                        if (takerExecuted.Length > 0)
                            netAmt = takerExecuted[0].NetAmount.GetValueOrDefault();
                    }
                    if (netAmt == 0.0M)
                    {
                        msg = "Closing taker placed but failed to fill: " + ((IOrder)rst).ToString();
                        set.RunRecord.Num12 = 0.0M;
                        set.RunRecord.Num14 = 0.0M;
                        Message(msg, set.RunRecord.Id);
                        RunFault(set, msg, false);
                    }
                    else
                    {
                        //calculate the run's closing taker new average price
                        decimal netTotalPrice = set.RunRecord.Num12.GetValueOrDefault() * set.RunRecord.Num14.GetValueOrDefault();
                        netTotalPrice += ((IOrder)rst).AveragePrice.GetValueOrDefault() * netAmt;
                        set.RunRecord.Num12 = set.RunRecord.Num12.GetValueOrDefault() + netAmt;
                        set.RunRecord.Num14 = netTotalPrice / set.RunRecord.Num12.GetValueOrDefault();

                        msg = "Closing taker placed: " + set.RunRecord.Order6 + " Net:" + set.RunRecord.Num12.ToString() + "@" + set.RunRecord.Num14.ToString(); Message(msg, set.RunRecord.Id);
                    }

                    set.RunRecord.Message += msg;

                    if (set.RunRecord.Num12.GetValueOrDefault() >= set.RunRecord.Num8.GetValueOrDefault()) //position is fully closed
                    {
                        AfterPositionClosed(set);
                    }
                    else
                    {
                        using (var db = new Tuna())
                        {
                            var o = db.Entry<Run>(set.RunRecord);
                            o.State = System.Data.Entity.EntityState.Modified;
                            await DBUtil.SaveDbAsync(db);
                        }
                    }
                }
                else
                {
                    msg = "Fail to place Closing taker " + mtOrder.TargetAPI.ToString() + " " + mtOrder.TargetSymbol + ". " + rst.Message;
                    Message(msg, set.RunRecord.Id);
                    RunFault(set, msg, false);
                }
            }
        }

        private async void MakerClosingOrderLeft_OnOrderCancelled(object sender, EventArgs e)
        {
            FloatingPriceOrder5 order = (FloatingPriceOrder5)sender;            
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);

            if (set == null || !order.isStarted)
                return; //if the set is gone or the order is stopped, do nothing

            order.Stop("Right closing maker order cancelled");
            string msg = "Left closing Maker was cancelled: " + order.ToString(); Message(msg, set.RunRecord.Id);
            if (order.CancelReason == Api.CancelReason.OrderIsPostOnly)
            {
                set.BoostClosingHurdleRate(-HURDLE_BOOST_FACTOR, HURDLE_BOOST_PERIOD_MINUTE);
                msg = "\nPost-Only new hurdle: " + set.MakerClosingOrderLeft.HurdleRate.ToString(); Message(msg, set.RunRecord.Id);
            }

            bool success = await PlaceLeftClosingMakerOrder(set);
            using (var db = new Tuna())
            {
                var r = db.Entry(set.RunRecord);
                if (success)
                {
                    set.MakerClosingOrderLeft.OnOrderHit += MakerClosingOrderLeft_OnOrderHit;
                    set.MakerClosingOrderLeft.OnError += MakerClosingOrderLeft_OnError;
                    set.MakerClosingOrderLeft.OnOrderCancelled += MakerClosingOrderLeft_OnOrderCancelled;
                    set.MakerClosingOrderLeft.OnOrderAmended += MakerOrder_OnOrderAmended;
                    set.MakerClosingOrderLeft.Start();

                    msg = "Left closing Maker placed: " + set.MakerClosingOrderLeft.ToString(); Message(msg, set.RunRecord.Id);
                    r.Entity.Order3 = set.MakerClosingOrderLeft.OrderID;
                }
                else
                {
                    //If reposting failed then also cancel the right maker                    
                    if (set.MakerClosingOrderRight != null)
                    {                        
                        IResult rst = await set.MakerClosingOrderRight.CancelOrder("Left closing order was cancelled.");
                        if (rst.Success)
                            set.RunRecord.Order5 = null;
                        else
                            msg = "Right maker failed to cancel. " + set.MakerClosingOrderRight.ToString(); Message(msg, set.RunRecord.Id);
                    }
                    r.Entity.Order3 = null;
                    r.Entity.Status = (int)RunStatus.Stopped;
                }
                r.State = System.Data.Entity.EntityState.Modified;
                r.Entity.Message = msg;
                await DBUtil.SaveDbAsync(db);
            }
        }

        private void MakerClosingOrderLeft_OnError(object sender, string e)
        {
            string msg = "Left Maker Closing order error: " + e;
            FloatingPriceOrder5 order = (FloatingPriceOrder5)sender;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            order.ClearEventSubscribers();
            order.Stop(msg);
            order.CancelOrder(msg);
            if (set != null)
            {
                Message(msg, set.RunRecord.Id);
                set.MakerClosingOrderRight.ClearEventSubscribers();
                set.MakerClosingOrderRight.Stop(msg);
                set.MakerClosingOrderRight.CancelOrder(msg);
                RunFault(set, e, false);
            }
            else
            {
                throw new NotImplementedException(e);
            }
        }

        private void MakerOrder_OnOrderAmended(object sender, EventArgs e)
        {
            FloatingPriceOrder5 fpx = (FloatingPriceOrder5)sender;
            if (!string.IsNullOrEmpty(fpx.OldOrderId) && fpx.OldOrderId != fpx.OrderID)
            {

                MakerTakerSet set = GetWorkingSet(fpx.OwnerApi, fpx.OrderID);
                if (set == null)
                    set = GetWorkingSet(fpx.OwnerApi, fpx.OldOrderId);

                if (set != null)
                {
                    if (set.MakerOpeningOrder != null && (set.MakerOpeningOrder.OrderID == fpx.OrderID || set.MakerOpeningOrder.OrderID == fpx.OldOrderId))
                    {
                        set.RunRecord.Order1 = fpx.OrderID;
                    }
                    else if (set.MakerClosingOrderLeft != null && (set.MakerClosingOrderLeft.OrderID == fpx.OrderID || set.MakerClosingOrderLeft.OrderID == fpx.OldOrderId))
                    {
                        set.RunRecord.Order3 = fpx.OrderID;
                    }
                    else if (set.MakerClosingOrderRight != null && (set.MakerClosingOrderRight.OrderID == fpx.OrderID || set.MakerClosingOrderRight.OrderID == fpx.OldOrderId))
                    {
                        set.RunRecord.Order5 = fpx.OrderID;
                    }
                    else
                    {
                        return;
                    }
                    using (var db = new Tuna())
                    {
                        var run = db.Entry(set.RunRecord);
                        run.State = System.Data.Entity.EntityState.Modified;
                        DBUtil.SaveDb(db);
                    }
                }
            }
        }

        private async void SettlementTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Common.Timer timer = (Common.Timer)sender;
            timer.Stop();
            MakerTakerSet set = (MakerTakerSet)timer.Tag;

            //check if its settlement time
            int Minutes_Before_Settlement = set.RunRecord.Num5.HasValue ? (int)set.RunRecord.Num5.GetValueOrDefault() : 2;
            if (DateTime.UtcNow >= set.RunRecord.Date1.Value.AddMinutes(-Minutes_Before_Settlement - 1)) //settlement time hit
            {
                set.StopClosingTakerTaker();
                //if (set.MakerOpeningOrder != null)
                //{
                //    set.MakerOpeningOrder.Stop("Settlement");                    
                //}

                IResult res = await ClosePosition(set, "Settlement");
                if (res.Success)
                {
                    set.RunRecord.Message = res.Message;
                    AfterPositionClosed(set);
                }
                else
                {
                    set.RunRecord.Message = res.Message;
                    set.RunRecord.Status = (int)RunStatus.Fault;
                    using (var db = new Tuna())
                    {
                        var o = db.Entry(set.RunRecord);
                        o.State = System.Data.Entity.EntityState.Modified;
                        await DBUtil.SaveDbAsync(db);
                        //await db.SaveChangesAsync();
                    }
                    Message(res.Message, set.RunRecord.Id);
                }

                set.TimerReset = new Common.Timer(180000);
                set.TimerReset.AutoReset = false;
                set.TimerReset.Tag = set.OpRecord.Id;
                set.TimerReset.Elapsed += TimerReset_Elapsed;
                set.TimerReset.Start();
            }
            else //not yet, set the timer again.
            {
                double interval = (set.RunRecord.Date1.Value.AddMinutes(-Minutes_Before_Settlement) - DateTime.UtcNow).TotalMilliseconds;
                timer.Interval = interval > Int32.MaxValue ? Int32.MaxValue : interval;
                timer.Start();
            }
        }

        private void TimerReset_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Common.Timer tmr = (Common.Timer)sender;
            tmr.Stop();
            int opid = (int)tmr.Tag;
            if (opid >= 0)
            {
                Run(opid);
            }
        }

        private void Exchange_OnConnectionClosed(object sender, EventArgs e)
        {
            Message("Paused due to disconnection.", 0);
        }

        #endregion  

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    OnMessage = null;
                    foreach (MakerTakerSet set in makerTakerSets.Values)
                    {
                        if (set.MakerOpeningOrder != null)
                            set.MakerOpeningOrder.Stop("disposing");
                        if (set.MakerClosingOrderLeft != null)
                            set.MakerClosingOrderLeft.Stop("disposing");
                        if (set.MakerClosingOrderRight != null)
                            set.MakerClosingOrderRight.Stop("disposing");
                    }
                    makerTakerSets.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~MakerTaker2() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        internal class MakerTakerSet : IDisposable
        {
            public FloatingPriceOrder5 MakerOpeningOrder;
            public FloatingPriceOrder5 MakerClosingOrderLeft;
            public FloatingPriceOrder5 MakerClosingOrderRight;
            public OP OpRecord;
            public Run RunRecord;
            public IApi Maker;
            public IApi Taker;
            public Common.Timer TimerSettlement;    //time until settlement
            public Common.Timer TimerReset;         //timeer for next run after settlement. Set after closing
            public bool IsPositionOpened;
            public bool IsPositionClosed;
            public string FaultMessage;
            public decimal MakerMakerFee; //maker fee for the maker side
            public decimal MakerTakerFee; //maker fee for the taker side
            public decimal TakerMakerFee; //taker fee for the maker side
            public decimal TakerTakerFee; //taker fee for the taker side
            public decimal OriginalOpeningHurdleRate;
            public decimal OriginalClosingHurdleRate;
            public DateTime HurdleBoostEffectiveUntil = DateTime.MaxValue;
            //private Object syncLock = new Object();
            private SemaphoreSlim smfTakerTakerCloser = new SemaphoreSlim(1);

            public event EventHandler<string> OnMessage;
            public event EventHandler OnPositionClosed;


            private SemaphoreSlim subscribe = new SemaphoreSlim(1);
            private bool takertakerSubscribed = false;

            private readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            private void Message(string message)
            {
                OnMessage?.Invoke(this, "Set " + RunRecord.Id.ToString() + " " + message);
            }

            public async void StartClosingTakerTaker()
            {
                await subscribe.WaitAsync();
                try
                {
                    if (!takertakerSubscribed)
                    {
                        Maker.OnOrderBookChanged += Exchange_OnOrderBookChanged;
                        Taker.OnOrderBookChanged += Exchange_OnOrderBookChanged;
                        takertakerSubscribed = true;
                    }
                }
                finally
                {
                    subscribe.Release();
                }
            }
            public async void StopClosingTakerTaker()
            {
                await subscribe.WaitAsync();
                try
                {
                    Maker.OnOrderBookChanged -= Exchange_OnOrderBookChanged;
                    Taker.OnOrderBookChanged -= Exchange_OnOrderBookChanged;
                    takertakerSubscribed = false;
                }
                finally
                {
                    subscribe.Release();
                }
            }

            private void Exchange_OnOrderBookChanged(object sender, Api.OrderBookChangedEventArgs e)
            {
                TakerTakerCloseSignal(e.OrderBook);
            }
            public void BoostOpeningHurdleRate(double factor, int durationMinutes)
            {
                if (OriginalOpeningHurdleRate == 0.0M)
                    OriginalOpeningHurdleRate = RunRecord.Num2.GetValueOrDefault();

                RunRecord.Num2 = RunRecord.Num2.GetValueOrDefault() + (RunRecord.Num2.GetValueOrDefault() * new decimal(factor));
                HurdleBoostEffectiveUntil = DateTime.Now.AddMinutes(durationMinutes);
            }

            internal void BoostClosingHurdleRate(double factor, ushort durationMinutes)
            {
                if (OriginalClosingHurdleRate == 0.0M)
                    OriginalClosingHurdleRate = RunRecord.Num3.GetValueOrDefault();

                RunRecord.Num3 = RunRecord.Num3.GetValueOrDefault() + (RunRecord.Num3.GetValueOrDefault() * new decimal(factor));
                HurdleBoostEffectiveUntil = DateTime.Now.AddMinutes(durationMinutes);
            }

            public void ResetHurdleRates()
            {
                RunRecord.Num2 = OriginalOpeningHurdleRate;
                RunRecord.Num3 = OriginalClosingHurdleRate;
                HurdleBoostEffectiveUntil = DateTime.MaxValue;
            }


            /// <summary>
            /// Signal for Taker-Taker close
            /// </summary>
            /// <param name="orderBook">The orderbook that was changed</param>
            private async void TakerTakerCloseSignal(IOrderBook orderBook)
            {
                if (!IsPositionOpened || IsPositionClosed)
                {
                    return;
                }
                if ((orderBook.Symbol == RunRecord.Text2 && orderBook == Maker.GetOrderBook(orderBook.Symbol)) ||
                    (orderBook.Symbol == RunRecord.Text4 && orderBook == Taker.GetOrderBook(orderBook.Symbol)))
                {
                    if (await Process())
                    {
                        StopClosingTakerTaker();
                        using (var db = new Tuna())
                        {
                            var r = db.Entry(RunRecord);
                            r.State = System.Data.Entity.EntityState.Modified;
                            DBUtil.SaveDb(db);
                        }
                        if (IsPositionClosed)
                            OnPositionClosed?.Invoke(this, new EventArgs());
                    }
                }
            }


            /// <summary>
            /// Executes taker order
            /// </summary>
            /// <param name="api">Exchange API to execute the order against</param>
            /// <param name="symbol">Instrument symbol</param>
            /// <param name="side">Order side; Buy or Sell</param>
            /// <param name="quoteAmount">Taker order qty</param>
            /// <param name="quotePrice">Taker order price</param>
            /// <returns></returns>
            internal Task<IResult> ExecuteTakerOrder(IApi api, string symbol, Api.Side side, decimal quoteAmount, decimal quotePrice, decimal priceBracket, Api.OrderOptions options)
            {
                if (side == Api.Side.Buy)
                {
                    if (USE_PRICE_BRACKET && priceBracket > 0.0M)
                    {
                        quotePrice = quotePrice + (quotePrice * priceBracket);
                        log.Debug("Using bracket price: " + quotePrice.ToStringNormalized());
                    }
                    return OrderManager.Instance.PostBuyOrderAsync(api, symbol, Api.OrderType.Limit, quoteAmount, quotePrice, options);
                }
                else if (side == Api.Side.Sell)
                {
                    if (USE_PRICE_BRACKET && priceBracket > 0.0M)
                    {
                        quotePrice = quotePrice - (quotePrice * priceBracket);
                        log.Debug("Using bracket price: " + quotePrice.ToStringNormalized());
                    }
                    return OrderManager.Instance.PostSellOrderAsync(api, symbol, Api.OrderType.Limit, quoteAmount, quotePrice, options);
                }
                else
                    throw new ArgumentOutOfRangeException("side", side.ToString(), "Side must be buy or sell.");
            }

            public async Task<bool> Process()
            {
                IOrderBook obBid, obAsk;
                IApi takerLong, takerShort;
                bool processed = false;
                bool lockAcquired = await smfTakerTakerCloser.WaitAsync(0);

                if (lockAcquired)
                {
                    try
                    {
                        processed = false;
                        string takerLongSymbol, takerShortSymbol;
                        decimal takerLongFeeRate, takerShortFeeRate;
                        decimal bidPriceTotal, askPriceTotal, topBidPrice, topAskPrice, shortTakerFee, longTakerFee, marginApparent = 0.0M, avgPrice = 0.0M;
                        decimal takerLongMakerFee, takerShortMakerFee, takerLongQtyToQuote, takerShortQtyToQuote;

                        #region Set Short Long
                        if (IsPositionOpened && !IsPositionClosed)
                        {
                            //closing maker settings
                            if (RunRecord.Text5.ToLower() == "sell")
                            { //Opening was sell on the left, for closing, buy on the left, sell on the right, 
                                takerLong = Maker;
                                takerLongSymbol = RunRecord.Text2;
                                takerLongFeeRate = MakerTakerFee;
                                takerLongMakerFee = MakerMakerFee;
                                takerLongQtyToQuote = RunRecord.Num8.GetValueOrDefault();

                                takerShort = Taker;
                                takerShortSymbol = RunRecord.Text4;
                                takerShortFeeRate = TakerTakerFee;
                                takerShortMakerFee = TakerMakerFee;
                                takerShortQtyToQuote = RunRecord.Num8.GetValueOrDefault();
                            }
                            else
                            {//Opening was buy on the left, for closing, sell on the left, buy on the right, 
                                takerShort = Maker;
                                takerShortSymbol = RunRecord.Text2;
                                takerShortFeeRate = MakerTakerFee;
                                takerShortMakerFee = MakerMakerFee;
                                takerShortQtyToQuote = RunRecord.Num8.GetValueOrDefault();

                                takerLong = Taker;
                                takerLongSymbol = RunRecord.Text4;
                                takerLongFeeRate = TakerTakerFee;
                                takerLongMakerFee = TakerMakerFee;
                                takerLongQtyToQuote = RunRecord.Num8.GetValueOrDefault();
                            }

                            //make case for polo
                            if (takerLong.ToString() == "Poloniex")
                            {
                                if (takerLongMakerFee < 1.0M)
                                    takerLongQtyToQuote = takerLongQtyToQuote / (1.0M - takerLongMakerFee);
                            }

                            //make case for bitmex. TODO: remove this
                            if (takerLong.ToString() == "BitMEX")
                                takerLongQtyToQuote = Math.Ceiling(takerLongQtyToQuote);
                            if (takerShort.ToString() == "BitMEX")
                                takerShortQtyToQuote = Math.Ceiling(takerShortQtyToQuote);
                        }
                        else
                        {
                            return false;
                        }
                        #endregion
                        //Short side
                        obBid = takerShort.GetOrderBook(takerShortSymbol);
                        if (obBid == null) return false;
                        bidPriceTotal = 0.0M; topBidPrice = 0.0M;
                        if (!Helper.GetTopPrice(obBid, Api.Side.Buy, takerShortQtyToQuote, out topBidPrice, out bidPriceTotal, out avgPrice))
                            return false; //not enough on orderbook                     
                        shortTakerFee = bidPriceTotal * takerShortFeeRate;

                        //Long side
                        obAsk = takerLong.GetOrderBook(takerLongSymbol);
                        if (obAsk == null) return false;
                        askPriceTotal = 0.0M; topAskPrice = 0.0M;
                        if (!Helper.GetTopPrice(obAsk, Api.Side.Sell, takerLongQtyToQuote, out topAskPrice, out askPriceTotal, out avgPrice))
                            return false; //not enough on orderbook                     
                        longTakerFee = askPriceTotal * takerLongFeeRate;

                        //See if the spread is less than or equal to the closing taker spread.
                        //calculate spread from long side's ask price - short side's bid price
                        marginApparent = askPriceTotal - bidPriceTotal;
                        decimal margin = 0.0M;
                        if (bidPriceTotal > 0.0M)
                        {
                            margin = marginApparent / askPriceTotal;
                            if (margin <= RunRecord.Num6.GetValueOrDefault())
                            {
                                StopClosingTakerTaker();
                                string msg = "Closing Margin Hit:" + marginApparent.ToStringNormalized() + " " + (margin * 100).ToStringNormalized() + "%" +
                                                    " Short:" + takerShort.ToString() + " " + takerShortSymbol + " " + bidPriceTotal.ToStringNormalized() +
                                                    " Long:" + takerLong.ToString() + " " + takerLongSymbol + " " + askPriceTotal.ToStringNormalized();
                                Message(msg);
                                RunRecord.Message = msg;

                                //Cancel the maker orders                                
                                IResult resLeft = null, resRight = null;
                                Task<IResult> canLeft = null, canRight = null;
                                bool leftOk = true, rightOk = true;
                                //IResult resCancel;
                                //Stop shit quickly
                                //if (MakerClosingOrderLeft != null) MakerClosingOrderLeft.Stop("Run " + RunRecord.Id.ToString() + " Close by Taker-Taker");
                                //if (MakerClosingOrderRight != null) MakerClosingOrderRight.Stop("Run " + RunRecord.Id.ToString() + " Close by Taker-Taker");                            

                                if (MakerClosingOrderLeft != null)
                                {
                                    canLeft = MakerClosingOrderLeft.CancelOrder("Cancel by Taker-Taker " + RunRecord.Id.ToString());
                                    //if (!resCancel.Success)
                                    //{
                                    //    leftOk = false;
                                    //    RunRecord.Message = "Failed to cancel left closing maker order." + MakerClosingOrderLeft.OrderID;
                                    //    RunRecord.Status = (int)RunStatus.CompletedWithError;
                                    //}
                                }
                                if (MakerClosingOrderRight != null)
                                {
                                    canRight = MakerClosingOrderRight.CancelOrder("Cancel by Taker-Taker " + RunRecord.Id.ToString());
                                    //if (!resCancel.Success)
                                    //{
                                    //    rightOk = false;
                                    //    RunRecord.Message = "Failed to cancel right closing maker order. " + MakerClosingOrderRight.OrderID;
                                    //    RunRecord.Status = (int)RunStatus.CompletedWithError;
                                    //}
                                }
                              
                                if (canLeft != null)
                                {
                                    resLeft = await canLeft;
                                    if (!resLeft.Success)
                                    {
                                        leftOk = false;
                                        RunRecord.Message = "Failed to cancel left closing maker order." + MakerClosingOrderLeft.OrderID;
                                        RunRecord.Status = (int)RunStatus.CompletedWithError;
                                    }
                                }
                                if (canRight != null)
                                {
                                    resRight = await canRight;
                                    if (!resRight.Success)
                                    {
                                        rightOk = false;
                                        RunRecord.Message = "Failed to cancel right closing maker order. " + MakerClosingOrderRight.OrderID;
                                        RunRecord.Status = (int)RunStatus.CompletedWithError;
                                    }
                                }

                                if (leftOk && rightOk)
                                {
                                    //Execute the sell order
                                    msg = "Placing taker Sell order: " + takerShort.ToString() + " " + takerShortSymbol + " Px:" + topBidPrice.ToStringNormalized() + " Amt:" + takerShortQtyToQuote.ToStringNormalized();
                                    Message(msg);

                                    IResult sellOrder = ExecuteTakerOrder(takerShort, takerShortSymbol, Api.Side.Sell, takerShortQtyToQuote, topBidPrice, 0.0M, Api.OrderOptions.None).Result;
                                    if (sellOrder.Success)
                                    {
                                        processed = true;
                                        //closing sell
                                        if (RunRecord.Text5.ToLower() == "sell")
                                        { //the closing sell order is the right
                                            IOrder ord = (IOrder)sellOrder;
                                            RunRecord.Order4 = ord.OrderID;
                                            RunRecord.Num12 = ord.NetAmount;
                                            RunRecord.Num14 = ord.AveragePrice.GetValueOrDefault() == 0.0M ? ord.Price : ord.AveragePrice.Value;
                                        }
                                        else
                                        { //the closing sell order is the left
                                            IOrder ord = (IOrder)sellOrder;
                                            RunRecord.Order6 = ord.OrderID;
                                            RunRecord.Num11 = ord.NetAmount;
                                            RunRecord.Num13 = ord.AveragePrice.GetValueOrDefault() == 0.0M ? ord.Price : ord.AveragePrice.Value;
                                        }

                                        //Execute buy order
                                        msg = "Placing taker Buy order: " + takerLong.ToString() + " " + takerLongSymbol + " Px:" + topAskPrice.ToStringNormalized() + " Amt:" + takerLongQtyToQuote.ToStringNormalized();
                                        Message(msg);

                                        IResult buyOrder = ExecuteTakerOrder(takerLong, takerLongSymbol, Api.Side.Buy, takerLongQtyToQuote, topAskPrice, 0.0M, Api.OrderOptions.None).Result;
                                        if (buyOrder.Success)
                                        {//closing buy
                                            if (RunRecord.Text5.ToLower() == "buy")
                                            { //the closing buy order is the right
                                                IOrder ord = (IOrder)buyOrder;
                                                RunRecord.Order4 = ord.OrderID;
                                                RunRecord.Num12 = ord.NetAmount;
                                                RunRecord.Num14 = ord.AveragePrice.GetValueOrDefault() == 0.0M ? ord.Price : ord.AveragePrice.Value;
                                            }
                                            else
                                            { //the closing buy order is the left
                                                IOrder ord = (IOrder)buyOrder;
                                                RunRecord.Order6 = ord.OrderID;
                                                RunRecord.Num11 = ord.NetAmount;
                                                RunRecord.Num13 = ord.AveragePrice.GetValueOrDefault() == 0.0M ? ord.Price : ord.AveragePrice.Value;
                                            }
                                            IsPositionClosed = true; ; //position closed

                                        }
                                        else
                                        { //buy order fail
                                            RunRecord.Message = buyOrder.Message;
                                            RunRecord.Status = (int)RunStatus.Fault;
                                        }
                                    }
                                    else
                                    { //sell order fail
                                        RunRecord.Message = sellOrder.Message;
                                        RunRecord.Status = (int)RunStatus.Fault;
                                    } //order execution completed
                                }
                            }//end margin match 
                        }
                        if (processed)
                        {
                            if (!string.IsNullOrEmpty(RunRecord.Order4) || !string.IsNullOrEmpty(RunRecord.Order6))
                            {
                                //Confirm both left and right closing qty. If not, get it.                                           
                                IOrder[] takerExecuted;
                                if (RunRecord.Num12.GetValueOrDefault() == 0.0M) //taker closing is empty
                                {
                                    if (RunRecord.Text5.ToLower() == "buy" && !string.IsNullOrEmpty(RunRecord.Order4))
                                    {
                                        takerExecuted = await takerLong.GetOrdersAsync(RunRecord.Order4);
                                    }
                                    else
                                    {
                                        takerExecuted = await takerShort.GetOrdersAsync(RunRecord.Order4);
                                    }
                                    if (takerExecuted.Length > 0)
                                    {
                                        RunRecord.Num12 = takerExecuted[0].NetAmount;
                                        RunRecord.Num14 = takerExecuted[0].AveragePrice;
                                    }
                                }
                                if (RunRecord.Num11.GetValueOrDefault() == 0.0M)
                                {
                                    if (RunRecord.Text5.ToLower() == "buy" && !string.IsNullOrEmpty(RunRecord.Order6))
                                    {
                                        takerExecuted = await takerShort.GetOrdersAsync(RunRecord.Order6);
                                    }
                                    else
                                    {
                                        takerExecuted = await takerLong.GetOrdersAsync(RunRecord.Order6);
                                    }
                                    if (takerExecuted.Length > 0)
                                    {
                                        RunRecord.Num11 = takerExecuted[0].NetAmount;
                                        RunRecord.Num13 = takerExecuted[0].AveragePrice;
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (lockAcquired)
                            smfTakerTakerCloser.Release();
                    }
                }//end sync
                return processed;
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {


                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                    // TODO: set large fields to null.
                    StopClosingTakerTaker();
                    // TODO: dispose managed state (managed objects).
                    MakerClosingOrderLeft = null;
                    MakerClosingOrderRight = null;
                    MakerOpeningOrder = null;
                    Maker = null;
                    Taker = null;
                    if (TimerSettlement != null)
                        TimerSettlement.Dispose();
                    if (TimerReset != null)
                        TimerReset.Dispose();
                    OnPositionClosed = null;
                    OnMessage = null;
                    disposedValue = true;
                }
            }

            // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
            // ~MakerTakerSet() {
            //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            //   Dispose(false);
            // }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
                // TODO: uncomment the following line if the finalizer is overridden above.
                // GC.SuppressFinalize(this);
            }
            #endregion
        }
    }
}
