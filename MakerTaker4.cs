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
    public class MakerTaker4 : Interface.IStrategy
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
        const ushort MAX_MAKER_RETRY = 1;
        const ushort MAX_MAKER_RETRY_INTERVAL_SECONDS = 5;
        private List<IApi> subscribedApis = new List<IApi>();
        private IDictionary<int, MakerTakerSet> makerTakerSets = new Dictionary<int, MakerTakerSet>();
        public event EventHandler<string> OnMessage;
        private readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        const double HURDLE_BOOST_FACTOR = 0.05; //5 percent;
        const ushort HURDLE_BOOST_PERIOD_MINUTE = 5; //5 minutes;
        private const bool USE_PRICE_BRACKET = true;

        #endregion

        #region Methods
        
        public void Start()
        {
            if (isStarted) return; isStarted = true;
            Message("Running Maker-Taker..." + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString(), 0);
            Run();
        }

        public async Task Stop()
        {
            if (!isStarted) return;
            using (var db = new Tuna())
            {
                foreach (MakerTakerSet set in makerTakerSets.Values)
                {
                    if (set.TimerSettlement != null)
                        set.TimerSettlement.Stop();

                    if (set.MakerOpeningOrder != null) set.MakerOpeningOrder.Stop("Maker-Taker stopped");
                    if (set.MakerClosingOrderLeft != null) set.MakerClosingOrderLeft.Stop("Maker-Taker stopped");
                    if (set.MakerClosingOrderRight != null) set.MakerClosingOrderRight.Stop("Maker-Taker stopped");

                    set.RunRecord.Status = (int)RunStatus.Stopped;
                    db.Entry(set.RunRecord).State = System.Data.Entity.EntityState.Modified;
                }
                db.SaveChangesAsync();
            }
            makerTakerSets.Clear();

            isStarted = false;            
            Message("Stopped " + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString(), 0);
        }

        /// <summary>
        /// Run an operation. Run all available if no ID is provide.
        /// </summary>
        private async void Run(int OpID = -1)
        {
            MakerTakerSet set;
            log.Debug("Run()" + OpID.ToString());

            using (var db = new Tuna())
            {
                log.Debug("DB Found? " + db.Database.Exists().ToString());

                //OPs left join Runs
                var queryOpRuns = from op in db.OPs.Where(o => o.Name == "Maker-Taker" && o.Enabled == true && (OpID == -1 || o.Id == OpID))
                                  join run in db.Runs.Where(o => o.Status <= (int)RunStatus.Running)
                on op.Id equals run.OpId into oprun
                                  from run in oprun.DefaultIfEmpty()
                                  select new { OP = op, Run = run };

                List<MakerTakerSet> tmpList = new List<MakerTakerSet>();
                log.Debug("No. of Ops " + queryOpRuns.Count().ToString());
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
                        
                    if (set.Maker.GetOrderBook(set.RunRecord.Text2) == null || set.Taker.GetOrderBook(set.RunRecord.Text4) == null)
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
                            //TODO: Replace timer with a scheduler.
                            //set the timer to trigger X minute before settlement to close the positions
                            //Note the timer has max interval of int.MaxValue(), if settlement is too long in the future, the timer will trigger before that and restart and repeat until settlement.
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
                await db.SaveChangesAsync();

                foreach (MakerTakerSet s in tmpList)
                    makerTakerSets.Add(s.RunRecord.Id, s);
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
                    
                    //TODO: Replace timer with a scheduler.
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
                    set.RunRecord.Started = DateTime.UtcNow; 
                    set.MakerOpeningOrder.Start();

                }
                else if (!set.IsPositionClosed)
                {
                    set.MakerClosingOrderLeft.OnOrderHit += MakerClosingOrderLeft_OnOrderHit;
                    set.MakerClosingOrderLeft.OnError += MakerClosingOrderLeft_OnError;
                    set.MakerClosingOrderLeft.OnOrderCancelled += MakerClosingOrderLeft_OnOrderCancelled;
                    set.MakerClosingOrderRight.OnOrderHit += MakerClosingOrderRight_OnOrderHit;
                    set.MakerClosingOrderRight.OnError += MakerClosingOrderRight_OnError;
                    set.MakerClosingOrderRight.OnOrderCancelled += MakerClosingOrderRight_OnOrderCancelled;

                    set.MakerClosingOrderLeft.Start();
                    set.MakerClosingOrderRight.Start();
                }
                else //closed
                {
                    //reset
                }
                makerTakerSets.Remove(set.RunRecord.Id);
                makerTakerSets.Add(set.RunRecord.Id, set);
                //Listen to orderbook for taker-taker close
                if (set.IsPositionOpened && !set.IsPositionClosed)
                {
                    set.OnPositionClosed += Set_OnPositionClosed;
                    set.StartClosingTakerTaker();
                }
                using (var db = new Tuna())
                {
                    var run = db.Entry<Run>(set.RunRecord);
                    run.Entity.Status = (int)RunStatus.Running;
                    run.State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();
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

                        res = await set.ExecuteTakerOrder(set.Maker, record.Text2, Api.Side.Buy, record.Num7.Value, topPrice, set.OpRecord.Num7.GetValueOrDefault()); //left taker order
                        if (res.Success)
                        {
                            record.Num11 = ((IOrder)res).NetAmount.GetValueOrDefault();
                            record.Num13 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                            record.Order6 = ((IOrder)res).OrderID;
                                                        
                            IOrderBook obTaker = set.Taker.GetOrderBook(record.Text4);
                            if (!Helper.GetTopPrice(obTaker, Api.Side.Buy, record.Num8.Value, out topPrice, out totalPrice, out avgPrice))
                                return new Result(false, "Not enough quantities on orderbook or quoting has failed " + set.Taker.ToString() + " " + record.Text2);
                            
                            res = await set.ExecuteTakerOrder(set.Taker, record.Text4, Api.Side.Sell, record.Num8.Value, topPrice, set.OpRecord.Num7.GetValueOrDefault()); //right taker order

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

                        res = await set.ExecuteTakerOrder(set.Maker, record.Text2, Api.Side.Sell, record.Num7.Value, topPrice, set.OpRecord.Num7.GetValueOrDefault()); //left taker
                        if (res.Success)
                        {
                            record.Num11 = ((IOrder)res).NetAmount.GetValueOrDefault();
                            record.Num13 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                            record.Order6 = ((IOrder)res).OrderID;                            

                            IOrderBook obRight = set.Taker.GetOrderBook(record.Text4);
                            if (!Helper.GetTopPrice(obRight, Api.Side.Sell, record.Num8.Value, out topPrice, out totalPrice, out avgPrice))
                                return new Result(false, "Not enough quantities on orderbook or quoting has failed " + set.Taker.ToString() + " " + record.Text2);

                            res = await set.ExecuteTakerOrder(set.Taker, record.Text4, Api.Side.Buy, record.Num8.Value, topPrice, set.OpRecord.Num7.GetValueOrDefault()); //right taker order
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
            if(set.TimerSettlement != null)
                set.TimerSettlement.Stop();
            set.RunRecord.Completed = DateTime.UtcNow;
            set.RunRecord.Status = (int)RunStatus.Completed;
            set.RunRecord.Message = set.FaultMessage;            
            using (var db = new Tuna())
            {
                var r = db.Entry<Run>(set.RunRecord);
                r.State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
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

                if (set.MakerOpeningOrder == null) //order retrieval failed
                {
                    success = await PlaceOpeningMakerOrder(set);
                    
                    if(success)
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


            /*--------------------Position opened-------------------*/
            else if (string.IsNullOrEmpty(set.RunRecord.Order4) && string.IsNullOrEmpty(set.RunRecord.Order6)) //position not closed
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
                        if(leftOrderOk)
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
            while ((set.MakerOpeningOrder == null || set.MakerOpeningOrder.IsCancelled) && MAX_MAKER_RETRY >= retry)
            {
                set.MakerOpeningOrder = await FloatingPriceOrder3.Create(makerApi,
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

                if (set.MakerOpeningOrder == null || set.MakerOpeningOrder.IsCancelled)
                {
                    retry++;
                    if (MAX_MAKER_RETRY > retry)
                    {
                        await Task.Delay(MAX_MAKER_RETRY_INTERVAL_SECONDS * 1000); //wait X minutes;
                    }
                    else
                    {
                        set.FaultMessage = "Failed to place opening maker order. ";
                        if (set .MakerClosingOrderLeft != null && set.MakerClosingOrderLeft.IsCancelled)
                            set.FaultMessage += "Order is canceled by exchange.";
                        return false;
                    }
                }
            }            
            set.RunRecord.Order1 = set.MakerOpeningOrder.OrderID;
            return true;
        }

        private async Task<bool> PlaceLeftClosingMakerOrder(MakerTakerSet set)
        {
            set.RunRecord.Num11 = null;
            set.RunRecord.Num13 = null;
            set.RunRecord.Order3 = null;
            PricingScheme ps;
            IApi leftMakerApi = null, rightMakerApi = null;
            string leftMakerInstrument = string.Empty, rightMakerInstrument = string.Empty;
            Api.Side leftMakerSide;
            Api.OrderType leftMakerOrderType;
            decimal leftMakerSize = 0.0M;
            decimal hurdleRate = 0.0M;

            rightMakerInstrument = set.RunRecord.Text4;
            rightMakerApi = set.Taker;
            //-----------------left side------------------------
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
            hurdleRate = set.RunRecord.Num3.GetValueOrDefault();   //closing hurdle rate

            leftMakerSize = set.RunRecord.Num7.HasValue ? set.RunRecord.Num7.Value : set.RunRecord.Num1.GetValueOrDefault();

            //Make case for Poloniex, if closing maker buy, adjust qty to compensate at least for maker fee
            if (leftMakerSide == Api.Side.Buy && leftMakerApi.ToString() == "Poloniex")
            {
                if (set.MakerMakerFee < 1.0M)
                    leftMakerSize = leftMakerSize / (1.0M - set.MakerMakerFee);
            }
            //Make case end         

            ushort retry = 0;
            while ((set.MakerClosingOrderLeft == null || set.MakerClosingOrderLeft.IsCancelled) && MAX_MAKER_RETRY >= retry)
            {
                set.MakerClosingOrderLeft = await FloatingPriceOrder3.Create(leftMakerApi,
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

                if (set.MakerClosingOrderLeft == null || set.MakerClosingOrderLeft.IsCancelled)
                {
                    retry++;
                    if (MAX_MAKER_RETRY >= retry)
                    {
                        await Task.Delay(MAX_MAKER_RETRY_INTERVAL_SECONDS * 1000); //wait X minutes;
                    }
                    else
                    {
                        set.FaultMessage = "Failed to place left closing maker order. ";
                        if (set.MakerClosingOrderLeft != null && set.MakerClosingOrderLeft.IsCancelled)
                            set.FaultMessage += "Order is canceled by exchange.";
                        return false;
                    }
                }
            }
            set.RunRecord.Order3 = set.MakerClosingOrderLeft.OrderID;
            return true;
        }

        private async Task<bool> PlaceRightClosingMakerOrder(MakerTakerSet set)
        {
            set.RunRecord.Num12 = null;
            set.RunRecord.Num14 = null;
            set.RunRecord.Order5 = null;
            PricingScheme ps;
            IApi leftMakerApi = null, rightMakerApi = null;
            string leftMakerInstrument = string.Empty, rightMakerInstrument = string.Empty;
            Api.Side rightMakerSide;
            Api.OrderType rightMakerOrderType;
            decimal rightMakerSize;
            decimal hurdleRate = 0.0M;

            leftMakerApi = set.Maker;
            leftMakerInstrument = set.RunRecord.Text2;
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

            hurdleRate = set.RunRecord.Num3.GetValueOrDefault();   //closing hurdle rate

            //Make case for Poloniex. Adjust the qty to compensate for at least the maker fee.
            if (rightMakerSide == Api.Side.Buy && rightMakerApi.ToString() == "Poloniex")
            {
                if (set.TakerMakerFee < 1.0M)
                    rightMakerSize = rightMakerSize / (1.0M - set.TakerMakerFee);
            }
            //Make case end

            ushort retry = 0;
            while ((set.MakerClosingOrderRight == null || set.MakerClosingOrderRight.IsCancelled) && MAX_MAKER_RETRY >= retry)
            {
                set.MakerClosingOrderRight = await FloatingPriceOrder3.Create(rightMakerApi,
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
                                
                if ( (set.MakerClosingOrderRight == null || set.MakerClosingOrderRight.IsCancelled))
                {
                    retry++;
                    if (MAX_MAKER_RETRY >= retry)
                    {
                        await Task.Delay(MAX_MAKER_RETRY_INTERVAL_SECONDS * 1000); //wait X minutes;
                    }
                    else
                    {
                        set.FaultMessage = "Failed to place right closing maker order. ";
                        if (set.MakerClosingOrderRight != null && set.MakerClosingOrderRight.IsCancelled)
                            set.FaultMessage += "Order is cancelled. ";
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

            set.MakerOpeningOrder = await FloatingPriceOrder3.Get(makerApi,
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
                        
            set.MakerClosingOrderLeft = await FloatingPriceOrder3.Get(makerApi,
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
            set.MakerClosingOrderRight = await FloatingPriceOrder3.Get(makerApi,
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
            if (set.MakerOpeningOrder != null) set.MakerOpeningOrder.Stop("Maker-Taker fault " + faultMessage);
            if (set.MakerClosingOrderLeft != null) set.MakerClosingOrderLeft.Stop("Maker-Taker fault " + faultMessage);
            if (set.MakerClosingOrderRight != null) set.MakerClosingOrderRight.Stop("Maker-Taker fault " + faultMessage);

            set.RunRecord.Message = faultMessage;
            set.RunRecord.Status = (int)RunStatus.Fault;
            using (var db = new Tuna())
            {
                var o = db.Entry<Run>(set.RunRecord);
                o.State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
            }

            if(doRerun)
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
                OnMessage?.Invoke(this, "[MakerTaker][" + runId.ToString() + "] " + message);
            }
            else
            {
                OnMessage?.Invoke(this, "[MakerTaker] " + message);
            }
            
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
            FloatingPriceOrder3 mtOrder = (FloatingPriceOrder3)sender;
            string msg = string.Empty;
            bool doRerun = false;            
            decimal qtyFilled = mtOrder.NetAmount.GetValueOrDefault();            
                        
            if (qtyFilled > 0)
            {
                set = GetWorkingSet(mtOrder.OwnerApi, mtOrder.OrderID);
                if (set == null)
                    return;

                set.MakerOpeningOrder.ClearEventSubscribers();
                msg = "Opening maker hit: " + mtOrder.ToString(); Message(msg, set.RunRecord.Id); Helper.PlaySfx(Sfx.Alert);
                
                set.RunRecord.Num7 = qtyFilled; //set maker executed qty    
                set.RunRecord.Num9 = mtOrder.AveragePrice.GetValueOrDefault() == 0.0M ? mtOrder.Price : mtOrder.AveragePrice.GetValueOrDefault();
                set.RunRecord.Message = msg;
                IResult rst;
                decimal qtyTakerQuote = set.RunRecord.Num7.Value;
                if (mtOrder.Side == Api.Side.Sell) //was an opening sell
                {
                    //Make case for Poloniex, if a buy taker were to be placed. Adjust the taker qty to compensate for at least the maker fee.
                    if (mtOrder.TargetAPI.ToString() == "Poloniex")
                    {
                        if (set.TakerMakerFee < 1.0M)
                            qtyTakerQuote = qtyTakerQuote / (1.0M - set.TakerMakerFee);
                    }
                    //Make case end
                    //Place an opposite buy taker order on the right side
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Buy, qtyTakerQuote, mtOrder.TargetPrice, set.OpRecord.Num7.GetValueOrDefault()); //right taker
                    if (rst.Success)
                    {                
                        set.RunRecord.Order2 = ((IOrder)rst).OrderID;
                        set.RunRecord.Num8 = ((IOrder)rst).NetAmount.GetValueOrDefault();
                        //I must have the correct amount here, get it from server if its missing.
                        if (set.RunRecord.Num8 == 0.0M) 
                        {
                            IOrder[] takerExecuted = await mtOrder.TargetAPI.GetOrdersAsync(set.RunRecord.Order2);
                            if (takerExecuted.Length > 0)
                                set.RunRecord.Num8 = takerExecuted[0].NetAmount;
                        }                        
                        set.RunRecord.Num10 = ((IOrder)rst).AveragePrice.HasValue ? ((IOrder)rst).AveragePrice : mtOrder.TargetPrice;
                        Message("Opening taker placed: " + ((IOrder)rst).ToString(), set.RunRecord.Id);
                        doRerun = true;
                    }
                    else
                    {
                        msg = "Fail to place opening taker " + mtOrder.TargetAPI.ToString() + " " + mtOrder.TargetSymbol + ". " + rst.Message;
                        Message(msg, set.RunRecord.Id);
                        RunFault(set, msg);
                    }
                }
                else //was an opening buy
                {
                    //Place an opposite sell taker order on the right side
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Sell, qtyTakerQuote, mtOrder.TargetPrice, set.OpRecord.Num7.GetValueOrDefault()); //right taker
                    if (rst.Success)
                    {
                        //TODO: Get order hit confirmation?
                        set.RunRecord.Order2 = ((IOrder)rst).OrderID;
                        set.RunRecord.Num8 = ((IOrder)rst).NetAmount.GetValueOrDefault();
                        //I must have the correct amount here, get it from server if its missing.
                        if (set.RunRecord.Num8 == 0.0M)
                        {
                            IOrder[] takerExecuted = await mtOrder.TargetAPI.GetOrdersAsync(set.RunRecord.Order2);
                            if (takerExecuted.Length > 0)
                                set.RunRecord.Num8 = takerExecuted[0].NetAmount;
                        }
                        set.RunRecord.Num10 = ((IOrder)rst).AveragePrice.HasValue ? ((IOrder)rst).AveragePrice : mtOrder.TargetPrice;
                        Message("Opening taker placed: " + ((IOrder)rst).ToString(), set.RunRecord.Id);
                        doRerun = true;
                    }
                    else
                    {
                        msg = "Fail to place opening taker  " + mtOrder.TargetAPI.ToString() + " " + mtOrder.TargetSymbol + ". " + rst.Message;
                        Message(msg, set.RunRecord.Id);
                        RunFault(set, msg);
                    }
                }
                using (var db = new Tuna())
                {
                    var o = db.Entry<Run>(set.RunRecord);
                    o.State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();
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
            FloatingPriceOrder3 order = (FloatingPriceOrder3)sender;
            order.Stop("Opening Maker cancelled");
            string msg;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set == null) return;
            
            msg = "Maker abruptly cancelled: " + order.ToString();       

            if (order.CancelReason == Api.CancelReason.OrderIsPostOnly)
            {
                set.BoostOpeningHurdleRate(HURDLE_BOOST_FACTOR, HURDLE_BOOST_PERIOD_MINUTE);
                msg += "\nPost-Only new hurdle: " + set.MakerOpeningOrder.HurdleRate.ToString();
            }

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

        /*
        private void MakerOpeningOrder_OnOrderCancelled0(object sender, EventArgs e)
        {
            FloatingPriceOrder3 order = (FloatingPriceOrder3)sender;
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

        private void MakerOpeningOrder_OnError(object sender, string e)
        {
            Message("Error " + e, 0);
            FloatingPriceOrder3 order = (FloatingPriceOrder3)sender;
            if (order.ErrorCount > MAX_ERROR_COUNT)
            {
                order.Stop("Opening maker order error " + e);
                Message("Order stopped. " + e + " " + order.OrderID + " " + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString(), 0);
            }
        }

        /// <summary>
        /// Raised when the closing maker order on the right market is hit. Cancels the left maker order. Executes a market order to close the left market position.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MakerClosingOrderRight_OnOrderHit(object sender, EventArgs e)
        {            
            MakerTakerSet set;
            FloatingPriceOrder3 mtOrder = (FloatingPriceOrder3)sender;
            decimal qtyFilled = mtOrder.NetAmount.GetValueOrDefault();
            log.Debug("MakerClosingOrderRight_OnOrderHit qtyFilled " + qtyFilled.ToString());
            string msg = string.Empty;
            if (qtyFilled > 0)
            {
                set = GetWorkingSet(mtOrder.OwnerApi, mtOrder.OrderID);
                if (set == null)
                    return;

                set.StopClosingTakerTaker();
                //stop the left order
                set.MakerClosingOrderLeft.ClearEventSubscribers();             
                set.MakerClosingOrderLeft.Stop("Right closing maker order hit");

                //stop the right order
                set.MakerClosingOrderRight.ClearEventSubscribers();
                msg = "Closing maker (right) hit: " + mtOrder.ToString(); Message(msg, set.RunRecord.Id); Helper.PlaySfx(Sfx.Alert);
                set.RunRecord.Num12 = qtyFilled; //set closing maker executed qty    
                set.RunRecord.Num14 = mtOrder.AveragePrice; //set closing maker executed px //TODO: fix polo average price
                set.RunRecord.Message = msg;

                Message("Canceling left maker order", set.RunRecord.Id);
                //IResult rst = await OrderManager.Instance.CancelOrderAsync(set.Maker, set.MakerClosingOrderLeft.OrderID);
                IResult rst = await set.MakerClosingOrderLeft.CancelOrder("Right closing order was hit");
                if (!rst.Success)
                {
                    Message("Failed to cancel left closing maker. " + rst.Message, set.RunRecord.Id);
                    set.RunRecord.Message += rst.Message;
                }
                else
                {
                    Message("Left maker order cancelled. " + rst.Message, set.RunRecord.Id);
                }

                decimal qtyTakerQuote = set.RunRecord.Num12.GetValueOrDefault() > set.RunRecord.Num7.GetValueOrDefault() ? set.RunRecord.Num7.GetValueOrDefault() : set.RunRecord.Num12.GetValueOrDefault();
                if (mtOrder.TargetAPI.ToString() == "BitMEX") //TODO: remove this fix
                    qtyTakerQuote = Math.Ceiling(qtyTakerQuote);

                //execute a taker order on the left                
                if (mtOrder.Side == Api.Side.Sell) //was an closing sell
                {
                    //make case for Polo
                    if (mtOrder.TargetAPI.ToString() == "Poloniex")
                    {
                        if (set.MakerMakerFee < 1.0M)
                            qtyTakerQuote = qtyTakerQuote / (1 - set.MakerMakerFee);
                    }
                    //make case end
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Buy, qtyTakerQuote, mtOrder.TargetPrice, set.OpRecord.Num7.GetValueOrDefault()); //taker to the left for the right
                }
                else //was an closing buy
                {
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Sell, qtyTakerQuote, mtOrder.TargetPrice, set.OpRecord.Num7.GetValueOrDefault()); //taker to the left for the right
                }
                if (rst.Success)
                {
                    set.RunRecord.Order4 = ((IOrder)rst).OrderID;
                    set.RunRecord.Num11 = ((IOrder)rst).NetAmount.GetValueOrDefault();
                    set.RunRecord.Num13 = ((IOrder)rst).AveragePrice.GetValueOrDefault() == 0.0M ? mtOrder.TargetPrice : ((IOrder)rst).AveragePrice.Value;

                    //I must have the confirmed amount here, if not, get it.
                    if (set.RunRecord.Num11 == 0.0M)
                    {
                        IOrder[] takerExecuted = await mtOrder.TargetAPI.GetOrdersAsync(set.RunRecord.Order4);
                        if (takerExecuted.Length > 0)
                        {
                            set.RunRecord.Num11 = takerExecuted[0].NetAmount;
                            set.RunRecord.Num13 = takerExecuted[0].AveragePrice;
                        }
                    }
                    //msg = "Closing taker placed: " + mtOrder.ToString(); Message(msg, set.RunRecord.Id);
                    msg = "Closing taker placed: " + set.RunRecord.Order4 + " Net:" + set.RunRecord.Num11.ToString() + "@" + set.RunRecord.Num13.ToString(); Message(msg, set.RunRecord.Id);
                    set.RunRecord.Message += msg;

                    AfterPositionClosed(set);
                }
                else
                {
                    msg = "Fail to place Closing taker " + mtOrder.TargetAPI.ToString() + " " + mtOrder.TargetSymbol + ". " + rst.Message; Message(msg, set.RunRecord.Id);         
                    RunFault(set, msg);
                }
            }
        }

        private async void MakerClosingOrderRight_OnOrderCancelled(object sender, EventArgs e)
        {          
            FloatingPriceOrder3 order = (FloatingPriceOrder3)sender;
            order.Stop("Right closing maker order cancelled");
            string msg;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set == null) return;
            msg = "Right Closing Maker was cancelled: " + order.ToString(); Message(msg, set.RunRecord.Id);

            if (order.CancelReason == Api.CancelReason.OrderIsPostOnly)
            {
                set.BoostClosingHurdleRate(-HURDLE_BOOST_FACTOR, HURDLE_BOOST_PERIOD_MINUTE);
                msg += "\nPost-Only new hurdle: " + set.MakerClosingOrderRight.HurdleRate.ToString();
            }

            bool success = await PlaceRightClosingMakerOrder(set);
            using (var db = new Tuna())
            {
                var r = db.Entry<Run>(set.RunRecord);
                if (success)
                {
                    set.MakerClosingOrderRight.OnOrderHit += MakerClosingOrderRight_OnOrderHit;
                    set.MakerClosingOrderRight.OnError += MakerClosingOrderRight_OnError;
                    set.MakerClosingOrderRight.OnOrderCancelled += MakerClosingOrderRight_OnOrderCancelled;
                    set.MakerClosingOrderRight.Start();

                    msg = "Right Closing Maker placed: " + set.MakerClosingOrderRight.ToString(); Message(msg, set.RunRecord.Id);
                    r.Entity.Order5 = set.MakerClosingOrderRight.OrderID;
                }
                else
                {
                    //cancel the left maker                    
                    if (set.MakerClosingOrderLeft != null)
                    {
                        //IResult res = await OrderManager.Instance.CancelOrderAsync(set.Maker, set.MakerClosingOrderLeft.OrderID);
                        IResult res = await set.MakerClosingOrderLeft.CancelOrder("Right closing order was cancelled.");

                        if (res.Success)
                            set.RunRecord.Order3 = null;
                        else
                            msg += "\nLeft maker failed to cancel. " + set.MakerClosingOrderLeft.ToString();
                    }
                    r.Entity.Order5 = null;
                    r.Entity.Status = (int)RunStatus.Stopped;
                }
                r.State = System.Data.Entity.EntityState.Modified;
                r.Entity.Message = msg;
                db.SaveChanges();
            }
        }

        private void MakerClosingOrderRight_OnError(object sender, string e)
        {
            FloatingPriceOrder3 order = (FloatingPriceOrder3)sender;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set != null)
            {
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
            log.Debug("MakerClosingOrderLeft_OnOrderHit");
            MakerTakerSet set;
            FloatingPriceOrder3 mtOrder = (FloatingPriceOrder3)sender;
            decimal qtyFilled = mtOrder.NetAmount.GetValueOrDefault();
            log.Debug("MakerClosingOrderLeft_OnOrderHit qtyFilled " + qtyFilled.ToString());
            string msg = string.Empty;
            if (qtyFilled > 0)
            {
                set = GetWorkingSet(mtOrder.OwnerApi, mtOrder.OrderID);
                if (set == null)
                    return;

                set.StopClosingTakerTaker();
                //stop the right order
                set.MakerClosingOrderRight.ClearEventSubscribers();
                set.MakerClosingOrderRight.Stop("Left closing maker order hit");

                //stop the left order
                set.MakerClosingOrderLeft.ClearEventSubscribers();
                msg = "Closing maker (left) hit: " + mtOrder.ToString(); Message(msg, set.RunRecord.Id); Helper.PlaySfx(Sfx.Alert);
                set.RunRecord.Num11 = qtyFilled; //set closing maker executed qty    
                set.RunRecord.Num13 = mtOrder.AveragePrice; //set closing maker executed px
                set.RunRecord.Message = msg;

                //Cancel the maker order on the right first
                Message("Canceling right maker order", set.RunRecord.Id);
                IResult rst = await set.MakerClosingOrderRight.CancelOrder("Left closing order was hit");
                //IResult rst = await OrderManager.Instance.CancelOrderAsync(set.Taker, set.MakerClosingOrderRight.OrderID);
                if (!rst.Success)
                {
                    Message("Failed to cancel right closing maker. " + rst.Message, set.RunRecord.Id);
                    set.RunRecord.Message += rst.Message;
                }
                else
                {
                    Message("Right maker order cancelled. " + rst.Message, set.RunRecord.Id);
                }
                              
                decimal qtyTakerQuote = set.RunRecord.Num11.GetValueOrDefault() > set.RunRecord.Num8.GetValueOrDefault() ? set.RunRecord.Num8.GetValueOrDefault() : set.RunRecord.Num11.GetValueOrDefault();
                if (mtOrder.TargetAPI.ToString() == "BitMEX") //TODO: remove this fix
                    qtyTakerQuote = Math.Ceiling(qtyTakerQuote);

                //attempt to execute a taker order on the right, regardless if cancel success.                
                if (mtOrder.Side == Api.Side.Sell) //was an closing sell
                {
                    //make case for Polo
                    if (mtOrder.TargetAPI.ToString() == "Poloniex")
                    {
                        if (set.TakerMakerFee < 1.0M)
                            qtyTakerQuote = qtyTakerQuote / (1 - set.TakerMakerFee);
                    }
                    //make case end
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Buy, qtyTakerQuote, mtOrder.TargetPrice, set.OpRecord.Num7.GetValueOrDefault()); //taker right
                }
                else //was an closing buy
                {
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Sell, qtyTakerQuote, mtOrder.TargetPrice, set.OpRecord.Num7.GetValueOrDefault()); //taker right
                }
                if (rst.Success)
                {
                    set.RunRecord.Order6 = ((IOrder)rst).OrderID;
                    set.RunRecord.Num12 = ((IOrder)rst).NetAmount.GetValueOrDefault();
                    //I must have the confirmed amount here, if not, get it.
                    if (set.RunRecord.Num12.GetValueOrDefault() == 0.0M)
                    {
                        IOrder[] takerExecuted = await mtOrder.TargetAPI.GetOrdersAsync(set.RunRecord.Order6);
                        if (takerExecuted.Length > 0)
                            set.RunRecord.Num12 = takerExecuted[0].NetAmount;
                    }
                    set.RunRecord.Num14 = ((IOrder)rst).AveragePrice.GetValueOrDefault() == 0.0M ? mtOrder.TargetPrice : ((IOrder)rst).AveragePrice.Value;

                    msg = "Closing taker placed: " + set.RunRecord.Order6 + " Net:" + set.RunRecord.Num12.ToString() + "@" + set.RunRecord.Num14.ToString(); Message(msg, set.RunRecord.Id);
                    set.RunRecord.Message += msg;

                    AfterPositionClosed(set);
                }
                else
                {
                    msg = "Fail to place Closing taker " + mtOrder.TargetAPI.ToString() + " " + mtOrder.TargetSymbol + ". " + rst.Message;
                    Message(msg, set.RunRecord.Id);
                    RunFault(set, msg);                    
                }
            }
        }
        
        private async void MakerClosingOrderLeft_OnOrderCancelled(object sender, EventArgs e)
        {
            FloatingPriceOrder3 order = (FloatingPriceOrder3)sender;
            order.Stop("Left closing maker order cancelled");
            string msg;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set == null) return;
                        
            msg = "Left Closing Maker was cancelled: " + order.ToString(); Message(msg, set.RunRecord.Id);

            if (order.CancelReason == Api.CancelReason.OrderIsPostOnly)
            {
                set.BoostClosingHurdleRate(-HURDLE_BOOST_FACTOR, HURDLE_BOOST_PERIOD_MINUTE);
                msg += "\nPost-Only new hurdle: " + set.MakerClosingOrderLeft.HurdleRate.ToString();
            }

            bool success = await PlaceLeftClosingMakerOrder(set);
            using (var db = new Tuna())
            {
                var r = db.Entry<Run>(set.RunRecord);
                if (success)
                {                    
                    set.MakerClosingOrderLeft.OnOrderHit += MakerClosingOrderLeft_OnOrderHit;
                    set.MakerClosingOrderLeft.OnError += MakerClosingOrderLeft_OnError;
                    set.MakerClosingOrderLeft.OnOrderCancelled += MakerClosingOrderLeft_OnOrderCancelled;
                    set.MakerClosingOrderLeft.Start();

                    msg = "Left Closing Maker placed: " + set.MakerClosingOrderLeft.ToString(); Message(msg, set.RunRecord.Id);
                    r.Entity.Order3 = set.MakerClosingOrderLeft.OrderID;
                }
                else
                {
                    //cancel the right maker                    
                    if (set.MakerClosingOrderRight != null)
                    {
                        //IResult res = await OrderManager.Instance.CancelOrderAsync(set.Taker, set.MakerClosingOrderRight.OrderID);
                        IResult rst = await set.MakerClosingOrderLeft.CancelOrder("Left closing order was cancelled.");
                        if (rst.Success)
                            set.RunRecord.Order5 = null;
                        else
                            msg += "\nRight maker failed to cancel. " + set.MakerClosingOrderRight.ToString();
                    }
                    r.Entity.Order3 = null;                    
                    r.Entity.Status = (int)RunStatus.Stopped;
                }
                r.State = System.Data.Entity.EntityState.Modified;
                r.Entity.Message = msg;
                db.SaveChanges();
            }
        }

        private void MakerClosingOrderLeft_OnError(object sender, string e)
        {
            FloatingPriceOrder3 order = (FloatingPriceOrder3)sender;
            MakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set != null)
            {
                RunFault(set, e, false);
            }
            else
            {
                throw new NotImplementedException(e);
            }
        }

        private void MakerOrder_OnOrderAmended(object sender, EventArgs e)
        {
            FloatingPriceOrder3 fpx = (FloatingPriceOrder3)sender;
            if (!string.IsNullOrEmpty(fpx.OldOrderId) && fpx.OldOrderId != fpx.OrderID)
            {

                MakerTakerSet set = GetWorkingSet(fpx.OwnerApi, fpx.OrderID);
                if(set == null)
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
                        var run = db.Entry<Run>(set.RunRecord);
                        run.State = System.Data.Entity.EntityState.Modified;
                        db.SaveChanges();
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
                        var o = db.Entry<Run>(set.RunRecord);
                        o.State = System.Data.Entity.EntityState.Modified;
                        await db.SaveChangesAsync();
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
            public FloatingPriceOrder3 MakerOpeningOrder;
            public FloatingPriceOrder3 MakerClosingOrderLeft;
            public FloatingPriceOrder3 MakerClosingOrderRight;            
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
            private Object syncLock = new Object();
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
                //if (!IsPositionOpened || IsPositionClosed || !Maker.IsConnected || !Taker.IsConnected)
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
                            var r = db.Entry<Run>(RunRecord);
                            r.State = System.Data.Entity.EntityState.Modified;
                            db.SaveChanges();
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
            internal Task<IResult> ExecuteTakerOrder(IApi api, string symbol, Api.Side side, decimal quoteAmount, decimal quotePrice, decimal priceBracket)
            {
                if (side == Api.Side.Buy)
                {
                    if (USE_PRICE_BRACKET && priceBracket > 0.0M)
                    {
                        quotePrice = quotePrice + (quotePrice * priceBracket);
                        log.Debug("Using bracket price: " + quotePrice.ToStringNormalized());
                    }
                    return OrderManager.Instance.PostBuyOrderAsync(api, symbol, Api.OrderType.Limit, quoteAmount, quotePrice);
                }
                else if (side == Api.Side.Sell)
                {
                    if (USE_PRICE_BRACKET && priceBracket > 0.0M)
                    {
                        quotePrice = quotePrice - (quotePrice * priceBracket);
                        log.Debug("Using bracket price: " + quotePrice.ToStringNormalized());
                    }
                    return OrderManager.Instance.PostSellOrderAsync(api, symbol, Api.OrderType.Limit, quoteAmount, quotePrice);
                }
                else
                    throw new ArgumentOutOfRangeException("side", side.ToString(), "Side must be buy or sell.");
            }
            
            public async Task<bool> Process()
            {
                IOrderBook obBid, obAsk;
                IApi takerLong, takerShort;
                bool processed = false;
                lock (syncLock)
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
                            IResult resCancel;
                            bool leftOk = true, rightOk = true;                            
                            //Stop shit quickly
                            //if (MakerClosingOrderLeft != null) MakerClosingOrderLeft.Stop("Run " + RunRecord.Id.ToString() + " Close by Taker-Taker");
                            //if (MakerClosingOrderRight != null) MakerClosingOrderRight.Stop("Run " + RunRecord.Id.ToString() + " Close by Taker-Taker");                            
                            if (MakerClosingOrderLeft != null)
                            {
                                resCancel = MakerClosingOrderLeft.CancelOrder("Cancel by Taker-Taker " + RunRecord.Id.ToString()).Result;
                                if (!resCancel.Success)
                                {
                                    leftOk = false;
                                    RunRecord.Message = "Failed to cancel left closing maker order." + MakerClosingOrderLeft.OrderID;
                                    RunRecord.Status = (int)RunStatus.CompletedWithError;
                                }
                            }
                            if (MakerClosingOrderRight != null)
                            {
                                resCancel = MakerClosingOrderRight.CancelOrder("Cancel by Taker-Taker " + RunRecord.Id.ToString()).Result;
                                if (!resCancel.Success)
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

                                IResult sellOrder = ExecuteTakerOrder(takerShort, takerShortSymbol, Api.Side.Sell, takerShortQtyToQuote, topBidPrice, 0.0M).Result;
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

                                    IResult buyOrder = ExecuteTakerOrder(takerLong, takerLongSymbol, Api.Side.Buy, takerLongQtyToQuote, topAskPrice, 0.0M).Result;
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
                }//end sync

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
