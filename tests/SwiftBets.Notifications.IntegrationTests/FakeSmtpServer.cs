using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SwiftBets.Notifications.IntegrationTests;

/// <summary>Just enough SMTP to accept messages from MailKit and keep their raw text for assertions.</summary>
internal sealed class FakeSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public FakeSmtpServer()
    {
        _listener.Start();
        _loop = AcceptAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public ConcurrentQueue<(string To, string Data)> Messages { get; } = new();

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 fake-smtp ready");
            var to = string.Empty;
            while (await reader.ReadLineAsync() is { } line)
            {
                var verb = line.Split(' ', 2)[0].ToUpperInvariant();
                switch (verb)
                {
                    case "EHLO":
                    case "HELO":
                        await writer.WriteLineAsync("250 fake-smtp");
                        break;
                    case "RCPT":
                        to = line[(line.IndexOf('<', StringComparison.Ordinal) + 1)..line.IndexOf('>', StringComparison.Ordinal)];
                        await writer.WriteLineAsync("250 ok");
                        break;
                    case "DATA":
                        await writer.WriteLineAsync("354 end with .");
                        var data = new StringBuilder();
                        while (await reader.ReadLineAsync() is { } body && body != ".")
                        {
                            data.AppendLine(body);
                        }

                        Messages.Enqueue((to, data.ToString()));
                        await writer.WriteLineAsync("250 queued");
                        break;
                    case "QUIT":
                        await writer.WriteLineAsync("221 bye");
                        return;
                    default:
                        await writer.WriteLineAsync("250 ok");
                        break;
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        await _loop;
        _stop.Dispose();
    }
}
