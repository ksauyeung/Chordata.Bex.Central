using System;
using System.Globalization;
using System.Media;


namespace Chordata.Bex.Central.Common
{
    public static class Helper
    {
        private const int DoubleRoundingPrecisionDigits = 8;
        internal static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public static void ClearForm(System.Windows.Forms.Control parent)
        {
            foreach (System.Windows.Forms.Control ctrControl in parent.Controls)
            {
                //Loop through all controls 
                if (object.ReferenceEquals(ctrControl.GetType(), typeof(System.Windows.Forms.TextBox)))
                {
                    //Check to see if it's a textbox 
                    ((System.Windows.Forms.TextBox)ctrControl).Text = string.Empty;
                    //If it is then set the text to String.Empty (empty textbox) 
                }
                else if (object.ReferenceEquals(ctrControl.GetType(), typeof(System.Windows.Forms.RichTextBox)))
                {
                    //If its a RichTextBox clear the text
                    ((System.Windows.Forms.RichTextBox)ctrControl).Text = string.Empty;
                }
                else if (object.ReferenceEquals(ctrControl.GetType(), typeof(System.Windows.Forms.ComboBox)))
                {
                    //Next check if it's a dropdown list 
                    ((System.Windows.Forms.ComboBox)ctrControl).SelectedIndex = -1;
                    //If it is then set its SelectedIndex to 0 
                }
                else if (object.ReferenceEquals(ctrControl.GetType(), typeof(System.Windows.Forms.CheckBox)))
                {
                    //Next uncheck all checkboxes
                    ((System.Windows.Forms.CheckBox)ctrControl).Checked = false;
                }
                else if (object.ReferenceEquals(ctrControl.GetType(), typeof(System.Windows.Forms.RadioButton)))
                {
                    //Unselect all RadioButtons
                    ((System.Windows.Forms.RadioButton)ctrControl).Checked = false;
                }
                if (ctrControl.Controls.Count > 0)
                {
                    //Call itself to get all other controls in other containers 
                    ClearForm(ctrControl);
                }
            }
        }

        public static void PrependText(string text, object textbox)
        {
            if (textbox is System.Windows.Forms.TextBox)
            {
                System.Windows.Forms.TextBox o = (System.Windows.Forms.TextBox)textbox;
                o.PerformSafely(() => o.Text = text + Environment.NewLine + o.Text);
            }
            else if (textbox is System.Windows.Forms.RichTextBox)
            {
                System.Windows.Forms.RichTextBox o = (System.Windows.Forms.RichTextBox)textbox;
                o.PerformSafely(() => o.Text = text + Environment.NewLine + o.Text);
            }
        }

        public static decimal Normalize(this decimal value)
        {
            return Math.Round(value, DoubleRoundingPrecisionDigits, MidpointRounding.AwayFromZero);
        }

        public static string ToStringNormalized(this decimal value)
        {
            return value.ToString("0." + new string('#', DoubleRoundingPrecisionDigits), InvariantCulture);
        }


        /// <summary>
        /// Get the top order book price for a given quantity
        /// </summary>
        /// <param name="orderBook">The orderbook</param>
        /// <param name="side">Side of the orderbook to query</param>
        /// <param name="qtyNeeded">Total quantity needed</param>
        /// <param name="topPrice">The lowest bid or highest ask that will fulfill all quantities needed</param>
        /// <param name="totalPrice">Total price needed to execute all quantities needed</param>
        /// <returns>True if prices are calculated, false if not enough quantities on order book</returns>
        public static bool GetTopPrice(Api.Interface.IOrderBook orderBook, Api.Side side, decimal qtyNeeded, out decimal topPrice, out decimal totalPrice, out decimal avgPrice)
        {
            topPrice = 0.0M;
            totalPrice = 0.0M;
            avgPrice = 0.0M;
            decimal cumulativeQty = 0.0M;

            System.Collections.Generic.IDictionary<decimal, decimal> quotes;

            if (side == Api.Side.Buy)
                quotes = orderBook.Bids;
            else if (side == Api.Side.Sell)
                quotes = orderBook.Asks;
            else
                return false;

            bool priceGotten = false; ushort retry = 0;
            while (!priceGotten)
            {
                try
                {
                    foreach (var quote in quotes)
                    {
                        cumulativeQty += quote.Value;
                        totalPrice += (quote.Key * quote.Value);
                        if (cumulativeQty >= qtyNeeded) //substract the extra
                        {
                            topPrice = quote.Key;
                            totalPrice -= (cumulativeQty - qtyNeeded) * quote.Key;
                            if (qtyNeeded > 0.0M)
                                avgPrice = totalPrice / qtyNeeded;
                            else
                                avgPrice = topPrice;

                            return true;
                        }
                    }
                }
                catch(InvalidOperationException)
                {

                    if (retry > 5)
                        return false;
                        //throw;
                }
                retry++;
            }
            return false;
        }
        internal static void PlaySfx(Sfx sound)
        {
            if (sound == Sfx.Alert)
            {
                System.Media.SoundPlayer audio = new System.Media.SoundPlayer(Properties.Resources.ring); // here WindowsFormsApplication1 is the namespace and Connect is the audio file name
                audio.Play();
            }
        }


    }
}
