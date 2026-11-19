namespace Skua.Flash.Ruffle;
public sealed class RuffleHostOptions
{
public int Port { get; set; }
public Func<byte[]>? SwfProvider { get; set; }
public string ServerListUrl { get; set; } = "https://game.aq.com/game/api/data/servers";
public string Quality { get; set; } = "medium";
public int FrameRate { get; set; }
public double MaxPixelRatio { get; set; }
public string PreferredRenderer { get; set; } = string.Empty;
public int TouchListRowHeight { get; set; }
public string EngineMemory { get; set; } = "fork";
public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";
}
