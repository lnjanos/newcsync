using System;
using System.Diagnostics;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CrystalSync;
using CrystalSync.Api;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace CrystalSync.Windows
{
    public class RegistrationWindow : Window, IDisposable
    {
        private readonly Plugin Plugin;

        private int currentStep = 1;

        private string TokenInput = string.Empty;

        private bool IsProcessing = false;

        private string StatusMessage = string.Empty;

        public RegistrationWindow(Plugin plugin)
            : base("Registration###RegistrationWindow", (ImGuiWindowFlags)0, false)
        {
            ((Window)this).Flags = (ImGuiWindowFlags)34;
            Plugin = plugin;
            ((Window)this).Size = new Vector2(400f, 200f);
            ((Window)this).SizeCondition = (ImGuiCond)1;
        }

        public void Dispose()
        {
        }

        public override void Draw()
        {
            ImGui.TextWrapped("Welcome to CrystalSync! Please follow the steps below to link your Discord account.");
            ImGui.Separator();
            switch (currentStep)
            {
                case 1:
                    DrawStep1();
                    break;
                case 2:
                    DrawStep2();
                    break;
                case 3:
                    DrawStep3();
                    break;
                default:
                    ImGui.Text("Unknown step.");
                    break;
            }
            if (!string.IsNullOrEmpty(StatusMessage))
            {
                ImGui.Separator();
                ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), StatusMessage);
            }
            if (Plugin.Configuration.IsRegistered)
            {
                ((Window)this).IsOpen = false;
            }
        }

        private void DrawStep1()
        {
            ImGui.Text("Step 1: Join our Discord Server to get started.");
            if (ImGui.Button("Join Discord"))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://discord.gg/gB2A4kSG6g",
                    UseShellExecute = true
                });
            }
            if (ImGui.Button("Next"))
            {
                currentStep = 2;
            }
        }

        private void DrawStep2()
        {
            ImGui.Text("Step 2: In Discord, use the `/register` command to generate your unique token.");
            ImGui.TextWrapped("After running the command, you will receive a token in Discord. Click 'Next' once you have your token.");
            if (ImGui.Button("Back"))
            {
                currentStep = 1;
            }
            if (ImGui.Button("Next"))
            {
                currentStep = 3;
            }
        }

        private void DrawStep3()
        {
            ImGui.Text("Step 3: Enter your token below to link your Discord account.");
            ImGui.InputText("Token", ref TokenInput, 100u);
            if (ImGui.Button("Register") && !IsProcessing)
            {
                RegisterAsync(TokenInput);
            }
            if (IsProcessing)
            {
                ImGui.SameLine();
                ImGui.Text("Processing...");
            }
            if (ImGui.Button("Back"))
            {
                currentStep = 2;
            }
        }

        private async Task RegisterAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusMessage = "Token cannot be empty.";
                return;
            }
            IsProcessing = true;
            StatusMessage = "Processing registration...";
            try
            {
                using HttpClient client = new HttpClient();
                RegisterRequest request = new RegisterRequest
                {
                    token = token,
                    character_name = Plugin.GetCharacterName(),
                    server_name = Plugin.GetServerName()
                };
                string json = JsonSerializer.Serialize(request);
                HttpResponseMessage response = await client.PostAsync(content: new StringContent(json, Encoding.UTF8, "application/json"), requestUri: Configuration.BaseUrl + "/verify-token");
                if (response.IsSuccessStatusCode)
                {
                    RegisterResponse registerResponse = JsonSerializer.Deserialize<RegisterResponse>(await response.Content.ReadAsStringAsync());
                    if (registerResponse != null)
                    {
                        Plugin.Configuration.IsRegistered = true;
                        Plugin.Configuration.DiscordId = registerResponse.discord_id;
                        Plugin.Configuration.Token = registerResponse.token;
                        Plugin.Configuration.Save();
                        StatusMessage = "Registration successful!";
                    }
                    else
                    {
                        StatusMessage = "Invalid response from server.";
                    }
                }
                else
                {
                    StatusMessage = "Error: " + await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                Exception ex2 = ex;
                StatusMessage = "Exception: " + ex2.Message;
            }
            finally
            {
                IsProcessing = false;
                Plugin.ForceOpenMainUI();
            }
        }
    }
}
