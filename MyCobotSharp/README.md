# MyCobotSharp

This is an unofficial C# client for MyCobot™, based on a message processing structure with error tolerant.

Its control code is separated from the communication code, by passing specific I/O streams
(such as network stream from a TCP socket), more underlying communication method can be supported.

# How to Use

Use `MyCobotSocketConnector` or `MyCobotSerialPortConnector`:

```csharp

// Connect to MyCobot through TCP. Also, you can use MyCobotSerialPortConnector.
var mycobot = await MyCobotSocketConnector.Connect("YOUR_COBOT_IP");

// Use PullAngles() to read the angles into mycobot.Angles array.
await mycobot.PullAngles();

// Print current angle of joint #0.
Console.WriteLine(mycobot.Angles[0]);

mycobot.Angles[0] = 34.5;
mycobot.Angles[1] = 23.42;

// ...

// Ask MyCobot to change angles of its joints.
await mycobot.PushAngles();

```

Or, you can provide your established streams to connect to your MyCobot:

```csharp
var mycobot = await MyCobot.Connect(YOUR_INPUT_STREAM, YOUR_OUTPUT_STREAM);

// ...

```

# Remarks

MyCobot™ is a trademark owned by Elephant Robotics, Shenzhen, China.