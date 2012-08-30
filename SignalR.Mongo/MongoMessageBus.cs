using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace SignalR.Mongo
{
    public class MongoMessageBus : IMessageBus, IIdGenerator<BsonObjectId>
    {
        private readonly InProcessMessageBus<BsonObjectId> _bus;
        private bool _connectionReady;
        private Task _connectingTask;
        private MongoServer _mongoServer;
        private MongoDatabase _mongoDatabase;
        private MongoCollection<MongoMessage> _mongoCollection;

        private readonly MongoConfiguration _config;

        

        private bool ConnectionReady
        {
            get { return _connectionReady && _mongoServer != null && _mongoServer.State == MongoServerState.Connected; }
        }

        public MongoMessageBus(MongoConfiguration config, IDependencyResolver resolver)
        {
            _config = config;
            _bus = new InProcessMessageBus<BsonObjectId>(resolver, this);

            VerifyConfig();
            EnsureConnection();
        }

        private void VerifyConfig()
        {
            if (_config == null)
                throw new ArgumentNullException("MongoConfiguration can not be null");

            if(string.IsNullOrWhiteSpace(_config.Server))
                throw new ArgumentException("Mongo server must be set in constructor");

            if (_config.Port == default(int))
                throw new ArgumentException("Mongo port must be set in constructor");

            if (string.IsNullOrWhiteSpace(_config.Database))
                throw new ArgumentException("Mongo database must be set in constructor");

            if (string.IsNullOrWhiteSpace(_config.Collection))
                throw new ArgumentException("Mongo collection must be set in constructor");
        }

        #region Implementation of IMessageBus

        public Task<MessageResult> GetMessages(IEnumerable<string> eventKeys, string id, CancellationToken cancel)
        {
            return _bus.GetMessages(eventKeys, id, cancel);
        }

        public Task Send(string connectionId, string eventKey, object value)
        {
            var message = new MongoMessage(connectionId, eventKey, value);
            if (ConnectionReady)
            {
                return Task.Factory.StartNew(() => _mongoCollection.Insert(message)).Catch();
            }

            return OpenConnection().Then(() => Task.Factory.StartNew(() => _mongoCollection.Insert(message)));
        }

        #endregion

        #region Implementation of IIdGenerator<BsonObjectId>

        public BsonObjectId GetNext()
        {
            return BsonObjectId.GenerateNewId();
        }

        public BsonObjectId ConvertFromString(string value)
        {
            return BsonObjectId.Parse(value);
        }

        public string ConvertToString(BsonObjectId value)
        {
            return value.ToString();
        }

        #endregion



        private Task OpenConnection()
        {
            if (ConnectionReady)
            {
                return TaskAsyncHelper.Empty;
            }
            try
            {
                EnsureConnection();
                return _connectingTask.Catch();
            }
            catch (Exception ex)
            {
                return TaskAsyncHelper.FromError(ex);
            }

        }

        private void EnsureConnection()
        {
            var tcs = new TaskCompletionSource<object>();
            
            if (Interlocked.CompareExchange(ref _connectingTask, tcs.Task, null) != null)
            {
                // Give all clients the same task for reconnecting
                return;
            }

            try
            {
                if (_mongoServer == null)
                {
                    var settings = new MongoServerSettings()
                        {
                            SafeMode = SafeMode.False, //this could be a setting in the future
                            SlaveOk = _config.AllowSlaveReads, 
                            Server = new MongoServerAddress(_config.Server, _config.Port)
                        };
                    _mongoServer = new MongoServer(settings);
                    Task.Factory.StartNew(OpenConnectionTask).ContinueWith((task) =>
                            {
                                if (task.IsFaulted)
                                {
                                    tcs.SetException(task.Exception);
                                    return;
                                }
                                if (task.IsCanceled)
                                {
                                    tcs.SetCanceled();
                                    return;
                                }
                                //ready to send notifications
                                tcs.SetResult(null);
                                _connectionReady = true;
                                //setup the receiving loop
                                //this is a blocking call while running
                                OpenReceivingLoop();
                            });

                }
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }


        private void OpenConnectionTask( )
        {
            _mongoDatabase = _mongoServer.GetDatabase(_config.Database);

            //create the collection if it doesn't exist
            if (!_mongoDatabase.CollectionExists(_config.Collection))
            {

                _mongoDatabase.CreateCollection(_config.Collection,
                                                CollectionOptions.SetAutoIndexId(true).SetCapped(true).SetMaxSize(_config.CollectionSize));
            }

            _mongoCollection = _mongoDatabase.GetCollection<MongoMessage>(_config.Collection);

            if (!_mongoCollection.IsCapped())
            {
                throw new MongoConnectionException(string.Format("MongoCollection {0} must be capped", _config.Collection));
            }
        }

        private void OpenReceivingLoop()
        {
            BsonObjectId startId = null;
            //grab the id of the 'end' so we only query the last
            var startIdObj = _mongoCollection.FindAll()
                .SetSortOrder(SortBy.Descending("$natural"))
                .FirstOrDefault();

            if (startIdObj != null)
                startId = startIdObj.Id;    

            
            while (true)
            {
                MongoCursor<MongoMessage> cursor;
                if (startId == null)
                {
                    cursor = _mongoCollection.FindAll()
                        .SetFlags(QueryFlags.TailableCursor | QueryFlags.AwaitData)
                        .SetSortOrder("$natural");
                }
                else
                {
                    cursor = _mongoCollection.Find(Query.GT("_id", startId))
                        .SetFlags(QueryFlags.TailableCursor | QueryFlags.AwaitData)
                        .SetOption("$oplogReplay", true) // <-- this doesn't work in the C# driver. Need to fix for initial loading perf
                        .SetSortOrder("$natural");
                }

                
                using (var enumerator = (MongoCursorEnumerator<MongoMessage>)cursor.GetEnumerator())
                {
                    while (true)
                    {
                        if (enumerator.MoveNext())
                        {
                            OnMessage(enumerator.Current);
                        }
                        else
                        {
                            if (enumerator.IsDead) break;

                            if (!enumerator.IsServerAwaitCapable) Thread.Sleep(TimeSpan.FromMilliseconds(50));
                        }
                        
                    }
                }
            }
        }

        private void OnMessage(MongoMessage current)
        {
            try
            {
                _bus.Send(current.ConnectionId, current.EventKey, current.Value).Catch();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                //throw;
            }
        }
    }
}
