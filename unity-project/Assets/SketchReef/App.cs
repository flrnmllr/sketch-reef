using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public class App : MonoBehaviour
{
    private Process pythonProcess;
    private static readonly HttpClient httpClient = new HttpClient();

    void Start()
    {
        string path = Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            "app"
        );

        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = path;
        psi.WorkingDirectory = Path.GetDirectoryName(path);
        psi.UseShellExecute = false;

        pythonProcess = Process.Start(psi);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

    async void OnApplicationQuit()
    {
        await StopWebServer();
        KillFallback();
    }

    async Task StopWebServer()
    {
        try
        {
            await httpClient.PostAsync("http://127.0.0.1:5000/shutdown", null);
            await Task.Delay(300);
        }
        catch
        {

        }
    }

    void KillFallback()
    {
        try
        {
            if (pythonProcess != null && !pythonProcess.HasExited)
            {
                pythonProcess.Kill();
            }
        }
        catch { }
    }
}