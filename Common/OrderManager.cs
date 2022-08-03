using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chordata.Bex.Api;
using Chordata.Bex.Api.Interface;
using Chordata.Bex.Central.Data;

namespace Chordata.Bex.Central
{
    public sealed class OrderManager
    {
        #region Singleton
        private static volatile OrderManager instance;
        private static object syncRoot = new Object();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private int amendInProgress = 0;
        public bool IsTradeEnabled
        {
            get
            {
                return (bool)Properties.Settings.Default["TradeEnabled"];
            }
        }

        public bool OrderInProgress {
            get
            {
                return amendInProgress > 0;
            }
        }

        private OrderManager()
        {
            //TODO: get all orders from all markets ???
            //using (var db = new Tuna())
            //{
            //    var query = from ord in db.Orders
            //                where ord.Status == OrderStatus.New.ToString() || ord.Status == OrderStatus.PartiallyFilled.ToString()
            //                select ord;

            //    foreach (Order record in query)
            //    {
                                                                                
            //    }
            //}
        }
        public static OrderManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new OrderManager();
                    }
                }

                return instance;
            }
        }
        #endregion

        private Dictionary<IApi, Dictionary<string, IOrder>> openOrders = new Dictionary<IApi, Dictionary<string, IOrder>>(2);

        public int OpenMarketCount
        {
            get
            {
                int c = 0;
                foreach (IApi api in openOrders.Keys)
                {
                    if (openOrders[api].Count > 0)
                        c++;
                }
                return c;
            }
        }

        public int OpenOrderCount
        {
            get
            {
                int c = 0;
                foreach (IApi api in openOrders.Keys)
                {
                    c += openOrders[api].Count;
                }
                return c;
            }
        }

        public async Task<IOrder[]> GetOrderAsync(IApi api, string orderId)
        {
            log.Debug("GetOrderAsync " + api.ToString() + " " + orderId);
            IOrder[] res = await api.GetOrdersAsync(orderId);
            for (int i = 0; i < res.Length; i++)
            {
                if (res[i].IsOpen)
                    AddOrder(api, res[i]);
            }
            return res;
        }

        /// <summary>
        /// Submit a buy order.
        /// </summary>
        /// <param name="api">The market's API object</param>
        /// <param name="symbol">Instrument symbol</param>
        /// <param name="orderType">Order type: Limit, Market, Stop, or StopLimit</param>
        /// <param name="amount">Order quantity</param>
        /// <param name="price">Order quote price per qty</param>
        /// <param name="options">Order options, not all options are valid for all exchanges, check the exchange's API documentation.</param>
        /// <param name="useMargin">True to use margin account. Not valid for all exchanges.</param>
        /// <returns>Result object containing the order object if successful, otherwise a Result object containing the error message</returns>
        public async Task<IResult> PostBuyOrderAsync(IApi api, string symbol, OrderType orderType, decimal amount, decimal price, OrderOptions options = OrderOptions.None, bool useMargin = false)
        {
            if (!IsTradeEnabled)
                return null;

            log.Debug("PostBuyOrderAsync(" + api.ToString() + ", " + symbol + ", " + orderType.ToString() + ", " + amount.ToString() + ", " + price.ToString() + ")");
            IResult res =  await api.PostBuyOrderAsync(symbol, orderType, amount, price, options, useMargin);
            if (res.Success)
            {                
                IOrder o = (IOrder)res;                
                if (o.OrderStatus != OrderStatus.Canceled && !string.IsNullOrEmpty(o.OrderID))
                {
                    log.Debug("PostBuyOrderAsync Success " + api.ToString() + " " + symbol + " Buy " + orderType.ToString() + " Order " + amount.ToString() + "@" + price.ToString() + " OrdID " + o.OrderID);
                    Order ord = new Order()
                    {
                        Amount = amount,
                        Exchange = api.ToString(),
                        OrderId = o.OrderID,
                        OrderType = orderType.ToString(),
                        Price = price,
                        Side = o.Side.ToString(),
                        Status = o.OrderStatus.ToString(),
                        Symbol = symbol,
                        CreateDate = DateTime.UtcNow

                    };
                    Common.DBUtil.AddOrderAsyc(ord, o);                    
                    /*
                    using (Tuna db = new Tuna())
                    {
                        Order ord = new Order()
                        {
                            Amount = amount,
                            Exchange = api.ToString(),
                            OrderId = o.OrderID,
                            OrderType = orderType.ToString(),
                            Price = price,
                            Side = o.Side.ToString(),
                            Status = o.OrderStatus.ToString(),
                            Symbol = symbol,
                            CreateDate = DateTime.UtcNow

                        };
                        db.Orders.Add(ord);
                        await Common.DBUtil.SaveDbAsync(db, true); //await db.SaveChangesAsync();
                        o.ID = ord.Id;
                    }*/
                    AddOrder(api, o);

                }
                else
                {
                    res.Success = false;
                    if (o.OrderStatus == OrderStatus.Canceled)
                        res.Message += " Order was cancelled. ";
                    log.Debug("PostBuylOrderAsync(" + api.ToString() + ", " + symbol + ", " + orderType.ToString() + ", " + amount.ToString() + ", " + price.ToString() + ") Failed:" + res.Message);
                }
            }
            else
            {
                log.Debug("PostBuylOrderAsync(" + api.ToString() + ", " + symbol + ", " + orderType.ToString() + ", " + amount.ToString() + ", " + price.ToString() + ") Failed:" + res.Message);
            }
            return res;            
        }

        /// <summary>
        /// Submit a sell order.
        /// </summary>
        /// <param name="api">The market's API object</param>
        /// <param name="symbol">Instrument symbol</param>
        /// <param name="orderType">Order type: Limit, Market, Stop, or StopLimit</param>
        /// <param name="amount">Order quantity</param>
        /// <param name="price">Order quote price per qty</param>
        /// <param name="options">Order options, not all options are valid for all exchanges, check the exchange's API documentation.</param>
        /// <param name="useMargin">True to use margin account. Not valid for all exchanges.</param>
        /// <returns>Result object containing the order object if successful, otherwise a Result object containing the error message</returns>
        public async Task<IResult> PostSellOrderAsync(IApi api, string symbol, OrderType orderType, decimal amount, decimal price, OrderOptions options = OrderOptions.None, bool useMargin = false)
        {
            if (!IsTradeEnabled)
                return null;
            log.Debug("PostSellOrderAsync(" + api.ToString() + ", " + symbol + ", " + orderType.ToString() + ", " + amount.ToString() + ", " + price.ToString() + ")");
            IResult res = await api.PostSellOrderAsync(symbol, orderType, amount, price, options, useMargin);
            if (res.Success)
            {
                IOrder o = (IOrder)res;                
                if (o.OrderStatus != OrderStatus.Canceled && !string.IsNullOrEmpty(o.OrderID))
                {
                    log.Debug("PostSellOrderAsync Success " + api.ToString() + " " + symbol + " Sell " + orderType.ToString() + " order " + amount.ToString() + "@" + price.ToString() + " ID " + o.OrderID);

                    Order ord = new Order()
                    {
                        Amount = amount,
                        Exchange = api.ToString(),
                        OrderId = o.OrderID,
                        OrderType = orderType.ToString(),
                        Price = price,
                        Side = o.Side.ToString(),
                        Status = o.OrderStatus.ToString(),
                        Symbol = symbol,
                        CreateDate = DateTime.UtcNow

                    };
                    Common.DBUtil.AddOrderAsyc(ord, o);
                    /*
                    using (Tuna db = new Tuna())
                    {
                        Order ord = new Order()
                        {
                            Amount = o.Amount,
                            Exchange = api.ToString(),
                            OrderId = o.OrderID,
                            OrderType = orderType.ToString(),
                            Price = o.Price,
                            Side = o.Side.ToString(),
                            Status = o.OrderStatus.ToString(),
                            Symbol = o.Symbol,
                            CreateDate = DateTime.UtcNow
                        };
                        db.Orders.Add(ord);
                        await Common.DBUtil.SaveDbAsync(db, true); //await db.SaveChangesAsync();
                        o.ID = ord.Id;
                        
                    }*/
                    AddOrder(api, o);                    
                }
                else
                {
                    res.Success = false;
                    if(o.OrderStatus == OrderStatus.Canceled)
                        res.Message += " Order was cancelled.";
                    log.Debug("PostSellOrderAsync(" + api.ToString() + ", " + symbol + ", " + orderType.ToString() + ", " + amount.ToString() + ", " + price.ToString() + ") Failed:" + res.Message);
                }
            }
            else
            {
                log.Debug("PostSellOrderAsync(" + api.ToString() + ", " + symbol + ", " + orderType.ToString() + ", " + amount.ToString() + ", " + price.ToString() + ") Failed:" + res.Message);
            }
            return res;
        }

        /// <summary>
        /// Change existing order. Method depends on exchange's implementation, e.g., BitMEX changes existing order, while Poloniex cancels and places a new order.
        /// </summary>
        /// <param name="api">Exchange's API object</param>
        /// <param name="symbol">Instrument symbol</param>
        /// <param name="orderId">Order ID of the order to change</param>
        /// <param name="amount">New order quantity. Required for some exchanges</param>
        /// <param name="price">New price per qty. Required for some exchanges</param>
        /// <returns>The Result object</returns>
        public async Task<IResult> AmendOrderAsync(IApi api, string symbol, string orderId, decimal? amount, decimal? price)
        {
            log.Debug("AmendOrderAsync(" + api.ToString() + ", " + symbol + ", " + orderId + ", " + amount.ToString() + ", " + price.ToString() + ")");
            IResult res;
            if (string.IsNullOrEmpty(orderId))
            {
                res = new Result(false, "Order ID cannot be empty.");
            }
            else if (!IsTradeEnabled)
            {
                res = new Result(false, "Trading is not enabled.");
            }
            else
            {
                try
                {
                    amendInProgress++;
                    res = await api.AmendOrderAsync(symbol, orderId, amount, price);
                    if (res.Success)
                    {
                        log.Debug("AmendOrderAsync(" + orderId + ") Success => " + ((IOrder)res).OrderID);
                        if (HasOrder(api, orderId))
                        {
                            IOrder order = openOrders[api][orderId];
                            order.UpdateFrom((IOrder)res);
                            if (openOrders.ContainsKey(api) && !openOrders[api].ContainsKey(orderId))
                            {
                                //TODO: remove this block
                                //the order amended is not in the open order collection.....
                            }

                            if (order.OrderID != orderId) //the order ID was changed
                            {
                                RemoveOrder(api, orderId);
                                AddOrder(api, order);
                            }
                            Common.DBUtil.UpdateOrderAsync(order, price, amount);
                            /*
                            using (Tuna db = new Tuna())
                            {
                                var original = await db.Orders.FindAsync(order.ID);
                                if (original != null)
                                {
                                    if (price.HasValue)
                                        original.Price = price;
                                    if (amount.HasValue)
                                        original.Amount = amount;
                                    original.OrderId = order.OrderID;

                                    db.SaveChanges();
                                }
                            }*/
                        }
                    }                   
                }
                finally
                {
                    amendInProgress--;
                }
            }

            if(res != null && !res.Success)
                log.Debug("AmendOrderAsync(" + api.ToString() + ", " + symbol + ", " + orderId + ", " + amount.ToString() + ", " + price.ToString() + ") Failed: " + res.Message);

            return res;
        }

        /// <summary>
        /// Cancels an order.
        /// </summary>
        /// <param name="api">Exchange's API object</param>
        /// <param name="orderId">Orider ID of the order to be cancelled</param>
        /// <returns></returns>
        public async Task<IResult> CancelOrderAsync(IApi api, string orderId)
        {
            log.Debug("CancelOrderAsync(" + api.ToString() + ", " + orderId + ")");

            IResult res = null;
            if (string.IsNullOrEmpty(orderId))
            {
                res = new Result(false, "Order ID cannot be empty.");
            }
            else if (!IsTradeEnabled)
            {
                res = new Result(false, "Trading is not enabled.");
            }
            else
            {
                res = await api.CancelOrderAsync(orderId);
                if (res.Success)
                {
                    log.Debug("CancelOrderAsync(" + api.ToString() + ", " + orderId + ") Success");

                    if (openOrders.ContainsKey(api) && openOrders[api].ContainsKey(orderId))
                    {
                        //IOrder order = openOrders[api][orderId];
                        //using (Tuna db = new Tuna())
                        //{
                        //    var original = db.Orders.Find(order.ID);
                        //    if (original != null)
                        //    {
                        //        original.Status = "Canceled";
                        //        await Common.DBUtil.SaveDbAsync(db, false);
                        //    }
                        //}
                        RemoveOrder(api, orderId);
                    }
                }
            }
            if (res != null && !res.Success)
                log.Debug("CancelOrderAsync(" + api.ToString() + ", " + orderId + ") Failed: " + res.Message);

            return res;
        }

        /// <summary>
        /// Cancel all orders currently being managed by the Order Manager
        /// </summary>
        /// <returns></returns>
        public async Task CancelAllOrders()
        {
            if (!IsTradeEnabled)
                return;

            foreach (IApi api in openOrders.Keys)
            {
                string orderNumbers = string.Empty;
                foreach (string orderId in openOrders[api].Keys)
                {
                    if (!string.IsNullOrEmpty(orderNumbers))
                        orderNumbers = ",";
                    orderNumbers += orderId;
                }
                if (!string.IsNullOrEmpty(orderNumbers))
                {
                    await api.CancelOrderAsync(orderNumbers);
                }
            }
            openOrders.Clear();
        }

        private bool HasOrder(IApi api, string orderId)
        {
            return openOrders.ContainsKey(api) && openOrders[api].ContainsKey(orderId);
        }

        private void AddOrder(IApi api, IOrder order)
        {
            if (!openOrders.ContainsKey(api))
                openOrders[api] = new Dictionary<string, IOrder>();
            
            openOrders[api][order.OrderID] = order;
        }

        private void RemoveOrder(IApi api, string orderId)
        {
            if (openOrders.ContainsKey(api))
                openOrders[api].Remove(orderId);
        }
    }
}
