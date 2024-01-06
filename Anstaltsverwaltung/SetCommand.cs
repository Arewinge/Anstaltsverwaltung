using Discord.Commands;
using System.Configuration;

namespace Anstaltsverwaltung
{
    public class SetCommand : ModuleBase<SocketCommandContext>
    {
        [Command("set", RunMode = RunMode.Async)]
        public async Task Set(string key = "", string value = "")
        {
            if (key == "" || value == "")
            {
                string output = "**Verfügbare Keys:**\n";
                foreach (string s in ConfigurationManager.AppSettings.AllKeys)
                {
                    output += s + " => " + ConfigurationManager.AppSettings.Get(s) + "\n";
                }
                await Context.Channel.SendMessageAsync(output);
            } else {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings.Remove(key);
                config.AppSettings.Settings.Add(key, value);
                config.Save();
                ConfigurationManager.RefreshSection("appSettings");
                await Context.Channel.SendMessageAsync("Einstellungsschlüssel **" + key + "** wurde auf den Wert **" + value + "** gesetzt.");
            }
        }
    }
}
