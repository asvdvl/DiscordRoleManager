using LiteDB;
using System;
using static ColorLogger.ColorLogger;

namespace DiscordRoleManager
{
    public class Settings
    {
        //on linux systems, if left without "AppDomain.CurrentDomain.BaseDirectory", 
        //the database was created in the user's home directory, which is sometimes not allowed, 
        //so here is a hard link to the directory with the program
        readonly LiteDatabase db = new(AppDomain.CurrentDomain.BaseDirectory + "Settings.db");
        public Settings()
        {
            Log("Try restore data from db");
            EmoteAndRoleRelationsList = db.GetCollection<EmoteAndRoleRelation>("EmoteRelation") as LiteCollection<EmoteAndRoleRelation>;
            StringsForAnswer = db.GetCollection<AnswersStrings>("StringsForAnswer") as LiteCollection<AnswersStrings>;
        }

        public ulong WorkGuildId = 694935732011270205;
        public ulong WorkTextChannelId = 824940342457401354;
        public ulong WorkMessageId = 830157913405259807;

        public LiteCollection<AnswersStrings> StringsForAnswer { get; set; }

        public LiteCollection<EmoteAndRoleRelation> EmoteAndRoleRelationsList { get; set; }

        private static Settings instance;
        public static Settings GetInstance()
        {
            if (instance == null)
                instance = new Settings();
            return instance;
        }
        
        public struct EmoteAndRoleRelation
        {
            public string EmoteName { get; set; }
            public bool IsEmoji { get; set; }
            public string EmbedDescription { get; set; }
            [BsonId]
            public ulong RoleId { get; set; }
        }

        public class AnswersStrings
        {
            public string Answer { get; set; }
            public bool NeedPing { get; set; } = false;
        }
    }
}
