using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SHA_Project.Services
{
    public class McpServerService
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;

        public event Action<string> CommandReceived;

        // Returns actual scan result JSON to Copilot
        public Func<string, Task<string>> CommandHandler;

        public void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(
                    "http://localhost:5010/mcp/");
                _listener.Start();
                _cts = new CancellationTokenSource();
                Task.Run(() => ListenLoop(_cts.Token));
            }
            catch { }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
        }

        private async Task ListenLoop(
            CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ctx = await
                        _listener.GetContextAsync();
                    _ = Task.Run(() => Handle(ctx));
                }
                catch { break; }
            }
        }

        private async Task Handle(
            HttpListenerContext ctx)
        {
            string body = "";

            using (var reader = new StreamReader(
                ctx.Request.InputStream,
                ctx.Request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            // Fire old event for build/clean
            CommandReceived?.Invoke(body);

            string response;

            if (CommandHandler != null)
            {
                try
                {
                    // Wait for full result
                    response = await CommandHandler(body)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    response =
                        "{\"status\":\"error\"," +
                        "\"message\":\"" +
                        ex.Message.Replace("\"", "'") +
                        "\"}";
                }
            }
            else
            {
                response =
                    "{\"status\":\"ok\"," +
                    "\"received\":\"" + body + "\"}";
            }

            // Add CORS headers so Copilot can read response
            ctx.Response.Headers.Add(
                "Access-Control-Allow-Origin", "*");
            ctx.Response.Headers.Add(
                "Access-Control-Allow-Methods",
                "POST, GET, OPTIONS");
            ctx.Response.Headers.Add(
                "Access-Control-Allow-Headers",
                "Content-Type");

            byte[] buf =
                Encoding.UTF8.GetBytes(response);

            ctx.Response.ContentType =
                "application/json";
            ctx.Response.ContentLength64 = buf.Length;

            await ctx.Response.OutputStream
                .WriteAsync(buf, 0, buf.Length);
            ctx.Response.Close();
        }
    }
}