using System.IO.Ports;

namespace MyCobotSharp;

public static class MyCobotSerialPortConnector
{
    public static Task<MyCobot> Connect(string port, int baud = 115200)
    {
        var connection = new SerialPort(port, baud)
        {
            RtsEnable = true,
            DtrEnable = true
        };
        connection.Open();
        return MyCobot.Connect(connection.BaseStream, connection.BaseStream);
    }
}