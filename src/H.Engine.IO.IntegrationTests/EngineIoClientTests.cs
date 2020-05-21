using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using H.WebSockets.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace H.Engine.IO.IntegrationTests
{
    [TestClass]
    public class EngineIoClientTests
    {
        public const string LocalCharServerUrl = "ws://localhost:1465/";

        public static async Task<Uri> GetRedirectedUrlAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            using var client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
            }, true);
            using var response = await client.GetAsync(uri, cancellationToken);

            return (int)response.StatusCode == 308
                ? new Uri(response.Headers.GetValues("Location").First())
                : response.RequestMessage.RequestUri;
        }

        private static async Task ConnectToChatBaseTestAsync(string url, CancellationToken cancellationToken = default)
        {
            await using var client = new EngineIoClient("socket.io");

            client.MessageReceived += (sender, args) => Console.WriteLine($"MessageReceived: {args.Value}");
            client.ExceptionOccurred += (sender, args) => Console.WriteLine($"ExceptionOccurred: {args.Value}");
            client.Opened += (sender, args) => Console.WriteLine($"Opened: {args.Value}");
            client.Closed += (sender, args) => Console.WriteLine($"Closed. Reason: {args.Reason}, Status: {args.Status:G}");

            var results = await client.WaitAllEventsAsync<EventArgs>(async () =>
            {
                Console.WriteLine("# Before OpenAsync");

                await client.OpenAsync(new Uri(url), 10);

                Console.WriteLine("# Before Delay");

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                Console.WriteLine("# Before CloseAsync");

                await client.CloseAsync(cancellationToken);

                Console.WriteLine("# After CloseAsync");
            }, cancellationToken, nameof(client.Opened), nameof(client.Closed));

            Console.WriteLine();
            Console.WriteLine($"WebSocket State: {client.WebSocketClient.Socket.State}");
            Console.WriteLine($"WebSocket CloseStatus: {client.WebSocketClient.Socket.CloseStatus}");
            Console.WriteLine($"WebSocket CloseStatusDescription: {client.WebSocketClient.Socket.CloseStatusDescription}");

            foreach (var pair in results)
            {
                Assert.IsNotNull(pair.Value, $"Client event(\"{pair.Key}\") did not happen");
            }
        }

        [TestMethod]
        public async Task ConnectToChatNowShTest()
        {
            using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var uri = await GetRedirectedUrlAsync(
                new Uri("https://socket-io-chat.now.sh/"),
                tokenSource.Token);

            await ConnectToChatBaseTestAsync($"wss://{uri.Host}/", tokenSource.Token);
        }

        [TestMethod]
        [Ignore]
        public async Task ConnectToLocalChatServerTest()
        {
            using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await ConnectToChatBaseTestAsync(LocalCharServerUrl, tokenSource.Token);
        }
    }
}