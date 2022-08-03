using System;
using System.Threading.Tasks;
using Chordata.Bex.Api;
using Chordata.Bex.Api.Interface;
using Chordata.Bex.Central.Common;

namespace Chordata.Bex.Central
{
    /// <summary>
    /// An self maintained order where the price always rests at a certain percentage above or below of the target market
    /// </summary>
    public sealed class FloatingPriceOrder3 : IOrder
    {
        #region Declarations
        private readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public event EventHandler OnOrderHit;
        public event EventHandler OnOrderAmended;
        public event EventHandler OnOrderFilled;
        public event EventHandler OnOrderCancelled;
        public event EventHandler<string> OnError;

        const ushort MAX_ERROR_COUNT = 3; //maximum error occurance before hard stop on the order
        private decimal lastPrice = 0.0M;
        private const decimal maxPricingSpared = 0.5M;
        private System.Timers.Timer errorTimer = new System.Timers.Timer(30000);
        private DateTime InitializedOnLocal = DateTime.MaxValue;
        private DateTime HurdleBoostEffectiveUntil = DateTime.MaxValue;
        private bool canceling = false;
        private bool stopping = false;
        private System.Threading.SemaphoreSlim semaphoreUpdating = new System.Threading.SemaphoreSlim(1, 1);

        #endregion

        #region Properties

        public ushort ErrorCount { get; private set; }

        private int OperationID { get; set; }

        public IApi OwnerApi { get; set; }

        public IApi TargetAPI { get; set; }

        public string TargetSymbol { get; set; }

        public decimal TargetPrice { get; private set; }

        public decimal TargetQty { get; set; }

        public decimal HurdleRate { get; set; }

        public decimal OriginalHurdleRate { get; private set; }

        public decimal TakerFeeRate { get; set; }

        public decimal MakerFeeRate { get; set; }

        public string OrderID { get; set; }

        public Side Side { get; set; }

        public decimal? AveragePrice { get; set; }

        public decimal? Price { get; set; }

        public decimal? Amount { get; set; }

        public decimal? FilledQty { get; set; }

        public decimal? NetAmount { get; set; }

        public string Symbol { get; set; }

        public OrderStatus OrderStatus { get; set; }

        public bool IsOpen { get; set; }

        public bool IsFullyFilled { get; set; }

        public bool IsFilled { get; set; }

        public bool IsCancelled { get; set; }

        public bool IsHurdleBoosted { get { return DateTime.Now < HurdleBoostEffectiveUntil; } }

        public int? ID { get; set; }

        public string OldOrderId { get; set; }

        public PricingScheme PricingScheme { get; set; }

        public CancelReason CancelReason { get; set; }

        public string Message { get; set; }

        #endregion

        #region Methods

        internal FloatingPriceOrder3()
        {
            errorTimer.Elapsed += ErrorTimer_Elapsed;
            InitializedOnLocal = DateTime.Now;
        }

        public void Start()
        {
            ErrorCount = 0;
            errorTimer.Start();
            OwnerApi.OnOrderChanged += Instance_OnOrderChanged;
            TargetAPI.OnOrderBookChanged += TargetAPI_OnOrderBookChanged;
        }

        public async void Stop(string reason)
        {
            //System.Threading.SpinWait.SpinUntil(() => !updating, 1000);
            errorTimer.Stop();
            log.Debug("FPX Stop - " + reason + " " + this.ToString());
            TargetAPI.OnOrderBookChanged -= TargetAPI_OnOrderBookChanged;
            await semaphoreUpdating.WaitAsync();
            try
            {
                OwnerApi.OnOrderChanged -= Instance_OnOrderChanged;
            }
            finally
            {
                semaphoreUpdating.Release();
            }            
        }    

        /// <summary>
        /// Create a new self maintaining floating price order based on a target market's top quote.
        /// </summary>
        /// <param name="symbol">Instrument to rest the order on</param>
        /// <param name="side">Buy or Sell</param>
        /// <param name="type">Order type</param>
        /// <param name="quoteAmount">Order qty</param>
        /// <param name="pricePerCoin">Initial price</param>
        /// <param name="targetSymbol">Instrument on the target market to base the price against</param>
        /// <param name="targetApi">Target market's API</param>
        /// <param name="hurdleRate">Hurdle rate</param>
        /// <param name="takerFeeRate">Taker fee of the target market</param>
        /// <param name="makerFeeRate">Maker fee of the owner's market</param>
        /// <param name="operationId">Op ID</param>
        /// <returns>A new FloatingPriceOrder object if successful, null otherwise</returns>
        public async static Task<FloatingPriceOrder3> Create(IApi ownerApi, string symbol, Side side, OrderType orderType, decimal quoteAmount, decimal pricePerCoin, string targetSymbol, IApi targetApi, decimal originalHurdleRate, decimal hurdleRate, DateTime hurdleRateEffectiveUntil, decimal takerFeeRate, decimal makerFeeRate, PricingScheme pricingScheme, int operationId)
        {
            decimal _topPrice = 0.0M;
            IResult result = null;
            if (side == Side.Buy)
            {
                //if (ownerApi.ToString() == "Poloniex" && makerFeeRate < 1) //make case for Poloniex, where the net qty you get is less the makerFeeRate
                //    quoteAmount = quoteAmount / (1 - makerFeeRate);

                if (pricePerCoin == 0.0M)
                    pricePerCoin = CalculateNewBidPrice(targetApi, targetSymbol, quoteAmount, makerFeeRate, takerFeeRate, hurdleRate, pricingScheme, out _topPrice);

                OrderOptions opt = OrderOptions.PostOnly;
                result = await OrderManager.Instance.PostBuyOrderAsync(ownerApi, symbol, orderType, quoteAmount, pricePerCoin, opt);
            }
            else if (side == Side.Sell)
            {
                if (pricePerCoin == 0.0M)
                    pricePerCoin = CalculateNewAskPrice(targetApi, targetSymbol, quoteAmount, makerFeeRate, takerFeeRate, hurdleRate, pricingScheme, out _topPrice);
                if (pricePerCoin == 0.0M) return null;

                OrderOptions opt = OrderOptions.PostOnly;
                result = await OrderManager.Instance.PostSellOrderAsync(ownerApi, symbol, orderType, quoteAmount, pricePerCoin, opt);
            }
            if (result != null)
            {
                FloatingPriceOrder3 order = new FloatingPriceOrder3();
                try
                {
                    order.UpdateFrom((IOrder)result);
                }
                catch (NullReferenceException)
                {

                }
                catch (InvalidOperationException)
                {
                }

                order.lastPrice = order.Price.Value;
                order.TargetSymbol = targetSymbol;
                order.TargetAPI = targetApi;
                order.HurdleRate = hurdleRate;
                order.OriginalHurdleRate = order.HurdleRate;
                order.HurdleBoostEffectiveUntil = hurdleRateEffectiveUntil;
                order.TakerFeeRate = takerFeeRate;
                order.MakerFeeRate = makerFeeRate;
                order.OwnerApi = ownerApi;
                order.PricingScheme = pricingScheme;
                order.OperationID = operationId;
                order.OldOrderId = order.OrderID;

                return order;
            }
            return null;
        }

        /// <summary>
        /// Create a self maintaining floating price order based on a taret market's top quote for an existing order.
        /// </summary>
        /// <param name="ownerApi">Order's API</param>
        /// <param name="orderID">Existing order's ID</param>
        /// <param name="targetSymbol">Instrument on the target market to base the price against</param>
        /// <param name="targetApi">Target market's API</param>
        /// <param name="hurdleRate">Hurdle rate</param>
        /// <param name="takerFeeRate">Taker fee of the target market</param>
        /// <param name="makerFe00.eRate">Maker fee of the owner's market</param>
        /// <param name="operationId">Op ID</param>
        /// <returns>A new FloatingPriceOrder object if successful, null otherwise</returns>
        public async static Task<FloatingPriceOrder3> Get(IApi ownerApi, string orderID, string targetSymbol, IApi targetApi, decimal hurdleRate, decimal takerFeeRate, decimal makerFeeRate, PricingScheme pricingScheme, int operationId)
        {
            IOrder[] orders = await OrderManager.Instance.GetOrderAsync(ownerApi, orderID);
            if (orders.Length > 0)
            {
                FloatingPriceOrder3 order = new FloatingPriceOrder3();
                order.UpdateFrom(orders[0]);
                order.lastPrice = order.Price.Value;
                order.TargetSymbol = targetSymbol;
                order.TargetAPI = targetApi;
                order.HurdleRate = hurdleRate;
                order.OriginalHurdleRate = order.HurdleRate;
                order.TakerFeeRate = takerFeeRate;
                order.MakerFeeRate = makerFeeRate;
                order.OwnerApi = ownerApi;
                order.OperationID = operationId;
                order.PricingScheme = pricingScheme;
                order.OldOrderId = order.OrderID;
                return order;
            }
            return null;
        }

        private static decimal CalculateNewAskPrice(IApi targetApi, string targetSymbol, decimal quantityNeeded, decimal makerFeeRate, decimal takerFeeRate, decimal hurdleRate, PricingScheme pricingScheme, out decimal topPrice)
        {
            decimal TopAskPrice = 0.0M, totalAskPrice = 0.0M, avgPrice = 0.0M;
            IOrderBook ob = targetApi.GetOrderBook(targetSymbol);
            if (ob != null)
            {
                if (!Helper.GetTopPrice(ob, Side.Sell, quantityNeeded, out TopAskPrice, out totalAskPrice, out avgPrice))
                {
                    topPrice = 0.0M;
                    return 0.0M;
                }
                topPrice = TopAskPrice;
                //sell_price = (Buy + BuyTakerFee + SellMakerFee) x (1 + Hurdle)
                //return (avgPrice + (avgPrice * takerFeeRate) + (avgPrice * hurdleRate) + (avgPrice * takerFeeRate * hurdleRate)) / (1 - makerFeeRate - (makerFeeRate * hurdleRate));
                if (pricingScheme == PricingScheme.PercentageOfTarget)
                {
                    return (avgPrice + (avgPrice * hurdleRate) + (avgPrice * takerFeeRate)) / (1 - makerFeeRate);
                }
                else if (pricingScheme == PricingScheme.PercentageOfNewPrice)
                {
                    return (avgPrice - (avgPrice * takerFeeRate)) / (1 + hurdleRate + makerFeeRate);
                }
                else
                {
                    return 0.0M;
                }
            }
            else
            {
                topPrice = 0.0M;
                return 0.0M;
            }
        }

        private static decimal CalculateNewBidPrice(IApi targetApi, string targetSymbol, decimal quantityNeeded, decimal makerFeeRate, decimal takerFeeRate, decimal hurdleRate, PricingScheme pricingScheme, out decimal topPrice)
        {
            decimal TopBidPrice = 0.0M, totalBidPrice = 0.0M, avgPrice = 0.0M;
            IOrderBook ob = targetApi.GetOrderBook(targetSymbol);
            if (ob != null)
            {
                if (!Helper.GetTopPrice(ob, Side.Buy, quantityNeeded, out TopBidPrice, out totalBidPrice, out avgPrice))
                {
                    topPrice = 0.0M;
                    return 0.0M;
                }
                topPrice = TopBidPrice;
                if (pricingScheme == PricingScheme.PercentageOfTarget)
                {
                    //buy_price = avgPrice + avgPrice * hurdle - takerFee - makerfee
                    //b = s + sR
                    return (avgPrice + (avgPrice * hurdleRate) - (avgPrice * takerFeeRate)) / (1 + makerFeeRate);

                }
                else if (pricingScheme == PricingScheme.PercentageOfNewPrice)
                {
                    //buy_price = avgPrice - buy_price * hurdle;                    
                    //b = s - bR
                    return (avgPrice - (avgPrice * takerFeeRate)) / (1 + hurdleRate + makerFeeRate);
                }
                else
                {
                    return 0.0M;
                }
            }
            else
            {
                topPrice = 0.0M;
                return 0.0M;
            }
        }

        /// <summary>
        /// Cancels the underlying order on the exchange and stops the order book monitoring process
        /// </summary>
        /// <returns></returns>
        public async Task<IResult> CancelOrder(string reason)
        {
            canceling = true;
            log.Debug("FPX Canceling Order - " + reason + " " + ToString());

            await semaphoreUpdating.WaitAsync();
            try
            {
                IResult res = await OrderManager.Instance.CancelOrderAsync(OwnerApi, OrderID);
                return res;
            }
            catch
            {
                return null;
            }
            finally
            {
                semaphoreUpdating.Release();
            }          
        }

        /// <summary>
        /// Process the order according to orderbook change
        /// </summary>
        private async void ProcessOrder()
        {
            
            decimal newPrice = 0.0M, topPrice = 0.0M;
            if (TargetAPI.IsOrderBookSubscribed(TargetSymbol))
            {
                //updating = true;
                await semaphoreUpdating.WaitAsync();
                try
                {
                    if (OrderStatus == OrderStatus.Canceled || canceling || stopping)
                        return;

                    //reset any hurdle boost
                    if (DateTime.Now >= HurdleBoostEffectiveUntil)
                        ResetHurdleRate();

                    if (Side == Side.Sell)
                    {
                        newPrice = CalculateNewAskPrice(TargetAPI, TargetSymbol, Amount.GetValueOrDefault(), MakerFeeRate, TakerFeeRate, HurdleRate, PricingScheme, out topPrice);
                        if (topPrice == 0.0M) //empty orderbook
                            return;
                        TargetPrice = topPrice;
                    }
                    else if (Side == Side.Buy)
                    {
                        newPrice = CalculateNewBidPrice(TargetAPI, TargetSymbol, Amount.GetValueOrDefault(), MakerFeeRate, TakerFeeRate, HurdleRate, PricingScheme, out topPrice);
                        if (topPrice == 0.0M)
                            return;
                        TargetPrice = topPrice;
                    }
                    else
                    {
                        return;
                    }
                    IInstrument inst = await OwnerApi.GetInstrumentAsync(Symbol);
                    if (Tools.RoundToTickSize(newPrice, inst.TickSize.GetValueOrDefault()) != Tools.RoundToTickSize(lastPrice, inst.TickSize.GetValueOrDefault()))
                    {
                        //sanity check
                        if (lastPrice != 0.0M)
                        {
                            decimal change = (newPrice - lastPrice) / lastPrice;
                            if (Math.Abs(change) >= 0.3M)
                                return;
                        }
                        decimal lastlastPrice = lastPrice;
                        lastPrice = newPrice;
                        IResult res = await OrderManager.Instance.AmendOrderAsync(OwnerApi, Symbol, OrderID, Amount, newPrice);
                        if (res.Success)
                        {
                            IOrder order = (IOrder)res;
                            if (order != null && !string.IsNullOrEmpty(order.OrderID))
                            {
                                UpdateFrom(order, false);
                                lastPrice = (decimal)order.Price;
                                OnOrderAmended?.Invoke(this, new EventArgs());
                            }
                        }
                        else
                        {
                            lastPrice = lastlastPrice;
                            if (res.ErrorCode == ErrorCode.RateLmitExceeded || res.ErrorCode == ErrorCode.ExchangeBusy)
                            {
                                await Task.Delay(1000);
                            }
                            else
                            {
                                ErrorCount++;
                                if (ErrorCount > MAX_ERROR_COUNT)
                                    OnError?.Invoke(this, res.Message + " AmendOrder " + OrderID);
                            }
                        }
                    }
                }
                finally
                {
                    //updating = false;
                    semaphoreUpdating.Release();
                }
            }
        }

        public void UpdateFrom(IOrder source, bool updateStatus)
        {
            IOrder sourceOrder = (IOrder)source;
            if (OrderID != sourceOrder.OrderID)
                OldOrderId = OrderID;

            CancelReason = source.CancelReason;
            if (sourceOrder.OrderID != null) OrderID = sourceOrder.OrderID;
            if (sourceOrder.Price != null) Price = sourceOrder.Price;
            if (sourceOrder.AveragePrice != null) AveragePrice = sourceOrder.AveragePrice;
            if (sourceOrder.Amount != null) Amount = sourceOrder.Amount;
            if (sourceOrder.NetAmount != null) NetAmount = sourceOrder.NetAmount;
            if (sourceOrder.FilledQty != null) FilledQty = sourceOrder.FilledQty;
            if (sourceOrder.Symbol != null) Symbol = sourceOrder.Symbol;

            if (updateStatus)
            {
                if (sourceOrder.OrderStatus != OrderStatus.None) OrderStatus = sourceOrder.OrderStatus;
                if (sourceOrder.OrderStatus != OrderStatus.None) IsOpen = sourceOrder.IsOpen;
                if (sourceOrder.OrderStatus != OrderStatus.None) IsFullyFilled = sourceOrder.IsFullyFilled;
                if (sourceOrder.OrderStatus != OrderStatus.None) IsCancelled = sourceOrder.IsCancelled;
                if (sourceOrder.OrderStatus != OrderStatus.None) IsFilled = sourceOrder.IsFilled;
            }
            if (sourceOrder.Side != Side.None) Side = sourceOrder.Side;
            if (sourceOrder.ID != null) ID = sourceOrder.ID;
        }

        public override string ToString()
        {
            return "FPX order " + OwnerApi.ToString() + " " + Symbol + " " + Side.ToString() + " " + Amount.Value.ToStringNormalized() + "@" + Price.Value.ToStringNormalized() + " >>" + TargetAPI.ToString() + " " + TargetSymbol + " " + OrderID;
        }

        #endregion

        #region Events

        private void Instance_OnOrderChanged(object sender, OrderChangedEventArgs e)
        {
            if (e.Order.OrderID == OrderID)
            {
                lock (this)
                {
                    IOrder order = e.Order;
                    bool logFilled = false;
                    if (order.OrderStatus == OrderStatus.Filled || order.OrderStatus == OrderStatus.PartiallyFilled)
                    {
                        logFilled = true;
                        if (logFilled) log.Debug("Inbound Order " + OrderID + " status:" + order.OrderStatus.ToString() + " isFilled?" + order.IsFilled.ToString() + " FPX filled? " + IsFilled.ToString());
                    }
                    if (!IsFilled && (order.IsFilled))
                    {
                        if (logFilled) log.Debug("FPX being filled. FilledQty " + order.FilledQty.GetValueOrDefault().ToString());
                        if (order.FilledQty > 0.0M)
                        {
                            Stop("Order filled");
                            UpdateFrom(order);

                            log.Debug("OnOrderHit null? " + (OnOrderHit == null).ToString());
                            OnOrderHit?.Invoke(this, new EventArgs());
                            log.Debug("OnOrderFilled null? " + (OnOrderFilled == null).ToString());
                            OnOrderFilled?.Invoke(this, new EventArgs());
                        }
                    }
                    else if (order.OrderStatus == OrderStatus.Canceled)
                    {
                        Stop("Order cancelled");
                        UpdateFrom(order);
                        OnOrderCancelled?.Invoke(this, new EventArgs());
                    }
                    else if (order.OrderStatus == OrderStatus.New)
                    {
                        UpdateFrom(order);
                    }
                    else if (sender is Api.Poloniex.PoloniexApi && !IsFilled && order.OrderStatus == OrderStatus.None) //this special case for poloniex only.
                    {
                        if (Amount > 0)
                        {
                            Stop("Polo Ord Monitor order fill");
                            UpdateFrom(order);
                            OnOrderHit?.Invoke(this, new EventArgs());
                            OnOrderFilled?.Invoke(this, new EventArgs());
                        }
                    }
                    else
                    {
                        if (logFilled) log.Debug("FPX default update");
                        UpdateFrom(order);
                    }
                }
            }
        }

        private void TargetAPI_OnOrderBookChanged(object sender, OrderBookChangedEventArgs e)
        {
            if (TargetAPI != null && !string.IsNullOrEmpty(TargetSymbol) && e.Symbol == TargetSymbol)
            {
                if (IsFilled) return;
                //else if (IsOpen && !canceling && !stopping)
                else if (IsOpen)
                {
                    ProcessOrder();
                }
            }
        }

        private void ErrorTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ErrorCount = 0;
        }

        public void ClearEventSubscribers()
        {
            OnOrderCancelled = null;
            OnOrderAmended = null;
            OnError = null;
            OnOrderFilled = null;
            OnOrderHit = null;
        }

        public void ResetHurdleRate()
        {
            HurdleRate = OriginalHurdleRate;
            HurdleBoostEffectiveUntil = DateTime.MaxValue;
        }

        public void UpdateFrom(IOrder source)
        {
            UpdateFrom(source, true);
        }

        #endregion
    }
}
