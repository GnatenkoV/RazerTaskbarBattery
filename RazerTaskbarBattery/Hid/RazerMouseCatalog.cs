namespace RazerTaskbar.Hid;

// Snapshot: https://github.com/openrazer/openrazer/blob/master/driver/razermouse_driver.h
// Battery transactions: driver/razermouse_driver.c, razer_attr_read_charge_level.
// The header lists mouse PIDs, NOT a guarantee that each model implements the battery command.
// No OpenRazer code was copied into this file; entries are identifiers and descriptive names.
internal sealed record RazerMouseProduct(string Name, byte? BatteryTransaction);

internal static class RazerMouseCatalog
{
    internal static readonly IReadOnlyDictionary<ushort, RazerMouseProduct> Products =
        new Dictionary<ushort, RazerMouseProduct>
        {
            [0x0013] = new("Razer Orochi 2011", null), // OROCHI_2011
            [0x0015] = new("Razer Naga", null), // NAGA
            [0x0016] = new("Razer DeathAdder 3 5G", null), // DEATHADDER_3_5G
            [0x001F] = new("Razer Naga Epic", (byte)0xFF), // NAGA_EPIC
            [0x0020] = new("Razer Abyssus 1800", null), // ABYSSUS_1800
            [0x0024] = new("Razer Mamba 2012 (Wired)", (byte)0xFF), // MAMBA_2012_WIRED
            [0x0025] = new("Razer Mamba 2012 (Wireless)", (byte)0xFF), // MAMBA_2012_WIRELESS
            [0x0029] = new("Razer DeathAdder 3 5G Black", null), // DEATHADDER_3_5G_BLACK
            [0x002E] = new("Razer Naga 2012", null), // NAGA_2012
            [0x002F] = new("Razer Imperator", null), // IMPERATOR
            [0x0032] = new("Razer Ouroboros", (byte)0xFF), // OUROBOROS
            [0x0034] = new("Razer Taipan", null), // TAIPAN
            [0x0036] = new("Razer Naga Hex Red", null), // NAGA_HEX_RED
            [0x0037] = new("Razer DeathAdder 2013", null), // DEATHADDER_2013
            [0x0038] = new("Razer DeathAdder 1800", null), // DEATHADDER_1800
            [0x0039] = new("Razer Orochi 2013", null), // OROCHI_2013
            [0x003E] = new("Razer Naga Epic Chroma", (byte)0xFF), // NAGA_EPIC_CHROMA
            [0x003F] = new("Razer Naga Epic Chroma Dock", (byte)0xFF), // NAGA_EPIC_CHROMA_DOCK
            [0x0040] = new("Razer Naga 2014", null), // NAGA_2014
            [0x0041] = new("Razer Naga Hex", null), // NAGA_HEX
            [0x0042] = new("Razer Abyssus", null), // ABYSSUS
            [0x0043] = new("Razer DeathAdder Chroma", null), // DEATHADDER_CHROMA
            [0x0044] = new("Razer Mamba (Wired)", (byte)0xFF), // MAMBA_WIRED
            [0x0045] = new("Razer Mamba (Wireless)", (byte)0xFF), // MAMBA_WIRELESS
            [0x0046] = new("Razer Mamba TE (Wired)", null), // MAMBA_TE_WIRED
            [0x0048] = new("Razer Orochi Chroma", null), // OROCHI_CHROMA
            [0x004C] = new("Razer Diamondback Chroma", null), // DIAMONDBACK_CHROMA
            [0x004F] = new("Razer DeathAdder 2000", null), // DEATHADDER_2000
            [0x0050] = new("Razer Naga Hex V2", null), // NAGA_HEX_V2
            [0x0053] = new("Razer Naga Chroma", null), // NAGA_CHROMA
            [0x0054] = new("Razer DeathAdder 3500", null), // DEATHADDER_3500
            [0x0059] = new("Razer Lancehead (Wired)", (byte)0x3F), // LANCEHEAD_WIRED
            [0x005A] = new("Razer Lancehead (Wireless)", (byte)0x3F), // LANCEHEAD_WIRELESS
            [0x005B] = new("Razer Abyssus V2", null), // ABYSSUS_V2
            [0x005C] = new("Razer DeathAdder Elite", null), // DEATHADDER_ELITE
            [0x005E] = new("Razer Abyssus 2000", null), // ABYSSUS_2000
            [0x0060] = new("Razer Lancehead TE (Wired)", null), // LANCEHEAD_TE_WIRED
            [0x0062] = new("Razer Atheris (Receiver)", (byte)0x1F), // ATHERIS_RECEIVER
            [0x0064] = new("Razer Basilisk", null), // BASILISK
            [0x0065] = new("Razer Basilisk Essential", null), // BASILISK_ESSENTIAL
            [0x0067] = new("Razer Naga Trinity", null), // NAGA_TRINITY
            [0x006A] = new("Razer Abyssus Elite D.Va Edition", null), // ABYSSUS_ELITE_DVA_EDITION
            [0x006B] = new("Razer Abyssus Essential", null), // ABYSSUS_ESSENTIAL
            [0x006C] = new("Razer Mamba Elite", null), // MAMBA_ELITE
            [0x006E] = new("Razer DeathAdder Essential", null), // DEATHADDER_ESSENTIAL
            [0x006F] = new("Razer Lancehead Wireless (Receiver)", (byte)0x1F), // LANCEHEAD_WIRELESS_RECEIVER
            [0x0070] = new("Razer Lancehead Wireless (Wired)", (byte)0x1F), // LANCEHEAD_WIRELESS_WIRED
            [0x0071] = new("Razer DeathAdder Essential White Edition", null), // DEATHADDER_ESSENTIAL_WHITE_EDITION
            [0x0072] = new("Razer Mamba Wireless (Receiver)", (byte)0x3F), // MAMBA_WIRELESS_RECEIVER
            [0x0073] = new("Razer Mamba Wireless (Wired)", (byte)0x3F), // MAMBA_WIRELESS_WIRED
            [0x0077] = new("Razer Pro Click (Receiver)", (byte)0x1F), // PRO_CLICK_RECEIVER
            [0x0078] = new("Razer Viper", null), // VIPER
            [0x007A] = new("Razer Viper Ultimate (Wired)", (byte)0xFF), // VIPER_ULTIMATE_WIRED
            [0x007B] = new("Razer Viper Ultimate (Wireless)", (byte)0xFF), // VIPER_ULTIMATE_WIRELESS
            [0x007C] = new("Razer DeathAdder V2 Pro (Wired)", (byte)0x3F), // DEATHADDER_V2_PRO_WIRED
            [0x007D] = new("Razer DeathAdder V2 Pro (Wireless)", (byte)0x3F), // DEATHADDER_V2_PRO_WIRELESS
            [0x0080] = new("Razer Pro Click (Wired)", (byte)0x1F), // PRO_CLICK_WIRED
            [0x0083] = new("Razer Basilisk X HyperSpeed", (byte)0xFF), // BASILISK_X_HYPERSPEED
            [0x0084] = new("Razer DeathAdder V2", null), // DEATHADDER_V2
            [0x0085] = new("Razer Basilisk V2", null), // BASILISK_V2
            [0x0086] = new("Razer Basilisk Ultimate (Wired)", (byte)0x1F), // BASILISK_ULTIMATE_WIRED
            [0x0088] = new("Razer Basilisk Ultimate (Receiver)", (byte)0x1F), // BASILISK_ULTIMATE_RECEIVER
            [0x008A] = new("Razer Viper Mini", null), // VIPER_MINI
            [0x008C] = new("Razer DeathAdder V2 Mini", null), // DEATHADDER_V2_MINI
            [0x008D] = new("Razer Naga Left Handed 2020", null), // NAGA_LEFT_HANDED_2020
            [0x008F] = new("Razer Naga Pro (Wired)", (byte)0x1F), // NAGA_PRO_WIRED
            [0x0090] = new("Razer Naga Pro (Wireless)", (byte)0x1F), // NAGA_PRO_WIRELESS
            [0x0091] = new("Razer Viper 8KHz", null), // VIPER_8K
            [0x0094] = new("Razer Orochi V2 (Receiver)", (byte)0x1F), // OROCHI_V2_RECEIVER
            [0x0095] = new("Razer Orochi V2 (Bluetooth)", (byte)0x1F), // OROCHI_V2_BLUETOOTH
            [0x0096] = new("Razer Naga X", null), // NAGA_X
            [0x0098] = new("Razer DeathAdder Essential 2021", null), // DEATHADDER_ESSENTIAL_2021
            [0x0099] = new("Razer Basilisk V3", null), // BASILISK_V3
            [0x009A] = new("Razer Pro Click Mini (Receiver)", (byte)0x1F), // PRO_CLICK_MINI_RECEIVER
            [0x009C] = new("Razer DeathAdder V2 X HyperSpeed", (byte)0x1F), // DEATHADDER_V2_X_HYPERSPEED
            [0x009E] = new("Razer Viper Mini SE (Wired)", (byte)0x1F), // VIPER_MINI_SE_WIRED
            [0x009F] = new("Razer Viper Mini SE (Wireless)", (byte)0x1F), // VIPER_MINI_SE_WIRELESS
            [0x00A1] = new("Razer DeathAdder V2 Lite", null), // DEATHADDER_V2_LITE
            [0x00A3] = new("Razer Cobra", null), // COBRA
            [0x00A5] = new("Razer Viper V2 Pro (Wired)", (byte)0x1F), // VIPER_V2_PRO_WIRED
            [0x00A6] = new("Razer Viper V2 Pro (Wireless)", (byte)0x1F), // VIPER_V2_PRO_WIRELESS
            [0x00A7] = new("Razer Naga V2 Pro (Wired)", (byte)0x1F), // NAGA_V2_PRO_WIRED
            [0x00A8] = new("Razer Naga V2 Pro (Wireless)", (byte)0x1F), // NAGA_V2_PRO_WIRELESS
            [0x00AA] = new("Razer Basilisk V3 Pro (Wired)", (byte)0x1F), // BASILISK_V3_PRO_WIRED
            [0x00AB] = new("Razer Basilisk V3 Pro (Wireless)", (byte)0x1F), // BASILISK_V3_PRO_WIRELESS
            [0x00AF] = new("Razer Cobra Pro (Wired)", (byte)0x1F), // COBRA_PRO_WIRED
            [0x00B0] = new("Razer Cobra Pro (Wireless)", (byte)0x1F), // COBRA_PRO_WIRELESS
            [0x00B2] = new("Razer DeathAdder V3", null), // DEATHADDER_V3
            [0x00B3] = new("Razer HyperPolling Wireless Dongle", (byte)0x1F), // HYPERPOLLING_WIRELESS_DONGLE
            [0x00B4] = new("Razer Naga V2 HyperSpeed (Receiver)", (byte)0x1F), // NAGA_V2_HYPERSPEED_RECEIVER
            [0x00B6] = new("Razer DeathAdder V3 Pro (Wired)", (byte)0x1F), // DEATHADDER_V3_PRO_WIRED
            [0x00B7] = new("Razer DeathAdder V3 Pro (Wireless)", (byte)0x1F), // DEATHADDER_V3_PRO_WIRELESS
            [0x00B8] = new("Razer Viper V3 HyperSpeed", (byte)0x1F), // VIPER_V3_HYPERSPEED
            [0x00B9] = new("Razer Basilisk V3 X HyperSpeed", (byte)0x1F), // BASILISK_V3_X_HYPERSPEED
            [0x00BE] = new("Razer DeathAdder V4 Pro (Wired)", (byte)0x1F), // DEATHADDER_V4_PRO_WIRED
            [0x00BF] = new("Razer DeathAdder V4 Pro (Wireless)", (byte)0x1F), // DEATHADDER_V4_PRO_WIRELESS
            [0x00C0] = new("Razer Viper V3 Pro (Wired)", (byte)0x1F), // VIPER_V3_PRO_WIRED
            [0x00C1] = new("Razer Viper V3 Pro (Wireless)", (byte)0x1F), // VIPER_V3_PRO_WIRELESS
            [0x00C2] = new("Razer DeathAdder V3 Pro Wired Alt", (byte)0x1F), // DEATHADDER_V3_PRO_WIRED_ALT
            [0x00C3] = new("Razer DeathAdder V3 Pro Wireless Alt", (byte)0x1F), // DEATHADDER_V3_PRO_WIRELESS_ALT
            [0x00C4] = new("Razer DeathAdder V3 HyperSpeed (Wired)", (byte)0x1F), // DEATHADDER_V3_HYPERSPEED_WIRED
            [0x00C5] = new("Razer DeathAdder V3 HyperSpeed (Wireless)", (byte)0x1F), // DEATHADDER_V3_HYPERSPEED_WIRELESS
            [0x00C7] = new("Razer Pro Click V2 Vertical Edition (Wired)", (byte)0x1F), // PRO_CLICK_V2_VERTICAL_EDITION_WIRED
            [0x00C8] = new("Razer Pro Click V2 Vertical Edition (Wireless)", (byte)0x1F), // PRO_CLICK_V2_VERTICAL_EDITION_WIRELESS
            [0x00CB] = new("Razer Basilisk V3 35K", (byte)0x1F), // BASILISK_V3_35K
            [0x00CC] = new("Razer Basilisk V3 Pro 35K (Wired)", (byte)0x1F), // BASILISK_V3_PRO_35K_WIRED
            [0x00CD] = new("Razer Basilisk V3 Pro 35K (Wireless)", (byte)0x1F), // BASILISK_V3_PRO_35K_WIRELESS
            [0x00D0] = new("Razer Pro Click V2 (Wired)", (byte)0x1F), // PRO_CLICK_V2_WIRED
            [0x00D1] = new("Razer Pro Click V2 (Wireless)", (byte)0x1F), // PRO_CLICK_V2_WIRELESS
            [0x00D3] = new("Razer Basilisk Mobile (Wired)", (byte)0x1F), // BASILISK_MOBILE_WIRED
            [0x00D4] = new("Razer Basilisk Mobile (Receiver)", (byte)0x1F), // BASILISK_MOBILE_RECEIVER
            [0x00D6] = new("Razer Basilisk V3 Pro 35K Phantom Green Edition (Wired)", (byte)0x1F), // BASILISK_V3_PRO_35K_PHANTOM_GREEN_EDITION_WIRED
            [0x00D7] = new("Razer Basilisk V3 Pro 35K Phantom Green Edition (Wireless)", (byte)0x1F), // BASILISK_V3_PRO_35K_PHANTOM_GREEN_EDITION_WIRELESS
            [0x00DA] = new("Razer Cobra HyperSpeed (Wired)", (byte)0x1F), // COBRA_HYPERSPEED_WIRED
            [0x00DB] = new("Razer Cobra HyperSpeed (Wireless)", (byte)0x1F), // COBRA_HYPERSPEED_WIRELESS
            [0x00DE] = new("Razer Viper V3 Pro SE (Wired)", (byte)0x1F), // VIPER_V3_PRO_SE_WIRED
            [0x00DF] = new("Razer Viper V3 Pro SE (Wireless)", (byte)0x1F), // VIPER_V3_PRO_SE_WIRELESS
        };
}
