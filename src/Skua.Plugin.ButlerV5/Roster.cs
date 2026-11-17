namespace ButlerV5;
public static class Roster
{
public static List<SyncData> GetOnline(string? exceptUsername = null)
{
return SyncFile.ReadAll()
.Where(SyncFile.IsOnline)
.Where(d => exceptUsername == null || !d.Username.Equals(exceptUsername, StringComparison.OrdinalIgnoreCase))
.OrderBy(d => d.Username, StringComparer.OrdinalIgnoreCase)
.ToList();
}
public static SyncData? FindMasterOrdering(string username)
=> FindMasterOrdering(username, GetOnline(username));
public static SyncData? FindMasterOrdering(string username, IEnumerable<SyncData> online)
=> online.FirstOrDefault(d =>
d.Followers.Any(f => f.Equals(username, StringComparison.OrdinalIgnoreCase)));
}
