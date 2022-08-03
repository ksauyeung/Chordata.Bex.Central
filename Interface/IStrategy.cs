using System;
using System.Threading.Tasks;

namespace Chordata.Bex.Central.Interface
{
    internal interface IStrategy : IDisposable
    {
        void Start();

        Task Stop();

        event EventHandler<string> OnMessage;

    }
}
