using Chordata.Bex.Api.Interface;
using Chordata.Bex.Central.Data;
using System;

using System.Threading.Tasks;

namespace Chordata.Bex.Central.Common
{
    internal class DBUtil
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        internal static async Task SaveDbAsync(Tuna db, bool ignoreFailure = false)
        {            
            try
            {                
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {                
                log.Error(e.Message + "\n" + e.StackTrace);
                if (!ignoreFailure)
                    throw;
            }            
        }

        internal static int SaveDb(Tuna db, bool ignoreFailure = false)
        {
            int i = 0;
            try
            {
                i = db.SaveChanges();
            }
            catch (Exception e)
            {
                log.Error(e.Message + "\n" + e.StackTrace);
                if (!ignoreFailure)
                    throw;
            }
            return i;
        }

        internal static Task<bool> AddOrderAsyc(Order newOrder, IOrder order)
        {
            return Task.Factory.StartNew(() =>
            {
                bool success = false;             
                using (Tuna db = new Tuna())
                {
                    db.Orders.Add(newOrder);
                    success = db.SaveChanges() > 0;
                }
                order.ID = newOrder.Id;
                return success;
            }
            );     
        }

        internal static Task<bool> UpdateOrderAsync(IOrder order, decimal? price, decimal? amount)
        {
            return Task.Factory.StartNew(() =>
            {
                int i = 0;
                using (Tuna db = new Tuna())
                {
                    var original = db.Orders.FindAsync(order.ID).Result;
                    if (original != null)
                    {
                        if (price.HasValue)
                            original.Price = price;
                        if (amount.HasValue)
                            original.Amount = amount;
                        original.OrderId = order.OrderID;

                        i = db.SaveChanges();
                    }
                }
                return i > 0;
            });
        }

        internal static Task<bool> WriteRunRecords(Run[] records)
        {
            return Task.Factory.StartNew(() =>
            {
                bool success = true;
                bool mod = false;
                using (Tuna db = new Tuna())
                {
                    for (int i = 0; i < records.Length; i++)
                    {
                        Run rec = records[i];
                        if (rec != null)
                        {
                            db.Entry(rec).State = System.Data.Entity.EntityState.Modified;
                            mod = true;
                        }
                    }
                    if (mod)
                    {
                        try
                        {
                            db.SaveChanges();
                        }
                        catch (Exception e)
                        {
                            log.Error(e.Message + "\n" + e.StackTrace);
                            success = false;
                        }
                    }
                }
                return success;
            });        
        }
    }
}
