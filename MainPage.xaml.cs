using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Timers;
using Plugin.Maui.Audio;

namespace Mauordle
{
    /*
     * TODO:
     * -FIX CROSS PLATFORM SIZING
     * 
     * -implement colours DONE
     * -implement light/dark theme
     * -add type here animation
     * -add animation to letters when updating the squares
     * -add sounds when doing that
     * -add settings page DONE
     * -implement saving results
     * -add results page
     * -add window resized function DONE
     * -FIX font resizing not working when un-maximizing window
     */
    public partial class MainPage : ContentPage
    {
        //constants
        public const int WORD_NUMBER = 6;
        public const int WORD_LENGTH = 5;
        private const string WORDS_FILE_NAME = "words.txt";
        private const string URL = "https://raw.githubusercontent.com/DonH-ITS/jsonfiles/main/words.txt";
        private const int WORDS_FILE_LENGTH = 3103;

        //static variables
        public static MainPage Instance { get; private set; }

        //platform specific variables, default is android
        private double INITIAL_BORDER_WIDTH_MULTIPLIER = 0.08;
        private double INITIAL_ENTRY_WIDTH_MULTIPLIER = 0.3;
        private int WIN_FONT_SIZE = 50;

        //properties
        private Entry entry;
        private Border[][] borders; //i coudlve just used a grid but i didnt want to rewrite the whole layout but this probably takes up less memory so who cares
        private int wordNum;
        private List<string> words; 
        private string targetWord;
        private bool wordsFileExists = false;
        private bool wordDisplayCreated = false;
        private bool startFullscreen = false; //mainly for debugging purposes as this will always be true on any release version
        private bool won = false;
        private int guess = 0;
        private ObservableCollection<ObservableCollection<BindableChar>> wordsTyped;
        private IAudioPlayer metalPipeFalling;
        private IAudioPlayer pizzaTowerTaunt;
        private IAudioPlayer spongebob;
        private double volume;
        public double Volume
        {
            get => volume;
            set
            {
                if (volume != value)
                {
                    volume = value;
                    OnPropertyChanged(nameof(Volume));

                    Preferences.Set("volume", value);

                    if(metalPipeFalling != null)
                        metalPipeFalling.Volume = value;
                    if(pizzaTowerTaunt != null)
                        pizzaTowerTaunt.Volume = value;
                    if(spongebob != null)
                        spongebob.Volume = value;
                }
            }
        }
        private bool isUpdatingTyped = false;  //used for UpdateWordsDisplay
        public bool IsUpdatingTyped { 
            get => isUpdatingTyped;
            set
            {
                if (isUpdatingTyped != value)
                {
                    isUpdatingTyped = value;
                    OnPropertyChanged(nameof(IsUpdatingTyped));
                    OnPropertyChanged(nameof(IsNotUpdatingTyped));
                }
            }
        }
        public bool IsNotUpdatingTyped { get => !IsUpdatingTyped; } //used for entry data binding
        private string typed = "";
        public string Typed
        {
            get => typed;
            set
            {
                if (typed != value)
                {
                    typed = value;
                    OnPropertyChanged(nameof(Typed));
                }
            }
        }
        private bool updatingSize = false;

        /////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////

        public MainPage()
        {
            InitializeComponent();

#if (WINDOWS || MACCATALYST)
            INITIAL_BORDER_WIDTH_MULTIPLIER = 0.12;
            INITIAL_ENTRY_WIDTH_MULTIPLIER = 0.15;
            WIN_FONT_SIZE = 90;
#endif

            BindingContext = this;
            Instance = this;

            if(wordsTyped == null)
                PopulateWordsTypedCollection();

            mainVStack.Loaded += OnMainVSLoaded; //this does all the file writing and reading stuff to ultimately get the target word
            this.LayoutChanged += ResizeElements;

            wordNum = new Random().Next(WORDS_FILE_LENGTH);

            Volume = Preferences.Get("volume", 1.0);
        }

        private void CreateWordDisplay()
        {
            if (!wordDisplayCreated)
            {
                borders = new Border[WORD_NUMBER][];
                for (int i = 0; i < WORD_NUMBER; i++)
                {
                    borders[i] = new Border[WORD_LENGTH];
                    HorizontalStackLayout hsl = new HorizontalStackLayout
                    {
                        HorizontalOptions = LayoutOptions.Center
                    };
                    for (int j = 0; j < WORD_LENGTH; j++)
                    {
                        Label l = new Label();
                        l.Style = (Style)Resources["WordLabel"];

                        //maui flattens 2D ObservableCOllections when binding for some reason so i have to bind it to wordsTyped[i] directly
                        l.BindingContext = wordsTyped[i];
                        l.SetBinding(Label.TextProperty, new Binding($"[{j}].Value"));
                        
                        Border border = new Border
                        {
                            WidthRequest = this.Height * INITIAL_BORDER_WIDTH_MULTIPLIER,
                            HeightRequest = this.Height * INITIAL_BORDER_WIDTH_MULTIPLIER,
                            Content = l
                        };
                        border.Style = (Style)Resources["DefaultBorder"];

                        hsl.Add(border);
                        borders[i][j] = border;
                    }
                    mainVStack.Add(hsl);
                }
                entry = new Entry
                {
                    Margin = 20,
                    WidthRequest = this.Width * INITIAL_ENTRY_WIDTH_MULTIPLIER,
                    HorizontalOptions = LayoutOptions.Center
                };
                entry.SetBinding(Entry.TextProperty, new Binding { 
                    Path = "Typed",
                    Mode = BindingMode.TwoWay
                });
                entry.SetBinding(Entry.IsEnabledProperty, new Binding
                {
                    Path = "IsNotUpdatingTyped",
                });
                entry.TextChanged += Entry_TextChanged;

                /*
                Label test = new Label();
                test.SetBinding(Label.TextProperty, new Binding { Path = "Typed" });
                mainVStack.Add(test);
                */
                
                mainVStack.Add(entry);
                wordDisplayCreated = true;
            }
            //DoTypeHereAnimation(2);
        }

        private void Entry_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (IsUpdatingTyped)
            {
                IsUpdatingTyped = false;
                return;
            }

            Entry entry = (Entry)sender;
            string output = Regex.Replace(e.NewTextValue, "[^a-zA-Z]", "");

            Typed = output.ToUpper();

            if (Typed.Length == WORD_LENGTH)
            {
                IsUpdatingTyped = true;

                UpdateWordsDisplay(Typed, guess);

                won = CheckWin(); //CheckWin does its thing inside the method, the return val is just for the sound effect
                
                entry.Text = String.Empty;
                ++guess;

                if(!won && guess != WORD_NUMBER)
                    metalPipeFalling.Play();
            }

            if (guess >= WORD_NUMBER && !won)
            {
                entry.IsEnabled = false;
                spongebob.Play();
            }
        }

        private void PopulateWordsTypedCollection()
        {
            wordsTyped = new ObservableCollection<ObservableCollection<BindableChar>>();
            for (int i = 0; i < WORD_NUMBER; i++)
            {
                ObservableCollection<BindableChar> bogos = new ObservableCollection<BindableChar>();
                for (int j = 0; j < WORD_LENGTH; j++) {
                    bogos.Add(new BindableChar(' '));
                }
                wordsTyped.Add(bogos);
            }
        }

        private async void OnMainVSLoaded(object? sender, EventArgs e)
        {
            int i = 0;
            while (!wordsFileExists && i < 5) { 
                wordsFileExists = await GetWordsFile();
                ++i;
            }

            if (!wordsFileExists) {
                await Shell.Current.DisplayAlert("Failed to load words list.", "Ensure you are connected to the internet and try again.", "ok");
                Application.Current.Quit();
            }
            else
            {
                await ReadWords();
            }

            if (metalPipeFalling == null)
            {
                metalPipeFalling = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("metal-pipe-falling-sound-effect.mp3"));
                metalPipeFalling.Volume = Volume;
            }

            if (pizzaTowerTaunt == null)
            {
                pizzaTowerTaunt = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("pizza-tower-taunt.mp3"));
                pizzaTowerTaunt.Volume = Volume;
            }

            if (spongebob == null)
            {
                spongebob = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("spongebob-sad.mp3"));
                spongebob.Volume = Volume;
            }
            //Shell.Current.DisplayAlert("bogos", FileSystem.Current.AppDataDirectory, "ok");
            Label test2 = new Label { Text = targetWord };
            mainVStack.Add(test2);

        }

        private async Task<bool> GetWordsFile()
        {
            string path = Path.Combine(FileSystem.Current.AppDataDirectory, WORDS_FILE_NAME);
            if (File.Exists(path))
                return true;

            HttpClient client = new HttpClient();
            try
            {
                HttpResponseMessage response = await client.GetAsync(URL);
                if (response.IsSuccessStatusCode)
                {
                    using (StreamWriter writer = new StreamWriter(path))
                    {
                        string contents = await response.Content.ReadAsStringAsync();
                        await writer.WriteAsync(contents);
                    }
                    return true;
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error when requesting word data", response.RequestMessage.ToString(), "ok");
                    return false;
                }
            }
            catch (Exception ex) { 
                await Shell.Current.DisplayAlert("Error when reading word data", ex.Message, "ok");
                return false;
            }
        }

        private async Task ReadWords()
        {
            string path = Path.Combine(FileSystem.Current.AppDataDirectory, WORDS_FILE_NAME);
            words = new List<string>();
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    while (!reader.EndOfStream)
                    {
                        words.Add(reader.ReadLine());
                    }
                    if(words.Count != WORDS_FILE_LENGTH)
                    {
                        wordNum = new Random().Next(words.Count); //originally asigned in constructor
                    }
                    targetWord = words[wordNum].ToUpper();
                }
            }
            catch (Exception ex) {
                File.Delete(path);
                await Shell.Current.DisplayAlert("Failed to read words file", ex.Message, "ok");
                Application.Current.Quit();
            }

            if (targetWord.Length != WORD_LENGTH)
            {
                File.Delete(path);
                await Shell.Current.DisplayAlert("Something went wrong when writing the words", "Word length is not " + WORD_LENGTH, "ok");
                Application.Current.Quit();

            }
        }

        private void UpdateWordsDisplay(string str, int index)
        {
            Dictionary<char,int> charOccurrences = GetCharOccurrences(targetWord); //probably inefficient if it was a longer string but who cares

            for(int i = 0; i < WORD_LENGTH; ++i){
                wordsTyped[index][i].Value = str[i];

                if (str[i] == targetWord[i])
                {
                    borders[index][i].Style = (Style)Resources["CorrectBorder"];
                    --charOccurrences[str[i]];
                }
            }

            for (int i = 0; i < WORD_LENGTH; i++) {
                if (targetWord.Contains(str[i]))
                {
                    if (charOccurrences[str[i]] > 0 && str[i] != targetWord[i])
                    {
                        borders[index][i].Style = (Style)Resources["SemiCorrectBorder"];
                        --charOccurrences[str[i]];
                    }
                }
            }
        }

        private bool CheckWin()
        {
            if(typed == targetWord)
            {
                Label winLabel = new Label
                {
                    Text = "You Win!",
                    FontSize = WIN_FONT_SIZE,
                    HorizontalOptions = LayoutOptions.Center
                };
                mainVStack.Insert(0, winLabel);
                entry.IsEnabled = false;

                pizzaTowerTaunt.Play();

                return true;
            }

            return false;
        }

        private Dictionary<char, int> GetCharOccurrences(string str)
        {
            Dictionary<char, int> count = new Dictionary<char, int>();

            foreach (char c in str)
            {
                if (count.ContainsKey(c))
                    count[c]++;
                else
                    count[c] = 1;
            }
            return count;
        }

        //unused function
        /*
        private Dictionary<char,int> GetRepeatChars(string str)
        {
            Dictionary<char, int> count = GetCharOccurrences(str);

            Dictionary<char,int> repeatChars = new Dictionary<char,int>();
            
            foreach (KeyValuePair<char,int> k in count)
            {
                if(k.Value > 1)
                    repeatChars[k.Key] = k.Value;
            }

            return repeatChars;
        }
        */

        private void DoTypeHereAnimation(int iterations)
        {
            System.Timers.Timer timer = new System.Timers.Timer(); //fix
            int seqIndex = 0;
            int[][] sequence = {
                [1,5],
                [1,2,4,5],
                [2,4],
                [2,3,4],
                [2,3,4],
                [3]
            };

            timer.Elapsed += (s, e) =>
            {
                if (seqIndex > 0)
                {
                    for (int i = 0; i < sequence[(seqIndex - 1) % WORD_NUMBER].Length; ++i)
                    {
                        Dispatcher.Dispatch(() =>
                        {
                            borders[(seqIndex - 1) % WORD_NUMBER][i].Style = (Style)Resources["DefaultBorder"];
                        });
                        
                    }
                }
                for(int i = 0; i < sequence[seqIndex % WORD_NUMBER].Length; ++i)
                {
                    Dispatcher.Dispatch(() =>
                    {
                        borders[seqIndex % WORD_NUMBER][i].Style = (Style)Resources["AnimationBorder"];
                    });
                }

                ++seqIndex;

                if (seqIndex == iterations * WORD_NUMBER)
                {
                    timer.Stop();
                    return;
                }
            };

            timer.Start();

            
        }

        private async void OpenSettingsPage(object sender, EventArgs args)
        {
            await Navigation.PushAsync(new SettingsPage());
        }

        /*
        protected override void OnAppearing()
        {
            base.OnAppearing();
        }
        */

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            CreateWordDisplay();
            //wordsTyped[0][0].Value = 'b';
        }

        protected void ResizeElements(object sender, EventArgs e)
        {
            if (wordDisplayCreated && !updatingSize)
            {
                updatingSize = true;
                for (int i = 0; i < WORD_NUMBER; ++i)
                {
                    for (int j = 0; j < WORD_LENGTH; ++j)
                    {
                        borders[i][j].WidthRequest = this.Width * .072;
                        borders[i][j].HeightRequest = this.Width * .072;

                        Label l = (Label)borders[i][j].Content;
                        int lFontSize = (int)Math.Round(borders[i][j].Width * 0.5);
                        l.FontSize = lFontSize; //FIX THIS NOT SIZING PROPERLY WHEN UN-MAXIMIZING WINDOW
                    }
                }

                entry.WidthRequest = this.Width * .08;
                updatingSize = false;
            }
        }

        //from: https://stackoverflow.com/questions/76881580/net-6-maui-open-application-in-full-screen-mode-windows
        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
#if WINDOWS
            if(startFullscreen){
                  var window = App.Current.Windows.FirstOrDefault().Handler.PlatformView as Microsoft.UI.Xaml.Window;
                  IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                  Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
                  Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                 appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                 //(appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter).Maximize();
            // this line can maximize the window
            }
#endif
        }

    }

}
