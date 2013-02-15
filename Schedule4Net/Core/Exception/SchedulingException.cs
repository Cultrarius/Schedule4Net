using System;
namespace Schedule4Net.Core.Exception
{
    [Serializable]
    public class SchedulingException : System.Exception
    {
        public SchedulingException(string msg)
            : base(msg)
        {
        }
    }
}
