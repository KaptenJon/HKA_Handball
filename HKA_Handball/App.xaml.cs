namespace HKA_Handball
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Start the game page directly
            return new Window(new GamePage()) { Title = "HKA Handball" };
        }
    }
}
