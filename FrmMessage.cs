using System;
using System.Windows.Forms;
using Chordata.Bex.Central.Common;
using Chordata.Bex.Central.Interface;

namespace Chordata.Bex.Central
{
    public partial class FrmMessage : Form
    {
        object sync = new object();
        ApiManager apiMan = ApiManager.Instance;

        public FrmMessage()
        {
            InitializeComponent();

            apiMan.OnMessage += Exchange_OnMessage;
        }

        private void FrmMessage_Load(object sender, EventArgs e)
        {
            //start all strategies here
            //TODO: remove string literals
            //IStrategy s;
                
            //s = RunManager.Instance.Start("Taker-Taker");
            //if (s != null) s.OnMessage += Exchange_OnMessage;

            //s = RunManager.Instance.Start("Maker-Taker");
            //if (s != null) s.OnMessage += Exchange_OnMessage;
        }

     
        private void Exchange_OnMessage(object sender, string e)
        {
            Helper.PrependText("[" + DateTime.UtcNow.ToString() + "] " + e, tbMessage);
        }

        public EventHandler<string> GetMessageListener()
        {
            return new EventHandler<string>(Exchange_OnMessage);
        }
        
        private void FrmMessage_FormClosing(object sender, FormClosingEventArgs e)
        {
            RunManager.Instance.StopAll();
        }
    }
}
