using System;
using System.Numerics;
using CrystalSync;
using CrystalSync.Api;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace CrystalSync.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private Plugin Plugin;

        private WebSocketClient WebSocketClient;

        private bool isProcessing = false;

        public MainWindow(Plugin plugin)
            : base("CrystalSync##MainWindow", (ImGuiWindowFlags)24, false)
        {
            //IL_004d: Unknown result type (might be due to invalid IL or missing references)
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400f, 200f),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
            Plugin = plugin;
            WebSocketClient = new WebSocketClient(plugin);
        }

        public void Dispose()
        {
        }

        public override void Draw()
        {
            ImGui.BeginChild("MainSection", new Vector2(0f, 0f), false, (ImGuiWindowFlags)2048);
            ImGui.TextColored(ImGuiColors.ParsedGold, "CrystalSync Status:");
            ImGui.Separator();
            bool running = Plugin.Configuration.isRunning;
            if (ImGui.Checkbox("Activate WebSocket", ref running))
            {
                Plugin.Configuration.isRunning = running;
                Plugin.Configuration.Save();
                if (running)
                {
                    ToggleWebSocketAsync(enable: true);
                }
                else
                {
                    ToggleWebSocketAsync(enable: false);
                }
            }
            if (isProcessing)
            {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudYellow, "Processing...");
            }
            ImGui.Separator();
            bool apiConnected = Plugin.Configuration.APIConnected;
            bool tryingReconnect = Plugin.Configuration.TryingAPIReconnect;
            ImGui.Text("WebSocket State:");
            ImGui.SameLine();
            if (apiConnected)
            {
                ImGui.TextColored(ImGuiColors.ParsedGreen, "Connected");
            }
            else
            {
                ImGui.TextColored(ImGuiColors.DalamudRed, "Disconnected");
            }
            if (tryingReconnect)
            {
                ImGui.TextColored(ImGuiColors.DalamudYellow, "Attempting to reconnect...");
            }
            else
            {
                ImGui.Text("No reconnect attempt in progress.");
            }
            ImGui.Separator();
            ImGui.Text("Registered: " + (Plugin.Configuration.IsRegistered ? "Yes" : "No"));
            ImGui.Text("Discord ID: " + Plugin.Configuration.DiscordId);
            ImGui.Separator();
            if (ImGui.Button("Open Config"))
            {
                Plugin.ToggleConfigUI();
            }
            ImGui.EndChild();
        }

        public async void ToggleWebSocketAsync(bool enable)
        {
            isProcessing = true;
            if (!enable)
            {
                await WebSocketClient.DisconnectAsync();
            }
            else
            {
                await WebSocketClient.ConnectAsync();
            }
            isProcessing = false;
        }
    }
}
