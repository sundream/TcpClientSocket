using System;
using System.Threading;
using Net;
using Sproto;

namespace TestNet {
	public class TestTcpClientSocket {
		private SprotoRpc Proto;
		private TcpClientSocket TcpSocket;

		public TestTcpClientSocket() {
			string c2s = 
@"
.package {
	type 0 : integer
	session 1 : integer
	ud 2 : integer
}

C2GS_Ping 1006 {
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

GS2C_Pong 1108 {
	request {
		str 0 : string
		time 1 : integer
	}
}
";
			SprotoMgr C2SProto = SprotoParser.Parse(c2s);
			SprotoMgr S2CProto = SprotoParser.Parse(s2c);
			Proto = new SprotoRpc(S2CProto,C2SProto);

			TcpSocket = new TcpClientSocket();
		}

		public void Run() {
			TcpSocket.Register(OnConnect,OnClose,OnMessage,Log);
			//string host = "127.0.0.1";
			string host = "111.230.108.129";
			int port = 8888;
			TcpSocket.Connect(host,port);

			while (true) {
				//Console.WriteLine("tcpsocket.Dispatch");
				ClientLogic();
				FixUpdated();
				Thread.Sleep(2000);
			}
		}

		public void ClientLogic() {
			// ping
			SprotoObject request = Proto.C2S.NewSprotoObject("C2GS_Ping.request");
			request["str"] = "hello,world!";
			RpcPackage Package = Proto.PackRequest("C2GS_Ping",request);
			TcpSocket.Send(Package.data,Package.size);
		}

		public void FixUpdated() {
			TcpSocket.Dispatch();
		}

		public void OnConnect(TcpClientSocket tcpsocket) {
			//Console.WriteLine("OnConnect dosomething");
		}

		public void OnClose(TcpClientSocket tcpsocket) {
			//Console.WriteLine("OnClose dosomething");
		}

		public void OnMessage(TcpClientSocket tcpsocket,byte[] data,int size) {
			//Console.WriteLine("OnMessage dosomething");
			// call SprotoRpc.Dispatch to unpack sproto package
			RpcMessage message = Proto.UnpackMessage(data,size);
			Console.WriteLine("[{0}] op=OnMessage,proto={1},tag={2},ud={3},session={4},type={5},request={6},response={7}",
					tcpsocket.Name,message.proto,message.tag,message.ud,message.session,message.type,message.request,message.response);
		}

		public static void Log(string msg) {
			Console.WriteLine(msg);
		}

	}
}
