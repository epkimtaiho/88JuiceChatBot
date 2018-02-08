using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace JuiceChatBot.Models
{
	public class SorryMessageList
	{
		public static string GetSorryMessage(int chk)
		{

			Debug.WriteLine("sorry cnt : " + chk);
			String sorryMsg = "";

			if (chk == 0)
			{
				sorryMsg = "쉽게 다시 한번 말씀해 주시면 안 될까요? 예를들어 코나의 특징이 알고 싶으시면 ''특징'' 이란 단어 입력만으로도 제가 이해할 수 있어요";
			}
			else
			{
				sorryMsg = "죄송해요, 무슨 말인지 이해하지 못했어요";
				//sorryMsg = "죄송해요, 준비된 답변이 없어요ㅜㅜ그런데…좋은 질문이에요!제게 답변할 기회를 주실래요? 지금 코나 챗봇 페이스북에서 답변 공약 이벤트 중이에요,  방금과 같은 질문과 별명을 함께 이벤트 게시물에 댓글로 남기시면 추첨하여 선물도 드리고 9월 25일까지 답변할 것을 약속 드릴게요~그럼 고고씽~!!";
			}
			return sorryMsg;
		}
	}
}