using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SignalR.Mongo
{
    public static class DependencyResolverExtensions
    {
        public static IDependencyResolver UseMongo(this IDependencyResolver resolver, MongoConfiguration config)
        {
            var bus = new Lazy<MongoMessageBus>(() => new MongoMessageBus(config, resolver));
            resolver.Register(typeof(IMessageBus), () => bus.Value);

            return resolver;
        }
    }
}
