using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
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
            "418"};
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
            _client.MessageReceived += OnMessageReceived;
            

            var token = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "token.key");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

#if DEBUG
            await _client.SetStatusAsync(UserStatus.DoNotDisturb);
            await _client.SetGameAsync("Debug mode " + DateTime.Now, null, ActivityType.Playing);
#endif

            await Task.Delay(-1);
        }

        private async Task OnMessageReceived(SocketMessage messageParam)
        {
            SocketUserMessage message = messageParam as SocketUserMessage;

            if (message == null || message.Author.Id == _client.CurrentUser.Id) return;

            if (message.Content.Contains(_client.CurrentUser.Id.ToString()))
            {
                await message.Channel.SendMessageAsync($"<@{message.Author.Id}> " + stringsForAnswer[rnd.Next(stringsForAnswer.Count)]);
            }
        }

        private async Task OnClientReady()
        {
            var em = new EmbedBuilder
            {
                Title = "Hello everyone! <:kot666:758691414090973226>",
                Fields = new List<EmbedFieldBuilder> { 
                    new EmbedFieldBuilder { Name = "asd", Value = "dsa" },
                    new EmbedFieldBuilder { Name = "<:kot666:758691414090973226>", Value = "dsa1" },
                    new EmbedFieldBuilder { Name = "asd2", Value = "<:kot666:758691414090973226>" },
                    new EmbedFieldBuilder { Name = "asd3", Value = "dsa3" },
                },
                Footer = new EmbedFooterBuilder { Text = "footer" }
            };

            workTextChannel = _client.GetGuild(WorkGuildId).GetTextChannel(WorkTextChannelId);

            var gettingMessageTask = workTextChannel.GetMessageAsync(WorkMessageId);
            gettingMessageTask.Wait();
            workTextMessage = gettingMessageTask.Result as RestUserMessage;

            if (workTextMessage == null)
            {
                var sendingMessageTask = workTextChannel.SendMessageAsync(null, false, em.Build());
                sendingMessageTask.Wait();

                workTextMessage = sendingMessageTask.Result;
                WorkMessageId = workTextMessage.Id;
                Console.WriteLine($"rewrite workMessageId, new value is {WorkMessageId}");
            }
            else
            {
                await workTextMessage.ModifyAsync(x => x.Embed = em.Build());
            }
        }
    }
}
