using UnityEngine;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

public class WebAccessor: IDisposable
{
    private HttpClient httpClient = null;
    public WebAccessor()
    {
        this.httpClient = new HttpClient();
    }
    public async void ConnectAsync(string baseUrl, string userName, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        try
        {
            string url = $"{baseUrl}/sse?user={userName}";
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }
                        Debug.Log(line);
                        await Task.Delay(100);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("canceled");
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception: {e.Message}");
        }
    }

    public void Dispose()
    {
        this.httpClient?.Dispose();
    }
}
