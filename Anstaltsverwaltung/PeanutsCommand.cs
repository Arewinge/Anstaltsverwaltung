using Discord.Audio;
using Discord;
using Discord.Commands;
using System.Diagnostics;

namespace Anstaltsverwaltung
{
    public class PeanutsCommand : ModuleBase<SocketCommandContext>
    {
        [Command("peanuts", RunMode = RunMode.Async)]
        public async Task Peanuts(IVoiceChannel channel = null)
        {

            channel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
            var client = await channel.ConnectAsync();
            await SendPeanuts(client);
            await channel.DisconnectAsync();

        }
        [Command("256643588132306944", RunMode=RunMode.Async)]
        public async Task tango(IVoiceChannel channel = null)
        {
            channel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
            var client = await channel.ConnectAsync();
            await SendTango(client);
            await channel.DisconnectAsync();
        }

        private async Task SendPeanuts(IAudioClient client)
        {
            using (var ffmpeg = CreateStream("./peanuts.mp3"))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = client.CreatePCMStream(AudioApplication.Mixed))
            {

                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
        }
        private async Task SendTango(IAudioClient client)
        {
            using (var ffmpeg = CreateStream("./polakentango.mp3"))
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
    }
}
