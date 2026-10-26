using System;

namespace Tesserae.Lauschwerk.Sample
{
    /// <summary>
    /// Where a sample page sits in the sidebar: which group, in what order inside it, and the icon
    /// its entry carries.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SampleDetailsAttribute : Attribute
    {
        public string Group { get; set; }
        public int    Order { get; set; }
        public UIcons Icon  { get; set; }
    }
}
