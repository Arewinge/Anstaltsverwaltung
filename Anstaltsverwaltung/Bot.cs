using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Channels;

namespace Anstaltsverwaltung
{
    public class Bot
    {
        private DiscordSocketClient BotClient;
        private CommandService Commands;
        private IServiceProvider ServiceProvider;
        private const string TOKEN = "MTE5MTc0NzQ4NDAzNzQxNDk0Mg.G5Fl0X.eKUBZDvRO4RcB5nRnu9dd0Afj-XNoZJA6P0A3c";

        private bool votePending = false;
        private int voteCount = 0;
        private int voteGoal = 0;
        private ulong voteMessageId = 0;
        private ulong counterMessageId = 0;
        private List<SocketGuildUser> votingUsers = new List<SocketGuildUser>();
        private SocketGuildUser? votingTarget;
        private bool privilegedUserPresent = false;
        private List<SocketGuildUser>? privilegedUsers;

        public Bot()
        {
            Commands = new CommandService();
            ServiceProvider = ConfigureServices();
            BotClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All,
                AlwaysDownloadUsers = true
            });
        }
        private IServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .BuildServiceProvider();
        }
        public async Task RunBot()
        {
            await BotClient.LoginAsync(TokenType.Bot, TOKEN);
            await BotClient.StartAsync();
            BotClient.Log += BotClient_Log;
            BotClient.Ready += BotClient_Ready;

            Timer theoTimer = new Timer(async (_) =>
            {
                string ip = "brrr";//await new HttpClient().GetStringAsync("http://icanhazip.com");
                IUser theo = await BotClient.GetUserAsync(256643588132306944);
                await theo.SendMessageAsync(ip);
            }, null, 0, 2*60*60*1000);

            await Task.Delay(-1);
        }

        #region Helper Functions
        private async Task SendSound(IAudioClient client, string sound)
        {
            using (var ffmpeg = CreateStream("./sounds/" + sound))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = client.CreatePCMStream(AudioApplication.Mixed))
            {

                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
        }
        private Process CreateStream(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }
        private async Task EvaluateVotekickResult(IMessageChannel channel)
        {
            int state = 0;
            if (voteCount >= (int)Math.Round(voteGoal * 0.667))
            {
                state = 1;
                votePending = false;
                await votingTarget.SetTimeOutAsync(TimeSpan.FromMilliseconds(int.Parse(ConfigurationManager.AppSettings.Get("VotekickTimeoutDuration"))));
                await channel.SendMessageAsync(votingTarget.Mention + " wurde der Anstalt **verwiesen!**");
            }
            await UpdateVotingCounterMessage((RestUserMessage)await channel.GetMessageAsync(counterMessageId), state);
        }
        private async Task UpdateVotingCounterMessage(RestUserMessage message, int state)
        {
            string content = $"**{voteCount} / {voteGoal}** ";
            switch (state)
            {
                case 0:
                    content += ":question:";
                    break;
                case 1:
                    content += ":white_check_mark:";
                    break;
                case 2:
                    content += ":x:";
                    break;
            }
            await message.ModifyAsync(msg => msg.Content = content);
        }
        private async void callbackFunc(object? channel)
        {
            var theChannel = (SocketTextChannel)channel;
            if (votePending)
            {
                votePending = false;
                if (privilegedUserPresent && voteCount == voteGoal / 2)
                {
                    int privilegedNoVotes = (from priv in privilegedUsers join u in votingUsers on priv.Id equals u.Id select priv).Count();
                    int privilegedYesVotes = privilegedUsers.Count - privilegedNoVotes;
                    voteCount += privilegedYesVotes;
                    await EvaluateVotekickResult(theChannel);
                }
                await theChannel.SendMessageAsync("Der Votekick ist abgelaufen.");
                var msg = await theChannel.GetMessageAsync(counterMessageId);
                await UpdateVotingCounterMessage((RestUserMessage)msg, 2);
            }
        }
        #endregion
        #region Base Events
        private async Task BotClient_Ready()
        {
            Console.WriteLine("Bot ready.");
            await Commands.AddModulesAsync(Assembly.GetExecutingAssembly(), ServiceProvider);
            await BotClient.SetGameAsync("mit dem Finanzamt");
            

            BotClient.UserJoined += BotClient_UserJoined;
            BotClient.ButtonExecuted += BotClient_ButtonExecuted;
            BotClient.SlashCommandExecuted += BotClient_SlashCommandExecuted;

            await RegisterSlashCommands();
        }
        private async Task RegisterSlashCommands()
        {
            List<ApplicationCommandProperties> applicationCommandProperties = new();
            SlashCommandBuilder sbBuilder = new SlashCommandBuilder();
            applicationCommandProperties.Add(sbBuilder.WithName("soundboard").WithDescription("Öffnet das Soundboard Menü.").Build());
            SlashCommandBuilder upBuilder = new SlashCommandBuilder();
            applicationCommandProperties.Add(upBuilder.WithName("upload").WithDescription("Lädt einen Soundboard Sound hoch.").AddOption("sound", ApplicationCommandOptionType.Attachment, "Der Sound als mp3 Datei", isRequired: true).Build());
            SlashCommandBuilder pBuilder = new SlashCommandBuilder();
            applicationCommandProperties.Add(pBuilder.WithName("party").WithDescription("PARTYYY.").Build());
            SlashCommandBuilder setBuilder = new SlashCommandBuilder();
            applicationCommandProperties.Add(setBuilder.WithName("set").WithDescription("Weist einem Einstellungsschlüssel einen Wert zu!").AddOption("key", ApplicationCommandOptionType.String, "Der Schlüssel", isRequired: true).AddOption("value", ApplicationCommandOptionType.String, "Der Wert", isRequired: true).Build());
            SlashCommandBuilder helpBuilder = new SlashCommandBuilder();
            applicationCommandProperties.Add(helpBuilder.WithName("help").WithDescription("Zeigt die Help-Nachricht an.").Build());
            SlashCommandBuilder vkBuilder = new SlashCommandBuilder();
            applicationCommandProperties.Add(vkBuilder.WithName("votekick").WithDescription("Startet einen Votekick gegen einen User").AddOption("user", ApplicationCommandOptionType.User, "Der User", isRequired: true).Build());
            SlashCommandBuilder pwBuilder = new SlashCommandBuilder();
            applicationCommandProperties.Add(pwBuilder.WithName("purgewaifu").WithDescription("Löscht alle Mudae Commands und Antworten aus einem Text Channel.").Build());

            await BotClient.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());
        }
        private Task BotClient_Log(LogMessage logMessage)
        {
            Console.WriteLine(logMessage);
            return Task.CompletedTask;
        }
        #endregion
        #region Functional Events
        private async Task BotClient_SlashCommandExecuted(SocketSlashCommand arg)
        {
            switch (arg.Data.Name)
            {
                case "soundboard":
                    await HandleSoundboardCommand(arg);
                    break;
                case "party":
                    await HandlePartyCommand(arg);
                    break;
                case "upload":
                    await HandleUploadCommand(arg);
                    break;
                case "votekick":
                    await HandleVotekickCommand(arg);
                    break;
                case "set":
                    await HandleSetCommand(arg);
                    break;
                case "purgewaifu":
                    await HandlePurgewaifuCommand(arg);
                    break;
            }
        }
        private Task BotClient_ButtonExecuted(SocketMessageComponent arg)
        {
            if (arg.Data.CustomId.Equals("AV-votekick-yes"))
            {
                _ = Task.Run(async () =>
                {
                    var channel = arg.Channel;
                    var message = arg.Message;

                    await arg.DeferAsync();

                    var user = votingUsers.Find(user => user.Id == arg.User.Id);

                    privilegedUsers = votingUsers.FindAll(user => user.Roles.ToList().Find(role => role.Id == ulong.Parse(ConfigurationManager.AppSettings.Get("PrivilegedRoleId"))) != null);
                    privilegedUserPresent = privilegedUsers != null;

                    if (!votePending) return;
                    if (message.Id != voteMessageId) return;
                    if (user == null) return;
                    voteCount++;
                    votingUsers.Remove(user);
                    await EvaluateVotekickResult(channel);
                });
                return Task.CompletedTask;
            } else if (arg.Data.CustomId.Equals("AV-votekick-no"))
            {
                arg.DeferAsync();
                return Task.CompletedTask;
            } else
            {
                _ = Task.Run(async () =>
                {
                    if (arg.GuildId == null) return;
                    var channel = BotClient.GetGuild(arg.GuildId.Value).GetUser(arg.User.Id).VoiceChannel;
                    await arg.DeferAsync();
                    var client = await channel.ConnectAsync();
                    await SendSound(client, arg.Data.CustomId);
                    await channel.DisconnectAsync();
                });
                return Task.CompletedTask;
            }
        }

        private async Task BotClient_UserJoined(SocketGuildUser arg)
        {
            ulong roleId = ulong.Parse(ConfigurationManager.AppSettings.Get("DefaultRoleId"));
            ulong textChannelId = ulong.Parse(ConfigurationManager.AppSettings.Get("WelcomeChannelId"));
            await arg.AddRoleAsync(roleId);
            await arg.Guild.GetTextChannel(textChannelId).SendMessageAsync("Insasse " + arg.Mention + " wurde aufgenommen. Herzlich Willkommen im Irrenhaus. :tada:");
        }
        #endregion
        #region Command Handler
        public async Task HandleSoundboardCommand(SocketSlashCommand arg)
        {
            ComponentBuilder buttons = new ComponentBuilder();
            foreach (var sound in Directory.GetFiles("./sounds/"))
            {
                buttons.WithButton(label: Path.GetFileNameWithoutExtension(sound), customId: Path.GetFileName(sound));
            }
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithTitle("Soundboard").WithColor(Color.Teal);
            await arg.RespondAsync(components: buttons.Build(), embed: embedBuilder.Build());
        }
        public async Task HandlePartyCommand(SocketSlashCommand arg)
        {
            await arg.RespondWithFileAsync("./files/canAsharkDance.gif");
        }
        public async Task HandleUploadCommand(SocketSlashCommand arg)
        {
            try
            {
                var attachment = (IAttachment)arg.Data.Options.First().Value;
                var url = attachment.Url;
                if (!attachment.Filename.EndsWith(".mp3")) return;
                using (HttpClient httpClient = new HttpClient())
                {
                    using (Stream stream = await httpClient.GetStreamAsync(url))
                    {
                        using (FileStream fs = new FileStream("./sounds/" + attachment.Filename, FileMode.OpenOrCreate))
                        {
                            await stream.CopyToAsync(fs);
                        }
                    }
                }
                await arg.RespondAsync("Der Sound **" + attachment.Filename + "** wurde hochgeladen.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await arg.RespondAsync("Ein Fehler ist aufgetreten.");
            }
        }
        public async Task HandleVotekickCommand(SocketSlashCommand arg)
        {
            SocketTextChannel channel = (SocketTextChannel)arg.Channel;
            SocketGuildUser target = (SocketGuildUser)arg.Data.Options.First().Value;
            if (votePending)
            {
                await arg.RespondAsync("Es läuft bereits ein Votekick!");
                return;
            }
            SocketGuildUser requestingUser = (SocketGuildUser)arg.User;
            var users = requestingUser.VoiceChannel.ConnectedUsers.ToList();
            votingUsers = users;
            if (users.Find(user => user.Id == target.Id) == null)
            {
                await arg.RespondAsync("Der User ist nicht in deinem Channel.");
                return;
            }
            if (requestingUser.Equals(target))
            {
                await arg.RespondAsync("Du kannst dich nicht selbst votekicken.");
                return;
            }
            votePending = true;
            votingTarget = target;
            voteGoal = requestingUser.VoiceChannel.ConnectedUsers.Count - 1;
            voteCount = 1;
            votingUsers.Remove(requestingUser);
            await arg.RespondAsync("**Votekick gegen den User **" + target.Mention + "\nStimme ab.", components: new ComponentBuilder().WithButton("Kick", "AV-votekick-yes", ButtonStyle.Danger, Emoji.Parse(":heavy_multiplication_x:")).WithButton("Kein Kick", "AV-votekick-no", ButtonStyle.Secondary, Emoji.Parse(":white_check_mark:")).Build());
            var message = await arg.GetOriginalResponseAsync();
            voteMessageId = message.Id;
            var counterMessage = await channel.SendMessageAsync($"**{voteCount} / {voteGoal}** :question:");
            counterMessageId = counterMessage.Id;

            Timer timer = new Timer(callbackFunc, channel, int.Parse(ConfigurationManager.AppSettings.Get("VotekickDuration")), Timeout.Infinite);

        }
        public async Task HandleSetCommand(SocketSlashCommand arg)
        {
            string key = arg.Data.Options.First().Value.ToString() ?? "";
            string value = arg.Data.Options.Last().Value.ToString() ?? "";
            if (key == "" || value == "")
            {
                string output = "**Verfügbare Keys:**\n";
                foreach (string s in ConfigurationManager.AppSettings.AllKeys)
                {
                    output += s + " => " + ConfigurationManager.AppSettings.Get(s) + "\n";
                }
                await arg.RespondAsync(output);
            }
            else
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings.Remove(key);
                config.AppSettings.Settings.Add(key, value);
                config.Save();
                ConfigurationManager.RefreshSection("appSettings");
                await arg.RespondAsync("Einstellungsschlüssel **" + key + "** wurde auf den Wert **" + value + "** gesetzt.");
            }
        }
        public async Task HandleHelpCommand(SocketSlashCommand arg)
        {
            string output = "**Funktionen:**\nSetzt neuen Nutzern automatisch die Rolle "
                + BotClient.GetGuild(arg.GuildId.Value).GetRole(ulong.Parse(ConfigurationManager.AppSettings.Get("DefaultRoleId"))).Name
                + "\n\n"
                + "**Commands:**"
                + "\nhelp: Zeigt diese Nachricht an!"
                + "\nsoundboard: Zeigt das Soundboard Menü."
                + "\nvotekick <User>: Startet einen Votekick gegen einen User in deinem VoiceChannel!"
                + "\nset <Schlüssel> <Wert>: Weist einem Einstellungsschlüssel einen Wert zu!"
                + "\nparty: PARTYYY!"
                + "\n\n||Außerdem passieren wundersame Dinge, wenn man die ID unseres Lieblingspolen kennt :thinking:||";
            await arg.RespondAsync(output);
        }
        public async Task HandlePurgewaifuCommand(SocketSlashCommand arg)
        {
            await arg.DeferAsync();
            var channel = arg.Channel;
            await foreach (var msg in channel.GetMessagesAsync().Flatten())
            {
                if (msg.Content.StartsWith("$") || msg.Author.Id == 432610292342587392) // Checks if the msg is a mudae command or answer
                {
                    await msg.DeleteAsync();
                }
            }
        }
        #endregion
    }
}
