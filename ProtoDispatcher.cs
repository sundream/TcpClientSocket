using System;
using System.Collections.Generic;
using Sproto;

namespace Net {
	public class ProtoDispatcher {
		public delegate void MessageHandler(SprotoTcpSocket Client,RpcMessage Message);

		private Dictionary<string,MessageHandler> Handlers = new Dictionary<string,MessageHandler>();

		public MessageHandler GetHandler(string proto) {
			MessageHandler handler = null;
			if (!this.Handlers.TryGetValue(proto,out handler)) {
					return null;
			}
			return handler;
		}

		public void AddHandler(string proto,MessageHandler handler) {
			this.Handlers.Add(proto,handler);
		}

		public bool RemoveHandler(string proto) {
			return this.Handlers.Remove(proto);
		}

		public bool DelHandler(string proto) {
			return this.RemoveHandler(proto);
		}

		public void Dispatch(SprotoTcpSocket Client,RpcMessage Message) {
			string proto = Message.proto;
			MessageHandler handler = this.GetHandler(proto);
			if (handler == null) {
				return;
			}
			handler(Client,Message);
		}
	}
}
