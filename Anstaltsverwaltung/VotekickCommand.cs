using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace Anstaltsverwaltung
{
    public class VotekickCommand : ModuleBase<SocketCommandContext>
    {
        [Command("votekick", RunMode = RunMode.Async)]
        public async Task Votekick(SocketGuildUser target)
        {
            SocketTextChannel channel = (SocketTextChannel)Context.Channel;
            if (BotManager.votePending)
            {
                await channel.SendMessageAsync("Es läuft bereits ein Votekick!");
                return;
            }
            SocketGuildUser requestingUser = Context.Guild.GetUser(Context.User.Id);
            var users = requestingUser.VoiceChannel.ConnectedUsers.ToList();
            BotManager.votingUsers = users;
            if (users.Find(user => user.Id == target.Id) == null)
            {
                await channel.SendMessageAsync("Der User ist nicht in deinem Channel.");
                return;
            }
            if (requestingUser.Equals(target))
            {
                await channel.SendMessageAsync("Du kannst dich nicht selbst votekicken.");
                return;
            }
            BotManager.votePending = true;
            BotManager.votingTarget = target;
            BotManager.voteGoal = requestingUser.VoiceChannel.ConnectedUsers.Count - 1;
            BotManager.voteCount = 1;
            BotManager.votingUsers.Remove(requestingUser);
            var message = await channel.SendMessageAsync("**Votekick gegen den User **" + target.Mention + "\nReagiere auf diese Nachricht um für den Kick zu stimmen.");
            BotManager.voteMessageId = message.Id;
            var counterMessage = await channel.SendMessageAsync($"**{BotManager.voteCount} / {BotManager.voteGoal}** :question:");
            BotManager.counterMessageId = counterMessage.Id;

            await Task.Delay(int.Parse(ConfigurationManager.AppSettings.Get("VotekickDuration")));

            if (BotManager.votePending)
            {
                BotManager.votePending = false;
                string responseMessage = "Der Votekick ist abgelaufen.";
                if (BotManager.privilegedUserPresent && BotManager.voteCount == BotManager.voteGoal / 2)
                {
                    int privilegedNoVotes = (from priv in BotManager.privilegedUsers join u in BotManager.votingUsers on priv.Id equals u.Id select priv).Count();
                    int privilegedYesVotes = BotManager.privilegedUsers.Count - privilegedNoVotes;
                    BotManager.voteCount += privilegedYesVotes;
                    await BotManager.EvaluateVotekickResult(channel);
                }
                await channel.SendMessageAsync("Der Votekick ist abgelaufen.");
                var msg = await channel.GetMessageAsync(counterMessage.Id);
                await BotManager.UpdateVotingCounterMessage((RestUserMessage)msg, 2);
            }
        }
    }
}
