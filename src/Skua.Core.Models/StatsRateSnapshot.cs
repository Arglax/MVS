using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Skua.Core.Models;
public sealed record StatsRateSnapshot(
DateTimeOffset SavedAt,
TimeSpan SessionDuration,
string RateUnit,
double ExperienceRate,
double GoldRate,
double KillsRate,
double DeathsRate,
double ItemsRate,
double ClassPointsRate,
int ClassRank)
{
public string? Character { get; init; }
[JsonExtensionData]
public Dictionary<string, JsonElement>? ExtensionData { get; set; }
[JsonIgnore] public string SavedAtDisplay => SavedAt.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
[JsonIgnore] public string CharacterDisplay => string.IsNullOrWhiteSpace(Character) ? "—" : Character;
[JsonIgnore] public string SessionDisplay => string.Concat(
((long)SessionDuration.TotalHours).ToString(CultureInfo.InvariantCulture), ":",
SessionDuration.Minutes.ToString("D2", CultureInfo.InvariantCulture), ":",
SessionDuration.Seconds.ToString("D2", CultureInfo.InvariantCulture));
[JsonIgnore] public string ExperienceDisplay => ExperienceRate.ToString("N0", CultureInfo.CurrentCulture);
[JsonIgnore] public string GoldDisplay => GoldRate.ToString("N0", CultureInfo.CurrentCulture);
[JsonIgnore] public string KillsDisplay => KillsRate.ToString("N2", CultureInfo.CurrentCulture);
[JsonIgnore] public string DeathsDisplay => DeathsRate.ToString("N2", CultureInfo.CurrentCulture);
[JsonIgnore] public string ItemsDisplay => ItemsRate.ToString("N1", CultureInfo.CurrentCulture);
[JsonIgnore] public string ClassPointsDisplay => ClassPointsRate.ToString("N0", CultureInfo.CurrentCulture);
}
