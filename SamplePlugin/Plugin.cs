using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CrystalSync;
using CrystalSync.Api;
using CrystalSync.Helpers;
using CrystalSync.Windows;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using static Dalamud.Game.Command.IReadOnlyCommandInfo;
using static Dalamud.Plugin.Services.IChatGui;
using static Dalamud.Plugin.Services.IFramework;
using static Dalamud.Plugin.Services.IGameInteropProvider;
using static System.Diagnostics.Activity;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using ClickLib;
using ClickLib.Clicks;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;
using ECommons.DalamudServices.Legacy;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Data.Parsing;

public sealed class Plugin : IDalamudPlugin, IDisposable
{
    private delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);

    private const string CommandName = "/crystalsync";

    private const string CommandAlias = "/csync";

    private readonly Dictionary<uint, string> worldNames = new Dictionary<uint, string>();

    private WebSocketClient WebSocketClient;

    public readonly WindowSystem WindowSystem = new WindowSystem("CrystalSync");

    private string localPlayerName = null;

    private readonly Hook<OnEmoteFuncDelegate> hookEmote;

    private readonly Dictionary<uint, List<string>> emoteNames = new Dictionary<uint, List<string>>();

    public string Name => "CrystalSync";

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; }

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; }

    [PluginService]
    internal static IClientState ClientState { get; private set; }

    [PluginService]
    internal static IObjectTable ObjectTable { get; private set; }

    [PluginService]
    internal static IFramework Framework { get; private set; }

    [PluginService]
    internal static ITargetManager TargetManager { get; private set; }

    [PluginService]
    internal static IChatGui ChatGui { get; private set; }

    [PluginService]
    internal static IDataManager DataManager { get; private set; }
    [PluginService]
    internal static ICondition Condition { get; private set; }

    public Configuration Configuration { get; init; }

    private ConfigWindow ConfigWindow { get; init; }

    private MainWindow MainWindow { get; init; }

    private RegistrationWindow RegistrationWindow { get; init; }

    [PluginService]
    internal static IGameInteropProvider GameInteropProvider { get; private set; }

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, (IDalamudPlugin)(object)this);
        WebSocketClient = new WebSocketClient(this);
        Configuration = (PluginInterface.GetPluginConfig() as Configuration) ?? new Configuration();
        Configuration.Save();
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        RegistrationWindow = new RegistrationWindow(this);
        WindowSystem.AddWindow((Window)(object)ConfigWindow);
        WindowSystem.AddWindow((Window)(object)MainWindow);
        Svc.Chat.ChatMessage += new OnMessageDelegate(OnChatMessage);
        hookEmote = GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", (OnEmoteFuncDelegate)OnEmoteDetour, (HookBackend)0);
        hookEmote.Enable();
        if (!Configuration.IsRegistered)
        {
            WindowSystem.AddWindow((Window)(object)RegistrationWindow);
        }
        if (Configuration.isRunning)
        {
            MainWindow.ToggleWebSocketAsync(enable: true);
        }
        CommandManager.AddHandler("/crystalsync", new CommandInfo(new HandlerDelegate(OnCommand))
        {
            HelpMessage = "Opens the CrystalSync plugin window."
        });
        CommandManager.AddHandler("/csync", new CommandInfo(new HandlerDelegate(OnCommand))
        {
            HelpMessage = "Alias -> /crystalsync."
        });
        PluginInterface.UiBuilder.Draw += DrawUI;
        Framework.Update += new OnUpdateDelegate(OnUpdate);

        LoadWorlds();
        LoadEmotes();

        Condition.ConditionChange += OnConditionChange;

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    // -----------------------------------------------
    // 1) Condition-Event
    // -----------------------------------------------
    public void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (!Configuration.isRunning || !Configuration.SendDuty) return;

        // Wenn eine Duty gefunden wird
        if (flag == ConditionFlag.WaitingForDutyFinder && value)
        {

            var addonPtr = Svc.GameGui.GetAddonByName("ContentsFinderConfirm");
            if (addonPtr == IntPtr.Zero)
                return;

            // Erstellen der SendMessageRequest
            SendMessageRequest sendMessage = new SendMessageRequest
            {
                token = Configuration.Token,
                sender_type = "queue",
                sender_id = "Duty Finder",
                receiver_id = Configuration.DiscordId,
                content = "You have found a Duty.",
                color = Utils.Vector4ToHex(Configuration.dutyColor)
            };

            // Asynchrones Weiterleiten der Nachricht an Discord
            ForwardMessageToDiscordAsync(sendMessage);
        }
    }

    // Einstieg, kein 'unsafe', kein 'async'
    public void HandleDutyFinder(string action)
    {
        if ( Configuration.ConfirmValues.TryGetValue(action, out var act) )
        {
            ConfirmDutyAsync(0, act);
        } else
        {
            ConfirmDutyAsync(0, Configuration.ConfirmValues["wait"]);
        }
        
    }

    // -----------------------------------------------
    // 2) Die asynchrone Methode mit Retry
    // -----------------------------------------------
    public async void ConfirmDutyAsync(int tries, int buttonValue)
    {
        // Nach 3 Versuchen abbrechen
        if (tries >= 3)
        {
            return;
        }

        // "Unsicheren" Teil in eine unsafe-Methode packen
        bool success;

        unsafe
        {
            success = TryWithdrawUnsafe(buttonValue);
        }
        await Task.Delay(2000);


        return;
    }

    // -----------------------------------------------
    // 3) Der "unsafe" Teil: FireCallback auf das Addon
    // -----------------------------------------------
    private unsafe bool TryWithdrawUnsafe(int buttonValue)
    {
        // 1) Addon holen
        var addonPtr = Svc.GameGui.GetAddonByName("ContentsFinderConfirm");
        if (addonPtr == IntPtr.Zero)
            return false;

        var addon = (AddonContentsFinderConfirm*)addonPtr;
        // Prüfen, ob überhaupt da und sichtbar
        if (!addon->IsVisible)
            return false;

        // Man will zwar "Withdraw", aber wir schauen zumindest,
        // ob der Pointer existiert.
        if (addon->WithdrawButton == null)
            return false;

        // 2) FireCallback mit 2 AtkValues
        //    - param[0]: bestimmt, welcher Button (z.B. 2 oder 3 = "Withdraw")
        //    - param[1]: i. d. R. 0
        // Du kannst hier testweise 0..3 durchprobieren:

        PressButtonWithCallback(addon, buttonValue);

        return true;
    }

    // -----------------------------------------------
    // 4) Eigentliche "FireCallback"-Helfermethode
    // -----------------------------------------------
    private static unsafe void PressButtonWithCallback(AddonContentsFinderConfirm* addon, int buttonValue)
    {
        // 2 AtkValue Felder, in der Regel INT-INT (ValueType=2 => Int)
        //  - param[0] = ButtonId (0=Commence, 1=Wait, 2=Withdraw => je nach Patch)
        //  - param[1] = meistens 0
        const int paramCount = 2;
        var values = stackalloc AtkValue[paramCount];

        values[0] = new AtkValue
        {
            Type = ValueType.Int,
            Int = buttonValue // 2 oder 3 => Withdraw, je nach Patch
        };
        values[1] = new AtkValue
        {
            Type = ValueType.Int,
            Int = 0
        };

        // FireCallback => ruft intern das entsprechende UI-Event auf
        addon->FireCallback((uint)paramCount, (FFXIVClientStructs.FFXIV.Component.GUI.AtkValue*)values, false);
    }

    public void LoadEmotes()
    {
        // Holen des ExcelSheets mit Emotes aus dem DataManager
        ExcelSheet<Emote> emotes = DataManager.GetExcelSheet<Emote>(null, null);

        // Überprüfen, ob das ExcelSheet erfolgreich geladen wurde
        if (emotes != null)
        {
            // Durchlaufen aller Emotes im ExcelSheet
            foreach (Emote emote in emotes)
            {
                // Standardwert für den Emote-Befehl
                string emoteCommand = "Unknown";

                // Überprüfen, ob der Emote einen gültigen TextCommand hat
                if (emote.TextCommand.ValueNullable.HasValue)
                {
                    // Extrahieren des Befehls aus dem TextCommand
                    emoteCommand = emote.TextCommand.Value.Command.ExtractText();
                }

                // Extrahieren der RowId des Emotes
                uint rowId = emote.RowId;

                // Extrahieren des Namens des Emotes
                string emoteName = emote.Name.ExtractText();

                // Erstellen einer Liste mit dem Emote-Namen und -Befehl
                List<string> emoteDetails = new List<string> { emoteName, emoteCommand };

                // Hinzufügen der Emote-Details zur Dictionary mit RowId als Schlüssel
                emoteNames[rowId] = emoteDetails;
            }
        }
    }

    public void LoadWorlds()
    {
        ExcelSheet<World> worldSheet = DataManager.GetExcelSheet<World>(null, null);

        if (worldSheet == null)
        {
            return;
        }

        foreach (World world in worldSheet)
        {
            uint rowId = world.RowId;

            string worldName = world.Name.ExtractText() ?? "Unbekannt";

            worldNames[rowId] = worldName;
        }
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        RegistrationWindow.Dispose();
        WebSocketClient?.DisconnectAsync().ConfigureAwait(continueOnCapturedContext: false);
        hookEmote?.Dispose();
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
        Framework.Update -= new OnUpdateDelegate(OnUpdate);
        Svc.Chat.ChatMessage -= new OnMessageDelegate(OnChatMessage);
        CommandManager.RemoveHandler("/crystalsync");
        CommandManager.RemoveHandler("/csync");
    }

    public void OnUpdate(IFramework framework)
    {
    }

    public void OnChatMessage(XivChatType type, int a2, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        // Überprüfen, ob die Konfiguration aktiviert ist und ob Nachrichten des entsprechenden Typs gesendet werden sollen
        if (!Configuration.isRunning ||
            (!Configuration.SendPartys && !Configuration.SendTells) ||
            !ShouldProcessMessage(type))
        {
            return;
        }

        // Dekodieren des Senders und Überprüfen, ob es erfolgreich war
        if (!Utils.DecodeSender(sender, type, out Dictionary<string, uint> players))
        {
            return;
        }

        foreach (var player in players)
        {
            string playerName = $"{player.Key}@{GetWorldName(player.Value)}";

            // Initialisieren des lokalen Spielernamens, falls noch nicht geschehen
            if (string.IsNullOrEmpty(localPlayerName))
            {
                localPlayerName = $"{ClientState.LocalPlayer.Name.TextValue}@{GetWorldName(ClientState.LocalPlayer.HomeWorld.RowId)}";
            }

            // Überspringen der eigenen Nachrichten
            if (playerName.Equals(localPlayerName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string messageContent = message.TextValue;

            // Bestimmen des Nachrichtentyps und der entsprechenden Farbe
            string senderType = type switch
            {
                XivChatType.TellIncoming => "tell",
                _ => "party"
            };

            string colorHex = type switch
            {
                XivChatType.TellIncoming => Utils.Vector4ToHex(Configuration.tellColor),
                _ => Utils.Vector4ToHex(Configuration.partyColor),
            };

            // Erstellen der SendMessageRequest
            SendMessageRequest sendMessage = new SendMessageRequest
            {
                token = Configuration.Token,
                sender_type = senderType,
                sender_id = playerName,
                receiver_id = Configuration.DiscordId,
                content = messageContent,
                color = colorHex
            };

            // Asynchrones Weiterleiten der Nachricht an Discord
            ForwardMessageToDiscordAsync(sendMessage);
        }
    }

    /// <summary>
    /// Überprüft, ob die Nachricht basierend auf ihrem Typ verarbeitet werden soll.
    /// </summary>
    /// <param name="type">Der Typ der Chatnachricht.</param>
    /// <returns>True, wenn die Nachricht verarbeitet werden soll; andernfalls False.</returns>
    private bool ShouldProcessMessage(XivChatType type)
    {
        return (type == XivChatType.TellIncoming && Configuration.SendTells) ||
               ((type == XivChatType.Party || type == XivChatType.CrossParty) && Configuration.SendPartys);
    }


    public async Task ForwardMessageToDiscordAsync(SendMessageRequest message)
    {
        try
        {
            using HttpClient client = new HttpClient();
            string json = JsonSerializer.Serialize(message);
            HttpResponseMessage response = await client.PostAsync(content: new StringContent(json, Encoding.UTF8, "application/json"), requestUri: CrystalSync.Configuration.BaseUrl + "/send-to-discord");
            if (!response.IsSuccessStatusCode)
            {
                ChatGui.PrintError($"Failed to send message to Discord: {response.StatusCode}", (string)null, (ushort?)null);
            }
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            ChatGui.PrintError("Exception while sending message to Discord: " + ex2.Message, (string)null, (ushort?)null);
        }
    }

    public void SendDM(string name, string message)
    {
        if (message.Length <= 450)
        {
            Svc.Framework.RunOnTick(() =>
            {
                Chat.Instance.SendMessage("/tell " + name + " " + message);
            }, default(TimeSpan), 0, default(CancellationToken));
            return;
        }
        int parts = (int)Math.Ceiling((double)message.Length / 450.0);
        for (int i = 0; i < parts; i++)
        {
            string part = message.Substring(i * 450, Math.Min(450, message.Length - i * 450));
            Svc.Framework.RunOnTick(() =>
            {
                Chat.Instance.SendMessage("/tell " + name + " " + part);
            }, default(TimeSpan), 0, default(CancellationToken));
            Thread.Sleep(500);
        }
    }

    public void SendEmote(string name, string emote, string target)
    {
        IPlayerCharacter newTarget = FindCharacterInObjectTable(name);
        if (newTarget == null || !((IGameObject)newTarget).IsTargetable)
        {
            return;
        }
        IPlayerCharacter currentTarget = null;
        Svc.Framework.RunOnTick(() =>
        {
            ref IPlayerCharacter reference = ref currentTarget;
            IGameObject target2 = TargetManager.Target;
            reference = (IPlayerCharacter)(object)((target2 is IPlayerCharacter) ? target2 : null);
            if (newTarget != null && ((IGameObject)newTarget).IsTargetable)
            {
                TargetManager.Target = (IGameObject)(object)newTarget;
            }
        }, default(TimeSpan), 0, default(CancellationToken));
        Thread.Sleep(1000);
        Svc.Framework.RunOnTick(() =>
        {
            Chat.Instance.SendMessage(emote);
        }, default(TimeSpan), 0, default(CancellationToken));
        if (target == "keep")
        {
            return;
        }
        Thread.Sleep(3500);
        Svc.Framework.RunOnTick(() =>
        {
            if (currentTarget != null && ((IGameObject)currentTarget).IsTargetable)
            {
                TargetManager.Target = (IGameObject)(object)currentTarget;
            }
        }, default(TimeSpan), 0, default(CancellationToken));
    }

    public void SendParty(string message)
    {
        if (message.Length <= 450)
        {
            Svc.Framework.RunOnTick(() =>
            {
                Chat.Instance.SendMessage("/p " + message);
            }, default(TimeSpan), 0, default(CancellationToken));
            return;
        }
        int parts = (int)Math.Ceiling((double)message.Length / 450.0);
        for (int i = 0; i < parts; i++)
        {
            string part = message.Substring(i * 450, Math.Min(450, message.Length - i * 450));
            Svc.Framework.RunOnTick(() =>
            {
                Chat.Instance.SendMessage("/p " + part);
            }, default(TimeSpan), 0, default(CancellationToken));
            Thread.Sleep(500);
        }
    }

    private void OnCommand(string command, string args)
    {
        if (ClientState.LocalPlayer != null)
        {
            ToggleMainUI();
        }
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void ToggleConfigUI()
    {
        if (Configuration.IsRegistered)
        {
            ((Window)ConfigWindow).Toggle();
        }
        else
        {
            ((Window)RegistrationWindow).Toggle();
        }
    }

    public void ForceOpenMainUI()
    {
        ((Window)MainWindow).IsOpen = true;
    }

    public void ToggleMainUI()
    {
        if (Configuration.IsRegistered)
        {
            ((Window)MainWindow).Toggle();
        }
        else
        {
            ((Window)RegistrationWindow).Toggle();
        }
    }

    public void ToggleRegistrationUI()
    {
        if (!Configuration.IsRegistered)
        {
            if (!WindowSystem.Windows.Contains((Window)(object)RegistrationWindow))
            {
                WindowSystem.AddWindow((Window)(object)RegistrationWindow);
            }
            ((Window)RegistrationWindow).Toggle();
        }
    }

    public string GetCharacterName()
    {
        return ((IGameObject)ClientState.LocalPlayer).Name.TextValue ?? "UnknownCharacter";
    }

    public string GetServerName()
    {
        //IL_000c: Unknown result type (might be due to invalid IL or missing references)
        //IL_0011: Unknown result type (might be due to invalid IL or missing references)
        return GetWorldName(ClientState.LocalPlayer.CurrentWorld.RowId) ?? "UnknownServer";
    }

    public string GetWorldName(uint rowId)
    {
        if (worldNames.TryGetValue(rowId, out string worldName))
        {
            return worldName;
        }
        return "Unknown";
    }

    public List<string> GetEmoteName(uint rowId)
    {
        if (emoteNames.TryGetValue(rowId, out List<string> emoteValues))
        {
            return emoteValues;
        }
        int num = 2;
        List<string> list = new List<string>(num);
        CollectionsMarshal.SetCount(list, num);
        Span<string> span = CollectionsMarshal.AsSpan(list);
        int num2 = 0;
        span[num2] = "Unknown";
        num2++;
        span[num2] = "Unknown";
        num2++;
        return list;
    }

    private void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        //IL_00d2: Unknown result type (might be due to invalid IL or missing references)
        //IL_00d7: Unknown result type (might be due to invalid IL or missing references)
        if (!Configuration.isRunning || !Configuration.SendEmotes)
        {
            return;
        }
        IPlayerCharacter localplayer = ClientState.LocalPlayer;
        if (localplayer != null && targetId == ((IGameObject)localplayer).GameObjectId)
        {
            IGameObject instigatorOb = ((IEnumerable<IGameObject>)ObjectTable).FirstOrDefault((Func<IGameObject, bool>)((IGameObject x) => (ulong)(nint)x.Address == instigatorAddr));
            IPlayerCharacter playerCharacter = (IPlayerCharacter)(object)((instigatorOb is IPlayerCharacter) ? instigatorOb : null);
            if (playerCharacter != null)
            {
                List<string> emote = GetEmoteName(emoteId);
                SendEmoteRequest request = new SendEmoteRequest
                {
                    token = Configuration.Token,
                    sender_type = "emote",
                    sender_id = ((IGameObject)playerCharacter).Name.ExtractText() + "@" + GetWorldName(playerCharacter.HomeWorld.RowId),
                    receiver_id = Configuration.DiscordId,
                    content = "used `" + emote[0] + "` on you.",
                    emote_command = emote[1],
                    color = Utils.Vector4ToHex(Configuration.emoteColor)
                };
                SendEmoteToDiscord(request);
            }
        }
        hookEmote.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }

    public async void SendEmoteToDiscord(SendEmoteRequest request)
    {
        try
        {
            using HttpClient client = new HttpClient();
            string json = JsonSerializer.Serialize(request);
            HttpResponseMessage response = await client.PostAsync(content: new StringContent(json, Encoding.UTF8, "application/json"), requestUri: CrystalSync.Configuration.BaseUrl + "/send-to-discord-emote");
            if (!response.IsSuccessStatusCode)
            {
                ChatGui.PrintError($"Failed to send message to Discord: {response.StatusCode}", (string)null, (ushort?)null);
            }
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            ChatGui.PrintError("Exception while sending message to Discord: " + ex2.Message, (string)null, (ushort?)null);
        }
    }

    public IPlayerCharacter? FindCharacterInObjectTable(string playerName)
    {
        //IL_004a: Unknown result type (might be due to invalid IL or missing references)
        //IL_004f: Unknown result type (might be due to invalid IL or missing references)
        string[] name = playerName.Split('@');
        foreach (IGameObject obj in (IEnumerable<IGameObject>)ObjectTable)
        {
            IPlayerCharacter pc = (IPlayerCharacter)(object)((obj is IPlayerCharacter) ? obj : null);
            if (pc != null && ((IGameObject)pc).Name.TextValue == name[0] && GetWorldName(pc.HomeWorld.RowId) == name[1])
            {
                return pc;
            }
        }
        return null;
    }
}
