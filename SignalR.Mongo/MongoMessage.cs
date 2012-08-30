using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace SignalR.Mongo
{
    public class MongoMessage
    {
        [BsonId]
        public BsonObjectId Id { get; set; }

        [BsonElement("ci")]
        public string ConnectionId { get; set; }

        [BsonElement("dc")]
        public DateTime DateCreated { get; set; }

        [BsonElement("ek")]
        public string EventKey { get; set; }

        [BsonElement("v")]
        public string Value { get; set; }


        public MongoMessage(string connectionId, string eventKey, object value)
        {
            ConnectionId = connectionId;
            EventKey = eventKey;
            Value = value.ToString();
            DateCreated = DateTime.UtcNow;
        }
    }
}
