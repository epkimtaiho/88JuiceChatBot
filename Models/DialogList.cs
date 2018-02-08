using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JuiceChatBot.Models
{
    public class DialogList
    {
        public int dlgId;
        public string dlgType;
        public string dlgGroup;
        public string dlgOrderNo;
        //text
        public string cardTitle;
        public string cardText;
        //media
        public string mediaUrl;
        public string btn1Type;
        public string btn1Title;
        public string btn1Context;
        public string btn2Type;
        public string btn2Title;
        public string btn2Context;
        public string btn3Type;
        public string btn3Title;
        public string btn3Context;
        public string btn4Type;
        public string btn4Title;
        public string btn4Context;
        public string cardDivision;
        public string cardValue;
        //card
        public List<CardList> dialogCard;
    }
}