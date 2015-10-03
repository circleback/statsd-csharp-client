using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsdClient
{

    [Serializable]
    public class MetricsDeliveryException : Exception
    {
        public string Metrics { get; private set; }
        public MetricsDeliveryException(string metrics, string message, Exception inner) : this(message, inner)
        {
            Metrics = metrics;
        }
        public MetricsDeliveryException() { }
        public MetricsDeliveryException(string message) : base(message) { }
        public MetricsDeliveryException(string message, Exception inner) : base(message, inner) { }
        protected MetricsDeliveryException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }
}
