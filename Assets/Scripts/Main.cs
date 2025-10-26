using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

public class Main : MonoBehaviour
{
    private WebAccessor webAccessor = null;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        webAccessor = new WebAccessor();
        Task.Run(() =>
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            webAccessor.ConnectAsync("http://localhost:8080", "example", cts.Token);
        });
    }

    // Update is called once per frame
    void OnDestroy()
    {
        webAccessor?.Dispose();
    }
}
