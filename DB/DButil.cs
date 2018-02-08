using JuiceChatBot.Models;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace JuiceChatBot.DB
{
    public class DButil
    {
        //DbConnect db = new DbConnect();
        //재시도 횟수 설정
        private static int retryCount = 3;

        public String GetMultiLUIS(string query)
        {
            //루이스 json 선언
            JObject Luis = new JObject();
            string LuisName = "";
            try
            {
                int MAX = MessagesController.LUIS_APP_ID.Count(s => s != null);
                Array.Resize(ref MessagesController.LUIS_APP_ID, MAX);
                Array.Resize(ref MessagesController.LUIS_NM, MAX);

                String[] returnLuisName = new string[MAX];
                JObject[] Luis_before = new JObject[MAX];

                List<string[]> textList = new List<string[]>(MAX);
                String entitiesSum = "";

                for (int i = 0; i < MAX; i++)
                {
                    //textList.Add(LUIS_APP_ID[i] +"|"+ LUIS_SUBSCRIPTION + "|" + query);
                    textList.Add(new string[] { MessagesController.LUIS_NM[i], MessagesController.LUIS_APP_ID[i], MessagesController.LUIS_SUBSCRIPTION, query });

                }

                //병렬처리 시간 체크
                System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                Parallel.For(0, MAX, new ParallelOptions { MaxDegreeOfParallelism = MAX }, async async =>
                {
                    var task_luis = Task<JObject>.Run(() => GetIntentFromBotLUIS(textList[async][1], textList[async][2], textList[async][3]));

                    try
                    {
                        Task.WaitAll(task_luis);

                        Luis_before[async] = task_luis.Result;
                        returnLuisName[async] = textList[async][0];

                    }
                    catch (AggregateException e)
                    {
                    }

                });

                watch.Stop();
                //Luis = Luis_before;

                try
                {
                    for (int i = 0; i < MAX; i++)
                    {
                        //엔티티 합치기
                        if ((int)Luis_before[i]["entities"].Count() > 0)
                        {
                            for (int j = 0; j < (int)Luis_before[i]["entities"].Count(); j++)
                            {
                                entitiesSum += (string)Luis_before[i]["entities"][j]["entity"].ToString() + ",";
                            }
                        }

                    }
                }
                catch (IndexOutOfRangeException e)
                {
                    Debug.WriteLine("error = " + e.Message);
                    return "";
                }


                string luisEntities = "";
                string luisType = "";

                if (!String.IsNullOrEmpty(LuisName))
                {
                    if (Luis != null || Luis.Count > 0)
                    {
                        float luisScore = (float)Luis["intents"][0]["score"];
                        int luisEntityCount = (int)Luis["entities"].Count();

                        if (MessagesController.relationList != null)
                        {
                            if (MessagesController.relationList.Count() > 0)
                            {
                                MessagesController.relationList[0].luisScore = (int)Luis["intents"][0]["score"];
                            }
                            else
                            {
                                MessagesController.cacheList.luisScore = Luis["intents"][0]["score"].ToString();
                            }
                        }



                        if (luisScore > Convert.ToDouble(MessagesController.LUIS_SCORE_LIMIT) && luisEntityCount > 0)
                        {
                            for (int i = 0; i < luisEntityCount; i++)
                            {
                                //luisEntities = luisEntities + Luis["entities"][i]["entity"] + ",";
                                luisType = (string)Luis["entities"][i]["type"];
                                luisType = Regex.Split(luisType, "::")[1];
                                luisEntities = luisEntities + luisType + ",";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(luisEntities) || luisEntities.Length > 0)
                    {
                        luisEntities = luisEntities.Substring(0, luisEntities.LastIndexOf(","));
                        luisEntities = Regex.Replace(luisEntities, " ", "");


                        luisEntities = MessagesController.db.SelectArray(luisEntities);

                        if (Luis["intents"] == null)
                        {
                            MessagesController.cacheList.luisIntent = "";
                        }
                        else
                        {
                            MessagesController.cacheList.luisIntent = (string)Luis["intents"][0]["intent"];
                        }

                        MessagesController.cacheList.luisEntities = luisEntities;
                    }

                    //MessagesController.cacheList.luisEntities = LuisName;

                }
                return LuisName;
            }
            catch (System.Exception e)
            {
                Debug.WriteLine(e.Message);
                return "";
            }
        }

        private static async Task<JObject> GetIntentFromBotLUIS(string luis_app_id, string luis_subscription, string query)
        {
            JObject jsonObj = new JObject();

            query = Uri.EscapeDataString(query);

            //string url = string.Format("https://westus.api.cognitive.microsoft.com/luis/v2.0/apps/{0}?subscription-key={1}&timezoneOffset=0&verbose=true&q={2}", luis_app_id, luis_subscription, query);
            string url = string.Format("https://southeastasia.api.cognitive.microsoft.com/luis/v2.0/apps/{0}?subscription-key={1}&timezoneOffset=0&verbose=true&q={2}", luis_app_id, luis_subscription, query);

            Debug.WriteLine("LUIS URL : " + url);

            using (HttpClient client = new HttpClient())
            {
                //취소 시간 설정
                client.Timeout = TimeSpan.FromMilliseconds(MessagesController.LUIS_TIME_LIMIT); //3초
                var cts = new CancellationTokenSource();
                try
                {
                    HttpResponseMessage msg = await client.GetAsync(url, cts.Token);

                    int currentRetry = 0;

                    Debug.WriteLine("msg.IsSuccessStatusCode1 = " + msg.IsSuccessStatusCode);

                    if (msg.IsSuccessStatusCode)
                    {
                        var JsonDataResponse = await msg.Content.ReadAsStringAsync();
                        jsonObj = JObject.Parse(JsonDataResponse);
                        currentRetry = 0;
                    }
                    else
                    {
                        //통신장애, 구독만료, url 오류                  
                        //오류시 3번retry
                        for (currentRetry = 0; currentRetry < retryCount; currentRetry++)
                        {
                            //테스용 url 설정
                            string url_re = string.Format("https://southeastasia.api.cognitive.microsoft.com/luis/v2.0/apps/{0}?subscription-key={1}&timezoneOffset=0&verbose=true&q={2}", luis_app_id, luis_subscription, query);
                            HttpResponseMessage msg_re = await client.GetAsync(url_re, cts.Token);

                            if (msg_re.IsSuccessStatusCode)
                            {
                                //다시 호출
                                Debug.WriteLine("msg.IsSuccessStatusCode2 = " + msg_re.IsSuccessStatusCode);
                                var JsonDataResponse = await msg_re.Content.ReadAsStringAsync();
                                jsonObj = JObject.Parse(JsonDataResponse);
                                currentRetry = 0;
                                break;
                            }
                            else
                            {
                                //초기화
                                jsonObj = JObject.Parse(@"{
                                    'query':'',
                                    'topScoringIntent':0,
                                    'intents':[],
                                    'entities':'[]'
                                }");
                            }
                        }
                    }

                    msg.Dispose();
                }
                catch (TaskCanceledException e)
                {
                    Debug.WriteLine("error = " + e.Message);
                    //초기화
                    jsonObj = JObject.Parse(@"{
                                    'query':'',
                                    'topScoringIntent':0,
                                    'intents':[],
                                    'entities':'[]'
                                }");

                }
            }
            return jsonObj;
        }

        public static void HistoryLog(String strMsg)
        {
            try
            {
                //Debug.WriteLine("AppDomain.CurrentDomain.BaseDirectory : " + AppDomain.CurrentDomain.BaseDirectory);
                string m_strLogPrefix = AppDomain.CurrentDomain.BaseDirectory + @"LOG\";
                string m_strLogExt = @".LOG";
                DateTime dtNow = DateTime.Now;
                string strDate = dtNow.ToString("yyyy-MM-dd");
                string strPath = String.Format("{0}{1}{2}", m_strLogPrefix, strDate, m_strLogExt);
                string strDir = Path.GetDirectoryName(strPath);
                DirectoryInfo diDir = new DirectoryInfo(strDir);

                if (!diDir.Exists)
                {
                    diDir.Create();
                    diDir = new DirectoryInfo(strDir);
                }

                if (diDir.Exists)
                {
                    System.IO.StreamWriter swStream = File.AppendText(strPath);
                    string strLog = String.Format("{0}: {1}", dtNow.ToString("MM/dd/yyyy hh:mm:ss.fff"), strMsg);
                    swStream.WriteLine(strLog);
                    swStream.Close(); ;
                }
            }
            catch (System.Exception e)
            {
                HistoryLog(e.Message);
            }
        }


        public Attachment getAttachmentFromDialog(DialogList dlg, Activity activity)
        {
            Attachment returnAttachment = new Attachment();
            ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
            if (dlg.dlgType.Equals(MessagesController.TEXTDLG))
            {

                if (!activity.ChannelId.Equals("facebook"))
                {
                    HeroCard plCard = new HeroCard()
                    {
                        Title = dlg.cardTitle,
                        Text = dlg.cardText
                    };
                    returnAttachment = plCard.ToAttachment();
                }

                
            }
            else if (dlg.dlgType.Equals(MessagesController.MEDIADLG))
            {

                string cardDiv = "";
                string cardVal = "";

                List<CardImage> cardImages = new List<CardImage>();
                List<CardAction> cardButtons = new List<CardAction>();

                HistoryLog("CARD IMG START");
                if (dlg.mediaUrl != null)
                {
                    HistoryLog("FB CARD IMG " + dlg.mediaUrl);
                    cardImages.Add(new CardImage(url: dlg.mediaUrl));
                }


                HistoryLog("CARD BTN1 START");
                if (activity.ChannelId.Equals("facebook") && dlg.btn1Type == null && !string.IsNullOrEmpty(dlg.cardDivision) && dlg.cardDivision.Equals("play") && !string.IsNullOrEmpty(dlg.cardValue))
                {
                    CardAction plButton = new CardAction();
                    plButton = new CardAction()
                    {
                        Value = dlg.cardValue,
                        Type = "openUrl",
                        Title = "영상보기"
                    };
                    cardButtons.Add(plButton);
                }
                else if (dlg.btn1Type != null)
                {
                    CardAction plButton = new CardAction();
                    plButton = new CardAction()
                    {
                        Value = dlg.btn1Context,
                        Type = dlg.btn1Type,
                        Title = dlg.btn1Title
                    };
                    cardButtons.Add(plButton);
                }

                if (dlg.btn2Type != null)
                {
                    if (!(activity.ChannelId == "facebook" && dlg.btn2Title == "나에게 맞는 모델 추천"))
                    {
                        CardAction plButton = new CardAction();
                        HistoryLog("CARD BTN2 START");
                        plButton = new CardAction()
                        {
                            Value = dlg.btn2Context,
                            Type = dlg.btn2Type,
                            Title = dlg.btn2Title
                        };
                        cardButtons.Add(plButton);
                    }
                }

                if (dlg.btn3Type != null )
                {
                    
                    CardAction plButton = new CardAction();

                    HistoryLog("CARD BTN3 START");
                    plButton = new CardAction()
                    {
                        Value = dlg.btn3Context,
                        Type = dlg.btn3Type,
                        Title = dlg.btn3Title
                    };
                    cardButtons.Add(plButton);
                    
                }

                if (dlg.btn4Type != null)
                {
                    CardAction plButton = new CardAction();
                    HistoryLog("CARD BTN4 START");
                    plButton = new CardAction()
                    {
                        Value = dlg.btn4Context,
                        Type = dlg.btn4Type,
                        Title = dlg.btn4Title
                    };
                    cardButtons.Add(plButton);
                }

                if (!string.IsNullOrEmpty(dlg.cardDivision))
                {
                    cardDiv = dlg.cardDivision;
                }

                if (!string.IsNullOrEmpty(dlg.cardValue))
                {
                    //cardVal = priceMediaDlgList[i].cardValue.Replace();
                    cardVal = dlg.cardValue;
                }
                HistoryLog("!!!!!FB CARD BTN1 START channelID.Equals(facebook) && cardButtons.Count < 1 && cardImages.Count < 1");
                HeroCard plCard = new UserHeroCard();
                if (activity.ChannelId == "facebook" && string.IsNullOrEmpty(dlg.cardTitle))
                {
                    HistoryLog("FB CARD BTN1 START channelID.Equals(facebook) && string.IsNullOrEmpty(card.cardTitle)");
                    plCard = new UserHeroCard()
                    {
                        Title = "선택해 주세요",
                        Text = dlg.cardText,
                        Images = cardImages,
                        Buttons = cardButtons,
                        Card_division = cardDiv,
                        Card_value = cardVal
                    };
                    returnAttachment = plCard.ToAttachment();
                }
                else if (activity.ChannelId == "facebook" && string.IsNullOrEmpty(dlg.cardValue))
                {
                    HistoryLog("FB CARD BTN1 START channelID.Equals(facebook) && string.IsNullOrEmpty(card.cardValue)");
                    plCard = new UserHeroCard()
                    {
                        Title = dlg.cardTitle,
                        Images = cardImages,
                        Buttons = cardButtons
                    };
                    returnAttachment = plCard.ToAttachment();
                }
                else
                {
                    HistoryLog("!!!!!!!!FB CARD BTN1 START channelID.Equals(facebook) && string.IsNullOrEmpty(card.cardTitle)");
                    plCard = new UserHeroCard()
                    {
                        Title = dlg.cardTitle,
                        Text = dlg.cardText,
                        Images = cardImages,
                        Buttons = cardButtons,
                        Card_division = cardDiv,
                        Card_value = cardVal
                    };
                    returnAttachment = plCard.ToAttachment();
                }
            }
            else
            {
                Debug.WriteLine("Dialog Type Error : " + dlg.dlgType);
            }
            return returnAttachment;
        }


        public Attachment getAttachmentFromDialog(CardList card, Activity activity)
        {
            ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
            Attachment returnAttachment = new Attachment();

            string cardDiv = "";
            string cardVal = "";

            List<CardImage> cardImages = new List<CardImage>();
            List<CardAction> cardButtons = new List<CardAction>();
            HistoryLog("CARD IMG START");
            if (card.imgUrl != null)
            {
                HistoryLog("FB CARD IMG " + card.imgUrl);
                cardImages.Add(new CardImage(url: card.imgUrl));
            }


            HistoryLog("CARD BTN1 START");


            if (activity.ChannelId.Equals("facebook") && card.btn1Type == null && !string.IsNullOrEmpty(card.cardDivision) && card.cardDivision.Equals("play") && !string.IsNullOrEmpty(card.cardValue))
            {
                CardAction plButton = new CardAction();
                plButton = new CardAction()
                {
                    Value = card.cardValue,
                    Type = "openUrl",
                    Title = "영상보기"
                };
                cardButtons.Add(plButton);
            }
            else if (card.btn1Type != null)
            {
                CardAction plButton = new CardAction();
                plButton = new CardAction()
                {
                    Value = card.btn1Context,
                    Type = card.btn1Type,
                    Title = card.btn1Title
                };
                cardButtons.Add(plButton);
            }

            if (card.btn2Type != null)
            {
                CardAction plButton = new CardAction();
                HistoryLog("CARD BTN2 START");
                plButton = new CardAction()
                {
                    Value = card.btn2Context,
                    Type = card.btn2Type,
                    Title = card.btn2Title
                };
                cardButtons.Add(plButton);
            }

            if (card.btn3Type != null)
            {
                CardAction plButton = new CardAction();

                HistoryLog("CARD BTN3 START");
                plButton = new CardAction()
                {
                    Value = card.btn3Context,
                    Type = card.btn3Type,
                    Title = card.btn3Title
                };
                cardButtons.Add(plButton);
            }

            if (card.btn4Type != null)
            {
                CardAction plButton = new CardAction();
                HistoryLog("CARD BTN4 START");
                plButton = new CardAction()
                {
                    Value = card.btn4Context,
                    Type = card.btn4Type,
                    Title = card.btn4Title
                };
                cardButtons.Add(plButton);
            }



            if (!string.IsNullOrEmpty(card.cardDivision))
            {
                cardDiv = card.cardDivision;
            }

            if (!string.IsNullOrEmpty(card.cardValue))
            {
                //cardVal = priceMediaDlgList[i].cardValue.Replace();
                cardVal = card.cardValue;
            }


            if(activity.ChannelId.Equals("facebook") && cardButtons.Count < 1 && cardImages.Count < 1)
            {
                HistoryLog("FB CARD BTN1 START channelID.Equals(facebook) && cardButtons.Count < 1 && cardImages.Count < 1");
                Activity reply_facebook = activity.CreateReply();
                reply_facebook.Recipient = activity.From;
                reply_facebook.Type = "message";
                HistoryLog("facebook  card Text : " + card.cardText);
                reply_facebook.Text = card.cardText;
                var reply_ment_facebook = connector.Conversations.SendToConversationAsync(reply_facebook);
            }
            else
            {
                HistoryLog("!!!!!FB CARD BTN1 START channelID.Equals(facebook) && cardButtons.Count < 1 && cardImages.Count < 1");
                HeroCard plCard = new UserHeroCard();
                if (activity.ChannelId == "facebook" && string.IsNullOrEmpty(card.cardValue))
                {
                    HistoryLog("FB CARD BTN1 START channelID.Equals(facebook) && string.IsNullOrEmpty(card.cardValue)");
                    plCard = new UserHeroCard()
                    {
                        Title = card.cardTitle,
                        Images = cardImages,
                        Buttons = cardButtons
                    };
                    returnAttachment = plCard.ToAttachment();
                }
                else
                {
                    HistoryLog("!!!!!!!FB CARD BTN1 START channelID.Equals(facebook) && string.IsNullOrEmpty(card.cardValue)");
                    if (activity.ChannelId == "facebook" && string.IsNullOrEmpty(card.cardTitle))
                    {
                        HistoryLog("FB CARD BTN1 START channelID.Equals(facebook) && string.IsNullOrEmpty(card.cardTitle)");
                        plCard = new UserHeroCard()
                        {
                            Title = "선택해 주세요",
                            Text = card.cardText,
                            Images = cardImages,
                            Buttons = cardButtons,
                            Card_division = cardDiv,
                            Card_value = cardVal
                        };
                        returnAttachment = plCard.ToAttachment();
                    }
                    else
                    {
                        HistoryLog("!!!!!!!!FB CARD BTN1 START channelID.Equals(facebook) && string.IsNullOrEmpty(card.cardTitle)");
                        plCard = new UserHeroCard()
                        {
                            Title = card.cardTitle,
                            Text = card.cardText,
                            Images = cardImages,
                            Buttons = cardButtons,
                            Card_division = cardDiv,
                            Card_value = cardVal
                        };
                        returnAttachment = plCard.ToAttachment();
                    }
                    
                }
            }

            //HeroCard plCard = new UserHeroCard()
            //{
            //    Title = card.cardTitle,
            //    Text = card.cardText,
            //    Images = cardImages,
            //    Buttons = cardButtons,
            //    Card_division = cardDiv,
            //    Card_value = cardVal
            //};
            //returnAttachment = plCard.ToAttachment();

            return returnAttachment;
        }


        public static Attachment GetHeroCard(string title, string subtitle, string text, CardImage cardImage, /*CardAction cardAction*/ List<CardAction> buttons)
        {
            var heroCard = new UserHeroCard
            {
                Title = title,
                Subtitle = subtitle,
                Text = text,
                Images = new List<CardImage>() { cardImage },
                Buttons = buttons,
            };

            return heroCard.ToAttachment();
        }
        public Attachment GetHeroCard(string title, string subtitle, string text, CardImage cardImage, /*CardAction cardAction*/ List<CardAction> buttons, string cardDivision, string cardValue)
        {
            var heroCard = new UserHeroCard
            {
                Title = title,
                Subtitle = subtitle,
                Text = text,
                Images = new List<CardImage>() { cardImage },
                Buttons = buttons,
                Card_division = cardDivision,
                Card_value = cardValue,

            };

            return heroCard.ToAttachment();
        }
        //지도 맵 추가
        public static Attachment GetHeroCard_Map(string title, string subtitle, string text, CardImage cardImage, /*CardAction cardAction*/ List<CardAction> buttons, string latitude, string longitude)
        {
            var heroCard = new UserHeroCard
            {
                Title = title,
                Subtitle = subtitle,
                Text = text,
                Images = new List<CardImage>() { cardImage },
                Buttons = buttons,
                Latitude = latitude,
                Longitude = longitude,
            };

            return heroCard.ToAttachment();
        }

    }
}