using System;
using System.Collections.Generic;
using System.Numerics;
using CrystalSync;
using Dalamud.Configuration;


namespace CrystalSync {
[Serializable]
    public class Configuration : IPluginConfiguration
    {
        public Vector4 tellColor = new Vector4(1f, 0.72f, 0.87f, 1f);

        public Vector4 partyColor = new Vector4(0.4f, 0.9f, 1f, 1f);

        public Vector4 emoteColor = new Vector4(0.63f, 0.02f, 0f, 1f);

        public Vector4 dutyColor = new Vector4(0.86f, 0.95f, 0.59f, 1f);

        public static string BaseUrl = "https://api-liz.com/crystalsync";

        public int Version { get; set; } = 1;

        public bool IsConfigWindowMovable { get; set; } = true;

        public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

        public bool isRunning { get; set; } = false;

        public bool APIConnected { get; set; } = false;

        public bool TryingAPIReconnect { get; set; } = false;

        public bool SendTells { get; set; } = true;

        public bool SendPartys { get; set; } = true;

        public bool SendEmotes { get; set; } = true;

        public bool SendDuty { get; set; } = true;

        public bool IsRegistered { get; set; } = false;

        public string Token { get; set; } = string.Empty;

        public string DiscordId { get; set; } = string.Empty;

        public static readonly Dictionary<string, int> ConfirmValues = new()
        {
            { "commence", 8 },
            { "withdraw", 9 },
            { "wait", 11 }
        };

        public void Save()
        {
            Plugin.PluginInterface.SavePluginConfig((IPluginConfiguration)(object)this);
        }
    }
}
