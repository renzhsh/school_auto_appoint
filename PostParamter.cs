using System;
using System.Collections.Generic;
using System.Text;

namespace AutoAppointApp
{
    public class PostParamter : Dictionary<string, string>
    {
        public bool IsEmpty { get => this.Count == 0; }

        public new PostParamter Add(string key, string value)
        {
            base.Add(key, value);

            return this;
        }
    }
}
