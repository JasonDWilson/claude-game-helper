using System.Windows.Threading;

namespace ClaudeGameHelper.Tests;

/// <summary>Runs a WPF dispatcher on its own STA thread so tests can create and pump real windows.</summary>
internal sealed class StaWindowHost : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Thread _thread;

    public StaWindowHost()
    {
        var ready = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;
        _thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        }) { IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait();
        _dispatcher = dispatcher!;
    }

    public T Invoke<T>(Func<T> func) => _dispatcher.Invoke(func);

    public void Invoke(Action action) => _dispatcher.Invoke(action);

    public void Dispose()
    {
        _dispatcher.InvokeShutdown();
        _thread.Join(TimeSpan.FromSeconds(5));
    }
}
