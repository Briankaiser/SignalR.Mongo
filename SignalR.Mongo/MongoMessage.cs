using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace SignalR.Mongo
{
    [BsonIgnoreExtraElements]
    public class MongoMessage
    {
        [BsonId]
        public BsonObjectId Id { get; set; }

        [BsonElement("ci")]
        public string ConnectionId { get; set; }

        [BsonElement("ek")]
        public string EventKey { get; set; }

        [BsonElement("v")]
        public string Value { get; set; }

        [BsonIgnore]
        public DateTime? DateCreated
        {
            get
            {
                //if we retrieved the MongoMessage - then it has a ObjectId - so read the date/time
                return Id != null ? Id.Value.CreationTime : (DateTime?)null;
            }
        }

        public MongoMessage(string connectionId, string eventKey, object value)
        {
            ConnectionId = connectionId;
            EventKey = eventKey;
            Value = value.ToString();
        }
    }
}
