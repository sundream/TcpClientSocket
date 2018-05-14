using System;
using System.Threading;
using Net;
using Sproto;

namespace TestNet {
	public class TestSprotoTcpSocket {
		public void Run()
		{
			string c2s = 
@"
.package {
	type 0 : integer
	session 1 : integer
	ud 2 : integer
}

login_ping 1006 {
	request {
		str 0 : string
	}
}
";
			string s2c = 
@"
.package {
	type 0 : integer
	session 1 : integer
	ud 2 : integer
}

login_pong 1008 {
	request {
		str 0 : string
		time 1 : integer
	}
}
";
			SprotoMgr C2S = SprotoParser.Parse(c2s);
			SprotoMgr S2C = SprotoParser.Parse(s2c);

			SprotoTcpSocket Client  = new SprotoTcpSocket(S2C,C2S);
			Client.OnConnect += this.OnConnect;
			Client.OnClose += this.OnClose;
			Client.Log = this.LogSocket;
			//string host = "127.0.0.1";
			string host = "111.230.108.129";
			int port = 8888;
			Client.TcpSocket.Connect(host,port);
			Client.Dispatcher.AddHandler("login_pong",this.HandlerLoginPong);


			while (true) {
				Client.TcpSocket.Dispatch();
				Thread.Sleep(1000);
			}
		}

		public void HandlerLoginPong(SprotoTcpSocket Client,RpcMessage Message) {
			// ping-pong loop
			string msg = String.Format("[{0}] op=OnMessage,proto={1},tag={2},ud={3},session={4},type={5},request={6},response={7}",
				Client.TcpSocket.Name,Message.proto,Message.tag,Message.ud,Message.session,Message.type,Message.request,Message.response);
			Console.WriteLine(msg);
			//Debug.Log(msg);
			SprotoObject request = Client.Proto.C2S.NewSprotoObject("login_ping.request");
			request["str"] = "ping";
			Client.SendRequest("login_ping",request);
		}

		private void OnConnect(SprotoTcpSocket Client) {
			//Console.WriteLine("OnConnect");
			// send first ping when onconnect
			SprotoObject request = Client.Proto.C2S.NewSprotoObject("login_ping.request");
			request["str"] = "ping";
			Client.SendRequest("login_ping",request);
		}

		private void OnClose(SprotoTcpSocket Client) {
			//Console.WriteLine("OnClose");
		}

		private void LogSocket(string msg) {
			Console.WriteLine(msg);
			//Debug.Log(msg);
		}
	}
}
