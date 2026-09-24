using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PersonalAppsHub.Services;

public sealed class NetworkMonitoringService
{
    public IReadOnlyList<NetworkApplication> GetActiveApplications()
    {
        var connections = ReadOwnedTcpConnections().ToArray();
        var udpProcessIds = ReadOwnedUdpProcessIds().ToHashSet();
        return connections.Select(connection => connection.ProcessId)
            .Concat(udpProcessIds)
            .Distinct()
            .Select(CreateNetworkApplication)
            .Where(application => application is not null)
            .Select(application => application!)
            .OrderBy(application => application.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<NetworkTargetMetrics>> MeasureApplicationAsync(
        uint processId, int sampleCount = 4, int timeoutMs = 650)
    {
        var application = CreateNetworkApplication(processId)
            ?? throw new InvalidOperationException("L’application sélectionnée n’est plus active.");
        var addresses = ReadOwnedTcpConnections()
            .Where(connection => connection.ProcessId == processId && connection.State == 5 &&
                                 IsPublicAddress(connection.RemoteAddress))
            .Select(connection => connection.RemoteAddress)
            .Distinct()
            .Take(8)
            .ToArray();
        if (addresses.Length == 0)
        {
            var hasUdp = ReadOwnedUdpProcessIds().Contains(processId);
            return hasUdp
                ? [NetworkTargetMetrics.UdpApplicationDetected(application.DisplayName)]
                : [];
        }

        return await Task.WhenAll(addresses.Select((address, index) =>
            MeasureTargetAsync(($"{application.DisplayName} · serveur {index + 1}", address.ToString()),
                sampleCount, timeoutMs))).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NetworkTargetMetrics>> MeasureAsync(
        IEnumerable<string> configuredTargets, int sampleCount = 4, int timeoutMs = 650)
    {
        var configured = configuredTargets.ToArray();
        var detection = await Task.Run(() =>
        {
            var games = configured.Any(IsGameToken) ? DetectActiveGames() : new GameDetection([], []);
            var targets = configured
                .Where(target => !IsGameToken(target))
                .Select(ResolveTarget)
                .Where(target => target.HasValue)
                .Select(target => target!.Value)
                .Concat(games.Servers)
                .DistinctBy(target => target.Host, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray();
            return (Targets: targets, games.Notices);
        }).ConfigureAwait(false);

        var measurements = await Task.WhenAll(detection.Targets
            .Select(target => MeasureTargetAsync(target, sampleCount, timeoutMs))).ConfigureAwait(false);
        var regularTargets = measurements.Where(result => !result.Name.StartsWith("Jeu · ", StringComparison.Ordinal));
        var gameTargets = measurements
            .Where(result => result.Name.StartsWith("Jeu · ", StringComparison.Ordinal))
            .GroupBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var best = group.OrderByDescending(result => result.SuccessfulSamples > 0)
                    .ThenBy(result => result.PacketLossPercent)
                    .ThenBy(result => result.AveragePingMs)
                    .First();
                var count = group.Count();
                return count == 1 ? best : best with
                {
                    DetailOverride = $"Meilleure adresse sur {count} détectées · {best.Host}"
                };
            });
        return regularTargets.Concat(gameTargets).Concat(detection.Notices).ToArray();
    }

    private static bool IsGameToken(string value) =>
        value.Trim().Equals("jeu", StringComparison.OrdinalIgnoreCase);

    private static async Task<NetworkTargetMetrics> MeasureTargetAsync(
        (string Name, string Host) target, int sampleCount, int timeoutMs)
    {
        var replies = new List<long>();
        using var ping = new Ping();
        for (var index = 0; index < sampleCount; index++)
        {
            try
            {
                var reply = await ping.SendPingAsync(target.Host, timeoutMs);
                if (reply.Status == IPStatus.Success) replies.Add(reply.RoundtripTime);
            }
            catch (PingException) { }
            catch (InvalidOperationException) { }
        }

        var lost = sampleCount - replies.Count;
        var average = replies.Count == 0 ? 0 : replies.Average();
        var jitter = replies.Count < 2 ? 0
            : replies.Zip(replies.Skip(1), (first, second) => Math.Abs(second - first)).Average();
        return new NetworkTargetMetrics(target.Name, target.Host, average, jitter,
            lost * 100d / sampleCount, replies.Count, sampleCount);
    }

    private static (string Name, string Host)? ResolveTarget(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0) return null;
        if (value.Equals("passerelle", StringComparison.OrdinalIgnoreCase))
        {
            var gateway = NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                                  network.NetworkInterfaceType is not NetworkInterfaceType.Loopback)
                .SelectMany(network => network.GetIPProperties().GatewayAddresses)
                .Select(address => address.Address)
                .FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return gateway is null ? null : ("Box / passerelle", gateway.ToString());
        }

        var separator = value.IndexOf('=');
        return separator > 0 && separator < value.Length - 1
            ? (value[..separator].Trim(), value[(separator + 1)..].Trim())
            : (value, value);
    }

    private static GameDetection DetectActiveGames()
    {
        var servers = new List<(string Name, string Host)>();
        var notices = new List<NetworkTargetMetrics>();
        var tcpConnections = ReadOwnedTcpConnections().ToArray();
        var udpProcessIds = ReadOwnedUdpProcessIds().ToHashSet();
        var processIds = tcpConnections.Select(connection => connection.ProcessId)
            .Concat(udpProcessIds).Distinct();

        foreach (var processId in processIds)
        {
            Process? process = null;
            try
            {
                process = Process.GetProcessById((int)processId);
                var processName = process.ProcessName;
                var windowTitle = process.MainWindowTitle.Trim();
                var hasUdp = udpProcessIds.Contains(processId);
                var recognizableByName = LooksLikeGame(string.Empty, processName, windowTitle, hasUdp);
                var executable = recognizableByName ? string.Empty : TryGetExecutablePath(process);
                if (!recognizableByName && !LooksLikeGame(executable, processName, windowTitle, hasUdp)) continue;
                var gameName = windowTitle;
                if (gameName.Length == 0) gameName = process.ProcessName;

                var remoteAddresses = tcpConnections
                    .Where(connection => connection.ProcessId == processId && connection.State == 5 &&
                                         IsPublicAddress(connection.RemoteAddress))
                    .Select(connection => connection.RemoteAddress).ToList();
                if (processName.Equals("ArmaReforgerSteam", StringComparison.OrdinalIgnoreCase) &&
                    TryReadLatestArmaServerAddress() is { } armaAddress)
                    remoteAddresses.Add(armaAddress);
                var distinctAddresses = remoteAddresses.Distinct().Take(3).ToArray();
                foreach (var address in distinctAddresses)
                    servers.Add(($"Jeu · {gameName}", address.ToString()));

                if (distinctAddresses.Length == 0 && udpProcessIds.Contains(processId))
                    notices.Add(NetworkTargetMetrics.UdpGameDetected(gameName));
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally { process?.Dispose(); }
        }
        return new GameDetection(servers, notices);
    }

    private static IPAddress? TryReadLatestArmaServerAddress()
    {
        try
        {
            var logsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "ArmaReforger", "logs");
            if (!Directory.Exists(logsRoot)) return null;
            var latest = Directory.EnumerateFiles(logsRoot, "console.log", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault();
            if (latest is null) return null;

            IPAddress? lastAddress = null;
            foreach (var line in File.ReadLines(latest.FullName))
            {
                if (!line.Contains("NETWORK", StringComparison.OrdinalIgnoreCase)) continue;
                var match = Regex.Match(line, @"\b(?<ip>(?:\d{1,3}\.){3}\d{1,3}):\d+\b");
                if (match.Success && IPAddress.TryParse(match.Groups["ip"].Value, out var address))
                    lastAddress = address;
            }
            return lastAddress;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static string TryGetExecutablePath(Process process)
    {
        try { return process.MainModule?.FileName ?? string.Empty; }
        catch (InvalidOperationException) { return string.Empty; }
        catch (System.ComponentModel.Win32Exception) { return string.Empty; }
    }

    private static bool LooksLikeGame(string executablePath, string processName, string windowTitle, bool hasUdp)
    {
        var path = executablePath.Replace('/', '\\');
        return processName.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase) ||
               processName.EndsWith("-WinGDK-Shipping", StringComparison.OrdinalIgnoreCase) ||
               (processName.Contains("Steam", StringComparison.OrdinalIgnoreCase) &&
                !processName.StartsWith("steam", StringComparison.OrdinalIgnoreCase)) ||
               path.Contains("\\steamapps\\common\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\Epic Games\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\Riot Games\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\XboxGames\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\GOG Galaxy\\Games\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\EA Games\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\Ubisoft Game Launcher\\games\\", StringComparison.OrdinalIgnoreCase) ||
               (hasUdp && windowTitle.Length > 0 && !IsKnownDesktopApplication(processName));
    }

    private static bool IsKnownDesktopApplication(string processName) => processName.ToLowerInvariant() is
        "applicationframehost" or "chatgpt" or "code" or "discord" or "explorer" or "flexhub" or
        "msedge" or "chrome" or "firefox" or "notepad" or "nvidia overlay" or "opacitywnd" or
        "razerappengine" or "steam" or "steamwebhelper" or "systemsettings" or "textinputhost";

    private static NetworkApplication? CreateNetworkApplication(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (processId == Environment.ProcessId || process.ProcessName.Equals("System", StringComparison.OrdinalIgnoreCase))
                return null;
            var title = process.MainWindowTitle.Trim();
            return new NetworkApplication(processId, process.ProcessName,
                title.Length == 0 ? process.ProcessName : title);
        }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any)) return false;
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] != 10 &&
               !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
               !(bytes[0] == 192 && bytes[1] == 168) &&
               !(bytes[0] == 169 && bytes[1] == 254);
    }

    private static IEnumerable<OwnedTcpConnection> ReadOwnedTcpConnections()
    {
        var size = 0;
        _ = GetExtendedTcpTable(IntPtr.Zero, ref size, true, 2, TcpTableOwnerPidAll, 0);
        if (size <= 0) yield break;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, true, 2, TcpTableOwnerPidAll, 0) != 0) yield break;
            var count = Marshal.ReadInt32(buffer);
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            var rowPointer = IntPtr.Add(buffer, sizeof(int));
            for (var index = 0; index < count; index++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                yield return new OwnedTcpConnection(row.OwningPid, row.State,
                    new IPAddress(BitConverter.GetBytes(row.RemoteAddress)));
                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static IEnumerable<uint> ReadOwnedUdpProcessIds()
    {
        var size = 0;
        _ = GetExtendedUdpTable(IntPtr.Zero, ref size, true, 2, UdpTableOwnerPid, 0);
        if (size <= 0) yield break;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedUdpTable(buffer, ref size, true, 2, UdpTableOwnerPid, 0) != 0) yield break;
            var count = Marshal.ReadInt32(buffer);
            var rowSize = Marshal.SizeOf<MibUdpRowOwnerPid>();
            var rowPointer = IntPtr.Add(buffer, sizeof(int));
            for (var index = 0; index < count; index++)
            {
                var row = Marshal.PtrToStructure<MibUdpRowOwnerPid>(rowPointer);
                yield return row.OwningPid;
                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private const int TcpTableOwnerPidAll = 5;
    private const int UdpTableOwnerPid = 1;

    private sealed record OwnedTcpConnection(uint ProcessId, uint State, IPAddress RemoteAddress);
    private sealed record GameDetection(
        IReadOnlyList<(string Name, string Host)> Servers,
        IReadOnlyList<NetworkTargetMetrics> Notices);

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State, LocalAddress, LocalPort, RemoteAddress, RemotePort, OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibUdpRowOwnerPid
    {
        public uint LocalAddress, LocalPort, OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int size, bool order,
        int ipVersion, int tableClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(IntPtr udpTable, ref int size, bool order,
        int ipVersion, int tableClass, uint reserved);
}

public sealed record NetworkTargetMetrics(
    string Name, string Host, double AveragePingMs, double JitterMs,
    double PacketLossPercent, int SuccessfulSamples, int TotalSamples,
    string? StatusOverride = null, string? DetailOverride = null)
{
    public string PingText => StatusOverride is not null ? "UDP" : SuccessfulSamples == 0 ? "Indisponible" : $"{AveragePingMs:0} ms";
    public string JitterText => SuccessfulSamples < 2 ? "—" : $"{JitterMs:0.0} ms";
    public string LossText => StatusOverride is not null ? "—" : $"{PacketLossPercent:0}%";
    public string DetailText => DetailOverride ?? $"{SuccessfulSamples}/{TotalSamples} réponses · {Host}";
    public string QualityText => StatusOverride ?? (SuccessfulSamples == 0 ? "Injoignable"
        : PacketLossPercent > 0 ? "Pertes détectées"
        : JitterMs > 20 ? "Jitter élevé"
        : AveragePingMs > 100 ? "Latence élevée"
        : "Connexion stable");

    public static NetworkTargetMetrics UdpGameDetected(string gameName) => new(
        $"Jeu · {gameName}", "UDP", 0, 0, 0, 0, 0,
        "Trafic UDP détecté", "Jeu reconnu · serveur distant non exposé par Windows");

    public static NetworkTargetMetrics UdpApplicationDetected(string applicationName) => new(
        applicationName, "UDP", 0, 0, 0, 0, 0,
        "Trafic UDP détecté", "Application active · serveur distant non exposé par Windows");
}

public sealed record NetworkApplication(uint ProcessId, string ProcessName, string DisplayName)
{
    public string SelectionText => $"{DisplayName} ({ProcessName}, PID {ProcessId})";
}
