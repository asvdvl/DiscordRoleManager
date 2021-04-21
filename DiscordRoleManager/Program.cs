using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordRoleManager
{
    class Program
    {
        //Maybe in the future, this will be stored normally (e.g. in db), but right now it's constants, sorry.
        const ulong WorkGuildId = 694935732011270205;
        const ulong WorkTextChannelId = 824940342457401354;
        ulong WorkMessageId = 829428255262113883;

        readonly List<string> stringsForAnswer = new List<string> {
            "атыбись!", "че надо, я занят!!1!", ", извините, но я сегодня слишком занят для бесполезного общения", "",
            "пожалуйся, не отвлекайте меня", "вы что-то сказали, я не расслышал?", "ты опять выходишь на связь, мудило?",
            "418", "бот сломан, по всем вопросам писать @Deleteduser"};
        readonly List<EmoteAndRoleRelation> emoteAndRoleRelationsList = new List<EmoteAndRoleRelation>
        {
            new EmoteAndRoleRelation { EmoteName = "kot666", RoleId = 829279908743872553, EmbedDescription = "Дает доступ к эксперементальному разделу." },     //rofl member
            new EmoteAndRoleRelation { EmoteName = "Osu", RoleId = 829808235082285106, EmbedDescription = "Дает доступ к осу чату." },        //osu-reader
            new EmoteAndRoleRelation { EmoteName = "Factorio", RoleId = 829808241516085249, EmbedDescription = "Дает доступ к факторио чату." },   //factorio-reader
            new EmoteAndRoleRelation { EmoteName = "kubi", RoleId = 829808245873967114, EmbedDescription = "Дает доступ к майнкрафт чату." },       //minecraft-reader
        };

        //runtime vars
        readonly Random rnd = new Random();
        SocketTextChannel workTextChannel;
        RestUserMessage workTextMessage;

        private DiscordSocketClient _client;

        public static void Main()
        => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _client.Ready += OnClientReady;

            Console.WriteLine("Start bot...");
            var token = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "token.key");
            Console.WriteLine("Token was readed");
            Console.WriteLine("Login...");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            Console.WriteLine("Login succesful.");
#if DEBUG
            Console.WriteLine("Debug build detected");
            await _client.SetStatusAsync(UserStatus.DoNotDisturb);
            await _client.SetGameAsync("Debug mode. start time: " + DateTime.Now, null, ActivityType.Playing);
#endif
            Console.WriteLine("Start complete.");
            await Task.Delay(-1);
        }

        private async Task OnClientReady()
        {
            Console.WriteLine("OnClientReady");

            var fields = new List<EmbedFieldBuilder>();
            foreach (var relation in emoteAndRoleRelationsList)
            {
                var role = _client.Guilds.SelectMany(guild => guild.Roles).First(roles => roles.Id == relation.RoleId);
                var field = new EmbedFieldBuilder
                {
                    Name = relation.isEmoji ? relation.EmoteName : role.Guild.Emotes.First(emote => emote.Name == relation.EmoteName).ToString(),
                    Value = relation.EmbedDescription
                };
                fields.Add(field);
            }

            var em = new EmbedBuilder
            {
                Title = "Драсть! Этот бот управляет ролями!",
                Description = "Для приминения роли добавьте соответствующую реакцию, для отмены, соответственно, удалите.",
                Fields = fields,
                Footer = new EmbedFooterBuilder { Text = "Discord role manager" }
            };

            workTextChannel = _client.GetGuild(WorkGuildId).GetTextChannel(WorkTextChannelId);

            workTextMessage = (RestUserMessage) await workTextChannel.GetMessageAsync(WorkMessageId, new RequestOptions { Timeout = 5000});
            if (workTextMessage == null)
            {
                workTextMessage = await workTextChannel.SendMessageAsync(null, false, em.Build());
                WorkMessageId = workTextMessage.Id;
                Console.WriteLine($"rewrite workMessageId, new value is {WorkMessageId}");
            }
            else
            {
                await workTextMessage.ModifyAsync(x => x.Embed = em.Build());          
                Console.WriteLine("Work message is found");
            }

            List<IEmote> reactionsToAdd = new List<IEmote>();

            foreach (var relation in emoteAndRoleRelationsList)
            {
                if (relation.isEmoji)
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
            if (!(messageParam is SocketUserMessage message) || message.Author.Id == _client.CurrentUser.Id) return;

            if (message.Content.Contains(_client.CurrentUser.Id.ToString()))
            {
                await message.Channel.SendMessageAsync($"<@{message.Author.Id}> " + stringsForAnswer[rnd.Next(stringsForAnswer.Count)]);
            }
            else if (message.Channel.GetType() == typeof(SocketDMChannel))
            {
                await message.Channel.SendMessageAsync(stringsForAnswer[rnd.Next(stringsForAnswer.Count)]);
            }
            Console.WriteLine($"{message.Author.Username} \"{message.Content}\" message");
        }

        private List<IRole> GetRolesFromRelation(SocketTextChannel currentWorkClannel, SocketReaction reaction)
        {
            var roles = new List<IRole>();
            emoteAndRoleRelationsList.Where(x => x.EmoteName == reaction.Emote.Name)
                .Select(x => x.RoleId).ToList().ForEach(roleId =>
                    roles.Add(currentWorkClannel.Guild.GetRole(roleId)));
            return roles;
        }

        private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> _, ISocketMessageChannel textChannel, SocketReaction reaction)
        {
            Console.WriteLine($"OnReactionRemoved, messageid: {reaction.MessageId} Name: " +
                $"{reaction.Emote.Name} {(reaction.Emote.Name.Length == 1 ? Char.ConvertToUtf32(reaction.Emote.Name, 0) : ' ')} " +
                $"by: {reaction.UserId}");
            if (reaction.MessageId.Equals(workTextMessage.Id) && reaction.UserId != _client.CurrentUser.Id)
            {
                Console.WriteLine($"Reaction removed for work message");
                var currentWorkClannel = textChannel as SocketTextChannel;
                var currentWorkUser = currentWorkClannel.Guild.GetUser(reaction.UserId);

                if (currentWorkUser != null)
                {
                    await currentWorkUser.RemoveRolesAsync(GetRolesFromRelation(currentWorkClannel, reaction));
                }
                else
                {
                    Console.WriteLine("ERROR: currentWorkUser is null!");
                }
            }
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> _, ISocketMessageChannel textChannel, SocketReaction reaction)
        {
            Console.WriteLine($"OnReactionAdded, messageid: {reaction.MessageId} Name: " +
                $"{reaction.Emote.Name} {(reaction.Emote.Name.Length == 1 ? Char.ConvertToUtf32(reaction.Emote.Name, 0) : ' ')} " +
                $"by: {reaction.UserId}");
            if (reaction.MessageId.Equals(workTextMessage.Id) && reaction.UserId != _client.CurrentUser.Id)
            {
                Console.WriteLine($"Reaction added for work message");
                var currentWorkClannel = textChannel as SocketTextChannel;
                var currentWorkUser = currentWorkClannel.Guild.GetUser(reaction.UserId);

                if (currentWorkUser != null)
                {
                    await currentWorkUser.AddRolesAsync(GetRolesFromRelation(currentWorkClannel, reaction));
                }
                else
                {
                    Console.WriteLine("ERROR: currentWorkUser is null!");
                }
            }
        }

        struct EmoteAndRoleRelation
        {
            public string EmoteName;
            public bool isEmoji;
            public string EmbedDescription;
            public ulong RoleId;
        }
    }
}
