using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsdClient
{

    [Serializable]
    public class MessageNotDeliveredException : Exception
    {
        public string Metrics { get; private set; }
        public MessageNotDeliveredException(string metrics, string message, Exception inner) : this(message, inner)
        {
            Metrics = metrics;
        }
        public MessageNotDeliveredException() { }
        public MessageNotDeliveredException(string message) : base(message) { }
        public MessageNotDeliveredException(string message, Exception inner) : base(message, inner) { }
        protected MessageNotDeliveredException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }
}
