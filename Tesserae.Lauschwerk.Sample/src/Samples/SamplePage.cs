using System;
using System.Linq;
using System.Threading.Tasks;

namespace Tesserae.Lauschwerk.Sample
{
    /// <summary>
    /// One entry in the sidebar and the page it opens. Built from the reflected sample type, so the
    /// class name is what names the page and the route.
    /// </summary>
    internal class SamplePage
    {
        public string                 Type             { get; }
        public string                 Name             { get; }
        public string                 Group            { get; }
        public int                    Order            { get; }
        public UIcons                 Icon             { get; }
        public Func<Task<IComponent>> ContentGenerator { get; }

        public SamplePage(string type, string name, string group, int order, UIcons icon, Func<Task<IComponent>> contentGenerator)
        {
            Type             = type;
            Name             = name;
            Group            = group;
            Order            = order;
            Icon             = icon;
            ContentGenerator = contentGenerator;
        }

        /// <summary>"CompletionAndHoverSample" reads as "Completion and Hover".</summary>
        public static string FormatName(Type sampleType) => FormatName(sampleType.Name);

        public static string FormatName(string sampleType)
        {
            return string.Join("", sampleType.Replace("Sample", "").Select(c => char.IsUpper(c) ? " " + c : "" + c))
               .Trim()
               .Replace(" And ", " and ");
        }
    }
}
