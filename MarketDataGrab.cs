using System.Timers;
using System.Linq;
using System.Threading.Tasks;
using Chordata.Bex.Central.Data;
using Chordata.Bex.Api.Interface;
using System;

namespace Chordata.Bex.Central
{

    internal class MarketDataGrab
    {
        string[] SUBSCRIBE_BITMEX = {"ETC7D", "ETHU17", "LTCU17", "XRPU17"};
        string[] SUBSCRIBE_POLONIEX = { "BTC_ETH", "BTC_ETC", "BTC_LTC", "BTC_XRP" };


        int INTERVAL_MS = 60000;
        Timer timer;
        ApiManager am = ApiManager.Instance;

        internal MarketDataGrab()
        {
           
        }

        internal async void Start()
        {
            IApi api;

            api = am.GetApi("BitMEX");
            api.Connect(false);
            foreach (string s in SUBSCRIBE_BITMEX)
            {                
                api.SubscribeToTickerAsync(s);
                api.SubscribeToOrderBookAsync(s);
            }
            api = am.GetApi("Poloniex");
            api.Connect(false);
            foreach (string s in SUBSCRIBE_POLONIEX)
            {
                api.SubscribeToTickerAsync(s);
                api.SubscribeToOrderBookAsync(s);
            }

            while(!IsReady())
                await Task.Delay(1000);

            timer = new Timer(INTERVAL_MS);
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        internal void Stop()
        {
            timer.Stop();
        }

        private bool IsReady()
        {
            IApi api = am.GetApi("BitMEX");
            foreach (string s in SUBSCRIBE_BITMEX)
            {
                if (api.GetOrderBook(s) == null)
                    return false;
            }

            api = am.GetApi("Poloniex");
            foreach (string s in SUBSCRIBE_POLONIEX)
            {

                if (api.GetOrderBook(s) == null)
                    return false;
            }
            return true;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                WriteMarketData();
            }
            catch
            {
            }
        }        

        private async void WriteMarketData()
        {
            using (Tuna db = new Tuna())
            {

                IApi api = am.GetApi("BitMEX");
                IOrderBook ob;
                IMarketData ticker;
                foreach (string s in SUBSCRIBE_BITMEX)
                {
                    ticker = api.GetTicker(s);
                    ob = api.GetOrderBook(s);

                    MarketHistory mh = new MarketHistory();
                    mh.Timestamp = DateTime.UtcNow;
                    mh.Exchange = api.ToString();
                    mh.Symbol = s;
                    if(ticker != null)
                        mh.TickerPrice = ticker.LastPrice;

                    if (ob.Asks.Count > 0) mh.AskPx0 = ob.Asks.ElementAt(0).Key;
                    if (ob.Asks.Count > 1) mh.AskPx1 = ob.Asks.ElementAt(1).Key;
                    if (ob.Asks.Count > 2) mh.AskPx2 = ob.Asks.ElementAt(2).Key;
                    if (ob.Asks.Count > 3) mh.AskPx3 = ob.Asks.ElementAt(3).Key;
                    if (ob.Asks.Count > 4) mh.AskPx4 = ob.Asks.ElementAt(4).Key;
                    if (ob.Asks.Count > 5) mh.AskPx5 = ob.Asks.ElementAt(5).Key;
                    if (ob.Asks.Count > 6) mh.AskPx6 = ob.Asks.ElementAt(6).Key;
                    if (ob.Asks.Count > 7) mh.AskPx7 = ob.Asks.ElementAt(7).Key;
                    if (ob.Asks.Count > 8) mh.AskPx8 = ob.Asks.ElementAt(8).Key;
                    if (ob.Asks.Count > 9) mh.AskPx9 = ob.Asks.ElementAt(9).Key;

                    if (ob.Asks.Count > 0) mh.AskSz0 = ob.Asks.ElementAt(0).Value;
                    if (ob.Asks.Count > 1) mh.AskSz1 = ob.Asks.ElementAt(1).Value;
                    if (ob.Asks.Count > 2) mh.AskSz2 = ob.Asks.ElementAt(2).Value;
                    if (ob.Asks.Count > 3) mh.AskSz3 = ob.Asks.ElementAt(3).Value;
                    if (ob.Asks.Count > 4) mh.AskSz4 = ob.Asks.ElementAt(4).Value;
                    if (ob.Asks.Count > 5) mh.AskSz5 = ob.Asks.ElementAt(5).Value;
                    if (ob.Asks.Count > 6) mh.AskSz6 = ob.Asks.ElementAt(6).Value;
                    if (ob.Asks.Count > 7) mh.AskSz7 = ob.Asks.ElementAt(7).Value;
                    if (ob.Asks.Count > 8) mh.AskSz8 = ob.Asks.ElementAt(8).Value;
                    if (ob.Asks.Count > 9) mh.AskSz9 = ob.Asks.ElementAt(9).Value;

                    if (ob.Bids.Count > 0) mh.BidPx0 = ob.Bids.ElementAt(0).Key;
                    if (ob.Bids.Count > 1) mh.BidPx1 = ob.Bids.ElementAt(1).Key;
                    if (ob.Bids.Count > 2) mh.BidPx2 = ob.Bids.ElementAt(2).Key;
                    if (ob.Bids.Count > 3) mh.BidPx3 = ob.Bids.ElementAt(3).Key;
                    if (ob.Bids.Count > 4) mh.BidPx4 = ob.Bids.ElementAt(4).Key;
                    if (ob.Bids.Count > 5) mh.BidPx5 = ob.Bids.ElementAt(5).Key;
                    if (ob.Bids.Count > 6) mh.BidPx6 = ob.Bids.ElementAt(6).Key;
                    if (ob.Bids.Count > 7) mh.BidPx7 = ob.Bids.ElementAt(7).Key;
                    if (ob.Bids.Count > 8) mh.BidPx8 = ob.Bids.ElementAt(8).Key;
                    if (ob.Bids.Count > 9) mh.BidPx9 = ob.Bids.ElementAt(9).Key;

                    if (ob.Bids.Count > 0) mh.BidSz0 = ob.Bids.ElementAt(0).Value;
                    if (ob.Bids.Count > 1) mh.BidSz1 = ob.Bids.ElementAt(1).Value;
                    if (ob.Bids.Count > 2) mh.BidSz2 = ob.Bids.ElementAt(2).Value;
                    if (ob.Bids.Count > 3) mh.BidSz3 = ob.Bids.ElementAt(3).Value;
                    if (ob.Bids.Count > 4) mh.BidSz4 = ob.Bids.ElementAt(4).Value;
                    if (ob.Bids.Count > 5) mh.BidSz5 = ob.Bids.ElementAt(5).Value;
                    if (ob.Bids.Count > 6) mh.BidSz6 = ob.Bids.ElementAt(6).Value;
                    if (ob.Bids.Count > 7) mh.BidSz7 = ob.Bids.ElementAt(7).Value;
                    if (ob.Bids.Count > 8) mh.BidSz8 = ob.Bids.ElementAt(8).Value;
                    if (ob.Bids.Count > 9) mh.BidSz9 = ob.Bids.ElementAt(9).Value;
                    db.MarketHistories.Add(mh);
                }

                api = am.GetApi("Poloniex");
                foreach (string s in SUBSCRIBE_POLONIEX)
                {
                    ticker = api.GetTicker(s);
                    ob = api.GetOrderBook(s);

                    MarketHistory mh = new MarketHistory();
                    mh.Timestamp = DateTime.UtcNow;
                    mh.Exchange = api.ToString();
                    mh.Symbol = s;
                    if (ticker != null)
                        mh.TickerPrice = ticker.LastPrice;

                    if (ob.Asks.Count > 0) mh.AskPx0 = ob.Asks.ElementAt(0).Key;
                    if (ob.Asks.Count > 1) mh.AskPx1 = ob.Asks.ElementAt(1).Key;
                    if (ob.Asks.Count > 2) mh.AskPx2 = ob.Asks.ElementAt(2).Key;
                    if (ob.Asks.Count > 3) mh.AskPx3 = ob.Asks.ElementAt(3).Key;
                    if (ob.Asks.Count > 4) mh.AskPx4 = ob.Asks.ElementAt(4).Key;
                    if (ob.Asks.Count > 5) mh.AskPx5 = ob.Asks.ElementAt(5).Key;
                    if (ob.Asks.Count > 6) mh.AskPx6 = ob.Asks.ElementAt(6).Key;
                    if (ob.Asks.Count > 7) mh.AskPx7 = ob.Asks.ElementAt(7).Key;
                    if (ob.Asks.Count > 8) mh.AskPx8 = ob.Asks.ElementAt(8).Key;
                    if (ob.Asks.Count > 9) mh.AskPx9 = ob.Asks.ElementAt(9).Key;

                    if (ob.Asks.Count > 0) mh.AskSz0 = ob.Asks.ElementAt(0).Value;
                    if (ob.Asks.Count > 1) mh.AskSz1 = ob.Asks.ElementAt(1).Value;
                    if (ob.Asks.Count > 2) mh.AskSz2 = ob.Asks.ElementAt(2).Value;
                    if (ob.Asks.Count > 3) mh.AskSz3 = ob.Asks.ElementAt(3).Value;
                    if (ob.Asks.Count > 4) mh.AskSz4 = ob.Asks.ElementAt(4).Value;
                    if (ob.Asks.Count > 5) mh.AskSz5 = ob.Asks.ElementAt(5).Value;
                    if (ob.Asks.Count > 6) mh.AskSz6 = ob.Asks.ElementAt(6).Value;
                    if (ob.Asks.Count > 7) mh.AskSz7 = ob.Asks.ElementAt(7).Value;
                    if (ob.Asks.Count > 8) mh.AskSz8 = ob.Asks.ElementAt(8).Value;
                    if (ob.Asks.Count > 9) mh.AskSz9 = ob.Asks.ElementAt(9).Value;

                    if (ob.Bids.Count > 0) mh.BidPx0 = ob.Bids.ElementAt(0).Key;
                    if (ob.Bids.Count > 1) mh.BidPx1 = ob.Bids.ElementAt(1).Key;
                    if (ob.Bids.Count > 2) mh.BidPx2 = ob.Bids.ElementAt(2).Key;
                    if (ob.Bids.Count > 3) mh.BidPx3 = ob.Bids.ElementAt(3).Key;
                    if (ob.Bids.Count > 4) mh.BidPx4 = ob.Bids.ElementAt(4).Key;
                    if (ob.Bids.Count > 5) mh.BidPx5 = ob.Bids.ElementAt(5).Key;
                    if (ob.Bids.Count > 6) mh.BidPx6 = ob.Bids.ElementAt(6).Key;
                    if (ob.Bids.Count > 7) mh.BidPx7 = ob.Bids.ElementAt(7).Key;
                    if (ob.Bids.Count > 8) mh.BidPx8 = ob.Bids.ElementAt(8).Key;
                    if (ob.Bids.Count > 9) mh.BidPx9 = ob.Bids.ElementAt(9).Key;

                    if (ob.Bids.Count > 0) mh.BidSz0 = ob.Bids.ElementAt(0).Value;
                    if (ob.Bids.Count > 1) mh.BidSz1 = ob.Bids.ElementAt(1).Value;
                    if (ob.Bids.Count > 2) mh.BidSz2 = ob.Bids.ElementAt(2).Value;
                    if (ob.Bids.Count > 3) mh.BidSz3 = ob.Bids.ElementAt(3).Value;
                    if (ob.Bids.Count > 4) mh.BidSz4 = ob.Bids.ElementAt(4).Value;
                    if (ob.Bids.Count > 5) mh.BidSz5 = ob.Bids.ElementAt(5).Value;
                    if (ob.Bids.Count > 6) mh.BidSz6 = ob.Bids.ElementAt(6).Value;
                    if (ob.Bids.Count > 7) mh.BidSz7 = ob.Bids.ElementAt(7).Value;
                    if (ob.Bids.Count > 8) mh.BidSz8 = ob.Bids.ElementAt(8).Value;
                    if (ob.Bids.Count > 9) mh.BidSz9 = ob.Bids.ElementAt(9).Value;
                    db.MarketHistories.Add(mh);
                }
                await db.SaveChangesAsync();
            }

        }
    }
}
