using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Anstaltsverwaltung
{
    public class SoundboardCommand : ModuleBase<SocketCommandContext>
    {
        [Command("soundboard", RunMode = RunMode.Async), Alias("sb")]
        public async Task Soundboard()
        {
            ComponentBuilder buttons = new ComponentBuilder();
            foreach (var sound in Directory.GetFiles("./sounds/"))
            {
                buttons.WithButton(label: Path.GetFileNameWithoutExtension(sound), customId: Path.GetFileName(sound));
            }
            await ReplyAsync("", components: buttons.Build());
        }
    }
}
