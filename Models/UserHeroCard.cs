using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JuiceChatBot.Models
{
    public class UserHeroCard : Microsoft.Bot.Connector.HeroCard
    {
        public UserHeroCard() : base() { }

        public UserHeroCard(String a, String b, String c, IList<CardImage> d, IList<CardAction> e, CardAction f) : base(a, b, c, d, e, f) { }

        [Newtonsoft.Json.JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "latitude")]
        public string Latitude { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "longitude")]
        public string Longitude { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "card_division")]
        public string Card_division { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "card_value")]
        public string Card_value { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "card_cnt")]
        public int Card_cnt { get; set; }

    }
}