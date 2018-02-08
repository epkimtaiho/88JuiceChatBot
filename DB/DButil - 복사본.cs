using KonaChatBot.Models;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Bot.Connector;

namespace KonaChatBot.DB
{
    public class DButil
    {
        DbConnect db = new DbConnect();
        
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
                        //entities 가 없을때 score 0으로 설정
                        if ((int)Luis_before[i]["entities"].Count() < 1)
                        {
                            Luis_before[i]["topScoringIntent"]["score"] = 0;
                        }

                        if (i == 0)
                        {
                            Luis = Luis_before[0];
                            LuisName = returnLuisName[0];
                        }
                        else
                        {

                            if (((float)Luis["topScoringIntent"]["score"] < (float)Luis_before[i]["topScoringIntent"]["score"]) && Luis_before[i]["topScoringIntent"]["intent"].ToString() != "None")
                            {
                                if (Luis_before[i]["entities"].Count() > 0)
                                {
                                    Luis = Luis_before[i];
                                    LuisName = returnLuisName[i];
                                }
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

                        if(MessagesController.relationList != null)
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

                        //string[] luisEntities_array = new string[10];
                        //luisEntities_array = Regex.Split(luisEntities, ",");
                        //Array.Resize(ref luisEntities_array, luisEntities_array.Count(s => s != null));
                        ////Array.Sort(luisEntities_array);
                        ////Array.Reverse(luisEntities_array);
                        //luisEntities_array = luisEntities_array.OrderByDescending(c => c).ToArray();
                        //foreach (var str in luisEntities_array)
                        //{
                        //    Debug.WriteLine(str.ToString());
                        //}
                        //Debug.WriteLine("luisEntities_array = " + luisEntities_array);

                        luisEntities = db.SelectArray(luisEntities);

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

                HttpResponseMessage msg = await client.GetAsync(url);

                if (msg.IsSuccessStatusCode)
                {
                    var JsonDataResponse = await msg.Content.ReadAsStringAsync();
                    jsonObj = JObject.Parse(JsonDataResponse);
                }
                msg.Dispose();

                if (jsonObj["entities"].Count() != 0 && (float)jsonObj["intents"][0]["score"] > 0.3)
                {
                    //break;
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
                HeroCard plCard = new HeroCard()
                {
                    Title = dlg.cardTitle,
                    Text = dlg.cardText
                };
                returnAttachment = plCard.ToAttachment();
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
                if (dlg.btn1Type != null)
                {
                    CardAction plButton = new CardAction();
                    if (activity.ChannelId.Equals("facebook") && !string.IsNullOrEmpty(dlg.cardDivision) && dlg.cardDivision.Equals("play") && !string.IsNullOrEmpty(dlg.cardValue))
                    {
                        HistoryLog("FB CARD BTN1 START");
                        plButton = new CardAction()
                        {
                            Value = dlg.cardValue,
                            Type = "openUrl",
                            Title = "영상보기"
                        };
                    }
                    else
                    {
                        HistoryLog("CARD BTN1 START");
                        plButton = new CardAction()
                        {
                            Value = dlg.btn1Context,
                            Type = dlg.btn1Type,
                            Title = dlg.btn1Title
                        };
                    }
                    //CardAction plButton = new CardAction()
                    //{
                    //    Value = card.btn1Context,
                    //    Type = card.btn1Type,
                    //    Title = card.btn1Title
                    //};

                    cardButtons.Add(plButton);
                }

                if (dlg.btn2Type != null)
                {
                    CardAction plButton = new CardAction();
                    if (activity.ChannelId.Equals("facebook") && string.IsNullOrEmpty(dlg.cardValue))
                    {
                        HistoryLog("FB CARD BTN2 START");
                        plButton = new CardAction()
                        {
                            Value = dlg.cardValue,
                            Type = "openUrl",
                            Title = "영상보기"
                        };
                    }
                    else
                    {
                        HistoryLog("CARD BTN2 START");
                        plButton = new CardAction()
                        {
                            Value = dlg.btn2Context,
                            Type = dlg.btn2Type,
                            Title = dlg.btn2Title
                        };
                    }
                    //CardAction plButton = new CardAction()
                    //{
                    //    Value = card.btn2Context,
                    //    Type = card.btn2Type,
                    //    Title = card.btn2Title
                    //};

                    cardButtons.Add(plButton);
                }

                if (dlg.btn3Type != null)
                {
                    CardAction plButton = new CardAction();
                    if (MessagesController.channelID.Equals("facebook") && string.IsNullOrEmpty(dlg.cardValue))
                    {
                        HistoryLog("FB CARD BTN3 START");
                        plButton = new CardAction()
                        {
                            Value = dlg.cardValue,
                            Type = "openUrl",
                            Title = "영상보기"
                        };
                    }
                    else
                    {
                        HistoryLog("CARD BTN3 START");
                        plButton = new CardAction()
                        {
                            Value = dlg.btn3Context,
                            Type = dlg.btn3Type,
                            Title = dlg.btn3Title
                        };
                    }
                    //CardAction plButton = new CardAction()
                    //{
                    //    Value = card.btn3Context,
                    //    Type = card.btn3Type,
                    //    Title = card.btn3Title
                    //};

                    cardButtons.Add(plButton);
                }

                if (dlg.btn4Type != null)
                {
                    CardAction plButton = new CardAction();
                    if (activity.ChannelId.Equals("facebook") && !string.IsNullOrEmpty(dlg.cardValue))
                    {
                        HistoryLog("FB CARD BTN4 START");
                        plButton = new CardAction()
                        {
                            Value = dlg.cardValue,
                            Type = "openUrl",
                            Title = "영상보기"
                        };
                    }
                    else
                    {
                        HistoryLog("CARD BTN4 START");
                        plButton = new CardAction()
                        {
                            Value = dlg.btn4Context,
                            Type = dlg.btn4Type,
                            Title = dlg.btn4Title
                        };
                    }
                    //CardAction plButton = new CardAction()
                    //{
                    //    Value = card.btn4Context,
                    //    Type = card.btn4Type,
                    //    Title = card.btn4Title
                    //};

                    cardButtons.Add(plButton);
                }


                
                if (activity.ChannelId.Equals("facebook") && !string.IsNullOrEmpty(dlg.cardDivision) && dlg.cardDivision.Equals("play") && !string.IsNullOrEmpty(dlg.cardValue))
                {
                    HistoryLog("FB CARD BTN1 START");
                    CardAction plButton = new CardAction()
                    {
                        Value = dlg.cardValue,
                        Type = "openUrl",
                        Title = "영상보기"
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


                if (activity.ChannelId.Equals("facebook") && cardButtons.Count < 1 && cardImages.Count < 1)
                {
                    HistoryLog("FB CARD BTN1 START channelID.Equals(facebook) && cardButtons.Count < 1 && cardImages.Count < 1");
                    Activity reply_facebook = activity.CreateReply();
                    reply_facebook.Recipient = activity.From;
                    reply_facebook.Type = "message";
                    HistoryLog("facebook  card Text : " + dlg.cardText);
                    reply_facebook.Text = dlg.cardText;
                    var reply_ment_facebook = connector.Conversations.SendToConversationAsync(reply_facebook);
                }
                else
                {
                    HistoryLog("!!!!!FB CARD BTN1 START channelID.Equals(facebook) && cardButtons.Count < 1 && cardImages.Count < 1");
                    HeroCard plCard = new UserHeroCard();
                    if (activity.ChannelId == "facebook" && string.IsNullOrEmpty(dlg.cardValue))
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
                        HistoryLog("!!!!!!!FB CARD BTN1 START channelID.Equals(facebook) && string.IsNullOrEmpty(card.cardValue)");
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
                }
            }
            else
            {
                Debug.WriteLine("Dialog Type Error : " + dlg.dlgType);
            }
            return returnAttachment;
        }

        //public Attachment getAttachmentFromDialog(DialogList dlg, Activity activity)
        //{
        //    Attachment returnAttachment = new Attachment();
        //    ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
        //    if (dlg.dlgType.Equals(MessagesController.TEXTDLG))
        //    {
        //        HeroCard plCard = new HeroCard()
        //        {
        //            Title = dlg.cardTitle,
        //            Text = dlg.cardText
        //        };
        //        returnAttachment = plCard.ToAttachment();
        //    }
        //    else if (dlg.dlgType.Equals(MessagesController.MEDIADLG))
        //    {

        //        string cardDiv = "";
        //        string cardVal = "";

        //        List<CardImage> cardImages = new List<CardImage>();
        //        List<CardAction> cardButtons = new List<CardAction>();

        //        if (dlg.mediaUrl != null)
        //        {
        //            cardImages.Add(new CardImage(url: dlg.mediaUrl));
        //        }

        //        if (dlg.btn1Type != null)
        //        {
        //            CardAction plButton = new CardAction()
        //            {
        //                Value = dlg.btn1Context,
        //                Type = dlg.btn1Type,
        //                Title = dlg.btn1Title
        //            };

        //            cardButtons.Add(plButton);
        //        }

        //        if (dlg.btn2Type != null)
        //        {
        //            CardAction plButton = new CardAction()
        //            {
        //                Value = dlg.btn2Context,
        //                Type = dlg.btn2Type,
        //                Title = dlg.btn2Title
        //            };

        //            cardButtons.Add(plButton);
        //        }

        //        if (dlg.btn3Type != null)
        //        {
        //            CardAction plButton = new CardAction()
        //            {
        //                Value = dlg.btn3Context,
        //                Type = dlg.btn3Type,
        //                Title = dlg.btn3Title
        //            };

        //            cardButtons.Add(plButton);
        //        }

        //        if (dlg.btn4Type != null)
        //        {
        //            CardAction plButton = new CardAction()
        //            {
        //                Value = dlg.btn4Context,
        //                Type = dlg.btn4Type,
        //                Title = dlg.btn4Title
        //            };

        //            cardButtons.Add(plButton);
        //        }
        //        if (!string.IsNullOrEmpty(dlg.cardDivision))
        //        {
        //            cardDiv = dlg.cardDivision;
        //        }

        //        if (!string.IsNullOrEmpty(dlg.cardValue))
        //        {
        //            //cardVal = priceMediaDlgList[i].cardValue.Replace();
        //            cardVal = dlg.cardValue;
        //        }

        //        HeroCard plCard = new UserHeroCard()
        //        {
        //            Title = dlg.cardTitle,
        //            Text = dlg.cardText,
        //            Images = cardImages,
        //            Buttons = cardButtons,
        //            Card_division = cardDiv,
        //            Card_value = cardVal

        //        };
        //        returnAttachment = plCard.ToAttachment();
        //    }
        //    else
        //    {
        //        Debug.WriteLine("Dialog Type Error : " + dlg.dlgType);
        //    }
        //    return returnAttachment;
        //}

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
            if (card.btn1Type != null)
            {
                CardAction plButton = new CardAction();
                if (activity.ChannelId.Equals("facebook") && !string.IsNullOrEmpty(card.cardDivision) && card.cardDivision.Equals("play") && !string.IsNullOrEmpty(card.cardValue))
                {
                    HistoryLog("FB CARD BTN1 START");
                    plButton = new CardAction()
                    {
                        Value = card.cardValue,
                        Type = "openUrl",
                        Title = "영상보기"
                    };
                }
                else
                {
                    HistoryLog("CARD BTN1 START");
                    plButton = new CardAction()
                    {
                        Value = card.btn1Context,
                        Type = card.btn1Type,
                        Title = card.btn1Title
                    };
                }
                //CardAction plButton = new CardAction()
                //{
                //    Value = card.btn1Context,
                //    Type = card.btn1Type,
                //    Title = card.btn1Title
                //};

                cardButtons.Add(plButton);
            }

            if (card.btn2Type != null)
            {
                CardAction plButton = new CardAction();
                if (activity.ChannelId.Equals("facebook") && string.IsNullOrEmpty(card.cardValue))
                {
                    HistoryLog("FB CARD BTN2 START");
                    plButton = new CardAction()
                    {
                        Value = card.cardValue,
                        Type = "openUrl",
                        Title = "영상보기"
                    };
                }
                else
                {
                    HistoryLog("CARD BTN2 START");
                    plButton = new CardAction()
                    {
                        Value = card.btn2Context,
                        Type = card.btn2Type,
                        Title = card.btn2Title
                    };
                }
                //CardAction plButton = new CardAction()
                //{
                //    Value = card.btn2Context,
                //    Type = card.btn2Type,
                //    Title = card.btn2Title
                //};

                cardButtons.Add(plButton);
            }

            if (card.btn3Type != null)
            {
                CardAction plButton = new CardAction();
                if (MessagesController.channelID.Equals("facebook") && string.IsNullOrEmpty(card.cardValue))
                {
                    HistoryLog("FB CARD BTN3 START");
                    plButton = new CardAction()
                    {
                        Value = card.cardValue,
                        Type = "openUrl",
                        Title = "영상보기"
                    };
                }
                else
                {
                    HistoryLog("CARD BTN3 START");
                    plButton = new CardAction()
                    {
                        Value = card.btn3Context,
                        Type = card.btn3Type,
                        Title = card.btn3Title
                    };
                }
                //CardAction plButton = new CardAction()
                //{
                //    Value = card.btn3Context,
                //    Type = card.btn3Type,
                //    Title = card.btn3Title
                //};

                cardButtons.Add(plButton);
            }

            if (card.btn4Type != null)
            {
                CardAction plButton = new CardAction();
                if (activity.ChannelId.Equals("facebook") && !string.IsNullOrEmpty(card.cardValue))
                {
                    HistoryLog("FB CARD BTN4 START");
                    plButton = new CardAction()
                    {
                        Value = card.cardValue,
                        Type = "openUrl",
                        Title = "영상보기"
                    };
                }
                else
                {
                    HistoryLog("CARD BTN4 START");
                    plButton = new CardAction()
                    {
                        Value = card.btn4Context,
                        Type = card.btn4Type,
                        Title = card.btn4Title
                    };
                }
                //CardAction plButton = new CardAction()
                //{
                //    Value = card.btn4Context,
                //    Type = card.btn4Type,
                //    Title = card.btn4Title
                //};

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

        public Attachment getRecommendDialog(int rcmdDlgId)
        {
            CardImage cardImage = new CardImage();
            List<CardAction> cardButtons = new List<CardAction>();
            //MEDIA 데이터 추출
            Attachment returnAttachment = new Attachment();

            List<Recommend_DLG_MEDIA> SelectRecommend_DLG_MEDIA = db.SelectRecommend_DLG_MEDIA(rcmdDlgId);
            if (rcmdDlgId != 4)
            {
                for (int i = 0; i < SelectRecommend_DLG_MEDIA.Count; i++)
                {
                    //CardImage 입력
                    cardImage = new CardImage()
                    {
                        Url = SelectRecommend_DLG_MEDIA[i].media_url
                    };

                    if (SelectRecommend_DLG_MEDIA[i].btn_1_context.Length != 0)
                    {
                        CardAction plButton = new CardAction()
                        {
                            Value = SelectRecommend_DLG_MEDIA[i].btn_1_context,
                            Type = SelectRecommend_DLG_MEDIA[i].btn_1_type,
                            Title = SelectRecommend_DLG_MEDIA[i].btn_1_title
                        };
                        cardButtons.Add(plButton);
                    }

                    if (SelectRecommend_DLG_MEDIA[i].btn_2_context.Length != 0)
                    {
                        CardAction plButton = new CardAction()
                        {
                            Value = SelectRecommend_DLG_MEDIA[i].btn_2_context,
                            Type = SelectRecommend_DLG_MEDIA[i].btn_2_type,
                            Title = SelectRecommend_DLG_MEDIA[i].btn_2_title
                        };
                        cardButtons.Add(plButton);
                    }

                    if (SelectRecommend_DLG_MEDIA[i].btn_3_context.Length != 0)
                    {
                        CardAction plButton = new CardAction()
                        {
                            Value = SelectRecommend_DLG_MEDIA[i].btn_3_context,
                            Type = SelectRecommend_DLG_MEDIA[i].btn_3_type,
                            Title = SelectRecommend_DLG_MEDIA[i].btn_3_title
                        };
                        cardButtons.Add(plButton);
                    }

                    if (SelectRecommend_DLG_MEDIA[i].btn_4_context.Length != 0)
                    {
                        CardAction plButton = new CardAction()
                        {
                            Value = SelectRecommend_DLG_MEDIA[i].btn_4_context,
                            Type = SelectRecommend_DLG_MEDIA[i].btn_4_type,
                            Title = SelectRecommend_DLG_MEDIA[i].btn_4_title
                        };
                        cardButtons.Add(plButton);
                    }

                    if (SelectRecommend_DLG_MEDIA[i].btn_5_context.Length != 0)
                    {
                        CardAction plButton = new CardAction()
                        {
                            Value = SelectRecommend_DLG_MEDIA[i].btn_5_context,
                            Type = SelectRecommend_DLG_MEDIA[i].btn_5_type,
                            Title = SelectRecommend_DLG_MEDIA[i].btn_5_title
                        };
                        cardButtons.Add(plButton);
                    }

                    //message.Attachments.Add(GetHeroCard(SelectRecommend_DLG_MEDIA[i].card_title, "", SelectRecommend_DLG_MEDIA[i].card_text, cardImage, cardButtons));
                    returnAttachment = GetHeroCard(SelectRecommend_DLG_MEDIA[i].card_title, "", SelectRecommend_DLG_MEDIA[i].card_text, cardImage, cardButtons);
                }
            }
            else
            {
                string domainURL = "https://bottest.hyundai.com";

                List<RecommendList> RecommendList = db.SelectedRecommendList();
                RecommendList recommend = new RecommendList();

                for (var i = 0; i < RecommendList.Count; i++)
                {
                    string main_color_view = "";
                    string main_color_view_nm = "";

                    if (!string.IsNullOrEmpty(RecommendList[i].MAIN_COLOR_VIEW_1))
                    {
                        main_color_view += domainURL + "/assets/images/price/360/" + RecommendList[i].MAIN_COLOR_VIEW_1 + "/00001.jpg" + "@";
                        main_color_view_nm += RecommendList[i].MAIN_COLOR_VIEW_NM1 + "@";
                    };

                    if (!string.IsNullOrEmpty(RecommendList[i].MAIN_COLOR_VIEW_2))
                    {
                        main_color_view += domainURL + "/assets/images/price/360/" + RecommendList[i].MAIN_COLOR_VIEW_2 + "/00001.jpg" + "@";
                        main_color_view_nm += RecommendList[i].MAIN_COLOR_VIEW_NM2 + "@";
                    };

                    if (!string.IsNullOrEmpty(RecommendList[i].MAIN_COLOR_VIEW_3))
                    {
                        main_color_view += domainURL + "/assets/images/price/360/" + RecommendList[i].MAIN_COLOR_VIEW_3 + "/00001.jpg" + "@";
                        main_color_view_nm += RecommendList[i].MAIN_COLOR_VIEW_NM3 + "@";
                    };

                    if (!string.IsNullOrEmpty(RecommendList[i].MAIN_COLOR_VIEW_4))
                    {
                        main_color_view += domainURL + "/assets/images/price/360/" + RecommendList[i].MAIN_COLOR_VIEW_4 + "/00001.jpg" + "@";
                        main_color_view_nm += RecommendList[i].MAIN_COLOR_VIEW_NM4 + "@";
                    };

                    if (!string.IsNullOrEmpty(RecommendList[i].MAIN_COLOR_VIEW_5))
                    {
                        main_color_view += domainURL + "/assets/images/price/360/" + RecommendList[i].MAIN_COLOR_VIEW_5 + "/00001.jpg" + "@";
                        main_color_view_nm += RecommendList[i].MAIN_COLOR_VIEW_NM5 + "@";
                    };

                    if (!string.IsNullOrEmpty(RecommendList[i].MAIN_COLOR_VIEW_6))
                    {
                        main_color_view += domainURL + "/assets/images/price/360/" + RecommendList[i].MAIN_COLOR_VIEW_6 + "/00001.jpg" + "@";
                        main_color_view_nm += RecommendList[i].MAIN_COLOR_VIEW_NM6 + "@";
                    };

                    if (!string.IsNullOrEmpty(RecommendList[i].MAIN_COLOR_VIEW_7))
                    {
                        main_color_view += domainURL + "/assets/images/price/360/" + RecommendList[i].MAIN_COLOR_VIEW_7 + "/00001.jpg";
                        main_color_view_nm += RecommendList[i].MAIN_COLOR_VIEW_NM7 + "@";
                    };

                    main_color_view = main_color_view.TrimEnd('@');
                    main_color_view_nm = main_color_view_nm.TrimEnd('@');

                    var subtitle = RecommendList[i].TRIM_DETAIL + "|" + "가격: " + RecommendList[i].TRIM_DETAIL_PRICE + "|" +
                                    main_color_view + "|" +
                                    RecommendList[i].OPTION_1_IMG_URL + "|" +
                                    RecommendList[i].OPTION_1 + "|" +
                                    RecommendList[i].OPTION_2_IMG_URL + "|" +
                                    RecommendList[i].OPTION_2 + "|" +
                                    RecommendList[i].OPTION_3_IMG_URL + "|" +
                                    RecommendList[i].OPTION_3 + "|" +
                                    RecommendList[i].OPTION_4_IMG_URL + "|" +
                                    RecommendList[i].OPTION_4 + "|" +
                                    RecommendList[i].OPTION_5_IMG_URL + "|" +
                                    RecommendList[i].OPTION_5 + "|" +
                                    main_color_view_nm;

                    if (SelectRecommend_DLG_MEDIA[0].btn_1_title.Length != 0)
                    {
                        CardAction plButton = new CardAction()
                        {
                            Value = SelectRecommend_DLG_MEDIA[0].btn_1_context,
                            Type = SelectRecommend_DLG_MEDIA[0].btn_1_type,
                            Title = SelectRecommend_DLG_MEDIA[0].btn_1_title
                        };
                        cardButtons.Add(plButton);
                    }

                    if (SelectRecommend_DLG_MEDIA[0].btn_2_title.Length != 0)
                    {
                        CardAction plButton = new CardAction()
                        {
                            Value = SelectRecommend_DLG_MEDIA[0].btn_2_context,
                            Type = SelectRecommend_DLG_MEDIA[0].btn_2_type,
                            Title = SelectRecommend_DLG_MEDIA[0].btn_2_title
                        };
                        cardButtons.Add(plButton);
                    }
                    returnAttachment = GetHeroCard("trim", subtitle, "고객님께서 선택한 결과에 따라 차량을 추천해 드릴게요", cardImage, cardButtons);
                }
            }
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

        public static async Task<Translator> getTranslate(string input)
        {
            Translator trans = new Translator();

            using (HttpClient client = new HttpClient())
            {
                string appId = "AIzaSyDr4CH9BVfENdM9uoSK0fANFVWD0gGXlJM";

                string url = string.Format("https://translation.googleapis.com/language/translate/v2/?key={0}&q={1}&source=ko&target=en&model=nmt", appId, input);

                HttpResponseMessage msg = await client.GetAsync(url);

                if (msg.IsSuccessStatusCode)
                {
                    var JsonDataResponse = await msg.Content.ReadAsStringAsync();
                    trans = JsonConvert.DeserializeObject<Translator>(JsonDataResponse);
                }
                return trans;
            }

        }
    }
}