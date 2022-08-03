using System;
using Chordata.Bex.Api.Interface;
using Chordata.Bex.Api;
namespace Chordata.Bex.Central
{
    public class Result : IResult
    {        
        public Result()
        {
            Message = "";
            Success = false;
        }
        public Result(bool isSuccess, string message)
        {
            Message = message;
            Success = isSuccess;
        }
        public string Message { get; set; }
        public bool Success { get; set; }
        
        public ErrorCode ErrorCode { get; }

    }
}
