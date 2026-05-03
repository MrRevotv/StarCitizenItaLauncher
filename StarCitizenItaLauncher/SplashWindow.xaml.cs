using System.Threading.Tasks;
using System.Windows;

namespace StarCitizenItaLauncher
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            AvviaCaricamento();
        }

        private async void AvviaCaricamento()
        {
            // Aspetta 2.5 secondi per farti godere la grafica
            await Task.Delay(2500);

            // Apre la finestra principale
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            // Chiude lo splash screen
            this.Close();
        }
    }
}