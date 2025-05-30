using System;
using System.Collections.Generic;

namespace BlockBlast
{
    public enum MessageType
    {
        SendFigure,
        ScoreUpdate,
        PlayerMove,
        GameOver,
        Chat,
        Unknown,
        Nickname
    }

    public class NetworkMessage
    {
        public MessageType Type { get; set; }
        public string Payload { get; set; }

        public NetworkMessage() { }

        public NetworkMessage(MessageType type, string payload)
        {
            Type = type;
            Payload = payload;
        }
    }
}
