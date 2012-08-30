﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using SignalR;
using SignalR.Mongo;

namespace Mongo.Sample
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Chat", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            RegisterGlobalFilters(GlobalFilters.Filters);
            RegisterRoutes(RouteTable.Routes);

            var config = new MongoConfiguration
                {
                    Server = "localhost",
                    Port = 27017,
                    Database = "signalr",
                    Collection = "test",
                    AllowSlaveReads = true
                };

            GlobalHost.DependencyResolver.UseMongo(config);

            //tweaked values for AWS ELB support
            //GlobalHost.Configuration.KeepAlive = TimeSpan.FromSeconds(20);
            //GlobalHost.Configuration.ConnectionTimeout = TimeSpan.FromSeconds(60);
        }
    }
}