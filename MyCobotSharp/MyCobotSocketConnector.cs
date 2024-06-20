using System.Net.Sockets;

namespace MyCobotSharp;

public static class MyCobotSocketConnector
{
    public static MyCobot Connect(string address, int port = 9000)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(address, port);
        var stream = new NetworkStream(socket, ownsSocket: true);
        return new MyCobot(stream, stream);
    }
}