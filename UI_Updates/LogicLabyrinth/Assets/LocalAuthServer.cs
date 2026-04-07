using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class LocalAuthServer : IDisposable
{
    private HttpListener listener;
    private int port;
    private bool codeReceived = false;
    private string authCode;
    private bool isListening = false;

    public LocalAuthServer(int serverPort = 8000) => port = serverPort;
    public int GetPort() => port;

    public void Start()
    {
        try
        {
            Stop();
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();
            isListening = true;
            Task.Run(ListenForRequests);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Server Start Failed: {ex.Message}");
        }
    }

    private async Task ListenForRequests()
    {
        while (isListening && listener != null && listener.IsListening)
        {
            try
            {
                var context = await listener.GetContextAsync();
                var query = context.Request.Url.Query.TrimStart('?');
                if (query.Contains("code="))
                {
                    authCode = GetValueFromQuery(query, "code");
                    codeReceived = true;
                    byte[] buffer = Encoding.UTF8.GetBytes("<html><body><h1>Login Success! Return to game.</h1></body></html>");
                    context.Response.ContentLength64 = buffer.Length;
                    await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                context.Response.Close();
                if (codeReceived) { await Task.Delay(1000); Stop(); }
            }
            catch { break; }
        }
    }

    private string GetValueFromQuery(string query, string key)
    {
        foreach (string pair in query.Split('&'))
        {
            string[] kvp = pair.Split('=');
            if (kvp.Length == 2 && kvp[0] == key) return Uri.UnescapeDataString(kvp[1]);
        }
        return null;
    }

    public async Task<string> WaitForAuthCode(TimeSpan timeout)
    {
        DateTime start = DateTime.Now;
        while (!codeReceived && (DateTime.Now - start) < timeout && isListening) await Task.Delay(100);
        return authCode;
    }

    public void Stop()
    {
        isListening = false;
        if (listener != null)
        {
            try { if (listener.IsListening) listener.Stop(); listener.Close(); } catch { }
            listener = null;
        }
    }

    public void Dispose() => Stop();
}