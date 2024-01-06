using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Anstaltsverwaltung
{
    public class HelpCommand : ModuleBase<SocketCommandContext>
    {
        [Command("help", RunMode = RunMode.Async)]
        public async Task Help()
        {
            string output = "**Funktionen:**\nSetzt neuen Nutzern automatisch die Rolle "
                + Context.Guild.GetRole(ulong.Parse(ConfigurationManager.AppSettings.Get("DefaultRoleId"))).Name
                + "\n\n"
                + "**Prefix:** ! (Ausrufezeichen)\n\n"
                + "**Commands:**"
                + "\nhelp: Zeigt diese Nachricht an!"
                + "\npeanuts: Lässt dich die süße Versuchung von Erdnüssen förmlich hören!"
                + "\nsoundboard | sb: Zeigt das Soundboard Menü."
                + "\nset <Schlüssel> <Wert>: Weist einem Einstellungsschlüssel einen Wert zu!"
                + "\nvotekick <User>: Startet einen Votekick gegen einen User in deinem VoiceChannel!"
                + "\n\n||Außerdem passieren wundersame Dinge, wenn man die ID unseres Lieblingspolen kennt :thinking:||";
            await Context.Channel.SendMessageAsync(output);
        }
    }
}
