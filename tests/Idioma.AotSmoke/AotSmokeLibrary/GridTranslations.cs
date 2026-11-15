using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl")]

namespace AotSmokeLibrary;

public partial class GridTranslationsEN : ITranslationSet
{
    public virtual string Title => "Grid";
    public virtual string Empty => "Nothing to show";
    public virtual string Rows(int count) => count is 1 ? "1 row" : $"{count} rows";
}

public class GridTranslationsNL : IGridTranslations
{
    public virtual string Title => "Raster";
    public virtual string Empty => "Niets te tonen";
    public virtual string Rows(int count) => count is 1 ? "1 rij" : $"{count} rijen";
}
