using System;
using System.Collections.Generic;
using System.Text;

namespace Shared {

  class CanMessage {

    public UInt16 id;
    public UInt64 payload;
    public Int64 timeStamp;

    public CanMessage() { }
    public CanMessage(UInt16 Id, UInt64 Payload) {
      id = Id;
      payload = Payload;
      timeStamp = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
    }

  }
}
