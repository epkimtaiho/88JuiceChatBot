namespace JuiceChatBot.Models
{
	using Microsoft.Bot.Connector;
	using System;
	using System.Collections.Generic;

	public class LinkHeroCard : Microsoft.Bot.Connector.HeroCard
	{
		public LinkHeroCard() : base() { }

		public LinkHeroCard(String a, String b, String c, IList<CardImage> d, IList<CardAction> e, CardAction f) : base(a, b, c, d, e, f) { }

		[Newtonsoft.Json.JsonProperty(PropertyName = "link")]
		public string Link { get; set; }
	}
}