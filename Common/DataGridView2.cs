using System;
using System.Windows.Forms;

namespace Chordata.Bex.Central.Common
{
    public class DataGridView2 : DataGridView
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            try
            {
                if (e.Modifiers == Keys.Control)
                {
                    switch (e.KeyCode)
                    {

                        case Keys.V:
                            PasteClipboard();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Copy/paste operation failed. " + ex.Message, "Copy/Paste", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            base.OnKeyDown(e);
        }

        private void PasteClipboard()
        {
            try
            {
                string s = Clipboard.GetText();
                string[] lines = s.Split('\n');

                int iRow = this.CurrentCell.RowIndex;
                int iCol = this.CurrentCell.ColumnIndex;
                DataGridViewCell oCell;
                if (iRow + lines.Length > this.Rows.Count - 1)
                {
                    bool bFlag = false;
                    foreach (string sEmpty in lines)
                    {
                        if (sEmpty == "")
                        {
                            bFlag = true;
                        }
                    }

                    int iNewRows = iRow + lines.Length - this.Rows.Count;
                    if (iNewRows > 0)
                    {
                        if (bFlag)
                            this.Rows.Add(iNewRows);
                        else
                            this.Rows.Add(iNewRows + 1);
                    }
                    else
                        this.Rows.Add(iNewRows + 1);
                }
                foreach (string line in lines)
                {
                    if (iRow < this.RowCount && line.Length > 0)
                    {
                        string[] sCells = line.Split('\t');
                        for (int i = 0; i < sCells.GetLength(0); ++i)
                        {
                            if (iCol + i < this.ColumnCount)
                            {
                                oCell = this[iCol + i, iRow];
                                oCell.Value = Convert.ChangeType(sCells[i].Replace("\r", ""), oCell.ValueType);
                            }
                            else
                            {
                                break;
                            }
                        }
                        iRow++;
                    }
                    else
                    {
                        break;
                    }
                }
                Clipboard.Clear();
            }
            catch (FormatException)
            {
                MessageBox.Show("The data you pasted is in the wrong format for the cell");
                return;
            }
        }
    }
}
