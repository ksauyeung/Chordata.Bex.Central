using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Chordata.Bex.Central.Common;
using Chordata.Bex.Central.Interface;

namespace Chordata.Bex.Central
{
    public partial class FrmStrategy : Form
    {
        public FrmStrategy()
        {
            InitializeComponent();
        }
        
        private void FrmStrategy_Load(object sender, EventArgs e)
        {
            if (RunManager.Instance.IsStarted("Maker-Taker"))
                btnMakerTaker.Text = "Stop";

            if (RunManager.Instance.IsStarted("Maker-Taker-Left"))
                btnMakerTaker.Text = "Stop";


            if (RunManager.Instance.IsStarted("Taker-Taker"))
                btnTakerTaker.Text = "Stop";



        }

        private void btnTakerTaker_Click(object sender, EventArgs e)
        {
            btnTakerTaker.Enabled = false;
            IStrategy s;

            if (btnTakerTaker.Text == "Start")
            {
                s = RunManager.Instance.Start("Taker-Taker");
                List<Form> forms = WindowManager.Instance.FindForm(typeof(FrmMessage));
                foreach (FrmMessage frm in forms)
                    s.OnMessage += frm.GetMessageListener();

                btnTakerTaker.Text = "Stop";
            }
            else
            {
                RunManager.Instance.Stop("Taker-Taker");
                btnTakerTaker.Text = "Start";
            }
            btnTakerTaker.Enabled = true;
        }

        private void btnMakerMaker_Click(object sender, EventArgs e)
        {
            btnMakerTaker.Enabled = false;
            IStrategy s;
            
            if (btnMakerTaker.Text == "Start")
            {                
                s = RunManager.Instance.Start("Maker-Taker");

                List<Form> forms = WindowManager.Instance.FindForm(typeof(FrmMessage));
                foreach (FrmMessage frm in forms)
                    s.OnMessage += frm.GetMessageListener();

                btnMakerTaker.Text = "Stop";                
            }
            else
            {
                RunManager.Instance.Stop("Maker-Taker");           
                btnMakerTaker.Text = "Start";
            }
            btnMakerTaker.Enabled = true;
        }

        private void btnMakerTakerLeft_Click(object sender, EventArgs e)
        {
            btnMakerTakerLeft.Enabled = false;
            IStrategy s;

            if (btnMakerTakerLeft.Text == "Start")
            {
                s = RunManager.Instance.Start("Maker-Taker-Left");

                List<Form> forms = WindowManager.Instance.FindForm(typeof(FrmMessage));
                foreach (FrmMessage frm in forms)
                    s.OnMessage += frm.GetMessageListener();

                btnMakerTakerLeft.Text = "Stop";
            }
            else
            {
                RunManager.Instance.Stop("Maker-Taker-Left");
                btnMakerTakerLeft.Text = "Start";
            }
            btnMakerTakerLeft.Enabled = true;
        }
    }
}
