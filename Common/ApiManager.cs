using System;
using System.Linq;
using System.Collections.Generic;
using Chordata.Bex.Central.Data;
using Chordata.Bex.Api.Interface;

namespace Chordata.Bex.Central
{
    public sealed class ApiManager
    {
        public event EventHandler<string> OnMessage;
        
        #region Singleton
        private static volatile ApiManager instance;
        private static object syncRoot = new Object();

        private ApiManager() { }
        public static ApiManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new ApiManager();
                    }
                }

                return instance;
            }
        }
        #endregion

        private readonly IDictionary<Type, IApi> APIs = new Dictionary<Type, IApi>();
        
        private API QueryParameters(string name)
        {
            using (var db = new Tuna())
            {
                var query = from a in db.APIs
                            where a.Name == name
                            select a;
                return  query.First();
            }
        }

        public IApi GetApi(string name)
        {
            if (name.ToLower() == "bitmex")
                return GetApi<Api.BitMEX.BitMEXApi>();
            else if (name.ToLower() == "poloniex")
                return GetApi<Api.Poloniex.PoloniexApi>(); 
            else if (name.ToLower() == "bitfinex")
                return GetApi<Api.Bitfinex.BitfinexApi>();
            else if (name.ToLower() == "okcoin")
                return GetApi<Api.OKCoin.OKCoinApi>();
            else if (name.ToLower() == "okexfuture")
                return GetApi<Api.OKEXFuture.OKEXFutureApi>();
            return null;
        }

        public T GetApi<T>()
        {
            Type tp = typeof(T);
            if (APIs.ContainsKey(tp))
                return (T)APIs[tp];
            
            IApi api = null;
            if (tp == typeof(Api.Bitfinex.BitfinexApi))
            {
                API p = QueryParameters(Api.Bitfinex.BitfinexApi.GetName());
                api = new Api.Bitfinex.BitfinexApi(p.ApiUrl1,
                    p.ApiUrl2,
                    p.HeartbeatInterval.GetValueOrDefault(),
                    (ushort)p.MaxReconnect.GetValueOrDefault());
                api.SetApiKey(p.DefaultKey, p.DefaultSecret);
            }
            else if (tp == typeof(Api.BitMEX.BitMEXApi))
            {
                API p = QueryParameters(Api.BitMEX.BitMEXApi.GetName());
                api = new Api.BitMEX.BitMEXApi(p.ApiUrl1,
                    p.ApiUrl2,
                    p.HeartbeatInterval.GetValueOrDefault(),
                    (ushort)p.MaxReconnect.GetValueOrDefault());
                api.SetApiKey(p.DefaultKey, p.DefaultSecret);
            }
            else if (tp == typeof(Api.Poloniex.PoloniexApi))
            {
                API p = QueryParameters(Api.Poloniex.PoloniexApi.GetName());
                api = new Api.Poloniex.PoloniexApi(p.ApiUrl1,
                    p.ApiUrl2,
                    p.HeartbeatInterval.GetValueOrDefault(),
                    (ushort)p.MaxReconnect.GetValueOrDefault());
                api.SetApiKey(p.DefaultKey, p.DefaultSecret);
            }
         
            else if (tp == typeof(Api.OKCoin.OKCoinApi))
            {
                API p = QueryParameters(Api.OKCoin.OKCoinApi.GetName());
                api = new Api.OKCoin.OKCoinApi(p.ApiUrl1,
                    p.ApiUrl2,
                    p.HeartbeatInterval.GetValueOrDefault(),
                    (ushort)p.MaxReconnect.GetValueOrDefault());
                api.SetApiKey(p.DefaultKey, p.DefaultSecret);
            }
            else if (tp == typeof(Api.OKEXFuture.OKEXFutureApi))
            {
                API p = QueryParameters(Api.OKEXFuture.OKEXFutureApi.GetName());
                api = new Api.OKEXFuture.OKEXFutureApi(p.ApiUrl1,
                    p.ApiUrl2,
                    p.HeartbeatInterval.GetValueOrDefault(),
                    (ushort)p.MaxReconnect.GetValueOrDefault());
                api.SetApiKey(p.DefaultKey, p.DefaultSecret);
            }

            if (api != null)
            {
                
                api.OnConnectionEstablished += Api_OnConnectionEstablished;
                api.OnConnectionReconnect += Api_OnConnectionReconnect;
                api.OnConnectionClosed += Api_OnConnectionClosed;
                
                APIs[tp] = api;
            }

            return (T)api;
        }

        private void Api_OnMessage(object sender, Api.ApiMessageEventArgs e)
        {
            OnMessage?.Invoke(this, "[Api Man] " + sender.ToString() + " " + e.ApiMessage );
        }

        private void Api_OnConnectionClosed(object sender, EventArgs e)
        {
            OnMessage?.Invoke(this, "[Api Man] " + sender.ToString() + " disconnected.");
        }

        private void Api_OnConnectionEstablished(object sender, EventArgs e)
        {
            OnMessage?.Invoke(this, "[Api Man] Connected to " + sender.ToString() + ".");
        }

        private void Api_OnConnectionReconnect(object sender, EventArgs e)
        {
            OnMessage?.Invoke(this, "[Api Man] Reconnecting to " + sender.ToString() + "...");
        }

        public void DisconnectAll()
        {
            foreach (var api in APIs)
            {
                if (api.Value.IsConnected)
                    api.Value.Disconnect();
            }
        }

        public IApi ApiConnect<T>()
        {
            IApi api = (IApi)GetApi<T>();
            if(api != null)
            {
                if (!api.IsConnected)
                {
                    OnMessage?.Invoke(this, "[Api Man] Connecting to " + api.ToString() + "...");
                    api.Connect(true);                    
                }
            }
            return api;         
        }

        public void ApiReconnect<T>()
        {
            IApi api = (IApi)GetApi<T>();
            if (api != null)
            {
                OnMessage?.Invoke(this, "[Api Man] Reconnecting to " + api.ToString() + "...");
                api.Reconnect();
            }
        }
    }
}
    