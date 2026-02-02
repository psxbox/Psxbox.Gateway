using System.Diagnostics;

namespace Psxbox.GatewayPoint
{
    public class CollectorNode : IDisposable
    {
        const int DEFAULT_WAIT_TIME_MS = 180_000;

        private readonly Stopwatch _stopwatch;
        private bool disposedValue;
        private CancellationTokenSource cts = new();

        public event Func<string, Task>? OnTime;

        public string IMEI { get; set; } = "";
        public string IpPort { get; }

        public CollectorNode(string ipPort, string imei)
        {
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
            IpPort = ipPort;
            IMEI = imei;

            Task.Run(() => TimerTask(cts.Token));
        }

        private async Task TimerTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_stopwatch.ElapsedMilliseconds >= DEFAULT_WAIT_TIME_MS)
                {
                    if (OnTime != null)
                        await OnTime.Invoke(IpPort);

                    RestartTimer();
                }
                await Task.Delay(1000, cancellationToken);
            }
        }

        public void RestartTimer() => _stopwatch.Restart();
        public override string ToString() => IMEI;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    OnTime = null;
                    _stopwatch.Stop();
                    cts.Cancel();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
