using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Configuration;
using Discord.Rest;
using System.Threading.Channels;
using Discord.Audio;
using System.Diagnostics;

namespace Anstaltsverwaltung
{
    public class BotManager
    {
        public static DiscordSocketClient BotClient;
        public static CommandService Commands;
        public static IServiceProvider ServiceProvider;
        public const string PREFIX = "!";
        public const string TOKEN = "MTE5MTc0NzQ4NDAzNzQxNDk0Mg.G5Fl0X.eKUBZDvRO4RcB5nRnu9dd0Afj-XNoZJA6P0A3c";

        public static bool votePending = false;
        public static int voteCount = 0;
        public static int voteGoal = 0;
        public static ulong voteMessageId = 0;
        public static ulong counterMessageId = 0;
        public static List<SocketGuildUser> votingUsers = new List<SocketGuildUser>();
        public static SocketGuildUser? votingTarget;
        public static bool privilegedUserPresent = false;
        public static List<SocketGuildUser>? privilegedUsers;

        public async Task RunBot()
        {
            Commands = new CommandService();
            ServiceProvider = ConfigureServices();
            BotClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All
            });
            await BotClient.LoginAsync(TokenType.Bot, TOKEN);

            await BotClient.StartAsync();
            BotClient.Log += BotHatWasGelogged;
            BotClient.Ready += BotIstBereit;

            await Task.Delay(-1);
        }
        public Task BotHatWasGelogged(LogMessage message)
        {
            Console.WriteLine(message);
            return Task.CompletedTask;
        }
        public async Task BotIstBereit()
        {
            Console.WriteLine("Bot ist bereit.");
            await Commands.AddModulesAsync(Assembly.GetExecutingAssembly(), ServiceProvider);
            await BotClient.SetGameAsync("mit dem Finanzamt");
            BotClient.MessageReceived += BotClient_MessageReceived;
            BotClient.UserJoined += BotClient_UserJoined;
            BotClient.ReactionAdded += BotClient_ReactionAdded;
            BotClient.ButtonExecuted += BotClient_ButtonExecuted;
        }

        private Task BotClient_ButtonExecuted(SocketMessageComponent arg)
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
        private async Task BotClient_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction reaction)
        {
            var channel = await arg2.GetOrDownloadAsync();
            var message = await arg1.GetOrDownloadAsync();

            var user = votingUsers.Find(user => user.Id == reaction.UserId);

            privilegedUsers = votingUsers.FindAll(user => user.Roles.ToList().Find(role => role.Id == ulong.Parse(ConfigurationManager.AppSettings.Get("PrivilegedRoleId"))) != null);
            privilegedUserPresent = privilegedUsers != null;

            if (!votePending) return;
            if (message.Id != voteMessageId) return;
            if (user == null) return;
            voteCount++;
            votingUsers.Remove(user);
            await EvaluateVotekickResult(channel);
        }
        public static async Task EvaluateVotekickResult(IMessageChannel channel)
        {
            int state = 0;
            if (voteCount >= (int)Math.Round(voteGoal * 0.667))
            {
                state = 1;
                await votingTarget.SetTimeOutAsync(TimeSpan.FromMilliseconds(int.Parse(ConfigurationManager.AppSettings.Get("VotekickTimeoutDuration"))));
                votePending = false;
                await channel.SendMessageAsync(votingTarget.Mention + " wurde **raus geschmissen**");
            }
            await UpdateVotingCounterMessage((RestUserMessage)await channel.GetMessageAsync(counterMessageId), state);
        }
        public static async Task UpdateVotingCounterMessage(RestUserMessage message, int state)
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

        private async Task BotClient_UserJoined(SocketGuildUser arg)
        {
            ulong roleId = ulong.Parse(ConfigurationManager.AppSettings.Get("DefaultRoleId"));
            ulong textChannelId = ulong.Parse(ConfigurationManager.AppSettings.Get("WelcomeChannelId"));
            await arg.AddRoleAsync(roleId);
            await arg.Guild.GetTextChannel(textChannelId).SendMessageAsync("Insasse " + arg.Mention + " wurde aufgenommen. Herzlich Willkommen im Irrenhaus. :tada:");
        }

        private async Task BotClient_MessageReceived(SocketMessage arg)
        {
            if (arg == null) return;
            SocketUserMessage msg = arg as SocketUserMessage;
            if (msg == null) return;
            int commandPosition = 0;
            if (msg.HasStringPrefix(PREFIX, ref commandPosition))
            {
                SocketCommandContext context = new SocketCommandContext(BotClient, msg);
                IResult result = await Commands.ExecuteAsync(context, commandPosition, ServiceProvider);
                if (!result.IsSuccess)
                {
                    Console.WriteLine(result.ErrorReason);
                }
            }
        }
        
        public IServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<HelpCommand>()
                .AddSingleton<PeanutsCommand>()
                .AddSingleton<SetCommand>()
                .AddSingleton<VotekickCommand>()
                .AddSingleton<SoundboardCommand>()
                .BuildServiceProvider();
        }
    }
}
