using System;
using System.Linq;
using static Tesserae.UI;

namespace Tesserae.Lauschwerk
{
    /// <summary>A dropdown of the seven languages Spot hears typed words in.</summary>
    internal static class LanguagePicker
    {
        internal static Dropdown Create(SpeechLanguage selected)
        {
            var items = Enum.GetValues(typeof(SpeechLanguage)).Cast<SpeechLanguage>()
               .Select(l => DropdownItem(l.ToString(), l.ToString()).SelectedIf(l == selected))
               .ToArray();

            return Dropdown().Items(items).W(140.px());
        }

        internal static SpeechLanguage Read(Dropdown dropdown)
        {
            var key = dropdown.SelectedItems.Length > 0 ? dropdown.SelectedItems[0].Text : null;
            return Enum.TryParse<SpeechLanguage>(key, out var language) ? language : SpeechLanguage.English;
        }
    }
}
