namespace Atc.Desktop.WebHost.Hosting;

/// <summary>
/// One running instance per app and user. A second instance hands its file paths to the first over a
/// named pipe and exits, so opening a file while the app runs is a warm start. Named mutexes and pipes
/// work on Windows and Linux in .NET; the pipe only accepts the same user.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex? ownedMutex;
    private readonly string pipeName;
    private readonly CancellationTokenSource stopListening = new();

    private SingleInstance(
        Mutex? ownedMutex,
        string pipeName)
    {
        this.ownedMutex = ownedMutex;
        this.pipeName = pipeName;
    }

    public bool IsPrimary
        => ownedMutex is not null;

    /// <summary>Takes the app's mutex, or, when another instance holds it, sends it the files.</summary>
    /// <param name="appId">The app id.</param>
    /// <param name="files">Full paths to hand over when this is a second instance.</param>
    /// <param name="beforeHandOff">Called just before the files are sent.</param>
    /// <returns>The instance; check <see cref="IsPrimary"/>.</returns>
    public static SingleInstance Acquire(
        string appId,
        IReadOnlyList<string> files,
        Action beforeHandOff)
    {
        var name = BuildName(appId, Environment.UserName);
        var mutex = new Mutex(initiallyOwned: true, @"Local\" + name, out var createdNew);
        if (createdNew)
        {
            return new SingleInstance(mutex, name);
        }

        mutex.Dispose();
        beforeHandOff();
        SendToPrimary(name, files);
        return new SingleInstance(ownedMutex: null, name);
    }

    public static string BuildName(
        string appId,
        string userName)
        => "AtcWebHost." + Sanitize(appId) + "." + Sanitize(userName);

    /// <summary>Listens for paths from later instances. An empty list means "just come to the front".</summary>
    /// <param name="onFiles">Called on a background thread with the received paths.</param>
    public void Listen(Action<IReadOnlyList<string>> onFiles)
    {
        var thread = new Thread(() => ListenLoop(onFiles))
        {
            IsBackground = true,
            Name = "atc-webhost-single-instance",
        };
        thread.Start();
    }

    public void Dispose()
    {
        stopListening.Cancel();
        stopListening.Dispose();

        if (ownedMutex is null)
        {
            return;
        }

        ownedMutex.ReleaseMutex();
        ownedMutex.Dispose();
    }

    private static void SendToPrimary(
        string pipeName,
        IReadOnlyList<string> files)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(timeout: 2000);
            using var writer = new StreamWriter(client, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(string.Join('\n', files));
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // The first instance is starting or closing. Nothing sensible to do from here.
        }
    }

    private static string Sanitize(string value)
        => new([.. value.Select(c => char.IsAsciiLetterOrDigit(c) || c == '.' ? c : '_')]);

    private void ListenLoop(Action<IReadOnlyList<string>> onFiles)
    {
        var token = stopListening.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                server.WaitForConnectionAsync(token).GetAwaiter().GetResult();
                using var reader = new StreamReader(server, Encoding.UTF8);
                var files = reader.ReadToEnd()
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                onFiles(files);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (IOException)
            {
                // A client went away mid-message; keep listening.
            }
        }
    }
}