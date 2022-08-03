using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Windows.Forms;

namespace Chordata.Bex.Central.Common
{
    public sealed class WindowManager
    {
        #region Singleton
        private static volatile WindowManager instance;
        private static object syncRoot = new Object();
        private WindowManager() { }
        public static WindowManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new WindowManager();
                    }
                }

                return instance;
            }
        }
        #endregion

        public Form MdiParent { get; set; }

        private List<Form> lstWindows = new List<Form>();
        private void frm_FormClosed(object sender, FormClosedEventArgs e)
        {
            lstWindows.Remove((Form)sender);
            if (!((Form)sender).IsDisposed)
            {
                ((Form)sender).Dispose();
            }
        }

        public List<Form> FindForm(Type form)
        {
            List<Form> lstFrm = new List<Form>();
            for (int i = 0; i < lstWindows.Count; i++)
            {
                if (lstWindows[i].GetType() == form)
                    lstFrm.Add(lstWindows[i]);
            }
            return lstFrm;
        }

        public bool OpenOne(Form frm, Form mdiParent)
        {
            foreach (Form win in lstWindows)
            {
                if (win.GetType() == frm.GetType())
                {
                    win.Activate();
                    if (win.WindowState == FormWindowState.Minimized)
                        win.WindowState = FormWindowState.Normal;

                    return true;
                }
            }
            return OpenForm(frm, mdiParent);
        }

        public bool OpenOneTitle(Form frm, Form mdiParent)
        {
            foreach (object o in lstWindows)
            {
                if (o.GetType() == frm.GetType() && frm.Text == ((Form)o).Text)
                {
                    ((Form)o).Activate();
                    return true;
                }
            }
            return OpenForm(frm, mdiParent);
        }

        public bool OpenOneFromOwner(Form frm, Form owner)
        {
            foreach (object o in owner.OwnedForms)
            {
                if (o.GetType() == frm.GetType())
                {
                    ((Form)o).Activate();
                    return true;
                }
            }
            return OpenForm(frm, owner, false);
        }

        public bool OpenForm(Form frm, Form mdiParent)
        {
            if (mdiParent != null)
            {
                frm.MdiParent = mdiParent;
            }
            return OpenForm(frm, false);
        }

        public bool OpenForm(Form frm, Form owner, bool showDialog)
        {
            if (owner != null)
            {
                frm.Owner = owner;
            }
            return OpenForm(frm, showDialog);
        }

        public bool OpenForm(Form frm, bool showAsDialog)
        {
            lstWindows.Add(frm);
            frm.FormClosed += new FormClosedEventHandler(frm_FormClosed);
            if (showAsDialog)
            {
                frm.ShowDialog();
            }
            else
            {
                frm.Show();
            }
            return true;
        }

        public bool CloseForm(System.Windows.Forms.Form frm)
        {
            frm.Close();
            return true;
        }

        public void CloseAll()
        {
            for (int i = lstWindows.Count - 1; i >= 0; i--)
            {
                lstWindows[i].Close();
            }
        }
    }
}
