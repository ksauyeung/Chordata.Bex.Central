namespace Chordata.Bex.Central
{
    public enum RunStatus
    {        
        Ready = 0,
        Stopped = 1,        
        Running = 2,
        Awaiting = 6,
        Completed = 10,
        CompletedWithError = 11,
        Fault = 99
    }

    public enum Sfx
    {
        Alert,
        Warning,        
        Error
    }

    public enum PricingScheme
    {          
        None,
        /// <summary>
        /// New price is calculated X percentage of new price from the target price. ( NewPx = TargetPx - NewPX * Rate) Opening buy / Closing sell
        /// </summary>
        PercentageOfNewPrice,

        /// <summary>
        /// New price is calculated based on the target price ( NewPx = TargetPx + TargetPx * Rate) Opening sell / Closing buy
        /// </summary>
        PercentageOfTarget
    }
}
