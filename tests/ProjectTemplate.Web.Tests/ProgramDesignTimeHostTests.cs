using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Verifies how the application entry point reports host aborts requested by design-time tooling.
/// </summary>
/// <remarks>
/// EF Core design-time commands such as <c>dotnet ef migrations add</c> resolve the service provider by running the
/// entry point, listening for the <c>HostBuilt</c> hosting diagnostic event, and throwing
/// <see cref="HostAbortedException"/> to stop the host after <c>builder.Build()</c>. These tests reproduce that
/// mechanism in process so the entry point's bootstrap exception handling is exercised exactly as the tooling does.
/// </remarks>
public sealed class ProgramDesignTimeHostTests
{
    private const string _hostingDiagnosticListenerName = "Microsoft.Extensions.Hosting";
    private const string _hostBuiltEventName = "HostBuilt";
    private const string _fatalMessage = "application terminated unexpectedly";

    /// <summary>
    /// Verifies that a tooling-initiated host abort is rethrown without being logged as a fatal startup failure.
    /// </summary>
    [Fact]
    public void Main_DesignTimeHostAbort_IsNotLoggedAsFatal()
    {
        (Exception? exception, string output) = RunEntryPointThrowingOnHostBuilt(() => new HostAbortedException());

        Assert.IsType<HostAbortedException>(exception);
        Assert.DoesNotContain(_fatalMessage, output, StringComparison.Ordinal);
        Assert.DoesNotContain(" FTL]", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a genuine startup failure is still logged as fatal and rethrown.
    /// </summary>
    [Fact]
    public void Main_StartupFailure_IsLoggedAsFatalAndRethrown()
    {
        (Exception? exception, string output) = RunEntryPointThrowingOnHostBuilt(
            () => new InvalidOperationException("Simulated startup failure."));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains(_fatalMessage, output, StringComparison.Ordinal);
        Assert.Contains(" FTL]", output, StringComparison.Ordinal);
    }

    private static (Exception? Exception, string Output) RunEntryPointThrowingOnHostBuilt(Func<Exception> createException)
    {
        MethodInfo entryPoint = typeof(Program).Assembly.EntryPoint
            ?? throw new InvalidOperationException("The web assembly does not define an entry point.");

        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter output = new();
        using HostBuiltThrowingObserver observer = new(createException);
        using IDisposable allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(observer);

        Console.SetOut(output);
        Console.SetError(output);

        Exception? exception = null;

        try
        {
            _ = entryPoint.Invoke(null, [Array.Empty<string>()]);
        }
        catch (TargetInvocationException ex)
        {
            exception = ex.InnerException;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        return (exception, output.ToString());
    }

    /// <summary>
    /// Mirrors the hosting listener that design-time tooling attaches through <c>HostFactoryResolver</c>.
    /// </summary>
    private sealed class HostBuiltThrowingObserver(Func<Exception> createException)
        : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = [];

        public void OnNext(DiagnosticListener value)
        {
            if (string.Equals(value.Name, _hostingDiagnosticListenerName, StringComparison.Ordinal))
            {
                _subscriptions.Add(value.Subscribe(this));
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (string.Equals(value.Key, _hostBuiltEventName, StringComparison.Ordinal))
            {
                throw createException();
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }
    }
}
