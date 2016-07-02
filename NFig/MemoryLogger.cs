using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NFig
{
    public class MemoryLogger<TTier, TDataCenter> : SettingsLogger<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        private readonly List<AppSnapshot<TTier, TDataCenter>> _history = new List<AppSnapshot<TTier, TDataCenter>>();

        public MemoryLogger(Action<Exception, AppSnapshot<TTier, TDataCenter>> onLogException) : base(onLogException)
        {
        }

        protected override Task LogAsyncImpl(AppSnapshot<TTier, TDataCenter> snapshot)
        {
            lock (_history)
            {
                _history.Add(snapshot);
            }

            return Task.FromResult(0);
        }

        protected override Task<AppSnapshot<TTier, TDataCenter>> GetSnapshotAsync(string commit)
        {
            throw new NotImplementedException();
        }
    }
}