using System.Buffers.Binary;
using System.Text;
using UnrealEditorBridge.Protocol;
for (var length = 0; length < EventSlotLayout.RecordHeaderSize; length++) {
    Check.That(!EventRecordParser.IsValidSlot(new byte[length]), "Truncated slot is invalid");
    Check.That(EventRecordParser.ReadPayloadJson(new byte[length]) == "", "Truncated payload is empty");
}
var payload = Encoding.UTF8.GetBytes("{\"name\":\"에셋\"}");
var slot = new byte[EventSlotLayout.PayloadOffset + payload.Length];
BinaryPrimitives.WriteUInt64LittleEndian(slot, 7);
BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(8), 1);
BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(12), (uint)payload.Length);
payload.CopyTo(slot, EventSlotLayout.PayloadOffset);
Check.That(EventRecordParser.IsValidSlot(slot), "Valid slot");
Check.That(EventRecordParser.ReadPayloadJson(slot) == Encoding.UTF8.GetString(payload), "UTF8 payload");
BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(12), (uint)payload.Length + 10);
Check.That(EventRecordParser.ReadPayloadJson(slot) == Encoding.UTF8.GetString(payload), "Existing truncation contract");
BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(12), 0);
Check.That(EventRecordParser.ReadPayloadJson(slot) == "", "Empty payload");
await Check.ThrowsAsync<ArgumentException>(() => Task.Run(() => HeaderParser.Parse(Array.Empty<byte>())), "Invalid header");
Console.WriteLine($"PASS {Check.Count} protocol regression checks");
