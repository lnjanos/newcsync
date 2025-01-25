using System;
using System.Numerics;
using CrystalSync;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace CrystalSync.Windows
{

    public class ConfigWindow : Window, IDisposable
    {
        private Configuration Configuration;
        
        private Plugin Plugin;

        public ConfigWindow(Plugin plugin)
            : base("Configuration###ConfigWindow", (ImGuiWindowFlags)0, false)
        {
            ((Window)this).Flags = (ImGuiWindowFlags)58;
            ((Window)this).Size = new Vector2(450f, 250f);
            Configuration = plugin.Configuration;

            TitleBarButtons.Add(Support.NavBarBtn);

            Plugin = plugin;
        }

        public void Dispose()
        {
        }

        public override void Draw()
        {
            Support.DrawRight();

            if (!ImGui.BeginTabBar("ConfigTabs"))
            {
                return;
            }
            if (ImGui.BeginTabItem("General"))
            {
                if (Configuration.IsRegistered)
                {
                    if (ImGui.Button("Unlink Discord Account"))
                    {
                        Unlink();
                    }
                }
                else
                {
                    ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), "You are not linked to Discord.");
                }
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Message Settings"))
            {
                ImGui.Text("Enable or disable specific message types:");
                bool sendTells = Configuration.SendTells;
                if (ImGui.Checkbox("Send Tells", ref sendTells))
                {
                    Configuration.SendTells = sendTells;
                    Configuration.Save();
                }
                bool sendPartys = Configuration.SendPartys;
                if (ImGui.Checkbox("Send Party Messages", ref sendPartys))
                {
                    Configuration.SendPartys = sendPartys;
                    Configuration.Save();
                }
                bool sendEmotes = Configuration.SendEmotes;
                if (ImGui.Checkbox("Send Emotes", ref sendEmotes))
                {
                    Configuration.SendEmotes = sendEmotes;
                    Configuration.Save();
                }
                bool sendDuties = Configuration.SendDuty;
                if (ImGui.Checkbox("Send DutyFinder", ref sendDuties))
                {
                    Configuration.SendDuty = sendDuties;
                    Configuration.Save();
                }
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Colors"))
            {
                ImGui.Text("Customize the colors for different message types:");
                ImGuiColorEditFlags colorPickerFlags = (ImGuiColorEditFlags)4194336;
                Vector4 tellColor = Configuration.tellColor;
                if (ImGui.ColorEdit4("Tell Color", ref tellColor, colorPickerFlags))
                {
                    Configuration.tellColor = tellColor;
                    Configuration.Save();
                }
                Vector4 partyColor = Configuration.partyColor;
                if (ImGui.ColorEdit4("Party Color", ref partyColor, colorPickerFlags))
                {
                    Configuration.partyColor = partyColor;
                    Configuration.Save();
                }
                Vector4 emoteColor = Configuration.emoteColor;
                if (ImGui.ColorEdit4("Emote Color", ref emoteColor, colorPickerFlags))
                {
                    Configuration.emoteColor = emoteColor;
                    Configuration.Save();
                }
                Vector4 dutyColor = Configuration.dutyColor;
                if (ImGui.ColorEdit4("Duty Color", ref dutyColor, colorPickerFlags))
                {
                    Configuration.dutyColor = dutyColor;
                    Configuration.Save();
                }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        private void Unlink()
        {
            Configuration.IsRegistered = false;
            Configuration.Token = string.Empty;
            Configuration.DiscordId = string.Empty;
            Configuration.Save();
            Plugin.ToggleRegistrationUI();
        }
    }
}
