namespace MyCobotSharp;

public enum MyCobotCommand : byte
{
    Header = 0xFE,
    Footer = 0xFA,
    IsPowerOn = 0x12,
    GetAngles = 0x20,
    SendAngles = 0x22,
    GetCoords = 0x23,
    SendCoords = 0x25,
    IsMoving = 0x2B,
}