using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using ECommons.EzSharedDataManager;
using ECommons.ImGuiMethods;
using ECommons;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager.Delegates;
using System.Drawing;

namespace CrystalSync.Windows
{
    public static class Support
    {
        public static Window.TitleBarButton NavBarBtn = new Window.TitleBarButton
        {
            Click = (m) => { GenericHelpers.ShellStart("https://ko-fi.com/kkcuy"); },
            Icon = FontAwesomeIcon.Heart,
            ShowTooltip = () => ImGui.SetTooltip($"Support {Svc.PluginInterface.Manifest.Name}")
        };

        public static Func<bool> IsOfficialPlugin = () => false;
        public static string Text = "♥ ko-fi";
        public static string DonateLink => "https://ko-fi.com/kkcuy";
        public static void DrawRaw()
        {
            DrawButton();
        }

        private static uint ColorNormal
        {
            get
            {
                var vector1 = ImGuiEx.Vector4FromRGB(0x022594);
                var vector2 = ImGuiEx.Vector4FromRGB(0x940238);

                var gen = GradientColor.Get(vector1, vector2).ToUint();
                var data = EzSharedData.GetOrCreate<uint[]>("ECommonsPatreonBannerRandomColor", [gen]);
                if (!GradientColor.IsColorInRange(data[0].ToVector4(), vector1, vector2))
                {
                    data[0] = gen;
                }
                return data[0];
            }
        }

        private static uint ColorHovered => ColorNormal;

        private static uint ColorActive => ColorNormal;

        private static readonly uint ColorText = 0xFFFFFFFF;

        public static void DrawButton()
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ColorNormal);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColorHovered);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColorActive);
            ImGui.PushStyleColor(ImGuiCol.Text, ColorText);
            if (ImGui.Button(Text))
            {
                GenericHelpers.ShellStart(DonateLink);
            }
            Popup();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            ImGui.PopStyleColor(4);
        }

        public static void RightTransparentTab(string? text = null)
        {
            text ??= Text;
            var textWidth = ImGui.CalcTextSize(text).X;
            var spaceWidth = ImGui.CalcTextSize(" ").X;
            ImGui.BeginDisabled();
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0f);
            if (ImGuiEx.BeginTabItem(" ".Repeat((int)MathF.Ceiling(textWidth / spaceWidth)), ImGuiTabItemFlags.Trailing))
            {
                ImGui.EndTabItem();
            }
            ImGui.PopStyleVar();
            ImGui.EndDisabled();
        }

        public static void DrawRight()
        {
            var cur = ImGui.GetCursorPos();
            ImGui.SetCursorPosX(cur.X + ImGui.GetContentRegionAvail().X - ImGuiHelpers.GetButtonSize(Text).X);
            DrawRaw();
            ImGui.SetCursorPos(cur);
        }

        private static string PatreonButtonTooltip => $"""
				If you like {Svc.PluginInterface.Manifest.Name}, please consider supporting it's developer via ko-fi or via other means! 
				
				This will help me to update the plugin while granting you access to priority feature requests, priority support, early plugin builds, participation in votes for features and more.

				Left click - to go to ko-fi;
				""";

        private static string SmallPatreonButtonTooltip => $"""
				If you like {Svc.PluginInterface.Manifest.Name}, please consider supporting it's developer via Patreon.

				Left click - to go to ko-fi;
				""";

        private static void Popup()
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
                ImGuiEx.Text(IsOfficialPlugin() ? SmallPatreonButtonTooltip : PatreonButtonTooltip);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                
            }
            if (ImGui.BeginPopup("NXPS"))
            {
                if (ImGui.Selectable("Subscribe on ko-fi"))
                {
                    GenericHelpers.ShellStart("https://ko-fi.com/kkcuy");
                }
                /*if ("Donate one-time via Ko-Fi")
                {
                    GenericHelpers.ShellStart("https://donate.nightmarexiv.com");
                }*/
                if (ImGui.Selectable("Donate via Cryptocurrency"))
                {
                    GenericHelpers.ShellStart($"https://crypto.nightmarexiv.com/{(IsOfficialPlugin() ? "?" + Svc.PluginInterface.Manifest.Name : "")}");
                }
                if (!IsOfficialPlugin())
                {
                    if (ImGui.Selectable("Join NightmareXIV Discord"))
                    {
                        GenericHelpers.ShellStart("https://discord.nightmarexiv.com");
                    }
                    if (ImGui.Selectable("Explore other NightmareXIV plugins"))
                    {
                        GenericHelpers.ShellStart("https://explore.nightmarexiv.com");
                    }
                }
                ImGui.EndPopup();
            }
        }
    }
}
