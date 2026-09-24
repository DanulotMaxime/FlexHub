using System.Diagnostics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;

namespace PersonalAppsHub.Services;

public sealed class Direct3DGpuLoadService : IDisposable
{
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    public bool IsRunning => _worker is { IsCompleted: false };
    public string? LastError { get; private set; }

    public void Start(int targetLoadPercent)
    {
        Stop();
        LastError = null;
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        _worker = Task.Factory.StartNew(() => RunSafely(targetLoadPercent, token), token,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private void RunSafely(int targetLoadPercent, CancellationToken token)
    {
        try { Run(targetLoadPercent, token); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            AppLog.Write($"Charge Direct3D indisponible : {ex.Message}");
        }
    }

    public void Stop()
    {
        _cancellation?.Cancel();
        try { _worker?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException exception) when (exception.InnerExceptions.All(item => item is OperationCanceledException)) { }
        _worker = null;
        _cancellation?.Dispose();
        _cancellation = null;
    }

    private static void Run(int targetLoadPercent, CancellationToken token)
    {
        var featureLevels = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 };
        D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
            featureLevels, out ID3D11Device device, out ID3D11DeviceContext context).CheckError();
        using (device)
        using (context)
        {
            var surfaceCount = targetLoadPercent switch { <= 50 => 2, <= 75 => 4, <= 90 => 6, _ => 8 };
            var dimension = targetLoadPercent <= 50 ? 2560u : 3840u;
            var textures = new List<ID3D11Texture2D>();
            var views = new List<ID3D11RenderTargetView>();
            try
            {
                var description = new Texture2DDescription(Format.R8G8B8A8_UNorm, dimension,
                    targetLoadPercent <= 50 ? 1440u : 2160u, 1, 1, BindFlags.RenderTarget,
                    ResourceUsage.Default, CpuAccessFlags.None, 1, 0, ResourceOptionFlags.None);
                for (var index = 0; index < surfaceCount; index++)
                {
                    var texture = device.CreateTexture2D(description);
                    textures.Add(texture);
                    views.Add(device.CreateRenderTargetView(texture));
                }

                var stopwatch = new Stopwatch();
                var frame = 0u;
                while (!token.IsCancellationRequested)
                {
                    stopwatch.Restart();
                    while (stopwatch.ElapsedMilliseconds < targetLoadPercent && !token.IsCancellationRequested)
                    {
                        for (var pass = 0; pass < 48; pass++)
                        foreach (var view in views)
                        {
                            frame++;
                            context.ClearRenderTargetView(view, new Color4(
                                (frame % 251) / 250f, (frame * 7 % 241) / 240f,
                                (frame * 13 % 239) / 238f, 1f));
                        }
                        context.Flush();
                    }
                    if (token.WaitHandle.WaitOne(Math.Max(1, 100 - targetLoadPercent))) break;
                }
                context.ClearState();
                context.Flush();
            }
            finally
            {
                foreach (var view in views) view.Dispose();
                foreach (var texture in textures) texture.Dispose();
            }
        }
    }

    public void Dispose() => Stop();
}
