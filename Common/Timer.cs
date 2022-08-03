namespace Chordata.Bex.Central.Common
{
    public class Timer : System.Timers.Timer
    {
        public Timer(double interval) : base(interval) { }
        public Timer() : base() { }
        /// <summary>
        /// Any related object
        /// </summary>
        public object Tag { get; set; }
    }
}
