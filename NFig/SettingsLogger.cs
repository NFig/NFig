using System;
using System.Threading.Tasks;

namespace NFig
{
    public abstract class SettingsLogger<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        private readonly Action<Exception, AppSnapshot<TTier, TDataCenter>> _onLogException;

        protected SettingsLogger(Action<Exception, AppSnapshot<TTier, TDataCenter>> onLogException)
        {
            if (onLogException == null)
                throw new ArgumentNullException(nameof(onLogException));

            _onLogException = onLogException;
        }

        public void Log(AppSnapshot<TTier, TDataCenter> snapshot)
        {
            Task.Run(async () =>
            {
                try
                {
                    await LogAsyncImpl(snapshot);
                }
                catch (Exception e)
                {
                    _onLogException(e, snapshot);
                }
            });
        }

        protected abstract Task LogAsyncImpl(AppSnapshot<TTier, TDataCenter> snapshot);

        protected abstract Task<AppSnapshot<TTier, TDataCenter>> GetSnapshotAsync(string commit);
    }
}