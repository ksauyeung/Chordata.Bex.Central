//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Chordata.Bex.Central.Data
{
    using System;
    using System.Collections.Generic;
    
    public partial class API
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ApiUrl1 { get; set; }
        public string ApiUrl2 { get; set; }
        public string DefaultKey { get; set; }
        public string DefaultSecret { get; set; }
        public Nullable<int> HeartbeatInterval { get; set; }
        public Nullable<int> MaxReconnect { get; set; }
    }
}
