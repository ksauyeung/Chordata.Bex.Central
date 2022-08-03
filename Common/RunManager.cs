using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chordata.Bex.Central.Interface;

namespace Chordata.Bex.Central
{
    internal sealed class RunManager
    {
        private static volatile RunManager instance;
        private static object syncRoot = new Object();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        internal RunManager()
        {

        }
        internal static RunManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new RunManager();
                    }
                }

                return instance;
            }
        }

        private Dictionary<string, IStrategy> ops = new Dictionary<string, IStrategy>(2);

        //TODO: Remove the string literals
        internal IStrategy Start(string name)
        {
            lock (syncRoot)
            {
                try
                {
                    if (name == "Maker-Taker")
                    {
                        //MakerTaker4 s = new MakerTaker4();
                        MakerTaker5 s = new MakerTaker5();
                        s.Start();
                        ops[name] = s;
                        return s;
                    }
                    else if (name == "Maker-Taker-Left")
                    {
                        MakerTakerLeft s = new MakerTakerLeft();
                        s.Start();
                        ops[name] = s;
                        return s;
                    }
                    else if (name == "Taker-Taker")
                    {
                        TakerTaker3 s = new TakerTaker3();
                        s.Start();

                        ops[name] = s;
                        return s;
                    }
                  
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    log.Error(ex.StackTrace);
                    log.Error(ex.Source);
                    if (ex.Data.Count > 0)
                    {
                        log.Error("  Extra details:");
                        foreach (System.Collections.DictionaryEntry de in ex.Data)
                            log.Error(string.Format("    Key: {0,-20}      Value: {1}",
                                              "'" + de.Key.ToString() + "'", de.Value));
                    }

                    if (ex.InnerException != null)
                    {
                        log.Error(ex.InnerException.Message);
                        log.Error(ex.InnerException.StackTrace);
                        log.Error(ex.InnerException.Source);
                    }
                    throw;
                }
            }            
            return null;
        }

        internal async void Stop(string name)
        {
            if (ops.ContainsKey(name))
            {

                await ops[name].Stop();
                ops[name].Dispose();                
                ops.Remove(name);
            }
        }

        internal void StopAll()
        {
            foreach (IStrategy s in ops.Values)
            {
                s.Stop();
            }
            ops.Clear();
        }

        internal bool IsStarted(string name)
        {
            return ops.ContainsKey(name);

        }

    }
}
