using System.Windows;
using System.Windows.Media;
using TaskbarVisu.Rendering;

namespace TaskbarVisu.UI;

/// <summary>
/// Texte en matrice de points 5×7, la police des modules (<see cref="PixelFont"/>), comme sur un afficheur :
/// les points allumés dans la couleur donnée, les points éteints de la matrice à peine visibles derrière.
/// Sert aux titres des pages et au nom de l'application ; le reste de la fenêtre garde la police de Windows.
/// </summary>
internal sealed class DotMatrixText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(DotMatrixText),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LitProperty = DependencyProperty.Register(
        nameof(Lit), typeof(Brush), typeof(DotMatrixText),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Taille d'un point de la matrice, en DIP.</summary>
    public double Dot { get; init; } = 3;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush Lit
    {
        get => (Brush)GetValue(LitProperty);
        set => SetValue(LitProperty, value);
    }

    public DotMatrixText()
    {
        SnapsToDevicePixels = true;
        // Les lecteurs d'écran lisent le texte, pas les points.
        System.Windows.Automation.AutomationProperties.SetName(this, string.Empty);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TextProperty) System.Windows.Automation.AutomationProperties.SetName(this, Text);
    }

    /// <summary>Colonnes de la matrice : chaque caractère, puis une colonne d'espace.</summary>
    private int Columns()
    {
        int columns = 0;
        foreach (char c in Text)
            if (PixelFont.Pattern(c) is { } bits) columns += bits.GetLength(1) + 1;
        return Math.Max(0, columns - 1);
    }

    protected override Size MeasureOverride(Size available) => new(Columns() * Dot, PixelFont.GlyphHeight * Dot);

    protected override void OnRender(DrawingContext dc)
    {
        // Un point plus petit que sa case : la grille de l'afficheur reste lisible entre les points.
        double gap = Dot >= 3 ? Math.Round(Dot / 4, 1) : 0;
        double size = Dot - gap;
        var lit = Lit;
        var unlit = lit.Clone();
        unlit.Opacity = 0.09;
        unlit.Freeze();

        double x = 0;
        int index = 0;
        foreach (char c in Text)
        {
            if (PixelFont.Pattern(c) is not { } bits) continue;
            int width = bits.GetLength(1);
            // La colonne d'espace entre deux caractères fait partie de la matrice : elle a ses points éteints.
            int cells = index < Text.Length - 1 ? width + 1 : width;
            for (int col = 0; col < cells; col++)
                for (int row = 0; row < PixelFont.GlyphHeight; row++)
                {
                    bool on = col < width && bits[row, col];
                    dc.DrawRectangle(on ? lit : unlit, null, new Rect(x + col * Dot, row * Dot, size, size));
                }
            x += (width + 1) * Dot;
            index++;
        }
    }
}
