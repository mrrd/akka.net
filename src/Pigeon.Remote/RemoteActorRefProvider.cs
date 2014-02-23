﻿using Pigeon.Actor;
using Pigeon.Configuration;
using Pigeon.Dispatch;
using Pigeon.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pigeon.Remote
{
    public class RemoteActorRefProvider : ActorRefProvider
    {
        public RemoteDaemon RemoteDaemon { get;private set; }

        public RemoteActorRefProvider(ActorSystem system)
            : base(system)
        {
            this.config = system.Settings.Config.GetConfig("akka.remote");
        }

        private Config config;

        public override void Init()
        {
            var host = config.GetString("server.host");
            var port = config.GetInt("server.port");
            this.Address = new Address("akka.tcp", System.Name, host, port);

            base.Init();

            this.RemoteDaemon = new Remote.RemoteDaemon (this.System,RootPath / "remote",null);
            this.RemoteTransport = new Remoting(System, this);
            RemoteHost.StartHost(System, port);
        }

        public override InternalActorRef ActorOf(ActorSystem system, Props props, InternalActorRef supervisor, ActorPath path)
        {
            var mailbox = System.Mailboxes.FromConfig(props.Mailbox);

            ActorCell cell = null;
            if (props.RouterConfig is NoRouter)
            {
                cell = new ActorCell(system,supervisor, props, path, mailbox);
              
            }
            else
            {
                cell = new RoutedActorCell(system,supervisor, props, path, mailbox);
            }

            cell.NewActor();
         //   parentContext.Watch(cell.Self);
            return cell.Self;
        }

        public override ActorRef ResolveActorRef(ActorPath actorPath)
        {
            if (this.Address.Equals(actorPath.Address))
            {
                if (actorPath.Head == "remote")
                {
                    //skip ""/"remote", 
                    var parts = actorPath.Skip(2).ToArray();
                    return RemoteDaemon.GetChild(parts);
                }
                else if (actorPath.Head == "temp")
                {
                    //skip ""/"temp", 
                    var parts = actorPath.Skip(2).ToArray();
                    return TempContainer.GetChild(parts);
                }
                else
                {
                    //standard
                    var currentContext = RootCell;
                    foreach (var part in actorPath.Skip(1))
                    {
                        currentContext = ((LocalActorRef)currentContext.Child(part)).Cell;
                    }
                    return currentContext.Self;
                }           
            }
            else
            {
                return new BrokenRemoteActorRef(System, actorPath, this.Address.Port.Value);
            }
        }

        public void UseActorOnNode(RemoteActorRef actor, Props props, Deploy deploy, InternalActorRef parent)
        {
            throw new NotImplementedException();
        }

        public Remoting RemoteTransport { get;private set; }
    }
}
