using System;

namespace TestNet {
	public class Program {
		static void Main (string[] args) {
			//TestTcpClientSocket tester1 = new TestTcpClientSocket();
			//tester1.Run();

			TestSprotoTcpSocket tester2 = new TestSprotoTcpSocket();
			tester2.Run();
		}
	}
}
