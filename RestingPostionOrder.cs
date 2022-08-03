using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Chordata.Bex.Api.Interface;
using Chordata.Bex.Api.BitMEX.Model;
using Chordata.Bex.Api.BitMEX;
using Chordata.Bex.Api;

namespace Chordata.Bex.Central
{
    /// <summary>
    /// An order that is self maintained at a set orderbook position.
    /// </summary>
    public class RestingPositionOrder : Order
    {
        public IApi API { get; set; }

       
        public int Level { get; set; }

        public RestingPositionOrder()
        {
        }
       
        /// <summary>
        /// Creates and post a new resting order
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="side">Buy or Sell</param>
        /// <param name="orderType">Order type</param>
        /// <param name="quoteAmount">Order Qty</param>
        /// <param name="pricePerCoin">Price</param>
        /// /// <param name="position">Position on order book.</param>
        /// <returns></returns>
        public async static Task<RestingPositionOrder> Create(string symbol, Side side, OrderType orderType, decimal quoteAmount, decimal pricePerCoin, int position)
        {
            IResult result = null;

            BitMEXApi bitMex = ApiManager.Instance.GetApi<BitMEXApi>();

            if (side == Side.Buy)
            {
                //result = await bitMex.PostBuyOrderAsync(symbol, orderType, quoteAmount, pricePerCoin);
                result = await OrderManager.Instance.PostBuyOrderAsync(bitMex, symbol, orderType, quoteAmount, pricePerCoin);
            }
            else if (side == Side.Sell)
            {
                //result = await bitMex.PostSellOrderAsync(symbol, orderType, quoteAmount, pricePerCoin);
                result = await OrderManager.Instance.PostSellOrderAsync(bitMex, symbol, orderType, quoteAmount, pricePerCoin);
            }            
            if (result != null && result.Success)
            {
                RestingPositionOrder rpOrder = new RestingPositionOrder();
                rpOrder.UpdateFrom((Order)result);
                rpOrder.Level = position;
                return rpOrder;
            }
            return null;
        }

        public void Start()
        {
            API.OnOrderBookChanged += API_OnOrderBookChanged;
        }

        public void Stop()
        {
            API.OnOrderBookChanged -= API_OnOrderBookChanged;
        }    

        private async void API_OnOrderBookChanged(object sender, OrderBookChangedEventArgs e)
        {
            if (e.OrderBook.Symbol == this.Symbol)
            {
                decimal a = 0.0M, b = 0.0M;
                IDictionary<decimal, decimal> orders = null;
                if (Side == Side.Sell)
                    orders = e.OrderBook.Asks;
                else
                    orders = e.OrderBook.Bids;

                if (orders.Count <= 1)
                    return;


                if (Level <= 1)
                    a = orders.ElementAt(0).Key;
                else
                    a = orders.ElementAt(Level - 2).Key;

                if (Level > orders.Count)
                    b = orders.ElementAt(orders.Count - 1).Key;
                else
                    b = orders.ElementAt(Level -1).Key;


                if ((Side == Side.Sell && this.Price > a && this.Price <= b) ||
                    (Side == Side.Buy && this.Price < a && this.Price >= b))
                {
                    //ok
                }
                else
                {
                    //calculate new price
                    decimal newPrice = (a + b) / 2;
                    newPrice = Math.Round(newPrice, 8);
                    if (newPrice > a && newPrice <= b)
                    {                        
                        IResult result = await ApiManager.Instance.GetApi<BitMEXApi>().AmendOrderAsync(this.Symbol, this.OrderID, this.OrderQty, newPrice);
                        if (string.IsNullOrEmpty(result.Message))
                        {                            
                            UpdateFrom((Order)result);
                            return;
                        }
                        else
                        {
                            //PrependText(result.Message, tbMessages);
                        }                        
                    }
                }
            }
        }
    }
}
