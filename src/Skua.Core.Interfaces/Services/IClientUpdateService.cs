namespace Skua.Core.Interfaces;
public interface IClientUpdateService
{
string? LatestVersion { get; }
bool UpdateAvailable { get; }
bool UsingNightlyChannel { get; }
Task CheckForUpdateAsync();
Task DownloadUpdateAsync(IProgress<string>? progress);
}
