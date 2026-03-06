using HKA_Handball.Services;
using Plugin.Maui.Audio;

namespace HKA_Handball
{
    public partial class App : Application
    {
        public App(SoundManager soundManager)
        {
            InitializeComponent();
            _soundManager = soundManager;
        }

        readonly SoundManager _soundManager;

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new NavigationPage(new MainMenuPage(_soundManager))
            {
                BarBackgroundColor = Color.FromArgb("#2C1B0E"),
                BarTextColor = Colors.White
            })
            { Title = "HKA Handball" };
        }
    }
}
