using Dalamud.Plugin.Services;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Text.Json;

namespace CrystalSync.Api
{
    internal class WebSocketClient
    {
        private readonly Plugin _plugin;

        private ClientWebSocket _webSocket;

        private CancellationTokenSource _cancellationTokenSource;

        private Task _receiveTask;

        private Task _heartbeatTask;

        private bool _isReconnecting = false;

        private IChatGui chatgui;

        public bool IsConnected
        {
            get
            {
                ClientWebSocket webSocket = _webSocket;
                return webSocket != null && webSocket.State == WebSocketState.Open;
            }
        }

        public WebSocketClient(Plugin plugin)
        {
            _plugin = plugin;
        }

        public async Task ConnectAsync()
        {
            if (IsConnected)
            {
                return;
            }
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            string wsUrl = "wss://api-liz.com/crystalsync/ws/" + _plugin.Configuration.Token;
            try
            {
                await _webSocket.ConnectAsync(new Uri(wsUrl), _cancellationTokenSource.Token);
                _plugin.Configuration.isRunning = true;
                _plugin.Configuration.APIConnected = true;
                _plugin.Configuration.TryingAPIReconnect = false;
                _plugin.Configuration.Save();
                _receiveTask = Task.Run((Func<Task?>)ReceiveLoop);
                _heartbeatTask = Task.Run((Func<Task?>)SendHeartbeat);
            }
            catch (Exception ex)
            {
                Plugin.ChatGui.PrintError("WebSocket connection failed: " + ex.Message, (string)null, (ushort?)null);
                _plugin.Configuration.APIConnected = false;
                _plugin.Configuration.isRunning = false;
                _plugin.Configuration.Save();
                if (!_isReconnecting)
                {
                    Task.Run((Func<Task?>)TryReconnectAsync);
                }
            }
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected && _webSocket == null)
            {
                return;
            }
            _cancellationTokenSource?.Cancel();
            try
            {
                if (IsConnected)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                _webSocket?.Dispose();
                _webSocket = null;
                _plugin.Configuration.isRunning = false;
                _plugin.Configuration.APIConnected = false;
                _plugin.Configuration.Save();
            }
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[4096];
            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.IsCancellationRequested)
                {
                    WebSocketReceiveResult result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await DisconnectAsync();
                        break;
                    }
                    int count = result.Count;
                    while (!result.EndOfMessage)
                    {
                        if (count >= buffer.Length)
                        {
                            await DisconnectAsync();
                            throw new Exception("Message too long to handle.");
                        }
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), _cancellationTokenSource.Token);
                        count += result.Count;
                    }
                    string message = Encoding.UTF8.GetString(buffer, 0, count);
                    HandleMessage(message);
                }
                if (!IsConnected && !_cancellationTokenSource.IsCancellationRequested && !_isReconnecting)
                {
                    Task.Run((Func<Task?>)TryReconnectAsync);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex2)
            {
                Plugin.ChatGui.PrintError("WebSocket receive error: " + ex2.Message, (string)null, (ushort?)null);
                await DisconnectAsync();
                if (!_isReconnecting)
                {
                    Task.Run((Func<Task?>)TryReconnectAsync);
                }
            }
        }

        private void HandleMessage(string message)
        {
            try
            {
                if (message == "pong" || message == "ping")
                {
                    return;
                }
                string content = string.Empty;
                string emote_command = string.Empty;
                string target = string.Empty;
                JObject json = JObject.Parse(message);
                string senderType = ((object)json["sender_type"])?.ToString();
                string token = ((object)json["token"])?.ToString();
                string senderId = ((object)json["sender_id"])?.ToString();
                string receiverId = ((object)json["receiver_id"])?.ToString();
                if (senderType == "emote")
                {
                    emote_command = ((object)json["emote_command"])?.ToString();
                    target = ((object)json["target"])?.ToString();
                    _plugin.SendEmote(receiverId, emote_command, target);
                    chatgui.PrintError("emote send 1", (string)null, (ushort?)null);
                    return;
                }
                content = ((object)json["content"])?.ToString();
                if (token != _plugin.Configuration.Token)
                {
                    Plugin.ChatGui.PrintError("Received message with invalid token.", (string)null, (ushort?)null);
                }
                else if (senderType == "tell")
                {
                    _plugin.SendDM(receiverId, content);
                }
                else if (senderType == "party")
                {
                    _plugin.SendParty(content);
                }
            }
            catch (Exception)
            {
            }
        }

        private async Task SendHeartbeat()
        {
            while (IsConnected && !_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(30000, _cancellationTokenSource.Token);
                    await SendPing();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex2)
                {
                    Plugin.ChatGui.PrintError("Heartbeat error: " + ex2.Message, (string)null, (ushort?)null);
                    await DisconnectAsync();
                    if (!_isReconnecting)
                    {
                        Task.Run((Func<Task?>)TryReconnectAsync);
                    }
                }
            }
        }

        private async Task SendPing()
        {
            if (!IsConnected)
            {
                return;
            }
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes("ping");
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, endOfMessage: true, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Plugin.ChatGui.PrintError("Failed to send ping: " + ex.Message, (string)null, (ushort?)null);
                await DisconnectAsync();
                if (!_isReconnecting)
                {
                    Task.Run((Func<Task?>)TryReconnectAsync);
                }
            }
        }

        public async Task SendMessageAsync(object message)
        {
            if (!IsConnected)
            {
                return;
            }
            string json = JsonSerializer.Serialize(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ArraySegment<byte> segment = new ArraySegment<byte>(bytes);
            try
            {
                await _webSocket.SendAsync(segment, WebSocketMessageType.Text, endOfMessage: true, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Plugin.ChatGui.PrintError("Failed to send WebSocket message: " + ex.Message, (string)null, (ushort?)null);
            }
        }

        private async Task TryReconnectAsync()
        {
            _isReconnecting = true;
            _plugin.Configuration.TryingAPIReconnect = true;
            _plugin.Configuration.Save();
            while (!_cancellationTokenSource.IsCancellationRequested && !IsConnected)
            {
                await Task.Delay(5000);
                try
                {
                    await ConnectAsync();
                }
                catch
                {
                }
            }
            _plugin.Configuration.TryingAPIReconnect = false;
            _plugin.Configuration.Save();
            _isReconnecting = false;
        }
    }
}
