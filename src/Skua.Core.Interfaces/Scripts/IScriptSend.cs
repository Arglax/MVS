namespace Skua.Core.Interfaces;
public interface IScriptSend
{
void Packet(string packet, string type = "String");
void ClientPacket(string packet, string type = "str");
void ClientEvent(string message, string sentBy = "Skua");
void ClientGuild(string message, string sentBy = "Skua");
void ClientModerator(string message, string sentBy = "Skua");
void ClientServer(string message, string sentBy = "Skua");
void ClientWarning(string message, string sentBy = "Skua");
void ClientWhisper(string message, string sentBy = "Skua");
void ClientZone(string message, string sentBy = "Skua");
void Whisper(string name, string message);
Task ClientPacketSpam(string packet, string type, int delay, CancellationToken token);
Task PacketSpam(string packet, string type, int delay, CancellationToken token);
}
