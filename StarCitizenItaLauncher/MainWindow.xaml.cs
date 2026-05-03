using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace StarCitizenItaLauncher
{
    public partial class MainWindow : Window
    {
        private const string RepoOwner = "MrRevotv";
        private const string RepoName = "AUTOINSTALLER-Traduzione-italiana-Star-Citizen-V2.0";
        private const string FilePath = "global.ini";

        private string _gameDirectory = "";
        private AppConfig _config;
        private string _latestCommitDateOnline = "";
        private bool _isSilentMode = false;

        public MainWindow()
        {
            InitializeComponent();
            _config = ConfigManager.LeggiConfig();

            if (_config.ChannelsWithCustom == null) _config.ChannelsWithCustom = new List<string>();
            if (_config.LastUpdates == null) _config.LastUpdates = new Dictionary<string, string>();

            ChkAutoStart.IsChecked = _config.StartWithWindows;
            ChkBackground.IsChecked = _config.RunInBackground;
            ChkAllowCustom.IsChecked = _config.AllowCustomFile;

            AggiornaVisibilitaCustom();

            VerificaPercorsiECanali();
            SelezionaCanaleLive();

            string[] args = Environment.GetCommandLineArgs();
            if (args.Contains("-silent"))
            {
                _isSilentMode = true;
                this.WindowState = WindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Visibility = Visibility.Hidden;
            }

            int attesa = _isSilentMode ? 20000 : 500;

            Task.Delay(attesa).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() => AvviaProcessoDiControllo());
            });
        }

        // --- NUOVO METODO: Forza la finestra a comparire in primo piano ---
        // Importiamo l'API nativa di Windows per forzare il focus
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        // --- NUOVO METODO AGGIORNATO ---
        private async void RisvegliaWindow()
        {
            if (!_isSilentMode) return;

            // Ci assicuriamo di eseguire tutto sul thread principale dell'interfaccia grafica
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                this.Show();
                this.Visibility = Visibility.Visible;
                this.ShowInTaskbar = true;

                // Se era minimizzata, la riportiamo normale
                if (this.WindowState == WindowState.Minimized)
                    this.WindowState = WindowState.Normal;

                // Forziamo in primo piano
                this.Topmost = true;
                this.Activate();
                this.Focus();

                // Usiamo l'API di Windows per bypassare il blocco "anti-focus-stealing"
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                SetForegroundWindow(hwnd);

                // Aspettiamo mezzo secondo per dare tempo a Windows di renderizzare la finestra sopra le altre
                await Task.Delay(500);

                // Rimuoviamo il blocco per non dare fastidio all'utente mentre la usa
                this.Topmost = false;

                _isSilentMode = false;
            });
        }

        private void SelezionaCanaleLive()
        {
            foreach (var item in ListaCanali.Items)
            {
                if (item.ToString()?.ToUpper() == "LIVE")
                {
                    ListaCanali.SelectedItem = item;
                    break;
                }
            }
        }

        private void VerificaPercorsiECanali()
        {
            ListaCanali.Items.Clear();

            if (string.IsNullOrEmpty(_config.BaseGameFolder) || !Directory.Exists(_config.BaseGameFolder))
            {
                return;
            }

            string[] canaliPossibili = { "LIVE", "PTU", "EPTU" };
            foreach (var canale in canaliPossibili)
            {
                if (Directory.Exists(Path.Combine(_config.BaseGameFolder, canale)))
                {
                    ListaCanali.Items.Add(canale);
                }
            }
        }

        private void AggiornaVisibilitaCustom()
        {
            BtnCustomLoad.Visibility = _config.AllowCustomFile ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void AvviaProcessoDiControllo()
        {
            await CercaCartellaGioco();

            if (string.IsNullOrEmpty(_config.BaseGameFolder))
            {
                if (_isSilentMode) { Application.Current.Shutdown(); return; }

                TxtStatus.Text = "CARTELLA NON TROVATA";
                TxtGamePath.Text = "Seleziona la cartella StarCitizen manualmente.";
                ImpostaBottone("SELEZIONA CARTELLA", "#FF007ACC");
                return;
            }

            await AggiornaStatoInterfaccia();
        }

        private bool CartellaBaseValida(string basePath)
        {
            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath)) return false;
            try
            {
                return Directory.GetDirectories(basePath).Any(dir =>
                {
                    string nome = new DirectoryInfo(dir).Name;
                    return nome == nome.ToUpper() && Directory.EnumerateFiles(dir, "*.p4k").Any();
                });
            }
            catch { return false; }
        }

        private async Task CercaCartellaGioco()
        {
            if (CartellaBaseValida(_config.BaseGameFolder))
            {
                _gameDirectory = _config.BaseGameFolder;
                TxtGamePath.Text = $"Percorso: {_gameDirectory}";
                PopolaMenuCanali();
                return;
            }

            string cartellaTrovata = await Task.Run(() =>
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    try
                    {
                        string path = Path.Combine(drive.Name, "Program Files", "Roberts Space Industries", "StarCitizen");
                        if (CartellaBaseValida(path)) return path;
                    }
                    catch { }
                }
                return string.Empty;
            });

            if (!string.IsNullOrEmpty(cartellaTrovata))
            {
                _gameDirectory = cartellaTrovata;
                _config.BaseGameFolder = cartellaTrovata;
                ConfigManager.SalvaConfig(_config);
                PopolaMenuCanali();
            }
            else
            {
                _config.BaseGameFolder = "";
            }
        }

        private void PopolaMenuCanali()
        {
            ListaCanali.Items.Clear();
            ListaCanali.Visibility = Visibility.Visible;

            try
            {
                foreach (string dir in Directory.GetDirectories(_config.BaseGameFolder))
                {
                    string nomeCanale = new DirectoryInfo(dir).Name;
                    if (nomeCanale == nomeCanale.ToUpper() && Directory.EnumerateFiles(dir, "*.p4k").Any())
                    {
                        ListaCanali.Items.Add(nomeCanale);
                    }
                }
            }
            catch { }

            if (ListaCanali.Items.Contains(_config.SelectedChannel))
                ListaCanali.SelectedItem = _config.SelectedChannel;
            else if (ListaCanali.Items.Count > 0)
                ListaCanali.SelectedIndex = 0;
        }

        private async Task ControllaAggiornamenti()
        {
            if (ListaCanali.SelectedItem == null) return;
            string canale = ListaCanali.SelectedItem.ToString()!;

            // Protocollo LIVE (Background)
            if (_isSilentMode && canale.ToUpper() != "LIVE")
            {
                Application.Current.Shutdown();
                return;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StarCitizenLauncher", "1.0"));

                    // Chiamata API GitHub per l'ultimo commit del file .ini
                    string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/commits?path={FilePath}&page=1&per_page=1";
                    string jsonResponse = await client.GetStringAsync(url);

                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        if (doc.RootElement.GetArrayLength() > 0)
                        {
                            // Data dell'ultimo commit su GitHub
                            string dataOnlineStr = doc.RootElement[0].GetProperty("commit").GetProperty("committer").GetProperty("date").GetString() ?? "";
                            DateTime dataOnline = DateTime.Parse(dataOnlineStr).ToLocalTime();
                            _latestCommitDateOnline = dataOnlineStr; // Salviamo per dopo

                            // Recupero data locale dal dizionario
                            _config.LastUpdates.TryGetValue(canale, out string? dataLocaleStr);

                            // Formattazione per la UI
                            TxtDataLocale.Text = string.IsNullOrEmpty(dataLocaleStr) ? "Mai" : DateTime.Parse(dataLocaleStr).ToLocalTime().ToString("g");
                            TxtDataOnline.Text = dataOnline.ToString("g");

                            // Controllo esistenza fisica del file
                            string percorsoCanale = Path.Combine(_config.BaseGameFolder, canale);
                            string fileTrad = Path.Combine(percorsoCanale, "data", "Localization", "italian_(italy)", "global.ini");
                            bool fileEsiste = File.Exists(fileTrad);

                            // LOGICA DI CONFRONTO
                            // Se il file non esiste OPPURE la data online è diversa da quella salvata
                            if (!fileEsiste || dataLocaleStr != dataOnlineStr)
                            {
                                TxtStatus.Text = !fileEsiste ? "TRADUZIONE MANCANTE" : "AGGIORNAMENTO DISPONIBILE";
                                ImpostaBottone("SCARICA AGGIORNAMENTO", "#FF28A745");

                                if (_isSilentMode)
                                {
                                    this.Visibility = Visibility.Visible;
                                    this.WindowState = WindowState.Normal;
                                    this.ShowInTaskbar = true;
                                    this.Activate();
                                }
                            }
                            else
                            {
                                if (_isSilentMode) Application.Current.Shutdown();
                                TxtStatus.Text = "SISTEMA AGGIORNATO";
                                ImpostaBottone("GIOCA", "#FF007ACC");
                            }
                        }
                    }
                }
            }
            catch
            {
                TxtStatus.Text = "ERRORE CONNESSIONE GITHUB";
                if (_isSilentMode) this.Visibility = Visibility.Visible;
            }
        }

        private async Task ScaricaEInstalla()
        {
            if (ListaCanali.SelectedItem == null) return;
            string canale = ListaCanali.SelectedItem.ToString()!;
            string percorsoCanale = Path.Combine(_config.BaseGameFolder, canale);

            try
            {
                BtnAction.IsEnabled = false;
                if (BtnCustomLoad.Visibility == Visibility.Visible) BtnCustomLoad.IsEnabled = false;
                ListaCanali.IsEnabled = false;
                PbDownload.Visibility = Visibility.Visible;
                PbDownload.Value = 0;
                TxtStatus.Text = "Download in corso...";

                string url = $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/main/{FilePath}";
                string dest = Path.Combine(percorsoCanale, "data", "Localization", "italian_(italy)", "global.ini");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                using (HttpClient client = new HttpClient())
                {
                    var data = await client.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(dest, data);
                }

                File.WriteAllText(Path.Combine(percorsoCanale, "user.cfg"), "g_language=italian_(italy)\ng_LanguageAudio=english\n");

                // --- AGGIORNAMENTO DATI E CONFIG ---
                _config.ChannelsWithCustom.Remove(canale);
                _config.LastUpdates[canale] = _latestCommitDateOnline; // Salviamo la data del commit online come data locale
                ConfigManager.SalvaConfig(_config);

                // --- AGGIORNAMENTO TESTO DATA NELL'INTERFACCIA ---
                if (DateTime.TryParse(_latestCommitDateOnline, out DateTime dt))
                {
                    TxtDataLocale.Text = dt.ToLocalTime().ToString("g"); // "g" sta per formato Generico (data e ora)
                }

                PbDownload.Visibility = Visibility.Hidden;
                ListaCanali.IsEnabled = true;
                if (BtnCustomLoad.Visibility == Visibility.Visible) BtnCustomLoad.IsEnabled = true;

                TxtStatus.Text = "INSTALLAZIONE COMPLETATA";

                // Questo metodo rimetterà il tastone su "GIOCA"
                await AggiornaStatoInterfaccia();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "ERRORE DURANTE IL DOWNLOAD";
                MessageBox.Show(ex.Message);
                PbDownload.Visibility = Visibility.Hidden;
                ListaCanali.IsEnabled = true;
                ImpostaBottone("RIPROVA", "#FFDC3545");
            }
        }

        private async void BtnCustomLoad_Click(object sender, RoutedEventArgs e)
        {
            if (ListaCanali.SelectedItem == null) return;
            var ofd = new OpenFileDialog { Filter = "File Traduzione (*.ini;*.p4k)|*.ini;*.p4k" };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string canale = ListaCanali.SelectedItem.ToString()!;
                    string percorsoCanale = Path.Combine(_config.BaseGameFolder, canale);
                    string dest = Path.Combine(percorsoCanale, "data", "Localization", "italian_(italy)", "global.ini");
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                    File.Copy(ofd.FileName, dest, true);
                    File.WriteAllText(Path.Combine(percorsoCanale, "user.cfg"), "g_language=italian_(italy)\ng_LanguageAudio=english\n");

                    if (!_config.ChannelsWithCustom.Contains(canale)) _config.ChannelsWithCustom.Add(canale);
                    _config.LastUpdates[canale] = "CUSTOM";
                    ConfigManager.SalvaConfig(_config);

                    TxtStatus.Text = "FILE CUSTOM CARICATO";
                    await AggiornaStatoInterfaccia();
                    MessageBox.Show("File custom installato correttamente!");
                }
                catch (Exception ex) { MessageBox.Show("Errore: " + ex.Message); }
            }
        }

        private async void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            string action = BtnAction.Content?.ToString() ?? "";

            if (action == "RIPRISTINA UFFICIALE")
            {
                RimuoviTraduzioneFisica();
                await AggiornaStatoInterfaccia();
            }
            // Usiamo .Contains così se il testo è "INSTALLA TRADUZIONE" o "SCARICA AGGIORNAMENTO" funziona in entrambi i casi
            else if (action.Contains("INSTALLA") || action.Contains("SCARICA") || action == "RIPROVA")
            {
                await ScaricaEInstalla();
            }
            else if (action.StartsWith("GIOCA"))
            {
                string rsi = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Roberts Space Industries", "RSI Launcher", "RSI Launcher.exe");
                if (File.Exists(rsi))
                {
                    Process.Start(rsi);
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show("Launcher RSI non trovato. Assicurati che Star Citizen sia installato nel percorso predefinito.");
                }
            }
            else if (action == "SELEZIONA CARTELLA")
            {
                var dialog = new OpenFolderDialog();
                if (dialog.ShowDialog() == true && CartellaBaseValida(dialog.FolderName))
                {
                    _config.BaseGameFolder = dialog.FolderName;
                    ConfigManager.SalvaConfig(_config);
                    AvviaProcessoDiControllo();
                }
            }
        }

        private async void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Vuoi rimuovere completamente la traduzione?", "Conferma", MessageBoxButton.YesNo);
            if (res == MessageBoxResult.Yes)
            {
                RimuoviTraduzioneFisica();
                await AggiornaStatoInterfaccia();
                MessageBox.Show("Traduzione rimossa correttamente.");
            }
        }

        private void BtnSfogliaCartella_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true && CartellaBaseValida(dialog.FolderName))
            {
                _config.BaseGameFolder = dialog.FolderName;
                ConfigManager.SalvaConfig(_config);
                TxtImpostazioniPercorso.Text = _config.BaseGameFolder;
                AvviaProcessoDiControllo();
            }
        }

        private void RimuoviTraduzioneFisica()
        {
            try
            {
                if (ListaCanali.SelectedItem == null) return;
                string canale = ListaCanali.SelectedItem.ToString()!;
                string percorsoCanale = Path.Combine(_config.BaseGameFolder, canale);

                string cartellaIta = Path.Combine(percorsoCanale, "data", "Localization", "italian_(italy)");
                if (Directory.Exists(cartellaIta)) Directory.Delete(cartellaIta, true);

                string cartellaLoc = Path.Combine(percorsoCanale, "data", "Localization");
                if (Directory.Exists(cartellaLoc) && !Directory.EnumerateFileSystemEntries(cartellaLoc).Any())
                {
                    Directory.Delete(cartellaLoc);
                }

                string userCfg = Path.Combine(percorsoCanale, "user.cfg");
                if (File.Exists(userCfg)) File.Delete(userCfg);

                _config.ChannelsWithCustom.Remove(canale);
                _config.LastUpdates.Remove(canale);
                ConfigManager.SalvaConfig(_config);
            }
            catch (Exception ex) { MessageBox.Show("Errore rimozione: " + ex.Message); }
        }

        private async Task AggiornaStatoInterfaccia()
        {
            // Tappato buco logico: se la lista è vuota, in modalità silent deve chiudersi, altrimenti rimane vivo in eterno
            if (ListaCanali.SelectedItem == null)
            {
                if (_isSilentMode) Application.Current.Shutdown();
                return;
            }

            string canale = ListaCanali.SelectedItem.ToString()!;
            string fileTrad = Path.Combine(_config.BaseGameFolder, canale, "data", "Localization", "italian_(italy)", "global.ini");
            bool fileEsiste = File.Exists(fileTrad);

            bool isCurrentCustom = _config.ChannelsWithCustom.Contains(canale);

            BtnRemove.Visibility = (fileEsiste && !isCurrentCustom) ? Visibility.Visible : Visibility.Collapsed;

            if (isCurrentCustom && fileEsiste)
            {
                TxtStatus.Text = "RILEVATO FILE CUSTOM";
                ImpostaBottone("RIPRISTINA UFFICIALE", "#FFCC8800");

                // Tappato buco logico: Se ha un file custom non possiamo fargli auto-update, chiudiamo il processo per non lasciarlo appeso.
                if (_isSilentMode) Application.Current.Shutdown();
            }
            else
            {
                await ControllaAggiornamenti();
            }
        }

        private async void ListaCanali_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaCanali.SelectedItem != null)
            {
                _config.SelectedChannel = ListaCanali.SelectedItem.ToString()!;
                ConfigManager.SalvaConfig(_config);
                await AggiornaStatoInterfaccia();
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            TxtImpostazioniPercorso.Text = _config.BaseGameFolder;
            PanelImpostazioni.Visibility = Visibility.Visible;
        }

        private void BtnChiudiSettings_Click(object sender, RoutedEventArgs e) => PanelImpostazioni.Visibility = Visibility.Hidden;

        private void ChkImpostazioni_Changed(object sender, RoutedEventArgs e)
        {
            if (_config == null) return;
            _config.StartWithWindows = ChkAutoStart.IsChecked ?? false;
            _config.RunInBackground = ChkBackground.IsChecked ?? false;
            _config.AllowCustomFile = ChkAllowCustom.IsChecked ?? false;
            ConfigManager.SalvaConfig(_config);
            ConfigManager.ImpostaAvvioAutomatico(_config.StartWithWindows, _config.RunInBackground);
            AggiornaVisibilitaCustom();
        }

        private void ApriLink(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        private void BtnYouTube_Click(object sender, RoutedEventArgs e) => ApriLink("https://www.youtube.com/@MrRevoTV");
        private void BtnDiscord_Click(object sender, RoutedEventArgs e) => ApriLink("https://discord.gg/W9xYAss9yE");
        private void BtnOrg_Click(object sender, RoutedEventArgs e) => ApriLink("https://robertsspaceindustries.com/en/orgs/ALSE");
        private void BtnHelp_Click(object sender, RoutedEventArgs e) => ApriLink("https://www.youtube.com/watch?v=TUO_VIDEO_TUTORIAL");
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) this.DragMove(); }

        private void ImpostaBottone(string testo, string coloreHex, bool abilitato = true)
        {
            BtnAction.Content = testo;
            BtnAction.Background = (Brush)(new BrushConverter().ConvertFrom(coloreHex) ?? Brushes.Transparent);
            BtnAction.IsEnabled = abilitato;
        }
    }
}