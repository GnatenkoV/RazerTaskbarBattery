using System.Text.Json.Serialization;

namespace RazerTaskbar.Models;

// Colors use RGB (0xRRGGBB), with -1 representing transparency.
[method: JsonConstructor]
public readonly record struct BatteryLevelColors(int Border, int Fill, int Number, int Empty = BatteryPalette.DefaultEmpty);

public sealed record BatteryPalette
{
    public const int Transparent = -1;
    public const int DefaultEmpty = Transparent;

    public BatteryLevelColors Low { get; init; } = new(0xFF4B4B, 0xE23131, 0xFFFFFF);
    public BatteryLevelColors Medium { get; init; } = new(0xFFCD20, 0xF2AD0F, 0xFFFFFF);
    public BatteryLevelColors High { get; init; } = new(0x24FF0D, 0x1EE105, 0xFFFFFF);
    // Read the former shared color, then remove it when settings are normalized.
    [JsonPropertyName("Empty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyEmpty { get; init; }

    public BatteryPalette MigrateLegacyEmpty()
    {
        if (LegacyEmpty is not int empty)
        {
            return this;
        }

        return this with
        {
            Low = Low with { Empty = empty },
            Medium = Medium with { Empty = empty },
            High = High with { Empty = empty },
            LegacyEmpty = null
        };
    }

    public BatteryLevelColors ForPercentage(int percentage) => percentage switch
    {
        <= 20 => Low,
        <= 40 => Medium,
        _ => High
    };
}
