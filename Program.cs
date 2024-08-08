using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.VisualBasic;
using MineStatLib;
using Newtonsoft.Json;
using RconSharp;

namespace svstatus
{
    class RCONStatus
    {
        public static IMessageChannel statuschannel;
        public static IMessageChannel chatchannel;
        public struct JSONConfig()
        {
            public string serverip = "127.0.0.1";
            public int serverport = 25565;
            public string rconip = "127.0.0.1";
            public int rconport = 25575;
            public ulong statusChannel = 0;
            public ulong chatChannel = 0;
        }
        public static JSONConfig config;
        public static RconClient client;
        public static string ip = "127.0.0.1";
        public static Dictionary<string, string> gamemode = new() {
            {"survival", "Supervivencia" },
            {"creative", "Creativo"},
            {"adventure", "Adventura"}
        };

        private struct MessageData()
        {
            public string message = "";
            public string player = "";
            public long timestamp = 0;
        }
        private struct Players()
        {
            public string name = "";
            public float health = 0;
            public string address = "127.0.0.1";
            public int level = 0;
            public string uuid = "";
            public string gameMode = "";
            public bool isOp = false;
        }
        private struct PlayerData()
        {
            public int max = 0;
            public int current = 0;
            public List<Players> players = [];
        }
        public static bool started = false;
        private static DiscordSocketClient _client;
        public static void Main()
        {
            ThreadStart thread1 = new(DiscordClient);
            Thread discord = new(thread1);
            using (StreamReader r = new("config.json"))
            {
                string json = r.ReadToEnd();
                config = JsonConvert.DeserializeObject<JSONConfig>(json);
            }
            discord.Start();
            Console.In.ReadLineAsync().GetAwaiter().GetResult();
        }
        public static async void DiscordClient()
        {
            DiscordSocketConfig config = new()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };
            _client = new(config);

            var token = "MTIyNTY0NDE2MjE5NjcwMTI0NQ.GVW4Eb.kcsV6wbb6ZK2pqQMkonDhw8ec3L7Tk6tMqb8WE";

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            _client.Ready += Ready;
            _client.MessageReceived += MessageReceived;

            await Task.Delay(-1);
        }
        public static async Task Ready()
        {

            var onlineCommand = new SlashCommandBuilder()
                .WithName("online")
                .WithDescription("Ver jugadores online");
            var settingsCommand = new SlashCommandBuilder()
                .WithName("settings")
                .WithDescription("Configuracion")
                .AddOption(new SlashCommandOptionBuilder()
                .WithName("status")
                .WithDescription("Canal de estado de servidor")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("channel", ApplicationCommandOptionType.Channel, "Mencion de canal", false)
                .AddOption("override", ApplicationCommandOptionType.Boolean, "Forzar el canal para ser este", false))
                .AddOption(new SlashCommandOptionBuilder()
                .WithName("chat")
                .WithDescription("Canal de chat de servidor para interactuar")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("channel", ApplicationCommandOptionType.Channel, "Mencion de canal", false)
                .AddOption("override", ApplicationCommandOptionType.Boolean, "Forzar el canal para ser este", false))
                .AddOption(new SlashCommandOptionBuilder()
                .WithName("get")
                .WithDescription("Muestra las configuraciones")
                .WithType(ApplicationCommandOptionType.SubCommand));
            var rconCommand = new SlashCommandBuilder()
                .WithName("rcon")
                .WithDescription("Ejecutar comandos RCON en el servidor")
                .AddOption(new SlashCommandOptionBuilder()
                .WithName("text")
                .WithDescription("Comandos a ejecutar como si fuera la consola de servidor")
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.String));
            try
            {
                await _client.Rest.CreateGlobalCommand(onlineCommand.Build());
                await _client.Rest.CreateGlobalCommand(settingsCommand.Build());
                await _client.Rest.CreateGlobalCommand(rconCommand.Build());

            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                Console.WriteLine(json);
            }
            _client.SlashCommandExecuted += SlashCommandHandler;
            Console.WriteLine("bot conectado");
            statuschannel = _client.GetChannel(config.statusChannel) as IMessageChannel;
            chatchannel = _client.GetChannel(config.chatChannel) as IMessageChannel;
            ThreadStart thread2 = new(MinestatCLient);
            Thread minestat = new(thread2);
            minestat.Start();
            ThreadStart thread3 = new(RCONClient);
            Thread rcon = new(thread3);
            rcon.Start();
        }
        public static async Task MessageReceived(SocketMessage message)
        {

            if (message.Channel.Id != config.chatChannel) { return; }
            if (message.Author.IsBot && message.Author.Id != 725129316790173767) { return; }

            string msg = message.Content;

            if (message.Author.Id == 725129316790173767 && message.Embeds.Count != 0) {
                if (message.Embeds.First().Fields.First().Name == "Escuchando") {
                msg = message.Embeds.First().Fields.First().Value;
                msg = msg[..msg.IndexOf("](http")];
                msg = "Escuchando: " + msg;
                }
            }

            bool authenticated;
            try
            {
                await client.ConnectAsync();

                authenticated = await client.AuthenticateAsync("123");
            }
            catch
            {
                return;
            }
            if (authenticated)
            {
                await client.ExecuteCommandAsync($"say [{message.Author.Username}]: {msg}");
            }
        }
        private static async Task SlashCommandHandler(SocketSlashCommand command)
        {
            // Let's add a switch statement for the command name so we can handle multiple commands in one event.
            switch (command.Data.Name)
            {
                case "online":
                    await HandleOnlineCommand(command);
                    break;
                case "settings":
                    await HandleSettingsCommand(command);
                    break;
                case "rcon":
                    await HandleRCONCommand(command);
                    break;
            }
            return;
        }

        private static async Task HandleOnlineCommand(SocketSlashCommand command)
        {

            bool authenticated;
            try
            {
                await client.ConnectAsync();

                authenticated = await client.AuthenticateAsync("123");
            }
            catch
            {
                await command.RespondAsync("El servidor parece apagado.");
                return;
            }
            if (authenticated)
            {
                var status = await client.ExecuteCommandAsync("craftcontrolrcon players");

                PlayerData data = JsonConvert.DeserializeObject<PlayerData>(status.ToString());

                if (data.current == 0) { await command.RespondAsync("No hay jugadores en linea."); return; }
                string description = "";
                foreach (Players players in data.players)
                {
                    description += $"Nombre: {players.name}\n\tModo: {gamemode[players.gameMode]}\n\tCorazones: {(int)players.health}\n\tNivel: {players.level}\n\n";
                }
                var embedBuile = new EmbedBuilder()
                    .WithTitle("Jugadores online")
                    .WithDescription(description)
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();

                await command.RespondAsync(embed: embedBuile.Build());
            }
            return;
        }
        private static async Task HandleSettingsCommand(SocketSlashCommand command)
        {
            if (command.Data.Options.First().Name == "get")
            {
                var msgBuiler = new EmbedBuilder()
                .WithTitle("Configuracion")
                .WithDescription($"IP: `{config.serverip}:{config.serverport}`\nPuerto RCON: {config.rconport}\nEstado de Servidor:<#{config.statusChannel}>\nChat de Servidor:`<#{config.chatChannel}>")
                .WithColor(Color.Gold)
                .WithCurrentTimestamp();
                await command.RespondAsync(embed: msgBuiler.Build());
                return;
            }
            IMessageChannel channel = command.Data.Options.First().Options.First().Value as IMessageChannel;
            bool? ovd = false;
            if (command.Data.Options.First().Options.ToList().Count > 1)
            {
                ovd = (bool?)command.Data.Options.First().Options.ToList()[1].Value;
            }
            string output;
            if (channel.GetChannelType() != 0) { await command.RespondAsync("Ese no es un canal de texto valido"); return; }

            switch (command.Data.Options.First().Name)
            {
                case "status":
                    if (channel.Id == config.chatChannel && ovd != true) { await command.RespondAsync("No puedes utilizar el mismo canal de chat para el estado, usa `override` para forzarlo"); return; }
                    config.statusChannel = channel.Id;
                    await command.RespondAsync("Se ha configurado el nuevo canal de estado");
                    output = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText("config.json", output);
                    statuschannel = _client.GetChannel(config.statusChannel) as IMessageChannel;

                    break;
                case "chat":
                    if (channel.Id == config.statusChannel && ovd != true) { await command.RespondAsync("No puedes utilizar el mismo canal de estado para el chat, usa `override` para forzarlo"); return; }
                    config.chatChannel = channel.Id;
                    await command.RespondAsync("Se ha configurado el nuevo canal de chat");
                    output = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText("config.json", output);
                    chatchannel = _client.GetChannel(config.chatChannel) as IMessageChannel;
                    break;
            }
        }
        private static async Task HandleRCONCommand(SocketSlashCommand command)
        {
            if (command.User.Id != 118467098908295172)
            {
                await command.RespondAsync("No tienes permisos para ejecutar este comando"); return;
            }

            bool authenticated;
            try
            {
                await client.ConnectAsync();

                authenticated = await client.AuthenticateAsync("123");
            }
            catch
            {
                await command.RespondAsync("El servidor parece apagado.");
                return;
            }
            if (authenticated)
            {
                var status = await client.ExecuteCommandAsync((string)command.Data.Options.First().Value);
                if (string.IsNullOrEmpty(status.ToString())) { await command.RespondAsync("Se ejecuto el comando"); return; }

                await command.RespondAsync(status.ToString());
            }
        }
        public static void MinestatCLient()
        {
            bool serverUp = false;
            int retries = 0;
            string motd = "";
            string address = "";
            int lastState = -1;

            var embedBuiler = new EmbedBuilder();
            while (true)
            {
                Thread.Sleep(3000);
                RconClient client = RconClient.Create(config.rconip, config.rconport);

                MineStat ms = new(config.serverip, 57901);
                if (address != config.serverip + ":" + config.serverport)
                {
                    var portBuilder = new EmbedBuilder()
                    .WithDescription($"Nueva IP es: **{config.serverip + ":" + config.serverport}**")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();

                    statuschannel.SendMessageAsync(embed: portBuilder.Build());
                }

                motd = ms.Stripped_Motd;
                address = config.serverip + ":" + config.serverport;

                if (retries == 3 && lastState != 0)
                {
                    embedBuiler = new EmbedBuilder()
                    .WithTitle($"Minecraft server {config.serverip + ":" + config.serverport}")
                    .WithDescription($"Servidor Apagado")
                    .WithColor(Color.Red)
                    .WithFooter(text: config.serverip + ":" + config.serverport)
                    .WithCurrentTimestamp();
                    retries = 0;
                    statuschannel.SendMessageAsync(embed: embedBuiler.Build());
                    serverUp = ms.ServerUp;
                    lastState = 0;
                }
                if (!ms.ServerUp)
                {
                    retries++;
                }

                if (ms.ServerUp && lastState != 1)
                {
                    if (ms.MaximumPlayers == "0") { continue; }
                    embedBuiler = new EmbedBuilder()
                        .WithTitle($"Minecraft server {ms.Stripped_Motd}")
                        .WithDescription($"Server Encendido en version {ms.Version} con {ms.CurrentPlayers} de {ms.MaximumPlayers} jugadores.\n"
                        + $"Latencia: {ms.Latency}ms")
                        .WithColor(Color.Green)
                        .WithFooter(text: config.serverip + ":" + config.serverport)
                        .WithCurrentTimestamp();

                    retries = 0;
                    statuschannel.SendMessageAsync(embed: embedBuiler.Build());
                    serverUp = ms.ServerUp;
                    lastState = 1;

                    if (started == false)
                    {

                    }
                }

            }
        }
        public static async void RCONClient()
        {
            List<Players> iterable = [];
            List<Players> old = [];
            MessageData lastmsg = new()
            {
                timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            };
            client = RconClient.Create(config.rconip, config.rconport);
            while (true)
            {
                Thread.Sleep(200);
                
                bool authenticated;
                try
                {

                    await client.ConnectAsync();

                    authenticated = await client.AuthenticateAsync("123");
                }
                catch
                {
                    client = RconClient.Create(config.rconip, config.rconport);
                    
                    continue;
                }
                if (authenticated)
                {
                    if (started == false)
                    {
                        await client.ExecuteCommandAsync("gamerule logAdminCommands false");
                        await client.ExecuteCommandAsync("op DualFennecFox");
                        await client.ExecuteCommandAsync("op Clover");
                        started = true;
                    }
                    var status = await client.ExecuteCommandAsync("craftcontrolrcon players");

                    

                    PlayerData data = JsonConvert.DeserializeObject<PlayerData>(status.ToString());

                    if (old.Count == 0)
                    {
                        iterable = new List<Players>(data.players);
                    }
                    else
                    {

                        for (int i = 0; i < data.players.Count; i++)
                        {
                            List<Players> matches = old.Where(p => p.uuid == data.players[i].uuid).ToList();
                            if (matches.Count == 0)
                            {
                                iterable.Add(data.players[i]);
                                // {1, 2, 3}
                            }
                            // {2, 3}
                        }
                    }
                    string fullhearth = "<:fullhearth:1225988366743376094>";
                    string halfhearth = "<:halfhearth:1225988400461647884>";
                    foreach (Players players in iterable)
                    {
                        string description = "";
                        for (int i = 0; i < (int)(players.health / 2) + 1; i++)
                        {

                            if (i == (int)(players.health / 2) && (players.health / 2) - (int)(players.health / 2) != 0)
                            {
                                description += halfhearth;
                                break;
                            }
                            if (i == (int)(players.health / 2)) { break; }
                            description += fullhearth;
                        }
                        var playerBuiler = new EmbedBuilder()
                            .WithTitle($"{players.name} Se ha conectado")
                            .AddField("Modo", gamemode[players.gameMode])
                            .AddField("Corazones", description)
                            .AddField("Experiencia", players.level)
                            .WithColor(Color.Blue)
                            .WithCurrentTimestamp();

                        await statuschannel.SendMessageAsync(embed: playerBuiler.Build());
                        iterable = [];
                    }
                    old = new List<Players>(data.players);

                    var mstatus = await client.ExecuteCommandAsync($"craftcontrolrcon chat {lastmsg.timestamp - 10000}");
                    if (mstatus.ToString() == "[]") { continue; }
                    MessageData mdata = JsonConvert.DeserializeObject<List<MessageData>>(mstatus.ToString()).LastOrDefault();

                    Color color = Color.Gold;
                    if (mdata.player == "RCON" && mdata.message.StartsWith('['))
                    {
                        string temp = mdata.message;
                        mdata.message = temp[(temp.IndexOf("]: ") + 3)..];
                        mdata.player = temp[1..temp.IndexOf("]:")];
                        color = Color.Orange;
                    }
                    if (String.IsNullOrEmpty(mdata.message)) { continue; }
                    if (mdata.timestamp == lastmsg.timestamp) { continue; }
                    var msgBuiler = new EmbedBuilder()
                    .WithAuthor(mdata.player)
                    .WithDescription(mdata.message)
                    .WithColor(color)
                    .WithFooter("Chat")
                    .WithCurrentTimestamp();
                    await chatchannel.SendMessageAsync(embed: msgBuiler.Build());
                    lastmsg = mdata;
                    client.Disconnect();
                }
            }
        }
    }
}