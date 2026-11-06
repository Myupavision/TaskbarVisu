using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TaskbarVisu.Core;
using TaskbarVisu.Modules;

namespace TaskbarVisu.UI;

/// <summary>
/// Modules à ajouter (page Ma barre) : ceux qui ne sont pas sur la barre, rangés par catégorie, avec un aperçu
/// en direct de chaque module sur le son du moment (rendu par le thread de rendu, à l'échelle de la vraie barre).
/// </summary>
public partial class SettingsWindow
{
    /// <summary>Largeur maximale d'un aperçu (DIP) : il tient dans une carte de la grille.</summary>
    private const int PreviewMaxWidth = 196;

    private sealed class PreviewView
    {
        public required string Id { get; init; }
        public required int WidthDip { get; init; }
        public required Image Image { get; init; }
        public WriteableBitmap? Bitmap;
        public int Version = -1;
    }

    // Aperçus de la page Ma barre (modules à ajouter) et de la page Style (barre en direct).
    private readonly List<PreviewView> _layoutPreviews = new(), _appearancePreviews = new();
    private DispatcherTimer? _previewTimer;
    private StackPanel? _galleryPanel;

    private void BuildGallery(Panel p)
    {
        p.Children.Add(Section("Ajouter un module"));
        var intro = Description(Loc.T("En direct sur le son du moment. Ajoutez-le à gauche ou à droite de la barre."));
        intro.Margin = new Thickness(2, -4, 0, 4);
        p.Children.Add(intro);
        _galleryPanel = new StackPanel();
        p.Children.Add(_galleryPanel);
        FillGallery();
    }

    private void FillGallery()
    {
        var p = _galleryPanel;
        if (p is null) return;
        p.Children.Clear();
        _layoutPreviews.Clear();
        bool centre = CentreMode(MiniBar());

        if (S.AllModules.All(c => c.Enabled))
            p.Children.Add(Card("Tous les modules sont sur la barre.", null));
        foreach (var category in ModuleCatalog.Categories)
        {
            var members = category.Ids
                .Select(id => (Info: ModuleCatalog.Find(id), Cfg: S.AllModules.FirstOrDefault(c => c.Id == id)))
                .Where(x => x.Info is not null && x.Cfg is not null && !x.Cfg.Enabled)
                .ToList();
            if (members.Count == 0) continue;

            var title = new TextBlock { Text = Loc.T(category.Name), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 14, 0, 0) };
            title.ToolTip = Loc.T(category.Blurb);
            p.Children.Add(title);
            var spacer = new Border { Height = 8 };
            p.Children.Add(spacer);

            var grid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, -8, 0) };
            foreach (var (info, cfg) in members) grid.Children.Add(GalleryCard(info!, cfg!, centre));
            // Cases vides : les cartes gardent la même largeur d'une catégorie à l'autre.
            for (int i = members.Count; i % 3 != 0; i++) grid.Children.Add(new Border());
            p.Children.Add(grid);
        }
        RequestPreviews();
    }

    private Border GalleryCard(ModuleInfo info, ModuleConfig cfg, bool centre)
    {
        // Aperçu : la largeur du module sur la barre (sans dépasser la carte), sur le fond de la barre.
        int widthDip = Math.Clamp(cfg.Width, info.MinWidth, PreviewMaxWidth);
        var image = new Image { Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        _layoutPreviews.Add(new PreviewView { Id = info.Id, WidthDip = widthDip, Image = image });
        bool light = App.TaskbarIsLight;
        var screen = new Border
        {
            Height = PreviewHeightDip(), CornerRadius = new CornerRadius(4), ClipToBounds = true,
            Background = new SolidColorBrush(light ? Color.FromRgb(0xEE, 0xEE, 0xF0) : Color.FromRgb(0x1C, 0x1C, 0x20)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)), BorderThickness = new Thickness(1),
            Child = image,
        };

        var icon = ModuleIcon(cfg, 14);
        icon.Margin = new Thickness(0, 0, 8, 0);
        var name = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0),
            Children = { icon, new TextBlock { Text = Loc.T(info.Name), FontWeight = FontWeights.SemiBold, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center } },
        };
        var about = Description(Loc.T(info.Description));
        about.TextTrimming = TextTrimming.WordEllipsis;
        about.Height = 32;
        about.Margin = new Thickness(0, 3, 0, 0);

        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        {
            void AddTo(string text, List<ModuleConfig> target)
            {
                var b = new Button { Content = Loc.T(text), Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(10, 3, 10, 3) };
                b.Click += (_, _) =>
                {
                    cfg.Enabled = true;
                    Place(cfg, target, null);
                    _selected = cfg;
                    Changed();
                    BuildLayout();
                };
                buttons.Children.Add(b);
            }
            if (centre) AddTo("+ Ajouter", S.Right);
            else
            {
                AddTo("+ Gauche", S.Left);
                AddTo("+ Droite", S.Right);
            }
        }

        var card = CardFrame(new StackPanel { Children = { screen, name, about, buttons } }, new Thickness(10, 10, 10, 10));
        card.Margin = new Thickness(0, 0, 8, 8);
        card.ToolTip = Loc.T(info.Description);
        return card;
    }

    /// <summary>Hauteur d'un aperçu : celle de la vraie barre, en DIP de cette fenêtre.</summary>
    private double PreviewHeightDip()
    {
        var bar = _app.Tracker.Current;
        double px = bar.Valid && bar.Taskbar.Height > 0 ? bar.Taskbar.Height : 48;
        return Math.Round(px / UiScale()) + 2;
    }

    private double UiScale()
    {
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        return scale > 0 ? scale : 1;
    }

    /// <summary>Icône d'un module, peinte à ses couleurs (palette générale, sa palette, ou sa couleur unie).</summary>
    private TextBlock ModuleIcon(ModuleConfig cfg, double size)
    {
        var icon = Glyph(ModuleCatalog.Icon(cfg.Id), size);
        icon.Foreground = cfg.Colors == ModuleColorMode.Solid
            ? new SolidColorBrush(ToColor(cfg.ColorSolid))
            : PaletteBrush(cfg.Colors == ModuleColorMode.Palette ? cfg.ColorPalette : S.Palette,
                cfg.Colors == ModuleColorMode.Palette ? cfg.Gradient : S.Gradient);
        return icon;
    }

    /// <summary>
    /// Page Style : les modules de la barre, en direct et dans l'ordre, sur le fond de la barre.
    /// Chaque réglage de couleur ou d'effet s'y voit tout de suite.
    /// </summary>
    private FrameworkElement LiveBar()
    {
        _appearancePreviews.Clear();
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var cfg in S.Left.Concat(S.Right).Where(c => c.Enabled))
        {
            var info = ModuleCatalog.Find(cfg.Id);
            if (info is null) continue;
            var image = new Image { Stretch = Stretch.None, VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            if (row.Children.Count > 0) image.Margin = new Thickness(Math.Max(2, S.Spacing), 0, 0, 0);
            _appearancePreviews.Add(new PreviewView { Id = cfg.Id, WidthDip = Math.Max(cfg.Width, info.MinWidth), Image = image });
            row.Children.Add(image);
        }

        FrameworkElement content = row;
        if (row.Children.Count == 0)
        {
            var empty = Description(Loc.T("Aucun module affiché : ajoutez-en depuis la page Ma barre."));
            empty.Margin = new Thickness(12, 0, 12, 0);
            empty.VerticalAlignment = VerticalAlignment.Center;
            content = empty;
        }
        bool light = App.TaskbarIsLight;
        var strip = new Border
        {
            Height = PreviewHeightDip() + 6, CornerRadius = new CornerRadius(6), ClipToBounds = true, Margin = new Thickness(0, 10, 0, 2),
            Background = new SolidColorBrush(light ? Color.FromRgb(0xEE, 0xEE, 0xF0) : Color.FromRgb(0x1C, 0x1C, 0x20)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0x80, 0x80, 0x80)), BorderThickness = new Thickness(1),
            Child = content,
        };
        var caption = Description(Loc.T("Votre barre, en direct : chaque réglage s'y voit tout de suite."));
        caption.Margin = new Thickness(2, 2, 0, 0);
        return new StackPanel { Children = { strip, caption } };
    }

    // ------------------------------------------------------------ Aperçus en direct

    /// <summary>Les aperçus ne tournent que si leur page (Ma barre ou Style) est à l'écran.</summary>
    private bool PreviewsWanted() =>
        IsVisible && WindowState != WindowState.Minimized && _welcomeStep < 0 && CurrentPreviews().Count > 0;

    /// <summary>Aperçus de la page affichée (aucun pour les autres pages).</summary>
    private List<PreviewView> CurrentPreviews() =>
        Tabs.SelectedItem == TabBar ? _layoutPreviews : Tabs.SelectedItem == TabStyle ? _appearancePreviews : new List<PreviewView>();

    private void RequestPreviews()
    {
        var host = _app.PreviewHost;
        if (host is null) return;
        if (!PreviewsWanted())
        {
            host.SetPreviews(null);
            _previewTimer?.Stop();
            return;
        }
        // Galerie : démo stéréo pour l'imageur et l'oscilloscope XY si le son du moment est mono.
        host.SetPreviews(CurrentPreviews().Select(v => (v.Id, v.WidthDip)).ToArray(), demo: Tabs.SelectedItem == TabBar);
        if (_previewTimer is null)
        {
            _previewTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
            _previewTimer.Tick += (_, _) => CopyPreviews();
        }
        _previewTimer.Start();
    }

    private void StopPreviews()
    {
        _previewTimer?.Stop();
        _app.PreviewHost?.SetPreviews(null);
    }

    /// <summary>Copie les dernières images rendues dans les aperçus de la galerie.</summary>
    private void CopyPreviews()
    {
        var host = _app.PreviewHost;
        if (host is null) return;
        double dpi = 96 * UiScale();
        foreach (var view in CurrentPreviews())
        {
            var frame = host.GetPreview(view.Id);
            if (frame is null) continue;
            lock (frame.Gate)
            {
                if (frame.Version == view.Version || frame.Width == 0) continue;
                view.Version = frame.Version;
                if (view.Bitmap is null || view.Bitmap.PixelWidth != frame.Width || view.Bitmap.PixelHeight != frame.Height)
                {
                    view.Bitmap = new WriteableBitmap(frame.Width, frame.Height, dpi, dpi, PixelFormats.Pbgra32, null);
                    view.Image.Source = view.Bitmap;
                }
                view.Bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Pixels, frame.Width * 4, 0);
            }
        }
    }
}
