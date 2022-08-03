using System;
using System.Linq;
using System.Collections.Generic;
using Chordata.Bex.Api.Interface;
using Chordata.Bex.Central.Common;
using Chordata.Bex.Central.Data;
using System.Threading.Tasks;
namespace Chordata.Bex.Central
{
    class TakerTaker3 : Interface.IStrategy
    {
        /* OP field descriptions:
         * Name:    Strategy name
         * Text1:   LeftTaker API
         * Text2:   LeftTaker instrument
         * Text3:   RightTaker API
         * Text4:   RightTaker instrument
         * Text5:   LeftTaker side
         * Num1:    Taker order qty
         * Num2:    Opening hurdle rate        
         * Num3:    Closing hurdle rate 
         * Num4:    Hours before settlement of early close cutoff time
         * Num5:    Minutes before settlement of closing of position
         * Num6:    Taker Taker Closing hurdle rate
         */

        /* Run field descriptions:
         * ---------------Run parameters (copied from OP)--------------------v
         * Text1:   LeftTaker API
         * Text2:   LeftTaker instrument
         * Text3:   RightTaker API
         * Text4:   RightTaker instrument
         * Text5:   LeftTaker side
         * Num1:    Taker order qty
         * Num2:    Opening hurdle rate
         * Num3:    Closing hurdle rate         
         * Num4:    Hours before settlement of early close cutoff time
         * Num5:    Minutes before settlement of closing of position
         * Num6:    Taker Taker Closing hurdle rate
         * v-----------------------Execution---------------------------------v
         * Num7:    LeftTaker opening executed Qty
         * Num8:    RightTaker opening executed Qty
         * Num9:    LeftTaker opening executed price
         * Num10:   RightTaker opening executed price    
         * Num11:   LeftTaker closing executed qty
         * Num12:   RightTaker closing executed qty
         * Num13:   LeftTaker closing executed price
         * Num14:   RightTaker closing executed price
         * Date1:   LeftTaker settlement datetime
         * Order1:  Opening LeftTaker order
         * Order2:  Opening RightTaker order
         * Order3:  Closing maker order left
         * Order4:  Closing taker order executed for left maker order (this is the right taker order)
         * Order5:  Closing maker order right
         * Order6:  Closing taker order executed for right maker order (this is the left taker order)
         * 
         */

        #region Declarations

        bool isStarted = false;
        const ushort MAX_ERROR_COUNT = 3; //maximum error occurance before hard stop on the order
        const ushort MAX_TAKER_RETRY = 2;
        const ushort MAX_TAKER_RETRY_INTERVAL_SECONDS = 3;
        private List<IApi> subscribedApis = new List<IApi>();
        private List<IApi> subscribedOrderbookApis = new List<IApi>();

        private IDictionary<int, TakerTakerSet> takerTakerSets = new Dictionary<int, TakerTakerSet>();
        public event EventHandler<string> OnMessage;
        private readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private Object syncRerun = new Object();


        #endregion

        #region Methods

        private void Message(string message)
        {
            OnMessage?.Invoke(this, "[TakerTaker] " + message);
            log.Debug(message);
        }

        public void Start()
        {
            if (isStarted) return; isStarted = true;
            Message("Running Taker-Taker..." + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString());
            Run();
        }

        private void SubscriibeToOrderBookChanged(IApi api)
        {

        }

        public async Task Stop()
        {
            using (Tuna db = new Tuna())
            {
                foreach (IApi i in subscribedApis)
                {
                    i.OnConnectionClosed -= Exchange_OnConnectionClosed;
                }

                foreach (var s in takerTakerSets)
                {
                    TakerTakerSet set = s.Value;
                    set.LeftTaker.OnOrderBookChanged -= Exchange_OnOrderBookChanged;
                    set.RightTaker.OnOrderBookChanged -= Exchange_OnOrderBookChanged;
                    set.RunRecord.Status = (int)RunStatus.Stopped;

                    var o = db.Entry(set.RunRecord);
                    o.State = System.Data.Entity.EntityState.Modified;
                }
                subscribedOrderbookApis.Clear();
                db.SaveChanges();
            }
            Message("Stopped " + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString());
            isStarted = false;
        }

        private async void Run(int OpID = -1)
        {
            TakerTakerSet set;

            using (var db = new Tuna())
            {
                //OPs left join Runs
                var queryOpRuns = from op in db.OPs.Where(o => o.Name == "Taker-Taker" && o.Enabled == true && (OpID == -1 || o.Id == OpID))
                                  join run in db.Runs.Where(o => o.Status <= (int)RunStatus.Running)
                on op.Id equals run.OpId into oprun
                                  from run in oprun.DefaultIfEmpty()
                                  select new { OP = op, Run = run };

                List<TakerTakerSet> tmpList = new List<TakerTakerSet>();

                foreach (var v in queryOpRuns)
                {
                    set = new TakerTakerSet();
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
                    set.LeftTaker = ApiManager.Instance.GetApi(set.RunRecord.Text1);
                    set.RightTaker = ApiManager.Instance.GetApi(set.RunRecord.Text3);

                    if (!subscribedApis.Contains(set.LeftTaker))
                    {
                        subscribedApis.Add(set.LeftTaker);
                        set.LeftTaker.OnConnectionClosed += Exchange_OnConnectionClosed;
                    }
                    
                    set.LeftTaker.Connect(true);

                    if (!subscribedApis.Contains(set.RightTaker))
                    {
                        subscribedApis.Add(set.RightTaker);
                        set.RightTaker.OnConnectionClosed += Exchange_OnConnectionClosed;
                    }
                    
                    set.RightTaker.Connect(true);

                    do
                    {
                        await Task.Delay(1000);
                    }
                    while (!set.RightTaker.IsConnected || !set.LeftTaker.IsConnected);
                    //---Both APIs should be connected below this line---

                    set.LeftTakerFeeRate = (await set.LeftTaker.GetInstrumentAsync(set.RunRecord.Text2)).TakerFee.GetValueOrDefault();
                    set.RightTakerFeeRate = (await set.RightTaker.GetInstrumentAsync(set.RunRecord.Text4)).TakerFee.GetValueOrDefault();

                    if (!set.RightTaker.IsOrderBookSubscribed(set.RunRecord.Text4))
                    {
                        Message("Subscribing to " + set.RunRecord.Text4 + ".");
                        set.RightTaker.SubscribeToOrderBookAsync(set.RunRecord.Text4);
                    }
                    if (!set.LeftTaker.IsOrderBookSubscribed(set.RunRecord.Text2))
                    {
                        Message("Subscribing to " + set.RunRecord.Text2 + ".");
                        set.LeftTaker.SubscribeToOrderBookAsync(set.RunRecord.Text2);
                    }

                    set.LeftTaker.SubscribeToOrdersAsync(string.Empty);
                    set.RightTaker.SubscribeToOrdersAsync(string.Empty);

                    if (set.LeftTaker.GetOrderBook(set.RunRecord.Text2) == null ||
                        set.RightTaker.GetOrderBook(set.RunRecord.Text4) == null)
                    {
                        Message("Waiting for orderbooks");
                        while (set.LeftTaker.GetOrderBook(set.RunRecord.Text2) == null)
                            await Task.Delay(1000);
                        Message("Orderbooks ok.");
                    }

                    //Position opened.
                    if (set.IsPositionOpened)
                    {
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

                            if (set.IsPositionOpened)
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
                        }
                        else
                        {
                            set.RunRecord.Message = set.FaultMessage;
                            set.RunRecord.Status = (int)RunStatus.Fault;
                        }
                    }
                 
                    if (!subscribedOrderbookApis.Contains(set.LeftTaker))
                        set.LeftTaker.OnOrderBookChanged += Exchange_OnOrderBookChanged;
                    if (!subscribedOrderbookApis.Contains(set.RightTaker))
                        set.RightTaker.OnOrderBookChanged += Exchange_OnOrderBookChanged;

                    tmpList.Add(set);
                }
                await db.SaveChangesAsync();

                foreach (TakerTakerSet s in tmpList)
                    takerTakerSets.Add(s.RunRecord.Id, s);
                tmpList.Clear();
            }
        }

        /// <summary>
        /// Prepare the orders for the MakerTakerSet
        /// </summary>
        /// <param name="set"></param>
        /// <returns>True if successful, the set's orders will be populated. False otherwise.</returns>
        private async Task<bool> PrepareOrders(TakerTakerSet set)
        {
            set.MakerClosingOrderLeft = null;
            set.MakerClosingOrderRight = null;
            string msg = string.Empty;
            bool success = false;

            if (!set.IsPositionOpened)
                set.FaultMessage = "Position is not opened.";
            else if (set.IsPositionClosed)
                set.FaultMessage = "Position is not closed.";
            else if (set.IsAbnormal)
                set.FaultMessage = "The set is abnormal. One legged hedge?";

            /*--------------------Position opened-------------------*/
            else if (!set.IsPositionClosed)
            {
                //////
                msg = "Closing Makers:";
                bool leftOrderOk = false, rightOrderOk = false;
                if (set.RunRecord.Num11.GetValueOrDefault() == 0.0M && set.RunRecord.Num12.GetValueOrDefault() == 0.0M) //closing makers not executed
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
                            IResult res = await OrderManager.Instance.CancelOrderAsync(set.RightTaker, set.RunRecord.Order5);
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
                                IResult res = await OrderManager.Instance.CancelOrderAsync(set.LeftTaker, set.RunRecord.Order3);
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
            }

            if (success)
            {
                Message("Orders prepared:\n" + msg);
            }
            else
            {
                set.FaultMessage = "Order preparation failed for run ID " + set.RunRecord.Id.ToString() + " . " + set.FaultMessage;
                Message(set.FaultMessage);
            }
            return success;
        }

        private async Task<bool> RetrieveLeftClosingOrders(TakerTakerSet set)
        {
            IApi makerApi = null, takerApi = null;
            string makerInstrument, takerInstrument;
            decimal hurdleRate = 0.0M;
            PricingScheme ps;
            makerApi = set.LeftTaker;
            takerApi = set.RightTaker;
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
                         0.0M,
                         0.0M,
                         ps,
                         set.RunRecord.OpId);

            return set.MakerClosingOrderLeft != null && set.MakerClosingOrderLeft.OrderStatus != Api.OrderStatus.Canceled;
        }

        private async Task<bool> RetrieveRightClosingOrders(TakerTakerSet set)
        {
            IApi makerApi = null, takerApi = null;
            string makerInstrument, takerInstrument;
            decimal hurdleRate = 0.0M;
            PricingScheme ps;
            makerApi = set.RightTaker;
            takerApi = set.LeftTaker;
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
                         0.0M,
                         0.0M,
                         ps,
                         set.RunRecord.OpId);

            return set.MakerClosingOrderRight != null && set.MakerClosingOrderRight.OrderStatus != Api.OrderStatus.Canceled;
        }

        private async Task<bool> PlaceRightClosingMakerOrder(TakerTakerSet set)
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

            leftMakerApi = set.LeftTaker;
            leftMakerInstrument = set.RunRecord.Text2;
            rightMakerApi = set.RightTaker;
            rightMakerInstrument = set.RunRecord.Text4;
            rightMakerSide = Api.Tools.ToSide(set.RunRecord.Text5);
            rightMakerOrderType = Api.OrderType.Limit;
            rightMakerSize = set.RunRecord.Num8.HasValue ? set.RunRecord.Num8.Value : set.RunRecord.Num1.GetValueOrDefault();
            if (rightMakerSide == Api.Side.Sell)
            {
                ps = PricingScheme.PercentageOfNewPrice;
            }
            else
            {
                ps = PricingScheme.PercentageOfTarget;
            }
            hurdleRate = set.RunRecord.Num3.GetValueOrDefault();   //closing hurdle rate

            ushort retry = 0;
            while ((set.MakerClosingOrderRight == null || set.MakerClosingOrderRight.IsCancelled) && MAX_TAKER_RETRY >= retry)
            {
                set.MakerClosingOrderRight = await FloatingPriceOrder3.Create(rightMakerApi,
                rightMakerInstrument,
                rightMakerSide,
                rightMakerOrderType,
                rightMakerSize,
                0.0M,
                leftMakerInstrument,
                leftMakerApi,
                hurdleRate,
                hurdleRate,
                DateTime.MaxValue,
                0.0M,
                0.0M,
                ps,
                set.RunRecord.OpId);

                if ((set.MakerClosingOrderRight == null || set.MakerClosingOrderRight.IsCancelled))
                {
                    retry++;
                    if (MAX_TAKER_RETRY >= retry)
                    {
                        await Task.Delay(MAX_TAKER_RETRY_INTERVAL_SECONDS * 1000); //wait X minutes;
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

        private async Task<bool> PlaceLeftClosingMakerOrder(TakerTakerSet set)
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
            rightMakerApi = set.RightTaker;
            leftMakerApi = set.LeftTaker;
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

            hurdleRate = set.RunRecord.Num3.GetValueOrDefault();   //closing hurdle rate

            ushort retry = 0;
            while ((set.MakerClosingOrderLeft == null || set.MakerClosingOrderLeft.IsCancelled) && MAX_TAKER_RETRY >= retry)
            {
                set.MakerClosingOrderLeft = await FloatingPriceOrder3.Create(leftMakerApi,
                leftMakerInstrument,
                leftMakerSide,
                leftMakerOrderType,
                leftMakerSize,
                0.0M,
                rightMakerInstrument,
                rightMakerApi,
                hurdleRate,
                hurdleRate,
                DateTime.MaxValue,
                0.0M,
                0.0M,
                ps,
                set.RunRecord.OpId);

                if (set.MakerClosingOrderLeft == null || set.MakerClosingOrderLeft.IsCancelled)
                {
                    retry++;
                    if (MAX_TAKER_RETRY >= retry)
                    {
                        await Task.Delay(MAX_TAKER_RETRY_INTERVAL_SECONDS * 1000); //wait X minutes;
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

        private async void TestOrderbookSignal2(IOrderBook orderBook)
        {
            List<TakerTakerSet> reruns = new List<TakerTakerSet>();
            List<TakerTakerSet> closeures = new List<TakerTakerSet>();
            
            if (isStarted)
            {
                using (var db = new Tuna())
                {
                    try
                    {
                        foreach (var i in takerTakerSets)
                        {
                            TakerTakerSet set = i.Value;
                            var o = db.Entry(set.RunRecord);

                            if ((orderBook.Symbol == set.RunRecord.Text2 && orderBook == set.LeftTaker.GetOrderBook(orderBook.Symbol)) ||
                                (orderBook.Symbol == set.RunRecord.Text4 && orderBook == set.RightTaker.GetOrderBook(orderBook.Symbol)))
                            {
                                if (await set.Process())
                                {
                                    if (set.IsPendingClosure)
                                    {
                                        closeures.Add(set);
                                    }
                                    else if (set.IsPendingRerun)
                                    {
                                        reruns.Add(set);
                                    }
                                    o.State = System.Data.Entity.EntityState.Modified;
                                }
                            }

                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        return;
                    }
                    db.SaveChanges();
                }
            }

            foreach (TakerTakerSet set in reruns)
            {
                Rerun(set);
            }
            foreach (TakerTakerSet set in closeures)
            {
                AfterPositionClosed(set);
            }
        }

        /// <summary>
        /// Rerun current set
        /// </summary>
        /// <param name="set"></param>
        private async void Rerun(TakerTakerSet set)
        {
            if (set.IsPositionOpened && !set.IsPositionClosed)
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
                 
                    //takerTakerSets.Remove(set.RunRecord.Id);
                    //takerTakerSets.Add(set.RunRecord.Id, set);

                    using (var db = new Tuna())
                    {
                        var run = db.Entry<Run>(set.RunRecord);
                        run.Entity.Status = (int)RunStatus.Running;
                        run.State = System.Data.Entity.EntityState.Modified;
                        db.SaveChanges();
                    }
                }
            }
        }

        private async Task<IResult> ClosePosition(TakerTakerSet set)
        {
            /* case 1: Taker orders are placed on both sides, execute 2 taker orders to close the position
             * case 2: One legged taker order..??
             */

            Result result = null;
            string message = string.Empty;
            Run record = set.RunRecord;
            decimal topPrice = 0.0M, totalPrice = 0.0M, avgPrice = 0.0M;

            if (set.IsPositionOpened) //case 1
            {
                IResult res;
                if (record.Text5 == "Sell")
                {
                    IOrderBook obLeft = set.LeftTaker.GetOrderBook(record.Text2);
                    if (!Helper.GetTopPrice(obLeft, Api.Side.Sell, record.Num7.Value, out topPrice, out totalPrice, out avgPrice))
                        return new Result(false, "Not enough quantities on orderbook or quoting has failed " + set.LeftTaker.ToString() + " " + record.Text2);

                    res = await set.ExecuteTakerOrder(set.LeftTaker, record.Text2, Api.Side.Buy, record.Num7.Value, topPrice);
                    if (res.Success)
                    {
                        record.Num11 = ((IOrder)res).NetAmount.GetValueOrDefault();
                        record.Num13 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                        record.Order6 = ((IOrder)res).OrderID;

                        IOrderBook obRight = set.RightTaker.GetOrderBook(record.Text4);
                        if (!Helper.GetTopPrice(obRight, Api.Side.Buy, record.Num8.Value, out topPrice, out totalPrice, out avgPrice))
                            return new Result(false, "Not enough quantities on orderbook or quoting has failed " + set.LeftTaker.ToString() + " " + record.Text2);

                        res = await set.ExecuteTakerOrder(set.RightTaker, record.Text4, Api.Side.Sell, record.Num8.Value, topPrice); //right taker order
                        if (res.Success)
                        {
                            record.Num12 = ((IOrder)res).NetAmount.GetValueOrDefault();
                            record.Num14 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                            record.Order4 = ((IOrder)res).OrderID;

                            //I must have the confirmed amount here, if not, get it.
                            if (set.RunRecord.Num12 == 0.0M)
                            {
                                IOrder[] takerExecuted = await set.RightTaker.GetOrdersAsync(set.RunRecord.Order4);
                                if (takerExecuted.Length > 0)
                                    set.RunRecord.Num12 = takerExecuted[0].NetAmount;
                            }

                            //I must have the confirmed amount here, if not, get it.
                            if (set.RunRecord.Num11 == 0.0M)
                            {
                                IOrder[] takerExecuted = await set.LeftTaker.GetOrdersAsync(set.RunRecord.Order6);
                                if (takerExecuted.Length > 0)
                                    set.RunRecord.Num11 = takerExecuted[0].NetAmount;
                            }
                        }
                    }
                    result = new Result(res.Success, message + res.Message);
                }
                else
                {
                    IOrderBook obLeft = set.LeftTaker.GetOrderBook(record.Text2);
                    if (!Helper.GetTopPrice(obLeft, Api.Side.Buy, record.Num7.Value, out topPrice, out totalPrice, out avgPrice))
                        return new Result(false, "Not enough quantities on orderbook or quoting has failed " + set.LeftTaker.ToString() + " " + record.Text2);

                    res = await set.ExecuteTakerOrder(set.LeftTaker, record.Text2, Api.Side.Sell, record.Num7.Value, topPrice); //left taker
                    if (res.Success)
                    {
                        record.Num11 = ((IOrder)res).NetAmount.GetValueOrDefault();
                        record.Num13 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                        record.Order6 = ((IOrder)res).OrderID;

                        IOrderBook obRight = set.RightTaker.GetOrderBook(record.Text4);
                        if (!Helper.GetTopPrice(obRight, Api.Side.Sell, record.Num8.Value, out topPrice, out totalPrice, out avgPrice))
                            return new Result(false, "Not enough quantities on orderbook or quoting has failed " + set.LeftTaker.ToString() + " " + record.Text2);

                        res = await set.ExecuteTakerOrder(set.RightTaker, record.Text4, Api.Side.Buy, record.Num8.Value, topPrice); //right taker order
                        if (res.Success)
                        {
                            record.Num12 = ((IOrder)res).NetAmount.GetValueOrDefault();
                            record.Num14 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                            record.Order4 = ((IOrder)res).OrderID;

                            //I must have the confirmed amount here, if not, get it.
                            if (set.RunRecord.Num12 == 0.0M)
                            {
                                IOrder[] takerExecuted = await set.RightTaker.GetOrdersAsync(set.RunRecord.Order4);
                                if (takerExecuted.Length > 0)
                                    set.RunRecord.Num12 = takerExecuted[0].NetAmount;
                            }

                            //I must have the confirmed amount here, if not, get it.
                            if (set.RunRecord.Num11 == 0.0M)
                            {
                                IOrder[] takerExecuted = await set.LeftTaker.GetOrdersAsync(set.RunRecord.Order6);
                                if (takerExecuted.Length > 0)
                                    set.RunRecord.Num11 = takerExecuted[0].NetAmount;
                            }
                        }
                    }
                    result = new Result(res.Success, message + res.Message);
                }



                //Cancel the maker orders
                if (result.Success)
                {
                    IResult resCancel;

                    if (set.MakerClosingOrderLeft != null)
                    {

                        set.MakerClosingOrderLeft.ClearEventSubscribers();
                        resCancel = await OrderManager.Instance.CancelOrderAsync(set.LeftTaker, set.MakerClosingOrderLeft.OrderID);
                        if (!resCancel.Success)
                        {
                            set.RunRecord.Message = "Failed to cancel left closing maker order." + set.MakerClosingOrderLeft.OrderID;
                            set.RunRecord.Status = (int)RunStatus.CompletedWithError;
                        }
                    }
                    if (set.MakerClosingOrderRight != null)
                    {
                        set.MakerClosingOrderRight.ClearEventSubscribers();
                        resCancel = await OrderManager.Instance.CancelOrderAsync(set.RightTaker, set.MakerClosingOrderRight.OrderID);
                        if (!resCancel.Success)
                        {
                            set.RunRecord.Message = "Failed to cancel right closing maker order. " + set.MakerClosingOrderRight.OrderID;
                            set.RunRecord.Status = (int)RunStatus.CompletedWithError;
                        }
                    }
                }

            }
            else if (!string.IsNullOrEmpty(record.Order1) && string.IsNullOrEmpty(record.Order2)) //case 2
            {
                string msg = "Run " + record.Id.ToString() + " has an opening maker leg only, please manually resolve this and reset the run.";
                result = new Result(false, msg);
                record.Message = msg;
            }

            return result;
        }

        private void AfterPositionClosed(TakerTakerSet set)
        {
            Message("Completed. Run ID " + set.RunRecord.Id.ToString());
            set.LeftTaker.OnOrderBookChanged -= Exchange_OnOrderBookChanged;
            set.RightTaker.OnOrderBookChanged -= Exchange_OnOrderBookChanged;

            //set.OpRecord.LastCompleted = DateTime.UtcNow;
            set.RunRecord.Completed = DateTime.UtcNow;
            set.RunRecord.Status = (int)RunStatus.Completed;
            using (var db = new Tuna())
            {
                //var o = db.Entry<OP>(set.OpRecord);
                var r = db.Entry<Run>(set.RunRecord);
                //o.State = System.Data.Entity.EntityState.Modified;
                r.State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
            }
            takerTakerSets.Remove(set.RunRecord.Id);

            bool doRestart = true;
            decimal hours = set.RunRecord.Num4.GetValueOrDefault();
            DateTime? settlement = set.RunRecord.Date1;
            if (settlement.HasValue && settlement.Value.AddHours(-(double)hours) <= DateTime.UtcNow)
                doRestart = false;

            if (doRestart)
                Run(set.OpRecord.Id);
        }

        private void Set_OnMessage(object sender, string e)
        {
            Message(e);
        }

        /// <summary>
        /// Run fault. Stops any order in the set and mark the run as fault so it cannot restart until user intervention
        /// </summary>
        /// <param name="set"></param>
        /// <param name="faultMessage"></param>
        private void RunFault(TakerTakerSet set, string faultMessage, bool doRerun = true)
        {
            if (set.MakerClosingOrderLeft != null) set.MakerClosingOrderLeft.Stop("Taker-Taker fault " + faultMessage);
            if (set.MakerClosingOrderRight != null) set.MakerClosingOrderRight.Stop("Taker-Taker fault " + faultMessage);

            set.RunRecord.Message = faultMessage;
            set.RunRecord.Status = (int)RunStatus.Fault;
            using (var db = new Tuna())
            {
                var o = db.Entry<Run>(set.RunRecord);
                o.State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
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
        private TakerTakerSet GetWorkingSet(IApi api, string orderId)
        {
            foreach (var set in takerTakerSets)
            {
                if ((set.Value.MakerClosingOrderLeft != null && (set.Value.MakerClosingOrderLeft.OwnerApi == api && set.Value.MakerClosingOrderLeft.OrderID == orderId)) ||
                    (set.Value.MakerClosingOrderRight != null && (set.Value.MakerClosingOrderRight.OwnerApi == api && set.Value.MakerClosingOrderRight.OrderID == orderId)))
                {
                    return set.Value;
                }
            }
            return null;
        }

        #endregion

        #region Events

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

        private void Exchange_OnOrderBookChanged(object sender, Api.OrderBookChangedEventArgs e)
        {
            TestOrderbookSignal2(e.OrderBook);
        }

        private void Exchange_OnConnectionClosed(object sender, EventArgs e)
        {
            Message("Paused due to disconnection.");
        }

        private void MakerOrder_OnOrderAmended(object sender, EventArgs e)
        {
            FloatingPriceOrder3 fpx = (FloatingPriceOrder3)sender;
            if (!string.IsNullOrEmpty(fpx.OldOrderId) && fpx.OldOrderId != fpx.OrderID)
            {
                TakerTakerSet set = GetWorkingSet(fpx.OwnerApi, fpx.OrderID);
                if (set != null)
                {
                    if (set.MakerClosingOrderLeft != null && set.MakerClosingOrderLeft.OrderID == fpx.OrderID)
                    {
                        set.RunRecord.Order3 = fpx.OrderID;
                    }
                    else if (set.MakerClosingOrderRight != null && set.MakerClosingOrderRight.OrderID == fpx.OrderID)
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

        private async void MakerClosingOrderRight_OnOrderCancelled(object sender, EventArgs e)
        {
            FloatingPriceOrder3 order = (FloatingPriceOrder3)sender;
            order.Stop("Right closing maker order cancelled");
            string msg;
            TakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set == null) return;
            msg = "Right Closing Maker was cancelled: " + order.ToString(); Message(msg);
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

                    msg = "Right Closing Maker placed: " + set.MakerClosingOrderRight.ToString(); Message(msg);
                    r.Entity.Order5 = set.MakerClosingOrderRight.OrderID;
                }
                else
                {
                    //cancel the left maker                    
                    if (set.MakerClosingOrderLeft != null)
                    {
                        IResult res = await OrderManager.Instance.CancelOrderAsync(set.LeftTaker, set.MakerClosingOrderLeft.OrderID);
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
            TakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set != null)
            {
                //TODO RunFault(set, e, false);
            }
            else
            {
                throw new NotImplementedException(e);
            }
        }

        private async void MakerClosingOrderRight_OnOrderHit(object sender, EventArgs e)
        {
            TakerTakerSet set;
            FloatingPriceOrder3 mtOrder = (FloatingPriceOrder3)sender;
            decimal qtyFilled = mtOrder.NetAmount.GetValueOrDefault();
            string msg = string.Empty;
            if (qtyFilled > 0)
            {
                set = GetWorkingSet(mtOrder.OwnerApi, mtOrder.OrderID);
                if (set == null)
                    return;

                //stop the left order
                set.MakerClosingOrderLeft.ClearEventSubscribers();
                set.MakerClosingOrderLeft.Stop("Right closing maker order hit");
                //stop the right order
                set.MakerClosingOrderRight.ClearEventSubscribers();

                msg = "Closing maker (right) hit: " + mtOrder.ToString(); Message(msg); Helper.PlaySfx(Sfx.Alert);
                set.RunRecord.Num12 = qtyFilled; //set closing maker executed qty    
                set.RunRecord.Num14 = mtOrder.AveragePrice; //set closing maker executed px
                set.RunRecord.Message = msg;

                if (mtOrder.TargetAPI.ToString() == "BitMEX") //TODO: remove this fix
                    qtyFilled = Math.Ceiling(set.RunRecord.Num12.Value);
                //execute a taker order on the left
                IResult rst;
                if (mtOrder.Side == Api.Side.Sell) //was an closing sell
                {
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Buy, qtyFilled, mtOrder.TargetPrice); //left taker
                }
                else //was an closing buy
                {
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Sell, qtyFilled, mtOrder.TargetPrice); //left taker
                }
                if (rst.Success)
                {
                    set.RunRecord.Order6 = ((IOrder)rst).OrderID;
                    set.RunRecord.Num11 = ((IOrder)rst).NetAmount;
                    set.RunRecord.Num13 = ((IOrder)rst).AveragePrice.GetValueOrDefault() == 0.0M ? mtOrder.TargetPrice : ((IOrder)rst).AveragePrice.Value;

                    //I must have the confirmed amount here, if not, get it.
                    if (set.RunRecord.Num11 == 0.0M)
                    {
                        IOrder[] takerExecuted = await mtOrder.TargetAPI.GetOrdersAsync(set.RunRecord.Order6);
                        if (takerExecuted.Length > 0)
                            set.RunRecord.Num11 = takerExecuted[0].NetAmount;
                    }

                    Message("Closing taker placed: " + mtOrder.ToString());

                    await OrderManager.Instance.CancelOrderAsync(set.LeftTaker, set.MakerClosingOrderLeft.OrderID);
                    AfterPositionClosed(set);
                }
                else
                {
                    msg = "Fail to place Closing taker " + mtOrder.TargetAPI.ToString() + " " + mtOrder.TargetSymbol + ". " + rst.Message;
                    Message(msg);
                    RunFault(set, msg);
                }
            }
        }

        private async void MakerClosingOrderLeft_OnOrderCancelled(object sender, EventArgs e)
        {
            FloatingPriceOrder3 order = (FloatingPriceOrder3)sender;
            order.Stop("Left closing maker order cancelled");
            string msg;
            TakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set == null) return;

            msg = "Left Closing Maker was cancelled: " + order.ToString(); Message(msg);
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

                    msg = "Left Closing Maker placed: " + set.MakerClosingOrderLeft.ToString(); Message(msg);
                    r.Entity.Order3 = set.MakerClosingOrderLeft.OrderID;
                }
                else
                {
                    //cancel the right maker                    
                    if (set.MakerClosingOrderRight != null)
                    {
                        IResult res = await OrderManager.Instance.CancelOrderAsync(set.LeftTaker, set.MakerClosingOrderRight.OrderID);
                        if (res.Success)
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
            TakerTakerSet set = GetWorkingSet(order.OwnerApi, order.OrderID);
            if (set != null)
            {
                //TODO  RunFault(set, e, false);
            }
            else
            {
                throw new NotImplementedException(e);
            }
        }

        private async void MakerClosingOrderLeft_OnOrderHit(object sender, EventArgs e)
        {
            TakerTakerSet set;
            FloatingPriceOrder3 mtOrder = (FloatingPriceOrder3)sender;
            decimal qtyFilled = mtOrder.Amount.GetValueOrDefault();
            string msg = string.Empty;
            if (qtyFilled > 0)
            {
                set = GetWorkingSet(mtOrder.OwnerApi, mtOrder.OrderID);
                if (set == null)
                    return;

                //stop the right order
                set.MakerClosingOrderRight.ClearEventSubscribers();
                set.MakerClosingOrderRight.Stop("Left closing maker order hit");
                //stop the left order
                set.MakerClosingOrderLeft.ClearEventSubscribers();

                msg = "Closing maker (left) hit: " + mtOrder.ToString(); Message(msg); Helper.PlaySfx(Sfx.Alert);
                set.RunRecord.Num11 = qtyFilled; //set closing maker executed qty    
                set.RunRecord.Num13 = mtOrder.AveragePrice; //set closing maker executed px
                set.RunRecord.Message = msg;

                if (mtOrder.TargetAPI.ToString() == "BitMEX") //TODO: remove this fix
                    qtyFilled = Math.Ceiling(set.RunRecord.Num11.Value);
                //execute a taker order on the right
                IResult rst;
                if (mtOrder.Side == Api.Side.Sell) //was an closing sell
                {
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Buy, qtyFilled, mtOrder.TargetPrice); //taker right
                }
                else //was an closing buy
                {
                    rst = await set.ExecuteTakerOrder(mtOrder.TargetAPI, mtOrder.TargetSymbol, Api.Side.Sell, qtyFilled, mtOrder.TargetPrice); //taker right
                }
                if (rst.Success)
                {
                    set.RunRecord.Order4 = ((IOrder)rst).OrderID;
                    set.RunRecord.Num12 = ((IOrder)rst).NetAmount;
                    set.RunRecord.Num14 = ((IOrder)rst).AveragePrice.GetValueOrDefault() == 0.0M ? mtOrder.TargetPrice : ((IOrder)rst).AveragePrice.Value;

                    //I must have the confirmed amount here, if not, get it.
                    if (set.RunRecord.Num12 == 0.0M)
                    {
                        IOrder[] takerExecuted = await mtOrder.TargetAPI.GetOrdersAsync(set.RunRecord.Order4);
                        if (takerExecuted.Length > 0)
                            set.RunRecord.Num12 = takerExecuted[0].NetAmount;
                    }

                    Message("Closing taker placed: " + mtOrder.ToString());

                    //cancel the right order
                    await OrderManager.Instance.CancelOrderAsync(set.RightTaker, set.MakerClosingOrderRight.OrderID);
                    AfterPositionClosed(set);
                }
                else
                {
                    msg = "Fail to place Closing taker " + mtOrder.TargetAPI.ToString() + " " + mtOrder.TargetSymbol + ". " + rst.Message;
                    Message(msg);
                    RunFault(set, msg);
                }
            }
        }

        private async void SettlementTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Common.Timer timer = (Common.Timer)sender;
            timer.Stop();
            TakerTakerSet set = (TakerTakerSet)timer.Tag;

            //check if its settlement time
            int Minutes_Before_Settlement = set.RunRecord.Num5.HasValue ? (int)set.RunRecord.Num5.GetValueOrDefault() : 2;
            if (DateTime.UtcNow >= set.RunRecord.Date1.Value.AddMinutes(-Minutes_Before_Settlement - 1)) //settlement time hit
            {
                //set.MakerOpeningOrder.Stop();
                IResult res = await ClosePosition(set);
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
                    OnMessage?.Invoke(this, res.Message);
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

        #endregion

        private class TakerTakerSet
        {
            public FloatingPriceOrder3 MakerClosingOrderLeft;
            public FloatingPriceOrder3 MakerClosingOrderRight;
            public OP OpRecord;
            public Run RunRecord;
            public IApi LeftTaker;
            public IApi RightTaker;
            public decimal LeftTakerFeeRate;
            public decimal RightTakerFeeRate;
            public Common.Timer TimerSettlement;    //time until settlement
            public Common.Timer TimerReset;
            public bool IsPositionOpened
            {
                get
                {
                    return !string.IsNullOrEmpty(RunRecord.Order1)
                        && !string.IsNullOrEmpty(RunRecord.Order2)
                        && RunRecord.Num7.HasValue
                        && RunRecord.Num8.HasValue;
                }
            }
            public bool IsPositionClosed
            {
                get
                {
                    return (!string.IsNullOrEmpty(RunRecord.Order4) && RunRecord.Num12.HasValue)
                        || (!string.IsNullOrEmpty(RunRecord.Order6) && RunRecord.Num11.HasValue);
                }
            }
            public bool IsAbnormal
            {
                get
                {
                    return ((string.IsNullOrEmpty(RunRecord.Order1) && !string.IsNullOrEmpty(RunRecord.Order2))
                        || (string.IsNullOrEmpty(RunRecord.Order2) && !string.IsNullOrEmpty(RunRecord.Order1))
                        || (string.IsNullOrEmpty(RunRecord.Order3) && !string.IsNullOrEmpty(RunRecord.Order5))
                        || (string.IsNullOrEmpty(RunRecord.Order5) && !string.IsNullOrEmpty(RunRecord.Order3))
                        || (RunRecord.Num11.GetValueOrDefault() != 0.0M && RunRecord.Num12.GetValueOrDefault() == 0.0M)
                        || (RunRecord.Num12.GetValueOrDefault() != 0.0M && RunRecord.Num11.GetValueOrDefault() == 0.0M));
                }
            }
            //public bool IsUpdating;
            public string FaultMessage;
            public event EventHandler<string> OnMessage;
            private Object syncLock = new Object();

            bool _IsPendingRerun = false;
            public bool IsPendingRerun
            {
                get
                {
                    return _IsPendingRerun;
                }
                private set
                {
                    _IsPendingRerun = value;
                }
            }

            bool _IsPendingClosure = false;
            public bool IsPendingClosure
            {
                get
                {
                    return _IsPendingClosure;
                }
                private set
                {
                    _IsPendingClosure = value;
                }
            }

            private void Message(string message)
            {
                OnMessage?.Invoke(this, "Set " + RunRecord.Id.ToString() + " " + message);
            }

            internal Task<IResult> ExecuteTakerOrder(IApi api, string symbol, Api.Side side, decimal quoteAmount, decimal quotePrice)
            {
                if (side == Api.Side.Buy)
                    return OrderManager.Instance.PostBuyOrderAsync(api, symbol, Api.OrderType.Limit, quoteAmount, quotePrice);
                else if (side == Api.Side.Sell)
                    return OrderManager.Instance.PostSellOrderAsync(api, symbol, Api.OrderType.Limit, quoteAmount, quotePrice);
                else
                    throw new ArgumentOutOfRangeException("side", side.ToString(), "Side must be buy or sell.");
            }

            /// <summary>
            /// Test the orderbook and process takers orders.
            /// </summary>
            /// <returns>True if set was processed, false otherwise.</returns>
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
                    decimal takerLongQtyToQuote, takerShortQtyToQuote;

                    #region Set Short Long
                    if (!IsPositionOpened)
                    {
                        //opening setting
                        if (RunRecord.Text5.ToLower() == "sell")
                        {
                            takerShort = LeftTaker;
                            takerShortSymbol = RunRecord.Text2;
                            takerShortFeeRate = LeftTakerFeeRate;
                            takerShortQtyToQuote = RunRecord.Num1.GetValueOrDefault();

                            takerLong = RightTaker;
                            takerLongSymbol = RunRecord.Text4;
                            takerLongFeeRate = RightTakerFeeRate;
                            takerLongQtyToQuote = RunRecord.Num1.GetValueOrDefault();
                        }
                        else
                        {
                            takerShort = RightTaker;
                            takerShortSymbol = RunRecord.Text4;
                            takerShortFeeRate = RightTakerFeeRate;
                            takerShortQtyToQuote = RunRecord.Num1.GetValueOrDefault();

                            takerLong = LeftTaker;
                            takerLongSymbol = RunRecord.Text2;
                            takerLongFeeRate = LeftTakerFeeRate;
                            takerLongQtyToQuote = RunRecord.Num1.GetValueOrDefault();
                        }
                    }
                    else if (!IsPositionClosed)
                    {
                        //closing maker settings
                        if (RunRecord.Text5.ToLower() == "sell")
                        { //Opening was sell on the left, for closing, buy on the left, sell on the right, 
                            takerLong = LeftTaker;
                            takerLongSymbol = RunRecord.Text2;
                            takerLongFeeRate = LeftTakerFeeRate;
                            takerLongQtyToQuote = RunRecord.Num7.GetValueOrDefault();

                            takerShort = RightTaker;
                            takerShortSymbol = RunRecord.Text4;
                            takerShortFeeRate = RightTakerFeeRate;
                            takerShortQtyToQuote = RunRecord.Num8.GetValueOrDefault();
                        }
                        else
                        {//Opening was buy on the left, for closing, sell on the left, buy on the right, 
                            takerShort = LeftTaker;
                            takerShortSymbol = RunRecord.Text2;
                            takerShortFeeRate = LeftTakerFeeRate;
                            takerShortQtyToQuote = RunRecord.Num7.GetValueOrDefault();

                            takerLong = RightTaker;
                            takerLongSymbol = RunRecord.Text4;
                            takerLongFeeRate = RightTakerFeeRate;
                            takerLongQtyToQuote = RunRecord.Num8.GetValueOrDefault();
                        }

                        //make case for polo
                        if (takerLong.ToString() == "Poloniex")
                        {
                            if (takerLongFeeRate < 1.0M)
                                takerLongQtyToQuote = takerLongQtyToQuote / (1.0M - takerLongFeeRate);
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

                    decimal margin = 0.0M;
                    bool isMarginOk = false;
                    string msg;
                    if (!IsPositionOpened) //for opening taker
                    {
                        marginApparent = bidPriceTotal - askPriceTotal - shortTakerFee - longTakerFee;
                        if (askPriceTotal > 0.0M)
                            margin = marginApparent / askPriceTotal;
                        isMarginOk = RunRecord.Num2.GetValueOrDefault() >= 0.0M && margin >= RunRecord.Num2.GetValueOrDefault();
                        msg = "Opening ";
                    }
                    else //for closing taker
                    {
                        marginApparent = askPriceTotal - bidPriceTotal - shortTakerFee - longTakerFee;
                        if (bidPriceTotal > 0.0M)
                            margin = marginApparent / bidPriceTotal;
                        isMarginOk = RunRecord.Num6.GetValueOrDefault() <= 0.0M && margin >= RunRecord.Num6.GetValueOrDefault();
                        msg = "Closing ";
                    }
                    
                    if (isMarginOk)
                    {
                        msg += "Margin Hit:" + marginApparent.ToStringNormalized() +" " + (margin * 100).ToStringNormalized() + "%" +
                                            " Short:" + takerShort.ToString() + " " + takerShortSymbol + " " + bidPriceTotal.ToStringNormalized() +
                                            " Long:" + takerLong.ToString() + " " + takerLongSymbol + " " + askPriceTotal.ToStringNormalized();
                        Message(msg);
                        RunRecord.Message = msg;

                        //Execute the sell order
                        msg = "Placing Sell order: " + takerShort.ToString() + " " + takerShortSymbol + " Px:" + topBidPrice.ToStringNormalized() + " Amt:" + takerShortQtyToQuote.ToStringNormalized();
                        Message(msg);

                        IResult sellOrder = ExecuteTakerOrder(takerShort, takerShortSymbol, Api.Side.Sell, takerShortQtyToQuote, topBidPrice).Result;
                        if (sellOrder.Success)
                        {
                            processed = true;
                            if (!IsPositionOpened)
                            { //opening sell
                                if (RunRecord.Text5.ToLower() == "sell") //the sell order is the left
                                {
                                    IOrder ord = (IOrder)sellOrder;
                                    RunRecord.Order1 = ord.OrderID;
                                    RunRecord.Num7 = ord.NetAmount;
                                    RunRecord.Num9 = ord.AveragePrice.GetValueOrDefault() == 0.0M ? ord.Price : ord.AveragePrice.Value;
                                }
                                else //the sell order is the right
                                {
                                    IOrder ord = (IOrder)sellOrder;
                                    RunRecord.Order2 = ord.OrderID;
                                    RunRecord.Num8 = ord.NetAmount;
                                    RunRecord.Num10 = ord.AveragePrice.GetValueOrDefault() == 0.0M ? ord.Price : ord.AveragePrice.Value;
                                }
                            }
                            else
                            { //closing sell                                
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
                            }
                            //Execute buy order
                            msg = "Placing Buy order: " + takerLong.ToString() + " " + takerLongSymbol + " Px:" + topAskPrice.ToStringNormalized() + " Amt:" + takerLongQtyToQuote.ToStringNormalized();
                            Message(msg);
                            IResult buyOrder = ExecuteTakerOrder(takerLong, takerLongSymbol, Api.Side.Buy, takerLongQtyToQuote, topAskPrice).Result;
                            if (buyOrder.Success)
                            {
                                if (!IsPositionOpened)
                                { //opening buy
                                    if (RunRecord.Text5.ToLower() == "buy") //the buy order is the left
                                    {
                                        IOrder ord = (IOrder)buyOrder;
                                        RunRecord.Order1 = ord.OrderID;
                                        RunRecord.Num7 = ord.NetAmount;
                                        RunRecord.Num9 = ord.AveragePrice.GetValueOrDefault() == 0.0M ? ord.Price : ord.AveragePrice.Value;
                                    }
                                    else //the buy order is the right
                                    {
                                        IOrder ord = (IOrder)buyOrder;
                                        RunRecord.Order2 = ord.OrderID;
                                        RunRecord.Num8 = ord.NetAmount;
                                        RunRecord.Num10 = ord.AveragePrice.GetValueOrDefault() == 0.0M ? ord.Price : ord.AveragePrice.Value;
                                    }

                                    IsPendingRerun = true; //position is now open
                                }
                                else
                                { //closing buy
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

                                    IsPendingClosure = true; ; //position closed
                                }
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
                    }//end margin match 
                }//end sync


                //Cancel the maker orders
                if (IsPendingClosure)
                {
                    IResult resCancel;
                    if (MakerClosingOrderLeft != null)
                    {
                        MakerClosingOrderLeft.ClearEventSubscribers();
                        MakerClosingOrderRight.ClearEventSubscribers();

                        resCancel = await OrderManager.Instance.CancelOrderAsync(LeftTaker, MakerClosingOrderLeft.OrderID);
                        if (!resCancel.Success)
                        {
                            RunRecord.Message = "Failed to cancel left closing maker order." + MakerClosingOrderLeft.OrderID;
                            RunRecord.Status = (int)RunStatus.CompletedWithError;
                        }
                    }
                    if (MakerClosingOrderRight != null)
                    {
                        resCancel = await OrderManager.Instance.CancelOrderAsync(RightTaker, MakerClosingOrderRight.OrderID);
                        if (!resCancel.Success)
                        {
                            RunRecord.Message = "Failed to cancel right closing maker order. " + MakerClosingOrderRight.OrderID;
                            RunRecord.Status = (int)RunStatus.CompletedWithError;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(RunRecord.Order4) || !string.IsNullOrEmpty(RunRecord.Order6))
                { //it was closing takers
                    //Confirm both left and right closing qty. If not, get it.                                           
                    if (RunRecord.Num12 == 0.0M)
                    {
                        IOrder[] takerExecuted = await takerShort.GetOrdersAsync(RunRecord.Order4);
                        if (takerExecuted.Length > 0)
                        {
                            RunRecord.Num12 = takerExecuted[0].NetAmount;
                            RunRecord.Num14 = takerExecuted[0].AveragePrice;
                        }
                    }
                    if (RunRecord.Num11 == 0.0M)
                    {
                        IOrder[] takerExecuted = await takerShort.GetOrdersAsync(RunRecord.Order6);
                        if (takerExecuted.Length > 0)
                        {
                            RunRecord.Num11 = takerExecuted[0].NetAmount;
                            RunRecord.Num13 = takerExecuted[0].AveragePrice;
                        }
                    }
                }
                else
                {
                    //Confirm opening qty. If not, get it.
                    if (RunRecord.Num7 == 0.0M)
                    {
                        IOrder[] takerExecuted = await takerShort.GetOrdersAsync(RunRecord.Order1);
                        if (takerExecuted.Length > 0)
                        {
                            RunRecord.Num7 = takerExecuted[0].NetAmount;
                            RunRecord.Num9 = takerExecuted[0].AveragePrice;
                        }
                    }

                    if (RunRecord.Num8 == 0.0M)
                    {
                        IOrder[] takerExecuted = await takerShort.GetOrdersAsync(RunRecord.Order2);
                        if (takerExecuted.Length > 0)
                        {
                            RunRecord.Num8 = takerExecuted[0].NetAmount;
                            RunRecord.Num10 = takerExecuted[0].AveragePrice;
                        }
                    }
                }
                return processed;

            }

            /// <summary>
            /// Close the entire position for the set at market. Closes the left taker first, then the right taker
            /// </summary>
            /// <param name="forceToClose">Set True to always close the other position even if one of them failed.</param>
            /// <returns>True if both positions are closed successfully, or forceToClose is set to True and one position is closed, False otherwise.
            public async Task<bool> ClosePosition(bool forceToClose)
            {
                /* 
                 * Position must be opened, both opening takers already executed.
                 * Position must not be already closed, no closing taker orders are executed.
                 */                
                Run runRecord = RunRecord;
                string message = string.Empty;
                decimal topPrice = 0.0M, totalPrice = 0.0M, avgPrice = 0.0M;
                bool leftOk = true, rightOk = true;

                if (IsPositionClosed)
                {
                    message = "Cannot close position that is is already closed. ";
                    runRecord.Message = message;
                    runRecord.Status = (int)RunStatus.Fault;
                    return false;
                }
                else if (!IsPositionOpened)
                {
                    message = "Cannot close position that is is not open. ";
                    runRecord.Message = message;
                    runRecord.Status = (int)RunStatus.Fault;
                    return false;
                }
                else //ready to be closed
                {
                    IResult res;
                    if (runRecord.Text5.ToLower() == "sell") //left taker opened with a sell order
                    {
                        /*left buy order*/
                        IOrderBook obLeft = LeftTaker.GetOrderBook(runRecord.Text2);
                        if (obLeft == null)
                        {
                            message += "Could not get order book for left taker " + LeftTaker.ToString() + " " + runRecord.Text2 + ". ";
                            leftOk = false;
                        }
                        else if (!Helper.GetTopPrice(obLeft, Api.Side.Sell, runRecord.Num7.Value, out topPrice, out totalPrice, out avgPrice))
                        {
                            message += "Not enough quantities on orderbook or quoting has failed " + LeftTaker.ToString() + " " + runRecord.Text2 + ". ";
                            leftOk = false;
                        }
                        if (leftOk)
                        {
                            res = await ExecuteTakerOrder(LeftTaker, runRecord.Text2, Api.Side.Buy, runRecord.Num7.Value, topPrice);
                            leftOk = res.Success;
                            if (leftOk)
                            {
                                runRecord.Num11 = ((IOrder)res).NetAmount.GetValueOrDefault();
                                runRecord.Num13 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                                runRecord.Order6 = ((IOrder)res).OrderID;
                            }
                            else
                            {
                                message += res.Message;
                            }
                        }

                        /*right sell order*/
                        if (leftOk || forceToClose)
                        {
                            IOrderBook obRight = RightTaker.GetOrderBook(runRecord.Text4);
                            if (obRight == null)
                            {
                                message += "Could not get order book for right taker " + RightTaker.ToString() + " " + runRecord.Text2 + ". ";
                                rightOk = false;
                            }
                            else if (!Helper.GetTopPrice(obRight, Api.Side.Buy, runRecord.Num8.Value, out topPrice, out totalPrice, out avgPrice))
                            {
                                message += "Not enough quantities on orderbook or quoting has failed " + RightTaker.ToString() + " " + runRecord.Text4 + ". ";
                                rightOk = false;
                            }
                            if (rightOk)
                            {
                                res = await ExecuteTakerOrder(RightTaker, runRecord.Text4, Api.Side.Sell, runRecord.Num8.Value, topPrice); //right taker order
                                rightOk = res.Success;
                                if (rightOk)
                                {
                                    runRecord.Num12 = ((IOrder)res).NetAmount.GetValueOrDefault();
                                    runRecord.Num14 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                                    runRecord.Order4 = ((IOrder)res).OrderID;

                                    //I must have the right confirmed amount here, if not, get it.
                                    if (RunRecord.Num12 == 0.0M)
                                    {
                                        IOrder[] takerExecuted = await RightTaker.GetOrdersAsync(RunRecord.Order4);
                                        if (takerExecuted.Length > 0)
                                            RunRecord.Num12 = takerExecuted[0].NetAmount;
                                    }
                                }
                                else
                                {
                                    message += res.Message;
                                }
                            }
                        }

                        if (leftOk)
                        {
                            //I must have the left confirmed amount here, if not, get it.
                            if (RunRecord.Num11 == 0.0M)
                            {
                                IOrder[] takerExecuted = await LeftTaker.GetOrdersAsync(RunRecord.Order6);
                                if (takerExecuted.Length > 0)
                                    RunRecord.Num11 = takerExecuted[0].NetAmount;
                            }
                        }
                    }
                    else //left taker opened with a buy order
                    {
                        /*left sell order*/
                        IOrderBook obLeft = LeftTaker.GetOrderBook(runRecord.Text2);
                        if (obLeft == null)
                        {
                            message += "Could not get order book for left taker " + LeftTaker.ToString() + " " + runRecord.Text2 + ". ";
                            leftOk = false;
                        }
                        else if (!Helper.GetTopPrice(obLeft, Api.Side.Buy, runRecord.Num7.Value, out topPrice, out totalPrice, out avgPrice))
                        {
                            message += "Not enough quantities on orderbook or quoting has failed " + LeftTaker.ToString() + " " + runRecord.Text2 + ". ";
                            leftOk = false;
                        }
                        if (leftOk)
                        {
                            res = await ExecuteTakerOrder(LeftTaker, runRecord.Text2, Api.Side.Sell, runRecord.Num7.Value, topPrice); //left taker
                            leftOk = res.Success;
                            if (leftOk)
                            {
                                runRecord.Num11 = ((IOrder)res).NetAmount.GetValueOrDefault();
                                runRecord.Num13 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                                runRecord.Order6 = ((IOrder)res).OrderID;
                            }
                            else
                            {
                                message += res.Message;
                            }
                        }

                        /*right buy order*/
                        if (leftOk || forceToClose)
                        {
                            IOrderBook obRight = RightTaker.GetOrderBook(runRecord.Text4);
                            if (obRight == null)
                            {
                                message += "Could not get order book for right taker " + RightTaker.ToString() + " " + runRecord.Text2 + ". ";
                                rightOk = false;
                            }
                            else if (!Helper.GetTopPrice(obRight, Api.Side.Sell, runRecord.Num8.Value, out topPrice, out totalPrice, out avgPrice))
                            {
                                message += "Not enough quantities on orderbook or quoting has failed " + RightTaker.ToString() + " " + runRecord.Text4 + ". ";
                                rightOk = false;
                            }
                        }
                        if (rightOk)
                        {
                            res = await ExecuteTakerOrder(RightTaker, runRecord.Text4, Api.Side.Buy, runRecord.Num8.Value, topPrice); //right taker order
                            rightOk = res.Success;
                            if (rightOk)
                            {
                                runRecord.Num12 = ((IOrder)res).NetAmount.GetValueOrDefault();
                                runRecord.Num14 = ((IOrder)res).AveragePrice.GetValueOrDefault();
                                runRecord.Order4 = ((IOrder)res).OrderID;

                                //I must have the confirmed amount here, if not, get it.
                                if (RunRecord.Num12 == 0.0M)
                                {
                                    IOrder[] takerExecuted = await RightTaker.GetOrdersAsync(RunRecord.Order4);
                                    if (takerExecuted.Length > 0)
                                        RunRecord.Num12 = takerExecuted[0].NetAmount;
                                }
                            }
                            else
                            {
                                message += res.Message;
                            }
                        }

                        if (leftOk)
                        {
                            //I must have the left confirmed amount here, if not, get it.
                            if (RunRecord.Num11 == 0.0M)
                            {
                                IOrder[] takerExecuted = await LeftTaker.GetOrdersAsync(RunRecord.Order6);
                                if (takerExecuted.Length > 0)
                                    RunRecord.Num11 = takerExecuted[0].NetAmount;
                            }
                        }
                    }

                    //Cancel the maker orders
                    IResult resCancel; bool leftCancelOk = true, rightCancelOk = true;
                    if (leftOk && MakerClosingOrderLeft != null)
                    {
                        MakerClosingOrderLeft.Stop("Close position");
                        resCancel = await OrderManager.Instance.CancelOrderAsync(LeftTaker, MakerClosingOrderLeft.OrderID);
                        leftCancelOk = resCancel.Success;
                        if (!leftCancelOk)
                        {
                            message += "Failed to cancel left closing maker order." + MakerClosingOrderLeft.OrderID;
                            RunRecord.Status = (int)RunStatus.CompletedWithError;
                        }
                    }
                    if (rightOk && MakerClosingOrderRight != null)
                    {
                        MakerClosingOrderRight.Stop("Close position");
                        resCancel = await OrderManager.Instance.CancelOrderAsync(RightTaker, MakerClosingOrderRight.OrderID);
                        rightCancelOk = resCancel.Success;
                        if (!rightCancelOk)
                        {
                            message += "Failed to cancel right closing maker order. " + MakerClosingOrderRight.OrderID;
                            RunRecord.Status = (int)RunStatus.CompletedWithError;
                        }
                    }

                    if (leftOk && rightOk)
                    {
                        runRecord.Status = (int)RunStatus.Completed;
                        if (leftCancelOk && rightCancelOk)
                        {
                            runRecord.Status = (int)RunStatus.CompletedWithError;
                            runRecord.Message = message;
                        }
                        return true;
                    }
                    else if (leftOk || rightOk && forceToClose)
                    {
                        runRecord.Status = (int)RunStatus.CompletedWithError;
                        runRecord.Message = message;
                        return true;
                    }
                    else
                    {
                        runRecord.Status = (int)RunStatus.Fault;
                        runRecord.Message = message;
                        return false;
                    }
                } //end case 1
            } //End ClosePosition()
        }

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
                    foreach (TakerTakerSet set in takerTakerSets.Values)
                    {
                        if (set.MakerClosingOrderLeft != null)
                            set.MakerClosingOrderLeft.Stop("disposing");
                        if (set.MakerClosingOrderRight != null)
                            set.MakerClosingOrderRight.Stop("disposing");
                    }
                    takerTakerSets.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~TakerTaker2() {
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
