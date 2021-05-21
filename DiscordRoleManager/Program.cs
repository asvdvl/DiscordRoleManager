using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ColorLogger.ColorLogger;

namespace DiscordRoleManager
{
    class Program
    {
        readonly Settings st = Settings.GetInstance();
        //runtime vars
        readonly Random rnd = new();
        SocketTextChannel workTextChannel;
        RestUserMessage workTextMessage;

        private DiscordSocketClient _client;

        public static void Main()
        => new Program().MainAsync().GetAwaiter().GetResult();
        
        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _client.Ready += OnClientReady;
            
            Log("Start bot...");
            var token = "";
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "token.key"))
            {
                token = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "token.key");
            }
            else
            {
                Log(LogLevel.Critical, "File with token not found. Exiting...");
                Environment.Exit(1);
            }
            Log("Token was readed");
            Log("Login...");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            Log("Login succesful.");
#if DEBUG
            Log(LogLevel.Debug, "Debug build detected");
            await _client.SetStatusAsync(UserStatus.DoNotDisturb);
            await _client.SetGameAsync("Debug mode. start time: " + DateTime.Now, null, ActivityType.Playing);

            //To debug via ssh
            Log(LogLevel.Debug, "Waiting for debugger to attach");
            for (int i = 0; i < 50 && !Debugger.IsAttached; i++)
            {
                if (!Debugger.IsAttached)
                {
                    Thread.Sleep(100);
                }
            }
            if (Debugger.IsAttached)
            {
                Log(LogLevel.Debug, "Debugger attached");
            }
            else
            {
                Log(LogLevel.Debug, "Debugger connection timeout");
            }
#endif
            Log("Start complete.");
            await Task.Delay(-1);
        }

        private async Task OnClientReady()
        {
            Log(LogLevel.Debug, "OnClientReady");
            //generate fields for embed
            var fields = new List<EmbedFieldBuilder>();
            try
            {
                foreach (var relation in st.EmoteAndRoleRelationsList.Query().ToList())
                {
                    var role = _client.Guilds.SelectMany(guild => guild.Roles).First(roles => roles.Id == relation.RoleId);

                    var Name = (relation.IsEmoji ? relation.EmoteName : role.Guild.Emotes.First(emote => emote.Name == relation.EmoteName).ToString()) + $" ({role.Name})";
                    var Value = relation.EmbedDescription;

                    var field = new EmbedFieldBuilder();
                    if (Name != null && Name.Length > 0)
                    {
                        field.WithName(Name);
                    }
                    if (Value != null && Value.Length > 0)
                    {
                        field.WithValue(Value);
                    }
                    else
                    {
                        field.WithValue("\u200b");
                    }

                    fields.Add(field);
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, ex.Message + "\n" + ex.StackTrace);
                return;
            }

            var em = new EmbedBuilder
            {
                Title = "Драсть! Этот бот управляет ролями!",
                Description = "Для приминения роли добавьте соответствующую реакцию, для отмены, соответственно, удалите.",
                Fields = fields,
                Footer = new EmbedFooterBuilder { Text = "Discord role manager" }
            };

            workTextChannel = _client.GetGuild(st.WorkGuildId).GetTextChannel(st.WorkTextChannelId);

            workTextMessage = (RestUserMessage) await workTextChannel.GetMessageAsync(st.WorkMessageId, new RequestOptions { Timeout = 5000});
            if (workTextMessage == null)
            {
                workTextMessage = await workTextChannel.SendMessageAsync(null, false, em.Build());
                st.WorkMessageId = workTextMessage.Id;
                Log(LogLevel.Warning, $"rewrite workMessageId, new value is {st.WorkMessageId}");
            }
            else
            {
                await workTextMessage.ModifyAsync(x => x.Embed = em.Build());
                Log("Work message is found");
            }

            //Making a list to add to the message
            List<IEmote> reactionsToAdd = new();

            foreach (var relation in st.EmoteAndRoleRelationsList.Query().ToList())
            {
                if (relation.IsEmoji)
                {
                    reactionsToAdd.Add(new Emoji(relation.EmoteName));
                }
                else
                {
                    reactionsToAdd.Add(workTextChannel.Guild.Emotes.First(emote => emote.Name == relation.EmoteName));
                }
            }

            await workTextMessage.AddReactionsAsync(reactionsToAdd.ToArray());

            _client.MessageReceived += OnMessageReceived;
            _client.ReactionAdded += OnReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;
        }

        private async Task OnMessageReceived(SocketMessage messageParam)
        {
            if (messageParam is not SocketUserMessage message || message.Author.Id == _client.CurrentUser.Id) return;

            if (message.Content.Contains(_client.CurrentUser.Id.ToString()))
            {
                var answer = 
                    st.StringsForAnswer.Query().Limit(1).Offset(
                        rnd.Next(st.StringsForAnswer.Count())
                        ).SingleOrDefault();

                var sendingstring = answer.NeedPing? $"<@{message.Author.Id}> " : "";
                sendingstring += answer.Answer;
                await message.Channel.SendMessageAsync(sendingstring);
            }
            else if (message.Channel.GetType() == typeof(SocketDMChannel))
            {
                await message.Channel.SendMessageAsync(
                    st.StringsForAnswer.Query().Limit(1).Offset(
                        rnd.Next(st.StringsForAnswer.Count())
                        ).SingleOrDefault().Answer
                    );
            }
            Log(LogLevel.Debug, $"{message.Author.Username} \"{message.Content}\" message");
        }

        private List<IRole> GetRolesFromRelation(SocketTextChannel currentWorkClannel, SocketReaction reaction)
        {
            var roles = new List<IRole>();
            st.EmoteAndRoleRelationsList.Query().Where(x => x.EmoteName == reaction.Emote.Name)
                .Select(x => x.RoleId).ToList().ForEach(roleId =>
                    roles.Add(currentWorkClannel.Guild.GetRole(roleId)));
            return roles;
        }

        private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> _, ISocketMessageChannel textChannel, SocketReaction reaction)
        {
            Log(LogLevel.Debug, $"OnReactionRemoved, messageid: {reaction.MessageId} Name: " +
                $"{reaction.Emote.Name} {(reaction.Emote.Name.Length == 1 ? Char.ConvertToUtf32(reaction.Emote.Name, 0) : ' ')} " +
                $"by: {reaction.UserId}");
            if (reaction.MessageId.Equals(workTextMessage.Id) && reaction.UserId != _client.CurrentUser.Id)
            {
                Log($"Reaction removed for work message");
                var currentWorkClannel = textChannel as SocketTextChannel;
                var currentWorkUser = currentWorkClannel.Guild.GetUser(reaction.UserId);

                if (currentWorkUser != null)
                {
                    await currentWorkUser.RemoveRolesAsync(GetRolesFromRelation(currentWorkClannel, reaction));
                }
                else
                {
                    Log(LogLevel.Warning, "currentWorkUser is null!");
                }
            }
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> _, ISocketMessageChannel textChannel, SocketReaction reaction)
        {
            Log(LogLevel.Debug, $"OnReactionAdded, messageid: {reaction.MessageId} Name: " +
                $"{reaction.Emote.Name} {(reaction.Emote.Name.Length == 1 ? Char.ConvertToUtf32(reaction.Emote.Name, 0) : ' ')} " +
                $"by: {reaction.UserId}");
            if (reaction.MessageId.Equals(workTextMessage.Id) && reaction.UserId != _client.CurrentUser.Id)
            {
                Log($"Reaction added for work message");
                var currentWorkClannel = textChannel as SocketTextChannel;
                var currentWorkUser = currentWorkClannel.Guild.GetUser(reaction.UserId);

                if (currentWorkUser != null)
                {
                    await currentWorkUser.AddRolesAsync(GetRolesFromRelation(currentWorkClannel, reaction));
                }
                else
                {
                    Log(LogLevel.Warning, "currentWorkUser is null!");
                }
            }
        }

        
    }
}
