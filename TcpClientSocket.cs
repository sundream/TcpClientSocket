using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Net {
	public class TcpClientSocket {
		private class Package {
			public byte[] data = null;
			public int size = 0;
			public int send_pos = 0;

			public Package (byte[] data,int size,int send_pos=0) {
				this.data = data;
				this.size = size;
				this.send_pos = send_pos;
			}
		}

		public delegate void OnConnectCallback (TcpClientSocket socket);
		public delegate void OnCloseCallback (TcpClientSocket socket);
		public delegate void OnMessageCallback (TcpClientSocket socket,byte[] bytes,int size);
		public delegate void LogType (string msg);
		private Socket socket;
		private Thread recvThread = null;
		private Thread sendThread = null;
		private OnConnectCallback onconnect;
		private OnCloseCallback onclose;
		private OnMessageCallback onmessage;
		private LogType log;

		private string name;
		private int timeout;
		private int send_hz;
		private int header_len;
		private bool big_endian;   // header_len is encode big_endian?

		private Queue<Package> recvQueue =  new Queue<Package>();
		private Queue<Package> sendQueue = new Queue<Package>();
		private ByteStream reader = new ByteStream();

		public TcpClientSocket (string name=null,int header_len=2,bool big_endian=true,int timeout=50,int send_hz=5) {
			this.name = name;
			// every 'timeout' millisecond send 'send_hz' package
			this.timeout = timeout;
			this.send_hz = send_hz;
			this.header_len = header_len;
			this.big_endian = big_endian;
			
			// sync socket + thread to send/receive
			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		}

		public void Register(OnConnectCallback onconnect,OnCloseCallback onclose,OnMessageCallback onmessage,LogType log=null) {
			this.onconnect = onconnect;
			this.onclose = onclose;
			this.onmessage = onmessage;
			this.log = log;
			if (this.log == null)
				this.log = Console.WriteLine;
		}

		public void Connect (string host,int port) {
			if (this.Connected) {
				return;
			}
			if (this.name == null) {
				this.name = String.Format("{0}:{1}",host,port);
			}
			try {
				this.socket.BeginConnect(host,port,new AsyncCallback(this.OnConnect),null);
			} catch (Exception e) {
				this.Log(String.Format("[{0}] op=ConnectError,error={1}",this.name,e.ToString()));
			}
		}

		public void Disconnect () {
			if (this.Connected) {
				try {
					this.socket.Close();
				} catch (Exception e) {
					this.Log(String.Format("[{0}] op=CloseError,error={1}",this.name,e.ToString()));
				}
				this.OnClose();
			}
		}

		public void Close () {
			this.Disconnect();
		}

		public void Send (byte[] data,int size=0) {
			if (size == 0) {
				size = data.Length;
			}
			if (!this.Connected) {
				this.Log(String.Format("[{0}] op=Send to a Closed Socket,size={1}",this.name,size));
				return;
			}
			byte[] send_data = new byte[this.header_len+size];
			this.encode_message_size(size,send_data);
			for (int i = 0; i < size; i++) {
				send_data[this.header_len+i] = data[i];
			}
			size = this.header_len + size;
			this.sendQueue.Enqueue(new Package(send_data,size));
		}

		// call this to dispatch receive message
		public void Dispatch(int recv_hz=5) {
			//this.Log(String.Format("[{0}] op=Dispatch,connected={1},recvQueue={2},sendQueue={3}",this.name,this.Connected,this.recvQueue.Count,this.sendQueue.Count));
			int recv_limit = recv_hz;
			while (this.recvQueue.Count > 0) {
				Package package = this.recvQueue.Dequeue();
				try {
					this.OnMessage(package.data,package.size);
				} catch (Exception e) {
					this.Log(String.Format("[{0}] op=OnMessageError,size={1},error={2}",this.name,package.size,e.ToString()));
				}
				recv_limit = recv_limit - 1;
				if (recv_limit <= 0)
					break;
			}
		}

		public bool Connected {
			get {
				return (this.socket != null) && this.socket.Connected;
			}
		}

		public string Name {
			get {
				return this.name;
			}
			set {
				this.name = value;
			}
		}

		private void SendThreadLoop() {
			while (this.Connected) {
				Thread.Sleep(this.timeout);
				// double check
				if (!this.Connected)
					break;
				// send message
				int send_limit = this.send_hz;
				while (this.sendQueue.Count > 0) {
					Package package = this.sendQueue.Peek();
					int send_bytes = 0;
					try {
						send_bytes = this.socket.Send(package.data,package.send_pos,package.size-package.send_pos,SocketFlags.None);
					} catch (Exception e) {
						this.Log(String.Format("[{0}] op=SendError,size={1},error={2}",this.name,package.size,e.ToString()));
					}
					package.send_pos = package.send_pos + send_bytes;
					if (package.send_pos < package.size)
						break;
					this.sendQueue.Dequeue();
					send_limit = send_limit - 1;
					if (send_limit <= 0)
						break;
				}
			}
		}

		private void ReceiveThreadLoop() {
			while (this.Connected) {
				Thread.Sleep(this.timeout);
				// double check
				if (!this.Connected)
					break;
				// receive message
				int max_recv_bytes = 8192;
				int recv_bytes = -1;
				SocketError errcode = SocketError.Success;
				try {
					// try expand
					this.reader.Expand(max_recv_bytes);
					recv_bytes = this.socket.Receive(this.reader.Buffer,this.reader.Position,max_recv_bytes,SocketFlags.None,out errcode);
					if (recv_bytes == 0) {
						this.Close();
					}
				} catch (Exception e) {
					this.Log(String.Format("[{0}] op=RecvError,error={1}",this.name,e.ToString()));
					if (errcode == SocketError.Shutdown || errcode == SocketError.HostDown) {
						this.Close();
					}
				}
				if (recv_bytes > 0) {
					this.reader.Seek(this.reader.Position+recv_bytes,ByteStream.SEEK_BEGIN);
				}
				int pos = 0;
				int len = this.reader.Position;
				int unread_len = len - pos;
				while (unread_len >= this.header_len) {
					int message_size = this.decode_message_size(this.reader.Buffer,pos);
					if (unread_len >= message_size + this.header_len) {
						byte[] data = new byte[message_size];
						for (int i=0; i < message_size; i++) {
							data[i] = this.reader.Buffer[i+pos+this.header_len];
						}
						pos = pos + this.header_len + message_size;
						unread_len = len - pos;
						this.recvQueue.Enqueue(new Package(data,message_size));
					} else {
						break;
					}
				}
				if (unread_len > 0) {
					if (pos != 0) {
						// move data to start,avoid stream expand too big
						this.reader.Seek(0,ByteStream.SEEK_BEGIN);
						this.reader.Write(this.reader.Buffer,pos,unread_len);
					} else {
						// recv a big message,cache it!
					}
				} else if (pos > 0) {
					// recv a unbroken message
					this.reader.Seek(0,ByteStream.SEEK_BEGIN);
				}
			}
		}

		private int decode_message_size(byte[] buffer,int pos=0) {
			int len = this.header_len;
			int size = 0;
			if (this.big_endian) {
				for (int i = 0; i < len; i++) {
					int offset = 8 * (len-i-1);
					size = size | (buffer[pos+i] << offset);
				}
			} else {
				for (int i = 0; i < len; i++) {
					int offset = 8 * i;
					size = size | (buffer[pos+i] << offset);
				}
			}
			return size;
		}

		private byte[] encode_message_size(int size,byte[] data=null) {
			int len = this.header_len;
			if (data == null)
				data = new byte[len];
			if (this.big_endian) {
				for (int i = 0; i < len; i++) {
					int offset = 8 * (len-i-1);
					byte b = (byte)((size >> offset) & 0xff);
					data[i] = b;
				}
			} else {
				for (int i = 0; i < len; i++) {
					int offset = 8 * i;
					byte b = (byte)((size >> offset) & 0xff);
					data[i] = b;
				}
			}
			return data;
		}

		private void OnConnect(IAsyncResult iar) {
			try {
				this.socket.EndConnect(iar);
				this._OnConnect();
			} catch (Exception e) {
				this.Log(String.Format("[{0}] op=ConnectError,error={1}",this.name,e.ToString()));
			}
		}

		private void _OnConnect() {
			this.Log(String.Format("[{0}] op=OnConnect",this.name));
			if (this.recvThread != null) {
				this.recvThread.Abort ();
			}
			if (this.sendThread != null) {
				this.sendThread.Abort ();
			}
			this.recvThread = new Thread(this.ReceiveThreadLoop);
			this.sendThread = new Thread(this.SendThreadLoop);
			this.recvThread.Start();
			this.sendThread.Start();
			if (this.onconnect != null)
				this.onconnect(this);
		}

		private void OnClose() {
			this.Log(String.Format("[{0}] op=OnClose",this.name));
			//this.recvThread.Abort();
			//this.sendThread.Abort();
			if (this.onclose != null)
				this.onclose(this);
		}

		private void OnMessage(byte[] data,int size) {
			//this.Log(String.Format("[{0}] op=OnMessage,size={1}",this.name,size));
			if (this.onmessage != null)
				this.onmessage(this,data,size);
		}

		private void Log(String msg) {
			if (this.log != null) {
				this.log(msg);
			}
		}
	}
}
