using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CrosshairStudio;

public partial class LauncherWindow : Window
{
    public LauncherWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LauncherRoot.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

            var widthPulse = new DoubleAnimation(30, 42, TimeSpan.FromMilliseconds(900))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            var heightPulse = new DoubleAnimation(30, 42, TimeSpan.FromMilliseconds(900))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            LauncherMark.BeginAnimation(FrameworkElement.WidthProperty, widthPulse);
            LauncherMark.BeginAnimation(FrameworkElement.HeightProperty, heightPulse);
        };
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        var main = new MainWindow();
        main.Show();
        Close();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
