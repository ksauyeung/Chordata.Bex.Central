using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace Chordata.Bex.Central
{
    static class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new Main());
            }
            catch (Exception e)
            {

                log.Debug("Main Catch-All...\n" +
                    e.Message +
                    "\n" + e.StackTrace +
                    "\n" + e.Source +
                    "\n" + e.TargetSite);
                if (e.InnerException != null)
                {
                    log.Debug("Inner:\n" +
                    e.InnerException.Message +
                    "\n" + e.InnerException.StackTrace +
                    "\n" + e.InnerException.Source +
                    "\n" + e.InnerException.TargetSite);
                }
                throw;
            }            
        }
    }
}
    