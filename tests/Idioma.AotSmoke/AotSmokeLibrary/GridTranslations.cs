using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl")]

namespace AotSmokeLibrary;

public partial class GridTranslationsEN : ITranslationSet
{
    public virtual string Title => "Grid";
    public virtual string Empty => "Nothing to show";
}

public class GridTranslationsNL : IGridTranslations
{
    public virtual string Title => "Raster";
    public virtual string Empty => "Niets te tonen";
}
