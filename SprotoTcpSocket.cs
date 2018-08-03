using System;
using System.Collections.Generic;
using Sproto;

namespace Net {
	public class SprotoTcpSocket {
		public delegate void OnConnectCallback(SprotoTcpSocket socket);
		public delegate void OnCloseCallback(SprotoTcpSocket socket);
		public delegate void LogType(string msg);

		public event Action<SprotoTcpSocket> OnConnect;
		public event Action<SprotoTcpSocket> OnClose;
		public LogType Log = null;
		public TcpClientSocket TcpSocket;
		public ProtoDispatcher Dispatcher;
		public SprotoRpc Proto;
		private Int64 message_id = 0;
		private Int64 session_id = 0;
		private Dictionary<Int64,ProtoDispatcher.MessageHandler> sessions = new Dictionary<Int64,ProtoDispatcher.MessageHandler>();

		public SprotoTcpSocket (string s2cfile,string c2sfile,bool isbinary=false) {
			this.TcpSocket = new TcpClientSocket();
			this.TcpSocket.Register(this._OnConnect,this._OnClose,this._OnMessage,this._Log);
			this.Dispatcher = new ProtoDispatcher();
			SprotoMgr S2C = SprotoParser.ParseFile(s2cfile);
			SprotoMgr C2S = SprotoParser.ParseFile(c2sfile);
			this.Proto = new SprotoRpc(S2C,C2S);
		}

		public SprotoTcpSocket(SprotoMgr S2C,SprotoMgr C2S) {
			this.TcpSocket = new TcpClientSocket();
			this.TcpSocket.Register(this._OnConnect,this._OnClose,this._OnMessage,this._Log);
			this.Dispatcher = new ProtoDispatcher();
			this.Proto = new SprotoRpc(S2C,C2S);
		}
		//Connect,Disconnect,Dispatch use member TcpSocket todo?

		public void SendRequest(string proto,SprotoObject request=null,ProtoDispatcher.MessageHandler handler=null) {
			Int64 session_id = 0;
			if (handler != null) {
				this.session_id = this.session_id + 1;
				session_id = this.session_id;
				this.sessions.Add(session_id,handler);
			}
			Int64 message_id = this.gen_message_id();
			RpcPackage package = this.Proto.PackRequest(proto,request,session_id,message_id);
			this.TcpSocket.Send(package.data,package.size);
		}

		public void SendResponse(string proto,SprotoObject response,Int64 session) {
			Int64 message_id = this.gen_message_id();
			RpcPackage package = this.Proto.PackResponse(proto,response,session,message_id);
			this.TcpSocket.Send(package.data,package.size);
		}

		private Int64 gen_message_id() {
			this.message_id = this.message_id + 1;
			return this.message_id;
		}

		private void _OnMessage(TcpClientSocket tcpSocket,byte[] data,int size) {
			RpcMessage Message = this.Proto.UnpackMessage(data,size);
			string msg = String.Format("[{0}] op=OnMessage,proto={1},tag={2},ud={3},session={4},type={5},request={6},response={7}",
				this.TcpSocket.Name,Message.proto,Message.tag,Message.ud,Message.session,Message.type,Message.request,Message.response);
			this._Log(msg);
			if (Message.type == "response") {
				Int64 session = Message.session;
				ProtoDispatcher.MessageHandler handler = null;
				if (!this.sessions.TryGetValue(session,out handler)) {
					return;
				}
				handler(this,Message);
				return;
			}
			this.Dispatcher.Dispatch(this,Message);
		}

		private void _OnConnect(TcpClientSocket tcpSocket) {
			if (this.OnConnect != null) {
				this.OnConnect(this);
			}
		}

		private void _OnClose(TcpClientSocket tcpSocket) {
			if (this.OnClose != null) {
				this.OnClose(this);
			}
		}

		private void _Log(string msg) {
			if (this.Log != null) {
				this.Log(msg);
			}
		}

	}
}
