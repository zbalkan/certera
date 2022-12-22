using System;
using System.Collections.Generic;

namespace Certera.Integrations.Notification
{
    public class NotificationException : AggregateException
    {
        public NotificationException() : base()
        {
        }

        public NotificationException(IEnumerable<Exception> innerExceptions) : base(innerExceptions)
        {
        }

        public NotificationException(params Exception[] innerExceptions) : base(innerExceptions)
        {
        }

        public NotificationException(string message) : base(message)
        {
        }

        public NotificationException(string message, IEnumerable<Exception> innerExceptions) : base(message, innerExceptions)
        {
        }

        public NotificationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public NotificationException(string message, params Exception[] innerExceptions) : base(message, innerExceptions)
        {
        }
    }
}
