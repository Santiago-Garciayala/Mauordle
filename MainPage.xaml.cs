using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Mauordle
{
    /*
     * TODO:
     * -implement colours
     * -implement light/dark theme
     * -add type here animation
     * -add animation to letters when updating the squares
     * -add sounds when doing that
     * -add settings page
     * -implement saving results
     * -add results page
     */
    public partial class MainPage : ContentPage
    {
        public const int WORD_NUMBER = 6;
        public const int WORD_LENGTH = 5;
        private const string WORDS_FILE_NAME = "words.txt";
        private const string URL = "https://raw.githubusercontent.com/DonH-ITS/jsonfiles/main/words.txt";
        private const int WORDS_FILE_LENGTH = 3103;

        private Entry entry;
        private Border[][] borders; //i coudlve just used a grid but i didnt want to rewrite the whole layout but this probably takes up less memory so who cares
        private int wordNum;
        private List<string> words; 
        private string targetWord;
        private bool wordsFileExists = false;
        private bool wordDisplayCreated = false;
        private bool startFullscreen = false; //mainly for debugging purposes as this will always be true on any release version
        private int guess = 0;
        private ObservableCollection<ObservableCollection<BindableChar>> wordsTyped;
        private bool isUpdatingTyped = false;  //used for UpdateWords
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

        public MainPage()
        {
            InitializeComponent();

            BindingContext = this;
            PopulateWordsTypedCollection();
            
            wordNum = new Random().Next(WORDS_FILE_LENGTH);
        }

        private void CreateWordDisplay()
        {
            if (!wordDisplayCreated)
            {
                borders = new Border[WORD_NUMBER][];
                for (int i = 0; i < 6; i++)
                {
                    borders[i] = new Border[WORD_LENGTH];
                    HorizontalStackLayout hsl = new HorizontalStackLayout
                    {
                        HorizontalOptions = LayoutOptions.Center
                    };
                    for (int j = 0; j < 5; j++)
                    {
                        Label l = new Label();
                        l.Style = (Style)Resources["WordLabel"];

                        //maui flattens 2D ObservableCOllections when binding for some reason so i have to bind it to wordsTyped[i] directly
                        l.BindingContext = wordsTyped[i];
                        l.SetBinding(Label.TextProperty, new Binding($"[{j}].Value"));
                        
                        Border border = new Border
                        {
                            WidthRequest = this.Height * .12,
                            HeightRequest = this.Height * .12,
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
                    WidthRequest = this.Width * .15,
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
                CheckWin();
                entry.Text = String.Empty;
                ++guess;
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

        private void CheckWin()
        {
            if(typed == targetWord)
            {
                Label winLabel = new Label
                {
                    Text = "You Win!",
                    FontSize = 90,
                    HorizontalOptions = LayoutOptions.Center
                };
                mainVStack.Insert(0, winLabel);
                entry.IsEnabled = false;
            }
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



        protected override void OnAppearing()
        {
            base.OnAppearing();

            //this does all the file writing and reading stuff to ultimately get the target word
            mainVStack.Loaded += OnMainVSLoaded;
        }
        

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            CreateWordDisplay();
            //wordsTyped[0][0].Value = 'b';
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
